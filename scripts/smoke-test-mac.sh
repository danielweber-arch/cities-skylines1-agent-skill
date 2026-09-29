#!/usr/bin/env bash
# Read-only checks against a running CS1 city with the bridge mod enabled (macOS/Linux).
set -euo pipefail

base="${BASE_URL:-http://127.0.0.1:32123}"

check() {
    echo "Checking $base$1"
    curl -fsS "$base$1"
    echo
}

check "/health"
check "/state/summary"
check "/state/problems?limit=20"
check "/state/economy"
check "/state/saves"

echo "Checking dry-run road command"
curl -fsS -X POST "$base/commands/build-road" \
    -H "Content-Type: application/json" \
    -d '{"dryRun":true,"roadPrefab":"Basic Road","start":{"x":0,"z":0},"end":{"x":80,"z":0},"name":"Agent Smoke Test Road"}'
echo
