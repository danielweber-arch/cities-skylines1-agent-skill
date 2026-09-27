# TODOS

Findings and follow-ups. `[x]` means fixed and verified in this repo; the rest are real but
deliberately out of scope, with enough context to pick up cold.

## Fixed — macOS port (branch `macos-port`)

- [x] **Save directory was Windows-only** — `src/SaveCommands.cs:105`
  `Path.Combine(GetFolderPath(LocalApplicationData), "Colossal Order\\Cities_Skylines\\Saves")`
  had two bugs on macOS: Mono resolves `LocalApplicationData` to `~/.local/share`, and the
  embedded backslashes are literal filename characters on Unix. Verified under Mono 6.14: the
  original expression produces `/Users/<me>/.local/share/Colossal Order\Cities_Skylines\Saves`,
  which does not exist, so `/state/saves` returned an empty list and `/commands/save` reported a
  path no file ever appeared at. Now resolved through `ColossalFramework.IO.DataLocation.saveLocation`
  (confirmed by disassembly to be `localApplicationData + "Saves"`, not the Steam Cloud folder),
  with a platform-aware fallback.
- [x] **Culture-sensitive query parsing** — `src/ApiServer.cs`
  Measured under Mono 6.14 with `CurrentCulture = de-DE`: `float.TryParse("18.5")` returns
  **true with the value 185** — `.` read as a group separator. `?nearMissDistance=18.5` silently
  applied a threshold 10x too large. Now `CultureInfo.InvariantCulture` throughout.
- [x] **Culture-sensitive `Content-Length` match** — `src/ApiServer.cs`
  Now `StringComparison.OrdinalIgnoreCase`. Hardening, not a bug fix: the suspected Turkish
  `I`/`i` failure was tested under `tr-TR` and does **not** reproduce, because `Content-Length`
  contains no `i`.

- [x] **Every `.ps1` helper now has a bash + curl + jq twin** — `scripts/*.sh` (2026-09-25)
  `smoke-test`, `save-city`, `inspect-road-anomalies`, `repair-road-anomalies`,
  `develop-starter-city`, `develop-city-with-infrastructure`, `repair-service-overlap`,
  `log-city-parameters`, `check-doc-links`, `start-resume`, `start-new-map`. Piece-for-piece:
  same endpoints, defaults, bodies (built with `jq -n`, so coordinates and `dryRun` are typed),
  sleeps, and save names. Bash 3.2 compatible. Verified against a mock bridge only; nothing has
  been run against the live game. The `.ps1` originals stay as legacy Windows tooling.
  The two launchers replace the `user32.dll` mouse clicks with an operator prompt and poll
  `/health` until `levelLoaded == true`; `CS1_NO_LAUNCH=1` skips the Steam `open` for testing.

## Fixed — v0.4 upgrade

- [x] **Node reuse was 2m and O(all slots)** — `src/NodeHelper.cs`
  `RoadCommands.FindNearbyNode` snapped at 2m and scanned all 32768 node slots. At 2m an agent's
  rounded coordinates land *next to* a junction rather than on it, which is the documented
  "looks connected, is not connected" gotcha. Now an 8m snap over the `m_nodeGrid` cells covering
  the search radius only. `NodeHelper` is the single place nodes and segments are created.
- [x] **Composite commands** — `src/CompositeCommands.cs`
  `/commands/build-grid`, `/commands/build-neighborhood`, `/commands/connect`. Lattice built
  nodes-first then segments, rollback on failure, `opId` idempotency, bounded at 400 cells
  (144 for a neighborhood, where zoning is the expensive half).
- [x] **Capture endpoint** — `src/CaptureCommands.cs`
  `GET /capture` renders an off-screen orthographic top-down PNG with an info-view overlay.
  Implemented as a frame-driven state machine because `InfoManager.SetCurrentMode` fades in over
  several frames — rendering in the same frame captures the previous overlay.
