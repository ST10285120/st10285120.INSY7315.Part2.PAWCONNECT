#!/usr/bin/env bash
# One-command Azure + GitHub setup for PawConnect.
#
# Creates every Azure resource (infra/main.bicep), a passwordless OpenID Connect identity for
# GitHub Actions, and, if the GitHub CLI is installed and logged in, the repository secrets,
# variables and environments the deploy pipeline needs.
#
# Usage:
#   az login                 # and optionally: gh auth login
#   ./scripts/azure-setup.sh --app pawconnect-st10285120 --repo myuser/PawConnect \
#       --email me@example.com [--sku S1|B1|F1] [--location southafricanorth]
#
#   --sku S1  (default) deployment slots, blue-green swaps. About US$70/month: fine for a few weeks on
#             the US$100 Azure for Students credit, then delete or scale down.
#   --sku B1  about US$13/month, no slots: staging is a second web app on the same plan.
#   --sku F1  FREE plan (60 CPU minutes/day, sleeps after ~20 min idle), no slots, same staging setup.
#             PostgreSQL B1ms is free for 12 months on a new Azure free / Students account.
#
# You'll be asked for two passwords (PostgreSQL admin and the first PawConnect admin). They
# go straight into Azure Key Vault and are never written to disk or GitHub. Staging uses the
# public demo password from the README (DEMO_PASSWORD to override), never the admin password.
set -euo pipefail

APP=""; REPO=""; EMAIL=""; SKU="S1"; LOCATION="southafricanorth"; RG=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --app) APP="$2"; shift 2 ;;
    --repo) REPO="$2"; shift 2 ;;
    --email) EMAIL="$2"; shift 2 ;;
    --sku) SKU="$2"; shift 2 ;;
    --location) LOCATION="$2"; shift 2 ;;
    --resource-group) RG="$2"; shift 2 ;;
    -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done
[[ -z "$APP" || -z "$REPO" || -z "$EMAIL" ]] && { echo "Required: --app, --repo and --email (see --help)"; exit 1; }
case "$SKU" in F1|B1|S1|P0v3) ;; *) echo "--sku must be F1, B1, S1 or P0v3"; exit 1 ;; esac
DEMO_PASSWORD="${DEMO_PASSWORD:-Demo!Paws2026}"
RG="${RG:-rg-$APP}"
command -v az >/dev/null || { echo "Install the Azure CLI first: https://aka.ms/installazurecli"; exit 1; }
az account show >/dev/null 2>&1 || { echo "Run 'az login' first."; exit 1; }

read -r -s -p "PostgreSQL admin password (12+ chars, mixed case, digit, symbol): " PG_PASSWORD; echo
read -r -s -p "First PawConnect admin password (8+ chars, upper, lower, digit): " ADMIN_PASSWORD; echo

SUB=$(az account show --query id -o tsv)
TENANT=$(az account show --query tenantId -o tsv)
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "==> 1/4 Resource group $RG in $LOCATION"
az group create --name "$RG" --location "$LOCATION" --output none

echo "==> 2/4 Deploying infrastructure (App Service, PostgreSQL, Key Vault, monitoring). This takes 5-10 minutes..."
az deployment group create \
  --resource-group "$RG" \
  --template-file "$SCRIPT_DIR/../infra/main.bicep" \
  --parameters appName="$APP" appServiceSku="$SKU" \
               postgresAdminPassword="$PG_PASSWORD" adminEmail="$EMAIL" \
               adminPassword="$ADMIN_PASSWORD" demoPassword="$DEMO_PASSWORD" alertEmail="$EMAIL" \
  --output none
WEB_URL=$(az deployment group show -g "$RG" -n main --query properties.outputs.webAppUrl.value -o tsv)
STAGING_URL=$(az deployment group show -g "$RG" -n main --query properties.outputs.stagingUrl.value -o tsv)

echo "==> 3/4 GitHub Actions identity (OpenID Connect, no stored passwords)"
CLIENT_ID=$(az ad app list --display-name "$APP-github" --query "[0].appId" -o tsv)
if [[ -z "$CLIENT_ID" ]]; then
  CLIENT_ID=$(az ad app create --display-name "$APP-github" --query appId -o tsv)
  az ad sp create --id "$CLIENT_ID" --output none
fi
az role assignment create --assignee "$CLIENT_ID" --role "Website Contributor" \
  --scope "/subscriptions/$SUB/resourceGroups/$RG" --output none 2>/dev/null || true
for SUBJECT in "environment:staging" "environment:production" "ref:refs/heads/main" "ref:refs/heads/develop"; do
  NAME="gh-$(echo "$SUBJECT" | tr ':/' '--')"
  az ad app federated-credential create --id "$CLIENT_ID" --output none --parameters "{
    \"name\": \"$NAME\", \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$REPO:$SUBJECT\", \"audiences\": [\"api://AzureADTokenExchange\"] }" 2>/dev/null || true
done

USE_SLOTS=true
[[ "$SKU" == "B1" || "$SKU" == "F1" ]] && USE_SLOTS=false

echo "==> 4/4 GitHub repository settings"
if command -v gh >/dev/null && gh auth status >/dev/null 2>&1; then
  gh secret set AZURE_CLIENT_ID --repo "$REPO" --body "$CLIENT_ID"
  gh secret set AZURE_TENANT_ID --repo "$REPO" --body "$TENANT"
  gh secret set AZURE_SUBSCRIPTION_ID --repo "$REPO" --body "$SUB"
  gh variable set AZURE_WEBAPP_NAME --repo "$REPO" --body "$APP"
  gh variable set AZURE_RESOURCE_GROUP --repo "$REPO" --body "$RG"
  gh variable set USE_DEPLOYMENT_SLOTS --repo "$REPO" --body "$USE_SLOTS"
  [[ "$USE_SLOTS" == "false" ]] && gh variable set AZURE_WEBAPP_NAME_STAGING --repo "$REPO" --body "$APP-staging"
  gh api --method PUT "repos/$REPO/environments/staging" --silent
  gh api --method PUT "repos/$REPO/environments/production" --silent
  echo "    Secrets, variables and environments set on $REPO."
  echo "    Now add yourself as a Required reviewer: GitHub > Settings > Environments > production."
else
  echo "    GitHub CLI not available. Add these in GitHub > Settings > Secrets and variables > Actions:"
  echo "      secret   AZURE_CLIENT_ID       = $CLIENT_ID"
  echo "      secret   AZURE_TENANT_ID       = $TENANT"
  echo "      secret   AZURE_SUBSCRIPTION_ID = $SUB"
  echo "      variable AZURE_WEBAPP_NAME     = $APP"
  echo "      variable AZURE_RESOURCE_GROUP  = $RG"
  echo "      variable USE_DEPLOYMENT_SLOTS  = $USE_SLOTS"
  [[ "$USE_SLOTS" == "false" ]] && echo "      variable AZURE_WEBAPP_NAME_STAGING = $APP-staging"
  echo "    Then create environments 'staging' and 'production' (with Required reviewers)."
fi

if [[ "$USE_SLOTS" == "false" ]]; then
  echo
  echo "NOTE: the $SKU plan has no deployment slots, so staging is the separate web app $APP-staging"
  echo "(already created) and production deploys go straight to $APP after the approval gate."
fi

echo
echo "Done. Production URL: $WEB_URL"
echo "      Staging URL:    $STAGING_URL"
echo "Push to 'develop' to deploy to staging; merge develop into 'main' to go live."
