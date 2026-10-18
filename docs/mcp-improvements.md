# MCP and bridge improvements: findings from the first autopilot city

Source: the Westwood / "Claude 1" autopilot run, 2026-10-17. Each item is based on measured
evidence: a tool error tallied across every turn transcript in
`cities/claude-1-8bace5ee/autopilot/turns/*.jsonl`, a lesson the agents logged in `lessons.md`,
or a failure seen while setting up the loop. Ordered by how much autonomous-play time each one cost.
Open items are mirrored in `TODOS.md`.

## A. Connecting an agent to the bridge

1. **The MCP server starts too slowly for headless turns.** `.mcp.json` launches the server with
   `npx tsx ./mcp-server/src/index.ts`. In `claude -p` the server was still `pending` when the
   turn began, so the model saw no `cs1_*` tools. Two turns then "planned" a city they could not
   see. Starting `node mcp-server/dist/index.js` instead connects at once (verified).
   **Fix:** point `.mcp.json` at `dist`, add an `npm run build` check to `scripts/build.sh`, and
   fail fast when `dist` is older than `src`.
2. **A headless turn inherits every connector the user has.** Without
   `--strict-mcp-config`, a city turn loaded Gmail, Slack, Apollo, Lovable and Vercel (195
   tools). That wastes context and lets a city agent reach the owner's mail.
   **Fix:** ship a strict `mcp.json` for agent runs (the autopilot now writes
   `tmp/autopilot/mcp.json`) and document `--strict-mcp-config` in `docs/chat.md` and
   `docs/autopilot.md`.
3. **Permission-rule pitfalls in `claude -p`, all verified 2026-10-17:**
   - `Write(path)` allow rules are ignored; only `Edit(path)` covers Write.
   - Relative file rules did not match. Absolute rules need a leading `//`
     (`Edit(//Users/.../cities/x/**)`).
   - Compound Bash (`cd x && cat y`) never matches an allow rule, so it is denied. There were 11
     such denials across the turns.
   - `test -s file` returns only an exit code, which the model cannot see. It guessed wrong and
     stopped turns early.

   **Fix:** give agents MCP tools for these jobs: `cs1_chat_pending` (count of waiting
   messages) and a `cs1_save` that is locked to one name. Then no turn needs Bash.
4. **Saving to one fixed name.** `cs1_save` defaults to `AgentAutoSave`, and `save-city.sh`
   defaults to a timestamped name. One forgotten argument creates a second save.
   **Fix:** add a `CS1_SAVE_NAME_LOCK` (env var or file) that the MCP server and `/commands/save`
   both enforce, refusing any other name. Report `lastWriteTimeUtc` with sub-second precision;
   it is whole seconds today.

## B. Game lifecycle

5. **`/commands/new-game` drops `cityName`.** We asked for "Claude 1" and got "Westwood". The
   returned `gameInstanceIdentifier` (1d890076...) also differs from the loaded city's id
   (8bace5ee...). Something after `LoadLevel` regenerates both.
   **Fix:** apply the name (and read the real id) on the simulation thread after the level
   loads, and return the real id.
6. **No way back to the main menu, and no rename.** `new-game` refuses while a level is loaded,
   so the supervisor had to kill the game.
   **Fix:** add `POST /commands/quit-to-menu {save?:bool}` and `POST /commands/rename-city`.
7. **A Steam update makes direct launches exit after about 3 s**, with nothing in `Player.log`.
   The loop retried every 20 s for minutes.
   **Fix:** detect a pending Steam update (`appmanifest_255710.acf` StateFlags) and wait, or
   back off exponentially.
8. **A new game loads paused.** The first turns ran with the simulation paused.
   **Fix:** add `unpause:true` to `new-game` and `load-save`, or report `paused` in the `new-game`
   result.
9. **The cheat-mod list is hard-coded** (`LifecycleCommands.cs`). The owner's chosen mods needed a
   code change and a rebuild.
   **Fix:** read the allowed list from a per-city setting, such as a `player-mods:` line in
   `city.md`, sent with the request.

## C. Building

10. **The plan checker reads template comments as orders.** `templates/city/city.md` has an
    example `- NO-BUILD <name>: bbox ...` inside an HTML comment. The new-city script replaced
    `<name>`, and the checker then raised 7 HARD H-NOBUILD findings on every first road.
    **Fix:** `checker.ts` must skip `<!-- -->` blocks and any line that still holds a
    `<placeholder>`; the template should show the example with a different prefix.