- [x] **Unbounded `Content-Length` allocation** — `src/ApiServer.cs`
  Was `new byte[contentLength]` on an unvalidated client number; `Content-Length: 2000000000`
  would have asked Unity for 2 GB and taken the game with it. Now capped at 4 MB, negatives
  rejected, and a truncated body is an error instead of being parsed as complete.
- [x] **IPv4-only listener** — `src/ApiServer.cs`
  `localhost` resolves to `::1` first on macOS, so the most natural URL an agent types hit a
  refused connection. Now binds `127.0.0.1` and `::1` separately. Still loopback only.
- [x] **`Debug.Log` from background threads** — `src/BridgeLog.cs`
  Unity's logger is not thread-safe on the runtime CS1 ships. Accept and worker threads now
  buffer messages; the game thread drains them once per frame.
- [x] **Wait handle leak** — `src/CommandQueue.cs`
  Two `ManualResetEvent`s per request were allocated and never disposed. Each is an OS file
  descriptor and macOS ships a far lower default `ulimit -n` than Windows, so a long session
  would eventually fail to open anything. Now one per request, disposed via a two-party release
  so neither thread can dispose while the other still holds it, and a timed-out command is
  abandoned rather than executed after its caller gave up.
- [x] **No server shutdown / `/health` unreachable before a city loaded** — `src/AgentBridge.cs`
  The listener started at level load, so `/health` was a refused connection from the main menu —
  indistinguishable from "the mod is not installed". Now starts at mod enable and stops at mod
  disable, and `ApiServer.Stop()` exists.
- [x] **`AcceptLoop` hot-spin** — `src/ApiServer.cs`
  Gives up after 20 consecutive accept failures instead of burning a core.
- [x] **MCP layer** — `mcp-server/`
  32 typed tools, arguments validated before the game sees them, and response filtering.
  Measured: `/state/networks` with 1500 segments goes from ~56,900 tokens raw to ~1,200 filtered.

## Open — not done, with reasons

- [ ] **Nothing has been exercised inside a running game.** Everything above is verified by
  compilation against the real CS1 assemblies, by executing the built DLL's parsing and zone-mix
  logic under Mono, and by driving the MCP server against a mock bridge. The parts that can only
  be proven in-game are: node snapping against real geometry, `SetZone` coverage per block, the
  `/capture` camera actually picking up the scene, and composite command timing. Run the
  acceptance tests in `SKILL.md` first.
- [ ] **`/commands/set-simulation-speed` unpause threw once: "InvalidOperationException: Already in
  the same thread. Call directly"** — `src/SimulationCommands.cs:24`. Measured 2026-09-25: the
  first `paused:false` after level load (following nine build-network calls) returned that error,
  yet game time advanced; two later `paused:false` calls and every `paused:true` call succeeded.
  Likely `SimulationManager.SimulationPaused`'s setter dispatching to the main thread from the
  main thread on a specific first-transition path. Not reproduced yet; wrap the setter in a
  try/catch that reports the resulting `SimulationPaused` state, and log the stack.
- [x] **Any web page can drive the city (CORS `*`)** — FIXED 2026-09-26: requests with an `Origin` header and OPTIONS preflights now get 403; CORS headers removed.
  Original finding: — `src/ApiServer.cs` `HttpResponse.Write` sends
  `Access-Control-Allow-Origin: *` and OPTIONS returns 200, so a page open in the user's browser
  can POST commands to 127.0.0.1:32123, and since the chat panel, inject `/chat/send` text an agent
  acts on. Found by the chat-panel worker 2026-09-26. Fix: drop the CORS headers and reject any
  request carrying an `Origin` header (curl, the MCP server and scripts never send one).
- [ ] **Headless chat agent inherits the repo allow rules** — `scripts/chat-bridge.sh` runs `claude -p`
  with `dontAsk`, but `.claude/settings.local.json` allows `Bash(./scripts/*)` and
  `Bash(curl http://127.0.0.1:32123/*)`, so it can run any repo script. Documented in docs/chat.md.
