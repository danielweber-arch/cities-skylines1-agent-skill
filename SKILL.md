---
name: cities-skylines1-agent-skill
description: "Operate Cities: Skylines 1 through the Skylines Agent Bridge mod and localhost API. Use when Claude needs to build, inspect, repair, resume, save, or continue a CS1 city using focused API calls rather than screenshot recognition, including road connectivity, service infrastructure, zoning, facilities, problem icons, and save verification."
---

# Cities: Skylines 1 Agent Skill

Control a running Cities: Skylines 1 city on macOS through the local Skylines Agent Bridge API.

## Core Rules

- Prefer API state over image recognition. Plan from state, verify with vision, never the reverse.
- Resume and repair existing saves by default. Start fresh only when explicitly requested.
- Build with composite commands. One `cs1_build_grid` beats eighty `cs1_build_network` calls.
- After any road work, road anomalies must be zero. That is the acceptance check, not a look at the screen.
- Every service building gets a `cs1_connect` immediately after placement.
- Save after meaningful changes and verify the `.crp` file exists before saying it saved.
- Commit repository changes after each coherent code/docs task when working inside this repository.

## Local Setup

The bridge listens on:

```text
http://127.0.0.1:32123
```

Both `127.0.0.1` and `::1` are bound, so `localhost` works too.

Build and install the mod (requires Mono: `brew install mono`):

```bash
./scripts/build.sh
```

The script finds the CS1 assemblies automatically; override with `CS1_MANAGED` or `CS1_GAME_DIR`.
It installs to:

```text
~/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/SkylinesAgentBridge
```

