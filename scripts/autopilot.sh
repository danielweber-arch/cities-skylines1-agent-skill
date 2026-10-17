#!/usr/bin/env bash
#
# Run a Cities: Skylines 1 city with no human at the terminal: an Opus mayor decides, a Sonnet
# operator controls the city, Fable 5.1 reviews the master plan, and the player talks to them
# only through the in-game Claude chat window. Design and guarantees: docs/autopilot.md.
#
# Usage: ./scripts/autopilot.sh [options]
#
#   --mayor-model M       decision model (default: opus)
#   --operator-model M    execution model (default: sonnet)
#   --planner-model M     master-plan reviewer (default: claude-fable-5-1)
#   --stall-timeout S     stop a turn that shows no activity for S seconds (default: 600)
#   --operator-wall S     hard wall clock for one operator turn (default: 2700)
#   --mayor-every M       a mayor turn at least every M minutes of operation (default: 120)
#   --no-launch           never start or restart the game; only wait for it
#   --found-new-game      with no autopilot city yet, start a new game from the main menu
#                         (default: wait for the player to start one, then adopt it)
#   -h, --help            show this help
#
# Chat commands typed in the game window (handled here, no model): /pause, /resume, /status,
# /autopilot on, /autopilot off.
#
# Never run this together with scripts/chat-bridge.sh or an interactive session watching the
# chat: both would answer. Install as an always-on agent with ./scripts/install-autopilot-agent.sh.
#
set -uo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
BASE="${BASE%/}"
MAYOR_MODEL="opus"
OPERATOR_MODEL="sonnet"
PLANNER_MODEL="claude-fable-5-1"
STALL_TIMEOUT=600
OPERATOR_WALL=2700
MAYOR_EVERY=120
NO_LAUNCH=0
FOUND_NEW=0
CLAUDE_BIN="${CLAUDE_BIN:-claude}"
MILESTONES="5000 10000 20000 40000 70000 100000 150000 200000 250000 300000 400000 500000"

usage() { awk 'NR <= 2 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0"; }
log() { echo "[autopilot $(date '+%Y-%m-%d %H:%M:%S')] $*" >&2; }
die() { log "fatal: $*"; exit 1; }

while [ $# -gt 0 ]; do
    case "$1" in
        --mayor-model)    MAYOR_MODEL="$2"; shift ;;
        --operator-model) OPERATOR_MODEL="$2"; shift ;;
        --planner-model)  PLANNER_MODEL="$2"; shift ;;
        --stall-timeout)  STALL_TIMEOUT="$2"; shift ;;
        --operator-wall)  OPERATOR_WALL="$2"; shift ;;
        --mayor-every)    MAYOR_EVERY="$2"; shift ;;
        --no-launch)      NO_LAUNCH=1 ;;
        --found-new-game) FOUND_NEW=1 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

command -v curl >/dev/null 2>&1 || die "curl is required"
command -v jq >/dev/null 2>&1 || die "jq is required"
command -v "$CLAUDE_BIN" >/dev/null 2>&1 || die "the claude CLI was not found (CLAUDE_BIN)"

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo"
export CS1_BRIDGE_URL="$BASE"
GAME_DIR="${CS1_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Cities_Skylines}"

ap="$repo/tmp/autopilot"          # global supervisor state (not per city)
mkdir -p "$ap"
queue="$ap/chat-queue.jsonl"      # player messages waiting for the mayor, one JSON per line
acked="$ap/acked-ids"
last_file="$ap/chat-last-id"
touch "$queue" "$acked"
mcp_config="$ap/mcp.json"
jq -n --arg repo "$repo" --arg base "$BASE" '{mcpServers: {"cs1-bridge": {command: "node", args: [($repo + "/mcp-server/dist/index.js")], env: {CS1_BRIDGE_URL: $base}}}}' >"$mcp_config"
self_mtime=$(stat -f %m "$0")
[ -f "$last_file" ] || echo 0 >"$last_file"

# ---------------------------------------------------------------------------------- bridge

api_get() { curl -sS --fail --max-time "${2:-20}" "$BASE$1" 2>/dev/null; }
api_post() { curl -sS --fail-with-body --max-time 30 -X POST -H 'Content-Type: application/json' --data "$2" "$BASE$1" 2>/dev/null; }
health() { api_get /health 8; }
level_loaded() { health | jq -e '.levelLoaded == true' >/dev/null 2>&1; }
game_running() { pgrep -x Cities >/dev/null 2>&1; }

say() {
    # $1 kind, $2 text, $3 optional inReplyTo. Long text is split into 3900-char chunks.
    local kind="$1" text="$2" reply="${3:-}" chunk body
    while [ -n "$text" ]; do
        chunk="${text:0:3900}"
        text="${text:3900}"
        if [ -n "$reply" ] && [ -z "$text" ]; then
            body=$(jq -n --arg t "$chunk" --arg k "$kind" --argjson r "$reply" '{text:$t, kind:$k, inReplyTo:$r}')
        else
            body=$(jq -n --arg t "$chunk" --arg k "$( [ -n "$text" ] && echo update || echo "$kind")" '{text:$t, kind:$k}')
        fi
        api_post /chat/say "$body" >/dev/null || return 1
    done
}
status() { api_post /chat/status "$(jq -n --arg s "$1" --arg t "${2:-}" '{state:$s, text:$t[0:200]}')" >/dev/null 2>&1 || true; }

# ------------------------------------------------------------------------- chat intake

