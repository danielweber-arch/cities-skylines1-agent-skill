# Autopilot: an unattended Claude mayor for a CS1 city

`scripts/autopilot.sh` runs a city with no human at the terminal. The player has granted
full autonomy over the city (2026-10-17): Claude makes every decision and never asks the
player a question. The only channel between the player and Claude is the in-game **Claude**
chat window (Ctrl+Shift+C). It replaces `scripts/chat-bridge.sh`; never run both.

## Roles

| Role | Model | Tools | Job |
|---|---|---|---|
| Supervisor | bash (`scripts/autopilot.sh`) | curl, jq | The loop. Waits for the game, picks the city, routes chat, decides which turn runs next, retries, backs off on usage limits. No judgment. |
| Mayor | Opus (`opus`) | read-only `cs1_*` state tools, Read/Grep/Glob, Write/Edit inside `cities/<slug>/`, chat `say` | All decisions. Writes the brief (city.md) at founding, the orders for the next step, answers the player. Never mutates the game. |
| Operator | Sonnet (`sonnet`) | all `cs1_*` tools, Read/Grep/Glob, Write/Edit inside `cities/<slug>/` + root `lessons.md`, chat `say`, `save-city.sh`, `review.sh` | Controls the city. Executes the current orders with dry-run -> real call -> state read -> save, logs ids and results, writes a report. Makes no strategic choices. |
| Planner | Fable 5.1 (`claude-fable-5-1`) | same read-only set as the mayor, Write/Edit `cities/<slug>/plan.md` | Reviews and rewrites the master plan at founding and at each milestone. |

Every turn is a fresh `claude -p` session (no `--resume` across turns): memory lives in files,
which keeps context small and makes a crash or restart lose nothing. Within one turn a stall or
crash is resumed in the same session (chat-bridge's `run_watched` logic).

## Files (per city, `cities/<slug>/`)

- `city.md`: brief + `## Standing orders`. Header carries `autopilot: on|off`.
- `plan.md`: master plan (planner owns; mayor may amend with a dated note).
- `progress.md`: operator's append-only log, created entity ids.
- `autopilot/orders.md`: the mayor's current orders: one step, concrete coordinates/prefabs,
  acceptance checks, budget cap.
- `autopilot/next.json`: mayor -> supervisor: `{"action":"operate"|"wait", "wakeMinutes":N,
  "milestone":<population or null>}`.
- `autopilot/report.json`: operator -> supervisor/mayor: `{"status":"done"|"partial"|"blocked"|
  "failed", "summary":"...", "saved":true|false}`.
- `autopilot/journal.md`: mayor's own decision log (one dated paragraph per turn).
- `autopilot/state.json`: supervisor bookkeeping (last mayor/planner time, last milestone).
- `autopilot/turns/`: raw stream-json of every turn.

## The loop

```
forever:
  wait for /health levelLoaded            (game down -> wait; player loads a save)
  city = /health .city ; dir = cities/<slug>
  if no dir: FOUND (new city): mayor writes city.md (autopilot: on, autonomy standing order),
             planner writes plan.md, mayor writes first orders
  if city.md autopilot != on: chat-only mode (mayor answers chat, read-only), no cycles
  pending chat? -> mayor turn (answer + revise orders)       [chat outranks everything]
  crisis? (cash < 0 and falling, population drop > 5%, new problem spike) -> mayor turn
  population crossed next milestone (5k, 10k, 20k, 40k, 70k, 100k, then +50k) -> planner turn, then mayor
  report says done/blocked/failed, or no orders, or 60 min since last mayor -> mayor turn
  next.action == operate -> operator turn
  next.action == wait    -> long-poll chat for wakeMinutes (wakes early on a chat message)
```

While any turn runs, a watcher long-polls `/chat/inbox`, keeps the panel online, and acks each
new player message once ("Got #N, the mayor will take it after this step") and queues it.
Queued messages go to the next mayor turn verbatim with camera/selection context.

Chat commands handled by the supervisor itself, no model: `/pause` (no operator turns until
`/resume`; chat still answered), `/resume`, `/status` (phase, last report, population, next wake).

## Guarantees

- **No questions.** Every prompt states the autonomy grant and that any "ask the player" in a
  repo doc means "decide, then log the decision in journal.md". The player is told, not asked.
- **No git.** `git` is denied to every turn.
- **No other city.** The supervisor only drives a city whose `city.md` says `autopilot: on`.
  A newly seen city (no dir) is founded with `autopilot: on`; Portville/Tampa/Ashford are untouched
  unless the player types `/autopilot on` in that city.
- **Usage limits.** A turn whose result/error mentions a usage/rate limit or overload sleeps with
  growing backoff (5, 10, 20, 30 min cap), shows "paused: usage limit, retry HH:MM" in the panel,
  and retries the same turn. Never gives up.
- **Stays up.** launchd agent (`scripts/install-autopilot-agent.sh`), `KeepAlive`, `RunAtLoad`,
  wrapped in `caffeinate -ims` so the Mac does not idle-sleep. Logs to
  `~/Library/Logs/cs1-autopilot.log`.

- **One save.** Player order 2026-10-17: one save file for the city, always overwritten, never a
  second version. The save name is fixed at founding in city.md (`Save file:`) and every save
  call uses exactly that name; the supervisor checks `/state/saves` after each turn and logs any
  extra file the city produced.
- **Fair play.** Player order 2026-10-17: no cheats, no gameplay mods. The bridge is the only mod
  (it is the controller, not a cheat). New game is started with no Unlimited Money / Unlock All /
  unlimited resources. The bridge's bypass flags (`ignoreUnlock`, `ignoreMilestoneLimit`,
  `ignoreMaxAreaCount`, anything that skips cost or an unlock) are refused while fair play is on.
  Every build pays its normal cost; only unlocked prefabs; tiles bought only when the milestone
  allows and the cash covers it.
- **Context.** Fresh session per turn; within a long turn Claude Code auto-compacts.

## Game lifecycle

- New city: the supervisor starts the game straight to the main menu (no launcher) and calls
  `POST /commands/new-game` (map + city name chosen by the mayor at founding).
- Crash or quit: the supervisor relaunches with `--continuelastsave` (`scripts/restart-game.sh`),
  then verifies the loaded city id matches the autopilot city before any turn.

## Known limits

- Hard wall clock per operator turn: 45 min (then stopped and resumed once with a continue prompt).
- Cost: one Opus turn per step plus per chat message; Sonnet does the volume.
