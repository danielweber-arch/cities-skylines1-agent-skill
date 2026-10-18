#!/usr/bin/env bash
#
# Launch Cities: Skylines through Steam for a new map and wait until the Agent Bridge reports a
# loaded city. Port of start-new-map.ps1.
#
# Usage: scripts/start-new-map.sh [--api-port N] [--steam-app-id ID] [--launcher-timeout N]
#                                 [--game-load-timeout N] [--skip-kill] [--skip-build]
#                                 [--skip-new-map]
#
#   --api-port N            bridge port (default 32123)
#   --steam-app-id ID       Steam app id to launch (default 255710)
#   --launcher-timeout N    seconds to wait for /health to first answer before warning that the
#                           mod is probably not enabled; polling continues up to
#                           --game-load-timeout (default 90)
#   --game-load-timeout N   total seconds, from launch, to wait for levelLoaded == true (default 300)
#   --skip-kill             do not stop a running Cities process first
#   --skip-build            do not run scripts/build.sh first
#   --skip-new-map          only launch; omit the in-game New Game steps from the operator prompt
#   -h, --help              show this help
#
# Environment:
#   CS1_NO_LAUNCH=1         skip the `open steam://rungameid/...` step (offline testing, or when
#                           the game is already running)
#
# The Windows original clicked the Paradox launcher's Play button, slept 30s, then clicked through
# the startup modals, New Game and Start at hardcoded screen coordinates with user32.dll. macOS
# cannot do that without Accessibility-permission UI scripting, so this version prints an operator
# prompt and polls /health instead.
#
set -euo pipefail

api_port=32123
steam_app_id=255710
launcher_timeout=90
game_load_timeout=300
skip_kill=0
skip_build=0
skip_new_map=0

usage() { sed -n '3,/^set -e/{/^#/p;}' "$0" | sed 's/^# \{0,1\}//'; }
die() { echo "error: $*" >&2; exit 1; }
step() { echo "[$(date +%H:%M:%S)] $*"; }

need_value() { [ $# -ge 2 ] && [ -n "$2" ] || { echo "error: $1 needs a value" >&2; usage >&2; exit 2; }; }
while [ $# -gt 0 ]; do
    case "$1" in
        --api-port)          need_value "$@"; api_port="$2"; shift ;;
        --steam-app-id)      need_value "$@"; steam_app_id="$2"; shift ;;
        --launcher-timeout)  need_value "$@"; launcher_timeout="$2"; shift ;;
        --game-load-timeout) need_value "$@"; game_load_timeout="$2"; shift ;;
        --skip-kill)         skip_kill=1 ;;
        --skip-build)        skip_build=1 ;;
        --skip-new-map)      skip_new_map=1 ;;
        -h|--help)           usage; exit 0 ;;
        *)                   echo "error: unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

for n in "$api_port" "$launcher_timeout" "$game_load_timeout"; do
    case "$n" in ''|*[!0-9]*) { echo "error: expected a non-negative integer, got '$n'" >&2; usage >&2; exit 2; } ;; esac
done

command -v jq >/dev/null 2>&1 || die "jq is required: brew install jq"
command -v curl >/dev/null 2>&1 || die "curl is required"

BASE="http://127.0.0.1:${api_port}"
script_dir="$(cd "$(dirname "$0")" && pwd)"

if [ "$skip_build" -eq 0 ]; then
    step "Building and installing SkylinesAgentBridge.dll"
    "$script_dir/build.sh" || die "build.sh failed; not launching the game"
fi

if [ "$skip_kill" -eq 0 ]; then
    if pgrep -x Cities >/dev/null 2>&1; then
        step "Stopping existing Cities process"
        pkill -x Cities || true
    fi
    sleep 3
fi

if [ "${CS1_NO_LAUNCH:-0}" = "1" ]; then
    step "CS1_NO_LAUNCH=1: skipping the Steam launch"
else
    step "Launching Cities: Skylines through Steam"
    [ -d /Applications/Steam.app ] || die "/Applications/Steam.app not found; install Steam or launch the game yourself and rerun with CS1_NO_LAUNCH=1"
    open "steam://rungameid/${steam_app_id}" || die "'open steam://rungameid/${steam_app_id}' failed; is Steam installed and signed in?"
fi

echo
echo "  ACTION NEEDED (this step was scripted mouse clicks on Windows):"
echo "    1. In the Paradox launcher click Play."
if [ "$skip_new_map" -eq 0 ]; then
    echo "    2. In Cities: Skylines dismiss the startup dialogs (new-features / old-mod warnings),"
    echo "       choose New Game, pick a map, and click Start."
else
    echo "    2. (--skip-new-map) Open whatever city you want in Cities: Skylines."
fi
echo "  First run? Before loading a city, open Content Manager -> Mods and enable"
echo "  \"Skylines Agent Bridge\", then return to the main menu."
echo "  Waiting up to ${game_load_timeout}s for ${BASE}/health to report levelLoaded:true ..."
echo

start=$(date +%s)
answered=0
warned=0
health=""
last_health=""
while :; do
    elapsed=$(( $(date +%s) - start ))
    if health="$(curl -sS --fail --max-time 2 "${BASE}/health" 2>/dev/null)"; then
        last_health="$health"
        if [ "$answered" -eq 0 ]; then
            answered=1
            step "Agent Bridge is answering; waiting for a city to load"
        fi
        if printf '%s' "$health" | jq -e '.levelLoaded == true' >/dev/null 2>&1; then
            break
        fi
    elif [ "$answered" -eq 0 ] && [ "$warned" -eq 0 ] && [ "$elapsed" -ge "$launcher_timeout" ]; then
        warned=1
        step "No answer from /health after ${launcher_timeout}s. If the main menu is up, the mod is not enabled (Content Manager -> Mods). Still waiting."
    fi
    if [ "$elapsed" -ge "$game_load_timeout" ]; then
        if [ "$answered" -eq 0 ]; then
            die "timed out after ${game_load_timeout}s: ${BASE}/health never answered. Most likely the Skylines Agent Bridge mod is not enabled (Content Manager -> Mods), the game did not start, or --api-port is wrong."
        else
            die "timed out after ${game_load_timeout}s: the bridge answered but levelLoaded never became true. The mod is loaded; no city was opened (New Game -> pick a map -> Start). Last /health: ${last_health}"
        fi
    fi
    sleep 2
done

step "Ready"
printf '%s' "$health" | jq .
