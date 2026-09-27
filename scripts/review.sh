#!/usr/bin/env bash
#
# Capture a top-down render of an area and print the file path.
#
#   ./scripts/review.sh <x> <z> [size] [mode]
#
# Prefer the cs1_capture MCP tool when you have it — this script exists for shell use and
# for checking the capture endpoint without the MCP layer in the way.
#
set -euo pipefail

X="${1:?usage: review.sh <x> <z> [size] [mode]}"
Z="${2:?usage: review.sh <x> <z> [size] [mode]}"
SIZE="${3:-1000}"
MODE="${4:-None}"
BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out_dir="$repo/tmp/review"
mkdir -p "$out_dir"
out="$out_dir/$(date +%Y%m%d-%H%M%S)-${MODE}.png"

status=$(curl -sS -o "$out" -w '%{http_code}' \
  "${BASE}/capture?x=${X}&z=${Z}&size=${SIZE}&pixels=1024&mode=${MODE}")

if [ "$status" != "200" ]; then
    echo "capture failed (HTTP $status):" >&2
    cat "$out" >&2
    echo >&2
    rm -f "$out"
    exit 1
fi

echo "$out"
