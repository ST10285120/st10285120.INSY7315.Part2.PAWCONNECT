#!/usr/bin/env bash
# Waits until a deployed app reports the expected git commit (or a newer commit on the same
# branch) on /version. Render builds take a few minutes.
# Usage: scripts/wait-for-version.sh https://pawconnect.onrender.com <commit-sha> [timeout-seconds] [exact]
#   exact: only this commit counts (used before a production slot swap).
set -euo pipefail
BASE_URL="${1:?base url}"; EXPECTED="${2:?commit sha}"; TIMEOUT="${3:-1200}"; MODE="${4:-}"
DEADLINE=$(( $(date +%s) + TIMEOUT ))

# True when the live commit already contains the expected one (someone pushed again meanwhile).
contains_expected() {
  local live="$1"
  git rev-parse --git-dir >/dev/null 2>&1 || return 1
  git cat-file -e "$live^{commit}" 2>/dev/null || git fetch -q origin "$live" 2>/dev/null || return 1
  git merge-base --is-ancestor "$EXPECTED" "$live" 2>/dev/null
}

while [ "$(date +%s)" -lt "$DEADLINE" ]; do
  CURRENT=$(curl -s --max-time 30 "$BASE_URL/version" | sed -n 's/.*"commit":"\([^"]*\)".*/\1/p' || true)
  if [ "$CURRENT" = "$EXPECTED" ]; then
    echo "Commit $EXPECTED is live on $BASE_URL"
    exit 0
  fi
  if [ "$MODE" != "exact" ] && [ -n "$CURRENT" ] && [ "$CURRENT" != "unknown" ] && contains_expected "$CURRENT"; then
    echo "Newer commit $CURRENT (which includes $EXPECTED) is live on $BASE_URL"
    exit 0
  fi
  echo "Waiting: live commit is '${CURRENT:-none}', expecting $EXPECTED"
  sleep 20
done
echo "Timed out waiting for $EXPECTED on $BASE_URL"
exit 1
