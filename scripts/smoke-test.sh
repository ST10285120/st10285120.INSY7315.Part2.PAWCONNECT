#!/usr/bin/env bash
# Smoke test used by the deployment pipeline: checks the key endpoints respond correctly.
# Usage: scripts/smoke-test.sh https://your-app.azurewebsites.net
set -euo pipefail
BASE_URL="${1:?Usage: smoke-test.sh <base-url>}"

check() {
  local path="$1" expected="$2" attempt=1
  # App Service can take a minute to warm up after a deployment, so retry for up to ~3 minutes.
  while [ $attempt -le 18 ]; do
    status=$(curl -s -o /tmp/smoke-body -w "%{http_code}" --max-time 20 "$BASE_URL$path" || echo "000")
    if [ "$status" = "$expected" ]; then
      echo "OK   $path -> $status"
      return 0
    fi
    echo "wait $path -> $status (attempt $attempt)"
    attempt=$((attempt + 1))
    sleep 10
  done
  echo "FAIL $path: expected $expected, last got $status"
  return 1
}

check "/health" 200
grep -q "Healthy" /tmp/smoke-body || { echo "FAIL /health did not report Healthy"; exit 1; }
check "/" 200
check "/Home/Browse" 200
check "/api/animals" 200
check "/Admin/Dashboard" 302   # admin area must redirect anonymous users to the login page
echo "Smoke test passed for $BASE_URL"
