#!/usr/bin/env bash
# Prints what a deployed PawConnect app is answering, so a failed deploy explains itself in the
# GitHub Actions log. Never fails the job by itself (the step that failed already did).
# Usage: scripts/diagnose-deploy.sh <base-url> <app-name> <use-slots> <deploy-auth>
BASE_URL="${1:-}"; APP="${2:-}"; SLOTS="${3:-}"; AUTH="${4:-}"

echo "::group::Deployment settings"
echo "Target URL:            ${BASE_URL:-<empty>}"
echo "App name:              ${APP:-<empty>}"
echo "USE_DEPLOYMENT_SLOTS:  ${SLOTS:-<not set, treated as true>}"
echo "AZURE_DEPLOY_AUTH:     ${AUTH:-<not set, OpenID Connect>}"
[ -z "$BASE_URL" ] && echo "::warning::The production URL is empty. Set the AZURE_WEBAPP_URL repository variable."
[ "$AUTH" = "publish-profile" ] && [ "$SLOTS" != "false" ] && \
  echo "::warning::Publish-profile deploys need USE_DEPLOYMENT_SLOTS = 'false'."
echo "::endgroup::"

[ -z "$BASE_URL" ] && exit 0

for path in /version /health /; do
  echo "::group::GET $path"
  curl -sS -i --max-time 30 "$BASE_URL$path" 2>&1 | head -c 2000
  echo
  echo "::endgroup::"
done

cat <<'TIPS'
How to read this:
  * Connection refused / timeout / 503 on every path: the app is not starting. Open the web app in the
    Azure portal → Log stream (or Diagnose and solve problems) and look for the startup exception.
    The usual causes are a missing ConnectionStrings__PawConnect setting or a Key Vault reference
    the app's managed identity cannot read.
  * /version answers but with an older commit: the package went to a different app or slot than the
    URL being checked. Compare AZURE_WEBAPP_NAME with AZURE_WEBAPP_URL.
  * /health answers "Unhealthy": the app runs but cannot reach PostgreSQL (firewall rule or password).
  * 404 from Azure's own page: the URL variable points at a web app that does not exist.
TIPS
exit 0
