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
- [ ] **Chat panel is unproven in-game** — `src/ChatPanel.cs`: hotkey swallowing while typing, Enter
  keeping focus, Esc not opening the pause menu, Ctrl/Cmd+Shift+C toggle, sprite names, layout.
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