- [ ] **TAmpa: Harbor02 is not reachable by bus in both directions** — measured 2026-09-26: a bus loop from the
  Harbor02 terminal platform to Jackson Ave / Laurel Blvd got LineNotConnected on the legs into and out of
  the terminal. The Greenaway/Finch/Laurel Bridge area joins the city only through one-way highway pieces
  (Laurel Highway 14996, Stephanie Lee Highway between nodes 18355 and 25579). Fix (an addition): a new
  two-way road, e.g. near node 18355 to Finch node 17258. Needs the user's go-ahead on where.
- [ ] **TAmpa: congestion snapshots are unstable** — live density differed hugely from the baseline minutes
  apart (30101: 61 -> 100, 29347: 68 -> 97). Average several reads before calling a segment congested.
- [ ] **User's incomplete lines 22 and 189 disappeared during TAmpa batch 2** (2026-09-26). Both were
  1-stop "Created" bus lines with their stop in the Harbor02 bus terminal (nodes 22204, 9667). They were
  present in the baseline and gone after the Harbor Shuttle line 197 was created (one stop snapped 103 m
  in station mode to "segment 0") and then deleted with transit-line-delete. Cause unconfirmed: suspect
  station-mode snapping onto the terminal platform shared their stop and ReleaseLine(197) or the game's
  line cleanup removed them. Fix: refuse station-mode snaps for Bus unless the point is inside a bus
  station, never create when any dry-run stop snapped > roadSnapDistance, and log every ReleaseLine id.
- [x] **`SetLineName`/`SetLineColor` throw "Already in the same thread"** — fixed 2026-09-26 in
  `src/TransitCommands.cs` `ApplyLineProperties`: queued with `SimulationManager.AddAction`, as the game
  panel does. Takes effect after a game restart.
