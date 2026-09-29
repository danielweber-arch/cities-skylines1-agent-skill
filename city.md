# City: Portville

Goal: 100,000+ population, no unaddressed problems
Style: Transit-oriented core
Layout: one Medium Road spine ("Station Avenue") from the north highway stubs east to a rail station on the mainline; densest zoning within 400 m of stations, RL outward; later a metro trunk under the river to the SW bank and a second mainline station in the south
Industry: generic, small, north of the core beside the highway interchange (160 m gap, offices as buffer once unlocked); sustainability first (wind/solar, no coal)
Must have: power, water, sewage, garbage, health, fire, police, education to "highly educated" (elementary + high school + university coverage), deathcare, parks/playgrounds (youth), tourism hooks at stations, buses first then train/metro
Avoid: jobs zoned ahead of homes; zoning before pipes and power; sewage upstream of the intake; placing buildings the game has not unlocked; >80% of cash committed at once
Save cadence: overwrite the single working save `Portville` after every verified step (NOT per-phase saves); confirm the file's mtime is newer than the request before announcing
Map: new map, highways + mainline only | Save file: Portville | Started: 2026-09-27

## Research summary
- Reused from city-ashford.md: 80 m grid spacing (16 m road + 2 x 32 m zone depth); `Basic Road` ~₡40 per 8 m cell, `Medium Road` ~₡60 per cell [E]; start cash ₡70k in vanilla [E]; R:I:C early block ratio ~5:2:1; residential chunks <= 150 cells; industry >= 2 blocks from housing.
- Build costs read from validated dry runs [M, `constructionCost`/100]: Wind Turbine ₡6,000; Water Intake ₡2,500; Water Outlet ₡2,500; Bus Depot ₡30,000; Solar Power Plant ₡80,000; Coal Power Plant ₡19,000.
- tampa-master-plan §1 [E]: ~4.8 residents per home at maturity; new-district yields RH 0.70 and RL 0.18 homes per zoned cell; target zoned-cell ratio R 100 : C 22 : I 18 : O 50 (RH:RL ~3:1). 100k needs ~21k homes, ~37k residential cells, ~9 km2 gross land at 50% zoned share.
- §4.7 transit mechanics [M code]: riders counted per boarding (a transfer counts twice); every feeder ends at a trunk station; stops 200-250 m apart; never give a bus two stops within walking distance of two stations of the same trunk line (it steals riders); trips throttled by the active vehicle count, so moving car trips to transit raises trips for everyone; walking legs capped at 1 km.
- Proven rules (lessons.md): rail/metro node turns <= 40 deg, 30 m straight at platform ends; one metro line per direction; route bus stop orders with U-turns forbidden (tmp/tampa/p1t/route3.py) before any dry run; bisect failing lines with 2-stop test lines.
- Log rules that apply from day 1: probe build-grid roads for lanes (tmp/tampa/w1/laneprobe.py, repair touch.py); pipe and power every road before painting its blocks; zone before stringing power lines (poles block set-zone); collision guard = ok && canPlace && toolErrors==[] && collidingBuildingIds==[] && collidingSegmentIds==[] (m1.py `clean`); page list endpoints by service and assert returned == total; stage commercial with residential (1 C block per 3-4 R blocks).
- This install uses the EU service set: `Elementary_School_EU`, `highschool_EU`, `University_EU`, `medicalclinicEU`, `hospital_EU`, `firehouse_EU`, `Fire_Station_EU`, `police_station_EU`, `Police Headquarters EU`. The TAmpa names (`Elementary School`, `Fire House` ...) do NOT exist here [M].
- Milestone unlocks [E, vanilla]: Little Hamlet (~440) clinic, elementary, landfill; Worthy Village (~990) fire, police; Tiny Town parks; Boom Town bus depot, cemetery; Big Town (~7.5k) high density, offices; train and metro between Busy Town and Small City. place-building does NOT check unlocks or money [M, docs/api.md], so the builder places a service only after the player confirms it is unlocked. Transit unlocks ARE readable: `/state/transit` transportPrefabs[].unlocked (now Bus false, Train false, Metro false, Airplane true, Ship true) [M].

