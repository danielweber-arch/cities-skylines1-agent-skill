#!/usr/bin/env bash
#
# Keep scripts/chat-bridge.sh running in the background on macOS with a launchd user agent,
# so the in-game Claude chat panel is always answered: started at login, restarted if it
# exits, and waiting quietly while the game is closed.
#
# Usage: ./scripts/install-chat-bridge-agent.sh install [chat-bridge options...]
#        ./scripts/install-chat-bridge-agent.sh uninstall
#        ./scripts/install-chat-bridge-agent.sh restart
#        ./scripts/install-chat-bridge-agent.sh status
#        ./scripts/install-chat-bridge-agent.sh logs
#
# Options after `install` are passed to chat-bridge.sh, e.g.
#   ./scripts/install-chat-bridge-agent.sh install --model sonnet --stall-timeout 900
#
# The agent runs with the PATH of the shell that installs it, so run `install` from a
# terminal where `claude`, `jq`, `curl` and `npx` all work. Re-run `install` after moving
# the repo or changing options.
#
set -euo pipefail

label="com.skylines-agent-bridge.chat"
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
plist="$HOME/Library/LaunchAgents/$label.plist"
log_file="$HOME/Library/Logs/cs1-chat-bridge.log"
domain="gui/$(id -u)"

die() { echo "error: $*" >&2; exit 1; }
usage() { awk 'NR <= 2 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0"; }

xml_escape() {
    printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'
}

[ "$(uname -s)" = "Darwin" ] || die "this installer is for macOS (launchd). On other systems run ./scripts/chat-bridge.sh under your own supervisor."

cmd="${1:-}"
[ $# -gt 0 ] && shift

case "$cmd" in
    install)
        for tool in claude jq curl npx; do
            command -v "$tool" >/dev/null 2>&1 || die "$tool is not on PATH in this shell. Install it (or fix PATH) and re-run."
        done

        case "$repo" in
            "$HOME/Desktop"/*|"$HOME/Documents"/*|"$HOME/Downloads"/*)
                echo "warning: the repo is under Desktop/Documents/Downloads. macOS privacy protection can block"
                echo "         background agents there. If the log shows 'Operation not permitted', move the repo"
                echo "         (e.g. to ~/code) or give /bin/bash Full Disk Access, then re-run install."
                ;;
        esac

        args_xml="        <string>/bin/bash</string>
        <string>$(xml_escape "$repo/scripts/chat-bridge.sh")</string>"
        for arg in "$@"; do
            args_xml="$args_xml
        <string>$(xml_escape "$arg")</string>"
        done

        mkdir -p "$(dirname "$plist")" "$(dirname "$log_file")"
        cat >"$plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>$label</string>
    <key>ProgramArguments</key>
    <array>
$args_xml
    </array>
    <key>WorkingDirectory</key>
    <string>$(xml_escape "$repo")</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>PATH</key>
        <string>$(xml_escape "$PATH")</string>
        <key>HOME</key>
        <string>$(xml_escape "$HOME")</string>
    </dict>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>ThrottleInterval</key>
    <integer>10</integer>
    <key>ProcessType</key>
    <string>Interactive</string>
    <key>StandardOutPath</key>
    <string>$(xml_escape "$log_file")</string>
    <key>StandardErrorPath</key>
    <string>$(xml_escape "$log_file")</string>
</dict>
</plist>
EOF
        plutil -lint "$plist" >/dev/null || die "generated plist is invalid: $plist"

        launchctl bootout "$domain/$label" 2>/dev/null || true
        launchctl bootstrap "$domain" "$plist"
        echo "Installed and started $label"
        echo "  plist: $plist"
        echo "  log:   $log_file"
        echo "It waits for the game, answers the chat panel, and restarts itself if it exits."
        ;;
    uninstall)
        launchctl bootout "$domain/$label" 2>/dev/null || true
        rm -f "$plist"
        echo "Stopped and removed $label"
        ;;
    restart)
        [ -f "$plist" ] || die "not installed; run: $0 install"
        launchctl kickstart -k "$domain/$label"
        echo "Restarted $label"
        ;;
    status)
        if launchctl print "$domain/$label" >/dev/null 2>&1; then
            launchctl print "$domain/$label" | grep -E '^\s*(state|pid|last exit code) ' || true
        else
            echo "$label is not loaded"
        fi
        if [ -f "$log_file" ]; then
            echo "--- last lines of $log_file"
            tail -n 10 "$log_file"
        fi
        ;;
    logs)
        touch "$log_file"
        exec tail -f "$log_file"
        ;;
    -h|--help|"")
        usage
        ;;
    *)
        echo "unknown command: $cmd" >&2
        usage >&2
        exit 2
        ;;
esac
