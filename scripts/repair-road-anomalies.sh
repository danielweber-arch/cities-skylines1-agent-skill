#!/usr/bin/env bash
#
# Port of repair-road-anomalies.ps1: bulldoze the road segments behind short stubs and
# near-miss dead ends (and, with --include-dead-ends, plain dead ends) inside a bounding
# box, then save the city if anything was bulldozed for real.
#
# Usage: ./scripts/repair-road-anomalies.sh [--base-url URL] [--near-miss-distance M]
#            [--short-segment-length M] [--min-x X] [--max-x X] [--min-z Z] [--max-z Z]
#            [--include-dead-ends] [--dry-run]
#
#   --base-url URL              bridge base URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
#   --near-miss-distance M      near-miss threshold passed to /state/road-anomalies (default: 18)
#   --short-segment-length M    short-stub threshold passed to /state/road-anomalies (default: 32)
#   --min-x X / --max-x X       bounding box, inclusive (default: -100000 / 100000)
#   --min-z Z / --max-z Z       bounding box, inclusive (default: -100000 / 100000)
#   --include-dead-ends         also bulldoze plain dead ends (default: off)
#   --dry-run                   send dryRun=true on every bulldoze and skip the save (default: off)
#   -h, --help                  show this help
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
NEAR_MISS=18
SHORT_LEN=32
MIN_X=-100000
MAX_X=100000
MIN_Z=-100000
MAX_Z=100000
INCLUDE_DEAD_ENDS=false
DRY_RUN=false

usage() { awk 'NR <= 2 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0" | sed '${/^$/d;}'; }
die() { echo "error: $*" >&2; exit 1; }
need() { [ "$1" -ge 2 ] || { echo "missing value for $2" >&2; usage >&2; exit 2; }; }

while [ $# -gt 0 ]; do
    case "$1" in
        --base-url)             need $# "$1"; BASE="$2"; shift ;;
        --near-miss-distance)   need $# "$1"; NEAR_MISS="$2"; shift ;;
        --short-segment-length) need $# "$1"; SHORT_LEN="$2"; shift ;;
        --min-x)                need $# "$1"; MIN_X="$2"; shift ;;
        --max-x)                need $# "$1"; MAX_X="$2"; shift ;;
        --min-z)                need $# "$1"; MIN_Z="$2"; shift ;;
        --max-z)                need $# "$1"; MAX_Z="$2"; shift ;;
        --include-dead-ends)    INCLUDE_DEAD_ENDS=true ;;
        --dry-run)              DRY_RUN=true ;;
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
MIN_X=$(num --min-x "$MIN_X")
MAX_X=$(num --max-x "$MAX_X")
MIN_Z=$(num --min-z "$MIN_Z")
MAX_Z=$(num --max-z "$MAX_Z")

api_get()  { curl -sS --fail-with-body --max-time 30  "${BASE}$1"; }
api_post() { echo "POST $1 $2" >&2; curl -sS --fail-with-body --max-time 180 -X POST -H 'Content-Type: application/json' --data "$2" "${BASE}$1"; }

result=$(api_get "/state/road-anomalies?limit=500&nearMissDistance=${NEAR_MISS}&shortSegmentLength=${SHORT_LEN}&includeDeadEnds=${INCLUDE_DEAD_ENDS}") || { printf '%s\n' "$result" >&2; exit 1; }
printf '%s\n' "$result" | jq .

# Segment ids to bulldoze, de-duplicated in first-seen order (the .ps1 used a HashSet[int]).
segments=$(printf '%s' "$result" | jq -r \
    --argjson minX "$MIN_X" --argjson maxX "$MAX_X" \
    --argjson minZ "$MIN_Z" --argjson maxZ "$MAX_Z" \
    --argjson includeDeadEnds "$INCLUDE_DEAD_ENDS" '
    def inbox:
        if . == null then false
        else ((.x // 0) >= $minX and (.x // 0) <= $maxX and (.z // 0) >= $minZ and (.z // 0) <= $maxZ)
        end;
    [ (.anomalies // [])[]
      | if .type == "shortRoadStub" and ((.start | inbox) or (.end | inbox)) then .segmentId
        elif .type == "deadEndNearRoad" and (.position | inbox) then .ownSegmentId
        elif $includeDeadEnds and .type == "deadEndRoad" and (.position | inbox) then .ownSegmentId
        else empty end
      | select(. != null) ]
    | reduce .[] as $id ([]; if index([$id]) then . else . + [$id] end)
    | .[]')

count=0
for segment_id in $segments; do
    body=$(jq -n --argjson dryRun "$DRY_RUN" --argjson id "$segment_id" \
        '{dryRun: $dryRun, entityType: "netSegment", id: $id, keepNodes: false}')
    api_post "/commands/bulldoze" "$body" | jq .
    count=$((count + 1))
    sleep 0.15
done

if [ "$count" -gt 0 ] && [ "$DRY_RUN" != "true" ]; then
    body=$(jq -n --arg name "AgentAutoSave-road-anomalies-repaired-$(date +%Y%m%d-%H%M%S)" '{name: $name}')
    api_post "/commands/save" "$body" | jq .
fi