## Site survey
- Sim state at survey [M]: NOT paused (paused false, speed 1, game date 2026-10); 0 buildings, 0 zones, 0 lines; demand R 100 / C 0 / W 0; taxes 9% all; policies none. `citizens.count` rose 780 -> 1,132 with zero buildings: pass-through highway travellers, not residents. Only problems: 4 RoadNotConnected on the highway stub ends (pre-existing).
- Owned tiles [M, validated Wind Turbine dry run: `OutOfArea` = not owned, 80 m grid]: SIX tiles already owned: centre (x -960..960, z -960..960), W (x -2880..-960, z -960..960), N (x -960..960, z 960..2880), S (x -960..960, z -2880..-960), E (x 960..2880, z -960..960), SE (x 960..2880, z -2880..-960). NOT owned: NW, NE (x 960..2880, z 960..2880), SW, and every tile of the outer ring sampled (1,665 outer points, all OutOfArea). Owned area ~21.3 km2, of which ~3.0 km2 water [M count x 0.0064 km2].
- River [M]: ~650-800 m wide, runs NW-SE through the centre tile: water x -960..-240 at z 400; x -320..320 at z 0; x 320..960 at z -480; x 1440..2000 at z -1520; lake/wide section x -2800..-1100, z 700-1300 in the W tile. Water surface (validated shore dry runs) 112.6 at (1131,-619), 104.6 at (-558,608), 99.0 at (-994,828) [M]; 80 m grid surface ~131 in the far SE to ~92 in the far NW [M]. Flow SE -> NW [E, from the surface gradient]: intake upstream = SE, outflow downstream = NW.
- The river splits the city: NE bank (centre-tile NE, E tile, N tile) holds the north highway stubs and the mainline; SW bank (W tile, centre SW, S tile) holds the south highway stubs and the biggest flat land.
- Highway entry nodes [M, degree-1 nodes traced to their interchanges]:
  - North (component "Albert/Hunter", outside at N edge (-4360,8649) and E edge (8649,-3710)): node 9971 (74,864) y 144 "Holmes Highway" and node 22760 (107,875) y 147 "Underhill Highway"; 5 segments north to the interchange ramps at ~(0..240, 1335..1545).
  - South (component "Crowley/Manor", outside at S edge (4350,-8649) and W edge (-8649,-6650)): node 8924 (-124,-898) "Graham Highway" and node 17500 (-94,-898) "Sunset Highway"; interchange ramps at ~(-340..-44, -1506..-1280).
  - The two highway components are separate; joining them needs a city road bridge over the river (later phase).
- Rail [M]: one mainline (`Train Track`, with `Train Track Elevated` on the river bridge), outside connections E edge (8644,6288) and S edge (4009,-8644). Through the owned area: NE bank x 1506 -> 2204 from z 935 to -959, nearly straight (node turns 1.4-2.8 deg per ~90 m) -> bends 7.7 deg/node round (2200,-900) -> elevated bridge over the river (1952,-1432)..(1413,-2418) -> SW bank straight (1391,-2494)..(1368,-2570) turn 0.0 -> leaves the owned area at z -2880 (x ~1320). North of z 960 (x ~1510) it is in the unowned NE tile.
- Other connections [M]: ship paths only in the far W (-8653,-2270); airplane paths at W/E/S edges. No existing stations.
- Terrain NE core [M, terrain y on the 80 m grid]: z 790 row: y 143 at x 160 -> 180 at x 720 -> 175-177 at x 1000-1400 (west part ~6.5% grade down to the river, east part nearly flat). Highest point of the owned area ~261 (S tile SW hills); flat buildable (80 m cell slope <= 5%) ~1.9 km2 SW bank centre+W, 1.8 km2 S tile, 2.3 km2 N tile, 0.9 km2 NE core [M counts].
- Wind: not exposed by the bridge [gap].
- Funds: not exposed by any endpoint; assume ₡70k start + milestone cash [E]; ask the player whether Unlimited Money is on.
- Prefabs to use (all in the live lists [M]): road=`Basic Road` (locals), `Medium Road` (spine/collectors), `Large Road` (later arterials), `Medium Road Bridge`/`Medium Road Elevated` (river crossing); power=`Wind Turbine`, later `Advanced Wind Turbine`, `Solar Power Plant`, `Fusion Power Plant`/`Nuclear Power Plant` if needed; network=`Power Line`; water=`Water Intake`, `Water Tower`, `Water Pipe`; sewage=`Water Outlet`, later `Water Treatment Plant`; garbage=`Landfill Site` then `Combustion Plant`; school=`Elementary_School_EU`, `highschool_EU`, `University_EU`, `Library 01`; fire=`firehouse_EU`, `Fire_Station_EU`; police=`police_station_EU`, `Police Headquarters EU`; health=`medicalclinicEU`, `hospital_EU`, `Child Health Center 01`, `Eldercare 01`; death=`Cemetery`, `Crematory`; parks=`Regular Park`, `Regular Playground`, `Regular Plaza`; transit=`Bus Depot`, `Train Station`, `Metro Entrance`, `Metro Track`, `Train Track`.
- Survey files: tmp/transitcity/survey/ (heights80.json: t = terrain y, s = surface incl. water, e = tool errors; 8,000 points incl. the full 9-tile block; land9.py map; topo.py/trace.py highway and rail traces; rail_poly.json; d1_grid_dry.json).

