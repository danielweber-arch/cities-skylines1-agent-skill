#!/usr/bin/env bash
#
# Port of smoke-test.ps1: read-only smoke test of the Skylines Agent Bridge. Hits /health,
# /state/summary, /state/problems, /prefabs/roads, then sends a dry-run build-road and a
# dry-run batch command. Nothing is built.
#
# Usage: ./scripts/smoke-test.sh [--base-url URL]
#
#   --base-url URL   bridge base URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
#   -h, --help       show this help
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"

usage() { awk 'NR <= 2 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0" | sed '${/^$/d;}'; }
die() { echo "error: $*" >&2; exit 1; }

while [ $# -gt 0 ]; do
    case "$1" in
        --base-url) [ $# -ge 2 ] || { usage >&2; exit 2; }; BASE="$2"; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done
BASE="${BASE%/}"

command -v jq >/dev/null 2>&1 || die "jq is required: brew install jq"

api_get()  { curl -sS --fail-with-body --max-time 30  "${BASE}$1"; }
api_post() { echo "POST $1 $2" >&2; curl -sS --fail-with-body --max-time 180 -X POST -H 'Content-Type: application/json' --data "$2" "${BASE}$1"; }

echo "Checking $BASE/health"
api_get "/health" | jq .

echo "Checking $BASE/state/summary"
api_get "/state/summary" | jq .

echo "Checking $BASE/state/problems"
api_get "/state/problems?limit=20" | jq .

echo "Checking $BASE/prefabs/roads"
api_get "/prefabs/roads" | jq .

echo "Checking dry-run road command"
road=$(jq -n '{
    dryRun: true,
    roadPrefab: "Basic Road",
    start: { x: 0, z: 0 },
    end: { x: 80, z: 0 },
    name: "Agent Smoke Test Road"
}')
api_post "/commands/build-road" "$road" | jq .

echo "Checking dry-run batch command"
batch=$(jq -n '{
    dryRun: true,
    stopOnError: true,
    commands: [
        {
            type: "build-road",
            roadPrefab: "Basic Road",
            start: { x: 120, z: 0 },
            end: { x: 200, z: 0 },
            name: "Agent Batch Smoke Road"
        },
        {
            type: "set-zone",
            zone: "ResidentialLow",
            center: { x: 160, z: 0 },
            radius: 48
        }
    ]
}')
api_post "/commands/batch" "$batch" | jq .
