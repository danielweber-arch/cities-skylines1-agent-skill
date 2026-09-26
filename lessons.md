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
