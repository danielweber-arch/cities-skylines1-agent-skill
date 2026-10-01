#!/bin/bash
# Copy the Claude Code workflow skills from this repo (canonical) into ~/.claude/skills/.
# Usage: ./scripts/install-skills.sh [--diff]   (--diff only shows what would change)
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
dest="${CLAUDE_SKILLS_DIR:-$HOME/.claude/skills}"
status=0
for skill in cs1-city cs1-transit; do
    src="$repo/skills/$skill/SKILL.md"
    [ -f "$src" ] || { echo "missing $src" >&2; exit 1; }
    if [ "${1:-}" = "--diff" ]; then
        if diff -u "$dest/$skill/SKILL.md" "$src"; then echo "$skill: up to date"; else status=1; fi
        continue
    fi
    mkdir -p "$dest/$skill"
    cp -f "$src" "$dest/$skill/SKILL.md"
    echo "installed $skill -> $dest/$skill/SKILL.md"
done
exit $status
