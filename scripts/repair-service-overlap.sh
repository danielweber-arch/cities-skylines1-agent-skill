#!/usr/bin/env bash
#
# Bulldoze the old service buildings that sit on/near the z=160 service road
# (x 180..580, z 120..190), re-lay offset pipes/power, rebuild the services off the
# road centerlines, let the simulation settle, dump state, and save.
# Port of repair-service-overlap.ps1.
#
# Usage: repair-service-overlap.sh [--base-url URL] [--dry-run]
#   --base-url URL   bridge URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
#   --dry-run        send dryRun:true on every command (default: off). NOTE: the mod ignores
#                    dryRun on /commands/set-simulation-speed and /commands/save, so a dry run
#                    still unpauses the simulation and writes a real save, exactly like the .ps1.
#   -h, --help       show this help
# Env: SETTLE_SECONDS  settle sleep after unpausing (default: 20, as in the .ps1)
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
DRY_RUN=false
SETTLE_SECONDS="${SETTLE_SECONDS:-20}"

usage() { sed -n '3,15p' "$0" | sed 's/^# \{0,1\}//'; }
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

build_network() { # prefab x1 z1 x2 z2 name
    local body
    body=$(jq -nc --argjson dry "$DRY_RUN" --arg prefab "$1" --argjson x1 "$2" --argjson z1 "$3" \
        --argjson x2 "$4" --argjson z2 "$5" --arg name "$6" \
        '{dryRun: $dry, roadPrefab: $prefab, start: {x: $x1, z: $z1}, end: {x: $x2, z: $z2}, name: $name}')
    api_post /commands/build-road "$body" | jq .
}

place_building() { # prefab x z [angle=180]
    local body
    body=$(jq -nc --argjson dry "$DRY_RUN" --arg prefab "$1" --argjson x "$2" --argjson z "$3" \
        --argjson angle "${4:-180}" \
        '{dryRun: $dry, buildingPrefab: $prefab, position: {x: $x, z: $z}, angleDegrees: $angle}')
    api_post /commands/place-building "$body" | jq .
}

bulldoze_building() { # id
    local body
    body=$(jq -nc --argjson dry "$DRY_RUN" --argjson id "$1" '{dryRun: $dry, entityType: "building", id: $id}')
    api_post /commands/bulldoze "$body" | jq .
}

echo "Finding old service buildings that overlap or sit too close to roads"
facilities=$(api_get "/state/facilities?limit=500")
ids=$(printf '%s' "$facilities" | jq -r '
    ["Inland Water Treatment Plant 01", "Boiler Station", "Landfill Site", "Medical Clinic",
     "Fire House", "Elementary School", "Cemetery", "711884134.Koban Police Box_Data", "Water Tower"] as $relocated
    | (.facilities // [])[]
    | select(.prefab as $p | $relocated | index($p))
    | (.position.x | tonumber) as $x | (.position.z | tonumber) as $z
    | select($x >= 180 and $x <= 580 and $z >= 120 and $z <= 190)
    | .id')
for id in $ids; do
    bulldoze_building "$id"
    sleep 0.15
done

echo "Adding non-overlap utility pipes along the north side of the service road"
build_network "Basic Road" 580 190 580 220 "Agent Sewage Plant Connector North"
build_network "Basic Road" 480 220 600 220 "Agent Sewage Plant Frontage Safe"
build_network "Water Pipe" 180 184 580 184 "Agent Service Water North Offset"
build_network "Water Pipe" 540 184 540 260 "Agent Sewage Plant Water Safe"
build_network "Water Pipe" 360 120 360 144 "Agent Boiler Water South Safe"
build_network "Heating Pipe" 180 184 580 184 "Agent Service Heating North Offset"
build_network "Heating Pipe" 360 120 360 144 "Agent Boiler Heating South Safe"
build_network "Power Line" 400 160 580 184 "Agent Service Power North Offset"

echo "Rebuilding services off the road centerlines"
place_building "711884134.Koban Police Box_Data" 200 184 180
place_building "Fire House" 260 184 180
place_building "Medical Clinic" 340 184 180
place_building "Elementary School" 440 184 180
place_building "Cemetery" 500 184 180
place_building "Landfill Site" 540 184 180
place_building "Landfill Site" 500 244 180
place_building "Inland Water Treatment Plant 01" 540 260 180
place_building "Boiler Station" 360 128 0

echo "Letting the simulation settle"
# dryRun is carried for uniformity; SimulationCommands.SetSimulationSpeed ignores it.
api_post /commands/set-simulation-speed \
    "$(jq -nc --argjson dry "$DRY_RUN" '{dryRun: $dry, paused: false, speed: 3}')" | jq .
sleep "$SETTLE_SECONDS"

echo "Problems"
api_get "/state/problems?limit=100" | jq .

echo "Facilities"
api_get "/state/facilities?limit=300" | jq .

echo "Saving city"
save_name="AgentAutoSave-overlap-fixed-$(date +%Y%m%d-%H%M%S)"
# dryRun is carried for uniformity; SaveCommands.Save ignores it and always saves.
api_post /commands/save \
    "$(jq -nc --argjson dry "$DRY_RUN" --arg name "$save_name" '{dryRun: $dry, name: $name}')" | jq .
