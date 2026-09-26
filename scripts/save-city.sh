#!/usr/bin/env bash
#
# Port of save-city.ps1: ask the bridge to save the city, then wait for the returned .crp
# path to appear on disk and print its name, modification time and size.
#
# Usage: ./scripts/save-city.sh [--base-url URL] [--name NAME] [--timeout SECONDS]
#
#   --base-url URL     bridge base URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
#   --name NAME        save name (default: AgentAutoSave-<yyyyMMdd-HHmmss>)
#   --timeout SECONDS  how long to wait for the file (default: 120; polled every 3s)
#   -h, --help         show this help
#
# Exits 1 if the save response has no path or the file does not appear in time.
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
NAME="AgentAutoSave-$(date +%Y%m%d-%H%M%S)"
TIMEOUT=120

usage() { awk 'NR <= 2 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0" | sed '${/^$/d;}'; }
die() { echo "error: $*" >&2; exit 1; }
need() { [ "$1" -ge 2 ] || { echo "missing value for $2" >&2; usage >&2; exit 2; }; }

while [ $# -gt 0 ]; do
    case "$1" in
        --base-url) need $# "$1"; BASE="$2"; shift ;;
        --name)     need $# "$1"; NAME="$2"; shift ;;
        --timeout)  need $# "$1"; TIMEOUT="$2"; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done
BASE="${BASE%/}"

command -v jq >/dev/null 2>&1 || die "jq is required: brew install jq"
case "$TIMEOUT" in ''|*[!0-9]*) echo "--timeout must be a whole number of seconds" >&2; exit 2 ;; esac

api_post() { echo "POST $1 $2" >&2; curl -sS --fail-with-body --max-time 180 -X POST -H 'Content-Type: application/json' --data "$2" "${BASE}$1"; }

# mtime (epoch seconds) of a file, GNU stat first, then BSD/macOS stat.
file_mtime() { stat -c %Y "$1" 2>/dev/null || stat -f %m "$1"; }
# ISO-8601 local time for an epoch, GNU date first, then BSD/macOS date.
iso_time() { date -d "@$1" +%Y-%m-%dT%H:%M:%S%z 2>/dev/null || date -r "$1" +%Y-%m-%dT%H:%M:%S%z; }

body=$(jq -n --arg name "$NAME" '{name: $name}')
response=$(api_post "/commands/save" "$body") || { printf '%s\n' "$response" >&2; exit 1; }
printf '%s\n' "$response" | jq .

path=$(printf '%s' "$response" | jq -r '.path // empty')
[ -n "$path" ] || die "Save response did not include a path."

deadline=$(( $(date +%s) + 10#$TIMEOUT ))
while :; do
    if [ -e "$path" ]; then
        mtime=$(file_mtime "$path")
        size=$(wc -c < "$path" | tr -d ' ')
        jq -n --arg full "$path" --arg when "$(iso_time "$mtime")" --argjson len "$size" \
            '{FullName: $full, LastWriteTime: $when, Length: $len}'
        exit 0
    fi
    sleep 3
    [ "$(date +%s)" -lt "$deadline" ] || break
done

die "Timed out waiting for save file: $path"
