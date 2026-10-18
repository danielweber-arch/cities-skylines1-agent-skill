#!/usr/bin/env bash
#
# Copy a bench scenario's save to a timestamped working copy, restart the game into it, then
# score it. NEVER modifies the scenario's original .crp file.
#
# Usage: scripts/bench-run.sh <scenario-id> [-- <extra args to bench-score.mjs>]
#
#   <scenario-id>   a scenario id from bench/manifest.json (e.g. tampa-transit)
#   -h, --help      show this help and exit 0 (works without the game running)
#
# Flow:
#   1. Read bench/manifest.json for the scenario's `save` filename.
#   2. Copy it to "bench-<scenario>-<ts>.crp" in the Saves folder (refuses if that name exists).
#   3. scripts/restart-game.sh (loads --continuelastsave, i.e. the copy just made).
#   4. scripts/bench-score.mjs --scenario <scenario-id> [extra args].
#
set -euo pipefail

SELF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SELF_DIR/.." && pwd)"
SAVES_DIR="${CS1_SAVES_DIR:-$HOME/Library/Application Support/Colossal Order/Cities_Skylines/Saves}"
MANIFEST="${CS1_BENCH_MANIFEST:-$REPO_ROOT/bench/manifest.json}"

usage() { awk 'NR <= 2 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0" | sed '${/^$/d;}'; }
die() { echo "error: $*" >&2; exit 1; }

if [ $# -eq 0 ] || [ "$1" = "-h" ] || [ "$1" = "--help" ]; then
    usage
    exit 0
fi

SCENARIO="$1"; shift

command -v jq >/dev/null 2>&1 || die "jq is required: brew install jq"
[ -f "$MANIFEST" ] || die "manifest not found: $MANIFEST"

SAVE_NAME=$(jq -r --arg id "$SCENARIO" '.scenarios[] | select(.id == $id) | .save' "$MANIFEST")
[ -n "$SAVE_NAME" ] && [ "$SAVE_NAME" != "null" ] || die "scenario \"$SCENARIO\" not found (or has no save) in $MANIFEST"

SRC="$SAVES_DIR/$SAVE_NAME"
[ -f "$SRC" ] || die "scenario save not found: $SRC"

TS=$(date +%Y%m%d-%H%M%S)
DEST_NAME="bench-${SCENARIO}-${TS}.crp"
DEST="$SAVES_DIR/$DEST_NAME"

[ -e "$DEST" ] && die "refusing to overwrite an existing bench save: $DEST"

cp "$SRC" "$DEST"
echo "copied $SRC -> $DEST (original never modified)"

"$SELF_DIR/restart-game.sh"

node "$SELF_DIR/bench-score.mjs" --scenario "$SCENARIO" --manifest "$MANIFEST" "$@"
