# TAmpa transit progress
currentStep: diagnosis
safetySave: TAmpa-transit-before
createdEntities:
  lines: [142 (Bus 13 West CBD Monuments & Airport, 16 stops, 5.3 km), 4 (Bus 11 Laurel North, 20 stops, 11.0 km), 13 (Bus 12 Laurel South, 15 stops, 7.5 km), 97 (Bus 9 Station Link, 8 stops, 3.9 km), 5 (Bus 10 Westside Industrial, 12 stops, 4.3 km)]
  deletedOwn: [5 (Bus 10 Westside Industrial: 62 then 61 riders/week, under the 75 rule), 96 (Harbor02 terminal loop: LineNotConnected on the legs into/out of the terminal), 31 (Bus 8 Laurel Crosstown, 33 stops: split into 4 + 13 per the user's 20-stop cap), 197 (Harbor Shuttle: stop 2 snapped 103 m onto segment 0, LineNotConnected; deleted)]
  buildings: []
  segments: [27754 (road Harbor02 link A, 26691->22834), 8478 (road Harbor02 link B, 3920->24803), 18194, 25181, 4049 (power line 5429->47366)]
  nodes: [7616, 1414, 10814, 10066]
  track: [15916 (C2a 16673->10066), 26053 (C2b 10066->20865), 35757 (E6 17506->3488)]
batches:
  - B1: Harbor02 terminal linked to Greenaway Street; station 47366 powered. Result: disconnectedLocalRoadComponents 1 -> 0; 47366 active; Cargo Center 17763 active. Harbor02 still Garbage problem.
  - B2: new bus lines 31, 97, 5 created (all complete, no problems). Harbor Shuttle 197 created and deleted (bad snap). Names/colours not set: SetLineName/SetLineColor throw on the main thread; fix built (AddAction) but needs a game restart.
  - B3: per-line budgets (tuning only, no route/stop change): 246 100->250, 179 100->200, 94 140->175, 60 100->50. Evidence: 246 814 waiting on 6 buses; 179 1,081 waiting on 11; 94 362 waiting; 60 6.5 riders/bus.
  - B3 applied (the first attempt sent empty budgets: zsh word-splitting bug in my loop). Verified: 246 at 250 (15 buses), 179 at 200 (22), 94 at 175 (39), 60 at 50 (6).
  - B4: user rules (in-game, msg 16): max 20 stops per line; lines under 75 riders removed or redesigned. Laurel Crosstown split into Bus 11 (20 stops) and Bus 12 (15 stops) sharing the train-station stops; original 31 deleted; line-id check before/after showed only 31 removed.
  - B5: Bus 13 West CBD Monuments & Airport (142) created, complete, no problems. Harbor02 terminal loop (96) failed pathing (LineNotConnected on legs 4->0 and 0->1): Harbor02 is not reachable by bus both ways (only via one-way highway pieces). Deleted; line ids before/after checked. Bus 4 (60) kept at 50% budget: it is the only line within 300 m of Grand Mall.
  - B6 rail: station 31333 linked to the mainline eastbound (Train Track C2a, C2b, E6; node reuse verified: 16673, 20865, 17506, 3488). Pre-check: no crossings with any road/track (only an intercity Train Line path). Expected: intercity trains 31333 <-> E connection. Bridge CanReuseNode widened to same service+subService+layer (built; needs game restart) so the 25 m 47366 -> mainline link (R3) can join platform node 31280.
  - B7 (check 2): removed Bus 10; Bus 2 budget 250 -> 300.

## 2026-09-26 session 3 (save: TAmpa b2)
- B8 rail: segment 7884 Train Track 31280 -> 2791 (47366 north platform to mainline), both nodes reused, createdNodeIds [].
- B8 names: lines 4 Laurel North, 13 Laurel South, 97 Station Link, 142 West CBD Monuments & Airport (SetLineName via AddAction confirmed working).
- B8 rail line: Train Line 69 "Tampa Rail North-South" stops 31333 (node 29102) <-> 47366 (node 29101). After 3 game days: LineNotConnected, 0/2 trains. Open.
- Player goal (chat 4): 10x rail riders (0 today) and 10x harbor (29 -> 290+).
- B9 rail fix (plan tmp/tampa/rail-plan.json ranks 1 and 3; rank 2 fillets skipped until measured need):
  - Cause (planner, measured turn angles): 31333 (via E6 35757 at 3488) and 47366 (via R3 7884 at 2791) both lead only EAST on the mainline; no reversal possible, so Line 69 had no path.
  - Rank 1: S-curve 19014 -> 20865, segments 6335 30988 31807 34047 31810 25291 33470 36510 17793 21432 716 17456 19935 24266; new nodes 9774 9114 5984 30777 4184 27466 29328 14442 6106 16911 5471 31731 10341. Bulldozed 15916, 26053 (ours) and 19115 (existing 48 m 115-degree stub; verified 19014->16673->10066->20865 before removal).
  - Rank 3: chord 29059 -> 4101, segments 21224 9838 8876 3746 12356 30405 28076 12763 14482 12380 12845 15561 31962 | 34119 35242 32821 9093 3015 1831; gap between nodes 13277 and 30510 across Dixon Street 18077 left for the player (bridge cannot make level crossings).
  - Saved TAmpa b2 17:32:42 (verified mtime).
- B10 harbor (plan tmp/tampa/harbor-plan.json):
  - R1 Laurel Link: Medium Road seg 32938, 14315 -> 25532 (both reused). roadTerrainCliff 26.9 m (cosmetic).
  - R2 Harbor02 Access: segs 20155, 35650, 16737, 11927 (Medium Road Bridge over Canal3), 21290; 9445 -> 17258; new nodes 23085 22226 11922 25519. No segment problems; cliff anomalies 17-28 m.
  - R3 Bay Ferry: Ship line 224, Harbor 23322 <-> Harbor02 31587, 3/3 ships running after ~3 game days.
  - Harbor02 still "Garbage, MajorProblem" right after build; re-check after 2 game weeks.
  - Saved TAmpa b2 17:36:58.
