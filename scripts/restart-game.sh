#!/usr/bin/env bash
# Restart Cities: Skylines into the most recent save without the Paradox launcher.
# Save first (./scripts/save-city.sh --name "<working save>") so the newest .crp is the one to load:
# --continuelastsave loads the most recently written save.
set -euo pipefail
GAME="${CS1_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Cities_Skylines}"
BASE="${CS1_API:-http://127.0.0.1:32123}"
TIMEOUT="${1:-600}"

pid=$(pgrep -x Cities || true)
if [ -n "$pid" ]; then
    kill -TERM $pid
    for _ in $(seq 1 30); do pgrep -x Cities >/dev/null || break; sleep 1; done
    pgrep -x Cities >/dev/null && kill -KILL $pid && sleep 2
fi

# Steam passes these to games it launches; without them the Steam API may relaunch via Steam.
cd "$GAME"
SteamAppId=255710 SteamGameId=255710 nohup "$GAME/Cities.app/Contents/MacOS/Cities" --continuelastsave >/dev/null 2>&1 &
disown

deadline=$(( $(date +%s) + TIMEOUT ))
until curl -sS --max-time 3 "$BASE/health" 2>/dev/null | grep -q '"levelLoaded":true'; do
    [ "$(date +%s)" -lt "$deadline" ] || { echo "Timed out waiting for the city to load" >&2; exit 1; }
    pgrep -x Cities >/dev/null || { echo "The game exited during startup" >&2; exit 1; }
    sleep 5
done
curl -sS "$BASE/health"; echo
