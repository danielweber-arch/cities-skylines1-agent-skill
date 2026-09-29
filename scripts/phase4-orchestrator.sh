#!/usr/bin/env bash
#
# Open Portville, take a live snapshot of the city, and have Claude Fable 5.1 orchestrate a
# Phase 4 plan from it. Planning only: nothing in the city is built, zoned, bulldozed or saved.
#
# Usage: ./scripts/phase4-orchestrator.sh [--model MODEL] [--skip-launch] [--snapshot-only]
#
#   --model MODEL     orchestrator model (default: claude-fable-5-1)
#   --skip-launch     do not start the game; expect Portville to be loaded already
#   --snapshot-only   take the live snapshot and stop (no Claude run)
#   -h, --help        show this help
#
# Steps:
#   1. If the bridge is not answering, runs ./scripts/start-resume.sh to open the game, then
#      asks you to load Portville (Load Game -> Portville) and waits for the city.
#   2. Saves a live snapshot to tmp/portville/p4live/<time>/: every read-only /state endpoint
#      as JSON, and top-down /capture images (whole map in several overlays, plus downtown).
#   3. Runs the orchestrator with read-only tools. It may start subagents for the analysis
#      and writes the result to portville-phase4-plan.md (and notes into the snapshot folder).
#   4. Opens the plan and the snapshot folder.
#
# Needs curl, jq and the claude CLI (CLAUDE_BIN=/path/to/claude to override).
#
set -euo pipefail

MODEL="claude-fable-5-1"
SKIP_LAUNCH=0
SNAPSHOT_ONLY=0
BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
BASE="${BASE%/}"
CLAUDE_BIN="${CLAUDE_BIN:-claude}"

usage() { awk 'NR <= 2 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0"; }
die() { echo "error: $*" >&2; exit 1; }
step() { echo "[phase4 $(date +%H:%M:%S)] $*"; }

while [ $# -gt 0 ]; do
    case "$1" in
        --model) [ $# -ge 2 ] || die "--model needs a value"; MODEL="$2"; shift ;;
        --skip-launch) SKIP_LAUNCH=1 ;;
        --snapshot-only) SNAPSHOT_ONLY=1 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

command -v curl >/dev/null 2>&1 || die "curl is required"
command -v jq >/dev/null 2>&1 || die "jq is required: brew install jq"
[ "$SNAPSHOT_ONLY" = 1 ] || command -v "$CLAUDE_BIN" >/dev/null 2>&1 || die "the claude CLI was not found"

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo"

health() { curl -sS --fail --max-time 5 "$BASE/health" 2>/dev/null; }
level_loaded() { health | jq -e '.levelLoaded == true' >/dev/null 2>&1; }

# --- 1. open the game -----------------------------------------------------------

if ! health >/dev/null; then
    [ "$SKIP_LAUNCH" = 1 ] && die "the bridge at $BASE is not answering. Start Cities: Skylines, or drop --skip-launch."
    step "opening Cities: Skylines"
    ./scripts/start-resume.sh --skip-kill || step "start-resume.sh returned an error; waiting for the city anyway"
fi

if ! level_loaded; then
    echo
    echo "  In the game: Load Game -> Portville. This script continues once the city is loaded."
    echo
fi
until level_loaded; do sleep 5; done

latest_save=$(curl -sS --fail --max-time 10 "$BASE/state/saves" | jq -r '.saves[0].name // "unknown"' 2>/dev/null || echo unknown)
step "city loaded (newest save on disk: $latest_save)"
if [ -t 0 ]; then
    printf '  Is the loaded city Portville? [Y/n] '
    read -r answer || answer=""
    case "$answer" in n|N|no|NO) die "load Portville in the game, then run this again with --skip-launch" ;; esac
fi

# --- 2. live snapshot -------------------------------------------------------------

stamp=$(date +%Y%m%d-%H%M%S)
snap="tmp/portville/p4live/$stamp"
mkdir -p "$snap"
step "snapshot -> $snap"

get_json() {
    # $1 file name, $2 path+query
    if curl -sS --fail --max-time 120 "$BASE$2" -o "$snap/$1.json"; then
        echo "  ok    $2"
    else
        echo "  FAIL  $2"
        rm -f "$snap/$1.json"
    fi
}

get_json health            "/health"
get_json summary           "/state/summary"
get_json demand            "/state/demand"
get_json economy           "/state/economy"
get_json zones             "/state/zones"
get_json problems          "/state/problems?limit=500"
get_json policies          "/state/policies"
get_json areas             "/state/areas"
get_json transit           "/state/transit?includeStops=true"
get_json traffic           "/state/traffic"
get_json facilities        "/state/facilities?limit=2000"
get_json growables         "/state/growables?limit=5000"
get_json external          "/state/external-connections"
get_json road-anomalies    "/state/road-anomalies?limit=500&includeDeadEnds=false"
get_json zone-anomalies    "/state/zone-anomalies?limit=500&includeUnzonedHoles=true"
get_json building-anomalies "/state/building-anomalies?limit=500"
get_json chirps            "/state/chirps?limit=100"
get_json saves             "/state/saves"

capture() {
    # $1 file name, $2 x, $3 z, $4 size, $5 mode
    if curl -sS --fail --max-time 120 "$BASE/capture?x=$2&z=$3&size=$4&pixels=1024&mode=$5" -o "$snap/$1.png"; then
        echo "  ok    capture $1"
    else
        echo "  FAIL  capture $1"
        rm -f "$snap/$1.png"
    fi
}

