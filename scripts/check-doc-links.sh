#!/usr/bin/env bash
#
# Check that relative links, asset references, and VitePress nav links in the
# docs resolve to files in the repo. Pure bash + grep + sed; runs on macOS
# bash 3.2 and on Linux CI.
# Port of check-doc-links.ps1.
#
# Usage: scripts/check-doc-links.sh [--repo PATH]
#
#   --repo PATH   repository root to check (default: the repo containing this script)
#
# Scans docs/**/*.{md,mts,json,svg} (excluding .vitepress/dist and node_modules)
# plus README.md, README.ja.md, CONTRIBUTING.md, CONTRIBUTING.ja.md when present.
# Broken targets are reported on stderr and the script exits 1; otherwise it
# prints "Docs links OK" and exits 0.
#
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() {
    sed -n '3,16p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

die() {
    echo "error: $*" >&2
    exit 1
}

while [ $# -gt 0 ]; do
    case "$1" in
        --repo)    [ $# -ge 2 ] || die "--repo needs a value"; repo="$2"; shift ;;
        -h|--help) usage; exit 0 ;;
        *)         echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

[ -d "$repo" ] || die "repository path was not found: $repo"
repo="$(cd "$repo" && pwd)"
docs="$repo/docs"
[ -d "$docs" ] || die "docs directory was not found: $docs"

# Byte-wise matching: the docs contain Japanese text and we only care about ASCII syntax.
export LC_ALL=C

# The .ps1 matched against the whole file, so its patterns could span line
# breaks. Mirror that by folding newlines into form feeds (which [[:space:]]
# still matches) and matching the file as one line.
FF="$(printf '\f')"

link_pattern='\[[^]]+\]\([^)]+\)'
image_pattern='src:[[:space:]]+[^[:space:]]+'
html_link_pattern='(^|[^[:alnum:]_])(href|src)="[^"]+"'
vitepress_link_pattern="link:[[:space:]]+['\"][^'\"]+['\"]"

# Extract group 1 of each pattern, one target per line.
extract_links()     { grep -oE "$link_pattern" "$1" 2>/dev/null | sed 's/^\[[^]]*\](\(.*\))$/\1/' || true; }
extract_images()    { grep -oE "$image_pattern" "$1" 2>/dev/null | sed "s/^src:[[:space:]]*//" || true; }
extract_html()      { grep -oE "$html_link_pattern" "$1" 2>/dev/null | sed 's/^.*\(href\|src\)="//; s/^[^"]*="//; s/"$//' || true; }
extract_vitepress() { grep -oE "$vitepress_link_pattern" "$1" 2>/dev/null | sed "s/^link:[[:space:]]*['\"]//; s/['\"]\$//" || true; }

# Lexically collapse "." and ".." so a path through a missing directory is
# judged the way .NET's Test-Path judged it (it normalises before checking).
normalize_path() {
    local path="$1" out="" part rest
    rest="$path"
    while [ -n "$rest" ]; do
        case "$rest" in
            */*) part="${rest%%/*}"; rest="${rest#*/}" ;;
            *)   part="$rest"; rest="" ;;
        esac
        case "$part" in
            ''|.) ;;
            ..)   out="${out%/*}" ;;
            *)    out="$out/$part" ;;
        esac
    done
    case "$path" in
        */) printf '%s/' "${out:-}" ;;
        *)  printf '%s' "${out:-/}" ;;
    esac
}

exists()  { [ -e "$(normalize_path "$1")" ]; }
is_file() { [ -f "$(normalize_path "$1")" ]; }

trim_ws() {
    local s="$1"
    s="${s#"${s%%[![:space:]]*}"}"
    s="${s%"${s##*[![:space:]]}"}"
    printf '%s' "$s"
}

# Resolve-DocsLink: print the resolved path, or nothing when the target is not checked.
resolve_docs_link() {
    local file_dir="$1" clean
    clean="$(trim_ws "$2")"
    # .Trim('"').Trim("'")
    while [ "${clean#\"}" != "$clean" ]; do clean="${clean#\"}"; done
    while [ "${clean%\"}" != "$clean" ]; do clean="${clean%\"}"; done
    while [ "${clean#\'}" != "$clean" ]; do clean="${clean#\'}"; done
    while [ "${clean%\'}" != "$clean" ]; do clean="${clean%\'}"; done

    case "$clean" in
        \<?*\>) clean="${clean#<}"; clean="$(trim_ws "${clean%>}")" ;;
        *[\<\>]*) return 0 ;;
    esac

    # PowerShell -match is case-insensitive.
    if printf '%s' "$clean" | grep -qiE '^(https?:|mailto:|#)'; then
        return 0
    fi

    local from_docs_root=false
    case "$clean" in
        /*)
            while [ "${clean#/}" != "$clean" ]; do clean="${clean#/}"; done
            if exists "$docs/public/$clean"; then
                printf '%s' "$docs/public/$clean"
                return 0
            fi
            from_docs_root=true
            ;;
    esac

    clean="${clean%%#*}"
    [ -n "$clean" ] || return 0

    local candidate
    if [ "$from_docs_root" = true ]; then
        candidate="$docs/$clean"
    else
        candidate="$file_dir/$clean"
    fi

    if is_file "$candidate"; then
        printf '%s' "$candidate"
        return 0
    fi

    # [System.IO.Path]::GetExtension: text after the last '.' of the last path
    # segment, empty when there is no dot or the dot is the final character.
    local name="${candidate##*/}" ext=""
    case "$name" in
        *.?*) ext="${name##*.}" ;;
    esac
    if [ -z "$ext" ]; then
        if exists "$candidate.md"; then
            printf '%s' "$candidate.md"
        else
            printf '%s' "${candidate%/}/index.md"
        fi
        return 0
    fi

    printf '%s' "$candidate"
}

failed=false

# check_targets FILE KIND: read targets on stdin, warn about each that does not resolve.
check_targets() {
    local file="$1" kind="$2" file_dir target resolved
    file_dir="$(dirname "$file")"
    while IFS= read -r target; do
        [ -n "$target" ] || continue
        target="$(printf '%s' "$target" | tr '\f' '\n')"
        resolved="$(resolve_docs_link "$file_dir" "$target")"
        if [ -n "$resolved" ] && ! exists "$resolved"; then
            echo "WARNING: Broken docs $kind in $file: $target -> $resolved" >&2
            failed=true
        fi
    done
}

file_list() {
    find "$docs" -type f \( -name '*.md' -o -name '*.mts' -o -name '*.json' -o -name '*.svg' \) \
        -not -path '*/.vitepress/dist/*' -not -path '*/node_modules/*' | sort
    local name
    for name in README.md README.ja.md CONTRIBUTING.md CONTRIBUTING.ja.md; do
        [ -e "$repo/$name" ] && echo "$repo/$name"
    done
    return 0
}

flat="$(mktemp "${TMPDIR:-/tmp}/check-doc-links.XXXXXX")"
trap 'rm -f "$flat"' EXIT

while IFS= read -r file; do
    tr '\n' "$FF" < "$file" > "$flat"
    check_targets "$file" "link"            < <(extract_links "$flat")
    check_targets "$file" "asset reference" < <(extract_images "$flat")
    check_targets "$file" "reference"       < <(extract_html "$flat"; extract_vitepress "$flat")
done < <(file_list)

if [ "$failed" = true ]; then
    exit 1
fi

echo "Docs links OK"
