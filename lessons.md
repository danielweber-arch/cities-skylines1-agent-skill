# Lessons

## Proven Rules
(Confirmed twice or more. Follow these first.)

- **Metro/rail track: every node turn <= 40 deg, platform ends straight.** The pathfinder refuses a lane across a node turn of about 45.8 deg (m_maxTurnAngle 45, XZ only). Build legs as Dubins arcs R >= 50 m in 25-60 m pieces with 30 m straight in line with each platform, and measure with tmp/tampa/metro/bends.py before creating lines. Confirmed: B15 M1 (max 33.9, 4/4 each way), B16 M2 (max 30.4) and M3 (max 31.3), 2/2 each way.
- **One metro line per direction, each station once.** Two stops at one Metro Entrance resolve to the same point and never path. Copy lines 7/227: one line A -> Z and one Z -> A. Confirmed: B15 (7, 227), B16 (135/207, 147/162).
- **A bus stop pair that duplicates a metro hop takes the metro's riders.** Never give a bus stops within walking distance of two stations of the same metro line. Confirmed: B18 (line 60 beside Blue NO-TM/C2: Blue 500 -> 211-326, recovered when the stops went), B27 (142 terminal-front stop + its stop 0 95 m from TM duplicated Blue AP -> TM: Blue 135+207 mean 141 with it, 243 after removing it; A/B/A).

- **Route bus stop orders on the road graph with U-turns forbidden before any dry run.** Vanilla lets a bus reverse only at End/OneWayOut nodes (PathFind.cs:726-755). Use tmp/tampa/p1t/route3.py (upper bound, +3-7%) with b27/route2.py (lower bound); a big gap between them on one leg means a turnaround. Confirmed: B27 (a U-turn-allowing router missed a turnaround leg by 1.3 km), P1T (route2 under-read line 9 by 1.8 km and line 4 by 1.3 km; route3 predicted the 4N/4S turnarounds, game within 7%).
- **When a metro line fails, bisect with 2-stop test lines per leg, then rebuild the bad piece in place (keepNodes).** Confirmed: B16 (33915 on S3-S4) and W1 (34851 on W1-C -> W1-W; its flags read "Created, End"). The middle-shift signature is not reliable: 34851 showed 1.44 m, not 6.0 m. Delete the test lines and diff line ids afterwards. With no middle station to bisect, rebuild the whole leg in place and create a new line: Portville L2 (2026-09-27, 42 pieces, LineNotConnected -> Complete 1/1).

- **Re-read the live network around the build box right before the first command, by prefab as well as id.** Another hand changes it between survey and build. Confirmed: 2026-09-25 operator lesson, Portville P1 (highway joined, 78 segments), Portville IC (highway became Large Oneway avenues under the same node ids).
- **Host a Train Station on a straight mainline: bulldoze the host segments, place the station collinear, link both platform ends to the old nodes.** `Train Station` (16x6): the platform is one 144 m `Train Station Track` centred 7.8 m behind the pivot (front = (-sin a, cos a)), so pivot = chord mid + 7.8 x front and a = chord heading (or +180 to face the other side). Dry-run with validate first: only the host segments may collide. Links reuse both nodes (createdNodeIds []), bends stay at the old node turns. Confirmed: TAmpa B11/B12 (M2, M, S, SE), Portville Central 41442 (platform 0.2 m off the chord, bends 0.8/4.4) and Riverside East 39763 (0.4/1.7), Portville North 518 (2026-09-27 P3 NE: hosts 19053+3581, links 23.9 m with createdNodeIds [], bends <= 0.5).

- **Never `connect` toService Road from a Roadside building: place it with the front edge 1-3 m off the road instead, and bulldoze any stub connect builds.** connect starts a road at `from` (the building centre) and runs it through the footprint. Pipes/power from the centre are harmless. Confirmed: P2b Bus Depot 18067 (stub 20827), pollution fix 2026-09-27 (stubs 14310, 17301; both plants Active without them).

- **A Water facility connects only through its own pipe node (`m_netNode`): probe it, pipe to it, never release it.** Probe with `connect` dryRun (toService Water, roadPrefab Water Pipe, maxDistance 45) from the placed position before any other pipe is near; the target must be a node on no existing pipe. Always pass `roadPrefab` to connect. Confirmed: Portville 3a (37774/4439, after a re-place) and 3b finish (WTP 42798 -> 28511, 27069 -> 18061, both Active on the first try).

- **Never loop over coordinates or JSON rows in zsh.** zsh does not word-split `set -- $var` or `$(...)`, so bodies go out broken and results read as 0. Use Python or bash. Confirmed: TAmpa 2026-09-26 (budgets not applied), Portville P2 2026-09-27 (set-zone matched 0 blocks), 3b finish (JSON args split wrong; caught).

## Log
(Newest at the bottom. Format: Situation / Action / Result / Rule)

### 2026-09-25 P1 spine attached to the highway
- **Situation:** fresh map; survey called the interchange stubs at (1008,1952)/(1006,1992) dead ends because they carried RoadNotConnected. A node-by-node trace of /state/networks showed the carriageways continue to (1504,1958)/(1503,1998).
- **Action:** built two Medium Road merges onto the true end nodes and a seven-piece 80 m spine east along z=1978, dryRun before every real call.
- **Result:** both merges snapped onto the highway nodes (31748, 28996), road anomalies 0, one local component, and the two pre-existing RoadNotConnected problems disappeared (problems total 0). Spine follows terrain down 8.4 m over 560 m.
- **Rule:** a RoadNotConnected node is not necessarily a dead end; trace segments by node id before choosing an attachment point. Build long roads as 80 m pieces so later grids snap onto real nodes.

### 2026-09-25 P2 grid snapped onto the spine
- **Situation:** D1 needed a 5x4 Basic Road grid whose middle row is the existing Medium Road spine.
- **Action:** one build-grid call, origin (1720,1818), spacing 80, snapDistance 8, opId set, dryRun first.
- **Result:** 24 nodes created, 6 spine nodes reused, 44 segments built, 5 skipped as duplicates, 0 grid anomalies, 264 zone blocks. Grid roads were auto-named by the game (Sterling Street), so filtering by "Agent" names misses them; use the returned ids.
- **Rule:** build the arterial as 80 m pieces first, then let build-grid reuse its nodes. Track created entities by id, never by name.

### 2026-09-25 the operator plays at the same time
- **Situation:** between the survey and P2 verification, roads, zones, 28 growables and a Water Outlet appeared that no worker built, and the sim was unpaused.
- **Action:** re-read state before every phase and classified everything not in createdEntities as operator-built.
- **Result:** avoided duplicating the outlet; found water at (3405,2284) from the outlet's y=67.
- **Rule:** never trust the survey as current; diff live state against progress.md before each phase, and treat unknown entities as the operator's.

### 2026-09-26 knowledge.md transit claims checked against the game code
- **Situation:** knowledge.md says "transit is weighted as preferred over private cars" and "fewer transfers win". The transit research decompiled the installed Assembly-CSharp.dll to check.
- **Action:** read TransportLineAI.GetCostMultiplier and PathFind.cs directly.
- **Result:** route cost for transit has no general preference term. The only transit multiplier is 0.75x under Free Public Transport (or the Come One Come All event) and 1/0.75x under High Ticket Prices. A walking stretch longer than 1,000 m is rejected by pathfinding (PathFind.cs lines 860, 1058, 1211). No transfer or waiting-time penalty exists in vanilla route choice; transfers cost only through extra walking.
- **Rule:** SUPERSEDES knowledge.md "Transit is weighted as preferred" and "fewer transfers win" (kept there, not deleted). Transit wins because most cims have no car and because it covers trips beyond 1 km of walking. Maximise coverage within walking range and trunk speed; use Free Public Transport as the only direct preference lever. Details in transit.md section 0.

### 2026-09-26 TAmpa batch 2: bus lines via the bridge
- **Situation:** creating new bus lines in the user's city through transit-line-create.
- **Action:** dry run each line, compare snapped segment ids with the design, then create.
- **Result:** three lines (33, 8, 12 stops) came back complete with no problems, snaps within 3.4 m. The fourth dry run had one stop snap 103 m onto "segment 0" (station mode); the script created it anyway, it showed LineNotConnected, and after deleting it the user's two 1-stop lines in the nearby bus terminal were gone too. Naming and colouring a line threw "Already in the same thread" until queued via SimulationManager.AddAction.
- **Rule:** treat any dry-run stop with snapDistance above the road snap limit, or segmentId 0, as a hard failure: never create. Do not create or delete lines near another line's stops without re-reading that line afterwards. Create without name/colour on an old DLL; set them after a restart.

### 2026-09-26 TAmpa session: what to do differently
- **Situation:** a long live session with the player typing in-game while Claude changed transit.
- **Action / Result:** (1) posted "budgets changed" before the calls returned; they had failed (zsh did not split `set -- $pair`, so the bodies had no budget). (2) A 103 m station-mode snap was created anyway and coincided with the loss of two user lines. (3) Created a new save per batch (TAmpa-transit-before, TAmpa-transit-B7); the user wants one save, overwritten. (4) The chat panel sat partly off-screen on a 16:10 Mac because it was positioned against the 1920-unit reference canvas.
- **Rule:** confirm, then announce. Run shell loops under bash. Treat bad snaps as hard failures and diff line ids around every create/delete. Keep one working save and overwrite it. Size and clamp UI to the real visible width (fixedHeight x screen aspect), not fixedWidth.


### 2026-09-26 — save-city.sh "succeeded" without saving (overwrite case)
- **Situation:** Overwriting the existing save `TAmpa b2`.
- **Action:** Ran `./scripts/save-city.sh --name "TAmpa b2"` and announced the save in the in-game chat.
- **Result:** The script printed the old file (LastWriteTime 16:58:24) and exited 0: it only checked that the file *existed*, which is always true on an overwrite. The real save only landed on the next call (17:19:49). I had announced it before checking, and had to correct the chat.
- **Rule:** A save is verified only when the file's mtime is later than the moment the save was requested. `save-city.sh` now enforces this and waits for a stable size. Still check the LastWriteTime yourself before announcing.

### 2026-09-26 — the working save is the one the player loaded
- **Situation:** The player saved `TAmpa b2` themselves and loaded it; the "one save" rule named `TAmpa-transit-B7`.
- **Rule:** The one working save is whatever the player currently has loaded (check the newest `.crp` mtime vs the load time). If the player switches saves, the working save switches with them. Never overwrite an older name the player has moved on from.

### 2026-09-26 — transport lines do not re-path after track is fixed
- **Situation:** Train Line 69 had LineNotConnected; the missing track was then built.
- **Action:** Waited ~1 game day for it to clear.
- **Result:** Still LineNotConnected with 0/2 trains. Deleted and recreated the same line: 2/2 trains, no problems within ~1 game day.
- **Rule:** After fixing the network under a broken line, recreate (or edit a stop of) the line to force path recalculation; don't wait for it.

### 2026-09-26 — build-network will not join a shared road/rail node
- **Situation:** An access road was planned to end on Richardson Avenue's end node 30970, which the rail chain also uses.
- **Result:** The bridge created a duplicate node 25531 on top of it (createdNodeIds non-empty) and the road was a dead end.
- **Rule:** Any createdNodeIds on an endpoint you meant to join is a failure; check it every call. Pick a road-only node (list nodes by segment prefab) for the join.

### 2026-09-26 — the agent CAN restart the game itself (Proven once)
- **Situation:** New DLL needed a restart; player AFK and said "I can't restart game. Unless you can- we're stuck".
- **Action:** Saved the working save (newest .crp), SIGTERM'd `Cities`, relaunched `Cities.app/Contents/MacOS/Cities --continuelastsave` from the install folder with `SteamAppId=255710 SteamGameId=255710` (the Paradox launcher's Resume uses exactly `./Cities --continuelastsave`).
- **Result:** No launcher, city loaded in ~80 s straight into the save; new DLL live.
- **Rule:** Use `scripts/restart-game.sh` after saving. The save must be the newest file in Saves/ — `--continuelastsave` loads the most recent one.

### 2026-09-26 — building Metro Track threw "Already in the same thread"
- **Situation:** First real Metro Track piece through build-network (to pass the "Metro Track Created" milestone that gates metro lines).
- **Result:** PlayerNetAI.CreateSegment ends with m_createPassMilestone.Unlock() → UnlockManager.CheckMilestone → ThreadHelper.dispatcher, which throws on the main thread. The segment was left half-built (end node 0 after rollback) and had to be bulldozed.
- **Rule:** Any game call that can fire UI/unlock events must not run on the main thread. NodeHelper.CreateSegment now detaches the milestone and replays Unlock() via SimulationManager.AddAction; metro unlocked 30 s later. After any failed network call, list the segments in the area and remove orphans (endNode 0).