# The 5x5 buildable tiles span about 9600 m; Portville runs from x ~-3700 to ~2900.
capture map-plain       0 0 9600 None
capture map-traffic     0 0 9600 Traffic
capture map-transport   0 0 9600 Transport
capture map-landvalue   0 0 9600 LandValue
capture map-pollution   0 0 9600 Pollution
capture map-density     0 0 9600 Density
capture downtown-plain  1200 700 2000 None
capture westbank-plain  -1500 -300 2400 None

cat >"$snap/README.md" <<EOF
# Portville live snapshot $stamp

Taken by scripts/phase4-orchestrator.sh from $BASE. JSON files are the raw /state reads;
PNG files are /capture renders (map-* cover 9600 m centred on 0,0; downtown-* and westbank-*
are close-ups). Planning only: nothing in the city was changed.
EOF

[ "$SNAPSHOT_ONLY" = 1 ] && { step "snapshot only; done"; open "$snap" 2>/dev/null || true; exit 0; }

# --- 3. orchestrator ----------------------------------------------------------------

# Read-only by construction: only state reads, prefab lists, captures and the master plan are
# allowed from the bridge; every mutating tool and every non-GET curl is denied (deny wins
# over any allow rule in the user or project settings).
allowed=(
    "Read" "Glob" "Grep" "Agent" "Task" "TodoWrite"
    "Write(portville-phase4-plan.md)" "Edit(portville-phase4-plan.md)"
    "Write($snap/**)" "Edit($snap/**)"
    "mcp__cs1-bridge__cs1_health" "mcp__cs1-bridge__cs1_master_plan" "mcp__cs1-bridge__cs1_capture"
    "mcp__cs1-bridge__cs1_chat_history"
    "mcp__cs1-bridge__cs1_prefabs_buildings" "mcp__cs1-bridge__cs1_prefabs_networks" "mcp__cs1-bridge__cs1_prefabs_roads"
    "mcp__cs1-bridge__cs1_state_areas" "mcp__cs1-bridge__cs1_state_building_anomalies" "mcp__cs1-bridge__cs1_state_chirps"
    "mcp__cs1-bridge__cs1_state_demand" "mcp__cs1-bridge__cs1_state_economy" "mcp__cs1-bridge__cs1_state_external_connections"
    "mcp__cs1-bridge__cs1_state_facilities" "mcp__cs1-bridge__cs1_state_growables" "mcp__cs1-bridge__cs1_state_networks"
    "mcp__cs1-bridge__cs1_state_policies" "mcp__cs1-bridge__cs1_state_problems" "mcp__cs1-bridge__cs1_state_road_anomalies"
    "mcp__cs1-bridge__cs1_state_saves" "mcp__cs1-bridge__cs1_state_summary" "mcp__cs1-bridge__cs1_state_traffic"
    "mcp__cs1-bridge__cs1_state_transit" "mcp__cs1-bridge__cs1_state_zone_anomalies" "mcp__cs1-bridge__cs1_state_zones"
    "Bash(curl -sS $BASE/state/*)" "Bash(curl -sS $BASE/capture*)" "Bash(curl -sS $BASE/health)"
    "Bash(jq *)"
)
denied=(
    "mcp__cs1-bridge__cs1_build_grid" "mcp__cs1-bridge__cs1_build_neighborhood" "mcp__cs1-bridge__cs1_build_network"
    "mcp__cs1-bridge__cs1_bulldoze" "mcp__cs1-bridge__cs1_connect" "mcp__cs1-bridge__cs1_move_building"
    "mcp__cs1-bridge__cs1_place_building" "mcp__cs1-bridge__cs1_repair_zone_clusters"
    "mcp__cs1-bridge__cs1_repair_zones_to_growables" "mcp__cs1-bridge__cs1_save"
    "mcp__cs1-bridge__cs1_set_building_active" "mcp__cs1-bridge__cs1_set_policy" "mcp__cs1-bridge__cs1_set_service_budget"
    "mcp__cs1-bridge__cs1_set_simulation_speed" "mcp__cs1-bridge__cs1_set_tax_rate" "mcp__cs1-bridge__cs1_set_zone"
    "mcp__cs1-bridge__cs1_transit_line_create" "mcp__cs1-bridge__cs1_transit_line_delete"
    "mcp__cs1-bridge__cs1_transit_line_edit" "mcp__cs1-bridge__cs1_unlock_area"
    "mcp__cs1-bridge__cs1_chat_say" "mcp__cs1-bridge__cs1_chat_status"
    "Bash(curl * -X *)" "Bash(curl * --data*)" "Bash(curl * -d *)" "Bash(curl *commands/*)"
    "Bash(./scripts/*)"
)

prompt="$(sed "s|{{SNAPSHOT}}|$snap|g; s|{{BASE}}|$BASE|g" scripts/prompts/phase4-orchestrator.md)"
log="$snap/orchestrator.log"
step "orchestrator ($MODEL) is planning; transcript -> $log"
cp -f portville-phase4-plan.md "$snap/phase4-plan-before.md" 2>/dev/null || true

rc=0
"$CLAUDE_BIN" -p "$prompt" \
    --model "$MODEL" \
    --permission-mode dontAsk \
    --allowedTools "${allowed[@]}" \
    --disallowedTools "${denied[@]}" \
    --output-format text >"$log" 2>&1 || rc=$?
tail -n 30 "$log"
[ "$rc" = 0 ] || step "the orchestrator exited with code $rc; see $log"

# --- 4. open the result ---------------------------------------------------------------

if [ -f portville-phase4-plan.md ]; then
    step "plan: $repo/portville-phase4-plan.md"
    open portville-phase4-plan.md 2>/dev/null || true
fi
open "$snap" 2>/dev/null || true
step "done. Nothing in the city was changed. Review the plan, then commit it."
