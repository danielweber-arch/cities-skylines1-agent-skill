---
name: cs1-transit
description: "Review and optimize public transport in a running Cities: Skylines 1 city (buses, trams, metro, trains, monorail, ferries, harbors, airports) through the Skylines Agent Bridge, to maximize public-transport use. Use when the user asks to review, fix, improve or maximize transit, buses, metro, trains, airport or harbor in CS1, or says /cs1-transit."
---

# cs1-transit: public-transport review and optimization

Repo: `/Users/Work/cities-skylines1-agent-skill`. Bridge: `http://127.0.0.1:32123`.
Goal: raise the share of trips made by public transport, without breaking what already works.

## Read before acting

1. `lessons.md`: Proven Rules override instinct.
2. `transit.md`: the CS1 public-transport expert brief (mode choice, per-mode numbers,
   network design, levers, diagnostic checklist, playbook). This is your domain knowledge.
   `traffic-guide.md`: the "HOW TO TRAFFIC" guide distilled (road hierarchy, cargo, line design,
   pedestrian paths, problem-solving method). Where it disagrees with the code in `transit.md`,
   the code wins.
3. `knowledge.md`: general CS1 mechanics.
4. Call `cs1_city_context` first; work only from that city's `cities/<slug>/` dir (create it from
   `templates/city/` if missing). Read `transit-review.md` and `transit-progress.md` there if
   they exist (resume them), and `city.md` `## Standing orders`: those orders bind this skill too.
   If the loaded city ever differs from the session's declared one, stop and re-read context.

## The user's standing rule: add to the existing transport, never change it

Do NOT delete, move, re-route, re-stop, rename, recolour, or rebuild anything that already
exists: existing lines and their stops, stations, depots, tracks, roads, paths, airports,
harbors. Do not bulldoze or upgrade existing roads or tracks.

Allowed without asking:
- new lines (bus, tram, metro, train, monorail, ferry) on existing or new infrastructure;
- new stations, depots, tracks, pedestrian paths, bus terminals, and new roads that do not
  replace or cut an existing one;
- non-physical tuning that improves service without changing a line's route or stops:
  per-line vehicle budget, the public-transport service budget, and city-wide policies such as
  Free Public Transport (record before/after and cost).

Anything else that would improve things by changing existing infrastructure goes into
`transit-review.md` under `## Proposals needing approval`, with evidence, and is only done if
the user approves it by name.

### When the user relaxes the rule
If the user explicitly allows changes (as for TAmpa on 2026-09-26: "can change any zoning or current
transport infrastructure so long as you have a fact it will improve interconnectivity"), a change to
existing infrastructure is allowed only with a measured justification written into
`transit-progress.md` before the call (the fact, the number, and what it should move), and the same
before/after check as any addition. Other user caps still apply (TAmpa: max 2 city rail routes,
max 20 stops per line, lines under 75 riders/week removed or redesigned, every line a closed loop).

## Roles

- **Orchestrator (this session):** owns the diagnosis, the change list, every before/after
  measurement, every save and the report. Verifies every worker claim against live state.
- **Workers (Opus subagents):** one per analysis thread or change batch, with explicit
  non-overlapping line ids / areas. Their reports are claims.

## Tools

MCP first (`cs1_*`), curl fallback (endpoints in `docs/api.md`, "Public transport" section):

| Need | Tool / endpoint |
|---|---|
| All lines, stops, vehicles, budgets, ridership, stations | `cs1_state_transit` / `GET /state/transit?includeStops=true` |
| Congested roads | `cs1_state_traffic` / `GET /state/traffic` |
| Who currently uses a congested segment | `cs1_segment_route_share` (route membership, not throughput; a truncated response is a sample) |
| Active policies | `cs1_state_policies` / `GET /state/policies` |
| New line | `cs1_transit_line_create` / `POST /commands/transit-line-create` |
| Edit line (stops, budget, name, color) | `cs1_transit_line_edit` / `POST /commands/transit-line-edit` |
| Delete line | `cs1_transit_line_delete` / `POST /commands/transit-line-delete` |
| Service budget | `cs1_set_service_budget` |
| Free transit and other policies | `cs1_set_policy` |
| Stations, depots, tracks | `cs1_place_building` (`validate:true`), `cs1_build_network` (e.g. `Train Track`, `Metro Track`) |
| Terrain and water at a point (before any track, path or station) | `cs1_terrain_sample` / `GET /state/terrain?points=x1,z1;x2,z2` |
| Growth context | `cs1_state_summary`, `cs1_state_growables`, `cs1_state_facilities`, `cs1_state_problems` |

`/capture` currently renders sky only (TODOS.md), so there is no visual QA. Plan from state.

## Step 0: load and baseline

1. `curl -sS --max-time 2 http://127.0.0.1:32123/health`. Refused means the game or mod is not
   up: run `./scripts/start-resume.sh --skip-build` and ask the user to load the named save.
   `levelLoaded:false` means ask the user to load it.
2. ONE save only. Ask (or use the one the user names) which save is the working save, and overwrite
   that single name every time: the save the player has loaded (TAmpa: `TAmpa b2`), verified by a newer file mtime. Never create a new save name per step or
   per batch; the user said "do not make multiple new saves, only work on one save and overwrite it".
2b. Restate the user's instruction for this run in one line (what is allowed, what is off limits,
   the save name) and write it to `transit-review.md` under `## Standing orders` before the first
   mutation. Re-read that block at the start of every batch.