## Transit-first spine design
> Superseded in part 2026-09-27 by the master plan: see "Transit-first spine design (rev. 2026-09-27)" right after this list, and `portville-master-plan.md` §3. The original list is kept below.
1. Central Station (trunk hub): `Train Station` (16x6) beside the mainline on the NE bank, lot ~x 1440-1500, z 700-830, parallel to the track (track heading ~7 deg west of north here: (1506,935) -> (1548,596)). Reserve x 1400..1560, z 560..950 now: no zoning, no roads except the spine end node (1400,790), which becomes the station plaza. Built when Train.unlocked is true.
2. Station Avenue: `Medium Road` z = 790 from the highway merge (90,790) to the plaza (1400,790), nodes every 80 m so every grid snaps onto it. It is the bus trunk until the train opens.
3. Station-area density: within 400 m of Central (x >= 1000) = RH + CH + a Metro Entrance, zoned only once high density unlocks (reserved until then); 400-800 m (x 600-1000) = RL/CL now, upzone later by player decision; > 800 m = RL; offices on the station quarter's north and south edges (buffer); industry 160 m north of the core beside the highway interchange.
4. Road hierarchy: highway -> Station Avenue (Medium) -> collectors every 400 m (Medium: x 520, x 1000) -> `Basic Road` 80 m grid locals. No junction within 300 m of the highway merge except the spine continuation (first grid node at x 200, 110 m, is a straight continuation; the first cross junction is x 520).
5. Metro M1 (when Metro.unlocked): under Station Avenue from Central west (stations ~Central (1440,760), (900,760), (380,760)), under the river to the SW bank ((-300,-400), (-900,-600)), south to the S tile ((-400,-1500)) and on to a second mainline station South (`Train Station` at ~(1360,-2600), rail straight there). Two lines (one per direction), stations 500-600 m apart, bends <= 40 deg.
6. Feeders: every bus line ends at Central or a metro station; stops 200-250 m; no bus stop pair duplicating a metro hop.
7. SW bank opens when M1 reaches it plus a road bridge (`Medium Road Bridge`, ~700 m; narrowest measured water span 640 m at z 0, x -320..320) that also joins the two highway components.
8. Tile purchases (player; the bridge cannot buy) [E]: 6 tiles are enough land for 100k at TAmpa yields; buy NE (x 960..2880, z 960..2880) first for a north mainline station and the rail corridor, then SW (flat 150-160 m land south-west) only if the level-up programme falls short.

## Transit-first spine design (rev. 2026-09-27, portville-master-plan.md §3)

