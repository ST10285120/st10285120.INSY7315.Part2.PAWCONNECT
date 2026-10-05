# Deploying PawConnect to Azure

This guide takes you from an empty Azure subscription to a live PawConnect site with a staging slot and the GitHub Actions pipeline. It takes roughly 30–45 minutes the first time.

> **Cost note.** Pick the App Service plan with `--sku` (the template and pipeline support all of them):
>
> | Plan | Approx. cost | Staging | Production release |
> |---|---|---|---|
> | **S1** (default) | ~US$70/month | `staging` deployment slot | **blue-green slot swap** with automatic swap-back, as in the design doc |
> | **B1** | ~US$13/month | separate `<app>-staging` web app on the same plan | deployed directly after approval, then health-checked |
> | **F1** | **free** (60 CPU min/day, sleeps when idle, no custom-domain TLS) | separate `<app>-staging` web app | as B1 |
>
> PostgreSQL Flexible Server Burstable **B1ms** is free for 12 months on a new Azure free or Azure for Students
> account (750 hours/month + 32 GB); check *Cost Management* in the portal. Azure for Students gives US$100 credit
> with no card. A good student plan: **F1 or B1 while building, S1 for the demo week** (to show the blue-green swap),
> then scale down. Prices change; check the Azure pricing calculator. No Azure at all? See
> [DEPLOYMENT-FREE.md](DEPLOYMENT-FREE.md) (Render + Neon).

## Quickest path: one command

```bash
az login
gh auth login          # optional: lets the script set the GitHub secrets for you
./scripts/azure-setup.sh --app pawconnect-<student-number> --repo <user>/<repo> --email <you@example.com> [--sku S1|B1|F1]
```

The script does steps 1 and 2 below for you. Then add yourself as a required reviewer on the `production` environment and go to step 3. The manual steps are kept here so you can explain each one in the presentation.

## 0. Prerequisites
- An Azure subscription (Azure for Students works) and the [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli): `az login`
- Your code pushed to a GitHub repository

## 1. Create the Azure resources (infrastructure as code)

```bash
az group create --name rg-pawconnect --location southafricanorth

az deployment group create \
  --resource-group rg-pawconnect \
  --template-file infra/main.bicep \
  --parameters appName=pawconnect-<your-student-number> \
               appServiceSku=S1 \
               postgresAdminPassword='<a strong password>' \
               adminEmail='<your email>' \
               adminPassword='<a strong password for the PawConnect admin>' \
               demoPassword='Demo!Paws2026' \
               alertEmail='<your email>'
```

This creates:

| Resource | Purpose |
|---|---|
| App Service plan + web app + staging (a `staging` slot on S1/P0v3, or a second `<app>-staging` web app on F1/B1) | Runs PawConnect on .NET 8 Linux, HTTPS only, TLS 1.2+, health check on `/health` |
| PostgreSQL Flexible Server (B1ms) with `pawconnect` and `pawconnect_staging` databases | Data, with 7-day backups and point-in-time restore |
| Key Vault | Connection strings, the production admin password and the (separate) staging demo password. The apps read them with their managed identities, so there are no secrets in code or GitHub. Production-only and staging-only settings are *sticky*, so a slot swap never moves them |
| Application Insights + Log Analytics | Request latency, failures and logs |
| Alert rules + action group | Email when server errors spike or average response time exceeds 1 s over 5 minutes |

> If your subscription blocks the region (Azure for Students has an allowed-regions policy), rerun with a region the error message lists, e.g. `--location westeurope`, and note the POPIA implication in your report.

When it finishes, the output shows `webAppUrl`. The app isn't deployed yet, so the site shows a placeholder.

## 2. Let GitHub Actions deploy (OpenID Connect, no stored passwords)

```bash
SUB=$(az account show --query id -o tsv)
TENANT=$(az account show --query tenantId -o tsv)
REPO="<github-user-or-org>/<repo-name>"

# An identity for the pipeline, allowed to manage only this resource group
APP_ID=$(az ad app create --display-name "pawconnect-github" --query appId -o tsv)
az ad sp create --id $APP_ID
az role assignment create --assignee $APP_ID --role "Website Contributor" \
  --scope /subscriptions/$SUB/resourceGroups/rg-pawconnect

# Trust GitHub Actions runs from this repo's main and develop branches
for BRANCH in main develop; do
  az ad app federated-credential create --id $APP_ID --parameters "{
    \"name\": \"github-$BRANCH\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$REPO:ref:refs/heads/$BRANCH\",
    \"audiences\": [\"api://AzureADTokenExchange\"] }"
done
# The deploy jobs use GitHub environments, so trust those too
for ENV in staging production; do
  az ad app federated-credential create --id $APP_ID --parameters "{
    \"name\": \"github-env-$ENV\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$REPO:environment:$ENV\",
    \"audiences\": [\"api://AzureADTokenExchange\"] }"
done
echo "AZURE_CLIENT_ID=$APP_ID  AZURE_TENANT_ID=$TENANT  AZURE_SUBSCRIPTION_ID=$SUB"
```

