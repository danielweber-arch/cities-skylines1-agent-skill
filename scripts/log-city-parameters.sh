#!/usr/bin/env bash
#
# Sample city parameters (summary, demand, tax rates, problems) from the bridge
# on a fixed interval and log them as JSONL, CSV, and an event log.
# Port of log-city-parameters.ps1.
#
# Usage: scripts/log-city-parameters.sh [--base-url URL] [--duration N] [--interval N]
#                                       [--output-dir DIR] [--include-problems]
#
#   --base-url URL       bridge URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
#   --duration N         seconds to keep sampling (default: 300)
#   --interval N         seconds between samples, must be >= 1 (default: 5)
#   --output-dir DIR     where the three log files go (default: ./tmp/parameter-logs)
#   --include-problems   fetch /state/problems?limit=300 instead of limit=80 (default: off)
#
# Writes, all sharing one yyyyMMdd-HHmmss stamp:
#   city-parameters-<stamp>.jsonl        one compact JSON record per sample
#   city-parameters-<stamp>.csv          one row per sample
#   city-parameter-events-<stamp>.log    "[<time>] message" lines, incl. TAX_CHANGE
# Event lines are echoed to stderr; the last line on stdout is a JSON result object.
#
set -euo pipefail

BASE="${CS1_BRIDGE_URL:-http://127.0.0.1:32123}"
duration=300
interval=5
output_dir="./tmp/parameter-logs"
include_problems=false

usage() {
    sed -n '3,21p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

flag_err() { echo "error: $*" >&2; usage >&2; exit 2; }
die() {
    echo "error: $*" >&2
    exit 1
}

while [ $# -gt 0 ]; do
    case "$1" in
        --base-url)         [ $# -ge 2 ] || flag_err "--base-url needs a value"; BASE="$2"; shift ;;
        --duration)         [ $# -ge 2 ] || flag_err "--duration needs a value"; duration="$2"; shift ;;
        --interval)         [ $# -ge 2 ] || flag_err "--interval needs a value"; interval="$2"; shift ;;
        --output-dir)       [ $# -ge 2 ] || flag_err "--output-dir needs a value"; output_dir="$2"; shift ;;
        --include-problems) include_problems=true ;;
        -h|--help)          usage; exit 0 ;;
        *)                  echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

command -v jq >/dev/null 2>&1 || die "jq is required: brew install jq"

case "$duration" in ''|*[!0-9]*) flag_err "--duration must be a non-negative integer." ;; esac
case "$interval" in ''|*[!0-9]*) flag_err "--interval must be an integer." ;; esac
[ "$interval" -ge 1 ] || flag_err "IntervalSeconds must be at least 1."

# The original used a 10 second timeout on every GET.
api_get() { curl -sS --fail-with-body --max-time 10 "${BASE}$1"; }

mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"

stamp="$(date +%Y%m%d-%H%M%S)"
jsonl_path="$output_dir/city-parameters-$stamp.jsonl"
csv_path="$output_dir/city-parameters-$stamp.csv"
events_path="$output_dir/city-parameter-events-$stamp.log"

# Get-Date -Format "s": local time, no offset.
now_sortable() { date +%Y-%m-%dT%H:%M:%S; }

add_event_line() {
    local line
    line="[$(now_sortable)] $1"
    printf '%s\n' "$line" >> "$events_path"
    printf '%s\n' "$line" >&2
}

health="$(api_get "/health")"
printf '%s' "$health" | jq -e '.ok == true' >/dev/null || die "Bridge health check failed."

