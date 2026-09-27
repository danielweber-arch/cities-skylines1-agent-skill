# Lessons

## Proven Rules
(Confirmed twice or more. Follow these first.)

- **Metro/rail track: every node turn <= 40 deg, platform ends straight.** The pathfinder refuses a lane across a node turn of about 45.8 deg (m_maxTurnAngle 45, XZ only). Build legs as Dubins arcs R >= 50 m in 25-60 m pieces with 30 m straight in line with each platform, and measure with tmp/tampa/metro/bends.py before creating lines. Confirmed: B15 M1 (max 33.9, 4/4 each way), B16 M2 (max 30.4) and M3 (max 31.3), 2/2 each way.
- **One metro line per direction, each station once.** Two stops at one Metro Entrance resolve to the same point and never path. Copy lines 7/227: one line A -> Z and one Z -> A. Confirmed: B15 (7, 227), B16 (135/207, 147/162).

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