- [ ] **Transit endpoints are unproven in-game** — `src/TransitCommands.cs`, `src/TransitState.cs` (2026-09-26).
  Stop snapping is an approximation of `TransportTool.GetStopPosition` (flat-distance ranking with
  fall-through instead of the camera raycast; station distance to the building pivot; elevation on
  stacked networks not controlled; platform chosen with the bridge's own spawn seed). Loop closure
  relies on `TransportLine.AddStop` closing within sqr 6.25 of the first stop, never run. A line can
  come back `Complete` and ok:true and later show `LineNotConnected` (paths are async). Line budget
  accepted 0..500 and ticket-price range unconfirmed (UI prefab data). Edits are not atomic when the
  game refuses a step midway. Bus-lane detection heuristic unverified.
- [ ] **Line and policy mutations run on Unity's main thread, not the simulation thread** — the game UI
  applies them through `SimulationManager.AddAction`. Possible data race, same risk as the existing
  `CreateBuilding` path. Fix: route mutations through `SimulationManager.AddAction` and wait.
- [x] **Chat panel off the right edge on 16:10 screens** — fixed 2026-09-26 in `src/ChatPanel.cs`: position and
  size clamp to the visible width (fixedHeight x Screen aspect) every frame on resolution change. Needs a restart.
- [ ] **Chat panel: partly proven in-game** (2026-09-26: the player sent 6 messages from the panel with camera and
  selection attached, and replies/updates rendered). Still unconfirmed: — `src/ChatPanel.cs`: hotkey swallowing while typing, Enter
  keeping focus, Esc not opening the pause menu, Ctrl/Cmd+Shift+C toggle, sprite names, layout.
- [ ] **`build-network` cannot join track to platform, cargo, elevated or bridge nodes** — `src/NodeHelper.cs:297`
  `CanReuseNode` only reuses a node of the identical prefab or road-to-road, so "Train Track" built to
  a station platform node creates an unconnected node on top. `dryRun` returns before snapping, so it
  cannot catch it. Found 2026-09-26 (TAmpa rail analysis). Fix: also accept same service + subService.
- [ ] **`/commands/connect` builds a road into the building it is aimed at** — `src/CompositeCommands.cs:127`.
  `NodeHelper.FindOrCreateNode(from, ...)` creates a node at `from`, then a segment to the nearest
  road node, so `from` = a building's position drives a road through that building. SKILL.md,
  the MCP server instructions and the cs1-city skill all say "always cs1_connect after placing a
  service building", which is harmful as written. Fix: start from the building's front/road-access
  point (AI `m_info` + angle), or refuse when `from` is inside a building footprint.
- [ ] **Dry runs cannot size zoning changes** — `src/ZoneCommands.cs:239` (`repair-zone-clusters`
  skips counting under dryRun, always reports `repairedBlocks 0, changedCells 0`) and `:88`
  (`set-zone` dry run returns no `changedCells`). Measured 2026-09-25: dry run 0, real run 1,318
  cells. Fix: count candidate cells in the dry path.
- [ ] **`place-building` does no footprint or collision check**, dry or real — found 2026-09-25 when
  a worker had to write its own footprint checker. Fix: test the prefab footprint against
  segments and buildings before creating, and report overlaps in the dry run.
- [ ] **Services placed flush with a road still show `RoadNotConnected`** — Elementary School 30955
  and Regular Playground 43893 (Ashford, 2026-09-25), angle 90 facing an x=2120 road; the
  operator's Library 47794 at the same geometry is connected. `RoadAccessFailed` cleared but the
  notification persists. Cause unknown.
- [ ] **`/state/facilities` hides Beautification buildings** unless `includeMapObjects=true`, so
  placed parks and playgrounds look missing.
- [ ] **`/capture` renders sky only in-game** — `src/CaptureCommands.cs`. Measured 2026-09-25 on a
  fresh map: `GET /capture?x=1000&z=1970&size=1500&mode=None` returned HTTP 200, a 1024x1024 PNG
  that is a pale teal-to-white radial gradient with no terrain, road or water, and
  `X-Bridge-Distinct-Colors: 9`. A second capture at the interchange (620,1960, size 600) was
  identical. The flat-colour guard does not trip because the gradient has 9 colours. The
  off-screen camera is not picking up the scene (the risk noted under "may render blank"
  below). Fix candidates: render through the game's `RenderManager`/main camera path, or
  reposition `CameraController`, render, restore. Until fixed, plan and verify from state APIs
  only.
- [ ] **`/capture` may render blank.** CS1 draws through its own `RenderManager` tied to the main
  camera, so a second camera may not pick up terrain and buildings. The endpoint detects a
  uniform image and returns an error rather than a valid PNG of nothing, so the failure is loud.
  If it does come back blank, the fallback is to reposition the game's own camera via
  `CameraController`, render, and restore — visible to the player but guaranteed correct.
- [ ] **Zone paint radius bleeds between blocks** — `src/CompositeCommands.cs` `ApplyZoneMix`
  Zone blocks sit alongside roads, so a radius of `spacing/2` around a block centre can also
  catch blocks belonging to the adjacent cell across the road. The per-block `changedCells`
  in the response is how you detect it. A precise fix needs block-to-cell mapping rather than
  a radius query.
- [ ] **Query pairs split on every `=`** — `src/ApiServer.cs`
  `pairs[i].Split('=')` yields more than 2 parts when a value contains `=`, and the pair is then
  ignored because the code requires `parts.Length == 2`. No current parameter takes an `=`.
- [ ] **`--dry-run` still unpauses the game and writes a real save** —
  `scripts/develop-city-with-infrastructure.sh`, `scripts/repair-service-overlap.sh`
  (inherited from the `.ps1` originals). `src/SimulationCommands.cs` reads only `paused`/`speed`
  and `SaveCommands.Save` reads only `name`, so the `dryRun:true` the scripts now send is
  ignored on those two endpoints. Impact: a "dry run" of either script sets speed 3 and creates
  an `AgentAutoSave-*.crp`. Fix: honour `dryRun` in both handlers (return the would-be result
  without acting), or skip the two calls under `--dry-run` in the scripts.
- [ ] **Original `repair-road-anomalies.ps1` bbox test only checked the stub's start point** —
  `scripts/repair-road-anomalies.ps1:32`. `(In-Box $item.start -or In-Box $item.end)` is parsed
  in PowerShell command mode as one `In-Box` call with `$item.start` as the argument and the
  rest as extra args, so a stub whose start is outside the box but end inside was never
  bulldozed. Not verified by running PowerShell (none on this machine); rests on argument-mode
  parsing. The bash port implements what the code says (start OR end), so it will remove a stub
  that straddles the box edge where the original would not. Decide which behaviour is wanted.
  The port also skips a null `ownSegmentId` where the original cast it to `0` and bulldozed
  segment 0.
- [ ] **`check-doc-links.sh` has never run on Linux** — `.github/workflows/docs.yml` now calls it
  on `ubuntu-latest`. Written for BSD grep/sed and GNU both, but the first CI run is the first
  Linux run. Existence checks are case-sensitive there; the old Windows run was not.
- [ ] **Scripts require curl 7.76+ (`--fail-with-body`)** — every `scripts/*.sh` API helper. Fine on
  macOS (8.7) and Ubuntu 22.04 (7.81); Ubuntu 20.04 (7.68) rejects the flag with exit 2. Found by
  the Cursor review 2026-09-25. Fix if it matters: capture `-w '%{http_code}'` and branch.
- [ ] **`save-city.sh` timeout edge** — like the original, no final existence check after the
  last 3 s sleep, so a file that lands in the final interval still reports a timeout.
- [ ] **`README.ja.md` and `docs/ja/**` still describe the Windows/PowerShell workflow.**
  The English README, SKILL.md, and docs/api.md were ported; the Japanese translations were not.
- [ ] **No cost model.** The agent can bankrupt the city and will not see it coming.
  `/state/economy` exists but nothing forces a spend check before a build. A `budgetRemaining`
  guard inside the composite commands would be cheap insurance.
- [ ] **Polling is still the model.** An SSE `GET /events` endpoint pushing problems as they occur
  would beat asking twelve endpoints whether anything broke — a bigger token win than the MCP
  filters.
- [ ] **Traffic is untouched.** The bridge can *detect* congestion via `mode=Traffic` and
  `/state/problems` but has no tools to fix it beyond bulldoze-and-rebuild. Real CS1 skill is
  intersection design and network hierarchy; that is the v2 target.
- [ ] **Exception detected by English message substring** — `src/GameThreadHelpers.cs:86`
  Left as-is: the string is hardcoded English in the shipped assembly.

- [x] `scripts/save-city.sh` reported success on an overwrite without waiting for the new write: it polled only for file existence (scripts/save-city.sh, loop after `/commands/save`). Impact: a save announced as done was not done (TAmpa b2, 2026-09-26 17:0x). Fix: require mtime >= request time and a stable size. Fixed in the commit that adds this entry.
- [ ] City Train Line 69 (31333 <-> 47366) shows LineNotConnected on both stops after 3 game days; no track path between the two stations in at least one direction. Rail planner tracing the graph.
- [ ] Bridge dryRun validates only the prefab: `place-building` dryRun passed a Harbor at (0,0) (src/BuildingCommands.cs:36), `build-network` likewise (src/RoadCommands.cs:34). Impact: dry runs cannot vet harbor/shore placements. Fix: run the prefab's own CheckBuildPosition / NetTool validation in dryRun.
  - [x] place-building half: `"validate"` (default on for Shoreline prefabs) runs BuildingTool's shore snap + BuildingAI.CheckBuildPosition + CheckSpace(test) on the simulation thread (42c8aaf). **Unverified in game until the next restart**: first use must be a dryRun on a known-good harbor (e.g. near 23322) to confirm it reports canPlace true there and ShoreNotFound at (0,0). `build-network` half still open.
- [ ] Bridge cannot build level crossings (track and road never share a node, NodeHelper.CanReuseNode), elevated track (nodes at terrain height), or read Train Track m_maxTurnAngle (read offline 2026-09-26 B15: 45 deg for Train Track, Train Track Elevated, Metro Track and Metro Station Track, sharedassets11.assets; the bridge still does not expose it); `/state/networks` does not expose segment flags (one-way direction, PathFailed). Impact: 47366 chord gap across Dixon St had to be drawn by the player; ship-lane breaks unmeasurable.
- [ ] TAmpa ship lanes are 3 disconnected components (harbor-plan F2): west connection 16546 and southwest 48864 dead-end away from the bay. Unconfirmed that their intercity ship lines PathFail. Needs segment flags (above) to measure.
- [x] Harbor02 access: the suggested 18355 -> Finch road was wrong (18355 is in an 11-node pocket joined only by one-way 14996). Built Laurel Link + Harbor02 Access instead (B10).
- [x] build-network of any PlayerNetAI prefab with a pass milestone (Metro Track etc.) threw "Already in the same thread" and left a half-built segment (src/NodeHelper.cs CreateSegment). Fixed: milestone replayed on the simulation thread.
- [ ] NodeHelper.Rollback releases created nodes even when an exception escaped after CreateSegment allocated a segment, leaving a segment with endNode 0 (seen: 31100). Fix: on exception, look up segments attached to created nodes and release them first.
- [ ] Bulldoze of buildings 5177 (crematory) and 24712 (house) returned HTTP 500 and they stay flagged Created|Deleted (services agent, GameThreadHelpers.ReleaseBuilding). Re-check after reload.
  Update 2026-09-26 (services round 2): still stuck after two reloads; retries return ok:true and do nothing. A third one, 1621, failed the same way with `IndexOutOfRangeException: Array index is out of range` (1 of 5 bulldozes; 2 of ~20 in round 1); no trace in Player.log. Likely cause: `/commands/bulldoze` runs on the Unity main thread (ApiServer.cs:402 RunOnGameThread) and `GameThreadHelpers.cs:8-34` catches the same-thread guard and invokes `ReleaseBuildingImplementation` by reflection, i.e. off the simulation thread and racing it; the exception lands after `Deleted` is set, and later calls skip the building. Impact, measured: 24712 (also flagged `Demolishing`) accumulated **226 growables stacked on its exact position (2809.8,29.7)**, which alone produced 117 of 128 Pollution, ~34 Death and ~38 Abandoned entries in /state/problems at 2035-09-09 (the lot sits 25 m from factory 1204). Fix: queue the release with `SimulationManager.AddAction` (the CommandResult.Deferred path) and await it; add a repair command that clears `Deleted` on a stuck building and re-releases it. Until fixed, avoid building bulldozes.
- [ ] **set-building-active does not stop a building** — `src/BuildingCommands.cs` SetBuildingActive (found 2026-09-26, services round 2). It clears `Building.Flags.Active` and calls the AI's ManualActivation/ManualDeactivation, but never zeroes `m_productionRate`; the game recomputes `Active` on the next simulation step. Measured: tower 11872 set inactive at 2035-11-20 read back `Created, Completed, Active` at 2035-12-11 with no one re-enabling it; pump 21582 (turned off in session 1) was likewise found Active after restarts. Impact: both water-source isolation tests for DirtyWater were invalid, and any caller that believes it turned a building off is wrong. Fix: do what the info panel's on/off button does (set `m_productionRate` to 0/100 on the simulation thread via AddAction) and verify production after one step.
- [ ] place-building dryRun checks only the prefab name: two shops (5913, 21373) were destroyed by overlapping crematoria placed from a stale list. Fix: footprint collision test in dryRun (services agent's act.py has one).
  Partly addressed by 42c8aaf: `"validate":true` adds BuildingTool.CheckSpace (ObjectCollision) for any prefab; it is opt-in for non-shore prefabs, so leave open until services scripts pass it.
- [ ] place-building (validated or not) uses BuildingManager.CreateBuilding, which does not create sub-buildings (the tool's CreateBuilding iterator adds them). Impact: a bridge-placed Harbor02 (Harbor-Bus Hub) would lack 'Harbor02 Sub' and its second dock. Validated path now refuses such prefabs (42c8aaf); the unvalidated path still places them partially. Fix: replicate the sub-building loop from BuildingTool.<CreateBuilding>c__Iterator0.
- [ ] Harbor02 31587 also serves the intercity ship lines: dock (4207,-2024) <-> E connection (Ship Line 13435/19941) and Harbor02 Sub dock (4356,-2084) <-> W (6267/12268) and SW (4905/17268). Measured 2026-09-26 from /state/networks. Harbor 23322 carries the same three, so bulldozing Harbor02 removes duplicates, not the only link. See tmp/tampa/harbor-move-plan.json step f.
- [x] Station M2 46988 shows Electricity, MajorProblem (rail-plan-2 rank 0 adds power from node 4953). Fixed 2026-09-26 by power line seg 30142 (4953 -> 5585); no problem after 1 game day. Not committed yet.
- [ ] Core2 station 36478 (1760,2265.6) shows "Water, MajorProblem" and is inactive (not Active) since placement (rail-plan-2 rank 1, B12). Line 78 still runs 2/2 trains. Nearest water pipe node 8358 at (1760,2162), ~80 m south of the footprint. Fix: a Water Pipe from 8358 to the station (needs the player/orchestrator OK; the rail brief forbade touching water networks).
- [ ] Bulldoze of netSegment 23658 threw HTTP 500 IndexOutOfRangeException once, then succeeded on retry (B12). Cause unknown; related to the building-bulldoze 500s above? Needs a stack trace from the game log.

- [ ] **Metro out-and-back stops cannot be told apart in a dry run** — `src/TransitCommands.cs:1090-1117` (found 2026-09-26, B14)
  For an underground `Metro Entrance`, all 12 `CalculateSpawnPosition` seeds return the platform-track midpoint. So
  `transit-line-create` resolves the outbound and return visits of a station to the same point (0.0 m apart). The metro
  plan's check (`metro-plan.json` bridgeIssues: "outbound and return stops ... differ > 4 m") can never pass. Measured on
  8 TAmpa stations with two sets of request points. Impact: blocks M1/M2/M3 line creation under the current gate. The
  game's TransportTool uses the same code, so a player-drawn line has the same stop points. Fix: drop or replace the
  gate (create the line, then require no LineNotConnected and vehicles == target), or expose the lane/platform the
  stop will use.
- [ ] **`place-building` `validate:true` dry run does nothing for Metro Entrance** — `src/BuildingCommands.cs` (found 2026-09-26)
  A dry run at a lot occupied by growable 6066 returned only `"Place-building validation passed."`: no `validated`,
  `toolErrors` or `canPlace` fields. docs/api.md says `validate:true` runs CheckBuildPosition + CheckSpace for any
  prefab. Either the dryRun returns before validation for non-shoreline prefabs, or the loaded DLL predates it. Not
  investigated. Impact: a caller trusting the docs gets no collision check. Fix: run validation on dryRun too, or fix
  the docs.
- [ ] transit-line-delete can throw "Already in the same thread" part-way (line 157, the last metro line): stops released, line left as "Metro Line 0" with 0 stops; a retry released it. Fix: run TransportManager.ReleaseLine via SimulationManager.AddAction (src/TransitCommands.cs:474) like SetLineName.
- [ ] **build-network can create a segment its nodes do not list** — `src/NodeHelper.cs` `CreateSegment` (found 2026-09-26, B16). Metro Track 33915 (20571 -> 31246, last piece of TAmpa M3 S3-S4) returned ok:true with the right start/end nodes and createdNodeIds [], and `/state/networks` showed a clean chain, but both nodes behaved as End nodes: `middle` of neighbours 19317 and platform 21245 shifted 6.0 m along the chord (the dead-end signature), and every path across the piece failed (LineNotConnected on lines 147/162; 2-stop test lines isolated S3-S4). Bulldoze keepNodes + rebuild (new 21242) fixed it at once. Cause not found. Impact: invisible to every current check; blocks a whole line. Fix: after `NetManager.CreateSegment`, verify both nodes list the segment in `GetSegment(0..7)` and fail loudly (or repair with `AddSegment`); expose each node's segment list and flags in `/state/networks`. Workaround until then: the middle-shift scan in lessons.md (B16).
- [ ] **transit-line-create threw "Already in the same thread"** once (B16 test line S2-S3, 20:34:43): the bridge rolled the line back cleanly and the retry succeeded. Same family as the transit-line-delete item above; fix together via `SimulationManager.AddAction` (`src/TransitCommands.cs`).
- [ ] **TAmpa metro tunnels have steep pieces at platform joins** — terrain-following nodes at terrain - 12 meet platforms up to 8 m off that line: 43.1% (S2-S3 into S3 platform 33116), 41.5% (S4-S5 into S5 17768), 37.6% (S3-S4 out of S3), 27.4% (C3 into TM 20266), 28.5% (S5-S6). All six metro lines path and run (16/16 vehicles), so no functional impact measured; the in-game tool would likely refuse such slopes. Fix if wanted: rebuild those legs with per-point elevations solved from the platform y (as rail B12). Also `/prefabs/networks` does not expose `m_maxSlope`, so the limit is unknown.
- [x] **b16_build.py under-reported tunnel grades** — `tmp/tampa/metro/b16_build.py` computed grades from dry-run y, which is terrain - 12 even where the piece snaps to a platform node (reported 18% max for S3-S4; live 37.6%). Fixed in the working tree (records `gradesLive` from live node y); not committed, per the B16 brief.
- [x] Building bulldoze left buildings stuck Created|Deleted (24712, 1621, 5177) and caused a 227-house stack: fixed in src/BulldozeCommands.cs (simulation-thread release via SimulationJob, stuck-Deleted recovery) and src/ApiServer.cs (bulldoze awaits the job). Segment bulldoze still uses the main-thread fallback in GameThreadHelpers.ReleaseSegment (open).
- [ ] **TAmpa: Laurel Blvd buses 13 and 203 lost 21-24% while the S/SE station access roads are jammed** (B18, 2026-09-26). /state/traffic at 2036-03-03: S Station Access 28066 density 100, SE Station Access 2742 95, Laurel Blvd 31824 98 (a line 203 stop), 26028 80. Riders 13 221 -> 174, 203 266 -> 203 between 2035-11-22 and 2036-04-01; the fall began 2036-02-07 with no route change to either line in those weeks. A +50% budget for 3 weeks did not help. Unverified cause: station car traffic on the dead-end access roads. Fix to test: a second exit for the S/SE access roads or a bus-only lane on Laurel (road work, needs the player's OK).
- [ ] **TAmpa: line 60 is under the player's 75-rider floor after three redesigns** (B18). Runs as a 7-stop NO metro -> west industry feeder (2,483 m): 37/wk, periods 15-66. The version with Core2 and Richardson stops carried 36-158 per period but pulled Metro Blue from about 500 to 211-326 per period. Decide: delete it, or accept it as a coverage line (47 industrial buildings had no stop within 400 m).
- [ ] **TAmpa: Bay Ferry 224 carries 0 riders** (B18). 0 at every read over 14 game weeks, 3 ships, line budget 100, the harbors 323 m apart. Recommend line budget at its minimum or deleting the line; the player decides.
- [ ] **TAmpa: Metro Red and Green are nearly empty beside overloaded buses** (B18). Red 7/227 44-49/wk and Green 147/162 17-35/wk, while Laurel North 4 (277/wk, 2,598 waiting) and Laurel South 13 (1,756 waiting) run the same corridors, with stations 25-165 m from their stops. Not explained; tunnel grades up to 43% (see the metro grades item) and station placement are candidates. Measure metro vehicle speed or run a test line before changing anything.
- [ ] **TAmpa: line 142 stop 15 reports segmentId 0** (node 25553 at (1209,2414), next to Airport 29539) in /state/transit while the line shows no problem. Check whether it is a stop on an airport-owned road, and whether the bridge reports such stops wrongly.