started_at="$(date +%s)"
deadline=$((started_at + 10#$duration))
previous_tax_map="null"
sample=0

if [ "$include_problems" = true ]; then
    problems_path="/state/problems?limit=300"
else
    problems_path="/state/problems?limit=80"
fi

echo "sample,wallTime,gameTime,buildIndex,paused,selectedSpeed,finalSpeed,citizens,residentialDemand,commercialDemand,workplaceDemand,problemTotal,problemCounts,taxResidentialLow,taxResidentialHigh,taxCommercialLow,taxCommercialHigh,taxIndustrialGeneric,taxOfficeGeneric" > "$csv_path"

add_event_line "Logging started. jsonl=$jsonl_path csv=$csv_path"

# One jq program per sample. Output lines, in order:
#   1. the compact JSONL record
#   2. the CSV row (every field quoted, like ConvertTo-Csv)
#   3. this sample's tax map as compact JSON
#   4+ zero or more TAX_CHANGE messages
# shellcheck disable=SC2016
sample_program='
  def cell: if . == null then ""
            elif . == true then "True"
            elif . == false then "False"
            elif type == "string" then .
            else tostring end;
  ($economy.aggregateTaxRates // [])
    | map({key: "\(.service).\(.subService)", value: (.rate | tonumber | round)})
    | from_entries as $tax
  | {
      sample: $sample,
      wallTime: $wallO,
      summary: $summary,
      demand: $demand,
      aggregateTaxRates: $economy.aggregateTaxRates,
      taxRates: $economy.taxRates,
      problems: $problems
    } | tojson,
  ([
      $sample,
      $wallS,
      $summary.gameTime,
      $summary.buildIndex,
      $summary.simulation.paused,
      $summary.simulation.selectedSpeed,
      $summary.simulation.finalSpeed,
      $summary.citizens.count,
      $demand.residential,
      $demand.commercial,
      $demand.workplace,
      $problems.total,
      (if $problems.countsByProblem then
          ($problems.countsByProblem | to_entries | map("\(.key):\(.value | cell)") | join("|"))
       else "" end),
      $tax["Residential.ResidentialLow"],
      $tax["Residential.ResidentialHigh"],
      $tax["Commercial.CommercialLow"],
      $tax["Commercial.CommercialHigh"],
      $tax["Industrial.IndustrialGeneric"],
      $tax["Office.OfficeGeneric"]
    ] | map(cell) | @csv),
  ($tax | tojson),
  (if $prev != null then
      $tax | to_entries[]
      | . as $e
      | select(($prev | has($e.key)) and $prev[$e.key] != $e.value)
      | "TAX_CHANGE \(.key): \($prev[.key]) -> \(.value) at gameTime=\($summary.gameTime | cell) buildIndex=\($summary.buildIndex | cell)"
   else empty end)
'

while [ "$(date +%s)" -lt "$deadline" ]; do
    # One wall-clock reading per sample: "s" form for CSV, "o"-style form (with offset) for JSONL.
    wall_raw="$(date +%Y-%m-%dT%H:%M:%S%z)"
    wall_s="${wall_raw:0:19}"
    wall_tz="${wall_raw:19}"
    wall_o="${wall_s}${wall_tz:0:3}:${wall_tz:3:2}"

    summary="$(api_get "/state/summary")"
    demand="$(api_get "/state/demand")"
    economy="$(api_get "/state/economy")"
    problems="$(api_get "$problems_path")"

    out="$(jq -rn \
        --argjson sample "$sample" \
        --arg wallO "$wall_o" \
        --arg wallS "$wall_s" \
        --argjson summary "$summary" \
        --argjson demand "$demand" \
        --argjson economy "$economy" \
        --argjson problems "$problems" \
        --argjson prev "$previous_tax_map" \
        "$sample_program")"

    line_no=0
    while IFS= read -r line; do
        line_no=$((line_no + 1))
        case "$line_no" in
            1) printf '%s\n' "$line" >> "$jsonl_path" ;;
            2) printf '%s\n' "$line" >> "$csv_path" ;;
            3) previous_tax_map="$line" ;;
            *) add_event_line "$line" ;;
        esac
    done <<< "$out"

    sample=$((sample + 1))
    sleep "$interval"
done

add_event_line "Logging finished. samples=$sample"

jq -cn \
    --argjson samples "$sample" \
    --arg jsonlPath "$jsonl_path" \
    --arg csvPath "$csv_path" \
    --arg eventsPath "$events_path" \
    '{ok: true, samples: $samples, jsonlPath: $jsonlPath, csvPath: $csvPath, eventsPath: $eventsPath}'
