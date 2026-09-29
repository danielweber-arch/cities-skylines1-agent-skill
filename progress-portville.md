# Portville progress

Working save: `Portville` (only this one is overwritten). Builder logs: tmp/portville/p1/actions.jsonl.

## 2026-09-27 13:49 P1 start: STOPPED at step 1 (highway attach), nothing built

Baseline read before any command [M]:
- /health ok; game 2027-02-12, speed 1, not paused; nodes 1,781, segments 1,778.
- Demand R 100 / C 0 / W 0. Facilities 0, growables 0, zones 0, transit lines 0; Bus/Train/Metro locked.
- /state/problems total 0 (the survey's 4 RoadNotConnected are gone).
- /state/external-connections: roadComponents 1 (survey: 2), localRoadComponents 0, cityConnectedToOutside true.
- road-anomalies(includeDeadEnds=false) total 0 (baseline for P1).
- Save `Portville` lastWrite 18:31:39Z (13:31 local), not re-saved (nothing changed).

Finding: 78 new "Highway with Bus Only Lane *" segments since the 13:27 survey. The north stubs 9971/22760
are now mid-chain nodes of the Holmes (SB, west, x~63) / Underhill (NB, east, x~96) pair, which runs
south through (63,769)/(96,780) and over the river on Holmes/Underhill Bridge (z 276..-313) into the old
south stubs 8924/17500 and on to the south interchange (6810 / 20791). The only degree-1 road nodes
are the 8 map-edge Highway Connections. There is no free highway or ramp end to attach to, and the
plan's merge point (90,790) now lies on Underhill itself.

Dry runs only (not built): HighwayRamp (73,581)->(200,790) ok, y 134.0->143.8; HighwayRamp
(200,790)->(106,972) ok, y 143.8->158.2. The bridge's dry run reports neither the snapped node nor the
ramp's travel direction, so it cannot confirm a proper merge.

Waiting on a player/orchestrator decision for the attach (see the report and TODOS.md).

## 2026-09-27 13:52-14:07 P1 (non-attach part): roads, power, water built; STOPPED on orchestrator order

Baseline 13:52 [M]: road-anomalies(includeDeadEnds=false) 0; problems 0; 997 road segments; roadComponents 1.
Scripts: tmp/portville/p1/ (plib.py, spine.py, probe.py, touchall.py, pipes.py, pumppower.py); every id in actions.jsonl.

1. Station Avenue (Medium Road, 15 x 80 m, (200,790) -> (1400,790)), each piece reused the previous end node:
   segs 22899 22867 33565 25503 2145 10008 13070 36105 11599 12759 30883 17315 36084 14136 32804.
   Nodes x200 19942 (free west end, left for the interchange), 280 720, 360 3384, 440 20966, 520 24490, 600 25598,
   680 32386, 760 12315, 840 21314, 920 12192, 1000 16748, 1080 17285, 1160 2593, 1240 2189, 1320 4426, 1400 28975 (plaza end).
   Grades: max 8.8 % (360 -> 440), y 143.8 at x 200 -> 179.2 at x 760 -> 176.8 at x 1400.
2. Collector x 520 (Basic Road, 4 x 80 m, z 790 -> 1110) from spine node 24490: segs 28892 12778 13232 13646; nodes 13395 32380 29158 20528 (north end, dead end).
3. D1 grid `portville-d1-grid-01` (Basic, origin (600,630), 5x4, 80 m, snap 8): 24 nodes created, 6 reused (all six spine
   nodes x 600..1000), 44 segments, 5 skipped (spine duplicates). Segs and nodes in tmp/portville/p1/d1_grid.json.
   road-anomalies stayed 0.
4. Lane probe: NOT POSSIBLE. transit-line-create dryRun returns HTTP 500 "Bus is not unlocked yet" (all 63 new segments).
   Instead, touch.py's node touch was applied to ALL 44 new road nodes (Basic Road Elevated 14 m stub, bulldozed at once):
   44/44 stubs started on the intended node, 44/44 removed, road segments back to 997+63 = 1060, anomalies 0.
   Lanes are therefore repaired preventively but not verified; re-probe once Bus unlocks.
5. Power: Wind Turbine 10913 (760,1200) and 24210 (840,1200), angle 0, clean guard. Power Line 34692 (800,1200)->(800,1081),
   1600 (800,1081)->(800,962); nodes 8927, 20335, 324 (pole 324 at (800,962), 12 m north of row z 950, off the D1 blocks).
6. Water: Water Intake 1428 at (1127.8,-622.5) angle -45.1 (validated shore snap from (1131.1,-619.1)); Water Outlet 8035 at
   (-553.9,610.0) angle -31.3 (from (-558.2,608.2)). 80 Water Pipe pieces (tmp/portville/p1/pipe_ids.json): 49 under every D1
   road (lattice x 600..1000, z 630..950), 16 intake -> (1000,630), 10 outlet -> (200,790) (crosses under the highway),
   5 under the spine (200..600,790). One pipe component. Note: the player later branched a pipe at (1016.7,466.8), which split
   my 28366/19061 into 33770 + 15665 via node 1691 (trunk intact).
   Pumps need power, not in the plan: Wind Turbine 29962 (1172.8,-544.6) + Power Line 36096 (1142.6,-597.0)->(1151.8,-581.0)
   for the intake; Wind Turbine 6243 (-478.9,610.0) + Power Line 19464 (-534.9,610.0)->(-515.4,610.0) for the outlet
   (local turbines so no pole line crosses the interchange area). Intake then showed no problems.

Acceptance at 14:06 (game 2027-07-09) [M]: road-anomalies 0; building-anomalies 0; my roads are one component
(external-connections: roadComponents 2 = highway + city, cityConnectedToOutside false until the attach).
Problems: 8035 WaterNotConnected (outlet; pipe node 30962 sits on its centre, same as the intake which cleared; suspected to
need sewage flow, unverified), 324 ElectricityNotConnected (dead-end pole, no buildings yet), plus NOT mine: 31187 Nuclear Power
Plant (2250,643) and 45983 Solar Power Plant (2317,562) Water MajorProblem, pole 28749.
Foreign (player-built during this step, not in my logs): the two plants above, 17 Water Pipe segments x 1013..2307 z 435..635,
~30 Power Line segments incl. a line through (1076,675)..(1579,761), i.e. across the reserved station strip x 1400-1560,
and lines at z ~557-580 along D1's south edge and west to the highway (x 108..556).

Saves (Portville.crp): request 13:53:24 -> mtime 13:53:25 (spine); ~13:56:02 -> 13:56:04 (collector+grid+touch);
14:06:39 -> 14:06:42 (power+water). Sim speed was 3 during the build; read back at speed 1 at 14:06 (changed outside this builder).

Not started (orchestrator stop order 14:05): zoning, services, D2, any highway/ramp work, pipe/power on collector x 520 and on
the spine east of x 1000.

## 2026-09-27 14:07 Interchange (IC): design only, nothing built, STOPPED on orchestrator order

Orchestrator stop (transport-first master plan first) arrived during design. No build-network,
bulldoze or other write command was sent by the interchange builder; the highway is untouched.
Verified 14:06 [M]: road-anomalies(includeDeadEnds=false) 0; external-connections roadComponents 2,
localRoadComponents 2, cityConnectedToOutside false (the D1 component, 64 segments, is still
unattached); problems 5 (Water/Electricity on the other builder's buildings, none road).
Save `Portville` written 19:06:50Z (request 19:06:48Z), captures the other builders' work.
Snapshot: tmp/portville/ic/net0.json.

Live facts for the design [M]:
- Station Avenue west end already exists: node 19942 (200, y143.8, 790), seg 22899 Medium Road
  "Pearl Boulevard" 19942 -> 720 (280,790).
- Underhill (NB, east) nodes: 22687 (62,485) 13256 (73,581) 15617 (84,676) 26081 (96,780)
  22760 (107,875) 10012 (106,972) 6104 (106,1069). Holmes (SB, west): 25046 (30,474) 6484 (41,570)
  23588 (51,665) 24181 (63,769) 9971 (74,864) 14564 (74,964) 24949 (74,1063). Segments ~96-104 m,
  at grade (Barrier), y 134-170. Centre lines only ~32 m apart.
- Underhill includes two two-way "Large Road with Tree Median and Bus Lane" segments
  (28139 21726->16451, 12351 16451->22687, z 287..485) — logged in TODOS.md.

Bridge behaviour (read in src/, game code decompiled with ilspycmd) [code]:
- build-network never splits a segment. Each endpoint snaps to the nearest road node within
  snapDistance (default 8, max 64, height ignored) or creates a free node. An endpoint mid-segment
  makes an unjoined node (roadCrossingWithoutNode / dead end). Ramps must end on existing highway
  nodes (list above) or on nodes created by an explicit re-lay of the highway.
- Direction: NodeHelper.CreateSegment calls NetManager.CreateSegment(..., invert:false). Lane
  direction = NetInfo m_finalDirection (inverted when the save is left-hand traffic) XOR the
  segment Invert flag; NetTool sets invert in LHT so drag direction is kept. Bridge one-ways
  therefore run start -> end in right-hand traffic and end -> start in LHT. All 4 map-edge road
  connection pairs read keep-right (e.g. north edge 25708 west = Outgoing i.e. lanes into the map,
  19197 east = Incoming), so Portville is RHT and a HighwayRamp should run start -> end. NOT yet
  verified on a built ramp: no endpoint exposes segment Invert or node OneWay flags, and the city
  has 0 zoned cells / 0 growables, so no traffic would use a ramp yet. Stored start/end of
  player-built segments is not a direction signal (Albert node 23039: two segments end there).
- RoadBaseAI.UpdateNode flags RoadNotConnected on a non-outside node whose car lanes are all in
  or all out; use it to check that a multi-piece ramp chain is consistently oriented.

Proposed design (for the planner; not built):
- Why not a direct left-side flyover from Holmes: with the carriageways ~32 m apart, a ramp
  leaving Holmes at <= 20 deg is over Underhill within ~50 m, where it can have risen only ~4 m;
  >= 10 m clearance needs ~145 m at 8 %.
- A local collector (Medium Road, all west of x 200) along x ~180 from z ~620 to ~980, joined
  to 19942, gives the ramps their landing and descent length.
- NB off: right diverge at Underhill 13256 (73,581), HighwayRamp at grade, <= 15 deg, curve NE to
  the collector south of 790 (dry run earlier: (73,581)->(200,790) y 134.0 -> 143.8).
- NB on: collector north of 790 -> right merge at Underhill 10012 (106,972) (dry run earlier:
  y 143.8 -> 158.2 over ~210 m, ~6.9 %).
- SB off: right diverge at Holmes 24949 (74,1063), HighwayRampElevated climbing west of Holmes to
  ~+12 m by z ~900, left curve over Holmes and Underhill at z ~840-870 (nodes outside both
  carriageways, >= 10 m clearance), descend east of Underhill onto the collector's north end.
- SB on: collector south end -> climb westward, cross Underhill and Holmes at z ~700, curve left to
  heading south west of Holmes, descend, right merge at Holmes 25046 (30,474).
- Build order when resumed: NB off first; check RoadNotConnected on its middle nodes and, once
  the city has residents, density on it; then the rest. Alternative needing highway changes:
  lift Underhill onto Elevated over z ~700-900 so Holmes ramps pass under at grade (heavier).

## 2026-09-27 14:16-14:19 Interchange (IC) BUILT: half-diamond on Underhill + at-grade SB crossover

Live state at build time differed from the 14:07 design [M]: Underhill (NB, east) is no longer a highway.
From z -1287 to 1263 it is now "Large Oneway Decoration Trees" / "Large Oneway Bridge" (game-renamed
"Stephen Harris Avenue" during this build); Holmes (SB, west) is "Large Oneway" north of node 14564 (z 964)
and over the river (z -117..276), and still "Highway with Bus Only Lane" on z 276..964 and south of -117.
39 road segments added / 36 removed vs net0.json (not by this builder; who changed it is unknown). The
design node ids 13256, 10012, 24949, 25046 all still exist. 0 RoadNotConnected anywhere before building,
so the new one-way chains are consistently oriented with the highway ends they join.
Scripts: tmp/portville/ic/lib.py, buildchain.py (chain builder, logs actions.jsonl), check.py, try.py, traffic.py.

Built (every call in tmp/portville/ic/actions.jsonl; y = node height):
1. Collector south (Medium Road): 8986 (168,136.1,630) -seg 1271-> 17764 (178,138.1,705) -seg 20486-> 19942 (200,143.8,790).
   Grades 2.7 %, 6.5 %. 8986 is a dead end (landing spot kept for a future SB on-ramp flyover).
2. NB off-ramp (HighwayRamp, start -> end): 13256 (73,134.4,581) -20835-> 11640 (100,136.1,650) -14880-> 5632 (130,136.8,684)
   -15018-> 17764 (T into the collector). Diverge ~15 deg right of Underhill (heading 21.4 vs 6.6), turns 20/25 deg,
   grades 2.9/1.4/2.5 %. Kept >= 23 m from power poles 40854 (132,625) and 34053 (139,713).
3. Collector north (Medium Road): 19942 -11012-> 18508 (188,149.7,865) -16340-> 31116 (185,160.6,960) -31900-> 29000 (182,173.3,1060).
   Grades 7.8 / 11.4 / 12.8 % (terrain: Underhill beside it is 12-14 %; nothing at grade west of x 200 can be flatter).
4. NB on-ramp (HighwayRamp): 18508 -24996-> 20511 (150,151.9,872; elevation 3.5) -27853-> 23218 (126,154.5,900; elevation 4.5)
   -27688-> 10012 (106,159.1,972). Merge ~15.5 deg from the right; grades 5.7 / 7.0 / 6.1 % (nodes raised to keep <= 8 %;
   a dry run after building still reads terrain 148.4 / 150.0 under them, so the embankment is not in the terrain sample:
   the ramp may visibly float up to ~4.5 m, unverified by eye).
5. SB access, changed from the design: the flyovers are replaced by an at-grade two-way crossover (Medium Road) on the
   existing surface nodes of the two one-way avenues: Holmes 24949 (74,170.0,1063) -7258-> Underhill 6104 (106,172.6,1069)
   -31467-> collector 29000. SB traffic turns left off Holmes at 24949; city traffic turns left onto Holmes at 24949.
   Why: both nodes are now plain Large Oneway junctions (junctions are native there), it needs 2 pieces instead of ~10
   floating elevated pieces without pillars, and the flyover landing zone (z 840-900, x 106-190) is where the NB on-ramp
   sits. Cost: two signalised junctions on the through avenues. The z ~700 SB on-ramp flyover was not built.

Checks after each step [M]: /state/problems never showed RoadNotConnected (9 -> 9 -> 6 rows, all Electricity/Sewage on
service buildings and 3 power nodes); road-anomalies(includeDeadEnds=false) 0 after every step; after the NB off-ramp and
since: /state/external-connections roadComponents 1, localRoadComponents 1, cityConnectedToOutside true (was 2 / 2 / false).
Direction: every ramp runs start -> end (bridge, RHT). Consistency is verified (no RoadNotConnected on middle nodes 11640, 5632,
20511, 23218). Absolute direction (Underhill really NB) is inferred from the map-edge keep-right flags and the chain having
no RoadNotConnected, not from vehicles.
Traffic: growables 0 (no zoned homes). At 14:20 ramps and collector density 0; through traffic on Underhill 15-23, Holmes 17.
Saves `Portville.crp`: request 19:16:53Z -> mtime 19:16:56Z (off-ramp); 19:17:40Z -> 19:17:42Z (on-ramp);
19:19:12Z -> 19:19:14Z (crossover). Sim left at speed 1 (not changed).
Removed ids: none.

## 2026-09-27 14:26-14:58 P1 growth builder (P1g): power, bus lanes, D2 access + landfill, clinic, cemetery, D2 industry

Scripts and ids: tmp/portville/p1g/ (actions.jsonl has every call). Followed portville-master-plan.md P1 + §8 amendments.
Before [M, 14:26, game 2027-11-03]: 6 problems (3 service buildings Electricity: Elementary 48888 (888,826),
Fire_Station_EU 13071 (1332,754), police_station_EU 12158 (504,758), all placed by another hand, NOT on the plan lots;
3 pole ends ElectricityNotConnected). Nuclear 31187 / Solar 45983 no longer had a Water problem (fixed before this step).
The power network was 5 separate components; the long player line A ((105,290)..(1266,731)) had no plant on it.

1. Power: A joined to the nuclear line via 32623 -> (1375,721) -> 16859 (around the fire station), police spur
   10713 -> (500,735), school spur 324 -> (880,962) -> (880,900) -> (880,856), D1 south feeder z 618 (x 640..960, from 2262),
   north feeder 324 -> (740,962), spur 324 -> (784,937), and A joined to the turbine/school grid 725 -> (1015,770) -> (960,850)
   -> (880,856) (segs 9338, 29421, 13871). One grid now: nuclear + solar + turbines. Services powered within a game week.
   At 2028-07-26 29 buildings had Electricity (capacity, after D1 + D2 grew): 4 Wind Turbines 17143 (904,1200), 35546 (904,1264),
   38226 (904,1328), 35139 (736,1280) + links 31656, 26781 -> 0 Electricity problems within ~3 game days. x > 960 is OutOfArea (NE tile).
2. Policies FreeTransport + EducationBoost set city-wide (cityPolicies confirms both; the call itself returned ok:false
   "Already in the same thread" - TODOS).
3. Promenade West bus lanes: Station Ave segs 2145, 10008, 13070, 36105, 11599, 12759 (x 520..1000) -> 34368, 5539, 31321, 9684,
   15124, 3720, and (§8.3) x 200..520 segs 22899, 22867, 33565, 25503 -> 869, 10096, 5431, 14331, all "Small 4 Lane Road with Bus
   Lanes", bulldoze keepNodes + rebuild on the same nodes, createdNodeIds [] every time; 11 nodes touched. Anomalies 0, 1 component.
4. D2 access (§8.2): Basic Road 29000 (182,1060) -> (200,1110) -> x 200 north -> z 1350 east -> x 520 south to collector end 20528
   (segs 30399, 33740, 1503, 18030, 7321, 13875, 19346, 18365, 5322, 35484, 5553). Landfill Site 33150 at (440,1391) a 180 facing
   z 1350; power 8927 -> (800,1320) -> (800,1440) -> (470,1440). Pipes: collector x 520 z 790..1350 + (440,1350), IC collector
   (8986..19942..29000..(200,1110)).
5. Zoning: RL chunk (720,910) radius 40 (118 cells, 8 blocks; radius 34 matched 0 blocks on this grid). Its 4 houses were not
   powered (pole 20 m away) and went Abandoned; I bulldozed the 4 abandoned houses (3312, 17650, 35677, 38405) and repainted;
   the regrown ones are powered. **Between ~14:38 and ~14:43 another hand zoned all of D1 RL (including the Promenade frontage
   cells z 750/830 and the plan's service lots) plus CL east of x 1000 and in D3 (x 1240..1400) and industry in the E tile.**
   I did not zone any z 750/830 cell. The D1 RL queue is therefore empty; no background stager was left.
6. D2 grid (W 61-68): build-grid opId portville-d2-grid-01 (28 nodes, 17 created, 11 reused, 35 new segs, 10 skipped = my access
   road), 17 nodes touched, 41 pipes under every D2 road. Industrial cells (240,1230), (320,1230), (400,1230) (128+148+144 cells
   counted, the plan's max 3). Power 17296 -> (500,1400) -> (500,1310) -> (400,1230) -> (320,1230) -> (240,1230).
7. Services: medicalclinicEU 22108 at (545,894) a 90 facing collector x 520 (plan lot (880,670) was built over); placed at est.
   482 residents (homes from growables x CalculateHomeCount recollection x 2.5, [E]). Cemetery 16132 at (479,1006) a 270 facing
   x 520 after a Death problem, power 1920 -> (500,1180) -> (500,1060). Parks, Bus Depot and B1 wait (Bus.unlocked false all
   session). The depot lot (660,982) is now built over (houses 6660, 24656); clean alternative: (624,1077) a 0, facing D2 row z 1110.
Ramp traffic [M /state/traffic, 2028-08-25]: NB off 20835/14880/15018 density 6/5/3, NB on 27688 53 (24996, 27853 read 10 on
2028-08-14), crossover 7258 12, 31467 7. Every ramp and the crossover carry traffic: direction confirmed by vehicles.
After [M, game 2028-08-25]: 143 RL, 38 CL, 28 I growables (~274 homes est.); demand R 4 / C 20 / W 12; problems 8: 5 Water on
foreign E-tile industry (x 2080..2390, z 350..440, being built by another hand), 3 pole ends. road/building anomalies 0,
localRoadComponents 1, cityConnectedToOutside true. Sim speed found at 1 twice (changed outside this builder), reset to 3.
Saves Portville.crp: 19:30:24Z -> 19:30:25Z; 19:46 -> 19:46:15Z; 19:55:37Z -> 19:55:39Z; final below.

## 2026-09-27 15:00-15:21 Rail builder: Central Station, Riverside East, Cargo Train Terminal + siding, T1

Scripts, ids and every call: tmp/portville/rail/ (actions.jsonl; reserved.json holds the footprints/corridors for other builders).
Before [M, 15:00, game 2028-09-28]: Train/Bus/Metro unlocked false; 0 transit lines; mainline one Train Track chain (208 segs).
Plan: portville-master-plan.md §3.2 + §8.1. Power seg 34420 named in the brief no longer existed.

1. **Power reroute (Central strip).** New Power Line 16859 -26523-> 28650 (1510,640) -36371-> 8553, then bulldozed 7230, 19485
   (node 14971 released). Station spur 28650 -9333-> 2558 (1512,740) inside the lot (Central had Electricity until then).
2. **Central Station 41442** `Train Station` at (1514.25,764.15) a 96.96, y 175.8 (validated; only host segs collided). Host segs
   15554, 16776 bulldozed (node 22783 released). Platform `Train Station Track` 14772: 27496 (1530.9,693.6) -> 2536 (1513.5,836.6),
   144.0 m, 0.2 m off the old chord. Links: 9331 (2536 -> 20344), 16599 (27496 -> 17129), 14.8 m each, createdNodeIds [].
   Road: `Basic Road` "Central Station Approach" 11367 28975 (1400,790) -> 22934 (1479,785); forecourt street 18556 (22934 -> 4101
   (1487,716)) and 25831 (22934 -> 409 (1474,830)), 40.5 m west of the track (0.7 m clear of the lot front). The plan's forecourt loop
   to Station Road x 1400 at z 700/860 can join 4101/409 later. Water Pipe 31546 (601 -> 26360) + 30712 (26360 -> 4109).
   Intercity lines appeared automatically at Central (Train Line segs 849/9823 to the E edge, 14711/19108 to the S edge).
3. **Riverside East 39763** `Train Station` (1729.85,80.76) a 293.09 (front faces ENE, toward E1), y 181.9. Host segs 9402, 19528
   bulldozed (node 19435 released). Platform 4420: 26387 (1694.3,143.9) -> 9891 (1750.7,11.4). Links 12382 (26387 -> 27475),
   27254 (9891 -> 16107), 26.4 m, createdNodeIds [] (a JOIN-FAIL line in actions.jsonl for 12382 is a script bug; the join was correct).
   Water Pipe 33078 (28515 -> 4181 (1745,60)); power via the cargo line below.
4. **Cargo Train Terminal 42520** (prefab `Cargo Center`, display "Cargo Train Terminal", 16x8) at (1642.79,440.56) a 286.75, y 175.9,
   east of the mainline between switches 12674 and 23254. (Trial placement 45989 at (1660,455) was used to read the track geometry,
   then bulldozed: `Train Cargo Track` runs 24 m behind the pivot, 176 m long, one two-way segment.) Cargo track 3153:
   28232 (1594.4,517.9) -> 5033 (1645.2,349.4), 22 m east of the 12674-23254 chord. Through siding (both ends on the mainline, so
   cargo trains stop off the main track and never use a passenger platform):
   N: lead 36656 (28232 -> 474 (1585.7,547.1)), switch leg 4552 (474 -> 12674); S: lead 3294 (5033 -> 30263 (1653.9,320.2)),
   switch leg 15744 (30263 -> 23254). Leads 30.4 m in line with the cargo track.
   Road: `Medium Road` "Cargo Terminal Road" from Robin Street node 15360 (1962.5,727.7): 3871, 34415, 28098, 36191, 12299
   (-> 5613 (1668.3,510.9)), Terminal Street 34752 (5613 -> 20850 (1702.9,396.0)) along the lot front (44.7 m from the pivot).
   Robin Avenue's west frontage (x 1950..2019, z 400..612) is now factories (16093, 27173, ... grown since the survey), so the planned
   join at 27113 would have demolished two of the player's factories; the road joins Robin Street instead and trucks reach Robin Ave
   (16434) and the highway ramps in ~70 m. Power: 2240 -33645-> 9570 -25581-> 8239 (pole in the lot).
5. **Riverside East road:** `Medium Road` "Riverside East Station Road" 9216, 14702, 15782 (20850 -> 29385 -> 9643 -> 13902
   (1728.4,167.1)), forecourt `Basic Road` 35952 (13902 -> 11996) and 32290 (11996 -> 26915 (1777.4,52.1)), 40.5 m from the track.
   RE power: 8239 -12965-> 31565 (1700,330) -23850-> 6828 -27001-> 27673 (pole in the RE lot).
6. **Nuclear link restored** (not in the brief, needed for the stations): Power Line 3131 (18196 -> 25824) had been removed by another
   hand while Robin Street was built, and 42 D1/D2 buildings showed Electricity. Built 25824 -31183-> 17840 (2090,662)
   -25975-> 31846 (2210,648) inside Nuclear 31187's lot; Electricity problems 42 -> 0. (TODOS.)
7. **T1:** Train unlocked between 15:07 and 15:19 (flag read true at 15:19; cause not identified). Line 11 "T1 Central - Riverside East",
   #C8102E, stops Central 41442 (node 28167) and RE 39763 (node 28166), snaps 0.02 m via station. After ~30 s: Complete, 1/1 train,
   no problems (no LineNotConnected), length 1,378.7 m (= 2 x 689 m, no detour). Only line id change: none -> {11}.

Bends [M, chord method, tmp/portville/rail/rbends.py]: Central links 0.8/0.7 deg at the platform ends, 4.4 (20344) and 4.3 (17129);
RE 0.4/0.4 at the platform ends, 1.7 (27475), 1.6 (16107); siding switch 12674 27.0 (4552 vs 5484), 23254 25.0 (15744 vs 12998),
intermediate 474 and 30263 20.8, cargo track ends 0.0. Max 27.0 <= 40.
Checks after the last build [M, 15:19-15:20]: stations 41442, 39763 and cargo 42520 flags "Created, Completed, Active", problems none;
/state/problems total 4 (none on rail); road-anomalies (includeDeadEnds=false) 0; roadComponents 1, localRoadComponents 1,
cityConnectedToOutside true; all rail (incl. both Train Connection Tracks) one component of 230 segments.
Not verified: cargo trains actually using the siding (none seen yet; needs industry cargo), T1 riders (0 so far), and whether trains
reverse cleanly at a 2-stop out-and-back line over time.
Saves Portville.crp: 20:07:47Z -> 20:07:50Z (Central); 20:19:13Z -> 20:19:15Z (RE + cargo + roads); 20:20:23Z -> 20:20:25Z (T1).
Removed ids: segs 7230, 19485 (power), 15554, 16776, 9402, 19528 (host track), trial building 45989. Nothing committed.

## 2026-09-27 15:1x-15:3x Highway / frontage / industrial builder (hwy): IC East, East Works, Dallas corridor + IC North Gate, pad zoning

Player: "Have highways and retail pads beside it like in Dallas TX. Use the industrial space I have built as industrial and
build/connect off of that." Design: tmp/portville/hwy/plan.md. Every call in tmp/portville/hwy/actions.jsonl; geometry
generators design_east.py / design_north.py; pre-build checker check.py (ownership, rail >= 40 m, rail reserved.json buffers,
building footprints, unexpected node snaps, same-height road crossings). NOTE: build-network's dryRun only samples terrain
(src/RoadCommands.cs) - it checks nothing else, so check.py is the real gate.
Live read [M]: the real highways in owned land are Albert (EB; SB in the E tile) / Hunter (WB; NB in the E tile) along
z 1471-1518 in the N tile (x 240..960) and x 2346..2899 in the E tile; direction from the N-interchange ramps (RHT) and confirmed
below by vehicles on the new ramps. Player industry: Robin Avenue had Albert SB in (ramp 22202 -> 16434) and Albert SB out
(19808 -> 32209) but no Hunter access.

1. **Water for the player's industry (orchestrator add-on):** Water Pipe under Robin Avenue 21260 -> 22823 -> 27069 -> 596 -> 4929
   -> 10432 -> 6788 -> 21518 (segs 31540, 21170, 10846, 32613, 6088, 25326, 34270) and 12270 -> 24970 -> 14103 (18551, 8404); then
   under the new Sterling-Robin link (built by another hand) 27069 -> 8956 -> 6274 -> 32689 -> 12254 (4537, 28094, 7876, 15031).
   Water problems on the 11 factories (x 2019-2386, z 340-440) 11 -> 0 within ~1 game month; 6612/46448 (new, on the link) had
   Water after it appeared and were piped in the second pass.
2. **IC East (truck route, both directions of the highway, no core):** `Large Road with Median Elevated` overpass from Robin E end
   19808 due east along z 474: 20659, 25133, 22820, 19501, 15886 (nodes 4623, 4999, 13744, 27593 -> J_E 3291 (2701,474));
   elevations 3.9/6.6/10.1/5.9, clearance 8.3 m over Albert 10408 and 8.6 m over Hunter 7784 [M terrain + node y].
   East Works Road (`Medium Road`, two-way, ~100 m east of Hunter): 22202, 35189, 19806, 20438, 30816 (S 2485 (2773,340) ->
   6869 -> 3291 -> 16474 -> 6620 -> N 23037 (2562,731)). ER2 (`Basic Road`, 90 m further east): 7036, 30776, 12032, 12139, 18086
   (13178 -> 13077 -> 17128 -> 24200 -> 9472 -> 5379). Cross streets (Basic) 19998, 14511, 31711, 36048, 8904, 2878.
   Hunter NB off-ramp (`HighwayRamp`, start -> end) 8418 -> 13333 -> 3049 -> 844 -> 2485: 15828, 26043, 19582, 8408.
   Hunter NB on-ramp 23037 -> 16016 -> 25731 -> 802: 35733, 36039, 5917.
   Pipes 28224, 1633, 25533, 5529 (from Robin pipe node 14103), ER 8011, 15785, 19044, 13942, 33801, ER2 11742, 18614, 11158,
   20227, 24288, cross 3429, 28752, 21268, 1833, 16635, 35619. Power: feed from Nuclear lot pole 31846 -> 8335 (2300,765) ->
   15667 (2470,770) -> 7295 (17695, 19798, 23673) and the ER/ER2 gap line 7295 -> 11927 -> 30644 -> 8127 -> 23773 -> 9791
   (23640, 35629, 3131, 13065, 14953). The gap end pole 9791 (2814,362) shows ElectricityNotConnected until industry grows there.
   **East Works is NOT zoned**: W demand 0-6 (< 30) and Industrial 967 vs Residential 4,608 cells (21 %, at the cap). Ready
   capacity ~800 Industrial cells (4 strips x ~450 m); zone when W >= 30 and residential allows.
3. **Pole line removed** (sat on the EB frontage alignment): power segs 23995, 30443, 21158, 31556, 2927 (poles (470/560/680/800,
   1440)). Landfill 33150 and D2 kept power (no Electricity problem after; 36178 (500,1400) -> (500,1310) remains).
4. **North corridor, Dallas pattern (x 280..950) + IC North Gate (§8.3):**
   - X1 = IC North Gate, Texas diamond with elevated cross street at x 520: D2 node 2434 -> 32413 -> J_S 1751 (520,1436,+8) ->
     4289 (+9.5, over the median) -> J_N 27048 (520,1560,+7) -> 20492 -> 9335 (520,1640): 9921, 7891, 2731, 19524, 24944
     (`Medium Road Elevated`), 12313 (`Medium Road`). Clearance >= 8.0 m over Albert 11697 and Hunter 4503.
   - EB frontage (`Small 3 Lane 1 Way Road`, elevated variant where it climbs to J_S), z 1436, x 280 -> 840: 7125, 19857, 5291,
     12836, 24580, 6653, 15531, 28286, 20966, 29728, 11630, 28011.
   - East U-turn (Texas turnaround, EB -> WB over both carriageways, +4..9.5, clearance >= 8.3 m): 25560, 15919, 16015, 971,
     17852, 15313 (3142 -> 31743 -> 7783 -> 6214 -> 5251 -> 30900 -> 27532).
   - WB frontage z 1560, x 840 -> 360: 35399, 9767, 20909, 2327, 28842, 25668, 607, 20490, 27917.
   - Slip ramps (`HighwayRamp`, X pattern around X1): EB exit Albert 19201 -> 6236 -> 1604 (28646, 32422); EB entrance 13114 ->
     24839 -> 13323 -> Albert 8293 (2693, 6930, 19100); WB exit Hunter 3118 -> 30147 -> 1228 -> 32244 (4661, 16061, 3736);
     WB entrance 22248 -> 12380 -> 21697 -> Hunter 3108 (17572, 28491, 16244).
   - Pad collectors: D2 z 1350 row extended 26084 -> 20022 -> 15996 (391, 24811); south cross streets x 280/360/680/760/840
     (14646, 22345, 17526, 36282, 1749); back collector z 1640 (`Medium Road`) 420 -> 8002 -> 9335 -> 32116 -> 1953 -> 23203 ->
     14814 (24472, 13049, 11852, 14098, 13240, 9421); north cross streets x 360/680/760/840 (34623, 6909, 34220, 24769).
   - Pipes under all of it (33 segs, tie-ins at the existing z 1350 pipe nodes 883, 16164, 12426, 23678): EB 14937, 27699, 18772,
     7114, 34202, 2306, 19195; WB 6782, 36491, 857, 12973, 32250, 30960; z 1640 4056, 3745, 10707, 13914, 625, 26198; z 1350 ext
     3948, 8942; X1 24529, 31710, 28258; cross 14999, 34287, 2347, 55, 769, 7535, 34080, 1265, 18397.
   - U-turns at X1 itself go J_S -> J_N (signalised); a separate U-turn bridge beside X1 cannot get 5 m clearance because the
     frontage roads are still climbing there. North end of the corridor stops at x 950 (NE tile unowned).
5. **Retail pads zoned** (C demand 36 >= 30 at the time): `CommercialLow` (high density not unlocked), preserveOccupied, radius-12
   paints along z 1394 for x 276..400 and 640..840, plus the two x 320 corner blocks. Zone blocks here are 4 cells wide with their
   centre 32 m from the road edge, so the pad strip (facing the EB frontage) and the strip facing the z 1350 collector share a
   centre line and were painted together (Dallas: pads + the collector-facing strip behind). Cells: CommercialLow 899 -> 1,385
   (+486); Residential 4,608, so C/R = 30 % (gate: one C per 3-4 R). Landfill 33150 blocks x 400-480 (skipped as occupied).
   North pads (north of the WB frontage) are NOT zoned: no homes north of the highway yet.

Checks [M]: after every group road-anomalies(includeDeadEnds=false) 0; roadComponents 1, localRoadComponents 1,
cityConnectedToOutside true; the only RoadNotConnected seen were the two open frontage ends (x 280, x 360) between building the
frontages and their cross streets, gone after. Lane probe NOT possible (Bus still locked: transit-line-create dryRun -> HTTP 500);
lanes shown instead by vehicles [M /state/traffic, game 2029-03-20, ~1.5 game days after build]: IC East on-ramp 35733/36039/5917
density 17/12/15, overpass 12/12/8/9/16, ER 9/5/10, X1 7/7/10/10, WB exit 4661/3736 5/1, WB frontage by X1 4. The Hunter off-ramp,
EB frontage, EB ramps, the U-turn and the WB entrance read 0 so far (see the later check below).
Saves Portville.crp: 20:21:07Z -> 20:21:08Z (IC East); 20:23:02Z -> 20:23:04Z (north corridor + pipes); 20:26:04Z -> 20:26:05Z (pads).
Removed ids: power segs 23995, 30443, 21158, 31556, 2927. Nothing committed.
Later check [M, game 2029-03-28, ~9 game days after build]: 12 CommercialLow shops grown on the south pads (6542, 6775, 28687,
28950, 29177, 30461, 31534, 32430, 32444, 33030, 38174, 41916), no problems on any (power by conduction from the landfill /
turbines / D2 - no pole line was needed in the pads). Traffic density: IC East on-ramp 8/10/10, overpass 25/9/7/11/18, EB frontage
up to 19, EB entrance ramp 19100 5, WB exit 12/8/14, WB frontage up to 12, X1 3-10. Still 0: Hunter NB off-ramp (E-edge imports),
EB exit ramp, east U-turn, WB entrance ramp (no homes north of the highway yet). C demand fell 36 -> 8 as the pads filled.
Problems in the work area: 9791 pole end (expected, above); factory 36351 (2370,500) Garbage -> Abandoned and 45561 burned down
(not caused by this work; TODOS).

## Land (orchestrator, 2026-09-27 15:3x)
Planned restart 15:29 (save 15:29:06 -> 15:29:07) to load the new /state/areas + /commands/unlock-area endpoints; they work in-game. Before: 7 tiles owned [(2,1),(3,1),(0,2),(1,2),(2,2),(3,2),(2,3)], max 9, last milestone Metropolis reached, cash reads Int64.MaxValue (unlimited money). Bought NE (3,3) and NW (1,3) within the cap, then SW (1,1) with ignoreMaxAreaCount (cap raised to 25). Owned now 10: the central 3x3 plus (0,2). Next candidates (all purchasable): (4,2) E-far, (3,0)/(2,0) S-far, (2,4) N-far, (0,1)/(0,3) W-far, (4,1).

## 2026-09-27 15:31-15:50 P2 builder: Ring South/East, D1 south + west, NR north half, utilities, services, zoning stager

Scripts, ids and every call: tmp/portville/p2/ (actions.jsonl; lib.py, check.py, plan1.py guard, build1.py, grids.py,
touchnew.py, pipes.py, power.py, spur.py, services.py, stager.py). Game restarted by the orchestrator at 15:29 before any P2
command; everything below is after the restart.
Before [M, 15:31, game 2029-04-09]: demand R 100 / C 0 / W 0; problems 10 (Fire on house 29446 (576,690) and on burned 45561
(2079,397); Garbage on Central Station 41442 and on abandoned 36351 (2370,500); NoEducatedWorkers x4 (CL shops on Station Ave
x 1024..1236 and (221,810)); 2 pole ends ElectricityNotConnected); road anomalies 0; zone anomalies 166; building anomalies 1;
roadComponents 1 / local 1; T1 line 11 1/1 train, no problems; Bus locked (transit-line-create dryRun: "Bus is not unlocked yet").

Live survey changed the plan's grids [M, plan1.py road_guard against live buildings]:
- D1 south as 5x2 (to z 630) would cut 10 houses: the z 630 row's south-side houses (z 590..622) span every column line and the
  z 630 segment 760->840 is gone (houses 24323/28307/44816/46820 grew in the gap). Built 5x1 (z 470..550) instead; the z 550 row's
  north blocks back onto those houses.
- D1 west as 3x6 (to z 950) would hit highschool_EU 29873 (460,886), Child Health Center 3902 (432,762), police 12158 (504,758),
  Wind Turbine 41640 (297,681) and the pole line along z 715-720 (all placed by another hand). Built 3x2 (z 470..630) instead.
- Ring East's last piece crossed shop 46014 (1400,7xx) in the reserved Central strip; house 46799 (576,642) sat on the
  (520,630)->(600,630) link line. Both bulldozed (full authority).

1. **Ring South** (Medium Road, "Ring South", 14 x 80 m, (280,470) -> (1400,470)): segs 32091 35805 25259 32690 34914 13624 31879
   10779 11882 3151 28966 23593 20493 30076; nodes 2490 6281 8612 9173 7498 3332 31287 9716 14412 2439 5459 7550 29004 15541 15262.
   **Ring East / Station Road** (Medium, (1400,470) -> 28975 (1400,790)): 27002 15572 2842 35773 (last piece reused 28975,
   createdNodeIds []); nodes 21668 1334 26302. Not in the orchestrator's list; built because the plan's P2 steps 1-2 make it the
   shared row of the three grids and the only second way out of NR/D1 south (Medium per §3.3).
2. **Grids** (Basic Road, spacing 80, snap 8): `portville-p2-d1s-01` origin (600,470) 5x1: 11 segs, 6 nodes created, 6 ring nodes
   reused, 5 ring segs skipped. `portville-p2-d1w-01` (280,470) 3x2: 14 segs, 8 created, 4 reused. `portville-p2-nr-01` (360,310)
   13x2: 54 segs, 28 created, 14 reused, 13 skipped. Ids in tmp/portville/p2/grid_*_real.json. Links (Basic): 17574
   1320 (520,630) -> 18113 (600,630) and 11294 IC-collector dead end 8986 (168,630) -> 476 (280,630), createdNodeIds [] both.
   road-anomalies 0 after every grid. Lane probe impossible (Bus locked): all 63 nodes of the 99 new segments touched (touch.py),
   63/63 stubs on the right node and removed, road segment count unchanged (1,351). Lanes preventively repaired, NOT verified.
3. **Pipes**: 99 Water Pipe segments, one under every new road (tmp/portville/p2/pipes1.json); one pipe component (397 segs).
4. **Power**: NR feed 30594 (572,555) -> (560,510) -> (560,430) -> (560,350) -> z 350 east to (1360,350) and west to (400,350)
   (poles on block back lines/cell corners), D1 west feed 27070 (540,610) -> (480,590) -> (400,590) -> (320,590); spurs to
   clinic, schools, parks (23169, 5131, 9142, 1148, 36579). Segments in power_ids.json + actions.jsonl. After the spurs, 0
   Electricity problems on every new building.
5. **Services** (validated, clean guard, road-facing, placed on empty grids before zoning; ids in services_ids.json):
   firehouse_EU 42958 (400,525) a0, 41812 (960,415) a180, 32749 D2 (575,1230) a270; Elementary_School_EU 454 (480,419) a180,
   42832 (1120,419) a180; medicalclinicEU 34191 (815,430) a270; police_station_EU 20282 (975,526) a270, 48186 (246,605) a0 (for the
   Crime shops on the IC strip); Combustion Plant 46375 (1360,285) a0 (Central Station/fire station Garbage); Regular Park 25727
   (239,526), 1385 (944,269); Regular Playground 17930 (640,419), 38276 (1200,419), 22425 (651,510), 36732 (400,281).
   Playground 2241 first landed at (1029,526) in the Culture Quarter (plan's highschool CQ-W lot) and was bulldozed/moved.
   Existing high school 29873 and Library 27492 (another hand) cover NoEducatedWorkers over time; no new one placed.
   Dead buildings bulldozed: 45561 (burned, E tile), 36351 (abandoned, Garbage, E tile), 46253 (1036,770) and later 8590, 21969,
   33024, 48609 (abandoned CL shops on Station Ave x 1000..1400, NoEducatedWorkers).
6. **Zoning stager** (stager.py, nohup, pid in stager.pid, stop: `touch tmp/portville/p2/STOP_STAGER`): one lattice cell (radius 40,
   8 blocks) per district every 3 game days while R >= 70, every 7 while 40 <= R < 70, hold below 40; CL on Ring South frontage only
   while C >= 30 and W >= 10 (1 per 3 R chunks); no industry/office. Cells within 400 m of Central/Riverside East are never queued
   ((1360,430) only). preserveOccupied false unless a non-residential growable is inside the cell (poles and my services would make
   preserve skip every block). Saves Portville after each painted round. Queues: D1S 10 cells (x 640..960, z 590/510), D1W 6
   (x 320..480, z 590/510), NR 25 (x 400..1360, z 350/430). Painted by 15:45: 15 chunks, ~1,800 cells RL (changedCells), e.g. R 99
   -> 26 after the first 6 chunks (gate held), then paced at R 40-45.
7. **Bus**: still locked (dryRun 15:33: "Bus is not unlocked yet (UnlockManager.Unlocked(m_UnlockMilestone) is false)"). No depot
   or line built. Depot site (624,1077) a0 re-validated clean at 15:4x. /state/areas reports the area milestone "Megalopolis"
   reached while Bus reads locked (TODOS).
8. **Mall test (dry run only)**: `Pedestrian Pavement` (1000,790) -> (1080,790) dry run ok:true (y 175.3 -> 174.8), but the dry run
   returns no node ids. Code [src/NodeHelper.cs CanReuseNode, lines ~347-369]: a node is reused only if the prefab is identical,
   both are Road service, or service+subService+layer match. Pavement is Beautification/BeautificationParks, the road node is
   Road, so the bridge would create a NEW node on top of 16748/28975 instead of joining: a dead-end path. **The bridge cannot join
   a path to a road node today.** Promenade/Mall not built (as ordered); TODOS.

Checks [M, 15:45, game 2029-08-20]: road anomalies 0; roadComponents 1, localRoadComponents 1, cityConnectedToOutside true;
building anomalies 1 (unchanged); zone anomalies 197 (new entries in the P2 box are patchyZoneCluster from half-painted
80 m clusters mid-staging, no mixedZoneBlock); T1 1/1, no problems. Growables in the new districts: D1S 8, D1W 25, NR 27, with no
Electricity/Water/Sewage problems. Problems 10 -> 9: Fire 0 (was 2), Garbage 0 (was 2); left: Crime 4 on the IC-strip shops
(x 116..135, z 709..843; police 48186 is ~200 m away but Underhill is one-way NB with no entry between the bridge and z 1063 -
TODOS), NoEducatedWorkers 3 (2 more abandoned shops on Station Ave x 1124/1288), 2 foreign pole ends.
Demand R 18 / C 6 / W 0 at 15:45 (stager holding).
Saves Portville.crp: 15:33:54 -> 15:33:56 (roads); 15:38:10 (services); 15:40:31 (stager rc 1: overlapped my manual save 15:41:13);
15:42:13, 15:45:12 (stager); 15:43:33 (cleanup). Nothing committed.

## 2026-09-27 16:28-16:40 P2b Bus: Bus Depot, B1/B2/B3, path-join test

Scripts and every call: tmp/portville/bus/ (actions.jsonl; lib.py, rt.py router = b27/route2 + p1t/route3 rules with U-turns only at
dead ends and all one-way prefabs excluded, lines.py stop points, dry.py, create.py, save.py, map.py).
Before [M, 16:28, game 2030-08-13]: Bus locked (`Bus Line Requirements` -> `Bus Depot Created` 0/1); lines: T1 (11) only;
road anomalies 0; building anomalies 1 (pole 36061, pre-existing). Stager (p2) had already exited (queues empty).

1. **Bus Depot 18067** at (624,1077) a0, y 182.3 (validated dry run clean: no tool errors, no colliding buildings/segments),
   front on D2 row z 1110. Flags "Created, Completed, Active", no problems (no Electricity/Water) through 16:40.
   `/commands/connect` from the lot front (624,1101) built Basic Road stub 20827 (new node 9125 -> 16123 (600,1110)) running
   alongside row z 1110 (road-anomalies: shortRoadStub + deadEndNearRoad) although the depot was already on the road;
   bulldozed (keepNodes false), anomalies back to 0, node 16123 keeps segs 9941/10073/32362. Road access proven by vehicles:
   the depot dispatched 24 buses. **Bus unlocked** ~20 s after placement (`lineTool` true, milestone 1/1); no ignoreUnlock used.
2. **Lines** (routed with U-turns forbidden, dry-run snaps all on the designed segment, 1.5-4.5 m; line ids diffed around each
   create: {11} -> +135 -> +218 -> +164, nothing removed). Budget 150 each; every stop laneId != 0; no problems after ~30 s.
   All three start/end at the Central forecourt stop (seg 18556, station side, NB after the dead-end turn at node 4101).
   | id | name | stops | length (router est.) | vehicles | stop segments |
   |---|---|---|---|---|---|
   | 135 | B1 Station Avenue Trunk | 9 | 2,005 m (2,218) | 6/6 | 18556, 36084 (x1200 WB), 15335 (x1000 NB), 15367, 9465 (z950 WB), 17031 (x600 SB), 31321, 3720 (Pearl Blvd EB), 36084 (EB) |
   | 218 | B2 Riverside NR - D1 South | 12 | 3,129 m (3,178) | 8/8 | 18556, 2842 (Station Rd SB), Ring South WB 20493, 3151, 31879, 32690, 35805, Anna St (z390) EB 12052, 4759, 1996, 24842, 15572 (Station Rd NB) |
   | 164 | B3 North Gate D2 | 8 | 3,928 m (4,138) | 10/10 | 18556, 13232 (x520 NB), 21688 (z1110 WB), 1503 (x200 NB), 13875, 29834 (z1350 EB), 4546 (x680 SB), 13646 (x520 SB) |
   Stop node ids: B1 21394 21393 21392 32765 21391 32763 21390 21389 32759; B2 9446 32756 32755 9445 32753 32751 32750 9444
   32748 32746 9443 32743; B3 21544 21543 21542 32739 21541 32736 21540 21539.
   District coverage: B1 = D1 core + Station Ave (loop Pearl Blvd EB / x1000 / z950 / x600, the master-plan loop extended to Central);
   B2 = NR + D1 south + D1 west (Ring South WB / z390 EB pair, 80 m apart on one line); B3 = D2 (perimeter loop) + x520 houses.
   Residential growables within 250 m (straight line) of a stop: 449/450 (uncovered: one house (336,766), Station Ave x<520).
   **Deviation from the brief:** D1S, D1W and NR share B2 instead of one feeder each: separate lines there would run 80 m apart
   (rows z 390/470/550/630), which breaks the ~150 m rule. Shared legs: B3 runs express (no stops) on Station Ave x520..1400 beside
   B1, and B1/B2/B3 share the Approach + forecourt into Central (the only two road entries to Central are Station Ave and Station
   Road). **No line ends at Riverside East:** it is east of the mainline and ~4.9 km by road from Central (via the highway ramps and
   Robin Street); a feeder there needs a road across the rail first.
   Riders after ~4 game months [M m_averageCount]: B1 105/wk, B2 59, B3 44 (lastPeriod 156 / 169 / 89).
3. **Path-join test (one only):** `Pedestrian Pavement` (1473.5,865.69) -> (1473.5,829.69), 36 m, ending exactly on Central Forecourt's
   dead-end surface Basic Road node 409 (1473.5,829.69) in the station reserve (guard: no building within 4 m; no other network within 14 m).
   Result: seg 24076, startNodeId 31594, **endNodeId 11553, createdNodeIds [31594, 11553] -> NOT joined, a new node on top of 409.**
   Bulldozed 24076 (keepNodes false, released true); no segment touches 31594/11553 afterwards; 409 keeps 25831; road anomalies 0;
   zone totals unchanged (12,342 zoned cells before and after). Running DLL built 16:24:54 (contains CanJoinPathToRoadNode), game
   started 16:26:31. TODOS updated.
4. Checks [M, 16:40, game 2030-12-11]: road anomalies 0; building anomalies 1 (same pole); 3 bus lines complete, vehicles = target.
Saves Portville.crp: request 21:30:43Z -> mtime 21:30:46Z (depot); 21:37:29Z -> 21:37:31Z (lines); 21:43:39Z -> 21:43:41Z (final, after the path cleanup). Nothing demolished, no zoning touched.
Removed ids (all mine): road seg 20827 (+ node 9125), path seg 24076 (+ nodes 31594, 11553).

## P3 Metro M1
### 2026-09-27 16:45-17:00 M1 first leg (Central -> Civic -> West Gate): PARTIAL, STOPPED at Civic (needs demolition)
Scripts and log: tmp/portville/m1/ (plib.py, place.py, plan.py, legs.py, scan_ave.py; actions.jsonl, stations.json, plan.json, scan_ave.json). Not saved (orchestrator saves).
- Live [M]: the Promenade roads do not exist. East of x 1000 the only frontages are Amanda Holmes St (Station Ave, Medium, z 790), Station Road x 1400, Ward St x 1000, the Central Approach/Forecourt. Station Ave x 1000..1400 is lined with active L2 shops on both sides; Promenade West (x 520..1000) with houses.
- **Built: Central-M1 `Metro Entrance` 39708** at (1438.41,770.55) a 356.4, south side of Central Station Approach (seg 11367), 76 m from Train Station 41442, clean (no collisions). Station track 15176 (Metro Station Track, y 164.25): west node 18560 (1365.8,763.1), east node 7706 (1509.5,754.1), E-W. Milestone "Metro Track Requirements" passed; "Metro Track Created" not yet (no track built), so Metro still reads locked.
- **Civic: no clean lot** within 260 m (scan every 8 m both sides of Station Ave x 208..1400, 298 dry runs, scan_ave.json). Best single-demolition lot: (1208,766) a 0, south side of Amanda Holmes St, collides only with **4544** `L2 4x3 Shop12` (active). Platform would be z 754, x 1136..1280, nearly collinear with Central's (turns 9.7 / 6.0 deg on a 2 x 43 m straight link). Other one-shop lots: (1196,815) a180 -> 27705; (1236,766) a0 -> 11800; (1120,815) a180 -> 7446. No-demolition alternative: build the planned Promenade South stub (z 710) west from Station Road node 26302 and front Civic on it (not done: Promenade roads are held for the orchestrator).
- **West Gate:** plan lot (544,809.5) a 180 (north side of Pearl Blvd) collides only with **45181** `L3 4x4 Detached07`. Clean alternative (376,809.5) a 180, 168 m west (loses the 300 m reach to (820,950)). Not placed.
- Plan [dry-run only, all pieces ok, no collisions]: C-Civ 2 x 43.1 m straight (86 m); Civ-WG plan 10 pieces 524.8 m, max bend 7.9; Civ-WG clean 13 pieces 691.4 m, max bend 5.9 (Dubins R 50, 30 m platform leads). Tunnel y 153.6..167.8 (terrain - 12).
- Lines M1E/M1W not created (need Civic).
- 16:53 resume after orchestrator demolished 4544 and 45181: **both lots had already regrown** (Civic lot: 45074 `L1 4x3 Shop02a` at (1208,758); West Gate lot: 10374 `L1 4x4 Detached06a` at (544,814), both Active). Validated dry runs collide only with those ids. Nothing placed or built; stopped (no further demolition allowed to this builder).
- 16:5x orchestrator demolished 45074/10374 and placed **Civic 43595** (1208,766) a0, track seg 19801, nodes 2160 (1136,754) / 24556 (1280,754), y 163.875; **West Gate 29599** (544,809.5) a180, seg 18958, nodes 4079 (616,821.5) / 27487 (472,821.5), y 158.188. Ends matched plan.json exactly (no re-plan).
- **Track built** (Metro Track, elev -12, tmp/portville/m1/build.py, build-result.json): Central->Civic segs 13243, 6821 (new node 21702), 18560 -> 24556; Civic->West Gate segs 32883, 18718, 27925, 25672, 10417, 23778, 16318, 18304, 21983, 32968 (new nodes 26797, 11352, 13739, 23762, 8356, 2255, 25120, 4663, 4591), 2160 -> 4079. Live bends (bends_live.py): max 9.7 (18560), 6.1 (24556), 7.9 (26797, 4591), rest <= 0.5. Track + platforms 1,043 m. Grades <= 5.1% except the last piece into West Gate 32968 = 27.8% (live platform y 158.2 vs tunnel ~165; cosmetic per the 2026-09-26 grade lesson).
- Metro unlocked on its own after the first track piece (both milestones passed); ignoreUnlock not used.
- **Lines** (dry run first: all stops via station, 12 m snaps to the intended building): **173 "M1W Central - West Gate"** and **30 "M1E West Gate - Central"**, #0072CE, budget 100. After ~100 s at speed 3: both Complete, problems none (no LineNotConnected), 3 stops, length 1,642.5 / 1,642.3 m, vehicles 1/1 each, riders 0 yet (2 waiting at Civic seen once). Line ids before: 11, 26, 135, 164, 218 (26 = T1B train, not mine).
- Not saved (orchestrator saves). Next: extend West Gate west end node 27487 (472,821.5) toward Riverside NE (200,330).

## P3 NE
2026-09-27 16:47-17:12, builder (NE bbox x 960..2880, z 960..2880 + T1 track). Scripts and every call: tmp/portville/p3ne/
(actions.jsonl; lib.py, survey.py, station.py, plan.py, roads1.py, grids.py, probe.py, repair.py, pipes.py, power.py,
spur.py, services.py, stager.py, rt.py, feeder.py, save.py, chk.py). Nothing in src/ or mcp-server/ touched; nothing demolished;
no existing zoning changed. Nothing committed.
Before [M, 16:47, game 2031-01-03]: 6,944 citizens, demand R 59 / C 50 / W 38; NE tile (3,3) empty except highways, the mainline,
3 wind turbines at x 904 and D1 houses at x 912..976 z 906..974; T1 line 11 1/1, no problems.

1. **Land.** NE tile (3,3) = x 960..2880, z 960..2880 was already owned (bought by the orchestrator 15:3x). /state/areas 16:47:
   owned 10 [(1,1),(2,1),(3,1),(0,2),(1,2),(2,2),(3,2),(1,3),(2,3),(3,3)], cap 10 after the 16:26 restart, purchasable 0,
   cash Int64.MaxValue. Nothing bought.
2. **North station 518** `Train Station` at (1553.96,1880.79) a 85.92 (front faces west), y 170.5, B11 host pattern. Hosts were
   19053 (5910 -> 29382) + 3581 (29382 -> 9134) (validated dry run: ObjectCollision from those two only); both bulldozed (node 29382
   released). Platform `Train Station Track` 2032: 341 (1556.8,1808.4) -> 22722 (1567.1,1952.0), 144.0 m. Links `Train Track`
   26168 (341 -> 5910) and 15409 (22722 -> 9134), 23.9 m each, createdNodeIds [] both. Bends [M, rbends.py]: 0.0/0.5 at 5910/341,
   0.5/0.1 at 22722/9134, max 3.3 in x 1500..1620, z 1600..2200; rail one component, 231 segments.
   **T1 extended as one line per direction** (a 3-stop loop only runs one way between each pair):
   - **11 "T1A Central > Riverside East > North"** (edit: North added at the end; stop nodes 28167, 28166, 28542).
   - **26 "T1B Central > North > Riverside East"** (new; stop nodes 1590, 32705, 32704; all three stops via station, snaps
     1.99 / 0.04 / 1.98 m).
   Each runs 1/1 train with no problems. Length 3,626 / 3,627 m, which equals Central-RE-North-Central with no detour.
   Line ids: {11} -> {11, 26}; nothing was removed. Train budget stays 100.
3. **Roads.** Collectors are 80 m pieces, dry-run first; every intended join reused its node:
   - **North Central Boulevard** (`Medium Road`, z 1640, from Smithson Street node 14814 (840,1640) to (1480,1640)): segs 17703 29750
     29464 9838 14183 32677 3093 27538; nodes 14814 11406 12165 13965 17441 29936 23832 12974 28702.
   - **North Central Avenue** (`Medium Road`, x 1400):
     - north part: 15581 19566 4378 (12974 -> 22691 -> 21082 -> 14046);
     - south part: 25004 21407 11918 (11527 (1400,1030) -> 24931 -> 19297 -> 22551 (1400,1270)).
   - **Highway overpass** (`Medium Road Elevated`, x 1400, 22551 -> 12454 -> 20009 -> 4727 -> 10017 -> 12974): segs 23230 23120 9649
     25936 15464. Node y is 164.01 / 169.10 / 173.45 / 178.94 / 177.42 / 174.20. Clearance at the carriageway edges is 8.4 m over
     Harris (y 168.36) and 8.5 m over Piper (y 169.89). Steepest ramp is 7.2%.
   - **North Station Road**: 9454 (`Medium Road`, 14046 -> 1143 (1480,1880)) and 36428 (`Basic Road`, 1143 -> 10334).
   - **North Station Forecourt** (`Basic Road`, 40.5 m west of the track, 0.7 m clear of the lot): 28790 31000
     (20073 -> 10334 -> 16450). Forecourt links 30651 (11376 (1480,1800) -> 20073) and 12492 (25319 (1480,1960) -> 16450).
   - **Ward Street link** 5381 (`Basic Road`, 27723 (1000,950) -> 32056 (1000,1030)).
   - **Downtown connections: 2.** One via Ward Street x 1000 -> D1 / Station Ave. One via North Central Blvd -> Smithson ->
     IC North Gate X1 -> D2 / x 520.
   - **Not built:**
     - Station Road link x 1400 z 790..1030: shop 15507 (L2 2x3 Shop15, (1392,822)) sits on it, and the metro builder is
       working at (1438,768).
     - Ring North z 950: shop 23705 (1024,934) blocks x 1000..1080.
   **Grids** (`Basic Road`, spacing 80, snap 8, dry run first):
   | grid | origin | size | segs | new nodes | reused | skipped |
   |---|---|---|---|---|---|---|
   | `portville-p3ne-ncs-a` | (1000,1030) | 5x3 | 35 | 19 | 5 | 3 |
   | `portville-p3ne-ncs-b` | (1000,1270) | 4x2 | 18 | 10 | 5 | 4 |
   | `portville-p3ne-ncw-a` | (1000,1640) | 6x3 | 35 | 17 | 11 | 10 |
   | `portville-p3ne-ncw-b` | (1000,1880) | 6x4 | 52 | 28 | 7 | 6 |
   Segment and node ids are in grid_*_real.json.
   **Lane probe** (probe.py; Bus dry run 4 m beside each segment):
   - ncs 53/53 ok.
   - ncw 82/87 ok. The 5 bad ones were 1109, 1516, 1954, 2147 and 3146 (right segment, snap 2.4-2.6 km, i.e. lanes at the
     origin). touch.py repaired them (nodes 14086 25319 6011 20365 6651 15501 31481); re-probe 5/5 ok.
   - Collectors 19/19 ok; forecourt links 2/2 ok.
   Road anomalies 0 and roadComponents 1 / local 1 / cityConnectedToOutside true after every group.
4. **Pipes:** 166 `Water Pipe` segments, one under every new road including the overpass line (pipes1.json). They are all in the
   one city pipe component (563 segs).
   **Power:** 49 `Power Line` segs (power_ids.json) fed from turbine-line node 31373 (884,1236):
   - back-line poles at z 1070/1230/1390 (south) and z 1680/1840/2000/2160 (north);
   - a spine at x 1040 across the highway;
   - a spur into the station lot (1545,1850).
   **Service spurs:** 30074 24959 13492 9751 9210 15692 10083 27444 33684 19372 34044 10607. The back-line poles alone left 11
   of 12 services with Electricity.
   Station 518 went Electricity/Water/RoadNotConnected -> none.
5. **Services** (validated, clean, road-facing, placed before zoning; services_ids.json):
   - firehouse_EU 36329 (1120,1165), 15112 (1120,1745)
   - police_station_EU 15780 (1280,1135), 39443 (1280,2095)
   - medicalclinicEU 34296 (1200,1325), 6351 (1215,1920)
   - Elementary_School_EU 18144 (1120,1321), 16707 (1120,2069)
   - Tropical Garden 31601 (1280,1303), 35865 (1280,1767); `Regular Park` did not fit between the rows
   - Regular Playground 9803 (971,1086), 31563 (1120,1909)
   - Crematory 24490 (979,1840), added 17:10 after Death appeared on NE house 11584 (the nearest deathcare, Cemetery 16132, is
     about 1.2 km away).
   All 13 read "Completed, Active" with no problems.
6. **Zoning stager** (stager.py, nohup, pid in stager.pid, log stager.log + actions.jsonl, stop: `touch tmp/portville/p3ne/STOP_STAGER`):
   - **Density:** high density is taken as unlocked. /prefabs/buildings reads Milestone1..12 all unlocked, incl. Train Station
     Milestone7, and the area milestone Megalopolis is reached. Every NE cell is within 800 m of North/Central, so residential
     is `ResidentialHigh` (§5).
   - **Queues:**
     - NCW: 36 cells, nearest North first.
     - NCS: 23 cells, nearest (1000,950) first.
     - JOB: 8 cells, all within about 250 m of North, plus (1360,1070)/(1280,1070) about 342/385 m from Central.
   - **Residential gate:** one chunk (radius 40) per district every 3 game days while R >= 70, every 7 while 40 <= R < 70; hold
     below 40.
   - **Jobs gate:** at most one job chunk per 3 residential chunks. `CommercialHigh` while C >= 30, else `Office` while W >= 30.
   - Saves Portville (mtime-checked) after each painted round.
   - **Off-by-one (fixed 17:05):** the first round painted a job chunk after only 2 residential chunks. The gate is now
     (jobs+1)*3 <= residential chunks and the stager was restarted (pid 49812).
   - **State at 17:09:** 8 RH chunks (NCW (1360,2000) (1360,1760) (1440,2080) (1440,1680); NCS (1040,1070) (1120,1070) (1040,1150)
     (1120,1150)) = 1,280 cells, and 2 CH chunks ((1440,1840), (1440,1920)) = 360 cells. Holding at R 31-48.
   - **Zones city-wide 17:10:** RH 1,186 and CH 329 (both new); RL 9,892, CL 1,467, I 967 unchanged.
7. **Feeder N1, line 94 "N1 North Central Feeder"** (#2E8B57, budget 100), 15 stops, starting at the North forecourt (station
   side, seg 31000). Stop segments: 31000 1516 8286 13159 8700 35970 14183 11918 7712 9188 19950 26776 34530 15581 4378.
   - **Routing:** routed first with rt.py (U-turns only at dead ends, one-ways excluded): legs 160-240 m, except the two
     highway-crossing legs (610 / 530 m); 4,029 m. It is 217 m from the nearest other bus line.
   - **Dry run:** every stop on its designed segment, 1.5-6.1 m.
   - **First read (30 s after create):** 7/7 buses, but LineNotConnected on stops 2-3 (z 2200 WB; the lane probe had passed
     there).
   - **Repair:** touched nodes 28430, 25920, 26027 and re-pathed with a remove/re-add of stop 3 (transit-line-edit).
   - **After:** 7/7, no problems, every laneId != 0, length 3,655 m (router 4,029; within the +3-10% upper-bound band).
   - Line ids {11,26,30,135,164,173,218} -> +94; nothing was removed.
Checks [M, 17:10-17:11, game 2031-06-27]:
- 7,434 citizens; demand R 42 / C 50 / W 29.
- Road anomalies 0; roadComponents 1 / local 1 / connected; building anomalies 1 (pre-existing).
- 65 NE growables (H1 53, H2 12).
- NE problems: 3 dead-end poles (ElectricityNotConnected, expected until blocks grow) and Death on 11584 (crematory placed).
- Station 518: no problems, passengerCount 114.
- Lines: T1A 1/1, T1B 1/1, N1 7/7, all with no problems.
- Sim unpaused, speed 3.
Saves Portville.crp (request -> mtime, UTC): 21:52:28 -> 21:52:31 (station + T1); 21:57:18 -> 21:57:20 (collectors + overpass);
22:00:09 -> 22:00:11 (grids, pipes, power); 22:04:09 -> 22:04:11 (services); stager 22:04:43 -> 22:04:45, 22:06:55 -> 22:06:56,
22:09:00 -> 22:09:02; 22:09:52 -> 22:09:54 (N1); 22:11:45 -> 22:11:47 (crematory).
Removed ids (all mine or the host track): track 19053, 3581; touch stubs (10, all removed).
Open:
- The NE east of the rail (x 1600..2880, about 60% of the tile) has no roads. It needs a rail crossing, i.e. an overpass north
  or south of the station.
- No highway ramps at the x 1400 overpass. NE traffic reaches the highway via IC North Gate (x 520, about 900 m west). A Dallas
  half-diamond at x 1400 would fit the player's directive.
- NCS to Central has no short walk (about 1.2 km by road via Ward Street), because the Station Road link is blocked.
- Water capacity is unread (2 intakes, one not Active per TODOS; 1 outlet).

## M1 fix (B1 feeder)
2026-09-27 17:2x-17:44 (local), transit builder. Single-variable experiment ordered by the orchestrator: turn B1 (line 135) into a
West Gate feeder so no bus duplicates an M1 hop. M1 (30, 173), B2 (218), B3 (164), N1 (94) and trains untouched. Scripts and every
call: tmp/portville/m1fix/ (lib.py, rt.py = bus/rt.py on a fresh roads.json, eval.py, edit.py, measure2.py; actions.jsonl,
baseline.json, edit_dry.json, edit_real.json, measure.jsonl, measure_prelim.jsonl). Nothing demolished, no zoning, no src/ or mcp-server/.
- **Baseline** [M, 17:2x, game 2031-09-23] lastPeriod (res+tour): M1E 0, M1W 1, B1 192, B3 141, B2 135 (m_averageCount 0/1/184/134/154).
  Every M1 stop waiting 0. B1 had 9 stops, 2,005 m, 6/6, budget 150; its Central stop had 94 waiting.
- **Design** (rt.py, U-turns only at dead ends, one-ways excluded; eval.py coverage vs 147 residential growables in x 380..1050,
  z 500..1050 that are > 150 m from every B2/B3 stop): 8 stops, router 2,240 m, covers 143/147 of them. Only stop within 150 m of an
  M1 station: West Gate (Ward St SB, 56 m from entrance 29599). Nearest stop to Civic 213 m; nothing on Station Ave / Amanda Holmes
  east of x 1000 (the Birdsong -> Alexander leg uses Pearl Blvd x 920..1000 only). The x 280 Smithson lobe also serves 17 houses
  at x 150..400 and is what keeps the length above 2,000 m.
- **Edit in place** (transit-line-edit on 135, id kept; dry run first, all 8 snaps on the designed segment; 2 snapped 20 m along the
  segment to the lane's stop position): removed old stops 1-7, moved 0 and 1, appended 6, renamed **"B1 West Gate Feeder"**. Line ids
  unchanged before/after. Stops (node, x, z, seg): 21368 (596,830) 17031 Ward SB [West Gate]; 21394 (480,634) 9027 Aspen WB;
  21367 (276,590) 17463 Smithson SB; 21366 (720,626) 10395 Aspen EB; 21392 (880,706) 12923 Crowley EB; 21365 (1004,833) 15335
  Alexander NB; 21364 (880,954) 15367 Ward St WB; 32763 (720,954) 19875 Ward St WB. All laneId != 0.
- **Verify** 50 s after: Complete, no problems, **length 1,941.9 m** (router 2,240, game/router 0.87 like the old B1 0.90), but
  **target 5 at budget 150** (target = ceil(budget x length / 60,000) on all three bus lines). **Deviation:** line budget set to
  **160** to keep 6 buses (brief: keep 6 vehicles); 40 s later 6/6, no problems.
- **Save** Portville.crp: request 17:32:03 -> mtime 17:32:10 local (22:32Z), 7.8 MB.
- **Measure** (speed 3; a period = 7 game days = 50-60 s wall; lines roll within ~2 s of each other). Edit 17:30:11, budget
  17:31:17; the periods ending ~17:31:5x and 17:32:47 are skipped. Periods ending 17:33:47-17:36:15 were read by a rollover probe
  (not in measure.jsonl): M1E 36/12/25/19, M1W 7/3/16/8. measure.jsonl, lastPeriod res+tour:
  | period (end, game) | M1E 30 | M1W 173 | B1 135 | B3 164 | B2 218 | M1E wait WG/Civ/Cen | M1W wait Cen/Civ/WG |
  |---|---|---|---|---|---|---|---|
  | baseline 2031-09-23 | 0 | 1 | 192 | 141 | 135 | 0/0/0 | 0/0/0 |
  | 1 2031-12-15 | 43 | 18 | 141 | 91 | 180 | 7/0/0 | 11/3/0 |
  | 2 2031-12-22 | 7 | 16 | 151 | 150 | 208 | 0/0/0 | 3/1/0 |
  | 3 2031-12-29 | 2 | 17 | 126 | 171 | 196 | 0/0/0 | 0/1/0 |
  | 4 2032-01-05 | 2 | 2 | 117 | 115 | 184 | 0/0/0 | 0/2/0 |
  | 5 2032-01-12 | 3 | 3 | 135 | 88 | 137 | 4/0/0 | 0/1/0 |
  | 6 2032-01-19 | 6 | 7 | 153 | 104 | 178 | 3/0/0 | 1/0/0 |
  | 7 2032-01-26 | 3 | 6 | 163 | 87 | 149 | 0/0/0 | 0/0/0 |
  | 8 2032-02-02 | 8 | 10 | 108 | 121 | 134 | 2/0/0 | 8/2/0 |
  m_averageCount after period 8: M1E 6, M1W 7, B1 141, B3 114, B2 166. B1 6/6 throughout.
- **Read:** M1 went from ~0-1 per week for 7 months to a burst of 15-61 per week in the first ~6 weeks after the edit, then settled at
  4-18 (periods 4-8 mean 10). Removing the duplicate B1 hops moved M1 off zero, so duplication was a real contributor; it is **not
  sufficient** to make M1 carry tens of riders a week. B1 lost ~25% (192 -> 108-163) with the downtown stops gone. Not controlled:
  the zoning stager ran throughout (8,564 citizens at 17:33, growing), and B1's budget went 150 -> 160.

## Access A1 (B2, T1, B4)
2026-09-27 17:3x-18:11 (local), builder. Scripts and every call: tmp/portville/access/ (actions.jsonl; lib.py, rt.py = bus/rt.py on a
fresh roads.json, map.py, stops_route.json, dry_B4.json, create_B4.json, measure.py + measure.jsonl, access_before/after.txt).
Lines 30, 173, 135, 164 not touched. Nothing demolished, no roads built, no zoning, no src/ or mcp-server/ edits. Nothing committed.
Before [M, game 2032-02-01]: 8,548 citizens; B2 218 8/8 at budget 150; T1A 11 and T1B 26 1/1 train each at budget 100.
1. **B2 (218)** budget 150 -> 206: target 11, vehicles 11/11 within ~40 s.
2. **T1A (11), T1B (26)** budget 100 -> 150: target 2, trains 2/2 each.
3. **B4 "B4 East Works", line 21** (#F2A900, budget 84, 6/6 buses, 7 stops, 4,123 m game / 4,073 m router):
   - Station choice: Riverside East 39763 has a road link into the industry, so Central was not needed. Path: Forecourt 32290/35952
     -> Riverside East Station Road -> Cargo Terminal Road (34752 .. 3871) -> node 15360 -> Robin Street 34875 -> Robin Avenue 16434.
   - The industry is Robin Avenue (spine, x 1970-2391) + Sterling Street (x 2019-2441). Sterling Street's only exits are Robin Ave
     node 27113 and Harris Highway at node 6159, so it cannot be driven westbound without the highway; it is covered from Robin Ave
     stops instead (every industrial lot centre <= 204 m from a B4 stop; 5 of 61 lots > 150 m). The turnaround is the
     Oscar White / Price / Hamilton / Young block east of Robin Bridge (x 2701-2816, no growables within 250 m, no stop there).
   - Stops (segment, node, resolved x/z): 0 32290 10548 (1765,69) Riverside East forecourt SB, station side, 45 m from the
     train stop; 1 36352 10547 (1994,541) Robin Ave SB; 2 27958 13117 (2193,397) EB; 3 13798 32764 (2350,448) EB; 4 36359 10546
     (2266,446) WB; 5 9525 32761 (2107,394) WB; 6 20024 32758 (1995,619) NB. Dry-run snaps 4.2-7.5 m, all on the designed
     segment and side; every laneId != 0. Nearest other-line stop to stops 1-6: 493 m. Line ids {11,26,30,94,135,164,173,218} -> +21.
   - Created at budget 100 (7 buses), cut to 84 for 6 (bus target = ceil(budget% x length / 60,000 m) holds: 4123 x 84 = 5.77).
4. **Checks** after 60 s [game 2032-03-07]: all 9 lines complete, no problems, vehicles = target. Save Portville.crp: request
   22:49:21Z -> mtime 22:49:23Z. **17:49-17:54 local the game was restarted by another hand** (Cities --continuelastsave, buildIndex
   14548 -> 14834) and loaded that save: after reload B4 6/6 at 84, B2 11/11 at 206, T1A/T1B 2/2 at 150, no problems. Sim came
   back at speed 1, then 2 (not set by me).
5. **Accessibility** (access.py, lot-cell weighted, straight line; 'after' includes growth to 9,046 citizens):
   | | Jobs <=150 m | <=300 m | <=500 m | Homes <=150 m |
   |---|---|---|---|---|
   | Any stop, before | 45% | 63% | 72% | 95% |
   | Any stop, after | 77% | 99% | 100% | 95% |
   | Bus only, before -> after | 38% -> 69% | 63% -> 99% | 66% -> 100% | 95% -> 95% |
   Rail-only coverage unchanged (jobs within 800 m of train/metro 98%; the East Works lots are still > 800 m from a rail stop
   except via B4).
6. **Measurement** [M, 30 s samples, 17:56-18:11 local, game 2032-03-14 -> 07-07, speed 2; measure.jsonl]. lastPeriod sequence
   (distinct values in order) and m_averageCount (weekly) first -> last:
   - B2 218: 150 217 209 216 169 182 200 211 204 215 163 168 165 190 189 218 219 227; last 3: 218/219/227; avg 147 -> 201.
   - T1A 11: 35 .. 70 197 143 62 40 37 53; last 3: 40/37/53; avg 33 -> 80 (peak 123).
   - T1B 26: 166 .. 156 106 81 117 49 106 23; last 3: 49/106/23; avg 55 -> 92 (peak 120).
   - B4 21: 0 3 7 24 38 16 79 93 41 90 64 55 40 23 19 11 7 17; last 3: 11/7/17; avg 0 -> 26 (peak 55). **Rising then falling**:
     peaked at 90-93 per period ~5-7 weeks after opening, then fell to 7-17. Below a 75-rider floor; cause not investigated.
   - Waiting at B2 (960,386): 14-41 in the first 6 samples, then 0-10. At B2 (1200,386): 96 -> 158 peak (bunched backlog while
     the 3 new buses entered), then 0-14 over the last 16 samples.
   - Waiting at Central (41442) train stops: T1A 1-49 (last 10: 1-30); T1B 0-140 (last 10: 0-76; last 4: 0-4). Still spiky with
     2 trains; not settled.
Removed ids: none.

## Pollution fix (replacement garbage)
2026-09-27 18:20-18:30 (game 2032-10-28 -> 11-03). Scripts, reads and every call: tmp/portville/pollution/ (actions.jsonl).
Rule: no industry or garbage next to residential. Distances are straight-line from building position to the nearest residential
growable (790 homes) and to the nearest residential zone block (block position minus 40 m cell slack; blocks from
/state/zone-anomalies with minMinorityCells=0, see lessons).

1. Garbage prefabs unlocked: Landfill Site (10x8), Combustion Plant (5x4, "Incineration Plant"). The east industry area
   x 1600-2400 z 0-800 is fully built: every frontage dry run there collided with growables (scan.py). The only clean frontage
   >= 330 m from homes and residential blocks is Basic Roads 2521/3275/17855/1021 at z ~830-850, just south of it.
2. Placed (validated dry run clean: toolErrors [], no colliding buildings/segments), angle 186, front on Basic Road 2521:
   - Combustion Plant **47905** (1827,876): nearest home 43299 (1376,1130) 517.6 m, residential cells >= 520.8 m, 0 homes <= 300 m.
   - Combustion Plant **18536** (1779,871): nearest home 43299 479.1 m, residential cells >= 487.9 m, 0 homes <= 300 m.
   Water: pipe 32787 (-> node 29959) + 1369 between the plants. Power: line 21018 (-> pole node 2240) + 8489.
   connect Road built stubs 14310/17301 into the footprints (known bridge defect); bulldozed them (own segments). After 6 game
   days both read flags "Created, Completed, Active, ZonesUpdated", problems "".
3. Existing plants vs the 300 m rule: Combustion Plant 41160 (676,1856) nearest home 22936 417.9 m (cells >= 372 m) PASS;
   Landfill 39778 (672,1920) nearest home 32962 420.0 m (cells >= 376 m) PASS; 16061 (1754,227) 389 m PASS; 40367 (2360,683) 1030 m PASS.
   Fail: 46375 (1360,285) 45 m, 41 homes <= 300 m; 33150 (440,1391) 63 m, 58 homes <= 300 m (already "Emptying").
4. Save Portville.crp: requested 18:27:53 (epoch 1790551673), file mtime 18:27:56, 8,757,036 bytes (was 18:14:54).
5. Prepared, NOT executed (orchestrator runs them):
   - bulldoze.json: (1) factories 22451, 18736, 29750, 45377, 48256; (2) Combustion Plant 46375; (3) Landfill 33150 once empty
     (its stock cannot be read by API - TODOS).
   - rezone.json: 24 D2-west blocks, all within 6-54 m of homes. set-zone repaints whole blocks, and 18 are mixed with
     ResidentialLow cells, so those go ResidentialLow (homes keep their zone); 6 pure Industrial/Unzoned blocks (17529, 4629,
     27215, 39676, 23644, 8476) go Office. preserveOccupied false (the radius-based protection skips all 24 even after the
     factories go). radius 4 at each block position matches exactly 1 block (dry-run verified).
   - City-wide industrial growables <= 200 m from a home: only the 5 D2-west factories. Extra: Industrial block 10945
     (1660,351) is 228 m from residential (no growable).
Not touched: transit lines, any existing building or zone.

## Power 2 (capacity) — 2026-09-27 18:33-18:43

Trigger: Electricity 140 in the NE after Combustion Plant 46375 was removed. At the first read (18:33) it was already
Electricity 1, ElectricityNotConnected 3 (the incinerator replacements 47905/18536 were ramping up). Built surplus anyway.
Scripts: tmp/portville/power2/ (comps.py, res.py, sites.py, place1.py, spur2.py, water.py, poll.py; actions.jsonl).

1. **Nuclear Power Plant 27435** (2461,377) a336, on Large Road 17285, east industrial side. Nearest home 1,081 m, nearest
   residential zone block 1,069 m. Power Line 9781 (main-grid pole 8127 (2742,496) -> new node 17926 (2541,377), 234 m,
   ends outside the lot). Water Pipe 15267 (-> pipe node 14669 (2450,474)).
2. **Solar Power Plant 7690** (2260,920) a243, Large Road 34006. Nearest home 909 m. Power Line 30083 (nuclear-lot pole
   8335 (2300,765) -> 20693). Water Pipe 36800 (-> 11910).
3. **Solar Power Plant 19956** (2170,813) a222, Large Road 21375. Nearest home 855 m. Power Line 19959 (8335 -> 28055).
   Water Pipe 24664 (-> 9640).
   All three showed "Water" (no water) until the pipes went in; then Active with problems "" within ~30 s.
4. Component joins main -> turbine grid (880,900)-(800,962), turbine -> NE1 (1023,1576)-(1040,1680), NE1 -> NE2
   (1200,1894)-(1200,2000): direct and two-leg routes all cross growables/services, so not built. These gaps are bridged
   by building conduction (NE shows 0 Electricity).
5. Poll (real time / game date / Electricity / ElectricityNotConnected): 18:33 2032-12-07 1/3; 18:39:25 2033-01-17 0/3;
   18:39:53 0/3; 18:40:24 0/3; 18:40:54 0/3; 18:41:25 0/3; 18:41:55 0/3; 18:42:25 2033-02-10 0/3. The 3 NotConnected are
   pre-existing dead-end poles 9791 (2814,362), 22522 (1450,-2147), 28749 (105,290).
6. Saved "Portville": requested 18:42:39, Portville.crp mtime 18:42:41, 8,833,000 bytes (stable).

Unverified: the bridge cannot read plant output, so the new nuclear's contribution is inferred from its Active flag and the
powered line, not measured. The south advanced turbines 5316, 13588, 44743 and wind 2856/29962 are not on any power line
component that reaches the main grid (union-find over Power Line segments); they may be islands producing nothing.
Nothing demolished, no zoning, no transit, no src/ edits.

## Phase 3a/3b Waterfront
2026-09-27 18:43- (local), waterfront builder. Scripts and every call: tmp/portville/p3wf/ (lib.py, a1..a8, actions.jsonl).
Lines 30, 173, 135, 164, 218, 21, 94, 11, 26 not touched. No src/ or mcp-server/ edits. Nothing of anyone else's demolished.

### 3a West sewage (tile 0,2 shore, -3692,389)
- Live before [18:43]: WTP **14649 had moved to (-881,760)** (was -616,653 in §10.8), Water Outlet **45275** had moved from
  (1651,-1111) to (-813,718), and by 18:50 Water Outlets **8035 and 45275 were gone** and a new **WTP 7575 (-683.7,671.6)** had
  appeared, all by another hand, all on the waterfront shore. Advanced Wind Turbine 23464 (-756,694) also on the shore.
- Placed (validate dry run clean, shore snap; unlocked:true): **WTP 37774** (-3695.8,390.4) a41.8, **Water Outlets 4439**
  (-3561.9,479.5) and **15732** (-3768.9,307.4), **Wind Turbines 44643** (-3701,560), **9361** (-3781,540), **37643** (-3850,480).
  (First placements 10654/1925/22046 were bulldozed and re-placed, see lessons: connect built Basic Road stubs and releasing them
  released the buildings' own pipe nodes.)
- Pipe trunk: 35 x 80 m Water Pipe from node 7982 (-867,789) to node 31795 (-3630,440), segs 9618 .. 36298 (a2_pipe.json), every
  piece chained on the previous end node. Hookups to each building's own m_netNode: 24800 (4074 -> 25134, WTP), 577 (265 -> 2976,
  OutA), 10475 + 2421 (25134 -> 10474, OutB), all with createdNodeIds [] at the building node. Pipe 9688 (31795 -> 4074) was built
  by another hand during the redo.
- Local power island (not tied to the city grid): Power Line 19437, 33177, 5864, 131, 7428, 35165 (turbines -> node 12702), plus
  24826 (-> WTP edge), 19311 (-> OutA edge), 9013 (18772 -> OutB edge); line ends <= 6 m from each footprint.
- Verify: all three Active, problems "" within ~1 game week; watched 2033-05-06 -> 05-17 (~11 game days, 2 min at speed 3):
  Sewage problems city-wide 0 at every sample, new plant/outlets/turbines Active throughout.
- **Not proven:** that 37774+4439+15732 alone carry the whole city's sewage. A test with 14649/7575 switched off was refused by the
  permission system; the bridge cannot read sewage capacity/usage.
- Save Portville.crp: request 18:55:04 -> mtime 18:55:06 (8,640,140 bytes).

## Phase 3c/3d Metro
2026-09-27 18:43-18:58 local (game 2033-02-11 -> 2033-05-18). Scripts, every call and result: tmp/portville/p3metro/ (lib.py,
dry_lots.py, place.py, l2.py, bends_l2.py, shift.py, rebuild.py, testline.py, watch.py; actions.jsonl, stations.json,
l2-result.json (rebuilt ids; original ids in l2-result-orig.json), rebuild-result.json, lines_before.json).
### 3c Reserve M2 lots (no track)
- Re-validated each lot right before placing (validate:true dry run + live footprint check vs buildings/networks). Sim running.
- **NCW 28424** (1063.5,1762) a270, fronts Victoria St 35970: platform seg 19205, nodes 15029 (1051.5,1834) / 7778 (1051.5,1690), y 173.2. Active, no problems.
- **CM2 2669** (1424.5,600) a90, fronts Station Road (now seg 28633, Medium, front edge 4.5 m off the road edge): seg 19709, nodes 32448 (1436.5,528) / 15687 (1436.5,672), y 163.8. Electricity problem at placement cleared within ~2 min; Active, no problems at 18:58.
- **NCN 1923** (1063.5,2324) a270: seg 36009, nodes 26670 (1051.5,2396) / 20352 (1051.5,2252), y 181.3. Victoria St extended does not exist (nearest road 124 m), so it reads RoadNotConnected + Electricity + Water, and by 18:58 Active false / MajorProblem. Reservation only.
- **NCS not placed:** the lot collides with **10699** `H1_3x2_Blockhouse_Corner` (1340,1286) (dry run collidingBuildingIds [10699]). Needs the orchestrator's paused demolish-and-place: `python3 tmp/portville/p3metro/place.py NCS` right after the bulldoze.
- Platform ends match the plan to 0.00 m for all four. M1 lines 30/173 stayed Complete 1/1.
- Save Portville.crp: requested 18:45:02, mtime 18:45:03 (9,015,063 B).
### 3d River gate (L2 WF -> WBE under the river)
- Paused 18:45:29 (game 2033-03-04). Waterfront area survey: no roads yet (nearest 495 m), pipes, turbines 6243/42033, outlet 8035, poles 31773/38166; the WF lot validated clean (waterfront builder had not built there).
- **WF 3276** (-408.6,700.1) a151.7: seg 19349, nodes 10208 (-339.5,676.5) / **7911** (-466.3,744.8), y 115.2. **WBE 41930** (-668,-282) a180: seg 16293, nodes **9351** (-596,-270) / 12847 (-740,-270), y 116.5. Both 0.00 m from plan.
- Dry run of all 42 L2 pieces (legs.json, Metro Track elev -12): all ok. Water probes on the route: riverbed y 22.5-23.0 (CannotBuildOnWater); tunnel y over water 90-96, i.e. ~70 m above the bed, in the water column.
- **Built L2**: 42 pieces 7911 -> 9351, 41 new nodes (list in l2-result-orig.json). Live: max bend 32.0 (nodes 14439/6454/23367), all other <= 31.9; track 1,175.7 m (1,463.7 with platforms); max grade 27.8% (shore climb, seg now 3635), 25.2/24.7/23.5% at the two shore drops; every inner node degree 2; no segment problems.
- **Test line 144** WF -> WBE (2 stops, one visit each; dry run: both via station, 12 m snaps): after unpause at speed 3, 6 samples over 90 s: Complete but **LineNotConnected on both stops**, straight-line "Metro Line" segments (1016 m), vehicles 1/1 then 0/1, length 2,290.2.
- Bisect: with only two stations no intermediate test stop exists; the middle-shift scan flagged 17762 / 10575 (5.1 m) but working M1 curve pieces show the same artefact (18718/21983 3.4 m, 13243/6821 4.25 m), so it is not evidence. Applied the gate's one-rebuild: paused, bulldozed every L2 piece keepNodes and rebuilt it between the same nodes (42/42 reused both nodes, createdNodeIds [] every time; new ids in rebuild-result.json). Geometry unchanged (max bend 32.0, grade 27.8%, 1,175.7 m).
- **Test line 48** (fresh, after deleting 144): 6 samples over 90 s at speed 3 (game 2033-05-09 -> 05-17): **Complete, problems none, vehicles 1/1 in every sample, length 2,545.5 m** (2 x 1,319.7 m stop-to-stop along track = 2,639 m; M1 reads 8.6% under the same estimate, so plausible). M1 30/173 1/1, no problems throughout.
- **Gate verdict: PASS after one rebuild.** A Metro Track tunnel in the water column (y ~92, bed ~22) paths and runs. The first failure was a build defect in one or more pieces, cleared by an in-place rebuild, not the water (same class as TAmpa B16 33915 / W1 34851).
- Station state at 18:58: WF RoadNotConnected (no Waterfront Drive yet), WBE Electricity + Water + MajorProblem, Active false (no roads/utilities on Westbank). The test train ran regardless.
- Deleted test lines 144 and 48; line ids 11,21,26,30,94,135,164,173,218 before and after (identical). Track and both stations kept. Sim left running at speed 3 (as found).
- Save Portville.crp: requested 18:57:50, mtime 18:57:52 (8,946,840 B). (Someone else saved at 18:55:06.)
Not touched: any other leg, existing lines, zoning, buildings, src/, mcp-server/.
- Later: Water Outlet 15732 disappeared between 18:57 and 19:10 (not by me; its pipe node 10474 remains). The west site is now
  WTP 37774 + Outlet 4439. Power line 9013 to it and the replacement 32877 were bulldozed (dangling pole ElectricityNotConnected).

### 3b Waterfront
- **Roads** (dry run + footprint/net guard, chained on end nodes): Waterfront Avenue (Medium) from Holmes/Dixon node 24949 (reused):
  24432, 5090, 14666, 21694, 3420, 22843, 18503, 2419, 21241; Waterfront Drive (Small 4 Lane Road with Bus Lanes): 9764, 2468, 6417,
  32197, 20831, 34394, 16130, 34319, 2813, 14368, 27197, 15398 (west end node 12092 = intended dead-end turnaround; the only new
  road anomaly). Lane probe (Bus dry run 4 m either side of each midpoint): 42/42 snapped to the right segment, <= 6.6 m.
- **Water** along the Drive: tap 15469 from node 10906 + 12 pipes (b6_pipe.json). Park Water problems cleared.
- **Power:** city-grid tie from node 20026 (112,378): 32586, 31190, 28550, 3404, 9514, 31174, 16977, 13067, 148 (ends at
  (-380,700), 28 m from the WF entrance), running 15 m shoreward of the promenade; guards: no node within road half-width + 3 m, no
  footprint crossed.
- **Attractions** (validated clean, unlocked:true, no sub-buildings): SeaWorld **48833** (-301.5,684.2); 10thAnniversary Park
  **41889** (-133.3,595.8) (shifted, see TODOS); Expensive Park **25348**; ChirpyBirthday Balloon Tours **8541**; Botanical garden
  **7831**; Regular Plaza **43832**; Tennis_Court_EU **43912**; Expensive Plaza **19145**; MerryGoRound **42242**. All Active, no
  problems, no RoadAccessFailed at 19:05. Skipped: JapaneseGarden (collides with 42033); Fishing Island (-845,749) and Floating
  Cafe (-264,448) have sub-buildings (player places them).
- **Promenade** (Pedestrian Pavement): A 6245, 34149, 33807, 17542, 7525 ((-94.5,402.6) -> (-429.6,610.4)); B 18231, 19082, 5188
  (rerouted around the Expensive Plaza), 11989, 15421, 6521 ((-634.9,730.8) -> (-936.9,863.1)). Links to the Drive, each end
  lane-linked (endNodeLaneSegmentId): 6921 -> 9764, 26516 -> 32197, 23188 -> 34394, 9453 -> 2813, 8849 -> 15398. Pieces 6-8
  (-429.6,610.4) -> (-634.9,730.8) wait on turbines 6243/42033. Footbridge not built (TODOS).
- **WF1 Waterfront Feeder, line 150** (#00A3AD, budget 80): stops Pearl Blvd WB seg 5431 (400.2,794.2) [West Gate, 145 m from
  entrance 29599; B1's stop is 55 m from it, so nothing closer fits the 150 m rule]; Drive seg 6417 WB (-218.6,574.9); seg 2813
  WB (-651.5,808.1); seg 2813 EB (-655.5,800.7); seg 6417 EB (-222.5,567.5). Router (rt.py on fresh roads.json, U-turns only
  at dead ends) 4,823 m; game 4,859 m. Dry-run snaps 1.8 m, all on the designed segment; laneIds != 0. Line ids
  {11,21,26,30,94,135,164,173,218} -> +150. After 65 s: Complete, no problems, 7/7 buses. No WF1 stop within 150 m of another
  line's stop; nearest WF1 stop to the WF entrance 228 m (so WF1 will not duplicate the WG -> WF metro hop once M1 reaches WF).
- **Accessibility** (straight line to the nearest stop of any line; all WF1): SeaWorld 137 m (WF entrance 108 m); 10th Park 88;
  Expensive Park 114; Balloon Tours 95; Botanical 207; Regular Plaza 177 (WF 79); Tennis 35; Expensive Plaza 39; MerryGoRound 156.
  Road access: no RoadAccessFailed on any. Walking: 5/5 promenade links lane-linked to Drive sidewalks; promenade dead ends
  (-429.6,610.4) and (-634.9,730.8) are not linked until pieces 6-8 exist. Nearest promenade node to the WF entrance: 66 m.
- Saves Portville.crp: 19:04:53 -> 19:04:55; 19:10:05 -> 19:10:07.

## Phase 3b finish
2026-09-27 19:25-19:50 local (game 2033-11-13 -> 2034-05-01), 3b finishing builder. Scripts and every call: tmp/portville/p3wf2/
(lib.py, actions.jsonl, t1_*, t2_*, t5_*, footbridge.json/.py, shore.json). No transit lines, zoning or src/ touched; nothing of
anyone else's demolished.

### 1. Treatment capacity + grid tie (west site)
- Validated shore dry runs (branch "shore", canPlace, no collisions), explicit angle, then own-node probe (`connect` dryRun, maxDistance 45,
  node not on any existing pipe):
  - **WTP 42798** (-3455.6,517.9) a17.1, heightAboveWater 15.5; own node **28511** (-3465.0,548.5); pipe **5677** trunk node 9494 -> 28511.
  - **WTP 27069** (-3384.8,565.8) a21.9, heightAboveWater 21.5; own node **18061** (-3396.7,595.5); pipes **10239** + **2491**
    trunk node 25332 -> 24507 -> 18061. createdNodeIds [] at every join.
- **City-grid tie (Power Line):** west part 33 x ~80 m from island hub 12702 (-3640,470) along the pipe trunk, 15 m on the water side,
  to node **31086** (-1023,754.3) (segs in t1_power_west.json; pylons stand in the river, the trunk corridor is open water); waterfront
  part 13 pieces **14699 .. 36500** from grid node 25747 (-435.6,596.7) along the shore strip (contour -15 m, between promenade and water)
  to 31086 (t1_power_east.json). Spurs ending <= 4 m from footprints: **24911** -> 42798, **21373** -> 27069, **32984** -> player's
  Advanced Wind Turbine 23464 (it had no line to the grid). Guards: no building within 2 m, no surface segment within half-width+1,
  no pylon within half-width/2+6 m of a road/path.
- Verify: union-find over all Power Line segments puts grid node 20026 (112,378), 15203 (WF) and island hub 12702 in one component
  (144 segments); no node of it reads ElectricityNotConnected. At 2034-04-13 and 2034-05-01: WTP 37774, 42798, 27069 and Outlet 4439
  Active, problems ""; city-wide Sewage problems 0.
- **Outlet 4439 is ready to retire** from my side (three WTPs Active, 0 Sewage problems). Not measured: capacity vs sewage use (the
  bridge has no capacity field). 4439 not touched.

### 2. Promenade 6-8 and shore-strip parks
- **P6 28736, P7 24325, P8 34859**: 7949 (-429.6,610.4) -> 16324 (-494.5,654.1) -> 5958 (-564.7,692.5) -> 25484 (-634.9,730.8),
  both ends joined to existing nodes (createdNodeIds [] at the joins); footprint + net guards clean. The promenade is now one line
  A1..A5, P6..P8, B1..B6. Drive links read lane links: 6921->9764, 26516->32197, 23188->34394, 9453->2813, 8849->15398.
- **JapaneseGarden 40273** (-512.3,693.2) a331.7 (faces the Drive; re-validated clean), 104 m from WF 3276.
- Shore side of the Drive (validated, clean): **Regular Playground 36894** (-451.6,660.5) 58 m from a WF anchor; **bouncer_castle 19679**
  (-545.4,711.0) 137 m; **dog-park-fence 34353** (-606.7,744.0) 75 m (WF1 stop seg 2813). All Active, no problems at 2034-05-01.

### 5. Downtown City Quay (player: "Build quays on waterfront for public use")
- Prefabs: `City Quay` and `Quay` are QuayAI, Beautification/None/Default; the bridge does not list lanes. **City Quay carries a
  pedestrian lane** (measured: path dead ends lane-link to it, see below). `Quay` not tested.
- Side: a W->E piece (water on the right) flattened a deck from 32 m inland to 4 m water-side of the centreline and left a face at
  +6..+8 m (terrain probes before/after). Built at terrain height (117.5) it sits below the current water peaks (111-123 on this
  stretch), so that test piece **2994 (mine) was bulldozed** and the quay rebuilt at **elevation +5**.
- **Quay:** 7 pieces W->E **31942, 8612, 6442, 26596, 17665, 25527, 29289**, nodes 4777 (-404.4,572.7) .. 32122 (-100.6,369.5), 366 m,
  deck y 121.2-124.0, 25 m shoreward of the shore contour, 25-30 m shoreward of the promenade, >= 12 m from every pylon (the last
  piece pulled to -18 m to clear pylon 23038). No building/segment in the guard band.
- **Walkway links:** Pedestrian Pavement stubs from the promenade, each ending 8 m inland of the quay centreline:
  **26758** (node 1638) -> lane seg **6442**; **29698** (13063) -> **8612**; **26068** (1833) -> **17665**; **35826** (26192) -> **25527**
  (endNodeLaneSegmentId). Promenade -> Drive sidewalks via the five links above.
- Distance: quay 127.5 m from WF 3276, 94-103 m from the WF1 Drive stops.
- /capture of the stretch (mode None, 420 m) rendered one flat colour: no picture.

### 3. Couplet footbridge (prepared, NOT run)
- tmp/portville/p3wf2/footbridge.json (bulldoze list + bodies), footbridge.py (build steps only; aborts if the lots still stand or any
  footprint guard hits; verifies joins and both landing lane links). Alignment z 818: Young St 11012 (x 195.5) -> Stephen Harris Ave
  26443 (x 100.1) and Holmes Hwy 6129 (x 68.5) at +12 m -> Waterfront Ave 3420 (x -40). Landings end 17 m from road centres
  (1 m outside the 16 m half-width).
- Bulldoze first: **23304** `L3 4x3 Shop14` (166.0,825.1) and **7077** `L3 4x4 Shop07a` (132.0,815.7) (7077 now stands on the
  lot 38341 had).
- Dry run with the shops present (`footbridge.py --dry`): all 7 build-network dry runs ok; FB0-FB3 blocked only by the guard on
  23304/7077; FB4-FB6 pass now. Ramps 19-22%.

### 4. Utilities check after the turbine removal
- WF 3276, SeaWorld 48833, all 12 waterfront parks/plazas, Advanced Wind Turbine 23464: Active, problems "" at 2034-04-13 and 2034-05-01. Nothing to fix
  beyond the grid tie above.

Saves Portville.crp: 19:38:36 -> 19:38:38 (9,907,389 B); 19:45:14 -> 19:45:16 (9,596,991 B).

## Water 2 (water builder, 2026-09-27 19:54-20:07)

**Problem at 19:54:** 48 buildings with Water (WaterNotConnected 0), 31 in the 200 m cell (1000,2000) and 13 more in the cells around it (NE north, x 1000-1300, z 2050-2330); Water Tower 25918 (1526,2241) Active false.

**Diagnosis [M]:**
- Pipe network: one component (686 Water Pipe segments, x -3788..2852, z -1309..2200) holding both intakes and all three WTPs. Every NE-north dry building was 17-49 m from a pipe node, so not a coverage gap and no disconnected branch.
- Tower 25918 had no pipe. Its own node m_netNode 10641 sat at its centre with no segment attached (connect dryRun maxDistance 45 -> targetNodeId 10641, distance 0.03), and the nearest pipe node was 10845 (1480,2200), 62 m away. productionRate was already 100 (set-building-active reported productionRateBefore 100), so the tower was not switched off. It had no problem flags at all, not even WaterNotConnected.
- Both supply intakes (1428, 38338) were Active and ~3.5 km of pipe away from the NE north, which sits at the far end of the network. The dry area was the part of the network farthest from any source. The bridge cannot read capacity against consumption, so a supply shortfall could not be measured directly.

**Actions (tmp/portville/water2/actions.jsonl):**
1. 19:56:42 Water Pipe **19005**, node 10845 -> tower node 10641. Tower 25918 read `Active` by 19:58.
2. 19:58:02 two `Water Intake`s upstream of 38338 on the east bank (flow SE -> NW; WTPs are far west, downstream). Both came from validated shore dry runs with toolErrors [] and no colliding ids:
   - **44588** (2152.4,-1595.8), angle -35.0, waterHeight 125.5
   - **4730** (2296.0,-1744.9), angle -45.4, waterHeight 125.8
   - angleDegrees was passed, but shore placement sets the angle itself.
3. Own-node probes: 44588 -> node **29697** (2163.2,-1582.5); 4730 -> node **3176** (2308.9,-1733.8), both 17 m from centre. Pipes:
   - 11403 (1866.6,-1309.2) -> 29697 in 6 pieces: 7706, 30567, 23262, 26917, 30695, 32754
   - 29697 -> 3176 in 3 pieces: 6466, 36358, 16868
   - Both chains end on the intakes' own nodes (createdNodeIds [] on the last piece).
4. 19:59:01 Power: the nearest power node was 893 m away, so each intake got its own `Wind Turbine`, ~60 m inland, with no homes within 400 m:
   - **2267** (2200,-1560) + Power Line **30675**, ending ~28 m from 44588's centre
   - **3173** (2350,-1720) + Power Line **18586**, ending at the same distance from 4730
   - Both intakes were Active with problems "" by 19:59:19.
   - `Advanced Wind Turbine` dry runs here all failed with `WaterNotFound` (TODOS).
5. The count settled at 4-5, all coverage gaps 97-118 m from any pipe (east industrial, Large Roads). Pipes added:
   - 20:02:44 **20159** (3637, Cargo Terminal Rd); **11286, 27129** (19156, Walker St); **33215, 17523** (42760, Greenaway St)
   - 20:04:50 along Walker Street (x 1777..2019), 4 pieces: **33133, 16050, 9995, 12453** (13724, 17211, 43934)

**Water count over time (poll.jsonl):**

| Time | Water |
|---|---|
| 19:54 | 48 |
| 19:59:19 | 4 |
| 19:59:30 | 4 |
| 20:00:00 | 5 |
| 20:00:30 | 4 |
| 20:01:01 | 5 |
| 20:01:31 | 5 |
| 20:02:51 | 6 |
| 20:03:21 | 3 |
| 20:03:51 | 5 |
| 20:04:22 | 5 |
| 20:04:54 | 4 |
| 20:05:25 | 2 |
| 20:05:55 | 2 |
| 20:06:26 | 2 |

- WaterNotConnected stayed 0 throughout.
- The 2 left are the reserved metro entrances NCN **1923** (1064,2324) and WBE **41930** (-668,-282). They have no road, power or water, and are already tracked in TODOS.
- The 48 -> 4 drop came after both the tower pipe (19:56:42) and the intakes (19:58-19:59:10). This run cannot tell which of the two did it.

Save Portville.crp: requested 20:06:37 -> written 20:06:39 (9,792,459 B).

## Phase 3e B3 feeder test
2026-09-27 19:55-20:10 (local), transit builder. Master plan §10.13 step 3e (codex's cheapest test): end B3 (164) at West Gate
instead of Central, Civic kept, everything else unchanged. Scripts and every call: tmp/portville/p3e/ (lib.py, rt.py = bus/rt.py on a
fresh roads.json, edit.py, measure.py, save.py; actions.jsonl, measure.jsonl, edit_dry.json, edit_real.json). Only line 164 was
edited. Nothing demolished, no roads, no zoning, no src/ or mcp-server/. Water 2 (another builder) ran pipe work 19:54-20:07 in the
same window and saved at 20:06:37 (that save contains this edit).
- **Before** [game 2034-06-04, 21,043 citizens]: B3 8 stops, game 3,931 m, 10/10 at budget 150; stop 0 Central forecourt (1488,746)
  seg 18556 (168 waiting); stop 1 (524,990) seg 13232 x520 NB, 182 m from West Gate entrance 29599 (544,809).
- **Design** (rt.py, U-turns only at dead ends): remove stop 0 (Central) and move old stop 1 to Edward St NB seg 28892. Router
  4,138 -> 2,560 m; the loop now turns round on Pearl Blvd / Ward St / Crowley St / Faith St (x 520-680, z 710-790), so B3 no
  longer runs on Station Ave to Central. New West Gate stop is 73 m from B1's West Gate stop (596,830) and 132 m from WF1's
  (400,794) (the West Gate exemption); every other B3 stop is >= 234 m from any other line's stop (unchanged stops).
- **Edit** (transit-line-edit 164, dry run first: snap 8.8 m onto seg 28892, then real at 19:59:54; line ids unchanged):
  applied "removed stop 0", "moved stop 0". Stop 0 = node 21193 (524.5,831.2), 29 m from entrance 29599; laneId 111682.
  30 s later: Complete, no problems, 7 stops, **length 2,237 m** (router 2,560, ratio 0.87 as for B1), 6/6 at budget 150.
  **Budget 150 -> 250** at 20:00:39 (10 buses need 241 < budget <= 268 for target = ceil(budget% x 2,237 / 60,000)); 45 s later
  10/10, target 10. Name "B3 North Gate D2" kept.
- **Save** Portville.crp: request 01:01:48Z -> mtime 01:01:51Z (20:01:51 local), 9.66 MB.
- **Measure** (speed 3, a period = 7 game days = 65-75 s wall; lastPeriod res+tour, measure.jsonl). The period containing the edit
  (game 07-04..07-10) is skipped. The period ending 07-17 was read once at 20:02:29 (lastPeriod only, no waits; actions.jsonl
  note); 07-24 onwards by measure.py.
  | period end (game) | phase | M1E 30 | M1W 173 | M1 | B3 164 | B1 135 | WF1 150 | B2 218 | wait M1E@WG | wait M1W@Central | wait B3@WG |
  |---|---|---|---|---|---|---|---|---|---|---|---|
  | 2034-06-12 | base | 25 | 13 | 38 | 207 | 148 | 88 | 261 | 1 | 2 | (Central 169) |
  | 06-19 | base | 15 | 19 | 34 | 204 | 176 | 113 | 301 | 16 | 14 | (174) |
  | 06-26 | base | 42 | 15 | 57 | 213 | 181 | 116 | 237 | 0 | 0 | (179) |
  | 07-03 | base | 22 | 31 | 53 | 168 | 188 | 84 | 274 | 10 | 9 | (90) |
  | 07-10 | skipped (edit) | | | | | | | | | | |
  | 07-17 | after 1 | 32 | 140 | 172 | 59 | 135 | 94 | 247 | - | - | - |
  | 07-24 | after 2 | 115 | 123 | 238 | 68 | 137 | 113 | 242 | 15 | 73 | 20 |
  | 07-31 | after 3 | 25 | 74 | 99 | 197 | 146 | 37 | 243 | 11 | 3 | 25 |
  | 08-07 | after 4 | 57 | 84 | 141 | 147 | 159 | 37 | 225 | 32 | 121 | 2 |
  | 08-14 | extra | 91 | 139 | 230 | 102 | 186 | 105 | 247 | 8 | 0 | 8 |
  | 08-21 | extra | 58 | 21 | 79 | 136 | 160 | 61 | 266 | 33 | 7 | 5 |
  | 08-28 | extra | 146 | 100 | 246 | 105 | 175 | 105 | 312 | 72 | 6 | 5 |
  Means, 4 base vs 4 after (07-17..08-07): **M1 45.5 -> 162.5/wk (3.57x)** (M1E 26.0 -> 57.2, M1W 19.5 -> 105.2); **B3 198.0 ->
  117.8 (-40.5%)**; B1 173.2 -> 144.2 (-16.7%); WF1 100.2 -> 70.2 (-29.9%); B2 268.2 -> 239.2 (-10.8%). Over all 7 after-periods:
  M1 172.1 (3.78x), B3 116.3, B1 156.9, WF1 78.9, B2 254.6. B3+M1 243.5 -> 280.2; the six lines together 785 -> 734 (-6.5%).
  m_averageCount 07-03 -> 08-28: M1E 34 -> 67, M1W 16 -> 74, B3 195 -> 136, B1 169 -> 163, WF1 104 -> 81, B2 251 -> 254.
  Waiting at M1E West Gate: base mean 6.8 (0-16), after 8-72 (mean 28.5 over 6 reads). M1W's Central stop (westbound boardings
  toward West Gate / B3) went 0-14 -> 0-121 (73 and 121 in two reads): Central -> D2 riders now take M1W then B3.
  End [game 2034-09-01, 22,239 citizens (+5.7%)]: all 10 lines vehicles = target, no problems; B3 10/10 at 250.
- **Read:** by the brief's criterion (M1 >= 2x baseline and >= 30/wk) the **feeder premise is supported**: M1 rose 3.6x to ~160/wk
  and both directions rose, with the largest gain on M1W from Central (the trip B3 used to make directly). B3 lost 40% of its
  boardings (-80/wk), but M1 gained more than that (+117/wk), so the ">30% lost and M1 gains less" case does not apply. Caveats,
  plainly: (1) boardings double-count transfers, so the +117 on M1 is not +117 trips; B3+M1 rose 37/wk while B1, WF1 and B2 fell
  (-29, -30, -29/wk), and the six lines together carried 6.5% fewer boardings with 5.7% more citizens. The test shows riders moved
  onto M1; it does not show more people using transit. (2) Period-to-period noise is large (M1 79-246, B3 59-197), 4 periods each
  side. (3) Not controlled: city growth, Water 2 pipe work in the same window, B3's budget 150 -> 250. (4) M1 runs 1 train per
  direction; M1W waits of 73-121 at Central suggest the 1-train service caps it.

## 2026-09-27 20:1x Orchestrator: 3e verdict + M1 capacity
- Re-derived 3e from tmp/portville/p3e/measure.jsonl: M1 combined 45.5/wk before (4 wks) → 172/wk over 6 post-edit weeks; B3 198 → ~126. Kept B3 as a West Gate feeder. Transfers double-count; six-line total fell ~6% while citizens rose ~6% — a shift onto M1, not new transit trips.
- M1 lines 30 and 173: budget 100 → 250, trains 1 → 2 each (verified 2/2, no problems). Transfer waits at Central reached 73–121 with 1 train.

## Traffic T1 (rail grade separation) (traffic builder, 2026-09-28 01:0x-01:26Z)

Scripts and raw data: `tmp/portville/traffic/` (sample.py, summ.py, design.py, guard.py, swap.py, checks.py; before.jsonl,
after.jsonl, actions.jsonl, checks_*.json, transit_before.json).

**Level-crossing scan** (/state/networks Road total 1569, PublicTransport total 874, both under the 5000 cap, nothing truncated).
Every road segment sharing a node with a track: 3 crossings, all on the N-S mainline.
- node 26873 (1506,984): Middle Ave 15868 + Campbell Blvd 7274 x Train Track 124/23135. **Grade-separated.**
- node 3590 (1540,640): Brittany Cooper St 30198 + Dexter St 3144 x Train Track 677/2661 (station throat, 45 m N of the cargo
  siding junction 12674). **Grade-separated.**
- node 19258 (1542,1658): Lilac Hwy 32346 + Smithson Hwy 36770 (Highway Barrier) x Train Track 16587/29668. **Not built** (low
  traffic, 16/15 mean before; west landing 64 m from junction 28702 would need ~15 % for 8 m; see TODOS).

**Campbell overpass** (01:15:45Z, sim paused for the swap, restored to speed 3). Bulldozed 15868, 7274, 5304 (no frontage, no
buildings within 14 m). Built `Medium Road Elevated` 26405 -> 12202 -> 20212 -> 4122 -> 11789 -> 27904 -> 3116:
segments **25658, 16161, 10698, 11515, 3029, 9455** (game-named "Brown Bridge"). Node y 172.52 / 175.09 / 177.45 / 177.25 /
175.74 / 173.49 / 171.23 as designed; grades 7.0 / 7.0 / -0.7 / -5.7 / -5.7 / -5.7 %. Clearance over tracks 124/23135
8.2-8.5 m (road y minus track y at closest approach). Both end nodes reused (createdNodeIds [] on the last piece, 26405 on the
first). Rail node 26873 stays (tracks only).

**Dexter overpass** (01:17:50Z, same procedure). Bulldozed 32819, 13449, 30198, 3144, 4064 (the whole Station Road 32050 ->
Dexter St 913 link; no frontage except factory 21227 / sweatshop 20395 at the 913 corner, both still Active on 17284). Built
`Basic Road Elevated` 32050 -> 2507 -> 12934 -> 9485 -> 10555 -> 26169 -> 26488 -> 913: segments **21128, 22201, 7973, 20576,
21495, 8084, 29279** ("Terry Bridge"; the game renamed Dexter St 17284 to "Terry Street"). Grades 4.3 x3 / 0 / -8.3 x3 %
(east landing is fixed by 913 and the frontage on 17284). Clearance over 2661 8.1-8.2 m, over 330 and cargo siding 4552
8.4 m; 14 m over Metro Station Track 19709. Starting at 32050 instead of 18221 kept the west grade at 4.3 % (from 18221 it
was 8.5 % with only 7 m clearance). Basic, not Large: the old 66 m Large Road piece 13449 was between two Basic pieces.

Guard: every piece dry-run ok and checked against live buildings, networks and reserved corridors (hwy/check.py run) plus a
per-track clearance sampler. False positives: "NOT OWNED" (hwy/own.py tile table predates the NE tile), reserved Central
buffer (existing road alignment). No demolition needed.

Checks after each build and at the end [M]: rail 234 track segments, 1 component; T1A 11 and T1B 26 Complete 2/2 trains,
no problems; Cargo Center 42520 Active, no problems; all 10 lines Complete, vehicles = target, no LineNotConnected (checked
~30 s, ~1 min and ~7 min after). Road anomalies: 0 from my pieces; 1 roadTerrainCliff on Harbor Road 22027 Webb St and 2 extra
road components (Webb St, Mary Morgan Bridge 1563) appeared at x -2200/-590 from another hand during this run. Lane probe
(transit-line-create dry run) cannot test elevated roads (no stop-capable lane); car use was verified by density > 0 on all
13 new pieces within ~1 min. Zoning: no zoned blocks along the removed segments were occupied; elevated roads carry no zone
blocks, so nothing to repaint. Pipes/power untouched (water pipes and power lines are separate networks; counts changed only
through another hand's builds in the west).

**Traffic, before (9 samples, game 2034-08-14..09-07) -> after (11 samples, 2034-10-28..12-01), mean density** [M]:

| Segment | Before | After |
|---|---|---|
| City trafficFlowPercent | 76.2 (75-78) | 84.7 (84-86) |
| Campbell 7274 / 5304 (at-grade) | 93.3 / 96.1 | removed; overpass 5.4-17.3 |
| Middle Ave 15868 | 53.0 | removed (25658: 15.7) |
| Campbell 20689 | 58.3 | 13.6 |
| Harris Ave 20927 | 63.2 | 14.1 |
| Dexter St 3144 / Brittany Cooper 30198 / Dexter St 4064 | 97.8 / 91.9 / 37.1 | removed; overpass 4.8-9.4 |
| Dexter Ave 4114 | 50.6 | 7.0 |
| Ramp 27688 / 27853 | 100 / 31.1 | 100 / 91.9 |
| Waterfront Ave 24432 / 5090 | 62.4 / 70.1 | 65.5 / 69.2 |
| Dixon 7258 | 47.6 | 60.8 |
| Holmes Blvd 18735 / 589 | 89.2 / 71.8 | 53.3 / 57.1 |
| Harris Hwy 24469 | 76.2 | 79.5 |
| Lilac 32346 / Smithson 36770 | 16.2 / 14.6 | 16.8 / 11.5 |

Caveat: the two windows are ~2 game months apart and not controlled for time of day or growth.

**Task 3 (ramp 27688), not built.** Waterfront 24432 was > 60 in 6/9 samples, so the trigger was met, but the queue there is
not the ramp's: 27688 is the NB on-ramp merging into Stephen Harris Ave NB at 10012; Waterfront feeds the SB Holmes Blvd
junction 24949, whose downstream 18735 (89 mean) runs into the 6 -> 3+bus lane drop at 14564, and Dixon 7258 (the link between
the two) was only 47.6. Lengthening the ramp would not touch that queue. After the build the ramp chain itself is saturated
(27853 31 -> 92). Both are filed in TODOS for the orchestrator.

Saves `Portville.crp`: request 01:17:11Z -> written 01:17:13Z (Campbell); request 01:18:59Z -> written 01:19:01Z (Dexter).

## Harris T2 (Greenaway disconnected from the highway) 2026-09-27 20:47-20:54 local

Loaded `Portville.crp` (newest save). Game came up paused at 2035-01-16, 23,572 citizens, demand R54/C50/W33, flow 78. The open hotspot with a clear local cause was Harris Highway, not the NB ramp: ramp 27688 is still 100, but it merges into a shop-fronted 6-lane avenue, and a light-free merge would mean either cutting that avenue to a 3-lane highway or flying a ramp over the shops at about 11% just to reach the existing node height. Left for a later pass.

**Harris node 22202** (2346,941): Highway 24469 and 35647 are the through route (bend 2.7° if 35647 is taken reversed). Large Road Greenaway 34006 joined them. Greenaway density was 3. No transit stop used 34006.

- Bulldozed 34006, keepNodes true. 22202 kept both highway segments. 18677 kept 7138.
- Built Large Road **23151** "Greenaway Street" from 18677 (2308.6,867.3) to new node 18725 (2326.8,903.0), 40 m along the old alignment, 43 m short of the highway. createdNodeIds [18725] only. Dry run first.
- Road anomalies from this edit: 0 (the three existing cliffs are Strawberry 5458/36379 and Harbor 22027).

**Traffic** (speed 3, two reads, then the save read):

| Segment | At load | +5 game days | +8 more | At save (2035-02-04) |
|---|---|---|---|---|
| 24469 Harris | 93 | 38 | 10 | 16 |
| 11006 upstream | 73 | 27 | 10 | 16 |
| 5369 further upstream | 74 | 32 | 8 | (outside the last probe) |
| 35647 downstream | 28 | 26 | 14 | 20 |
| 23151 Greenaway stub | — | 11 | 9 | 5 |
| City flow | 78 | 80 | 81 | 83 |

**Cost:** L1 shops 35707 (2346,884) and 47012 (2343,863) were Active with no problems immediately after the build and were gone after 5 game days (growables 1,489 -> 1,487). CommercialLow repaint at those points changed 0 cells. The other three shops along lower Greenaway (23067, 43062, 41797) stayed Active.

Citizens 23,572 -> 23,645. Demand at the save R54/C50/W33. Still open, and worse on this run's later reads: Holmes 6129/11388 at 100, Waterfront 24432 at 73, NB ramp 27688 at 100.

Save `Portville.crp`: request before the edit 20:47:29 local (10,707,531 B); after, mtime 2026-09-27 20:54:15-0500, 10,386,944 B.

## Harbor link and waterfront walk (2026-09-27 21:17-21:22 local)

Only save touched: `Portville.crp`. Phase 3 transit already in this save: M1 (lines 30/173), Central and the other stations, B1-B4, WF1, T1, the promenade, the quay, and SeaWorld. This pass connected the harbor and closed the one promenade path that did not meet its neighbour.

**Chat.** The in-game window title, button, and status lines now say Grok (`src/ChatPanel.cs`, `src/AgentBridgeNotifier.cs`). Ctrl+Shift+C or the Grok button on the API console. This session watches `/chat/inbox`.

**Harbor road.** Harbor 26710 was on segment 22027 only. The lake (terrain y ~89) blocks a straight line to Cooper Avenue. Medium Road follows the dry ridge at z 470 from node 30278 (-2175,542) to (-1150,470): segments 18220, 20548, 7107, 15172, 32476, 9707, 18738, 32617, 10179, 27969, 35451, 33659, 2780. Steepest new piece is 18220 at 19.8%. Medium Road Bridge 28854, 33944, 25584, 24553, 25512, 1202, 34543 crosses the lake at x -1150, deck y 128 down to 122, grades about 1%. Medium Road 36302 joins Waterfront Drive node 12092 with createdNodeIds []. Harbor node to that drive node: 21 hops. Approach and bridge both have 2 pedestrian lanes, so the walk continues onto Waterfront Drive, which already reaches the promenade paths and the quay.

**Path.** Pedestrian Pavement 16 joins promenade nodes 19929 and 30850 (13 m, createdNodeIds []).

**Bus H1**, line 102, stops on 22027 (harbor), 15398 (west end of the drive), and 4925 (beside metro 3276). Snaps 0.02 / 0.05 / 0.01 m. Complete, no problems, length 5,093 m, 7/9 vehicles at budget 100 about 40 s after creation.

Road-terrain cliffs on nine ground pieces are filed in TODOS. The bridge pieces are clean.

Save `Portville.crp` mtime 2026-09-27 21:22:37-0500, 9,767,688 B.

## M1 to the waterfront and west bank (2026-09-27 21:28-21:38 local)

Chat is still the same in-memory transcript. `/chat/history` returned every entry (player and Grok), and the MCP client's live read returned the same 8 entries. The panel pulls that history into the scroll list. `npm test` in `mcp-server` passed 19/19.

**Metro.** West Gate node 27487 was 824 m from waterfront station node 10208, not on the same track. Built 18 `Metro Track` pieces (13586..4031), max planned turn 11.9 deg, grade about 5%. The last piece reused node 10208. Added the waterfront entrance 3276 to lines 30 and 173. Both came back Complete, 3/3 trains, 3,582 m, no problems. The west-bank entrance 41930 was already on that same track component, so both lines gained that stop too: Complete, 6/6 trains, 6,181 m, no problems. Four other station tracks are still isolated (19205, 19709, 27014, 36009).

**City Hall** 19581 at (1360,640), facing Station Road, Active, no problems. Three commercial blockhouses (2050, 2180, 29100) were removed for the lot. Every monument is within 400 m of a transit stop (farthest is Grand Mall at 315 m).

Save overwrote `Portville.crp` only. Directory still has the same 10 `.crp` names; no `Portville` copy. Mtime 2026-09-27 21:38:13-0500, 10,731,992 B. The game replaced the package (inode 158311005 -> 158313563); it did not add a file.

## Waterfront promenade joined (2026-09-27 21:4x)

Player in chat: fix the waterfront, then a monument plan for walking and transit. Two promenade pieces were on opposite sides of Waterfront Drive, so a ground path was refused (`pathOnRoad` on 2813 and 14368).

- Pedestrian Elevated **6783** from node 2764 to 14049, elevation 4 m, createdNodeIds []. The west and mid promenade became one component (16 nodes, x -937..-565).
- Pedestrian Elevated **15638** from node 3863 to 5958, elevation 4 m, createdNodeIds []. That joins the central promenade (x -579..-493) across drive segment 34319.
- Sidewalks were already continuous: harbor node 12092 to metro drive 4925 is 6 hops; 4925 to the footbridge avenue 3420 is 9 hops.
- The quay's 9 m gap to its path was left alone. It is the lane link.

Monument sites are §10.14 of `portville-master-plan.md`. Three lots validated clean and not built: fountain (-440,760), fountain (1640,680), fountain (1634,1841), cinema (1690,41). West Gate has no clean lot.

## Monuments attempted, industry bus extended (2026-09-27 21:5x)

Player: place the monuments, plan transit around them, and serve industry.

The four station lots do not have a road close enough for a unique. A fountain placed 52 m off Waterfront Drive (26090) came back RoadNotConnected and was removed. Cinema, Modern Art Museum, and Friendly Neighborhood had no clean lot on the station roads. Working monuments stay the ones already connected: City Hall 19581, SeaWorld 48833, and the older towers.

Industry: 386 generic plants. The east block around (2800,400) and the south block around (2200,-200) were more than 400 m from B4. Added stops on line 21 at Oscar White Avenue (segment 19806, snap 10 m) and segment 7418 (snap 23 m). B4 is Complete, 9 stops, 14/14 buses, 9,946 m, no problems. The (1600,800) plants are 83 m from the north train, not from this bus.

## West-bank station road (2026-09-27 22:1x)

Mary Morgan Boulevard was 87 m north of metro entrance 41930. Medium Road 25218 joins boulevard node 25869 to new node 21352, and Medium Road 6000 continues to node 5104 at (−668,−272), 10 m north of the station. Water pipe 21839 runs from pipe node 30522 to the new road. A power line was already 50 m away. After the road, the station flags are Created, Completed, Active, ZonesUpdated: no RoadAccessFailed. Garbage, Crime, and MajorProblem are still set. Those are service coverage, not a missing road.

## Westbank Homes grid (2026-09-27 22:2x)

Spine off Mary Morgan node 11251, four east-west streets at z −400/−500/−600/−700 from x −1280 to −560. Power from node 20507, water from node 7717. 1,910 new high-density cells. After about a week, 17 homes were up and residential demand fell from 77 to 49. Two of those homes had a problem. Population 27,134.

## M1 to the airport site (2026-09-27 22:3x)

Metro track from west-bank platform node 12847 to a new station track, segment 46, midpoint (−2228, −356). Flat at y 116.5, turns under 15 degrees, 19 hops from the old platform. Airport Road continues Westbank Homes from node 10709 to that site. Metro Entrance 21346 is at (−2228, −330). Both M1 lines (30 and 173) have the stop, no problems, 8/8 trains, 9,246 m. The Airport building itself was not placed: the dry run was clean and the real placement was rejected. The bridge refuses it because the prefab has 1 sub-building. It has to be placed with the in-game tool. On 2044-05-07 the city had 29,169 people, residential demand 2, commercial demand 50. Added 88 CommercialLow cells at the airport road (20), Mary Morgan (52), and the waterfront (16).

## WB Downtown bus (2026-09-28)

The new west-bank homes at the south and west edge were 470–800 m from the nearest metro stop, which is the ride to Central. Bus line 171, WB Downtown, stops on the west street (segment 7468), the middle of the grid (5225), and the station road (25218). Snaps 0 / 0 / 12 m. Complete, no problems, 2,717 m, 3/7 buses after a short run. One transfer at the west-bank metro to Central.

## Westbank and North train capacity (2026-09-28 20:39 local)

Player message #7: focus interconnectivity between attractions, industry, travel hubs, and congested roads. Live game 2045-12-11, 42,966 citizens. The existing network already connects the Harbor and promenade via H1/M1, East Works to Riverside East via B4, and Westbank to its metro via WB Downtown. All 12 transit lines were complete. Traffic flow was 67% at inspection (66% on follow-up); 31 road segments were at 80–100 density. Persistent hotspots include the Holmes/Stephen Harris corridor (segments 589, 6129, 11388) and Riverside East station approach (15491, 32290). The known Riverside East forecourt widening is still blocked by the adjacent shop identified in §10.17; no road was changed this pass.

- Raised WB Downtown bus line 171 budget from 150% to 250%. It recalculated to 11/13 buses (was 8/8), remained Complete, and retained its three stops linking Westbank homes to the M1 entrance.
- Raised T1B Central > North > Riverside East budget from 150% to 200%; it remained Complete at 2/2 trains and 3 stops.
- Left WF1 and B4 budgets and roads unchanged: waterfront avenue congestion is high, and B4 already joins East Works to the Riverside East train hub.
- Saved checkpoint `Portville-Interconnectivity-2045.crp` (14,434,670 bytes, written 2026-09-29 01:39Z). The original `Portville.crp` remains present.

Open: the 100-density Holmes/Stephen Harris and Riverside East approaches still need a locally validated access fix; city flow remains below the 75% target. Recheck traffic after the higher feeder frequency runs for several in-game periods.
