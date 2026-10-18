#!/usr/bin/env bash
#
# Answer the in-game Claude chat panel with headless `claude -p` when no interactive
# Claude Code session is watching it. Long-polls the bridge for player messages, runs one
# Claude turn per message from the repo root (continuing the same conversation), and posts
# the answer back into the panel.
#
# Usage: ./scripts/chat-bridge.sh [options]
#        ./scripts/chat-bridge.sh say <reply|update|status> <text> [inReplyTo]
#        ./scripts/chat-bridge.sh status <idle|thinking|working|offline> [text]
#
#   --base-url URL          bridge base URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
#   --once                  handle one player message, then exit
#   --model MODEL           passed through to claude --model
#   --permission-mode MODE  passed through to claude (default: dontAsk, which denies every
#                           tool not pre-approved; the script pre-approves only the cs1-bridge
#                           MCP tools, Read/Grep/Glob, curl to the bridge, and its own `say`)
#   --accept-api            also answer messages sent with POST /chat/send (source "api");
#                           by default only messages typed in the game panel are answered
#   --new-session           start a fresh Claude conversation instead of resuming the last one
#   --wait SECONDS          long-poll length per request, 1..30 (default: 25)
#   --stall-timeout SECONDS restart a Claude turn that shows no activity for this long, >= 30
#                           (default: 600). Turns that keep working are never cut off.
#   --max-restarts N        stalled or crashed turns resumed per message before giving up
#                           (default: 5)
#   -h, --help              show this help
#
# The `say` and `status` forms post one line to the panel and exit; the headless Claude uses
# `say update` to report progress while it works.
#
# Needs curl, jq and the claude CLI (override the binary with CLAUDE_BIN=/path/to/claude).
# Ctrl-C sets the panel's status to offline.
#
# The loop is meant to run unattended: it waits for the game instead of exiting when the
# bridge is down, picks up again after the game restarts, acknowledges messages that arrive
# while Claude is busy, and recovers a turn that stalls or crashes by resuming the same
# conversation where it left off (long turns that keep working are never cut off). To keep it running
# in the background on macOS (restarted on crash and at login), use
# ./scripts/install-chat-bridge-agent.sh.
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
ONCE=0
MODEL=""
PERMISSION_MODE="dontAsk"
ACCEPT_API=0
NEW_SESSION=0
WAIT=25
STALL_TIMEOUT=600
MAX_RESTARTS=5
CLAUDE_BIN="${CLAUDE_BIN:-claude}"

usage() { awk 'NR <= 2 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0" | sed '${/^$/d;}'; }
die() { echo "error: $*" >&2; exit 1; }
need() { [ "$1" -ge 2 ] || { echo "missing value for $2" >&2; usage >&2; exit 2; }; }
log() { echo "[chat-bridge $(date +%H:%M:%S)] $*" >&2; }

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
state_dir="${CHAT_BRIDGE_STATE_DIR:-$repo/tmp/chat-bridge}"
session_file="$state_dir/session-id"
acked_file="$state_dir/acked-ids"

api_get() { curl -sS --fail-with-body --max-time "$((WAIT + 10))" "${BASE}$1"; }
api_post() { curl -sS --fail-with-body --max-time 15 -X POST -H 'Content-Type: application/json' --data "$2" "${BASE}$1"; }

post_say() {
    # $1 kind, $2 text, $3 optional inReplyTo
    local body
    if [ -n "${3:-}" ]; then
        body=$(jq -n --arg text "$2" --arg kind "$1" --argjson reply "$3" '{text: $text[0:4000], kind: $kind, inReplyTo: $reply}')
    else
        body=$(jq -n --arg text "$2" --arg kind "$1" '{text: $text[0:4000], kind: $kind}')
    fi
    api_post "/chat/say" "$body" >/dev/null
}

post_status() {
    # $1 state, $2 optional text. Never fatal: a status line is not worth dying over.
    local body
    body=$(jq -n --arg state "$1" --arg text "${2:-}" '{state: $state, text: $text[0:200]}')
    api_post "/chat/status" "$body" >/dev/null 2>&1 || true
}

# --- one-shot subcommands -----------------------------------------------------

if [ "${1:-}" = "say" ] || [ "${1:-}" = "status" ]; then
    command -v jq >/dev/null 2>&1 || die "jq is required: brew install jq"
    BASE="${BASE%/}"
    if [ "$1" = "say" ]; then
        [ $# -ge 3 ] || die "usage: chat-bridge.sh say <reply|update|status> <text> [inReplyTo]"
        case "$2" in reply|update|status) ;; *) die "kind must be reply, update or status" ;; esac
        if [ -n "${4:-}" ]; then
            case "$4" in *[!0-9]*) die "inReplyTo must be a message id" ;; esac
        fi
        post_say "$2" "$3" "${4:-}" || die "could not post to ${BASE}/chat/say"
    else
        [ $# -ge 2 ] || die "usage: chat-bridge.sh status <idle|thinking|working|offline> [text]"
        body=$(jq -n --arg state "$2" --arg text "${3:-}" '{state: $state, text: $text[0:200]}')
        api_post "/chat/status" "$body" >/dev/null || die "could not post to ${BASE}/chat/status"
    fi
    exit 0
fi

# --- options ------------------------------------------------------------------

while [ $# -gt 0 ]; do
    case "$1" in
        --base-url)        need $# "$1"; BASE="$2"; shift ;;
        --once)            ONCE=1 ;;
        --model)           need $# "$1"; MODEL="$2"; shift ;;
        --permission-mode) need $# "$1"; PERMISSION_MODE="$2"; shift ;;
        --accept-api)      ACCEPT_API=1 ;;
        --new-session)     NEW_SESSION=1 ;;
        --wait)            need $# "$1"; WAIT="$2"; shift ;;
        --stall-timeout)   need $# "$1"; STALL_TIMEOUT="$2"; shift ;;
        --max-restarts)    need $# "$1"; MAX_RESTARTS="$2"; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done