1. **Central Station:** a through `Train Station` hosted on the mainline between 20344 (1511,851) and 17129 (1533,679), centre ~(1522,765), replacing host segs 15554/16776 (B11 pattern). Built at Train unlock (P3). The forecourt road is built after the validated dry run. The reserve is now x 1400..1600, z 470..950, and the player's power line in it is rerouted in P2.
2. **Station Avenue:**
   - x 200..520: `Medium Road`, the car feed from IC West (IC West built 14:16–14:19: collector x 168..200, NB ramps, at-grade SB crossover 24949–6104).
   - x 520..1000: "Promenade West", rebuilt in P1 as `Small 4 Lane Road with Bus Lanes` while nothing fronts it.
   - x 1000..1400: becomes **the Mall** (`Pedestrian Pavement`, car-free) in P2.
3. **Downtown Ring** (cars go round the Promenade): x 520 collector; Ring South z 470; Ring East/Station Road x 1400; Ring North z 950. All `Medium Road`.
4. **Central Promenade** (player requirement): the Mall z 790, x 1000..1400, between flank streets z 710/870 (`Small 4 Lane Road with Bus Lanes`). Uniques and plazas ≤ 8 cells deep face the flank streets. The Culture Quarter superblocks (x 1000..1400, z 470..630, no z 550 road) take deep anchors. Slot and prefab list in the master plan §3.8.
5. **Metro:**
   - M1 E–W: Central-M1 (1360,722), Civic (1020,722), West Gate (560,806), Riverside NE (200,330), under the river to Riverside SW (−560,−330), Westbank Centre (−1150,−350), Westbank West (−1750,−350), Airport (−2300,−360).
   - M2 N–S: Northfield N (−100,2400), Northfield S (−100,1850), North Gate (450,1200), Central-M2 (1392,560), Riverside East (1200,100), under the river to Centre-South (700,−900), Southfield (300,−1700), South (1300,−2520).
   - One line per direction; bends ≤ 40°.