# Pull new panel messages into the queue (or handle supervisor commands). Safe to call from
# the main loop and from the background watcher; every id is taken once (acked file).
CHAT_WAIT=0
intake_chat() {
    local after resp next id src text
    after=$(cat "$last_file" 2>/dev/null || echo 0)
    resp=$(api_get "/chat/inbox?after=${after}&wait=${CHAT_WAIT}&unanswered=true" $((CHAT_WAIT + 10))) || return 1
    # The chat store lives in game memory: ids restart at 1 when the game restarts.
    next=$(printf '%s' "$resp" | jq -r '.lastId // empty' 2>/dev/null)
    case "$next" in ''|*[!0-9]*) return 1 ;; esac
    if [ "$next" -lt "$after" ]; then
        log "chat ids reset (game restarted); reading from the start"
        echo 0 >"$last_file"; : >"$acked"
        return 0
    fi
    printf '%s' "$resp" | jq -c '.messages[]?' 2>/dev/null | while IFS= read -r m; do
        id=$(printf '%s' "$m" | jq -r '.id')
        src=$(printf '%s' "$m" | jq -r '.source // "panel"')
        grep -qx "$id" "$acked" 2>/dev/null && continue
        echo "$id" >>"$acked"
        [ "$src" = "panel" ] || { log "skipping api-sourced chat #$id"; continue; }
        text=$(printf '%s' "$m" | jq -r '.text' | tr -d '\r')
        case "$(printf '%s' "$text" | tr '[:upper:]' '[:lower:]' | sed 's/^ *//; s/ *$//')" in
            /pause)  touch "$ap/paused"; say reply "Paused. The city keeps running, but I won't change anything until you type /resume. I'll still answer messages." "$id" ;;
            /resume) rm -f "$ap/paused"; say reply "Resumed. Back to work." "$id" ;;
            /status) say reply "$(status_text)" "$id" ;;
            "/autopilot on")  echo on >"$ap/autopilot-request"; say reply "Autopilot will take over this city at the next step." "$id" ;;
            "/autopilot off") echo off >"$ap/autopilot-request"; say reply "Autopilot is off for this city. I'll still answer messages but won't change anything." "$id" ;;
            *) printf '%s\n' "$m" >>"$queue"
               say update "Got #$id. The mayor will pick it up as soon as the current step ends." "$id" || true ;;
        esac
    done
    echo "$next" >"$last_file"
}

status_text() {
    local h pop date phase rep
    h=$(health)
    pop=$(printf '%s' "$h" | jq -r '.city.population // "?"' 2>/dev/null)
    date=$(printf '%s' "$h" | jq -r '.city.gameDate // "?"' 2>/dev/null)
    rep=$(jq -r '"\(.status): \(.summary)"' "${dir:-/nonexistent}/autopilot/report.json" 2>/dev/null | head -c 600)
    phase=$(grep -m1 '^currentPhase:' "${dir:-/nonexistent}/progress.md" 2>/dev/null)
    printf 'Autopilot %s. City %s, population %s, game date %s. %s. Last step: %s. Waiting messages: %s.' \
        "$([ -f "$ap/paused" ] && echo PAUSED || echo running)" "${city_name:-?}" "$pop" "$date" \
        "${phase:-no phase yet}" "${rep:-none yet}" "$(wc -l <"$queue" | tr -d ' ')"
}

watcher_pid=""
start_watcher() {
    # While a turn runs: keep the panel online (each long poll is a heartbeat) and take in
    # new messages so the player hears back within seconds.
    (
        trap 'exit 0' TERM
        while :; do
            CHAT_WAIT=20 intake_chat || sleep 5
        done
    ) &
    watcher_pid=$!
}
stop_watcher() {
    [ -n "$watcher_pid" ] || return 0
    pkill -TERM -P "$watcher_pid" 2>/dev/null; kill -TERM "$watcher_pid" 2>/dev/null
    wait "$watcher_pid" 2>/dev/null; watcher_pid=""
}

stop_tree() {
    local pid="$1"; [ -n "$pid" ] || return 0
    pkill -TERM -P "$pid" 2>/dev/null; kill -TERM "$pid" 2>/dev/null
    local n=0
    while kill -0 "$pid" 2>/dev/null && [ "$n" -lt 20 ]; do sleep 0.5; n=$((n + 1)); done
    pkill -KILL -P "$pid" 2>/dev/null; kill -KILL "$pid" 2>/dev/null; true
}

turn_pid=""
shutdown() {
    trap - INT TERM
    stop_watcher; stop_tree "$turn_pid"
    status offline "autopilot stopped"
    log "stopped"
    exit 0
}
trap shutdown INT TERM
trap stop_watcher EXIT

# ------------------------------------------------------------------------- game lifecycle

launch_game() {
    # $1 = "menu" (no save) or "continue" (most recent save). Never through the launcher.
    [ "$NO_LAUNCH" = 1 ] && return 1
    game_running && return 0
    log "starting Cities: Skylines ($1)"
    local args=()
    [ "$1" = continue ] && args=(--continuelastsave)
    (cd "$GAME_DIR" && SteamAppId=255710 SteamGameId=255710 nohup "$GAME_DIR/Cities.app/Contents/MacOS/Cities" ${args[@]+"${args[@]}"} >/dev/null 2>&1 &)
    local n=0
    until health >/dev/null; do
        sleep 5; n=$((n + 5))
        game_running || { log "the game exited during startup"; return 1; }
        [ "$n" -ge 600 ] && { log "no bridge answer 600 s after launch"; return 1; }
    done
}

wait_level_loaded() {
    local n=0
    until level_loaded; do
        sleep 5; n=$((n + 5))
        [ "$n" -ge "${1:-600}" ] && return 1
        health >/dev/null || return 1
    done
}