BASE="${BASE%/}"

case "$WAIT" in ''|*[!0-9]*) echo "--wait must be a whole number of seconds" >&2; exit 2 ;; esac
[ "$WAIT" -ge 1 ] && [ "$WAIT" -le 30 ] || { echo "--wait must be 1..30" >&2; exit 2; }
case "$STALL_TIMEOUT" in ''|*[!0-9]*) echo "--stall-timeout must be a whole number of seconds" >&2; exit 2 ;; esac
[ "$STALL_TIMEOUT" -ge 30 ] || { echo "--stall-timeout must be at least 30" >&2; exit 2; }
case "$MAX_RESTARTS" in ''|*[!0-9]*) echo "--max-restarts must be a whole number" >&2; exit 2 ;; esac
case "$PERMISSION_MODE" in
    bypassPermissions) log "warning: bypassPermissions lets the headless Claude run any command a player message talks it into." ;;
esac

command -v curl >/dev/null 2>&1 || die "curl is required"
command -v jq >/dev/null 2>&1 || die "jq is required: brew install jq"
command -v "$CLAUDE_BIN" >/dev/null 2>&1 || die "the claude CLI was not found (set CLAUDE_BIN=/path/to/claude)"

# The headless Claude's `./scripts/chat-bridge.sh say` calls inherit this, so they reach the
# same bridge. (The cs1_* MCP tools take their URL from .mcp.json instead.)
export CS1_BRIDGE_URL="$BASE"

mkdir -p "$state_dir"
if [ "$NEW_SESSION" = 1 ]; then
    rm -f "$session_file"
fi
: >"$acked_file"

# Block until the bridge answers. The game may not be running yet or may be restarting;
# waiting (rather than exiting) keeps a supervised loop from crash-looping.
wait_for_bridge() {
    local waited=0
    until curl -sS --fail --max-time 5 "${BASE}/health" >/dev/null 2>&1; do
        if [ $((waited % 60)) -eq 0 ]; then
            log "waiting for the bridge at $BASE (start Cities: Skylines with the mod enabled)"
        fi
        sleep 5
        waited=$((waited + 5))
    done
}

wait_for_bridge

# --- shutdown -------------------------------------------------------------------

watcher_pid=""
turn_pid=""
poll_pid=""

# Stop a process and its direct children (claude's MCP servers), escalating to KILL.
stop_tree() {
    local pid="$1"
    [ -n "$pid" ] || return 0
    pkill -TERM -P "$pid" 2>/dev/null || true
    kill -TERM "$pid" 2>/dev/null || true
    local n=0
    while kill -0 "$pid" 2>/dev/null && [ "$n" -lt 20 ]; do
        sleep 0.5
        n=$((n + 1))
    done
    pkill -KILL -P "$pid" 2>/dev/null || true
    kill -KILL "$pid" 2>/dev/null || true
}

