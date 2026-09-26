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
#   -h, --help              show this help
#
# The `say` and `status` forms post one line to the panel and exit; the headless Claude uses
# `say update` to report progress while it works.
#
# Needs curl, jq and the claude CLI (override the binary with CLAUDE_BIN=/path/to/claude).
# Ctrl-C sets the panel's status to offline.
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
ONCE=0
MODEL=""
PERMISSION_MODE="dontAsk"
ACCEPT_API=0
NEW_SESSION=0
WAIT=25
CLAUDE_BIN="${CLAUDE_BIN:-claude}"

usage() { awk 'NR <= 2 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0" | sed '${/^$/d;}'; }
die() { echo "error: $*" >&2; exit 1; }
need() { [ "$1" -ge 2 ] || { echo "missing value for $2" >&2; usage >&2; exit 2; }; }
log() { echo "[chat-bridge $(date +%H:%M:%S)] $*" >&2; }

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
state_dir="${CHAT_BRIDGE_STATE_DIR:-$repo/tmp/chat-bridge}"
session_file="$state_dir/session-id"

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
        -h|--help) usage; exit 0 ;;
        *) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done
BASE="${BASE%/}"

case "$WAIT" in ''|*[!0-9]*) echo "--wait must be a whole number of seconds" >&2; exit 2 ;; esac
[ "$WAIT" -ge 1 ] && [ "$WAIT" -le 30 ] || { echo "--wait must be 1..30" >&2; exit 2; }
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

api_get "/health" >/dev/null || die "the bridge at $BASE is not answering. Start Cities: Skylines with the mod enabled."

# --- shutdown -------------------------------------------------------------------

heartbeat_pid=""

stop_heartbeat() {
    if [ -n "$heartbeat_pid" ]; then
        kill "$heartbeat_pid" 2>/dev/null || true
        wait "$heartbeat_pid" 2>/dev/null || true
        heartbeat_pid=""
    fi
}

shutdown() {
    trap - INT TERM
    stop_heartbeat
    post_status offline "chat-bridge stopped"
    log "stopped; panel status set to offline"
    exit 130
}
trap shutdown INT TERM
# Never leave the heartbeat loop orphaned, whatever ends the script.
trap stop_heartbeat EXIT

# While a Claude turn runs no inbox poll happens, so keep the panel from declaring Claude
# offline (it does that after 120 s without a chat call).
start_heartbeat() {
    (
        nap=""
        trap '[ -n "$nap" ] && kill "$nap" 2>/dev/null; exit 0' TERM
        while :; do
            sleep 30 & nap=$!
            wait "$nap"
            curl -sS --max-time 5 "${BASE}/chat/status" >/dev/null 2>&1 || true
        done
    ) &
    heartbeat_pid=$!
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
repo. Before acting, read CLAUDE.md, lessons.md, knowledge.md and transit.md (whichever
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

run_claude() {
    # $1 prompt, $2 output file. Resumes the saved conversation when there is one.
    local prompt="$1" out="$2" session=""
    local -a args=(-p "$prompt" --output-format json --permission-mode "$PERMISSION_MODE" --allowedTools "${allowed_tools[@]}")
    [ -n "$MODEL" ] && args+=(--model "$MODEL")
    [ -f "$session_file" ] && session=$(cat "$session_file")

    if [ -n "$session" ]; then
        if (cd "$repo" && "$CLAUDE_BIN" "${args[@]}" --resume "$session") >"$out" 2>"$out.err"; then
            return 0
        fi
        log "resuming session $session failed; starting a new conversation"
        rm -f "$session_file"
    fi

    (cd "$repo" && "$CLAUDE_BIN" "${args[@]}") >"$out" 2>"$out.err"
}

handle_message() {
    local message="$1"
    local id text source out reply session is_error
    id=$(printf '%s' "$message" | jq -r '.id')
    text=$(printf '%s' "$message" | jq -r '.text')
    source=$(printf '%s' "$message" | jq -r '.source // "panel"')

    if [ "$source" != "panel" ] && [ "$ACCEPT_API" != 1 ]; then
        log "skipping #$id: sent through /chat/send (source=$source); pass --accept-api to answer these"
        return 1
    fi

    log "#$id: $text"
    post_status thinking "reading message #$id"

    out="$state_dir/turn-$id.json"
    start_heartbeat
    local rc=0
    run_claude "$(build_prompt "$message")" "$out" || rc=$?
    stop_heartbeat
    if [ "$rc" = 0 ]; then
        is_error=$(jq -r '.is_error // false' "$out" 2>/dev/null || echo true)
        reply=$(jq -r '.result // empty' "$out" 2>/dev/null || true)
        session=$(jq -r '.session_id // empty' "$out" 2>/dev/null || true)
        [ -n "$session" ] && printf '%s' "$session" >"$session_file"
        if [ "$is_error" = "true" ] && [ -z "$reply" ]; then
            reply="Claude hit an error on this one. Details: $out"
        fi
    else
        reply=$(jq -r '.result // empty' "$out" 2>/dev/null || true)
        [ -n "$reply" ] || reply="Claude could not run (exit $rc). Details: $out.err"
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

log "watching ${BASE}/chat/inbox (Ctrl-C to stop)"
post_status idle "chat-bridge is listening"

last=0
while :; do
    if ! response=$(api_get "/chat/inbox?after=${last}&wait=${WAIT}&unanswered=true"); then
        log "inbox poll failed; retrying in 5 s"
        sleep 5
        continue
    fi

    new_last=$(printf '%s' "$response" | jq -r '.lastId // empty')
    count=$(printf '%s' "$response" | jq '.messages | length')

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

    [ -n "$new_last" ] && [ "$new_last" -gt "$last" ] && last="$new_last"
done