### 2026-09-26 — per-point elevation works; solve heights from dry-run terrain, not estimates
- **Situation:** Rail routes 2 and 3 (61 pieces, 46 elevated) on the new DLL. The plan's terrain was interpolated from samples up to 100 m apart.
- **Action:** Dry-ran every piece at elevation 0 (startY/endY are terrain + elevation; they ignore snapping) plus 1/4, 1/2, 3/4 points, re-scanned obstacles live, then solved node heights as the max of (lower bound - grade x distance), fixed at the existing node and the platform node read after placing the station.
- **Result:** Survey differed from the plan's estimates by up to 4.9 m (branch 23 end 170.4 vs 172.8+). Every built node matched the solve within 0.03 m. Rounding elevations to 0.5 m pushed 45 m pieces over the 8% cap (8.07-8.4%); rounding to 0.01 m fixed it. The live obstacle scan found items the plan lacked (two wind turbines, a clinic, a gravel path) and dropped ids that had churned.
- **Rule:** Place the station first and read its platform node y. Survey, then solve. Round elevations finely on short pieces, and re-check each grade after rounding. Flag clearance at the node(s) nearest the obstacle, not at both ends of every piece that touches it.

### 2026-09-26 — bulldoze can throw a transient IndexOutOfRange
- **Situation:** Upgrading one-way spur segment 23658 in place (bulldoze keepNodes, rebuild).
- **Result:** The bulldoze returned HTTP 500 IndexOutOfRangeException, and the segment was left untouched (no orphans). The same call 20 s later succeeded.
- **Rule:** After a failed bulldoze, re-read the segment and the area, then retry once. Stop if it fails twice.

### 2026-09-26 — a new station can open with a Water problem and shut down
- **Situation:** Core2 36478 was placed about 80 m from the nearest water pipe. The plan only foresaw an Electricity fix.
- **Result:** Within one game day it showed "Water, MajorProblem" and lost its Active flag. Its line (78) still ran 2/2 trains with no line problems. S, SE and SW (all near pipes) were fine.
- **Rule:** Before placing a station, check the distance to both water and power networks, and plan the pipe along with the power line.

### 2026-09-26 — keep a spur node alive: build the new link before bulldozing its last segment
- **Situation:** Node 19499 was left with one segment (19900) after the S host segments were removed, and the plan bulldozed 19900 next.
- **Action:** Built S link S (6096 -> 19499) first, then did the 19900 bulldoze and rebuild.
- **Result:** 19499 was reused, with createdNodeIds [] on every call. Level crossing 25231 also survived both gates, because one rail segment always stayed on it.
- **Rule:** Before a keepNodes bulldoze, make sure every kept node still has another segment. Build the new connection first if it would not.

### 2026-09-26 — underground Metro Entrance has ONE stop point; "different platforms" cannot be checked
- **Situation:** M1 out-and-back metro line dry run. The gate required each station visited twice to snap to two platforms more than 4 m apart.
- **Action:** Dry-ran transit-line-create twice: first with the plan's points (5 m either side of the track), then with points 10 m either side of it and 36 m along it.
- **Result:** Every stop went via the station, to the intended building, but both visits of every station resolved to the same point (the Metro Station Track midpoint, 0.0 m apart). The bridge's port of TransportTool gets it from CalculateSpawnPosition with 12 seeds, and all 12 return the same point for a Metro Entrance. The in-game line tool uses the same code, so a player-drawn out-and-back line has the same stop points.
- **Rule:** For underground Metro Entrance stations, the dry run cannot show platform separation. Out-and-back lines rely on fixedPlatform=false and pathfinding. Check them by creating the line and reading LineNotConnected and the vehicle count, not with a >4 m dry-run check. Decide this before gating a metro build on it.

### 2026-09-26 — plan station lots churn in two ways: new growables and level-ups
- **Situation:** Checking metro-plan lots about 1.5 h after planning.
- **Result:** One lot had a new growable on it (5105 at the M2 Central-west site). Two listed demolitions kept their id and position but changed prefab by levelling up (146: H1 -> H3; 1494: H1 -> H2). One (43746) was gone. place-building dryRun, even with validate:true, returned only "validation passed" for Metro Entrance (no toolErrors, no CheckSpace), so it caught none of this.
- **Rule:** Re-run the footprint check against live buildings and networks just before each placement. A listed id with a different prefab at the same position and footprint is a level-up, not a different building. Compare it with an older snapshot before deciding.

### 2026-09-26 — Metro tunnels: dry-run y is the built y; resample plan polylines to the piece rule
- **Situation:** 106 Metro Track pieces at elevation -12.
- **Result:** Built node y matched dry-run terrain-12 exactly. Station platforms sit at building y - 12.2. Every platform end matched the 1005-derived geometry (centre 12 m behind the lot, +/-72 m along it) within 0.2 m. Arc-length resampling of a polyline with a sharp corner gives chords shorter than the step (31 m at M1.23). A cubic Bezier between the platform-end tangents cut the worst node turn from 66-71 deg to 51-63 deg.
- **Rule:** Check the chord length after resampling, not the step length. Prefer a tangent-matched Bezier where a plan leg leaves a station at a sharp angle.