stop_watcher() {
    if [ -n "$watcher_pid" ]; then
        kill "$watcher_pid" 2>/dev/null || true
        wait "$watcher_pid" 2>/dev/null || true
        watcher_pid=""
    fi
}

shutdown() {
    trap - INT TERM
    [ -n "$poll_pid" ] && kill "$poll_pid" 2>/dev/null
    stop_watcher
    stop_tree "$turn_pid"
    post_status offline "chat-bridge stopped"
    log "stopped; panel status set to offline"
    exit 130
}
trap shutdown INT TERM
# Never leave the watcher orphaned, whatever ends the script.
trap stop_watcher EXIT

# While a Claude turn runs the main loop is not polling, so this watcher does two jobs.
# Its long polls count as heartbeats, which keeps the panel from declaring Claude offline
# (it does that after 120 s without a chat call). And every message that arrives meanwhile
# gets an immediate "queued" line, so the player knows it was received. Each message is
# acknowledged once, even if it waits through several turns. Children run in the
# background and are waited on, so a TERM stops the watcher at once, not after a poll.
start_watcher() {
    local busy_id="$1"
    (
        child=""
        trap '[ -n "$child" ] && kill "$child" 2>/dev/null; exit 0' TERM
        after="$busy_id"
        inbox_file="$state_dir/watcher-inbox.json"
        while :; do
            curl -sS --fail --max-time 25 "${BASE}/chat/inbox?after=${after}&wait=20&unanswered=true" \
                >"$inbox_file" 2>/dev/null & child=$!
            if ! wait "$child"; then
                child=""
                sleep 5 & child=$!
                wait "$child" || true
                child=""
                continue
            fi
            child=""
            for id in $(jq -r '.messages[].id' "$inbox_file" 2>/dev/null || true); do
                case "$id" in ''|*[!0-9]*) continue ;; esac
                grep -qx "$id" "$acked_file" 2>/dev/null && continue
                echo "$id" >>"$acked_file"
                post_say update "Got message #$id. Finishing #$busy_id first, then I'll pick this up." "$id" || true
            done
            next=$(jq -r '.lastId // empty' "$inbox_file" 2>/dev/null || true)
            case "$next" in ''|*[!0-9]*) ;; *) [ "$next" -gt "$after" ] && after="$next" ;; esac
        done
    ) &
    watcher_pid=$!
}

# --- the Claude turn ----------------------------------------------------------------

curl_pattern_base="${BASE}"
allowed_tools=(
    "mcp__cs1-bridge"
    "Read"
    "Grep"
    "Glob"
    "Bash(./scripts/chat-bridge.sh say *)"
    "Bash(./scripts/chat-bridge.sh status *)"
    "Bash(curl -sS ${curl_pattern_base}/*)"
    "Bash(curl -s ${curl_pattern_base}/*)"
)

build_prompt() {
    # $1 = one inbox message as compact JSON
    local message="$1"
    local id text camera selected game_time
    id=$(printf '%s' "$message" | jq -r '.id')
    text=$(printf '%s' "$message" | jq -r '.text')
    camera=$(printf '%s' "$message" | jq -c '.camera')
    selected=$(printf '%s' "$message" | jq -c '.selected')
    game_time=$(printf '%s' "$message" | jq -r '.gameTime // "unknown"')

    cat <<EOF
You are the in-game city assistant for a Cities: Skylines 1 city, talking to the player
through the chat panel inside the game. Your working directory is the Skylines Agent Bridge
repo. Before acting, read CLAUDE.md, city.md, lessons.md, knowledge.md and transit.md (whichever
exist) if you have not already in this conversation, and follow them.

Act on the city with the cs1_* MCP tools (cs1-bridge server). If a tool you need is missing,
use curl against ${BASE} as documented in docs/api.md. Inspect before you change anything,
and verify with the same state tools afterwards.

While you work, post a short progress line to the player's panel at each meaningful step
(not every tool call) with:
  ./scripts/chat-bridge.sh say update "<one line>"
Do NOT post your final answer yourself. Your final message in this turn is posted to the
panel automatically as the reply, so make it the answer the player should read: plain text,
concise, no Markdown tables, under 4000 characters.

Player message #${id} (game time ${game_time}):
<<<
${text}
>>>
Camera target when sent (world x,z; null if unknown): ${camera}
Entity selected in game when sent ("this"/"here" usually means it): ${selected}
EOF
}

