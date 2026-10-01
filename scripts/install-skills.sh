#!/bin/bash
# Copy the Claude Code workflow skills from this repo (canonical) into ~/.claude/skills/.
# Syncs every folder under skills/ (SKILL.md plus any companion files).
# Usage: ./scripts/install-skills.sh [--diff]   (--diff only shows what would change)
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
dest="${CLAUDE_SKILLS_DIR:-$HOME/.claude/skills}"
status=0
for dir in "$repo"/skills/*/; do
    skill="$(basename "$dir")"
    [ -f "$dir/SKILL.md" ] || { echo "missing $dir/SKILL.md" >&2; exit 1; }
    if [ "${1:-}" = "--diff" ]; then
        if diff -ru "$dest/$skill" "$dir"; then echo "$skill: up to date"; else status=1; fi
        continue
    fi
    mkdir -p "$dest/$skill"
    cp -f "$dir"* "$dest/$skill/"
    echo "installed $skill -> $dest/$skill"
done
exit $status
