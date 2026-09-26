#!/usr/bin/env bash
#
# Lay a compact connected starter district (5x5 grid of Basic Road blocks),
# zone it, and add an arterial plus highway links. Port of develop-starter-city.ps1.
#
# Usage: develop-starter-city.sh [--base-url URL] [--dry-run]
#   --base-url URL   bridge URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
#   --dry-run        send dryRun:true on every command (default: off)
#   -h, --help       show this help
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
DRY_RUN=false

usage() { sed -n '3,10p' "$0" | sed 's/^# \{0,1\}//'; }
die() { echo "error: $*" >&2; exit 1; }

while [ $# -gt 0 ]; do
    case "$1" in
        --base-url) [ $# -ge 2 ] || { usage >&2; exit 2; }; BASE="$2"; shift ;;
        --dry-run) DRY_RUN=true ;;
        -h|--help) usage; exit 0 ;;
        *) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

command -v jq >/dev/null 2>&1 || die "jq is required: brew install jq"

api_get()  { curl -sS --fail-with-body --max-time 30  "${BASE}$1"; }
api_post() { echo "POST $1 $2" >&2; curl -sS --fail-with-body --max-time 180 -X POST -H 'Content-Type: application/json' --data "$2" "${BASE}$1"; }

build_road() { # x1 z1 x2 z2 name
    local body
    body=$(jq -nc --argjson dry "$DRY_RUN" --argjson x1 "$1" --argjson z1 "$2" \
        --argjson x2 "$3" --argjson z2 "$4" --arg name "$5" \
        '{dryRun: $dry, roadPrefab: "Basic Road", start: {x: $x1, z: $z1}, end: {x: $x2, z: $z2}, name: $name}')
    api_post /commands/build-road "$body" | jq .
}

set_zone() { # zone x z radius
    local body
    body=$(jq -nc --argjson dry "$DRY_RUN" --arg zone "$1" --argjson x "$2" --argjson z "$3" \
        --argjson radius "$4" '{dryRun: $dry, zone: $zone, center: {x: $x, z: $z}, radius: $radius}')
    api_post /commands/set-zone "$body" | jq .
}

echo "Health"
api_get /health | jq .

# A compact connected starter district. Each road is one block segment, so
# the bridge can reuse matching endpoints and create actual intersections.
# Keep the district inland; the eastern shoreline on the default map floods
# low roads around x=640+.
xs=(160 240 320 400 480)
zs=(-160 -80 0 80 160)

for z in "${zs[@]}"; do
    i=0
    while [ "$i" -lt $(( ${#xs[@]} - 1 )) ]; do
        build_road "${xs[$i]}" "$z" "${xs[$((i + 1))]}" "$z" "Agent Block E-W ${xs[$i]} $z"
        sleep 0.12
        i=$((i + 1))
    done
done

for x in "${xs[@]}"; do
    i=0
    while [ "$i" -lt $(( ${#zs[@]} - 1 )) ]; do
        build_road "$x" "${zs[$i]}" "$x" "${zs[$((i + 1))]}" "Agent Block N-S $x ${zs[$i]}"
        sleep 0.12
        i=$((i + 1))
    done
done

set_zone "ResidentialLow" 200 -120 48
set_zone "ResidentialLow" 200 120 48
set_zone "ResidentialLow" 320 40 48
set_zone "CommercialLow" 400 -80 42
set_zone "Office" 440 80 42
set_zone "Industrial" 480 0 36

build_road 400 160 400 300 "Agent Outside Arterial 1"
build_road 400 300 422.737 522.957 "Agent Outside Highway Link South"
build_road 400 300 423.614 554.945 "Agent Outside Highway Link North"

echo "Summary"
api_get /state/summary | jq .