run_watched() {
    # $1 output file, rest: claude arguments. Runs one claude invocation with streaming
    # output (one JSON event per message and tool call) and watches that stream: while
    # events keep arriving the turn runs as long as it needs, but if none arrive for
    # STALL_TIMEOUT seconds it is stuck, so it is stopped. Returns claude's exit code, or
    # 124 if it stalled.
    local out="$1"
    shift
    rm -f "$out.stalled"
    (cd "$repo" && exec "$CLAUDE_BIN" "$@") >"$out" 2>"$out.err" &
    local pid=$!
    turn_pid=$pid
    (
        nap=""
        trap '[ -n "$nap" ] && kill "$nap" 2>/dev/null; exit 0' TERM
        size=-1
        idle=0
        while kill -0 "$pid" 2>/dev/null; do
            sleep 5 & nap=$!
            wait "$nap"
            now=$(wc -c <"$out" 2>/dev/null | tr -d ' ' || echo 0)
            if [ "$now" != "$size" ]; then
                size="$now"
                idle=0
            else
                idle=$((idle + 5))
            fi
            if [ "$idle" -ge "$STALL_TIMEOUT" ]; then
                touch "$out.stalled"
                stop_tree "$pid"
                exit 0
            fi
        done
    ) &
    local guard=$!
    local rc=0
    wait "$pid" || rc=$?
    turn_pid=""
    kill "$guard" 2>/dev/null || true
    wait "$guard" 2>/dev/null || true
    if [ -f "$out.stalled" ]; then
        rm -f "$out.stalled"
        return 124
    fi
    return "$rc"
}

# The last "result" event of a stream-json run, or nothing.
turn_result() { jq -c 'select(.type == "result")' "$1" 2>/dev/null | tail -n 1; }

# The conversation id of a run, available from its first event on.
turn_session() { jq -r 'select(.session_id != null) | .session_id' "$1" 2>/dev/null | head -n 1; }

continue_prompt() {
    # $1 message id, $2 why the previous attempt ended
    cat <<EOF
Your previous attempt at the player's message #$1 $2 before it finished, and was
restarted. Continue that request from where you left off. Some steps may already be
done: check the city's current state with the state tools before repeating anything.
Keep posting progress with ./scripts/chat-bridge.sh say update "...", and end with the
answer for the player as before.
EOF
}

run_claude() {
    # $1 message id, $2 prompt, $3 output file. Resumes the saved conversation when there
    # is one. A turn that stalls or crashes is resumed in the same conversation with a
    # "continue" prompt, up to MAX_RESTARTS times, so work is never silently abandoned.
    # Returns 0 when a result was produced, 124 if every attempt stalled, else the exit code.
    local id="$1" prompt="$2" out="$3" session="" rc=0 attempt=0 why
    local -a base=(--output-format stream-json --verbose --permission-mode "$PERMISSION_MODE" --allowedTools "${allowed_tools[@]}")
    [ -n "$MODEL" ] && base+=(--model "$MODEL")
    [ -f "$session_file" ] && session=$(cat "$session_file")

    while :; do
        rc=0
        if [ -n "$session" ]; then
            run_watched "$out" -p "$prompt" "${base[@]}" --resume "$session" || rc=$?
        else
            run_watched "$out" -p "$prompt" "${base[@]}" || rc=$?
        fi

        local started
        started=$(turn_session "$out")
        if [ -n "$started" ]; then
            session="$started"
            printf '%s' "$session" >"$session_file"
        fi

        if [ -n "$(turn_result "$out")" ]; then
            return 0
        fi

        if [ -z "$started" ] && [ -n "$session" ] && [ "$rc" != 124 ]; then
            # The resume itself failed (session gone or unreadable): start fresh once.
            log "#$id: resuming session $session failed; starting a new conversation"
            rm -f "$session_file"
            session=""
            continue
        fi

        attempt=$((attempt + 1))
        if [ "$attempt" -gt "$MAX_RESTARTS" ]; then
            return "$rc"
        fi

        if [ "$rc" = 124 ]; then
            why="stalled (no activity for ${STALL_TIMEOUT} s)"
        else
            why="stopped unexpectedly (exit $rc)"
        fi
        log "#$id: turn $why; resuming (restart $attempt of $MAX_RESTARTS)"
        post_say update "That step got stuck, so I restarted it and am picking up where I left off." "$id" || true
        post_status working "resuming message #$id"
        cp -f "$out" "$out.attempt-$attempt" 2>/dev/null || true
        sleep $((attempt * 5))
        if [ -n "$session" ]; then
            prompt=$(continue_prompt "$id" "$why")
        fi
    done
}