### 2026-09-26 — metro (and rail) lanes do not connect across a node turn of about 46 deg
- **Situation:** M1 had 10 stations joined by tunnels. Two-stop lines worked, but any line that passed through a station got LineNotConnected. Nine nodes turned 41-67 deg.
- **Action:** Read `m_maxTurnAngle` from the NetInfo MonoBehaviours in sharedassets11.assets (UnityPy raw data; the float sequence halfWidth, pavementWidth, segmentLength, minHeight, maxHeight, maxSlope, maxBuildAngle, maxTurnAngle). Read PathFind.ProcessItemCosts. Replaced the four legs with sharp nodes by Dubins arcs (R 50-55, 25-30 m pieces, first piece 30 m straight off the platform), built new before bulldozing old.
- **Result:** Metro Track, Metro Station Track, Train Track and Train Track Elevated all read 45 deg. Pathfinding refuses a non-car vehicle lane when dot(dirA, dirB) >= 0.01 - cos(min of the two infos' angle), so the limit is a turn of about 45.8 deg. Directions are XZ only, so grade has no effect. After the fix the max bend was 33.9 deg; 10-stop lines in both directions (7 and 227) ran 4/4 trains with no problems within 30 s at speed 3.
- **Rule:** Keep every Metro/Train track node at 40 deg or less, including the platform-end junction. Leave a platform with one straight piece in line with it, then curve. A 120 deg turn needs roughly R 50 m and 5 nodes at 30 deg. Check bends with the same method (angle between the two segments at each 2-segment node) before creating any line.

### 2026-09-26 build-network can leave a segment unlinked on its nodes (B16)
- **Situation:** M3 lines 147/162 showed LineNotConnected on 5 of 6 stops each while every bend was <= 31.3 deg and /state/networks showed a clean chain S1 -> S6 (segment start/end node ids all correct, 0 orphans).
- **Action:** Bisected with temporary 2-stop lines per leg plus one spanning line (S2-S4, running through S3 without stopping). S1-S2, S2-S3, S4-S5, S5-S6 pathed both ways; S3-S4 and S2-S4 failed. Then compared each segment's `middle` with its chord midpoint: the shift is purely along the chord and reveals the node class (about +-5.2 m where a Bend meets a Middle node, 6.0 m where one end is an End node, as on every terminus platform). Piece 19317 and S4's through platform 21245 showed the 6.0 m End signature although both their nodes have two segments by segment-side data; piece 33915 between them showed none.
- **Result:** Nodes 20571 and 31246 behaved as dead ends, i.e. they did not list segment 33915 (the last piece of S3-S4, built normally, ok:true, createdNodeIds []). Bulldozing 33915 with keepNodes and rebuilding the same piece (new 21242) removed both 6.0 m shifts, and both lines went to 2/2 vehicles with no problems within 60 s. Grade was ruled out on the way: legs with 41.5% and 43.1% pieces path fine. Cause of the bad link not found.
- **Rule:** After building any leg, scan its segments and the platforms it joins for the End signature (|middle shift| about 6 m on a segment whose nodes both have 2 segments). Rebuild such a piece in place (keepNodes). When a line fails, bisect with 2-stop test lines per leg and one line that runs through a station without stopping; delete them afterwards.

### 2026-09-26 station order and lot position decide the tunnel length (B16)
- **Situation:** M2's planned Central-west lot was taken (growable 5105); the free lot on Core2's road 20549 sat 270 m east of the Trash Mall station and 780 m of U-turn away from north offices.
- **Action:** Planned every leg with a two-ended Dubins search (tmp/tampa/metro/b16plan.py: R 50-300, 30 m straight at both platform ends, clearance >= 14 m from all tunnel track and the other planned legs), for two station orders and for six lot positions along 20549.
- **Result:** Keeping the plan's order needed a 796 m north-offices -> Core2 loop that crossed another leg; the order NO -> AP -> 1005 -> TM -> C2 -> FN needed 2,025 m in total. Along road 20549, a lot 38 m further east turned the TM -> C2 link from a 448 m loop into a 169 m S-curve (for a 30 m sideways offset with both 30 m straights kept, 125 m between platform ends forced a loop and 147 m was enough for a 152 m S-curve). Every platform end matched the 1005-derived geometry to 0.0 m.
- **Rule:** Before placing stations, plan all legs for each candidate order and for several lots along the same frontage; pick by total length and max bend. Parallel platforms offset about 30 m sideways need roughly 150 m between their ends, or the leg becomes a loop.

### 2026-09-26 terrain-following tunnels: steep pieces are real but harmless so far (B16)
- **Situation:** New tunnel nodes sit at terrain - 12 (SampleRawHeightSmoothWithWater), but station platforms sit wherever the building puts them, up to 8 m off that line.
- **Result:** The first or last piece at a platform reached 37.6-43.1% (the dry-run check reported 18% because it used terrain - 12 for the platform node; b16_build.py now also records gradesLive from live node y). All six metro lines path and run at those grades.
- **Rule:** Report grades from live node y, never from dry-run y at a join. Grade does not block metro pathing; smoothing is cosmetic unless the player wants in-game-tool-legal slopes.

### 2026-09-26 level-ups can shrink a footprint; re-run the collision check to see which demolitions are still needed (B16)
- **Situation:** Plan demolitions 41777 (H1 3x3) and 1494 (H1 3x3) were H3 3x2 at the same id, position (to 1 mm) and angle.
- **Result:** Treated as in-place level-ups of the planned buildings. Running the lot check without exclusions showed 146 no longer overlapped the planned S1 lot while 41777 did; moving S1 20 m along its road swapped 41777 for 146 and raised clearance from M1's tunnel 36766 from 12.5 m to 25.3 m.
- **Rule:** Before demolishing, run the footprint check with no exclusions and demolish only what still overlaps. Also check the planned platform line against existing tunnels: place-building's guard ignores tunnels.


### 2026-09-26 — bulldozing buildings on the main thread left them stuck, and the game stacked 227 houses on one lot
- **Situation:** /commands/bulldoze called BuildingManager.ReleaseBuilding on the main thread.
- **Result:** ReleaseBuildingImplementation sets Deleted first, then BuildingAI.ReleaseBuilding dispatches a UI event → "Already in the same thread" → everything after it (units, paths, vehicles, grid, ReleaseItem) never ran. The implementation returns at once for Deleted buildings, so the fallback could not finish it. Stuck 24712 made the game keep spawning replacement houses on its lot (227 stacked, 117 Pollution + 34 Death + 38 Abandoned entries from one lot).
- **Fix:** bulldoze of buildings now runs on the simulation thread through SimulationJob and awaits the result (flagsBefore/flagsAfter/released); stuck-Deleted buildings are recovered by clearing Deleted and running the full release. 24712, 1621, 5177 all released (flagsAfter None).
- **Rule:** Every game mutation that can fire events runs on the simulation thread (SimulationJob/AddAction). A problem count that jumps by 100+ at one coordinate is a stuck object, not a city-wide issue.

### 2026-09-26 B18 new lines need the period count, not the weekly average, at 3 weeks
- **Situation:** measuring two new feeder lines 3 game weeks after creation.
- **Action:** logged m_averageCount, m_finalCount (lastPeriod) and m_tempCount every 30 s (tmp/tampa/b18/samples.jsonl).
- **Result:** each line's period rolls about every 7-8 game days, staggered by line. After 3 weeks line 11 showed an average of 5-16 while its periods were 49, 64, 96; the average reached 80 only after about 12 weeks. Rail line periods swing 49-485 week to week with no change made (90: 485, 250, 291, 183, 70).
- **Rule:** judge a new or edited line by its last 3-4 periods, not by m_averageCount, and compare against the spread of periods before the change. A mode total that moves less than its pre-change swing is not a result.

### 2026-09-26 B18 a bus line that duplicates a metro pair takes metro riders
- **Situation:** line 60 redesigned to run Core2/Richardson -> NO metro -> west industry, parallel to Metro Blue between TM/C2 and NO.
- **Result:** 60 carried 36-158 per period while Blue (135+207) fell from about 500 to 211-326 per period. Removing the two Blue-parallel stops brought Blue back to 350-456 within 4-7 weeks, and 60 fell to 15-66. Adding back only the Core2 stop made 60 a 4.7 km loop and lowered it further.
- **Rule:** a feeder must start at the trunk station, not run beside the trunk. A feeder into an area within 1 km walk of the station (NO -> industry is about 650 m) gets few riders, because walking wins. Check the walk from the station to the target before building a feeder.

### 2026-09-26 B18 more buses on a line with 1,000+ waiting did not add riders
- **Situation:** lines 4, 13, 142, 203 had 1,100-2,600 waiting.
- **Action:** line budgets 100 -> 150 (+40-50% buses) for 3 game weeks.
- **Result:** riders 263 -> 263, 246 -> 230, 188 -> 191, 277 -> 262; waiting on 13 fell 1,398 -> 674. Reverted. The corridor of 13 and 203 (Laurel Blvd) had segments at density 95-100, including the S and SE station access roads.
- **Rule:** waiting passengers alone do not mean too few buses. Read /state/traffic on the line's corridor first; a jammed line needs a road fix, not more buses.

### 2026-09-26 B18 a ferry between two harbors 323 m apart carries nobody
- **Situation:** Bay Ferry 224 between Harbor 23322 and Harbor 42184, plus a bus stop moved to 42184's front (line 203 on Cook St 10561).
- **Result:** 0 riders at every read over 14 game weeks; the Cook St stop had 0 waiting at every read.
- **Rule:** a ferry only gets riders where the water route is much shorter than any land route and both ends are near homes or jobs. Check the walking distance between the two docks before building a ferry line.

### 2026-09-26 B20 a density of 100 on a short stub is only about 3 cars
- **Situation:** the S and SE station access stubs (18 m and 17 m, 2 lanes) read 99-100 in every sample and were suspected of funnelling Laurel Blvd traffic.
- **Action:** read RoadBaseAI.SimulationStep and CarAI.SimulationStep (decompiled): density = trafficBuffer*100/(vehicle-lane length*16), and each car adds about 14 per 16-frame step.
- **Result:** 100 is reached with about (lane length/14) cars present on average. That is about 3 cars on the stub and about 24 on a 56 m 6-lane Laurel segment. The real hot spot was the junction approach 26028 (88, rising from 30-57 further out).
- **Rule:** normalise density by lane length before calling a segment jammed. Ignore short stubs. Find where the density ramps up along the approaches: that is where the queue starts.

### 2026-09-26 B20 rebuilding a road demolishes the buildings that front it
- **Situation:** planned to upgrade hot segments (Merge B 752, Stephen 8278) with bulldoze keepNodes + rebuild.
- **Action:** read NetManager.ReleaseSegment and PrivateBuildingAI; found the fronting buildings by comparing building angle (+90 deg = facing) with the nearest road.
- **Result:** releasing a segment releases its four zone blocks. The new segment's blocks start unzoned, and a building whose cells fail CheckZoning is flagged Demolishing. 752 has 2 fronting shops; 8278 has 2 fronting houses. Every hot segment in TAmpa had fronting buildings, a bus line or a station within 60 m, so no change was made.
- **Rule:** before any bulldoze + rebuild, list the growables that front the segment (tmp/tampa/b20/feas.py). If any do, the change touches zoning. Use the in-game Upgrade tool (the player), or wait for a zone-safe upgrade command in the bridge.

### 2026-09-26 B20 a line that "lost riders" recovered without a change
- **Situation:** B18 logged line 13 falling 221 -> 174 and blamed jammed Laurel roads.
- **Result:** with no change, line 13's weekly periods over 7 weeks were 269, 193, 338, 235, 253, 363, 247 (m_averageCount 263). Line 94's were 521-673 and line 4's 221-397.
- **Rule:** weekly bus periods swing about +-30% with no change. Do not blame a road for a rider drop, or credit a road fix for a gain, unless the change is larger than that spread over at least 4 periods.

### 2026-09-26 — why metro Red/Green carry few riders (read-only diagnosis, tmp/tampa/metro-diagnosis.md)
- **Situation:** Red ~45/wk per line, Green 17-35, while nearby buses had 1,700-2,600 waiting.
- **Result:** Ruled out: train speed (speed code has a curve term but no grade term; pathfinder ignores vehicle speed/waiting/frequency) and station access (all 16 entrances active, 19-34 m from roads). Confirmed: Green is fully duplicated by bus 13 (all 15 station pairs) and rail 78 (67-127 m from S1/S2/S4); Red's 4 northern stations have no jobs within 500 m and Red never reaches the Core2 hub. Every metro/rail trip pays a platform walking penalty (walkway lanes charged 5-10x walking) — inferred, not observed.
- **Rule:** Place metro where it links homes to JOBS the bus/rail network does not already serve; a line that parallels an existing bus or rail line within ~150 m gets nothing. Check job counts within 500 m of each station before building.

### 2026-09-26 B19 airplane passengers are set by tourism and path success, not by the airport
- **Situation:** the player asked to "get the airport to max capacity". One vanilla Airport (29539), 4 Airplane Connections, 54-86 airplane passengers/week.
- **Action:** decompiled TransportStationAI, PassengerPlaneAI, OutsideConnectionAI, HumanAI (ilspycmd, Assembly-CSharp.dll) and sampled the airport for 9 game months.
- **Result:** tourist offers are made per outside connection: city size x tourism resource x the connection prefab's touristFactor x budget (capped at 125% production for budgets >= 150) x the connection's path-success ratio. A plane leaves for a gate stop when a waiting passenger there becomes BoredOfWaiting, and only if no other plane of that airport is heading to that stop within 3 km; there is no plane cap (maxVehicleCount 100000). Measured 2-4 planes out at any time. After linking every hub to the airport (one month, 21 samples) airplane passengers stayed at 59-77/wk.
- **Rule:** do not build a second airport for volume: it adds gate stops, not tourist offers. The levers are tourism attractiveness, the plane budget up to 150, and transit from the airport (it raises the path-success ratio). Expect any effect to be slow and within the weekly swing.

### 2026-09-26 B19 a free platform end can be boxed in by another line's tunnel
- **Situation:** planned FN -> S1 to join Metro Blue and Green. S1's only free end (NW) had been placed 25 m from Red's CO -> NR hook in B16.
- **Action:** intersected S1's axis with every metro segment before planning.
- **Result:** Red 36766 sits 35.6 m out along the axis, so the 30 m lead ends 4.6 m from Red. The only rule-compliant way in was a 4-way crossing node X placed on both S1's axis and 36766's chord: Red and the express each go straight (0 deg), the cross turns are 53.7/126.3 deg, above the 45.8 deg lane limit, so trains cannot switch lines at X. Built with the sim paused (bulldoze keepNodes, two Red halves, then the arm); Red never showed a problem and kept 8/8 and 9/9 trains without being recreated, but both Red paths grew by 118 m (not explained).
- **Rule:** when placing a station, check that each FREE platform end has 30 m of lead plus a turning corridor clear of other tunnels, not just that the lot is 12 m clear. If a crossing is unavoidable, put the node exactly on both chords (0 deg for both lines) with a crossing angle well above 46 deg, and build it in two stages (split plus a short arm first, watch the old line for a game day, then the long tunnel).

### 2026-09-26 B19 metro lines reverse at a stop; a dead end is not needed
- **Situation:** Blue turned back at FN and Green at S1, both at dead-end platform ends. The FN -> S1 link turned both ends into through nodes.
- **Action:** read TransportLineAI.StartPathFind, then compared line lengths before and after.
- **Result:** a non-fixed stop (metro and train stops: fixedPlatform false) gets two start and two end lane positions (pathPosA/B), so the path may leave a stop in the opposite direction. Blue 135/207 and Green 147/162 became 6 m shorter (5213.2 -> 5207.2) with no detour. Green showed LineNotConnected for about 1 game day and then cleared on its own.
- **Rule:** joining a terminus's dead end to new track does not break out-and-back or one-direction lines; check that the line length does not jump. A LineNotConnected that appears when the line's own track is edited can clear by itself; one caused by missing track elsewhere does not (see "transport lines do not re-path").

### 2026-09-26 B19 when the shortest Dubins path cuts another line, search via points
- **Situation:** FN -> X had no candidate: every two-ended Dubins path (R 50-300) crossed Red's CP-CO curve.
- **Action:** chained Dubins legs through one or two via points (x, z, heading), ranked by analytic length (dubins._words, no simulation), rejected on raw-curve clearance before resampling, then resampled with the fewest pieces that keep every bend <= 32 deg.
- **Result:** the first full search simulated every word and did not finish in 10 minutes; with analytic lengths and pruning it ran in 3 minutes and found 20 routes of about 1,460 m. The chosen one (via (2550,1250) and (2720,1200)) is 52 pieces, max bend 32.0 deg, clearance 17.3 m. Also: a resample that ends 1 m short of a join node makes a 1 m chord that fails every piece rule; end the curve exactly on the join node.
- **Rule:** plan around an obstacle line with via points on its far side, not with bigger radii. Prune on cheap checks before resampling.

### 2026-09-26 B22 tourism attractiveness is saturated by one wonder; uniques barely move airplanes
- **Situation:** the brief was to build unique buildings and parks near the metro stations to raise tourism and so airplane passengers (B19: airplanes are demand-bound).
- **Action:** read CheckActualTourismResource and every global Attractiveness writer in the decompiled Assembly-CSharp; read monument and connection prefab values from sharedassets11.assets (tmp/tampa/tourism/b22_mon.py, b22_oc.py); sampled 12 game weeks with no change.
- **Result:** T = floor(100*S/(S+200)), an integer, with S = global Attractiveness + average land value. The Plaza of Transference (SpaceElevatorAI) adds a fixed 1000, so S >= 1188 and T is about 85-87. A unique (att 5-20) moves T by 0 or 1 point (0-1.2% airplane offers); all 15 unbuilt ones move it 1-2 points. Regular parks add only local attractiveness. Meanwhile airplane passengers went 63 -> 104 -> 76 -> 92 per week with nothing changed. The Plaza is also a tourist gateway with touristFactor 15,000 vs 1,500 per Airplane Connection.
- **Rule:** before building for tourism, compute S and T; once a SpaceElevatorAI building is active, uniques and parks are not an airplane lever. T raises every connection alike, so it never changes the airplane share. Measure the no-change weekly range before crediting any build with an effect.

### 2026-09-27 — switching off the Plaza of Transference did not help the airport
- **Situation:** The Plaza is a tourist entry point with 15,000 tourist factor vs 6,000 for all four airplane connections; the hypothesis was that it steals airport tourists.
- **Action:** Turned it off (real SetProductionRate 0) for 8 game weeks.
- **Result:** Airplane passengers 62-103 (mean 83) vs 63-104 (mean ~81) with it on. No measurable change.
- **Rule:** Airport volume here is set by city size and connection path success, not by competing tourist entry points. Keep the Plaza on.

### 2026-09-27 — `for row in $(jq -c ...)` splits JSON rows on spaces
- **Situation:** Looping over segment rows whose prefab name contained spaces ("Large Road with Grass Median").
- **Result:** Every row was split into fragments; all calls were refused ("id is required", "Road is too short") — harmless only because the API validated.
- **Rule:** Iterate JSON rows with `jq -c '.[]' f | while IFS= read -r row; do ...; done`, never `for row in $(...)`. Save immediately after any live change — the player can close the game at any moment (bus lanes were lost this way).

### 2026-09-27 B27 a detached bus stop is invisible to the problem flags
- **Situation:** line 142 showed no problems, but its stop 15 had laneId 0 / segmentId 0.
- **Action:** compared the stop across every saved /state/transit snapshot (B18, B20, B27).
- **Result:** laneId 0, nodeFinalCounter 0 and 0 waiting in all of them since B18: the stop node was not attached to any lane, so nobody could board, and the line still counted as complete with no problem.
- **Rule:** in any transit health check, list stops with laneId 0 (bus/tram) as broken even when the line has no problem flag.

### 2026-09-27 B27 read the road topology before picking stops; forbid U-turns in the router
- **Situation:** planning an airport <-> west industry bus. The industry rectangle joins the network at one corner, and the two parallel spine roads (Empire/Thornton) join only at their east end.
- **Action:** built a road-graph router (tmp/tampa/b27/route.py, route2.py with no immediate reversal) and routed every candidate stop order before the dry run.
- **Result:** a stop on Empire westbound would have cost a 1.6 km leg; every order carried ~1 km of dead running each way. The router predicted 5,086 m and 7,208 m against 4,916 m and 6,861 m in game (+3-5%). The version that allowed U-turns under-estimated a turnaround leg by 1.3 km.
- **Rule:** before choosing stops, route the candidate order on the road graph with U-turns forbidden; drop any stop whose leg exceeds ~2x the straight-line distance. A stop's side (right-hand traffic) fixes the direction the bus must be heading there.

### 2026-09-27 B27 an airport loop into job-only areas stays under 75
- **Situation:** Bus 4 rebuilt as terminal -> offices/industry (88 growables with no stop within 400 m) -> Grand Mall -> terminal.
- **Result:** 20-65/week across four phases (v1 mean 34; redesign giving terminal -> mall a 784 m leg: 33; while 142 shared its terminal stop: 22; after that was removed: 52). Industry stops read counter 1-5; the terminal and Grand Mall stops carried most riders. Coverage near the airport 88 -> 0 uncovered.
- **Rule:** a feeder into job-only areas that are within 1 km of a metro station gets few riders even when it starts at the airport (third confirmation of the B18 feeder lesson, with a different start). Build it for coverage only if the player accepts a sub-75 line.

### 2026-09-27 B27 stage changes and verify with A/B/A; wait out the path transient before saving
- **Situation:** two changes near the airport (new Bus 4, then a 142 stop), in a city whose lines swing +-30% a week.
- **Action:** created Bus 4 alone, measured 9 periods, redesigned, measured, then the 142 edit, measured, then reverted it and measured again.
- **Result:** only the revert separated the 142 effect from noise (Blue 141 -> 243 while line 97, 1.3 km away, also swung 31% with nothing changed). After removing a stop, /state/transit showed 493 m and target 2 vehicles for ~40 s before settling at 5,334 m; a save taken in that window caught the transient.
- **Rule:** one change per measurement phase, first period after a change dropped, and use a far-away unchanged line as a noise control. After any stop edit, poll until lengthMeters and targetVehicleCount settle, then save.


### 2026-09-27 M1 — the validated dry run says canPlace:true even when it hits a building
- **Situation:** placing services with `place-building` `validate:true`.
- **Result:** a Crematory dry run at (2665,800) returned `canPlace:true`, `toolErrors:[]` and `collidingBuildingIds:[37376]` (CheckSpace runs in test mode, so the collision is reported but not raised as an error).
- **Rule:** the collision guard is `ok && canPlace && toolErrors==[] && collidingBuildingIds==[] && collidingSegmentIds==[]` (tmp/tampa/m1/m1.py `clean`). Never trust `canPlace` alone.

### 2026-09-27 M1 — the east water outage was a missing pipe, not capacity
- **Situation:** 26 buildings with Water along Roberts Avenue (x 3900-4260), 16 abandoned; the pipe network was one component.
- **Action:** 5 Water Pipe pieces along the road from the nearest pipe node, plus 7 more at three smaller gaps.
- **Result:** Water 34 -> 5 within about a game month; Abandoned 17 -> 3 as abandoned lots were replaced.
- **Rule:** before adding pumps, measure the distance from each dry building to the nearest pipe; over ~100 m it is a coverage gap. Lay pipes along the road the buildings front.

### 2026-09-27 M1 — zoning without pipes loses the first buildings
- **Situation:** harbour-south infill painted Industrial/Office on empty blocks with no pipe along their roads.
- **Result:** within ~3 game weeks 8 new buildings there had Water and 3 were Abandoned.
- **Rule:** pipe (and power) every road before painting its blocks, the same as for a new district.

### 2026-09-27 M1 — power poles make set-zone skip the block
- **Situation:** NW1 power trunk nodes were put on block centres along x=1260.
- **Result:** `set-zone` with preserveOccupied true skipped all 8 blocks around each pole (changedCells 0): the poles count as service buildings.
- **Rule:** zone before running power lines through a new grid, or keep line nodes on block edges; if poles are the only occupants of new blocks, paint those blocks with preserveOccupied false, one block at a time.

### 2026-09-27 M1 — a Power Line end 15-20 m from buildings links them
- **Situation:** 4 shops with no powered building within 160 m.
- **Action:** one 90 m Power Line, ends 20 m from powered shop 45997 and 14 m from shop 37376, clear of all footprints.
- **Result:** the shops were powered within about a game day (3 had already gone Abandoned); the same pattern powered the NW1 services.
- **Rule:** a power link does not need to touch the building; keep ends within ~15-20 m and the line clear of footprints.

### 2026-09-27 M1 — the built city has almost no room for a 6x4 school
- **Situation:** 6 more Elementary Schools for the least-covered homes.
- **Action:** probed both sides of every non-highway road segment with a validated dry run (tmp/tampa/m1/freesites.py, ~6.5 min).
- **Result:** 127 free lots city-wide (68 for Library 01), none inside the uncovered areas; the best 4 newly covered 19, 9, 9 and 6 homes. Elementary coverage 82.9% -> 85.8% with 6 schools.
- **Rule:** in built-up TAmpa, coverage gains come from new districts; place schools while a grid is still empty, and do not expect infill sites.

### 2026-09-27 M1 — a feeder into a one-access district detours to turn round
- **Situation:** NW1 has one road link (node 23921) and metro 1005 is 400 m east of it on Robert Blvd; no side street lets a bus loop back.
- **Result:** line 140 came out 4,887 m, about 1.4 km longer than the loop drawn, with no problem flag.
- **Rule:** check line length against the drawn loop after creation; a big excess means a turnaround. Give a district a second access (or a block loop at the station) before its feeder.

### 2026-09-27 WF — the bridge's list endpoints truncate at 5,000 rows without saying so
- **Situation:** road and pole guards for the waterfront districts were built on `/state/networks?limit=20000` and `/state/facilities?limit=6000`.
- **Action:** compared `returned` with `total` after a Winston Crowley stub looked like an isolated 3-node component.
- **Result:** networks returned 5,000 of 6,389 segments (1,631 of 2,082 roads) and facilities 5,000 of 5,702. The "isolated" stub was connected through segments the capped call dropped. Paging per `service=` gives complete lists; a full re-check of the 289 pieces built so far found 0 conflicts.
- **Rule:** never trust an unfiltered list call; page by service and assert `returned == total` (`tmp/tampa/wf/wf.py segs()`, `all_facilities()`).

### 2026-09-27 WF — preserveOccupied skips every block within ~40-60 m of any building
- **Situation:** staged residential zoning (half a city block per chunk, 14 game days apart) in new districts.
- **Action:** read `ZoneCommands.cs`: a block is protected if any growable, service, park, boulder or pole lies within max(w,l)*4+38 m of the block position; set-zone paints whole blocks whose position is within the radius.
- **Result:** after chunk 1 grew, the next half-block chunk was skipped (houses from the first half within 38 m). Checking the chunk's interior box against live footprints and repainting with preserveOccupied:false when the box is empty (same-zone new growth tolerated) unblocked it; pre-existing buildings are never repainted.
- **Rule:** zone a new district by city-block interiors (centre of the 80 m lattice cell, radius 34 = only that cell's 8 blocks) and fall back to an explicit footprint check, not preserveOccupied, once growth starts.

### 2026-09-27 WF — the TAmpa shore is walled off: plan the crossing before the district
- **Situation:** "activate all waterfront land" — 3.64 km^2 of free low ground in 19 pockets.
- **Action:** swept every candidate access with the footprint guard.
- **Result:** every large pocket sits behind the coastal rail (E1, NE shore) or the Layton/Stephanie Lee highway (P0), and every at-grade approach is lined with built frontage (Richardson, Dixon, Laurel, Stephen, the M2 access road). Two Medium Road Elevated crossings worked first time: over the coastal track at z 3792 (+10.9 m, the game placed no pillar over the rail, road anomalies 75 -> 75) and over the highway at x 1100 (+10.5/+11.7 m over the carriageways). E1 still needs two tenements demolished.
- **Rule:** for any new district in built TAmpa, find the frontage gap (road_guard sweep along the arterial) and the grade-separated crossing first; budget a Medium Road Elevated bridge with nodes between, not on, the crossed lanes and grades <= 10 %.

### 2026-09-27 WF — the 50-cell / 2-week residential rule and a large district
- **Situation:** NE-E (82 half-block chunks) and P0 stage 1 (139) queued under the master-plan rule (<= 50 cells, 14 game days apart per district).
- **Result:** at speed 3 one 14-day step is ~4.4 min of wall time, so the queues take ~6 h and ~10 h. Meanwhile the commercial/office painted up front outran the workers: W demand 14 -> 0, 5 new NE shops abandoned with NoWorkers within 20 min.
- **Rule:** paint CH/O in proportion to the residential that is actually open (not the district total), and agree the chunk interval with the player before zoning a district over ~20 chunks (`ZONER_GAP_DAYS` in `tmp/tampa/wf/zoner.py`).

### 2026-09-27 P1T — a recreated line ramps up for many weeks, so a split cannot be judged against the old line
- **Situation:** line 94 (554/week) was split into 193+195 at 1005; after 5 post-ramp periods the halves carried 279 combined (-50%), so the split was reverted by recreating 94 with the identical 19 stops (line 23).
- **Action:** logged every line's weekly period every 30 s (tmp/tampa/p1t/samples.jsonl) across three windows: before, split, revert.
- **Result:** line 23 climbed 17, 91, 174, 268, 191, 198, 296, the same path the split halves took. The sum over all lines stayed flat (3,421 / 3,437 / 3,462 without the other builders' new lines). While 94 was gone, 142 went 126 -> 246 and rail 180 105 -> 219.
- **Rule:** deleting a line and creating its replacement costs riders for many weeks whatever the design. Judge a split on the sum over all lines plus a noise-control line, not on the new lines against the old line's periods. Prefer editing stops over delete/recreate. When comparing designs, compare two new lines of the same age.

### 2026-09-27 P1T — splitting a line at a stop on a road with no junctions adds two long turnarounds
- **Situation:** plan §4.7.3 split line 4 at the Red HIT stop pair (seg 6110, Dixon St). It estimated the halves at 3.8 + 5.2 km assuming the buses could turn at the hub.
- **Action:** routed the closing legs with U-turns forbidden (tmp/tampa/p1t/route3.py) before creating the halves.
- **Result:** Dixon St has no junction for ~600 m either side, so 4N turns in 2,175 m and 4S in 1,472 m. In game: 5,473 + 6,936 m (line 4 was 8,889), 32 buses instead of 23. Riders 257 -> 275 -> 355 (the last is above the ±30% swing).
- **Rule:** before any split, route each half's closing leg with U-turns forbidden. If the hub sits on a stretch with no junction, move the split to a stop pair beside a junction or a block the bus can circle, or accept and report the extra bus-km.

### 2026-09-27 P1T — the road router must forbid U-turns at ordinary nodes
- **Situation:** b27/route2.py allows a U-turn at any node. It read line 9 at 3,086 m (game 4,890) and line 4 at 7,558 m (game 8,889).
- **Action:** read PathFind.ProcessItemMain (tmp/tampa/10x/dec/PathFind.cs:726-755).
- **Result:** a road vehicle may take its own segment back only at End or OneWayOut nodes, or where no other segment at the node carries its vehicle category. route3.py allows reversal only where the node has one road segment, and requires arriving at a stop's segment by a different one. It reads +3-7% high (94 10,224 vs 9,716; 4 9,508 vs 8,889; 9 5,025 vs 4,890).
- **Rule:** use route3.py (upper bound) and route2.py (lower bound) together. A leg that differs a lot between them is a turnaround: fix it before the dry run.

### 2026-09-27 P1T — a feeder hub on an arterial: circle the station's block instead of turning round
- **Situation:** the NW1 feeder had a ~1.4 km turnaround east of metro 1005 (4,890 m, periods 0-11). Sophie North now joins NW1 to Robert at 4-way node 13951, and 1005 sits inside the Robert / Richardson / Birch / Sophie Blvd block.
- **Action:** made a one-way loop: hub stop on Robert eastbound 33 m from 1005 (the lane 94 already used), right round the block, up Sophie North, round NW1, down Chester, east on Robert.
- **Result:** 3,353 m, 11 stops, 9 buses, no problems; periods 55-98 (mean ~82) against 0-11 before.
- **Rule:** for a feeder that must start and end at a station on an arterial, use one hub stop in one direction and close the loop round the station's block, instead of serving both sides and turning round.

### 2026-09-27 P1T — stop densification in TAmpa finds almost nothing
- **Situation:** plan §4.7.6 #8: densify to 200-250 m on the split halves and on 13, 142, 203 in mixed cells.
- **Action:** scored a candidate on every leg over 420 m by homes+jobs within 250 m that have no bus stop of any line within 250 m, requiring homes and jobs within 300 m (tmp/tampa/p1t/densify.py).
- **Result:** one mirrored pair qualified (203 on Crowley Blvd, ~73 homes each); every other candidate scored 0 because another line's stop was already within 250 m. 203 went 228 -> 289 mean (+27%, inside the swing).
- **Rule:** in TAmpa's built-up core, stop spacing is not a lever. Spend stops on new districts.

### 2026-09-27 W1 — build-grid roads can come out with no lanes
- **Situation:** 13 build-grid calls for the W1 district (623 road segments), then services along them.
- **Action:** after 24 of 43 services showed RoadNotConnected, dry-ran a 2-stop bus line 4 m beside every road segment (tmp/tampa/w1/laneprobe.py).
- **Result:** 474/623 segments snapped at a distance equal to the stop's distance from (0,0), i.e. no lane geometry: 0/44 in the first grid call, 27-53 in every later one, 0 of the 62 single build-network pieces. A bus test line on them was LineNotConnected. Building a 14 m Basic Road Elevated stub from a node and bulldozing it straight away updated the node and fixed every segment on it (160 nodes -> 0/623 bad, 0/43 services with a problem, road anomalies unchanged, no zone change because elevated roads have no zone blocks). City-wide RoadAccessFailed then fell from 87 (WF's count) to 4.
- **Rule:** after any build-grid, run the lane probe on the new segments before placing buildings or zoning; repair with tmp/tampa/w1/touch.py. Do the same for pipes laid with build-grid (no probe exists; touch them).

### 2026-09-27 W1 — set-zone paints whole zone blocks, by block centre
- **Situation:** residential had to go in chunks of at most 50 cells.
- **Result:** radius 28 at a block interior point changed 80 cells (5 zone blocks). The command paints every zone block whose centre is inside the radius (src/ZoneCommands.cs). A zone block centre sits road half-width + 16 m from the road centreline at the segment middle; radius 12 there hits exactly 1 block (<= 32 cells), radius 8 missed some blocks next to collectors, 16 hit 2.
- **Rule:** to stage zoning, target zone-block centres with radius 12 (tmp/tampa/w1/stage_zone.py). With the 50-cell / two-week rule a 14k-cell district takes about 440 chunks (~880 game weeks): plan for it, or get the player to relax the rule.

### 2026-09-27 W1 — jobs zoned before homes go short of workers
- **Situation:** W1 offices (4,541 cells) and parkway CommercialHigh (2,385 cells) were zoned at once while residential waited for the staged queue; commercial demand was 44, residential 82.
- **Result:** 72 shops grew on the parkway within ~3 game months; 32 of them showed NoWorkers (14 MajorProblem) while W1 had 6 residential buildings. City-wide NoWorkers 14 -> 81 over the same window (other builders also zoned jobs).
- **Rule:** in a new district, zone commercial in step with the residential chunks (e.g. one CH block per 3-4 residential blocks), not ahead of them. Offices are safer to zone early only while workplace demand is near 0.

### 2026-09-27 W1 — /state/networks and /state/facilities return at most 5,000 rows
- **Situation:** road and pole guards built on one `?limit=20000` call after the city passed 5,000 segments.
- **Result:** `returned` stayed at 5,000 while `total` was higher; everything with a higher id was invisible (two power poles went onto road edges; a missing "road" scare). Per-service calls are complete (Road 2,226, Water 1,767, Electricity 209, PublicTransport 2,137).
- **Rule:** page by `service=` and check `returned == total` (w1lib.segs_now / allfac).

### 2026-09-27 W1 — Blue West, the station order and lots
- **Situation:** Blue extended from NO's free south end to three new stations 1.1 km, 0.5 km and 0.7 km apart.
- **Action:** stations placed on the new grid first (a Metro Entrance needs frontage), then b16plan.py per leg with the other platforms' nodes in skip_nodes (otherwise the start platform itself fails the 14 m clearance and the planner returns nothing).
- **Result:** 54 pieces, 2,048 m, max turn 29.2 deg; a lot touching a Medium Road's edge failed with ObjectCollision until moved 3.5 m back (-500 -> -496.5); the station still got road access.
- **Rule:** pass every platform end node of the leg into skip_nodes; leave ~0.5 m between a station lot and a Medium Road edge.

### 2026-09-27 W1 — Blue 135 after the L3 fix
- **Situation:** 135 showed LineNotConnected on its three new W1 stops for 160 s after the append; the cause was L3 piece 34851.
- **Action:** rebuilt 34851 (as 17709), then removed and re-added the three stops in one transit-line-edit about a minute later, without waiting to see whether 135 would re-path on its own.
- **Result:** 8/8 trains, no problems, 9,979 m within 30 s of the edit.
- **Rule:** consistent with "lines do not re-path after track fixes" (Train Line 69), but not a second confirmation because I did not wait. Re-adding the affected stops in one edit is a quick, safe way to force the re-path.


### 2026-09-27 E1 — a lattice row along the shore edge comes out as roadTerrainCliff
- **Situation:** E1 grid built as 286 guarded 80 m pieces (grades <= 10 %); the rule was that road anomalies must not rise from 75.
- **Action:** read /state/road-anomalies after the batch and matched segment ids against my build list.
- **Result:** 75 -> 96: 21 of my pieces flagged roadTerrainCliff (maxSideToSideDelta 24-46 m), all on rows running along the top of the shore slope (z 1000, z 2040 east of x 3620, z 2120). Their own grade was fine; the terrain beside them falls to the water. Bulldozing them brought the count back to 75 but left one 80 m stub (17971) as a separate component (disconnected 0 -> 1) until it was removed too.
- **Rule:** keep the outermost lattice row one block back from the shoreline slope (test side-to-side terrain, not only the piece's grade), check anomalies by segment id after every grid, and re-check disconnected components after removing anything.

### 2026-09-27 E1 — wf.net_guard ignores a crossing within 10 m of the new piece's end
- **Situation:** planning the E1 lattice east of the coastal main line with tmp/tampa/wf/autogrid.plan_grid (dry run + guard_piece).
- **Result:** the plan connected lattice points west of the track to E1: edges such as (3300,1480)-(3380,1480) cross the track at x ~3308, 8 m from the end, and net_guard skips any intersection within ends_ok=10 m of an end. Caught on the map before building; nothing crossed the track.
- **Rule:** when a grid sits beside a track or highway, add an explicit side-of-line predicate (e1/plan_grid.py trackx) and never rely on net_guard alone near lattice nodes.

### 2026-09-27 E1 — a feeder hub at a big junction: stop on the arterial leg that is already the turnaround
- **Situation:** nearest station to the E1 access was Red metro 12209, 25 m south of Empire Blvd beside the Dixon/Empire/Laurel junction 17238; Empire has no junction for 250 m west, Dixon Medium none for 170 m south.
- **Action:** routed hub candidates with the no-U-turn router on live roads (e1/router.py, route3 logic plus 'Oneway' prefabs): Dixon Medium south 4,570 m total, Dixon Large SW-bound 3,477 m, Empire westbound the same path as the latter but 37 m from the station.
- **Result:** line 141, 12 stops, router 5,877 m, game 6,006 m (+2 %), 16/16 buses, no problems.
- **Rule:** at a station beside a multi-leg junction, route every leg's first stop with U-turns forbidden and put the hub on the leg the turnaround loop already uses; it costs nothing extra and is the closest stop.

### 2026-09-27 Portville P1: the highway changed between the survey and the build
- **Situation:** The plan attached Station Avenue to two north highway stubs (9971, 22760) that the survey saw as dead ends with RoadNotConnected. The build started 22 minutes after the survey.
- **Action:** Re-read /state/networks and traced both nodes by segment id before building anything.
- **Result:** 78 new bus-lane highway segments now join the north stubs to the south stubs across the river. There were 0 free road ends apart from the 8 map-edge connections, roadComponents went from 2 to 1, and there were 0 problems. Nothing was built. The attach went back to the player.
- **Rule:** Always re-trace the attach nodes live at build time; a survey older than the player's last session is stale. With no free end, attaching means designing an interchange. That is the player's call, not the builder's: stop and report with dry-run options.

### 2026-09-27 Portville P1: the lane probe needs Bus unlocked
- **Situation:** the W1 rule says to lane-probe every build-grid road with a Bus transit-line-create dry run. In a new city Bus is locked.
- **Action:** ran tmp/portville/p1/probe.py over the 63 new road segments.
- **Result:** every call returned HTTP 500 "Bus is not unlocked yet"; no segment could be checked. Touching all 44 new nodes (touch.py pattern) was harmless: 44/44 stubs started on the right node and were removed, segment count and road anomalies (0) unchanged.
- **Rule:** before Bus unlocks, touch every new grid node preventively and log the lanes as unverified; re-probe when Bus unlocks.

### 2026-09-27 Portville P1: water pumps need their own power
- **Situation:** the P1 plan had pipes to the intake and outlet but no power to either; both are far from the town's turbines.
- **Result:** both showed Electricity + WaterNotConnected. A Wind Turbine ~75-90 m away plus a ~19 m Power Line (ends 8 m from each footprint) cleared the intake's problems within about a game week. The outlet kept WaterNotConnected with a pipe node on its centre and no sewage producers yet (cause unverified).
- **Rule:** plan power for every pump and outlet with the pipes. A local turbine avoids long pole lines over future districts or across another builder's area.

### 2026-09-27 Portville P1: the player builds in the area while the builder works
- **Situation:** during 14 minutes of P1 building, a Nuclear Power Plant, a Solar Power Plant, 17 pipes and ~30 power line segments appeared, one line across the reserved station strip and one pipe splitting my trunk segment.
- **Rule:** (confirms the 2026-09-25 operator lesson) re-read networks by id before trusting a build log; a missing id may just be split by a player's junction (check for a new node on the same line).

### 2026-09-27 Portville IC — build-network never splits a segment; bridge one-ways run start -> end only in right-hand traffic
- **Situation:** designing a highway interchange on Holmes/Underhill (bus-lane highway pair) through the bridge.
- **Action:** read src/RoadCommands.cs and src/NodeHelper.cs, decompiled NetTool/NetManager/NetInfo/NetSegment/NetNode/RoadBaseAI (ilspycmd in the session scratchpad; needs DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/<ver>/libexec).
- **Result:** an endpoint snaps to the nearest road node within snapDistance (8 m default, 64 max, height ignored) or creates a free node; an existing segment is never split. Segments are created with invert=false. Lane direction = m_finalDirection (inverted in LHT saves) XOR segment Invert; NetTool sets invert in LHT, the bridge never does. So a bridge-built one-way runs start -> end in RHT and end -> start in LHT. Portville's 4 map-edge connection pairs read keep-right (Road Connection Incoming/Outgoing flags). The start/end order of player-built segments says nothing about travel direction (Invert is common). RoadNotConnected marks a node whose car lanes are all in or all out.
- **Rule:** join ramps only at existing highway nodes (or re-lay the highway to create one); give ramp start = where traffic comes from. Confirm keep-right from the outside-connection flags before trusting start -> end, and check a multi-piece ramp for RoadNotConnected on its middle nodes. A direct flyover from the far carriageway is impossible when the carriageways are ~32 m apart: ~145 m of climb is needed before crossing.

### 2026-09-27 Portville IC built: the highway had become an avenue in the 9 minutes between design and build
- **Situation:** building the 14:07 interchange design (ramps onto "Highway with Bus Only Lane" Holmes/Underhill).
- **Action:** re-listed every road segment in x -100..320, z 380..1180 and diffed against the design snapshot (net0.json) before the first command.
- **Result:** Underhill and the north part of Holmes were now "Large Oneway" surface avenues (39 added / 36 removed segments, same node ids at the ramp points). The NB half-diamond still fitted (HighwayRamp joins a Large Oneway node fine; 0 RoadNotConnected, anomalies 0, cityConnectedToOutside false -> true after the off-ramp alone). The SB flyovers were replaced by a 2-piece at-grade Medium Road crossover between the surface nodes 24949 and 6104.
- **Rule:** (third confirmation of "re-trace live at build time") diff the whole build box against the design snapshot by prefab, not only the attach node ids: ids can survive while the road type under them changes, and the change can make a simpler design valid.

### 2026-09-27 Portville IC: a ground prefab with node elevation does not raise the terrain sample
- **Situation:** NB on-ramp HighwayRamp nodes built with elevation 3.5 and 4.5 to keep grades <= 8 % on 11-14 % terrain.
- **Result:** built fine (y 151.9 / 154.5, no problems), but a dry run at the same points afterwards still reads terrain 148.4 / 150.0.
- **Rule:** do not count on the bridge's ground pieces to build an embankment; use the Elevated prefab for anything more than ~1 m above terrain, or accept the terrain grade. Unverified by eye whether the piece floats.

### 2026-09-27 Portville IC: build-network ids survive a game rename
- **Situation:** mid-build the game renamed Underhill Avenue to "Stephen Harris Avenue" and the collector pieces to Pearl/Dixon Boulevard.
- **Rule:** (confirms "track created entities by id, never by name") re-find roads by segment id; names move when road groups change.

### 2026-09-27 P1g — a lattice cell needs set-zone radius 40, not 34
- **Situation:** first RL chunk in Portville D1 (80 m grid, Basic roads), block centre (720,910), city.md said radius 34.
- **Action:** dry-run set-zone at radius 34..48.
- **Result:** radius 34 matched 0 blocks; 35..47 matched the same 8 (the cell's own blocks); 48 picked up neighbours. Painted at 40: 118 cells changed, 8 blocks. (Later, after other roads were rebuilt, 33 already matched 8, so block positions sit right at ~33-35 m.)
- **Rule:** paint an 80 m lattice cell with radius 40 at its centre and check `touchedBlocks` == 8 in a dry run first.

### 2026-09-27 P1g — Portville power: separate pole networks and too little capacity
- **Situation:** 3 services and the first 4 RL houses unpowered; 5 separate power-line components, the long one with no plant; houses 20 m from a pole on the turbine-only network.
- **Action:** joined every component into the nuclear line; then, after D1 + D2 grew, 29 buildings showed Electricity and I added 4 Wind Turbines.
- **Result:** the services were powered in about a game week; the first 4 houses went Abandoned before the join (cause not separated: capacity of the 2-turbine grid vs the 20 m gap), regrown houses were powered; the 29-building shortfall cleared within ~3 game days of the turbines.
- **Rule:** read the power-line components (union-find over Power Line segments) before zoning and make it one grid with a plant on it; add generation as soon as Electricity problems appear on more than a handful of buildings (industry is the big consumer). A zoned block does not need a pole inside it once any neighbour building is powered.

### 2026-09-27 P1g — another hand zones while the builder follows the demand gate
- **Situation:** one chunk per game week was the plan; at speed 3 a game week is ~30 s real time.
- **Result:** within 5 minutes all of D1 (incl. the reserved Promenade frontage and service lots), CL in D3 and industry in the E tile were zoned by someone else; the planned clinic lot was built over and the Bus Depot lot (660,982) too.
- **Rule:** re-read /state/zone-anomalies and growables right before each chunk and each service placement; when a planned lot is gone, find the nearest clean lot facing a Basic road (find_sites) instead of repainting built lots.

### 2026-09-27 Portville rail — Cargo Train Terminal geometry and a siding on existing nodes
- **Situation:** the cargo terminal had to sit beside the mainline without blocking passenger platforms; the bridge never splits segments, and the Cargo Center's track offset was unknown.
- **Action:** placed a trial `Cargo Center` in empty land and read its nets, bulldozed it, then solved a through siding whose switches are existing mainline nodes (12674, 23254, 353 m apart): cargo track 22 m off the chord, one 58-62 m diagonal and a 30 m in-line lead at each end.
- **Result:** `Cargo Center` ("Cargo Train Terminal", 16x8) makes one two-way `Train Cargo Track` of 176 m, centred 24 m behind the pivot, plus `Cargo Connection` truck paths that end exactly at the front edge. Re-placed 42520 matched the solved ends to 0.01 m. Turns: switches 27.0 and 25.0 deg, diagonals 20.8, leads 0.0. Station active with no problems once a Medium Road ran 0.7 m clear of its front. net_guard flags the building's own `Cargo Connection` segments; exclude that prefab.
- **Rule:** for a siding, choose two existing mainline nodes at least (176 + 2 x (30 + offset / tan 20deg)) m apart and place the terminal between them; offset 20-22 m keeps every turn under 30 deg. Place a trial building to read any station's track geometry before planning links.

### 2026-09-27 Portville rail — other builders remove power lines silently
- **Situation:** between two reads 7 minutes apart, another builder's new road (Robin Street) replaced the ground where Power Line 3131 ran; the city's link to Nuclear 31187 went with it.
- **Result:** 42 D1/D2 buildings showed Electricity, far from any work of mine. Two new power pieces to a pole inside the plant's lot cleared all 42 by the next read (~3 min).
- **Rule:** after any build near power lines (yours or not), and whenever Electricity problems jump, list the power segments along the old route by id and diff them against the last snapshot before blaming capacity.

### 2026-09-27 Portville rail — a train station creates intercity lines; Train unlocked mid-session
- **Situation:** Train read unlocked false at 15:00; Central Station was placed at 15:04.
- **Result:** Train Line segments to the E and S edge connections appeared at Central immediately (intercity service, no player line). Train read unlocked true at 15:19 (cause not identified; not verified whether the station placement or a milestone flipped it). T1 (line 11) then created cleanly: 1/1 train, no problems, 1,378.7 m = 2 x station distance.
- **Rule:** re-read `transportPrefabs[].unlocked` after placing the first station of a type before recording "locked"; exclude `Train Line` segments from rail-track connectivity checks.

### 2026-09-27 Portville hwy: build-network dryRun is only a terrain sample
- **Situation:** planning IC East, the Dallas frontage corridor and IC North Gate (89 road pieces, 3 new highway nodes per side).
- **Action:** read src/RoadCommands.cs before trusting dry runs; wrote tmp/portville/hwy/check.py (owned tile, rail >= 40 m,
  rail builder reserved.json polygons and polylines, building footprints incl. poles, unexpected snaps within 8 m, same-height
  road crossings) and ran it on every generated piece before any real call.
- **Result:** the dryRun returns ok for anything (it only sets startY/endY from terrain); the real call also skips collision and
  area checks. check.py caught 5 poles on the frontage alignment, 2 ramp nodes closer than 19 m to a carriageway, a planned pole
  inside a lot, and a false "rail" hit from `Train Line` transport-line segments (filter on 'Track'). Built result: road anomalies 0,
  one component, 0 RoadNotConnected after the cross streets closed the frontage ends.
- **Rule:** treat build-network dryRun as a terrain probe. Gate every piece with an own-geometry check against live buildings,
  networks, owned tiles and reserved corridors; filter rail by 'Track' in the prefab (transport lines are PublicTransport too).

### 2026-09-27 Portville hwy: Texas diamond over an at-grade highway
- **Situation:** the Dallas pattern wants frontage roads meeting the cross street at grade, but the highway is at ground level.
- **Action:** elevated cross street (`Medium Road Elevated`, +8 / +9.5 over the median / +7) with the frontage roads climbing to
  meet it at elevated junction nodes (elevated prefab on every piece touching an elevated node); clearance planned >= 8 m.
- **Result:** measured 8.0-9.3 m over both carriageways; the roadCrossingWithoutNode tolerance in GameState.cs is 5 m, so no
  anomaly. Grades 5-12 %. Vehicles used X1 and both frontage roads within ~2 game days.
- **Rule:** for an interchange on an at-grade highway, raise the cross street and let the frontage roads climb to it; a separate
  U-turn bridge right beside the cross street cannot clear 5 m because the frontage roads are still climbing there, so put the
  dedicated U-turn at the corridor end (or accept the signalised double-left at the diamond).

### 2026-09-27 Portville hwy: zone blocks are 4 cells wide, centred 32 m off the road edge
- **Situation:** painting only the retail pad strip (south of a frontage road) that backs onto another road's strip 86 m away.
- **Action:** scanned set-zone dryRun (radius 4.5, 6 m grid) to locate block centres.
- **Result:** block centres sit ~32 m from the road edge (the 8-deep block's middle), so the pad strip (1428 -> 1396) and the
  opposite strip (1358 -> 1390) both have centres at z ~1390-1396; a radius paint cannot separate them. Painted both CL
  (+486 cells) and two corner blocks that came out patchy (14 cells each) with a radius-3 repaint.
- **Rule:** to paint one side of a road only, the two roads must be >= ~100 m apart, or accept painting both strips; locate
  block centres by a dryRun scan, not from the cell geometry.

### 2026-09-27 Portville hwy: retail pads powered by conduction, no pole line
- **Situation:** zoned 486 CL cells on the south pads with no power line through them (the old z 1440 pole line was removed
  because it sat on the frontage alignment).
- **Result:** 12 shops grew within ~9 game days with no Electricity problem; neighbours were the powered landfill 33150,
  turbines 35139/38226 and D2 houses.
- **Rule:** pads next to powered buildings (<= ~40 m) take power by conduction; only run poles where the new zone is isolated.
  Confirmed once.

### 2026-09-27 Portville P2 — a zsh loop sent broken coordinates again
- **Situation:** checking set-zone matching per lattice cell with `for c in "720 590" ...; do set -- $c; ...` in the Bash tool (zsh).
- **Action / Result:** zsh did not split `$c`, so `$1` was "720 590" and every dry run returned matchingBlocks 0. I briefly read it as "the new grid has no zone blocks yet"; the same cells from Python matched 4-8 blocks.
- **Rule:** second confirmation of the TAmpa rule "run shell loops under bash": drive loops over coordinates from Python (or `bash -c`), never zsh `set -- $var`. A zero from a loop is a tooling suspect before it is a game fact.

### 2026-09-27 Portville P2 — guard every planned grid piece against live lots before build-grid
- **Situation:** the plan's D1 south (5x2) and D1 west (3x6) grids joined an already-built district.
- **Action:** plan1.py ran road_guard (building footprints) and net_guard on every lattice piece before any build.
- **Result:** 13 pieces were blocked: houses of the z 630 row back onto z 590..622 across every column line, and D1 west held a high school, a child health centre, a police station, a turbine and a pole line placed by another hand. Shrinking the grids to 5x1 and 3x2 built with 0 anomalies and no demolition beyond 2 buildings.
- **Rule:** where a new grid meets built blocks, stop the lattice one row short of the backs of existing lots; a column that crosses a built back-lot strip demolishes a house per column. Check with road_guard before build-grid (its dry run reports no collisions).

### 2026-09-27 Portville P2 — services first, then paint with preserveOccupied false
- **Situation:** new empty grids with pole lines and service buildings placed before zoning.
- **Result:** preserveOccupied true skipped all 8 blocks of most cells (poles, a firehouse or a school nearby). Painting with preserveOccupied false when the cell held no other-type growable zoned 160 cells per cell; building anomalies stayed 1, no Electricity/Water problems on the first 60 houses.
- **Rule:** in a new district, place services and poles first, then paint each cell with preserveOccupied false unless a growable of another zone type is inside the cell (stager.py `paint`).

### 2026-09-27 Portville P2 — R demand falls fast; the gate works
- **Situation:** R 99 with W 0 and C 0.
- **Result:** 6 chunks (~720 cells) took R from 99 to 26 within about one game month; the stager then held and resumed at R 40-45 with 7-day gaps.
- **Rule:** at R 100 in a ~4.5k city, 3 cells per district per week is already enough; do not zone a whole district at once.

### 2026-09-27 Portville P2 — the bridge will not join a path to a road node
- **Situation:** the Mall needs `Pedestrian Pavement` ending on road nodes 16748/28975.
- **Action:** dry run (ok, but it reports no node ids) and a read of src/NodeHelper.cs `CanReuseNode`.
- **Result:** reuse needs the same prefab, Road+Road, or equal service/subService/layer; paths are Beautification/BeautificationParks, so a real call would drop a second node on top of the road node (dead-end path).
- **Rule:** do not build paths that must join roads until the bridge allows it; the dry run cannot reveal node joins, so read NodeHelper before trusting any cross-service join.

### 2026-09-27 P2b: Bus depot unlocks the bus line tool; connect on a roadside lot builds a stray stub
- **Situation:** Portville Bus lines were refused ("Bus Line Requirements" -> "Bus Depot Created" 0/1). Placed `Bus Depot` 18067 at (624,1077) a0 facing row z 1110, then ran `/commands/connect` from the lot front (624,1101) as the skill says.
- **Action:** validated placement (clean), connect, then read road-anomalies and `/state/transit` unlock.
- **Result:** Bus `lineTool` flipped true within ~20 s of the depot existing. connect built Basic Road 20827 (new node 9125 -> 16123) running alongside the existing row road (shortRoadStub + deadEndNearRoad); the depot was already served (it dispatched 24 buses after the stub was bulldozed).
- **Rule:** For a Roadside building whose front edge is within a few metres of a road, do not call connect; check road-anomalies right after any connect and bulldoze a stub it made. Unlock a transit mode by building its depot/station, not with ignoreUnlock.

### 2026-09-27 P2b: routing three bus lines first-time-right on a lattice
- **Situation:** B1/B2/B3 in Portville; the network has player one-way couplets whose stored start/end is not a direction signal, so a directed graph that trusted start->end reached only 212 nodes from Central.
- **Action:** router (tmp/portville/bus/rt.py) with every one-way prefab excluded and U-turns only at dead-end nodes; stop points at segment midpoints 6 m to the right of travel; terminal stop on a dead-end forecourt street so the turnaround is legal.
- **Result:** every dry-run stop snapped to the designed segment (1.5-4.5 m); game lengths 2,005 / 3,129 / 3,928 m vs router 2,218 / 3,178 / 4,138 (router high by 1.6-10 %); no LineNotConnected, vehicles = target.
- **Rule:** Put stop points at segment midpoints (the game places them there anyway) and never 6 m from a lattice node, or the nearest segment is the cross street (two B3 points at x 360 snapped 0.0 m to the N-S street). Exclude one-way prefabs from bus routing unless their direction is known.

### 2026-09-27 P2b: path-to-road join test failed on the new DLL
- **Situation:** the bridge's new `CanJoinPathToRoadNode` (DLL 16:24:54, game started 16:26:31) was meant to let a `Pedestrian Pavement` end on a surface road node.
- **Action:** one 36 m pavement ending exactly on dead-end Basic Road node 409 (1473.5,829.69).
- **Result:** endNodeId 11553, createdNodeIds [31594, 11553]: a new node on top of 409, not a join. Removed it (seg 24076, keepNodes false); zone totals unchanged.
- **Rule:** Paths still cannot join roads through the bridge. Do not build the Mall or any path meant to meet a road until TODOS "Path-to-road node join" is fixed and re-tested with one piece.

### 2026-09-27 Portville M1: Metro Entrance dry run now reports collisions; Dubins loops on short gaps
- **Situation:** siting M1 stations on Station Ave and planning the 86 m Central -> Civic gap.
- **Action:** place-building dryRun validate:true for `Metro Entrance` over known shops; b16plan.py for platform ends 86 m apart with 9 m lateral offset.
- **Result:** the dry run returned collidingBuildingIds (e.g. [4544]) and SlopeTooSteep/ObjectCollision toolErrors, contradicting the 2026-09-26 note that it only said "validation passed" (bridge has changed since). The planner returned a 402 m, 15-piece loop at R 50 for the 86 m gap: with two 30 m leads the 26 m middle cannot absorb a 9 m S-shift (needs about L^2/4R), so it chose a circle. A plain 2 x 43 m straight has 9.7 / 6.0 deg platform-end bends.
- **Rule:** use the dry run's collidingBuildingIds as the lot check (still re-run just before placing). When planned length is far above the station gap, the planner has looped: check a direct straight's end bends first; if <= 40 deg, use it.

### 2026-09-27 — Demolished lots regrow in under a minute (Portville M1)
- **Situation:** Civic/West Gate metro lots needed one building each demolished (4544, 45181).
- **Action:** demolished at speed 3, then handed placement back to a builder.
- **Result:** within ~60 s real time both lots had new growables (45074, 10374) on the still-zoned cells; placement blocked. Second try: paused the sim, demolished 45074/10374, placed Metro Entrances 43595/29599 immediately — clean.
- **Rule:** demolish-for-placement runs paused, with the placement in the same step (or unzone first). Never leave a gap between bulldoze and place at speed.

### 2026-09-27 Portville M1: zoned lots regrow within a minute; Metro Entrance stops came back fixedPlatform with two points
- **Situation:** the orchestrator demolished shop 4544 and house 45181 for M1 station lots; the builder re-checked about a minute later. Then lines M1W/M1E were created on three Metro Entrances.
- **Action:** validated dry run on both lots; later transit-line-create for one line per direction.
- **Result:** both lots already held new growables (45074 L1 shop, 10374 L1 house). The second demolition only held because the orchestrator paused the game and placed the stations straight after. The lines read fixedPlatform true, with stops at different points per direction (Central z 760.2 vs 756.2, Civic 756 vs 752, West Gate 823.5 vs 819.5). That contradicts the 2026-09-26 note (one stop point, fixedPlatform false). Both lines were Complete with no problems and 1/1 vehicles within 100 s.
- **Rule:** to free a zoned lot, pause (or unzone) and demolish and place in one step. Metro Entrance platform sides can now be checked in the dry run and line stops; keep one line per direction anyway.

### 2026-09-27 Portville P3 NE: extending a 2-stop train line to a third station
- **Situation:** T1 (line 11) ran out-and-back Central <-> Riverside East. The brief asked to extend it to the new North station.
- **Action:** added North to line 11, giving loop Central -> RE -> North. Created line 26 with the reverse order, Central -> North -> RE. Both use one stop per station.
- **Result:** both lines ran 1/1 train with no problems within 45 s. Lengths were 3,626 / 3,627 m, i.e. the loop with no detour. Each loop's closing leg runs through Central without stopping, and that pathed fine. Line 26's Central stop resolved 2 m from line 11's (the other platform side).
- **Rule:** a 3-station out-and-back train service needs two lines in opposite orders. One 3-stop loop serves each station pair in one direction only (Central -> North would ride via RE). Add the new station to the old line by edit, and create the mirror line.

### 2026-09-27 Portville P3 NE: the lane probe passed but a bus leg still failed
- **Situation:** feeder N1 (line 94). All 87 NCW grid segments passed the lane probe after the touch repair.
- **Result:** 30 s after creation, stops 2-3 (z 2200 row, WB over 8286 -> 30724 -> 18627 -> 13159) showed LineNotConnected, and the line length was 344 m short. 39 of my new straight road segments showed a 4-5 m middle shift, but legs over many of them pathed fine, so the shift did not tell good from bad.
- **Action:** touched the three nodes on the failing leg (28430, 25920, 26027), then removed and re-added stop 3 in one transit-line-edit.
- **Result:** 7/7 buses, no problems, length 3,655 m.
- **Rule:** the probe proves that each segment's stop lane exists, not that lanes connect at nodes. After creating a line on new grid roads, read problems at about 30 s. On LineNotConnected, touch every node of the failing leg's router path and re-add one stop of that leg. Do not use middle shift on roads to find bad nodes.

### 2026-09-27 Portville P3 NE: back-line power poles do not power roadside services
- **Situation:** power lines ran along block back lines every 160 m, before zoning, with 12 services placed on the empty blocks.
- **Result:** 11/12 services read Electricity. Their lots sat 49-80 m from the nearest pole, and there were no growables yet to conduct. p2/spur.py (a pole 6-14 m outside each lot, joined to the nearest pole) cleared all of them within 30 s.
- **Rule:** in an empty district, spur every service right after placing it. Back-line poles only carry once blocks have grown.

### 2026-09-27 Portville M1 fix: B1 made a West Gate feeder (duplication test)
- **Situation:** metro M1 (lines 30/173, 3 stations, 1.64 km) had carried ~0-1 riders a week for 7 game months. Bus B1 had stops
  29-59 m from all three stations. Baseline lastPeriod: M1E 0, M1W 1, B1 192.
- **Action:** edited B1 (135) in place to 8 stops with exactly one near an M1 station (West Gate, 56 m). Nothing within 213 m of Civic
  or Central. Routed with rt.py first (router 2,240 m; game 1,942 m). Budget 150 -> 160 to keep 6 buses. Measured 8 weekly periods.
- **Result:** M1 combined riders were 15-61 a week for the first ~6 weeks, then 4-18 (periods 4-8 mean 10). m_averageCount went
  M1E 0 -> 6 and M1W 1 -> 7. B1 went 192 -> 108-163. M1 stop waiting stayed 0-11.
- **Rule:** removing a bus that duplicates metro hops moves a dead metro off zero (third confirmation of the duplication rule's
  direction). It does not make a short 3-station metro carry tens a week. Look at the metro's own catchment and trip ends next.
  A bus line's target vehicles = ceil(budget% x length / 60,000 m) (fits B1/B2/B3: 6/8/10). Shortening a line drops a bus unless
  the budget rises. The in-place transit-line-edit (remove 1..n-2, move 0/1, append the rest) keeps the line id. Periods roll
  every 7 game days (~50-60 s at speed 3), line by line within ~2 s, so sample once per rollover, not on the first change.

### 2026-09-27 Portville Access A1: more buses on a crowded line, 2 trains per T1 line, an industry feeder
- **Situation:** B2 (218) had 105/146 waiting at two stops with 8 buses; T1A/T1B ran 1 train each; the East Works industry (61 lots,
  625 cells) had no stop, so only 45% of job lot cells were within 150 m of any stop.
- **Action:** B2 budget 150 -> 206 (11 buses); T1A/T1B budget 100 -> 150 (2 trains each); new 7-stop bus B4 (line 21, 4,123 m,
  6 buses at budget 84) from the Riverside East forecourt around Robin Avenue, routed on the road graph with rt.py first.
- **Result:** targets reached within 40 s. Jobs within 150 m of a stop 45% -> 77%, within 300 m 63% -> 99%. B2 waiting at (1200,386)
  peaked at 158 while the new buses fed in, then read 0-14 for the last 8 minutes; B2 weekly riders 147 -> 201. T1A 33 -> 80, T1B
  55 -> 92 weekly; Central train-stop waiting still spikes to 76-140. B4 rose to 90-93 riders per period, then fell to 7-17.
  Train target = ceil(budget% x length / C) with C between 362,600 and 543,900 m% (3,626 m: 1 at 100, 2 at 150).
- **Rule:** raising a crowded bus line's budget clears its stop queue within ~2 game months; expect a short spike first as the backlog
  boards. A new industrial feeder's first weeks overstate its riders; judge it on periods 8+. Check the road graph for branches that
  only exit onto a highway (Sterling St here) before planning stops on them; cover them from the spine instead.

### 2026-09-27 Portville pollution fix: cs1_connect Road again built a stub through the building
- **Situation:** two Combustion Plants placed Roadside with the front edge ~3 m from Basic Road 2521, then `/commands/connect` Road/Water/Electricity from each plant centre, as SKILL.md says.
- **Action:** connect toService Road with from = building position.
- **Result:** Basic Road 14310 and 17301 (34 m, new nodes 26101, 13985) ran from the plant centres to the road through both footprints (deadEndRoad x2). Bulldozed them; 6 game days later both plants were Active with problems "" without them. Known open TODO (src/CompositeCommands.cs:127), seen in P2b too.
- **Rule:** see Proven Rules. Also: `/state/zone-anomalies?minMinorityCells=0&minUnzonedCells=0&limit=5000` lists every zoned block with position and zone counts (the only per-block zone read), and `set-zone` repaints a whole block, so check a block's zoneCounts for mixed zones before choosing the target zone.

### 2026-09-27 18:31 — Removing an incinerator caused a power shortage (Portville pollution fix)
- **Situation:** removed Combustion Plant 46375 (45 m from homes) after two replacement incinerators (47905, 18536) went live; also demolished 5 D2-west factories and rezoned 24 blocks (18 → ResidentialLow, 6 → Office).
- **Result:** /state/problems went to Electricity 140 (shortage, not disconnection: ElectricityNotConnected only 4), all in the NE district (x 800–1600, z 1600–2400) — the newest growth at the grid's far end.
- **Rule:** incinerators are power plants too, and the bridge cannot read capacity. Before removing any power-producing building, add surplus generation first; stop zoning stagers while power is short.

### 2026-09-27 18:43 — Portville Power 2: surplus generation after the incinerator removal
- **Situation:** Electricity 140 in the NE after Combustion Plant 46375 (an incinerator, which also generates power) was removed. By the first read ~2 min later it was Electricity 1: the replacement incinerators 47905/18536 were ramping up as garbage arrived.
- **Action:** added Nuclear 27435 (2461,377) and Solar 7690, 19956 on the east industrial side, 855-1,081 m from the nearest home; one Power Line each to a main-grid pole (lot edge +6-14 m, spur2.py), then `connect` toService Water with roadPrefab `Water Pipe` from each centre.
- **Result:** all three read problems "Water" until piped, then Active with "" within ~30 s. Electricity 0 for 7 polls over 3 min. Direct power-line joins between the main, turbine and NE line components were blocked by growables every time; conduction through buildings already bridges them. Union-find shows the south advanced turbines are not on a line reaching the main grid.
- **Rule:** incinerator power drops for a while when one is replaced (the new ones need garbage delivered), so add surplus before any power-producing removal. Nuclear/solar need water as well as power: pipe every new plant in the same step. In zsh a `for b in "..."; set -- $b` loop sent from=(0,0) to connect (dry run only, caught); use Python (third confirmation of the zsh rule).
- **Correction (18:43):** the 140-building shortage had already fallen to Electricity 1 by 18:33, before any new plant — most likely the two replacement incinerators ramping up as garbage arrived (unconfirmed). Surplus (Nuclear 27435, Solar 7690, 19956) was added anyway; Electricity 0 since. Rule stands: add generation before removing a power producer, and re-read problems a few minutes after a swap before reacting.

### 2026-09-27 Portville L2: a metro tunnel in the water column works; the first build failed until rebuilt in place
- **Situation:** River gate (Phase 3d). L2 WF -> WBE, 42 Metro Track pieces at elevation -12, which over water is 12 m below the water surface (y 90-96) while the riverbed is y ~22, so 18 pieces run through open water.
- **Action:** built paused, measured live (max bend 32.0, max grade 27.8%, all inner nodes degree 2), 2-stop test line WF -> WBE, speed 3 for 90 s. It failed; with no middle station to bisect, rebuilt all 42 pieces in place (bulldoze keepNodes + build-network between the same nodes, paused), deleted the line and created a fresh one.
- **Result:** first line 144: LineNotConnected on both stops, vehicles 1 -> 0. After the rebuild, line 48: Complete, no problems, 1/1 vehicle in 6/6 samples, length 2,545.5 m. Rebuild took under a minute real time; every piece reused both nodes. The middle-shift scan was no help: working M1 curve-to-straight pieces show 3.4-4.25 m shifts, the same as the flagged L2 pieces.
- **Rule:** under-river metro at -12 is fine; do not re-route for water. A fresh bridge-built metro leg can fail invisibly; when a test line reads LineNotConnected and no middle station exists, rebuild the whole leg in place (keepNodes, paused) and create a NEW line before calling the route bad. Third confirmation of the rebuild-in-place rule (B16, W1, L2).

### 2026-09-27 Portville L2: a station with no power, water or road still dispatches a metro test train
- **Situation:** WF 3276 (RoadNotConnected) and WBE 41930 (no power, water or road; Active false, MajorProblem within a game month) on the L2 test line.
- **Result:** the line held 1/1 vehicles for 8 game days; the train spawned from WF.
- **Rule:** a 2-stop metro gate test does not need the stations served. For real service, give each station road, power and water before opening the line (lesson 2026-09-26: an unserved station shuts down).

### 2026-09-27 scripts: `from mlib import *` overwrites your HERE
- **Situation:** tmp/portville/p3metro/lib.py set HERE, then did `from mlib import *`; mlib exports its own HERE (tmp/tampa/metro).
- **Result:** stations.json and l2-result.json were written into tmp/tampa/metro/ (moved back; the TAmpa folder had no files of those names).
- **Rule:** set HERE and any output paths AFTER star imports, or import mlib by name.

### 2026-09-27 Portville 3a: water facilities connect only through their own pipe node
- **Situation:** new WTP and two Water Outlets at the west site, joined to a 2.8 km pipe trunk with `/commands/connect` toService Water/Electricity from each building centre.
- **Action:** connect without `roadPrefab`; then bulldozed the stubs it made; then piped to the centres; then read WaterFacilityAI.ProduceGoods (monodis IL).
- **Result:** connect built **Basic Roads** (its default prefab) to Water nodes 22.7-32 m from each centre, and the Electricity connects reused those road nodes. Those Water nodes were the buildings' own `m_netNode`; bulldozing the stubs released them, and pipes ending at the centres left all three WaterNotConnected for 2+ game days. The IL confirms a Water facility is connected only if `m_netNode` has a segment. After bulldozing and re-placing the three buildings, pipes that ended on the new m_netNodes (found with a connect dryRun probe, createdNodeIds [] at the end) made them Active in under a game week.
- **Rule:** always pass `roadPrefab` to connect (Water Pipe / Power Line). For a Water facility, run the pipe to its m_netNode (probe `connect` dryRun from the building position, maxDistance 45, before any other pipe is near). Never bulldoze a segment ending on a building's own node with keepNodes false.

### 2026-09-27 Portville 3b: dry-run lots again after the roads they front are built
- **Situation:** §10.8 lots were validated clean before Waterfront Avenue/Drive and the promenade existed.
- **Result:** once built, 10thAnniversary Park collided with Avenue segments 2419/18503 (moved 30 m along the Drive), and Promenade 9 crossed the Expensive Plaza just placed (road_guard caught it; rerouted). The Metro Entrance 3276, 24 m off the Drive centre, read RoadAccessFailed for ~2 min after the Drive was built, then cleared by itself (FindRoadAccess: a lane within 20 m of the point 4 m in front of the front edge).
- **Rule:** build roads first, re-run the validated dry run on every lot, place lots, then guard paths against the placed footprints. Give a new road ~2-3 game days before acting on RoadAccessFailed.

### 2026-09-27 Portville 3b finish: City Quay is walkable, faces right, and must be raised above the water peaks
- **Situation:** player asked for public quays on the downtown waterfront. The bridge lists `City Quay`/`Quay` (QuayAI) without lanes; build-network dry runs say nothing about sides or collisions.
- **Action:** built one City Quay piece W->E at terrain height with water on its right; probed terrain (Castle Ruins 03 validated dry run = bare terrain y) across it before and after; later a Pedestrian Pavement dead end 8 m inland of the quay centreline.
- **Result:** the deck flattened from 32 m inland to 4 m water-side of the centreline, face at +6..+8 m on the RIGHT of the build direction; land side graded 30+ m inland. At terrain height the deck (117.5) sat below measured water peaks (shore-snap waterHeight 111-123 on that stretch, swinging several metres within a minute), so it was removed and rebuilt with `elevation` 5 (deck 121-124, face ~6-8 m). Each path stub's end node read endNodeLaneSegmentId = a quay segment: City Quay has a pedestrian lane and path dead ends lane-link to it like a sidewalk.
- **Rule:** build quays with the water on the right of the build direction, at an elevation that clears the water's peaks (sample waterHeight several times), keep pylons/paths/buildings >= 12 m inland of the centreline (the grading band is ~30 m), and connect them with path dead ends 8 m inland (verify endNodeLaneSegmentId).

### 2026-09-27 Portville waterfront: the river surface is ~10 m higher than the plan measured
- **Situation:** §10.8 (18:1x) measured the NE-bank water at ~99-107 and put the promenade 40 m inland of the y-112 contour.
- **Result:** at 19:3x, Water Outlet shore snaps along x -560..0 read waterHeight 111-123 (e.g. (-393,538) 118.5; (-306,480) 118.3; one reading 123.5 at (-400,540)); the plan's contour now lies under water there, the y-118 shoreline is 25-35 m inland of it and the promenade (y ~124) is ~1-6 m above the peaks. West of x -600 the water is still 90-100. Readings at one point swing 3-8 m within a minute.
- **Rule:** re-measure water heights (several samples) before any shore-side build; never reuse a shore contour from an earlier session. The cause of the rise is unknown (TODOS).


### 2026-09-27 19:53 — build-network piece shorter than snapDistance collapses onto one node (Portville footbridge)
- **Situation:** footbridge landing FB0 was 8 m long (178.5→170.5); default snapDistance is 8 m.
- **Result:** `Both endpoints snapped to the same node (18705)` — a node that did not exist before (the start's own new node). Nothing built.
- **Rule:** for pieces ≤ 8 m, pass `snapDistance` well below the piece length (2 m worked); chained pieces still join because each starts exactly at the previous end.

### 2026-09-27 Water 2: a water tower with no pipe shows no problem at all; dry far-end districts clear with a local source
- **Situation:** 48 Water (not WaterNotConnected) buildings in the NE north, 17-49 m from pipes on the single pipe network. Tower 25918 there read Active false with problems "" and productionRate 100. Both intakes were ~3.5 km of pipe away.
- **Action:** probed the tower's own node (connect dryRun maxDistance 45 -> node 10641 at the centre, no segment) and piped to it from the nearest pipe node, 62 m away (seg 19005). Added 2 upstream intakes (44588, 4730) on their own nodes, each with a local Wind Turbine and a short Power Line. Then piped 3 coverage gaps of 97-118 m along Large Roads.
- **Result:** Water 48 -> 4 within ~1 game month, then 2 (both unserved reserved metro entrances). WaterNotConnected stayed 0. The tower read Active within ~1 min of the pipe. The run does not show which change carried the drop.
- **Rule:** a Water facility with Active false and no problem flags may simply be unpiped. Probe its own node before anything else. "Water" with WaterNotConnected 0 on a pipe-covered district far from the sources means add a source near it or pipe its idle tower, then add intakes for surplus. After that, whatever stays dry is >~95 m from a pipe, so treat it as a coverage gap.

### 2026-09-27 Portville 3e: a bus ended at a metro station instead of the trunk terminal moves its riders onto the metro
- **Situation:** M1 (Central - Civic - West Gate, 1 train each way) carried ~45 boardings/wk; B3 (164, 10 buses) ran from D2 to the Central forecourt, 1 km express along the M1 corridor.
- **Action:** removed B3's Central stop and moved its first stop to Edward St NB, 29 m from the West Gate entrance (the only trunk contact); budget 150 -> 250 to hold 10 buses on the shorter 2,237 m loop. 4 periods before vs 4 after, skipping the edit period (tmp/portville/p3e/).
- **Result:** M1 45.5 -> 162.5/wk (3.6x; M1W from Central 19.5 -> 105). B3 198 -> 118 (-40%). B1 -17%, WF1 -30%, B2 -11%; the six lines together -6.5% while citizens grew 5.7%. M1W waits at Central reached 73-121 with one train.
- **Rule:** cutting a bus's direct run to the trunk terminal and ending it at a metro station raises metro boardings several-fold within one game month, but count trips, not boardings: a transfer is two boardings, and total boardings across the network can still fall. Judge a feeder change on the sum over all lines, and add metro trains when the transfer stop's waits exceed a train load.

### 2026-09-28 Portville traffic T1: grade-separating two mainline level crossings
- **Situation:** the two sustained hotspots (Campbell 5304/7274 ~95, Dexter 3144/30198 ~92-98 over 9 samples) sat on road/rail shared nodes 26873 and 3590. City flow 76 %.
- **Action:** replaced each at-grade link with an elevated road (`Medium Road Elevated` 6 pieces, `Basic Road Elevated` 7 pieces) between the nearest ground nodes far enough away for <= ~8 % at >= 8 m clearance, pausing the sim for the bulldoze + chain swap. Heights set as absolute targets minus a terrain probe; clearance checked per track at closest approach.
- **Result:** both hotspots gone (overpasses 5-17), neighbours dropped (Campbell 20689 58 -> 14, Harris Ave 63 -> 14, Dexter Ave 51 -> 7), city flow 76 -> 85 %. Rail stayed one component; both train lines and all 8 other lines Complete without re-adding stops. Dexter needed the ramp to start at the next junction (32050, 150 m back) because 18221 gave 8.5 % and 7 m. Vehicles used every new piece within ~1 min.
- **Rule:** a level crossing is fixed by moving the ramp ends back to the next junction, not by squeezing a bridge between the nodes beside the track: 8 m at 8 % needs ~100 m each side. Measure ground-to-track height first (Campbell's west end was 3.6 m above the track). A transport line whose path only used the removed road still needs a check, but train lines are unaffected when only road segments leave a crossing node.

### 2026-09-28 lane probe does not work on elevated roads
- **Situation:** post-build lane probe (tmp/portville/hwy/probe.py, a bus-stop dry run 3 m off each new segment) on the new overpasses.
- **Result:** all 13 "bad": TransportTool's GetStopPosition rejects them (no stop-capable lane). Density > 0 on every piece a minute later.
- **Rule:** verify elevated pieces by /state/traffic density > 0 (or a line path through them), not the bus-stop probe.

### 2026-09-28 bulldozing a node's segments deletes the node: do not expect its id afterwards
- **Situation:** the Dexter design expected the chain to pass through 18221, a 2-segment bend node, both of whose segments were bulldozed first.
- **Result:** caught before the real run: keepNodes false releases an orphaned node, so the new chain gets a fresh id there (2507).
- **Rule:** only put expect-node checks on nodes that keep at least one other segment after the bulldoze.

### 2026-09-27 Portville Harris T2: an empty side road was the highway jam
- **Situation:** Harris Highway 24469 read density 93 at load (save from Traffic T1), with 11006 at 73 and 5369 at 74. Node 22202 joined those highway pieces to Large Road Greenaway 34006. Greenaway's own density was 3. The through-highway bend, taking 35647 reversed, is 2.7°. Shops fronted 34006.
- **Action:** paused, bulldozed 34006 with keepNodes true (22202 kept 24469 and 35647; 18677 kept 7138), then built Large Road 23151 along the old line for 40 m, ending 43 m short of the highway. No transit stops were on 34006. Ran the sim at speed 3.
- **Result:** 24469 was 38 after ~5 game days and 10 after ~8 more (16 at the save, game 2035-02-04). 11006 73 -> 10 -> 16. 5369 74 -> 8. 35647 stayed 14-26. City flow 78 -> 83. Road anomalies from this edit: 0. Shops 35707 and 47012 (26 m and 33 m off the old centre line) were Active right after the build and gone after 5 game days. A CommercialLow repaint changed 0 cells.
- **Rule:** a highway node that also holds a large road gets a junction even when the side road is empty. If the side road's density is ~0, disconnect it and stop the replacement short of the highway; do not build an overpass. Check growables that fronted the removed segment after a few game days, not only immediately.

### 2026-09-27 Portville harbor: the straight line to the nearest road is the lake
- **Situation:** Harbor 26710 (-2238,536) sat on one Harbor Road segment, its own road component. The nearest other road was Cooper Avenue, 1.2 km northeast. A chord that way dropped to terrain y 89, the lake.
- **Action:** sampled an 80 m terrain grid. Land at y 117-157 runs east along z 470 to x -1150, then the lake (y ~89) lies between z 520 and z 940. Built Medium Road on that ridge from harbor node 30278, then Medium Road Bridge at deck y 128 down to 122, then one Medium Road into Waterfront Drive node 12092.
- **Result:** createdNodeIds was empty on the drive join and on the promenade path join. Harbor node to the drive is 21 hops. Bridge and approach both have 2 pedestrian lanes. Bus H1 (line 102) came back Complete with 7/9 vehicles and no problems, length 5,093 m. Nine ground pieces still read roadTerrainCliff; the bridge pieces do not. The first ridge piece grades 19.8%.
- **Rule:** before connecting a west-shore building, grid-sample terrain. y near 89 between the harbor and the drive is water. Stay on the z 470 ridge, bridge x -1150 from z 470 to z 960, and land on the drive. Do not aim the chord at Cooper Avenue.