autopilot_city_dir() {
    # The one city this supervisor owns: a city.md with "autopilot: on".
    grep -l '^autopilot: on$' cities/*/city.md 2>/dev/null | head -n 1 | xargs -n1 dirname 2>/dev/null
}

cheat_mods() {
    # Enabled built-in cheat mods, as reported by /health enabledMods (empty when none or unknown).
    health | jq -r '.enabledMods[]? // empty' 2>/dev/null |
        grep -i -E 'unlimited|unlock all|unlockall' | grep -v -i 'unlimited money' || true
}

# Ensure the game is up with the autopilot city loaded, or (no autopilot city yet) start one.
# Sets: city_id city_name dir. Returns 1 to mean "not ready, try again later".
LAUNCHED=0
ensure_city() {
    local h id name owned saved owned_id
    owned=$(autopilot_city_dir)
    owned_id=$(sed -n 's/^id: //p' "$owned/city.md" 2>/dev/null | head -n 1)

    if ! health >/dev/null; then
        if [ -n "$owned" ]; then launch_game continue || return 1
        else launch_game menu || return 1
        fi
        LAUNCHED=1
        sleep 10
    fi

    h=$(health)
    if ! printf '%s' "$h" | jq -e '.levelLoaded == true' >/dev/null 2>&1; then
        if [ -n "$owned" ]; then
            saved=$(head -n 1 "$ap/save-name" 2>/dev/null)
            if [ -n "$saved" ]; then
                log "at the main menu; loading save $saved"
                api_post /commands/load-save "$(jq -n --arg n "$saved" '{name:$n}')" >/dev/null || log "load-save failed"
                LAUNCHED=1
            fi
        elif [ "$FOUND_NEW" = 1 ]; then
            found_new_game || return 1
            LAUNCHED=1
        else
            status idle "start a new game: Claude will take it over as soon as it loads"
            CHAT_WAIT=25 intake_chat || sleep 5
            return 1
        fi
        wait_level_loaded 900 || return 1
        h=$(health)
    fi

    id=$(printf '%s' "$h" | jq -r '.city.id // empty')
    name=$(printf '%s' "$h" | jq -r '.city.name // empty')
    [ -n "$id" ] || { log "loaded city has no id yet"; return 1; }
    if [ "$LAUNCHED" = 1 ] && [ -n "$owned_id" ] && [ "$id" != "$owned_id" ]; then
        # We relaunched with --continuelastsave and the newest save on disk is another city.
        saved=$(head -n 1 "$ap/save-name" 2>/dev/null)
        log "relaunch loaded $name, not the autopilot city; loading save $saved"
        api_post /commands/load-save "$(jq -n --arg n "$saved" '{name:$n}')" >/dev/null || { log "load-save failed"; return 1; }
        sleep 20; wait_level_loaded 900 || return 1
        h=$(health); id=$(printf '%s' "$h" | jq -r '.city.id // empty'); name=$(printf '%s' "$h" | jq -r '.city.name // empty')
        [ "$id" = "$owned_id" ] || { log "load-save did not bring back the autopilot city"; return 1; }
    fi
    if [ "$LAUNCHED" = 1 ] && health | jq -e '.levelLoaded == true' >/dev/null 2>&1; then
        LAUNCHED=0
        if [ ! -f "$ap/paused" ] && api_get /state/summary 20 | jq -e '.simulation.paused == true' >/dev/null 2>&1; then
            log "the game loaded paused; unpausing"
            api_post /commands/set-simulation-speed '{"paused":false,"speed":1}' >/dev/null || true
        fi
    fi
    city_id="$id"; city_name="$name"
    dir=$(grep -l "^id: ${id}$" cities/*/city.md 2>/dev/null | head -n 1 | xargs -n1 dirname 2>/dev/null)

    if [ -z "$dir" ] && [ -z "$owned" ] && [ ! -f "$ap/founding" ] &&
       ! grep -q -i -x "name: $name" cities/*/city.md 2>/dev/null; then
        log "adopting the new city the player started: $name ($id)"
        # A name the player asked for (tmp/autopilot/new-city-name) wins over the game's own.
        want=$(head -n 1 "$ap/new-city-name" 2>/dev/null)
        echo "${want:-$name}" >"$ap/save-name"
        touch "$ap/founding"
    fi
    if [ -z "$dir" ] && [ -z "$owned" ] && [ -f "$ap/founding" ]; then
        # The game we just started for the new city.
        create_city_dir "$id" "$(head -n 1 "$ap/new-city-name" 2>/dev/null || echo "$name")"
        rm -f "$ap/founding" "$ap/new-city-name"
        # First save before any model turn, so a crash can never fall back to another city.
        ./scripts/autopilot-save.sh >>"$dir/autopilot/save.log" 2>&1 || log "first save failed (see $dir/autopilot/save.log)"
    fi
    return 0
}

slugify() { printf '%s' "$1" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//'; }

create_city_dir() {
    local id="$1" name="$2" slug save
    slug="$(slugify "$name")-${id:0:8}"
    dir="cities/$slug"
    mkdir -p "$dir/autopilot/turns" "$dir/cache"
    for f in progress.md plan.md lessons.md; do
        [ -f "$dir/$f" ] || sed "s/<city name>/$name/g; s/<name>/$name/g" "templates/city/$f" >"$dir/$f"
    done
    [ -f templates/city/cache-README.md ] && cp -n templates/city/cache-README.md "$dir/cache/README.md"
    save=$(head -n 1 "$ap/save-name" 2>/dev/null)
    [ -n "$save" ] || { save="$name"; echo "$save" >"$ap/save-name"; }
    sed "s/^id: unknown$/id: $id/; s/^bindOnLoad: true$/bindOnLoad: false/; s/<name>/$name/g" templates/city/city.md >"$dir/city.md"
    # Header line the supervisor keys on, then the player's standing orders verbatim.
    sed -i '' "s/^bindOnLoad: false$/bindOnLoad: false\\
autopilot: on/" "$dir/city.md"
    # The game mods the player created this city with (their own setup).
    local pm
    pm=$(health | jq -r '[.enabledMods[]? | select(test("unlimited|unlock all"; "i"))] | join(",")' 2>/dev/null)
    sed -i '' "s/^autopilot: on$/autopilot: on\\
player-mods: ${pm}/" "$dir/city.md"
    cat >>"$dir/city.md" <<EOF

## Standing orders (player, 2026-10-17, binding)

- Full autonomy: Claude has all decision-making power over this city. Never ask the player a question or wait for input. Wherever a repo doc says "ask the player", decide, then log the decision in autopilot/journal.md.
- ONE save only: \`$save\`, always overwritten. Never create another save name or version. Save only with ./scripts/autopilot-save.sh.
- Fair play: no cheats and no gameplay mods, with ONE exception the player granted on 2026-10-17 ("we will start a new game with unlimited currency"): the built-in Unlimited Money mod. Money is no constraint; everything else is played honestly: use only unlocked buildings and policies, buy tiles only when the milestone allows, never use a bridge bypass flag (ignoreUnlock, ignoreMilestoneLimit, ignoreMaxAreaCount, or any other).
- No git commands.
- The player talks to us only through the in-game chat. Their chat messages outrank the plan.
- City goal (player, 2026-10-17): "maximum tourism and pedestrian access to monuments. we want to make this a destination city with easy access to all monuments. you have free reign over the city. plan for growth to 100k residents and have a city master plan and transportation plan based on the layout of the game. maximize all forms of public transportation and all geographical advantages."
- Zoning rule (player, 2026-10-17): "do not overzone- build in waves and ensure zoning for areas stays moderate. do not build houses next to industrial." Zone in small waves only when demand calls for it, keep every demand bar moderate (never zone a type whose demand is low), and keep residential zones away from industry with a buffer (offices, commercial, parks or open land between them, never a shared road frontage).
- Monitoring (player, 2026-10-17): "create a plan to monitor city progress and ensure we hit our goals". The plan carries a KPI scorecard with targets per population milestone; the supervisor samples autopilot/metrics.jsonl every 10 minutes; every mayor review scores the KPIs and corrects course when one is off track.
EOF
    log "founded $dir (save $save)"
}

found_new_game() {
    # No autopilot city exists and the game is at the main menu: the mayor picks the map and the
    # name, then the bridge starts the game.
    local cheats maps choice map cname
    cheats=$(cheat_mods)
    if [ -n "$cheats" ]; then
        log "refusing to start a new game: cheat mods enabled: $cheats"
        return 1
    fi
    maps=$(api_get /state/maps 30) || { log "GET /state/maps failed (mod too old?)"; return 1; }
    run_turn founder "$MAYOR_MODEL" "$(founder_prompt "$maps")" "$ap/founder" 1800 || return 1
    choice="$ap/founder/choice.json"
    map=$(jq -r '.map // empty' "$choice" 2>/dev/null)
    cname=$(jq -r '.cityName // empty' "$choice" 2>/dev/null)
    [ -n "$map" ] && [ -n "$cname" ] || { log "founder wrote no usable choice.json"; return 1; }
    echo "$cname" >"$ap/save-name"
    touch "$ap/founding"
    log "starting new game: map=$map city=$cname"
    api_post /commands/new-game "$(jq -n --arg m "$map" --arg c "$cname" '{map:$m, cityName:$c}')" >"$ap/founder/new-game.json" ||
        { log "new-game failed: $(cat "$ap/founder/new-game.json")"; rm -f "$ap/founding"; return 1; }
}

# ------------------------------------------------------------------------------- turns

COMMON_DENY=(
    "Bash(git *)" "Bash(rm *)" "Bash(sudo *)"
    "Bash(./scripts/autopilot.sh*)" "Bash(./scripts/chat-bridge.sh)" "Bash(./scripts/chat-bridge.sh --*)"
    "Bash(./scripts/restart-game.sh*)" "Bash(./scripts/start-*)" "Bash(./scripts/save-city.sh*)"
    "Bash(./scripts/install-*)" "Bash(./scripts/build.sh*)"
    "Bash(curl *commands/save*)" "Bash(curl *new-game*)" "Bash(curl *load-save*)" "Bash(curl *ignore*)"
    "mcp__cs1-bridge__cs1_save" "mcp__cs1-bridge__cs1_new_game" "mcp__cs1-bridge__cs1_load_save"
    "Bash(./scripts/bench-*)" "Bash(./scripts/develop-*)" "Bash(./scripts/repair-*)" "Bash(./scripts/smoke-test*)"
    "Bash(./scripts/phase4-orchestrator*)" "Bash(./scripts/codex-*)" "Bash(./scripts/check-doc-links*)"
    "mcp__cs1-bridge__cs1_chat_inbox" "mcp__cs1-bridge__cs1_chat_say" "mcp__cs1-bridge__cs1_chat_status"
    "Agent" "Task" "WebFetch" "WebSearch" "AskUserQuestion"
)
MUTATING_TOOLS=(
    "mcp__cs1-bridge__cs1_build_grid" "mcp__cs1-bridge__cs1_build_neighborhood" "mcp__cs1-bridge__cs1_build_network"
    "mcp__cs1-bridge__cs1_bulldoze" "mcp__cs1-bridge__cs1_connect" "mcp__cs1-bridge__cs1_move_building"
    "mcp__cs1-bridge__cs1_place_building" "mcp__cs1-bridge__cs1_repair_zone_clusters"
    "mcp__cs1-bridge__cs1_repair_zones_to_growables" "mcp__cs1-bridge__cs1_set_building_active"
    "mcp__cs1-bridge__cs1_set_policy" "mcp__cs1-bridge__cs1_set_service_budget" "mcp__cs1-bridge__cs1_set_simulation_speed"
    "mcp__cs1-bridge__cs1_set_tax_rate" "mcp__cs1-bridge__cs1_set_zone" "mcp__cs1-bridge__cs1_transit_line_create"
    "mcp__cs1-bridge__cs1_transit_line_delete" "mcp__cs1-bridge__cs1_transit_line_edit" "mcp__cs1-bridge__cs1_unlock_area"
    "mcp__cs1-bridge__cs1_stamp_layout" "mcp__cs1-bridge__cs1_set_building_emptying" "mcp__cs1-bridge__cs1_batch"
    "Bash(curl * -X *)" "Bash(curl * --data*)" "Bash(curl * -d *)"
)

tools_for() {
    # $1 role -> sets ALLOW and DENY arrays.
    # File rules need an absolute path written with a leading "//" (verified 2026-10-17:
    # relative rules and Write(...) rules are ignored and the tool is denied).
    local d="/$repo/${dir:-tmp/autopilot/founder}" r="/$repo"
    ALLOW=("Read" "Grep" "Glob" "Bash(jq *)" "Bash(./scripts/chat-bridge.sh say *)"
           "Bash(curl -sS $BASE/state/*)" "Bash(curl -s $BASE/state/*)" "Bash(curl -sS $BASE/health)"
           "Bash(curl -sS $BASE/capture*)" "Bash(./scripts/review.sh *)")
    DENY=("${COMMON_DENY[@]}")
    case "$1" in
        operator)
            ALLOW+=("mcp__cs1-bridge" "Edit($d/**)" "Edit($r/lessons.md)" "Edit($r/knowledge.md)"
                    "Bash(./scripts/autopilot-save.sh)" "Bash(curl -sS -X POST $BASE/commands/*)"
                    "Bash(test -s tmp/autopilot/chat-queue.jsonl)") ;;
        mayor|founder)
            # Read-only for the game. The repo's settings.local.json allows Bash(./scripts/*)
            # and POST curls; deny beats allow, so all of it is denied here.
            ALLOW+=("mcp__cs1-bridge" "Edit($d/**)" "Edit($r/tmp/autopilot/founder/**)" "Edit($r/lessons.md)")
            DENY+=("${MUTATING_TOOLS[@]}" "Bash(./scripts/*)" "Bash(curl *POST*)") ;;
        planner)
            ALLOW+=("mcp__cs1-bridge" "Edit($d/plan.md)" "Edit($d/autopilot/plan-review.md)")
            DENY+=("${MUTATING_TOOLS[@]}" "Bash(./scripts/*)" "Bash(curl *POST*)") ;;
    esac
}

is_usage_limit() {
    # $1 output file. True when the turn ended on a usage/rate limit or overload.
    grep -q -i -E 'usage limit|rate.limit|limit reached|limit will reset|resets at|overloaded|"status":(429|529)|credit balance|out of extra usage' \
        "$1" "$1.err" 2>/dev/null
}

run_watched() {
    # $1 output, $2 wall seconds, rest claude args. Returns claude's rc, 124 stalled, 125 wall.
    local out="$1" wall="$2"; shift 2
    rm -f "$out.stalled" "$out.wall" "$out.gamedown"
    (exec "$CLAUDE_BIN" "$@") >"$out" 2>"$out.err" &
    local pid=$!; turn_pid=$pid
    (
        trap 'exit 0' TERM
        size=-1; idle=0; total=0; down=0
        while kill -0 "$pid" 2>/dev/null; do
            sleep 5; total=$((total + 5))
            # A crashed game makes every tool call fail fast, which looks like activity.
            if [ $((total % 15)) -eq 0 ]; then
                if curl -sS --fail --max-time 5 "$BASE/health" >/dev/null 2>&1; then down=0; else down=$((down + 1)); fi
                if [ "$down" -ge 3 ]; then touch "$out.gamedown"; stop_tree "$pid"; exit 0; fi
            fi
            now=$(wc -c <"$out" 2>/dev/null | tr -d ' ')
            if [ "$now" != "$size" ]; then size="$now"; idle=0; else idle=$((idle + 5)); fi
            if [ "$idle" -ge "$STALL_TIMEOUT" ]; then touch "$out.stalled"; stop_tree "$pid"; exit 0; fi
            if [ "$total" -ge "$wall" ]; then touch "$out.wall"; stop_tree "$pid"; exit 0; fi
        done
    ) &
    local guard=$! rc=0
    wait "$pid" || rc=$?
    turn_pid=""
    kill "$guard" 2>/dev/null; wait "$guard" 2>/dev/null
    [ -f "$out.gamedown" ] && { rm -f "$out.gamedown"; return 126; }
    [ -f "$out.stalled" ] && { rm -f "$out.stalled"; return 124; }
    [ -f "$out.wall" ] && { rm -f "$out.wall"; return 125; }
    return "$rc"
}

turn_result() { jq -c 'select(.type == "result")' "$1" 2>/dev/null | tail -n 1; }
turn_session() { jq -r 'select(.session_id != null) | .session_id' "$1" 2>/dev/null | head -n 1; }

LAST_REPLY=""
run_turn() {
    # $1 role, $2 model, $3 prompt, $4 output dir, $5 wall seconds.
    # Retries forever on usage limits (with backoff); resumes stalls/crashes up to 3 times.
    local role="$1" model="$2" prompt="$3" odir="$4" wall="${5:-$OPERATOR_WALL}"
    local out session="" rc attempt=0 backoff=300 result
    mkdir -p "$odir"
    out="$odir/$(date +%Y%m%d-%H%M%S)-$role.jsonl"
    tools_for "$role"
    local -a args=(-p "$prompt" --model "$model" --output-format stream-json --verbose
                   --strict-mcp-config --mcp-config "$mcp_config"
                   --permission-mode dontAsk --allowedTools "${ALLOW[@]}" --disallowedTools "${DENY[@]}")
    LAST_REPLY=""
    status working "$role ($model)"
    start_watcher
    while :; do
        rc=0
        if [ -n "$session" ]; then
            run_watched "$out" "$wall" "${args[@]}" --resume "$session" || rc=$?
        else
            run_watched "$out" "$wall" "${args[@]}" || rc=$?
        fi
        [ -n "$session" ] || session=$(turn_session "$out")
        result=$(turn_result "$out")
        if [ "$rc" = 126 ]; then
            log "$role: the game stopped answering mid-turn; stopping the turn to recover the game"
            stop_watcher; status idle ""
            return 2
        fi

        if is_usage_limit "$out" && { [ -z "$result" ] || printf '%s' "$result" | jq -e '.is_error == true' >/dev/null 2>&1; }; then
            log "$role: usage limit; sleeping $((backoff / 60)) min"
            status working "paused: Claude usage limit, retrying at $(date -v+${backoff}S +%H:%M)"
            cp -f "$out" "$out.limit-$(date +%s)" 2>/dev/null
            sleep "$backoff"
            [ "$backoff" -lt 1800 ] && backoff=$((backoff * 2)); [ "$backoff" -gt 1800 ] && backoff=1800
            session=""   # a fresh start re-reads the files; nothing was lost
            continue
        fi
        if [ -n "$result" ] && [ "$rc" != 124 ] && [ "$rc" != 125 ]; then
            LAST_REPLY=$(printf '%s' "$result" | jq -r '.result // empty' 2>/dev/null)
            break
        fi
        attempt=$((attempt + 1))
        if [ "$attempt" -gt 3 ]; then
            log "$role: gave up after 3 restarts (rc $rc)"
            stop_watcher; status idle ""
            return 1
        fi
        log "$role: turn ended without a result (rc $rc); resuming ($attempt/3)"
        cp -f "$out" "$out.attempt-$attempt" 2>/dev/null
        sleep $((attempt * 10))
        if [ -n "$session" ]; then
            args=(-p "Your previous attempt was interrupted ($( [ "$rc" = 125 ] && echo 'wall-clock limit' || echo "exit $rc")). Check the city's current state before repeating anything, finish the task as briefly as you can, and write your output files as instructed." \
                  --model "$model" --output-format stream-json --verbose --permission-mode dontAsk
                  --strict-mcp-config --mcp-config "$mcp_config"
                  --allowedTools "${ALLOW[@]}" --disallowedTools "${DENY[@]}")
        fi
    done
    stop_watcher
    status idle "autopilot running"
    return 0
}

prompt_file() {
    # $1 template name, then KEY=VALUE pairs substituted for {{KEY}} (literal, any characters).
    local t="scripts/prompts/autopilot-$1.md"; shift
    python3 -I -c '
import sys
text = open(sys.argv[1]).read() + "\n" + open(sys.argv[2]).read()
for kv in sys.argv[3:]:
    k, _, v = kv.partition("=")
    text = text.replace("{{" + k + "}}", v)
sys.stdout.write(text)
' "scripts/prompts/autopilot-common.md" "$t" "$@"
}

founder_prompt() {
    prompt_file founder DIR="tmp/autopilot/founder" BASE="$BASE" MAPS="$1" CITY="(new city, not created yet)" SAVE="(chosen by you)"
}

# --------------------------------------------------------------------------- bookkeeping

st() { jq -r "$1 // empty" "$dir/autopilot/state.json" 2>/dev/null; }
st_set() {
    # $1 key, $2 JSON value
    local f="$dir/autopilot/state.json" tmp
    [ -f "$f" ] || echo '{}' >"$f"
    tmp=$(jq --arg k "$1" --argjson v "$2" '.[$k] = $v' "$f") && printf '%s\n' "$tmp" >"$f"
}
now() { date +%s; }

next_milestone() {
    local done_ms="$1" m
    for m in $MILESTONES; do [ "$m" -gt "$done_ms" ] && { echo "$m"; return; }; done
    echo $((done_ms + 100000))
}

crisis() {
    # Prints a one-line reason when something needs the mayor now.
    local h pop cash maxpop
    h=$(health); pop=$(printf '%s' "$h" | jq -r '.city.population // 0')
    cash=$(api_get /state/areas 20 | jq -r '.cash // empty' 2>/dev/null)
    maxpop=$(st '.maxPopSinceMayor'); maxpop=${maxpop:-0}
    [ "$pop" -gt "$maxpop" ] && st_set maxPopSinceMayor "$pop"
    if [ -n "$cash" ] && [ "${cash%%.*}" -lt 0 ] 2>/dev/null; then echo "cash is negative ($cash)"; return; fi
    if [ "$maxpop" -gt 1000 ] && [ "$pop" -lt $((maxpop * 95 / 100)) ] && [ $((maxpop - pop)) -gt 200 ]; then
        echo "population fell from $maxpop to $pop"; return
    fi
}

check_saves() {
    # Log (and tell the mayor) about any save file other than the one allowed name that was
    # written since the autopilot city was founded.
    local save since extra
    save=$(head -n 1 "$ap/save-name" 2>/dev/null); since=$(st '.foundedUtc')
    [ -n "$save" ] && [ -n "$since" ] || return 0
    extra=$(api_get /state/saves 20 | jq -r --arg n "$save" --arg s "$since" \
        '[.saves[]? | select(.name != $n and .lastWriteTimeUtc >= $s) | .name] | join(", ")' 2>/dev/null)
    if [ -n "$extra" ] && [ "$extra" != "$(st '.extraSavesSeen')" ]; then
        log "WARNING: extra save files written since founding: $extra"
        st_set extraSavesSeen "$(jq -n --arg e "$extra" '$e')"
        printf '{"supervisor":true,"text":"Supervisor notice: save files other than %s were written since founding: %s. The player allows ONE save. Find out why and stop it (game autosave, or a tool)."}\n' "$save" "$extra" >>"$queue"
    fi
}

sample_metrics() {
    # One compact KPI record every 10 minutes into autopilot/metrics.jsonl (the monitoring plan).
    local f="$dir/autopilot/metrics.jsonl" last h sm tr ar pr
    last=$(st '.lastMetrics'); last=${last:-0}
    [ $(( $(now) - last )) -ge 600 ] || return 0
    st_set lastMetrics "$(now)"
    h=$(health); sm=$(api_get /state/summary 20); tr=$(api_get /state/transit 30); ar=$(api_get /state/areas 20)
    pr=$(api_get "/state/problems?limit=500" 30)
    jq -cn --arg t "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
        --argjson h "${h:-null}" --argjson sm "${sm:-null}" --argjson tr "${tr:-null}" \
        --argjson ar "${ar:-null}" --argjson pr "${pr:-null}" '{
          wall: $t, gameDate: $h.city.gameDate, population: $h.city.population,
          demand: $sm.demand, simulation: $sm.simulation,
          transit: ($tr.cityPassengersByType // null), transitLines: (($tr.lines // []) | length),
          cash: $ar.cash, tilesOwned: ($ar.owned // $ar.ownedCount // null),
          problems: ($pr.total // $pr.count // (($pr.problems // []) | length))
        }' >>"$f" 2>/dev/null || log "metrics sample failed"
}

# --------------------------------------------------------------------------- the loop

log "autopilot starting (mayor $MAYOR_MODEL, operator $OPERATOR_MODEL, planner $PLANNER_MODEL)"
dir=""; city_id=""; city_name=""
not_ready=0

while :; do
    # Picked up edits to this script at a safe point: between turns, never mid-turn.
    if [ "$(stat -f %m "$0" 2>/dev/null)" != "$self_mtime" ] && bash -n "$0" 2>/dev/null; then
        log "script changed on disk; restarting between turns"
        exec /bin/bash "$0"
    fi
    if ! ensure_city; then
        not_ready=$((not_ready + 1))
        [ $((not_ready % 12)) -eq 1 ] && log "waiting for the game / city"
        sleep 20
        continue
    fi
    not_ready=0

    # Requested on/off from the chat for the loaded city.
    if [ -f "$ap/autopilot-request" ] && [ -n "$dir" ]; then
        req=$(cat "$ap/autopilot-request"); rm -f "$ap/autopilot-request"
        owned=$(autopilot_city_dir)
        if [ "$req" = on ] && { [ -z "$owned" ] || [ "$owned" = "$dir" ]; }; then
            grep -q '^autopilot:' "$dir/city.md" && sed -i '' 's/^autopilot: .*/autopilot: on/' "$dir/city.md" ||
                sed -i '' "1a\\