Enable **Skylines Agent Bridge** in the CS1 content manager once. Then launch Cities: Skylines
through Steam with `./scripts/start-resume.sh`, which waits for `/health` to report a loaded
city. The launcher and in-game clicks (Play, **Resume**/**Load Game**) stay manual on macOS.

Helper scripts (all take `--base-url`, default `http://127.0.0.1:32123`, except the launcher and `review.sh`):

| Task | Script |
|------|--------|
| Launch and wait for a loaded city | `./scripts/start-resume.sh` |
| End-to-end API check | `./scripts/smoke-test.sh` |
| Save and verify the `.crp` | `./scripts/save-city.sh --name X` |
| Road anomaly hints | `./scripts/inspect-road-anomalies.sh` |
| Bounded stub repair | `./scripts/repair-road-anomalies.sh --dry-run` first, then without it |
| Time-series logging | `./scripts/log-city-parameters.sh` |
| Render an area | `./scripts/review.sh <x> <z> [size] [mode]` |

The API answers from the main menu, so a refused connection means the mod is not loaded — not
that the city is still loading. `/health` reports `levelLoaded:false` until a city is open, and
every other endpoint returns `409` until then.

```bash
until curl -sS --max-time 2 http://127.0.0.1:32123/health >/dev/null 2>&1; do sleep 2; done
curl -sS http://127.0.0.1:32123/health
```

## Use the MCP tools

This repository ships an MCP server that exposes the whole API as typed tools with validated
arguments and filtered responses. It is registered in `.mcp.json`, so Claude Code picks it up
automatically from the repository root.

**Prefer the `cs1_*` tools over raw curl.** Bad arguments are rejected before the game sees
them, and state responses are filtered from tens of thousands of tokens down to hundreds. Raw
curl is the fallback for debugging the bridge itself.

| Task | Tool |
|---|---|
| Is the bridge up, is a city loaded | `cs1_health` |
| Where does the city stand | `cs1_state_summary`, `cs1_state_demand` |
| What is broken | `cs1_state_problems`, `cs1_state_road_anomalies`, `cs1_state_zone_anomalies` |
| What exists | `cs1_state_networks`, `cs1_state_facilities`, `cs1_state_growables` |
| Valid prefab names | `cs1_prefabs_roads`, `cs1_prefabs_buildings` |
| Build a district | `cs1_build_grid`, `cs1_build_neighborhood` |
| Attach something to the network | `cs1_connect` |
| Paint zones | `cs1_set_zone`, `cs1_repair_zone_clusters` |
| Place services | `cs1_place_building` then `cs1_connect` |
| Look at the result | `cs1_capture` |
| Persist | `cs1_save` then `cs1_state_saves` |

Every state tool defaults to a filtered summary and takes `detail:"full"` for the raw payload.
`full` on a developed city runs to tens of thousands of tokens — use it only for a field the
summary drops.

## Inspection Loop

Run these before acting. With raw curl, quote every URL that carries a query string or the
shell eats the `&`:

```bash
curl -sS http://127.0.0.1:32123/health
curl -sS http://127.0.0.1:32123/state/summary
curl -sS http://127.0.0.1:32123/state/demand
curl -sS "http://127.0.0.1:32123/state/problems?limit=200"
curl -sS "http://127.0.0.1:32123/state/road-anomalies?limit=500&nearMissDistance=18&shortSegmentLength=32&includeDeadEnds=false"
curl -sS "http://127.0.0.1:32123/state/zone-anomalies?limit=200&includeUnzonedHoles=true"
curl -sS "http://127.0.0.1:32123/state/facilities?limit=500"
curl -sS "http://127.0.0.1:32123/state/growables?limit=500"
curl -sS "http://127.0.0.1:32123/state/networks?limit=1000&service=Road"
curl -sS "http://127.0.0.1:32123/state/economy"
curl -sS "http://127.0.0.1:32123/state/chirps?limit=50"
```

Use `includeMapObjects=true` on `/state/facilities` only when raw helper objects such as pipe
junctions are needed.

## Building

### Composite commands first

A neighborhood is one call, not eighty. `cs1_build_grid` creates the whole lattice — nodes
first, then segments — and returns `blockCenters`, the coordinates of each city block, so you
never do the arithmetic yourself.

```bash
curl -sS -X POST http://127.0.0.1:32123/commands/build-grid \
  -H "Content-Type: application/json" \
  -d '{"roadPrefab":"Basic Road","origin":{"x":200,"z":-300},"cols":6,"rows":4,"spacing":80,"opId":"grid-downtown-01"}'
```

`cs1_build_neighborhood` goes further: grid, then a connection to the existing road network,
then zoning from a mix. The connection runs *before* zoning, so it can never leave a zoned
orphan island — if it cannot reach a road, the grid is rolled back.

```bash
curl -sS -X POST http://127.0.0.1:32123/commands/build-neighborhood \
  -H "Content-Type: application/json" \
  -d '{"roadPrefab":"Basic Road","center":{"x":400,"z":200},"radiusOrCols":5,
       "connectTo":{"x":380,"z":90},
       "zoneMix":{"ResidentialLow":0.7,"CommercialLow":0.3},
       "commercialPlacement":"perimeter","opId":"hood-north-01"}'
```

`cs1_connect` joins any point to the nearest network node of a service. Use it after every
service building placement — it removes the whole class of "I built a water tower and it has
no road access" failures.

```bash
curl -sS -X POST http://127.0.0.1:32123/commands/connect \
  -H "Content-Type: application/json" \
  -d '{"from":{"x":512,"z":-88},"toService":"Road","maxDistance":200,"roadPrefab":"Basic Road"}'
```

### Rules that apply to every composite command

- **Always set `opId`.** If a call times out, retry with the same `opId` and the cached
  manifest comes back instead of a second overlapping grid.
- **`spacing` defaults to 80 for a reason.** CS1 zoning cells are 8m and zoneable depth is
  4 cells (32m) per side, so 80m fills the block from both sides with 16m for the road.
  100m leaves an unzoneable dead strip down the middle of every block.
- **Bounded.** `cols * rows` is capped at 400 for a grid and 144 for a neighborhood. A
  misplaced decimal gets a clear error, not a thirty-second freeze.
- **`dryRun:true`** returns the plan and `blockCenters` without touching the city.

### Acceptance check

```bash
curl -sS "http://127.0.0.1:32123/state/road-anomalies?limit=500&includeDeadEnds=false"
```

`total` must be **0**. If it is not, node reuse did not do its job and nothing downstream will
work — fix it before zoning or placing anything.

## Command Pattern

Keep individual repairs separate and auditable.

```bash
# Delete a bad segment
curl -sS -X POST http://127.0.0.1:32123/commands/bulldoze \
  -H "Content-Type: application/json" \
  -d '{"entityType":"netSegment","id":19023,"keepNodes":false}'

# Build a single segment
curl -sS -X POST http://127.0.0.1:32123/commands/build-network \
  -H "Content-Type: application/json" \
  -d '{"roadPrefab":"Basic Road","start":{"x":400,"z":300},"end":{"x":423.614,"z":554.945},"name":"Agent Highway Link"}'

# Place a building, then connect it
curl -sS -X POST http://127.0.0.1:32123/commands/place-building \
  -H "Content-Type: application/json" \
  -d '{"buildingPrefab":"Water Tower","position":{"x":120,"z":-220},"angleDegrees":0}'

# Paint zones
curl -sS -X POST http://127.0.0.1:32123/commands/set-zone \
  -H "Content-Type: application/json" \
  -d '{"zone":"ResidentialLow","preserveOccupied":true,"center":{"x":240,"z":-40},"radius":40}'

# Run the simulation
curl -sS -X POST http://127.0.0.1:32123/commands/set-simulation-speed \
  -H "Content-Type: application/json" \
  -d '{"paused":false,"speed":3}'

# Lower taxes when /state/problems reports TaxesTooHigh
curl -sS -X POST http://127.0.0.1:32123/commands/set-tax-rate \
  -H "Content-Type: application/json" \
  -d '{"service":"Commercial","rate":9}'
```

Valid zone names, exactly: `ResidentialLow`, `ResidentialHigh`, `CommercialLow`,
`CommercialHigh`, `Industrial`, `Office`, `Unzoned`. CS1 has no plain `Commercial` or
`Residential` zone.

Save and verify. `/commands/save` returns immediately with the target `path`; the file appears
a few seconds later, so poll before declaring success:

```bash
curl -sS -X POST http://127.0.0.1:32123/commands/save \
  -H "Content-Type: application/json" \
  -d '{"name":"AgentAutoSave-clean"}'

curl -sS http://127.0.0.1:32123/state/saves
ls -la ~/Library/Application\ Support/Colossal\ Order/Cities_Skylines/Saves/
```

Save names are sanitized server-side: anything outside letters, digits, `-`, `_`, and space
becomes `_`, and the name is truncated to 64 characters. Read `saveName` from the response
rather than assuming the name you sent.

## Visual verification

Vision is for verification only. Never plan layout from an image; plan from state APIs.

Capture a review render:

- after completing any phase in `city-plan.md`
- after any `build-neighborhood` or `build-grid` call
- when `/state/problems` reports something the state APIs cannot localise
- **never more than once per 10 commands**

```bash
./scripts/review.sh 400 200 1200 None      # prints the PNG path
```

Use `mode=None` after road construction and for zoning — CS1 paints zone colours on the ground
in the default view and has no separate zone overlay. Use `mode=Traffic` after 2+ simulated
weeks, `mode=Water` or `mode=Electricity` to find service coverage holes, `mode=LandValue` to
judge whether the development strategy is working. Request one mode per capture.

Roughly 1–1.5K tokens per 1024px image. At once per ten commands that is noise; without the
cadence rule it becomes the largest line item in the run.

If the render contradicts a state API, trust the state API and report the discrepancy — it
usually means a prefab did not place where it was requested. If a capture comes back reporting
a single flat colour, the off-screen camera failed to pick up the scene; say so rather than
describing an empty image as an empty city.

## Plan discipline

Over a long build every model loses the thread: it starts a transit-oriented town and ends up
with a suburb because turn 140 has no memory of turn 12's intent. The fix is to keep the intent
in a file. The plan is the source of truth; context is scratch space.

**Phase A — plan.** No mutations. Read state, write `city-plan.md` from
`templates/city-plan.md`, and stop for review. Two minutes here saves an hour of wrong
construction.

**Phase B — execute.** One plan phase per session.

Rules:

- Before any mutation, read `progress.json` and only the current phase block from
  `city-plan.md`. Do not load the whole plan every turn.
- Never build anything not described in the current phase.
- If reality contradicts the plan — terrain blocks a district, funds run out, a bbox overlaps
  water — do not improvise. Stop, append to `## Amendments` explaining what changed and why,
  update the affected district or phase, then proceed.
- A phase is complete only when its acceptance criteria pass as literal API assertions. "It
  looks fine" is not completion. Record the passing values in `progress.json`.
- Track every created node, segment, and building id in `progress.json` under the phase that
  made it. That is the undo log: a failed phase can be bulldozed precisely without touching
  anything else.
- Save the game and commit `city-plan.md` + `progress.json` at every phase boundary. The git
  history of those two files is how you debug a run that went wrong four hours in.

Test of whether the externalised state is sufficient: start a fresh session with no history and
say "resume". It should read `progress.json`, identify the current phase, and continue.

## Known Gotchas

- CS1 network crossings are not intersections unless a real node is created. The bridge snaps
  endpoints within 8m onto existing nodes, which handles most of this; `snapDistance` tunes it.
  If a pipe or road visually crosses another segment without connecting, bulldoze and rebuild
  with a shared endpoint.
- Heating service buildings may create a connection helper offset from the building center.
  Query `/state/facilities?service=Water&includeMapObjects=true` when diagnosing heating pipes.
- Do not treat all dead ends as errors. Use bounded checks or `includeDeadEnds=false` unless
  the user asks to remove cul-de-sacs and stubs.
- Use `/state/zone-anomalies` when zone colours look mottled, or when circular paint left
  residential/commercial/industrial/office cells mixed in one block.
- `/commands/set-zone` defaults to `preserveOccupied=true`; check `/state/growables` first and
  keep that flag enabled unless the user explicitly wants to repaint developed blocks.
- If a whole block shows blue/green/yellow mottling, use `/state/zone-anomalies` and repair with
  `/commands/repair-zone-clusters` using `preferGrowableZone=true`.
- A composite command can hold the game thread for several seconds. That is expected; the HTTP
  timeout is generous and `opId` makes a retry safe.