handle_message() {
    local message="$1"
    local id text source out reply result is_error
    id=$(printf '%s' "$message" | jq -r '.id')
    text=$(printf '%s' "$message" | jq -r '.text')
    source=$(printf '%s' "$message" | jq -r '.source // "panel"')

    if [ "$source" != "panel" ] && [ "$ACCEPT_API" != 1 ]; then
        log "skipping #$id: sent through /chat/send (source=$source); pass --accept-api to answer these"
        return 1
    fi

    log "#$id: $text"
    post_status thinking "reading message #$id"

    out="$state_dir/turn-$id.jsonl"
    start_watcher "$id"
    local rc=0
    run_claude "$id" "$(build_prompt "$message")" "$out" || rc=$?
    stop_watcher

    result=$(turn_result "$out")
    if [ -n "$result" ]; then
        reply=$(printf '%s' "$result" | jq -r '.result // empty' 2>/dev/null || true)
        is_error=$(printf '%s' "$result" | jq -r '.is_error // false' 2>/dev/null || echo true)
        if [ "$is_error" = "true" ] && [ -z "$reply" ]; then
            reply="Claude hit an error on this one. Details: $out"
        fi
    else
        log "#$id: no result after $MAX_RESTARTS restarts (last exit $rc)"
        reply="I kept getting stuck on this one and restarted $MAX_RESTARTS times without finishing. The progress lines above show what got done. Details: $out.err"
    fi

    [ -n "$reply" ] || reply="Done, but Claude ended the turn without a written answer. Details: $out"
    if post_say reply "$reply" "$id"; then
        log "#$id answered (${#reply} chars)"
    else
        log "#$id: could not post the reply to the panel"
    fi
    post_status idle ""
    return 0
}

# --- main loop ----------------------------------------------------------------------

last=0

# The chat store lives in the game's memory, so message ids restart at 1 when the game
# restarts. A stale `last` would then skip every new message; start over instead.
resync() {
    local latest
    latest=$(curl -sS --fail --max-time 5 "${BASE}/chat/status?heartbeat=false" 2>/dev/null |
        jq -r '.latestId // empty' 2>/dev/null || true)
    case "$latest" in ''|*[!0-9]*) return 0 ;; esac
    if [ "$latest" -lt "$last" ]; then
        log "the game restarted (chat ids reset); reading the inbox from the start"
        last=0
        : >"$acked_file"
    fi
}

log "watching ${BASE}/chat/inbox (Ctrl-C to stop)"
post_status idle "chat-bridge is listening"

poll_file="$state_dir/inbox.json"
failures=0
while :; do
    # Poll in the background and wait on it, so Ctrl-C or a launchd stop runs the shutdown
    # trap at once instead of after the long poll ends.
    api_get "/chat/inbox?after=${last}&wait=${WAIT}&unanswered=true" >"$poll_file" 2>/dev/null &
    poll_pid=$!
    poll_rc=0
    wait "$poll_pid" || poll_rc=$?
    poll_pid=""
    response=$(cat "$poll_file" 2>/dev/null || true)
    if [ "$poll_rc" != 0 ]; then
        failures=$((failures + 1))
        if [ "$failures" -ge 3 ]; then
            log "lost the bridge; waiting for the game to come back"
            wait_for_bridge
            resync
            post_status idle "chat-bridge is listening"
            log "bridge is back; watching again"
            failures=0
        else
            sleep 2
        fi
        continue
    fi
    failures=0

    count=$(printf '%s' "$response" | jq '.messages | length' 2>/dev/null || true)
    case "$count" in
        ''|*[!0-9]*) log "unexpected inbox response; retrying"; sleep 2; continue ;;
    esac
    new_last=$(printf '%s' "$response" | jq -r '.lastId // empty' 2>/dev/null || true)

    i=0
    while [ "$i" -lt "$count" ]; do
        message=$(printf '%s' "$response" | jq -c ".messages[$i]")
        id=$(printf '%s' "$message" | jq -r '.id')
        if handle_message "$message" && [ "$ONCE" = 1 ]; then
            exit 0
        fi
        last="$id"
        i=$((i + 1))
    done

    case "$new_last" in
        ''|*[!0-9]*) ;;
        *) [ "$new_last" -gt "$last" ] && last="$new_last" ;;
    esac
done
