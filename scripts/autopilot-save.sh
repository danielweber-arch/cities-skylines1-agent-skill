#!/usr/bin/env bash
#
# Save the autopilot city to its ONE save file, overwriting it, and prove the write landed.
# Takes no arguments on purpose: the name comes from tmp/autopilot/save-name (written once at
# founding), so no turn can create a second save under another name.
#
# Exit 0 only when /state/saves shows the file with an mtime newer than before the request.
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
BASE="${BASE%/}"
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
name_file="$repo/tmp/autopilot/save-name"

die() { echo "autopilot-save: $*" >&2; exit 1; }
[ $# -eq 0 ] || die "takes no arguments; the save name is fixed in $name_file"
[ -s "$name_file" ] || die "no save name recorded in $name_file"
name=$(head -n 1 "$name_file")

# lastWriteTimeUtc of the save named exactly <name> ("" when none yet).
mtime_of() {
    curl -sS --fail --max-time 20 "$BASE/state/saves" 2>/dev/null |
        jq -r --arg n "$name" '[.saves[]? | select(.name == $n) | .lastWriteTimeUtc] | first // ""' 2>/dev/null || true
}

before=$(mtime_of)
body=$(jq -n --arg name "$name" '{name: $name}')
resp=$(curl -sS --fail-with-body --max-time 30 -X POST -H 'Content-Type: application/json' --data "$body" "$BASE/commands/save") ||
    die "save request failed: $resp"
saved_as=$(printf '%s' "$resp" | jq -r '.saveName // .name // empty' 2>/dev/null || true)
if [ -n "$saved_as" ] && [ "$saved_as" != "$name" ]; then
    die "bridge saved under '$saved_as', not '$name' (response: $resp)"
fi

# lastWriteTimeUtc has one-second resolution; never request two saves in the same second.
for _ in $(seq 1 60); do
    sleep 2
    now=$(mtime_of)
    if [ -n "$now" ] && [ "$now" != "$before" ]; then
        echo "saved $name.crp (mtime $now; was ${before:-none})"
        exit 0
    fi
done
die "the save did not land within 120 s (mtime still ${before:-none}); response was: $resp"
