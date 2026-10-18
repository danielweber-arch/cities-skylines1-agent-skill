#!/usr/bin/env bash
#
# Build the starter district (via develop-starter-city.sh), then add utilities,
# water/heating pipes, power lines, basic services and extra residential zoning,
# run the simulation briefly, dump state, and save. Port of develop-city-with-infrastructure.ps1.
#
# Usage: develop-city-with-infrastructure.sh [--base-url URL] [--dry-run]
#   --base-url URL   bridge URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
#   --dry-run        send dryRun:true on every command (default: off). NOTE: the mod ignores
#                    dryRun on /commands/set-simulation-speed and /commands/save, so a dry run
#                    still unpauses the simulation and writes a real save, exactly like the .ps1.
#   -h, --help       show this help
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
DRY_RUN=false

usage() { sed -n '3,13p' "$0" | sed 's/^# \{0,1\}//'; }
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

place_building() { # prefab x z [angle=0]
    local body
    body=$(jq -nc --argjson dry "$DRY_RUN" --arg prefab "$1" --argjson x "$2" --argjson z "$3" \
        --argjson angle "${4:-0}" \
        '{dryRun: $dry, buildingPrefab: $prefab, position: {x: $x, z: $z}, angleDegrees: $angle}')
    api_post /commands/place-building "$body" | jq .
}

set_zone() { # zone x z radius
    local body
    body=$(jq -nc --argjson dry "$DRY_RUN" --arg zone "$1" --argjson x "$2" --argjson z "$3" \
        --argjson radius "$4" '{dryRun: $dry, zone: $zone, center: {x: $x, z: $z}, radius: $radius}')
    api_post /commands/set-zone "$body" | jq .
}

echo "Creating connected road and zone district"
starter="$(cd "$(dirname "$0")" && pwd)/develop-starter-city.sh"
if [ "$DRY_RUN" = true ]; then
    "$starter" --base-url "$BASE" --dry-run
else
    "$starter" --base-url "$BASE"
fi

echo "Placing utilities"
place_building "Wind Turbine" 120 220 0
place_building "Water Tower" 120 -220 0
build_network "Basic Road" 480 160 600 160 "Agent Sewage Loop South"
build_network "Basic Road" 480 160 480 220 "Agent Sewage Loop West"
build_network "Basic Road" 480 220 600 220 "Agent Sewage Loop North"
build_network "Basic Road" 600 160 600 220 "Agent Sewage Loop East"
place_building "Inland Water Treatment Plant 01" 540 260 180

echo "Laying water pipes"
build_network "Water Pipe" 120 -220 120 200 "Agent Water Trunk"
build_network "Water Pipe" 120 -120 520 -120 "Agent Water West-East -120"
build_network "Water Pipe" 120 0 520 0 "Agent Water West-East 0"
build_network "Water Pipe" 120 120 520 120 "Agent Water West-East 120"
build_network "Water Pipe" 320 -180 320 180 "Agent Water North-South"
build_network "Water Pipe" 480 120 580 120 "Agent Restore Service Pipe A"
build_network "Water Pipe" 180 200 580 200 "Agent Service Water North Offset"
build_network "Water Pipe" 540 184 540 260 "Agent Sewage Plant Water Safe"
build_network "Water Pipe" 360 120 360 220 "Agent Boiler Water South Safe"

echo "Laying power line"
build_network "Power Line" 120 220 240 160 "Agent Power Tie 1"
build_network "Power Line" 240 160 400 160 "Agent Power Tie 2"
build_network "Power Line" 400 160 400 80 "Agent Power Loop 1"
build_network "Power Line" 400 80 240 80 "Agent Power Loop 2"

echo "Placing basic city services"
place_building "711884134.Koban Police Box_Data" 200 200 180
place_building "Fire House" 260 200 180
place_building "Medical Clinic" 340 200 180
place_building "Elementary School" 440 200 180
place_building "Cemetery" 500 200 180
place_building "Landfill Site" 540 200 180
place_building "Landfill Site" 500 260 180
place_building "Boiler Station" 360 220 180
build_network "Heating Pipe" 120 -220 120 200 "Agent Heating Trunk"
build_network "Heating Pipe" 120 -120 520 -120 "Agent Heating West-East -120"
build_network "Heating Pipe" 120 0 520 0 "Agent Heating West-East 0"
build_network "Heating Pipe" 120 120 520 120 "Agent Heating West-East 120"
build_network "Heating Pipe" 180 200 580 200 "Agent Service Heating North Offset"
build_network "Heating Pipe" 360 120 360 220 "Agent Boiler Heating South Safe"
build_network "Heating Pipe" 320 -180 320 180 "Agent Heating North-South"
build_network "Power Line" 400 160 580 200 "Agent Service Power North Offset"

echo "Adding extra residential capacity"
set_zone "ResidentialLow" 180 -40 60
set_zone "ResidentialLow" 260 -40 60
set_zone "ResidentialLow" 260 120 60

echo "Letting the city run"
# dryRun is carried for uniformity; SimulationCommands.SetSimulationSpeed ignores it.
api_post /commands/set-simulation-speed \
    "$(jq -nc --argjson dry "$DRY_RUN" '{dryRun: $dry, paused: false, speed: 3}')" | jq .
sleep 5

echo "Summary"
api_get /state/summary | jq .

echo "Problems"
api_get "/state/problems?limit=100" | jq .

echo "Facilities"
api_get "/state/facilities?limit=300" | jq .

echo "Saving city"
save_name="AgentAutoSave-$(date +%Y%m%d-%H%M%S)"
# dryRun is carried for uniformity; SaveCommands.Save ignores it and always saves.
api_post /commands/save \
    "$(jq -nc --argjson dry "$DRY_RUN" --arg name "$save_name" '{dryRun: $dry, name: $name}')" | jq .
