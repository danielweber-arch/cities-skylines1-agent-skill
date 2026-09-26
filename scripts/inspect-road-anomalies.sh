#!/usr/bin/env bash
#
# Port of inspect-road-anomalies.ps1: print /state/road-anomalies and a repair hint per
# near-miss dead end, short stub and plain dead end. Read-only.
#
# Usage: ./scripts/inspect-road-anomalies.sh [--base-url URL] [--near-miss-distance M]
#            [--short-segment-length M] [--exclude-dead-ends]
#
#   --base-url URL              bridge base URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
#   --near-miss-distance M      dead-end-to-road distance that counts as a near miss (default: 18)
#   --short-segment-length M    dead-end segments shorter than this are stubs (default: 32)
#   --exclude-dead-ends         do not report plain dead ends (default: they are included)
#   -h, --help                  show this help
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
NEAR_MISS=18
SHORT_LEN=32
INCLUDE_DEAD_ENDS=true

usage() { awk 'NR <= 2 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0" | sed '${/^$/d;}'; }
die() { echo "error: $*" >&2; exit 1; }
need() { [ "$1" -ge 2 ] || { echo "missing value for $2" >&2; usage >&2; exit 2; }; }

while [ $# -gt 0 ]; do
    case "$1" in
        --base-url)             need $# "$1"; BASE="$2"; shift ;;
        --near-miss-distance)   need $# "$1"; NEAR_MISS="$2"; shift ;;
        --short-segment-length) need $# "$1"; SHORT_LEN="$2"; shift ;;
        --exclude-dead-ends)    INCLUDE_DEAD_ENDS=false ;;
        -h|--help) usage; exit 0 ;;
        *) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done
BASE="${BASE%/}"

command -v jq >/dev/null 2>&1 || die "jq is required: brew install jq"

# Validate and canonicalise a number (the .ps1 bound these as [double]).
num() { jq -n --arg v "$2" '$v | tonumber | if isnan or isinfinite then error("not finite") else . + 0 end' 2>/dev/null || { echo "$1 must be a number, got '$2'" >&2; exit 2; }; }
NEAR_MISS=$(num --near-miss-distance "$NEAR_MISS")
SHORT_LEN=$(num --short-segment-length "$SHORT_LEN")

api_get() { curl -sS --fail-with-body --max-time 30 "${BASE}$1"; }

result=$(api_get "/state/road-anomalies?limit=500&nearMissDistance=${NEAR_MISS}&shortSegmentLength=${SHORT_LEN}&includeDeadEnds=${INCLUDE_DEAD_ENDS}") || { printf '%s\n' "$result" >&2; exit 1; }
printf '%s\n' "$result" | jq .

if [ "$(printf '%s' "$result" | jq '(.total // 0) > 0')" = "true" ]; then
    echo ""
    echo "Road anomaly repair hints:"
    printf '%s' "$result" | jq -r '
        def s: if . == null then "" else tostring end;
        (.anomalies // [])[]
        | if .type == "deadEndNearRoad" then
            "- Connect dead-end node \(.nodeId|s) to nearby segment \(.nearestSegmentId|s); distance \(.distance|s)m at x=\(.position.x|s), z=\(.position.z|s)"
          elif .type == "shortRoadStub" then
            "- Consider bulldozing or extending short road segment \(.segmentId|s); length \(.length|s)m"
          elif .type == "deadEndRoad" then
            "- Review dead-end node \(.nodeId|s); segment \(.ownSegmentId|s) at x=\(.position.x|s), z=\(.position.z|s)"
          else empty end'
fi
