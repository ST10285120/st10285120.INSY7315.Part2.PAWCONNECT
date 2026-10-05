# Free hosting: Render + Neon

This is the no-cost alternative to the Azure setup in [DEPLOYMENT.md](DEPLOYMENT.md). It uses the
same Docker image, the same database migrations and the same GitHub Actions pipeline (CI, manual
approval, smoke tests). Only the hosting target changes.

| Part | Service (free plan) | Notes |
|---|---|---|
| Web app (production + staging) | [Render](https://render.com) web services, Docker | Sleeps after 15 minutes without traffic; the first request then takes about a minute |
| Database | [Neon](https://neon.tech) serverless PostgreSQL 16 | One project with two branches: `main` (production) and `staging` |
| CI/CD | GitHub Actions (`deploy.yml`) | Builds and tests, then calls Render's deploy hook and waits for the new commit to go live |

> **Which to use for marks?** The rubric's top band asks for a stable, live deployment with a rationale. Azure
> matches the Task 1 design document (App Service, Key Vault, blue-green slots). Use it if you can, even if only
> for the demo weeks; the free **F1** plan is an option too (`--sku F1`, see DEPLOYMENT.md). Render + Neon is the
> fallback when you can't use Azure at all. Either way, explain the choice in the presentation.

> **POPIA.** Neon and Render host in Europe (choose Frankfurt), not South Africa. For a real shelter that means
> personal information leaves the country, which POPIA section 72 allows only with adequate protection or consent.
> For this assessment, use the demo data only.

## 1. Database: Neon

1. Sign up at neon.tech (GitHub login is fine) and **create a project**: PostgreSQL 16, region **AWS Europe Central (Frankfurt)**.
2. Neon creates a `main` branch with a database. Under **Branches**, create a branch called `staging` from `main`.
3. For each branch, open **Connect** and copy the connection string. Use the **direct** connection, not the
   *pooled* one (untick "Connection pooling"): EF Core migrations and Npgsql's own pooling work best with a
   direct connection. It looks like:
   `postgresql://pawconnect_owner:…@ep-xxxx.eu-central-1.aws.neon.tech/neondb?sslmode=require`

PawConnect accepts this URL format as it is (`PostgresConnectionString` converts it), so paste it unchanged.

## 2. Web app: Render

1. Push the repository to GitHub (with `render.yaml` at the root).
2. In Render: **New → Blueprint**, connect GitHub, pick the repository. Render reads `render.yaml` and proposes two
   free Docker services: `pawconnect` (branch `main`) and `pawconnect-staging` (branch `develop`).
3. Fill in the values Render asks for:

   | Service | Variable | Value |
   |---|---|---|
   | pawconnect | `ConnectionStrings__PawConnect` | Neon **main** branch URL |
   | pawconnect | `Seed__AdminEmail` | your email (the first administrator) |
   | pawconnect | `Seed__AdminPassword` | a strong password (8+ characters, upper, lower, digit) |
   | pawconnect-staging | `ConnectionStrings__PawConnect` | Neon **staging** branch URL |
   | pawconnect-staging | `Seed__DemoPassword` | `Demo!Paws2026` (the demo password in the README) |

4. Click **Apply**. The first build takes about 5–8 minutes. On start-up the app runs the migrations, creates the
   roles and the administrator, and (staging only) loads the demo data.
5. Check both URLs: `https://<service>.onrender.com/health` should say `Healthy`, and `/version` shows the commit.

`autoDeploy` is off in `render.yaml` on purpose: Render must not deploy a commit that hasn't passed CI. GitHub
Actions triggers each deploy instead.

## 3. Connect the pipeline

1. In Render, open each service → **Settings → Deploy Hook** and copy the URL.
2. In GitHub → **Settings → Secrets and variables → Actions**:

   | Kind | Name | Value |
   |---|---|---|
   | Variable | `HOSTING_PLATFORM` | `render` (switches `deploy.yml` from Azure to Render) |
   | Variable | `RENDER_PRODUCTION_URL` | `https://pawconnect-xxxx.onrender.com` |
   | Variable | `RENDER_STAGING_URL` | `https://pawconnect-staging-xxxx.onrender.com` |
   | Secret | `RENDER_DEPLOY_HOOK_PRODUCTION` | the production service's deploy hook |
   | Secret | `RENDER_DEPLOY_HOOK_STAGING` | the staging service's deploy hook |

3. **Settings → Environments**: create `staging` and `production`; on `production` add **Required reviewers**
   (the manual approval gate).

Now the flow is the same as on Azure:

```
push to develop → CI (build, 132 tests, coverage gate, scans, accessibility) → deploy hook → wait for the
                  new commit on /version → smoke test staging
merge to main   → CI → manual approval → deploy hook → wait for /version → smoke test production
```

Render only switches traffic to a new build once its `/health` check passes, so a broken build never replaces
the running version. To roll back: Render → service → **Events** → an earlier deploy → **Rollback**.

## 4. Demo week: keep production awake (optional)

Free services sleep after 15 minutes idle. Set the repository variable `KEEP_AWAKE` to `true` and
`.github/workflows/keep-awake.yml` pings production every 10 minutes between 07:00 and 23:00 SAST, so it's
awake when your lecturer opens it. That uses roughly 500 of Render's 750 free instance hours a month; turn it
off again afterwards.

## Troubleshooting

| Symptom | Fix |
|---|---|
| First request takes ~1 minute | The free service was asleep. Expected; use `KEEP_AWAKE` during the demo |
| `Database` unhealthy on `/health` | Check the Neon URL in Render (no line breaks, `sslmode=require` kept). Neon also suspends idle databases; the first query wakes it in a second or two |
| Deploy job times out waiting for the commit | Render build failed: open the service's **Events/Logs**. The job waits up to 20 minutes |
| Pipeline still deploys to Azure | The variable must be exactly `HOSTING_PLATFORM` = `render` |
| Sign-in works, then you're signed out after a deploy | It shouldn't happen: sign-in keys are stored in the database. Check both services point at the right Neon branch |