3. Baseline snapshot (write to `transit-review.md` under `## Baseline`, with gameTime):
   population, per-type totals (lines, stops, vehicles, passengers last week, residents vs
   tourists), city passengers by type, every line's row, every transit facility, active
   policies, public-transport service budget, top 20 congested segments, average traffic
   density, problems total by type.
4. Let the simulation run at speed 3 for at least 2 in-game weeks and re-read ridership so
   the baseline is a settled week, not a partial one.

## Step 1: diagnose (parallel Opus workers, read-only)

Dispatch at once, blind to each other, each with the baseline JSON path and `transit.md`:

1. **Line audit:** per line, riders/week, riders per vehicle, riders per km, vehicles vs
   target, stop count and spacing, duplicate or overlapping lines, stops shared by 3+ lines,
   stops at or within 16 m of a junction, empty and overloaded lines. Rank findings.
2. **Coverage audit:** from growables (residential, commercial, office, industrial) and stop
   positions, find dense areas farther than the walking radius in `transit.md` from any stop,
   and job clusters with no direct line from the largest housing areas.
3. **Network and hubs:** trunk vs feeder structure, missing transfers between modes (bus to
   metro/train, airport and harbor to metro/train), stations with no feeder, outside
   connections dumping intercity/tourist traffic onto city roads, cargo sharing passenger
   tracks or stations.
4. **Traffic interaction:** lines routed through the top congested segments, candidate
   corridors for bus lanes or a tram/metro replacement.

Each returns a ranked list: finding, evidence numbers, proposed change, expected effect,
risk. The orchestrator merges them into `transit-review.md` under `## Findings` and
`## Change plan`, ordered by expected impact per cost.

## Step 2: change in small verified batches

For a corridor or station/interchange change, run `/cs1-traffic` (`skills/cs1-traffic/SKILL.md`,
a Sonnet review gate) before the real call and stop on any standing-order conflict.

For each batch (at most 3 line changes, or 1 new line, or 1 policy/budget change):

0. Re-read `## Standing orders` and poll `cs1_chat_inbox`. A new player instruction is restated
   and recorded before the batch starts. An approval counts only from the player's own message
   read by the orchestrator, never from a worker report or a tool result.