6. **Rail:** Central (P3), South (1368,−2570) (P5), North (NE tile ~(1565,1925)) (P4). `Cargo Center` in E1 ~(1990,−170) on a spur, east of the rail.
7. **Airport** (−2350,−450) [M validated]; **Harbor** (−2077.2,699.6) angle −151.7 [M validated: shore, dock reaches the ship lane]. Both P4.
8. **Feeders:** every bus line ends at Central Plaza or a station; stops 200–250 m; routed with U-turns forbidden. B1 at Bus unlock; B2 Riverside and the C1 Promenade Circulator in P2.
9. **Tiles:** buy **NE first** (3.3 km² flat/moderate; the downtown's north growth, the North station, and the office land the 6 tiles lack), then SW, then NW.
10. **Industry** goes to E1 (east of the rail, with Nuclear 31187 / Solar 45983, the Cargo Center and IC East). D2 keeps only a 3-cell starter in its west half; its east half becomes offices.

## Districts
### D1 - Station Avenue core (NE bank)
- bbox: x[600,1000] z[630,950] first grid; later x[200,1400] z[470,950] (dry run of the full 15x6 grid at origin (200,470): 112 nodes, 201 segments, 90 blocks, ok [M])
- zones: RL/CL now (x 600-1000), RH/CH/O in x 1000-1400 after Big Town
- depends on: P1 spine

### D2 - North works (industry)
- bbox: x[200,680] z[1110,1350] (build-grid origin (200,1110), cols 6, rows 3, spacing 80, `Basic Road`)
- zones: Industrial only; landfill and turbines beside it
- depends on: collector x 520 from the spine; W demand > 30

### Later: D3 Central quarter (x 1000-1400), D4 SW bank Riverside (x -1500..-80, z -900..0), D5 S tile (x -960..960, z -2800..-1100) with South station, D6 N tile (x -900..900, z 1450..2800, west of the interchange).

## P1 concrete build list (in order; dryRun every call, opId on every grid)
1. Highway merge [M dry runs passed]: `Medium Road` (74,864) -> (90,790) and (107,875) -> (90,790). Must reuse nodes 9971 and 22760 (createdNodeIds must NOT contain an endpoint at the stubs). Accept: external-connections localRoadComponents 1, disconnected 0; RoadNotConnected count drops from 4 to 2.
2. Station Avenue: `Medium Road` (90,790) -> (200,790) [M dry run ok, y 139.8 -> 143.8], then 15 pieces of 80 m (200,790) -> (280,790) ... -> (1400,790).
3. Collector x 520: `Basic Road` (520,790) -> (520,870) -> (520,950) -> (520,1030) -> (520,1110) (80 m pieces; later upgraded to Medium).
4. D1 grid: build-grid `Basic Road`, origin (600,630), cols 5, rows 4, spacing 80, snapDistance 8, opId `portville-d1-grid-01`. Row z 790 must reuse the six spine nodes x 600..1000. Then run laneprobe.py on the new segments and touch.py if any have no lanes.
5. Power: `Wind Turbine` at (760,1200) [M clean], second at (840,1200) [M clean] only when NoElectricity appears or before D2 opens. `Power Line` (760,1200) -> (760,958) after the first zoning (poles off block centres; end within 15-20 m of the first buildings).
6. Water supply: `Water Intake` at (1150,-600) validated -> placed at (1131.1,-619.1) angle -46.2, shore, 13.6 m above water [M]. `Water Pipe` from there in 80 m pieces to the grid corner (1000,630), then under every D1 road.
7. Sewage: `Water Outlet` at (-560,600) validated -> (-558.2,608.2) angle -29.0, shore [M], ~2 km downstream of the intake. `Water Pipe` (-558,608) -> (200,790) -> along the spine to (600,790).
8. First zoning (after pipes and power reach the block; D1 lattice block centres; radius 34 paints one lattice cell ~100 cells; chunk rule below):
   - Reserved for services, never zone: (880,910) Elementary, (880,670) Clinic, (640,830) Fire, (960,830) Police.
   - CommercialLow: (800,750), (800,830) (spine frontage), only when C >= 30, one per 3-4 open R cells.
   - ResidentialLow, in this order: (720,750), (720,830), (640,750), (720,910), (640,910), (800,910), (720,670), (640,670), (800,670), (880,750), (880,830), (960,750), (960,670), (960,910).
   - D2 Industrial (after its grid, pipes, power): (280,1230), (360,1230), (440,1230), (280,1310), (360,1310), (440,1310), only when W >= 30.
9. First services (place only when the player confirms the unlock; validated dry run + clean guard; try angles 0/90/180/270 and keep the lot facing a Basic Road, not the spine):
   - Little Hamlet: `Elementary_School_EU` in block (880,910); `medicalclinicEU` in block (880,670); `Landfill Site` north of D2 at ~(440,1390) facing the z 1350 road (needs D2's north row built first).
   - Worthy Village: `firehouse_EU` in block (640,830); `police_station_EU` in block (960,830).
   - Tiny Town: `Regular Playground` + `Regular Park` in the 160 m buffer between D1 and D2 along collector x 520 (~(560,1030)).
   - Boom Town: `Cemetery` beside D2 on collector x 520 (~(480,1030)); `Bus Depot` at (660,982) north of row z 950 [M clean, angle 0 or 180].
10. First bus (when Bus.unlocked and the depot exists): line B1 D1 loop ~1.6 km, stops ~200 m apart in travel order (points 4 m off the road centre on the travel side): (700,786), (900,786) on Station Ave; (1004,870) on x 1000; (900,954), (700,954) on row z 950; (596,870) on x 600. Route the order with route3.py (no U-turns) first; dry-run; reject any stop with segmentId 0 or snap > 32 m. When Central opens, extend B1 east to terminate at the plaza (1400,790).
11. Budget at P1 [E]: spine+merge 1.47 km Medium ~₡11k, D1 grid 44 new 80 m segments ~₡17.6k, collector ~₡1.6k, 1 turbine ₡6k [M], intake + outlet ₡5k [M], ~4.4 km pipe ~₡11k [E] = ~₡52k; keep ₡14k reserve; the second turbine, D2 and services come out of milestone cash.

## Demand gate (every phase)
- Residential: one chunk (<= 150 cells, i.e. one lattice cell) per game week per district while R >= 40; stop at R < 20. Every road of the chunk has pipe and power first.
- Commercial: one C cell per 3-4 R cells actually open, and only while C >= 30. Never zone jobs ahead of homes.
- Industrial / Office: only while W >= 30; industrial cells <= 20% of open residential cells; offices preferred for new jobs once unlocked.
- Re-read demand, problems (NoWorkers, NoGoods) before each chunk; if NoWorkers appears, pause jobs zoning.

## Phases
> Superseded 2026-09-27 by "Phases (rev. 2026-09-27)" below and `portville-master-plan.md` §6. The original list is kept.
- [ ] P1 Seed town (highway merge, Station Avenue, D1 grid, collector, turbine, intake, outlet, pipes, first zoning, Little Hamlet/Worthy Village services, D2 when W demand appears, B1 at Boom Town) - accept: cityConnectedToOutside true, localRoadComponents 1, road_anomalies(includeDeadEnds=false).total 0, building_anomalies 0, no NoWater/NoElectricity/NoSewage after 2 game weeks, every unlocked service placed and connected, B1 running with no LineNotConnected and vehicles = target (once Bus unlocked), save `Portville` verified by mtime
- [ ] P2 Town to ~7.5k (Big Town): D1 west (grid origin (200,470) remainder), rows z 470-630, second turbine/solar, Water Tower backup, schools to 100% of homes within 500 m, parks per family block <= 400 m, B2 feeder, all ending at the plaza - accept: R > 0, W >= 0, NoWorkers <= 3, elementary coverage 100%
- [ ] P3 Central Station + station quarter (Train.unlocked): `Train Station` at x 1440-1500 z 700-830 on/beside the mainline (bends <= 40 deg, straight platform ends), D3 RH/CH/O within 400 m, `highschool_EU` <= 250 m from the platform, `Library 01`; buses re-terminated at Central - accept: train line 2/2 vehicles no problems, station walk-in ok, riders > 0 after 3 weeks
- [ ] P4 Metro M1 + river crossing (Metro.unlocked): M1 Central -> SW bank (two lines, one per direction), `Medium Road Bridge` joining both highway components, D4 Riverside zoned station-first; `University_EU` at an M1 station - accept: M1 both directions 0 problems, disconnectedLocalRoadComponents 0, ~30k
- [ ] P5 South: M1 to the S tile and South `Train Station` (~(1360,-2600)), D5, `Water Treatment Plant` downstream (NW), `hospital_EU`, fire/police HQ - accept: ~60k, modal share tracked
- [ ] P6 Grow to 100k: D6 N tile, level-up programme (education, parks, land value near stations), tile purchase NE only if land runs short - accept: population >= 100,000 (player UI), problems trending down, all lines healthy, save verified

## Progress
- [x] P1-2 Station Avenue (200,790) -> (1400,790), 15 Medium pieces (west end node 19942 free for the interchange)
- [ ] P1-1 Highway attach / interchange (other builder; west of x 200)
- [x] P1-3 Collector x 520 (Basic, z 790 -> 1110)
- [x] P1-4 D1 grid portville-d1-grid-01 (44 segs, 6 spine nodes reused); lane probe blocked (Bus locked), all 44 nodes touched instead
- [x] P1-5 Wind Turbines (760,1200) + (840,1200), Power Line to (800,962)
- [x] P1-6 Water Intake 1428 + pipe trunk to (1000,630) + pipes under every D1 road; intake powered by its own turbine 29962
- [x] P1-7 Water Outlet 8035 + pipe to (200,790) and under the spine to (600,790); own turbine 6243 (outlet still WaterNotConnected, open)
- [ ] P1-8 first zoning, P1-9 services, D2, P1-10 B1 (not started; wait for the attach and the master plan)
- [x] P1 growth (2026-09-27 14:26-14:58, P1g): one power grid (nuclear+solar+8 turbines), FreeTransport + EducationBoost, Station Ave x 200..1000 = Small 4 Lane Road with Bus Lanes, D2 access road + Landfill 33150, D2 grid + 3 Industrial cells, clinic 22108 (545,894), cemetery 16132 (479,1006); D1 RL zoned (mostly by another hand, incl. frontage); parks, Bus Depot, B1 waiting on unlocks (depot lot (660,982) built over; use (624,1077))
- [x] P2 part 1 (2026-09-27 15:31-15:50, P2 builder): Ring South z 470 (Medium, x 280..1400) + Station Road x 1400 (z 470..790); D1 south grid 5x1, D1 west 3x2, NR north half 13x2 (smaller than plan where built lots blocked); pipes + power; 3 firehouses, 2 elementary, clinic, 2 police, Combustion Plant, 2 parks, 4 playgrounds; demand-gated RL stager running (tmp/portville/p2/stager.py); Bus still locked (no depot/B1); Mall: bridge cannot join paths to road nodes (TODOS)

## Phases (rev. 2026-09-27, portville-master-plan.md §6)
- [ ] **P1 Seed town remainder:**
  - IC West SB flyovers.
  - Nuclear/Solar water pipes; pipes and power on collector x 520 and the IC collector.
  - Promenade West bus lanes (segs 2145, 10008, 13070, 36105, 11599, 12759).
  - D1 RL cells (720,910), (720,670), (800,910), (800,670), (640,910), (960,670); CL (800,750), (800,830). Promenade frontage cells stay unzoned.
  - Services by unlock: Elementary (880,910), clinic (880,670), fire (640,670), police (960,910), landfill, cemetery, depot.
  - D2 starter industry cells (240,1230), (320,1230), (400,1230), (240,1310), (320,1310), (400,1310). These are corrected cell centres; the old list sat on grid lines.
  - B1 plus FreeTransport at Bus unlock.
  - Accept: see the master plan §6 P1.
- [ ] **P2 Town to ~7.5k plus the Promenade skeleton:**
  - Ring South z 470, Station Road x 1400, Ring North z 950.
  - Flank streets z 710/870 (bus lanes); downtown locals (no car street across z 710..870); Culture Quarter street x 1200.
  - The Mall (Pedestrian Pavement x 1000..1400; stop and report if the path will not join road nodes).
  - D1 south/west grids; NR north half; utilities; power-line reroute out of the Central strip.
  - RL zoning outside the reserves; high school at CQ-W, Library at Civic.
  - B1 extension, B2, C1 circulator.
  - Accept: see the master plan §6 P2.
- [ ] **P3 Central + M1 core** (~25k): Central Station, T1, M1 Central–Civic–West Gate, ped bridge to E1, IC North Gate, IC East, NR south, E1, D2 offices.
- [ ] **P4 Both banks** (~55k): M1 under the river to Westbank and the Airport, Harbor + H1, NE tile (North station, downtown north), M2 North Gate–Central–Riverside East.
- [ ] **P5 North and south** (~85k): Northfield + M2 north, CS + Riverside Bridge + M2 south, South station.
- [ ] **P6 100k+:** Southfield, level-up and upzoning programme, reserves only if short.

## Amendments
- 2026-09-27: city renamed from "Transit City" to "Portville" by the player; working save `Portville`.
- 2026-09-27: survey found six tiles already owned and the sim running at speed 1 (the brief said paused).
- 2026-09-27: progress.md still holds Ashford's state; the P1 builder starts a Portville progress file (not overwritten by this planner).

- 2026-09-27: Unlimited Money is on (player). Cash is never a constraint; the P1 budget line is moot.
- 2026-09-27: Player gave full authority over Portville: "maximum transportation efficiency on unlimited budget and population growth". Decision: highway attach = option (a), a full interchange on the Holmes/Underhill highway at Station Avenue west end (200,790), built by the orchestrator's builder.
- 2026-09-27: Player: "You are allowed to delete or change anything you must. You have full decision making control over the city." Demolition/rezoning/highway changes are within authority; demolitions run in the main session.
- 2026-09-27: Master plan written (`portville-master-plan.md`). The spine design and phases are revised above (originals kept). Changes:
  - Station Ave x 1000..1400 becomes the car-free Mall of the Central Promenade (player: downtown attractions corridor).
  - x 520..1000 gets bus lanes in P1.
  - A Downtown Ring carries the cars.
  - Fire and police lots move off the Promenade frontage.
  - Industry moves to E1; D2 cells corrected.
  - NE tile to be bought first.
  - Couplet observed converted to Large Oneway boulevards by the player (14:07–14:17).
