# Lessons

## Proven Rules
(Confirmed twice or more. Follow these first.)

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