autopilot: on
" "$dir/city.md"
        elif [ "$req" = off ]; then
            sed -i '' 's/^autopilot: on$/autopilot: off/' "$dir/city.md"
        fi
    fi

    on=0
    [ -n "$dir" ] && grep -q '^autopilot: on$' "$dir/city.md" 2>/dev/null && on=1
    CHAT_WAIT=0 intake_chat || true

    if [ "$on" != 1 ]; then
        # Not our city: answer chat read-only, never change anything.
        if [ -s "$queue" ]; then
            msgs=$(cat "$queue"); : >"$queue"
            run_turn mayor "$MAYOR_MODEL" "$(prompt_file chatonly BASE="$BASE" CITY="$city_name" MESSAGES="$msgs" DIR="${dir:-tmp/autopilot}" SAVE="n/a")" "$ap/chatonly-turns" 1800 &&
                say reply "${LAST_REPLY:-Read your message, but I had nothing to add.}" "$(printf '%s\n' "$msgs" | tail -n 1 | jq -r '.id // empty')"
        else
            status idle "autopilot is not driving this city (type /autopilot on)"
            CHAT_WAIT=25 intake_chat || sleep 5
        fi
        continue
    fi

    mkdir -p "$dir/autopilot/turns"
    [ -n "$(st '.foundedUtc')" ] || st_set foundedUtc "\"$(date -u +%Y-%m-%dT%H:%M:%S)\""
    save=$(head -n 1 "$ap/save-name" 2>/dev/null)
    if [ -z "$save" ]; then
        save=$(sed -n 's/.*Save file: *\([^|]*\).*/\1/p' "$dir/city.md" | head -n 1 | sed 's/ *$//')
        [ -n "$save" ] && [ "$save" != "<name>" ] || save="$city_name"
        echo "$save" >"$ap/save-name"
    fi
    check_saves
    sample_metrics
    vars=(BASE="$BASE" CITY="$city_name" DIR="$dir" SAVE="$save")

    # 1. Founding: brief + master plan.
    if ! grep -q '^Goal: [^<]' "$dir/city.md"; then
        run_turn mayor "$MAYOR_MODEL" "$(prompt_file brief "${vars[@]}")" "$dir/autopilot/turns" 2700 || { sleep 60; continue; }
        say update "${LAST_REPLY:-City brief written.}"
        continue
    fi
    if [ "$(st '.planReviewedAt')" = "" ]; then
        run_turn planner "$PLANNER_MODEL" "$(prompt_file planner "${vars[@]}" REASON="founding: write and review the master plan")" "$dir/autopilot/turns" 3600 || { sleep 60; continue; }
        st_set planReviewedAt "$(now)"; st_set milestoneDone 0
        say update "${LAST_REPLY:-Master plan reviewed.}"
        rm -f "$dir/autopilot/next.json"   # the mayor re-plans against the new plan
        continue
    fi

    # 2. Milestone review by the planner.
    pop=$(health | jq -r '.city.population // 0')
    ms_done=$(st '.milestoneDone'); ms_done=${ms_done:-0}
    ms_next=$(next_milestone "$ms_done")
    if [ "$pop" -ge "$ms_next" ]; then
        say update "Population passed $ms_next. Fable is reviewing the master plan."
        run_turn planner "$PLANNER_MODEL" "$(prompt_file planner "${vars[@]}" REASON="milestone: population passed $ms_next (now $pop)")" "$dir/autopilot/turns" 3600 || { sleep 60; continue; }
        st_set milestoneDone "$ms_next"; st_set planReviewedAt "$(now)"
        say update "${LAST_REPLY:-Plan updated for the next stage.}"
        rm -f "$dir/autopilot/next.json"
        continue
    fi

    # 3. Does the mayor need to decide?
    why=""
    [ -s "$queue" ] && why="player chat"
    [ -z "$why" ] && why=$(crisis)
    [ -z "$why" ] && [ ! -f "$dir/autopilot/next.json" ] && why="no current orders"
    if [ -z "$why" ]; then
        rs=$(jq -r '.status // empty' "$dir/autopilot/report.json" 2>/dev/null)
        rt=$(stat -f %m "$dir/autopilot/report.json" 2>/dev/null || echo 0)
        nt=$(stat -f %m "$dir/autopilot/next.json" 2>/dev/null || echo 0)
        [ "$rt" -gt "$nt" ] && [ -n "$rs" ] && [ "$rs" != partial ] && why="operator reported $rs"
        # Two partial reports in a row on the same orders: let the mayor look instead of looping.
        if [ "$rt" -gt "$nt" ] && [ "$rs" = partial ]; then
            pc=$(st '.partialCount'); pc=$(( ${pc:-0} + 1 )); st_set partialCount "$pc"
            [ "$pc" -ge 2 ] && why="operator reported partial twice in a row"
        fi
        if [ -n "$why" ] && [ "$rs" != done ]; then
            h_orders=$(shasum "$dir/autopilot/orders.md" 2>/dev/null | cut -c1-12)
            if [ "$h_orders" = "$(st '.blockedHash')" ]; then
                prev_b=$(st '.blockedCount'); n_b=$(( ${prev_b:-0} + 1 ))
            else
                n_b=1
            fi
            st_set blockedHash "\"$h_orders\""; st_set blockedCount "$n_b"
            if [ "$n_b" -ge 2 ]; then
                log "same orders failed twice; forcing a 20 min wait before the mayor re-plans"
                st_set forceNote '"The same orders (unchanged orders.md) were blocked or failed twice in a row. Do not reissue them: change the approach, the site, or the goal."'
                st_set blockedCount 0
                echo '{"action":"wait","wakeMinutes":20}' >"$dir/autopilot/next.json"
                why=""
            fi
        fi
    fi
    last_mayor=$(st '.lastMayor'); last_mayor=${last_mayor:-0}
    [ -z "$why" ] && [ $(( $(now) - last_mayor )) -ge $((MAYOR_EVERY * 60)) ] && why="periodic review"

    if [ -n "$why" ]; then
        msgs=""
        if [ -s "$queue" ]; then msgs=$(cat "$queue"); : >"$queue"; fi
        log "mayor turn: $why"
        note=$(st '.forceNote')
        [ -n "$note" ] && why="$why. $note"
        if run_turn mayor "$MAYOR_MODEL" "$(prompt_file mayor "${vars[@]}" REASON="$why" MESSAGES="${msgs:-none}")" "$dir/autopilot/turns" 2700; then
            st_set lastMayor "$(now)"; st_set maxPopSinceMayor 0; st_set partialCount 0; st_set forceNote '""'
            last_id=$(printf '%s\n' "$msgs" | jq -r 'select(.id != null) | .id' 2>/dev/null | tail -n 1)
            if [ -n "$LAST_REPLY" ]; then
                if [ -n "$last_id" ]; then say reply "$LAST_REPLY" "$last_id"; else say update "$LAST_REPLY"; fi
            fi
            # Mark every earlier message answered too, so a restart does not re-read them.
            for mid in $(printf '%s\n' "$msgs" | jq -r 'select(.id != null) | .id' 2>/dev/null | sed '$d'); do
                say reply "(answered with #$last_id)" "$mid" || true
            done
            if ! jq -e '.action == "operate" or .action == "wait"' "$dir/autopilot/next.json" >/dev/null 2>&1; then
                log "mayor left no valid next.json; defaulting to wait 10 min"
                echo '{"action":"wait","wakeMinutes":10}' >"$dir/autopilot/next.json"
            fi
        else
            # Put the messages back so they are not lost.
            [ -n "$msgs" ] && printf '%s\n' "$msgs" >>"$queue"
            sleep 60
        fi
        continue
    fi

    # 4. Act on the mayor's decision.
    action=$(jq -r '.action // "wait"' "$dir/autopilot/next.json" 2>/dev/null)
    if [ "$action" = operate ] && [ -f "$ap/paused" ]; then action=wait; fi
    if [ "$action" = operate ]; then
        cheats=$(cheat_mods)
        # The player created this city themselves with these mods on and said "work in it"
        # (2026-10-17). Mods listed in the city's "player-mods:" header line are the player's
        # own setup for that city and do not block; any other cheat mod still does.
        allowed=$(sed -n 's/^player-mods: //p' "$dir/city.md" | head -n 1)
        if [ -n "$cheats" ]; then
            blocked=$(printf '%s\n' "$cheats" | while IFS= read -r c; do
                case ",$allowed," in (*",$c,"*) ;; (*) echo "$c" ;; esac
            done)
        else
            blocked=""
        fi
        if [ -n "$blocked" ]; then
            say update "Cheat mods are enabled that this city was not created with ($blocked), so I'm not building until they're turned off."
            CHAT_WAIT=25 intake_chat; sleep 300; continue
        fi
        log "operator turn"
        rm -f "$dir/autopilot/report.json"
        op_rc=0
        run_turn operator "$OPERATOR_MODEL" "$(prompt_file operator "${vars[@]}")" "$dir/autopilot/turns" "$OPERATOR_WALL" || op_rc=$?
        if [ "$op_rc" = 2 ]; then
            continue   # the game went down mid-turn: recover it, then the operator resumes the same orders
        fi
        if [ "$op_rc" = 0 ]; then
            [ -f "$dir/autopilot/report.json" ] ||
                jq -n --arg s "${LAST_REPLY:0:1500}" '{status:"partial", summary:("operator wrote no report; its last words: " + $s), saved:false}' >"$dir/autopilot/report.json"
            # The supervisor saves after every operator turn regardless; one file, overwritten.
            ./scripts/autopilot-save.sh >>"$dir/autopilot/save.log" 2>&1 || log "post-turn save failed (see save.log)"
            [ -n "$LAST_REPLY" ] && say update "${LAST_REPLY:0:1500}"
        else
            jq -n '{status:"failed", summary:"operator turn did not finish after restarts", saved:false}' >"$dir/autopilot/report.json"
        fi
        continue
    fi

    # wait: the city grows; wake early on chat. Re-check crisis/milestones every 5 min.
    wake=$(jq -r '.wakeMinutes // 10' "$dir/autopilot/next.json" 2>/dev/null)
    case "$wake" in ''|*[!0-9]*) wake=10 ;; esac
    [ "$wake" -lt 1 ] && wake=1; [ "$wake" -gt 120 ] && wake=120
    started_wait=$(stat -f %m "$dir/autopilot/next.json")
    if [ $(( $(now) - started_wait )) -ge $((wake * 60)) ]; then
        # Waited long enough: the mayor looks again.
        rm -f "$dir/autopilot/next.json"
        continue
    fi
    status idle "watching the city grow; next look $(date -r $((started_wait + wake * 60)) +%H:%M)"
    t_end=$(( $(now) + 300 ))
    while [ "$(now)" -lt "$t_end" ] && [ ! -s "$queue" ]; do
        CHAT_WAIT=25 intake_chat || sleep 5
        [ -s "$queue" ] && break
        level_loaded || break
        sample_metrics
    done
done