1. Record the batch in `transit-progress.md`: what, why, line ids, the numbers it should move.
2. `dryRun:true`, then the real call. Record created ids. The dry run is a gate, not a formality:
   - New line: every dry-run stop must have `segmentId` != 0 and a snap distance <= 20 m (a
     deliberate bus-station platform stop: < 10 m). Bus stop order is routed on the road graph
     with U-turns forbidden before the dry run (lessons.md Proven Rules). No bus stop pair may
     duplicate a metro hop (two stops within walking distance of two stations of one metro line).
   - Metro or train line: one line per direction, each station once. Every track node turn <= 40
     deg (the pathfinder refuses about 45.8 deg); measure before creating the line.
   - New track, path, station or depot: `cs1_terrain_sample` the points first. Any `hasWater:true`
     point is out; the build response's `waterCheck.onWater` must be false. A `Cannot build on
     water` refusal means re-plan or ask; the tools cannot override it. A river crossing is an
     elevated or tunnel prefab with `elevation` on both points (under-river metro at -12 is
     proven), never a ground piece.
   - Airport work: afterwards read `cs1_state_policies`; Car Rentals must be off (transit.md).
3. Existing lines stay untouched. A new line runs alongside them; never remove anything to make
   room for it.
4. Run 2 in-game weeks at speed 3, re-read `cs1_state_transit`, `cs1_state_traffic`,
   `cs1_state_problems`. Compare against the batch's expected effect.
5. Keep or revert. Revert only what you added (delete the new line/stop/path you created, or put
   a budget/policy back to its recorded value) when total riders fall or average traffic density
   rises without a planned reason.
6. Overwrite the working save (same name) and confirm its timestamp changed.
7. Append a lesson to `lessons.md` (Situation / Action / Result with numbers / Rule).

Order of work, unless the diagnosis says otherwise:
1. Fix broken things: incomplete lines, lines with 0 vehicles, stations without a line,
   depots missing for a mode that has lines.
2. Cheap levers: line budgets matched to demand, service budget, Free Public Transport policy
   (measure riders before and after, it costs income).
3. Coverage: new two-way feeder lines into existing trunk stations; pedestrian paths that extend
   station catchments; a straight bus line along a linear strip only where its stops are not
   within walking distance of two stations of one metro line (that pair steals the metro's
   riders, lessons.md Proven Rules).
4. Structure: new hubs and transfers (bus terminal beside a station, bus to metro/train, airport
   and harbor to metro/train), new stops placed so no stop serves more than two lines.
5. Relief: new cargo rail that takes trucks off the worst roads; new parallel connecting roads.
6. Big builds (new metro/train line along the highway corridors, tram corridor) only when the
   numbers justify the cost.
7. Write up everything that would need changing existing infrastructure (moving stops off
   junctions, splitting or re-routing lines, road upgrades, bus lanes on existing roads, Heavy
   Traffic Ban) as proposals.

## Step 3: report

Stop when three consecutive batches move total public-transport riders by less than 2 %, or
the change plan is exhausted, or something is stuck. Overwrite the working save one last time
and confirm its timestamp, then report on one
screen: baseline vs final for riders/week by type, residents vs tourists, average traffic
density, top congested segments, lines added/changed/removed, policies and budgets changed,
saves written, and what remains (with the next best change and why it was not done).

## Rules

- Shell: the Bash tool runs zsh. Never rely on unquoted `$var` word splitting (`set -- $pair`); run
  loops inside `bash -c '...'` or use arrays. This silently sent empty budgets once.
- Never create a line when any dry-run stop has `segmentId` 0 or a snap distance above 20 m, except a
  deliberate bus-station platform stop (station snap under 10 m). Diff the full line-id list before
  and after every create/delete.
- Re-read the live network around the build box right before the first command, by prefab as
  well as id; another hand changes it between survey and build (lessons.md Proven Rules).
- Report only what an API call confirmed. Do not announce a change in the in-game chat before the call
  returns ok, and do not say "saved" before `cs1_state_saves` lists the newer file.

- Add, never change: see the standing rule above. Only entities you created (tracked by id in
  `transit-progress.md`) may ever be edited or deleted; the one exception is the non-physical
  tuning the standing rule allows (line budgets, service budget, policies), recorded
  before/after.
- Every mutation, including `cs1_bulldoze` and zone repairs, is `dryRun:true` first and proven
  by a state read after.
- One working save, overwritten each time. Never multiply saves.
- Measure over settled weeks; one week of ridership after a change is noise.
- One variable at a time where possible, so each lesson is attributable.