11. **Utilities cannot cross roads mid-segment** (`H-CROSSING`: 7 refusals across power lines and
    water pipes). The agent learns this by trial and error.
    **Fix:** add a `routeAround: true` option on `build-network` for Power Line and Water Pipe.
    It would route through the nearest road nodes, or under the road, as the game's pipe tool
    allows. Failing that, the refusal should suggest the detour points.
12. **`place-building` collides with roads** (`ObjectCollision` for the clinic and school, moved
    by hand). The plain path defaults `validate:false`.
    **Fix:** default `validate:true`, and on a collision return the nearest free spot along the
    road frontage (`suggestedPosition`).
13. **`set-zone` with a radius spills onto the far side of perimeter roads** (1,280 cells zoned
    instead of the 8 intended blocks). This breaks the owner's "zone in moderate waves" rule.
    **Fix:** add `set-zone {blockIds:[...]}` or `{polygon}` modes, and a dry run that returns the
    exact cell count and block list before anything is written.
14. **Transit stops fail on placement.** Errors seen: "no Road segment within 12 m",
    "stops[2] snaps to the same position as stops[1]", and a created line whose "west stops
    report not connected".
    **Fix:** `transit_line_create` should snap each stop to the nearest pedestrian-capable segment
    and report the snapped point. It should validate the whole route (pathfind between
    consecutive stops) before it creates anything, and reject a line that cannot complete its
    loop.
15. **No accessibility tool**, which matters now that the owner requires every monument to be
    reachable by car, transit and sidewalk, and industry to be on transit.
    **Fix:** add `cs1_access_check {buildingIds | category: "monument"|"unique"|"industry"}`.
    For each building it would return:
    - **Car:** a road connection, and whether it reaches the highway.
    - **Transit:** the nearest stop's line ids and walking distance.
    - **Walking:** a link to the pedestrian network (sidewalk or path) and walking distance to
      the nearest stop.

    This turns the monument-access and commute KPIs into one state read.
16. **Zone-to-industry adjacency** (owner rule: no houses next to industry) needs a check, not a
    prompt.
    **Fix:** add an `H-ADJACENT` advisory to the plan checker: residential zone cells within N m
    of industrial cells, or sharing a road frontage with them.
17. **Construction cost is not shown in composite dry runs** (`build-grid`, `build-neighborhood`,
    `connect`). Already logged in TODOS (fair play).

22. **A road can't join a rail node, so there's no level crossing.** North Station had no road
    access because the only road was across the tracks. A road built to rail node 8918 ended on
    a new node instead of snapping onto the rail node (2026-10-17 20:33), so no level crossing
    formed.
    **Fix:** let `build-network` / `connect` snap a road endpoint onto an existing rail
    (train/metro ground) node and create the crossing the way the game's road tool does. Also
    report station front-side road access in `place-building`'s result, so a station is never
    placed facing away from every road.

## D. State reads and monitoring

18. **`/capture` returns a blank image on this install** (the mayor's note, 2026-10-17), so all
    visual checks fall back to state reads.
    **Fix:** debug the capture camera on macOS/Metal; until then the tool should say "blank" in
    its result instead of returning an empty PNG.
19. **There is no KPI endpoint.** The supervisor stitches metrics together from five calls and
    guesses field names (`problems.total` vs `count`, the `cityPassengersByType` shape).
    **Fix:** add `GET /state/kpis`. It would return population, the tourists count, transit
    riders by type (residents/tourists), unique buildings and monuments built, open problems by
    type, demand, and a traffic flow percentage, in one stable schema.
20. **Tool argument errors** (3 MCP input-validation failures: a zone enum guessed wrong, a
    missing `prefab`, a missing coordinate).
    **Fix:** put the valid enums in the tool descriptions, and add an alias map for common
    variants, such as "ResidentialHigh" vs "Residential High".
21. **"Building was not found"** for ids taken from an earlier state read (3 times). The
    buildings had already been removed.
    **Fix:** return `alreadyGone:true` with ok status for bulldoze-style commands, so the agent
    does not count it as a failure.

## E. Already fixed during this run

- bash 3.2: an empty-array expansion under `set -u`, and `case` inside `$( )`, both broke the
  supervisor (fixed).
- The fair-play gates (cost, unlock, owned tile, bypass flags) were added; they still need
  live verification.
- Editing a running bash script corrupts it. The supervisor now reloads itself between turns
  when its file changes; edits are written to a temp file and moved into place.