> If your university tenant doesn't let you create app registrations, ask your lecturer, or use the portal: **Web app → Deployment Center → GitHub** creates the credentials for you. You would then do the slot swap manually in **Deployment slots → Swap**.

In GitHub → **Settings → Secrets and variables → Actions**:

| Type | Name | Value |
|---|---|---|
| Secret | `AZURE_CLIENT_ID` | from the output above |
| Secret | `AZURE_TENANT_ID` | from the output above |
| Secret | `AZURE_SUBSCRIPTION_ID` | from the output above |
| Variable | `AZURE_WEBAPP_NAME` | e.g. `pawconnect-st10285120` |
| Variable | `AZURE_RESOURCE_GROUP` | `rg-pawconnect` |
| Variable | `USE_DEPLOYMENT_SLOTS` | `true` (S1/P0v3) or `false` (F1/B1) |
| Variable | `AZURE_WEBAPP_NAME_STAGING` | only for F1/B1: `<app>-staging` (created by `infra/main.bicep`) |

Then in **Settings → Environments** create `staging` and `production`. On **production**, tick **Required reviewers** and add yourself or your team leader. This is the manual approval gate before going live.

### No permission to create the GitHub identity?
University Azure accounts often block **Microsoft Entra ID → App registrations** ("You don't have access"). Use publish profiles instead (F1/B1, no slots):
1. For **each** web app (production and staging): **Settings → Configuration → General settings** → turn **SCM Basic Auth Publishing Credentials** **On** → **Save**. Then **Overview → Download publish profile**.
2. In GitHub, add secrets `AZURE_PUBLISH_PROFILE` (production file's full contents) and `AZURE_PUBLISH_PROFILE_STAGING` (staging file's contents), and the variable `AZURE_DEPLOY_AUTH` = `publish-profile` (plus `AZURE_WEBAPP_NAME`, `AZURE_WEBAPP_NAME_STAGING`, `USE_DEPLOYMENT_SLOTS` = `false`).
3. Keep the downloaded files private and delete them afterwards: they are passwords.

## 3. Deploy

```bash
git checkout develop && git push     # → CI → staging slot → smoke test
# open a PR develop → main, merge it   → CI → approval → staging → swap → health check
```

On first start the app runs the EF Core migrations, creates the roles and branch, and creates the administrator from `Seed:AdminEmail` / the Key Vault password. Staging loads the demo data instead, with the demo password from the README, never the production admin password, so it's safe to give staging logins to your lecturer.

## 4. Verify
- `https://<app>.azurewebsites.net/health` → `Healthy`
- Sign in with your admin email, then go to **Users** and create volunteer accounts.
- Application Insights → **Live metrics** while you click around.

## Optional: real email
Create an **Azure Communication Services Email** resource, or use any SMTP provider, then set these app settings. Store the password in Key Vault and reference it like the others:

```
Notifications__Email__SmtpHost     = smtp.azurecomm.net
Notifications__Email__SmtpPort     = 587
Notifications__Email__UserName     = <ACS SMTP username>
Notifications__Email__Password     = @Microsoft.KeyVault(VaultName=<vault>;SecretName=SmtpPassword)
Notifications__Email__FromAddress  = DoNotReply@<your-verified-domain>
```

Without these settings, emails are written to the log and to the `Notifications` table instead of being sent. That's fine for demos.

## Troubleshooting
| Symptom | Fix |
|---|---|
| 500.30 / app won't start | App Service → **Log stream**. Usually the Key Vault reference hasn't resolved yet (wait a minute after deployment, or check the managed identity has *Key Vault Secrets User*) |
| 500.30 and you can't see why | **Advanced Tools → Go → Debug console → CMD**, then `cd site\wwwroot` and `dotnet PawConnect.Web.dll` (Windows plan) prints the real start-up error. "Address already in use" there means the app itself started fine |
| `Failed to connect to <ip>:5432 … timed out` | PostgreSQL → **Networking**: add firewall rules for the web app's **Outbound IP addresses** (web app → Properties), then **Stop → Start** the app |
| Deploy uploads, then "Wait until this build is running" times out | The app's address isn't `<name>.azurewebsites.net`: copy the **Default domain** from the app's Overview into the variables `AZURE_WEBAPP_URL` / `AZURE_WEBAPP_URL_STAGING` (with `https://`) |
| `Database` unhealthy | PostgreSQL → Networking: *Allow public access from any Azure service* must be on, or the app's outbound IPs added as firewall rules |
| Slot swap step fails | You're on F1 or B1: set `USE_DEPLOYMENT_SLOTS=false` and `AZURE_WEBAPP_NAME_STAGING=<app>-staging` (the setup script does this), or scale the plan to S1 |
| F1: site slow or "quota exceeded" | The free plan sleeps when idle and has 60 CPU minutes a day. Scale up to B1/S1 for the demo |
| Login fails in the pipeline | Check the federated credential subjects match your repo name and branch or environment exactly |
