# Portville master plan: 0 to 100k+, transport first

Status: PLAN ONLY. Nothing in this file was built by the planner. It was written 2026-09-27 (game 2027-07)
from live bridge GETs and read-only `dryRun:true` POSTs. Live networks were re-read at 14:08 and at 14:17,
while the interchange builder was working.
Scratch data and scripts: the planner's session scratchpad (`live/`: `map.py`, `land.py`, snapshots).
Survey data: `tmp/transitcity/survey/` (`heights80.json`, `rail_poly.json`, `land9.py`).

Tags:
- **[M]** measured, either live in this game or taken from TAmpa's decompiled-code reads.
- **[E]** estimated; the assumption is named.
- **[G]** given in the brief.
- **[U]** unverified: from memory of the vanilla game, and must be read in-game before anyone relies on it.

---

## 0. Headline

| Item | Value |
|---|---|
| Goal | 100k+ residents, transit first, highly educated, young, tourist-friendly, low-carbon. Unlimited money, full authority. [G] |
| Homes needed | **20.9k** at TAmpa's 4.78 residents per home [E from M], **25k** at 4.0 per new home. Plan to 25k. |
| Zoned cells needed | ~**83k** (R 43.9k : C 9.6k : I 7.9k : O 21.9k), i.e. 5.3 km² net, **~9–10.7 km² gross** [E, TAmpa planning yields] |
| Land | 6 tiles owned [M]. Flat plus moderate buildable land is about **12.4 km²** [M, 80 m survey]. That is enough for the homes, but offices come out about **30% short**. **Buy the NE tile first** (x 960..2880, z 960..2880: 3.3 km² flat/moderate). It carries the mainline north of Central and the downtown's northward growth. With NE the plan holds 33k homes and 30k jobs, about 130k people at 4.0/home [E]. |
| City core | NE bank, between the N–S couplet (x ≈ 60–110) and the mainline (x ≈ 1510–1590). **Central Station** is a through station hosted on the mainline at about (1522,765). **Central Promenade** runs along z 790 from Central west to x 520. |
| Trunk | **Rail**: Central (P3), South (1368,−2570), North (NE tile, ~(1565,1925)). **Metro M1** (E–W): Central → Civic → West Gate → under the river → Westbank → Airport. **Metro M2** (N–S): Northfield → North Gate → Central → under the river → Centre-South → Southfield → South. Every station is on or within one transfer of Central. |
| Modes available | Bus, Train, Metro, Airplane, Ship only [M `/state/transit` transportPrefabs]. **No tram, monorail, trolleybus, cable car or taxi** in this install. Bus, Train and Metro are locked; Airplane and Ship are unlocked [M]. |
| Now [M, 14:20] | 0 zones, 0 growables, 0 lines. Station Avenue (in-game "Pearl Boulevard"), the D1 grid and **IC West** are built: half-diamond on Underhill plus an at-grade SB crossover (progress-portville.md 14:16–14:19). `cityConnectedToOutside` true; roadComponents 1. Demand R 100 / C 0 / W 0. |
| Targets at 100k | Every home within 800 m of rail or metro. Every downtown block within 300 m of a metro/rail stop and 150 m of a bus stop. ≥ 98% of growables within 250 m of a bus stop. Transit riders ≥ 12%/week of population (stretch 18%). Traffic flow ≥ 75%. Vehicles < 10k of 16,384 [E]. |
| Next | Finish P1 (interchange, utilities, bus lanes on Promenade West, D1 zoning by demand, services by unlock). Then P2 (downtown ring, Promenade, D1 south/west, Riverside NE north half, B1/B2). The lists are in §6. |

---

## 1. Capacity math

### 1.1 Yields reused from TAmpa (tampa-master-plan §1) [M in TAmpa, E here]

| Quantity | TAmpa measured | Planning value for Portville | Why |
|---|---|---|---|
| Residents per home | 4.78 [M/G] | 4.0 for new households | New cities start with smaller households. Plan to the lower value. |
| RH homes per zoned cell | 0.975 mature | **0.70** | New districts start at L1 (72% of mature). |
| RL homes per zoned cell | 0.212 mature | **0.18** | 85% of mature. |
| Jobs per zoned cell | CL 0.61, CH 0.95, O 0.77, I 1.17 | **CL 0.49, CH 0.76, O 0.62, I 0.94** | 80% of mature. |
| Zoned share of gross land | 50% (45% on hills) | 50%; 40% downtown; 45% on hills | An 80 m grid is 64% zone blocks before arterials, parks and stations. |
| Commercial need | ~1 shop job per 8 homes, plus a tourist term [M code] | C = 22% of R cells | Tourism from the Promenade, the airport and the harbor adds to the resident term. |

### 1.2 What 100k needs [E]

- **Homes:** 100,000 / 4.0 = 25,000.
- **Residential cells:** at RH:RL = 3:1 by cells, 0.75·0.70 + 0.25·0.18 = 0.57 homes per R cell, so 25,000 / 0.57 = **43.9k** R cells (RH 32.9k, RL 11.0k).
- **Job zones:** the TAmpa ratio R 100 : C 22 : I 18 : O 50 gives C 9.6k, I 7.9k and O 21.9k cells, for **27.4k jobs** (1.10 per home).
  - C 6.5k (CH 70%), I 7.4k, O 13.5k.
  - Service jobs (schools, hospitals, stations) come on top.
- **Total:** 83.3k zoned cells = **5.33 km² net**. That is 10.7 km² gross at 50% zoned share, or 8.9 km² at 60%.
- **Mature yields:** at mature values the same R cells hold ~34k homes. Level-ups (education, parks, land value near stations) are the reserve.

### 1.3 District capacity [E, planning yields]

Usable land = flat (slope ≤ 6% per 80 m) plus moderate (≤ 10%), owned, not water, and more than 40 m from any highway, rail, one-way or large road [M, `land.py`].

| District | Box | Usable km² | Zoned share | Zoned cells | Mix (by cells) | Homes | Jobs | People @4.0 | Phase |
|---|---|---|---|---|---|---|---|---|---|
| D1 Station Ave West | x 200..1000, z 470..950, less the Promenade strip | 0.33 | 55% | 2.8k | RH 45, RL 20, CH 10, O 25 | 1.0k | 0.7k | 4.0k | P1–P2 |
| DT Downtown + Promenade | x 1000..1400, z 470..950, plus strip x 520..1000, z 710..870 | 0.30 | 40% | 1.9k | RH 35, CH 30, O 35 | 0.5k | 0.8k | 1.8k | P2–P4 |
| D2 North Gate | x 160..1000, z 1030..1400 | 0.29 | 55% | 2.5k | I 35 (west), O 50, RH 15 | 0.3k | 1.6k | 1.0k | P1–P3 |
| NR Riverside NE | x 280..1400, z −400..470 (shore-limited) | 0.73 | 50% | 5.7k | RH 50, RL 20, CL 10, O 20 | 2.2k | 1.0k | 8.8k | P2–P3 |
| E1 East Works | x 1600..2400, z −600..960 (east of the mainline) | 0.90 | 50% | 7.0k | I 55, O 35, CL 10 | 0 | 5.5k | 0 | P2–P4 |
| **NE North Central (buy)** | x 960..2880, z 960..2880 | 3.30 | 50% | 25.8k | RH 35, RL 10, CH 10, O 45 | 6.8k | 9.2k | 27.1k | P3–P5 |
| SW Westbank | x −2880..−160, z −960..640 | 2.73 | 50% | 21.3k | RH 50, RL 20, CL 10, O 20 | 8.2k | 3.7k | 32.9k | P4 |
| N1 Northfield | x −960..960, z 1600..2880 | 2.16 | 50% | 16.9k | RH 50, RL 20, CL 10, O 20 | 6.5k | 2.9k | 26.1k | P4–P5 |
| CS Centre-South | x −80..1300, z −1500..−300 | 0.79 | 45% | 5.6k | RH 50, RL 20, CL 10, O 20 | 2.1k | 1.0k | 8.6k | P5 |
| SF Southfield | x −960..1400, z −2880..−1300 (S tile + SE tile west of the rail) | 2.36 | 45% | 16.6k | RH 40, RL 25, CL 10, O 10, I 15 | 5.4k | 4.2k | 21.6k | P5–P6 |
| **Total** | | 13.9 | | 106k | | **33.0k** | **30.5k** | **132k** (158k @4.78) | |

- **Without the NE tile:** 26.2k homes (enough for 100k at 4.0) but only 21.3k jobs (0.81 per home). That is why NE is bought, and why offices dominate its mix.
- **Also usable:** SE2 (x 2000..2880, z −2880..−960, east of the river, 0.77 km²) is a reserve. The NW and SW tiles (2.3 and 2.9 km² flat/moderate) can be bought after NE.

---

## 2. Land, districts and tiles

**Geography [M, 80 m survey and live networks]:**
- **River:** 650–800 m wide, running NW to SE. It flows from the SE (surface y ~131) to the NW (y ~92), into a lake in the W tile (x −2880..−1100, z 640..1300) that opens west to the ship lane.
- **N–S couplet:** Holmes (SB, x ≈ 63–74) and Underhill (NB, x ≈ 96–107) link the north interchange (x 0..240, z 1335..1545) to the south interchange (−340..−44, −1506..−1280). They cross the river on Holmes/Underhill Bridge (x −58..40, z −313..287).
  - **Changed live at 14:07–14:17 [M]:** the player converted all of Underhill, and Holmes north of z 964 plus both bridges, to `Large Oneway Decoration Trees` / `Large Oneway Bridge` on the same nodes.
  - Holmes is still `Highway with Bus Only Lane Barrier` on z 276..964 and south of z −117 (progress-portville.md 14:16). So the couplet is now mostly a surface boulevard.
- **E–W highway:** Albert/Hunter runs along z ≈ 1440–1520 from x −1000 to 1700, then SE through the E tile (x 2100..2880, z 1440..−80) to the east edge.
- **Crowley/Manor highway:** runs from the W edge along z ≈ −800..−960 to the south interchange, then diagonally SE through the S and SE tiles, (−200,−1450) → (1900,−2880).
- **Mainline:** x 1506..1590 on the NE bank, nearly straight (node turns ≤ 2.8° from z 1287 to −520). It bends 7.7° per node round (2200,−900), crosses the river elevated (1952,−1432) → (1413,−2418), then runs straight to (1368,−2570) with turn 0.0°.

**Districts and order.** Densest land goes to the NE bank first, because that is where the rail is. The SW bank opens when M1 and a second access exist.

| Order | District | Why this order |
|---|---|---|
| 1 | D1 + DT (NE core) | Road-connected now; Central and the Promenade are here. |
| 2 | D2 North Gate | Next to the interchange. Starter industry (west half) for early W demand; offices later. |
| 3 | NR Riverside NE | Flat land between the core and the river; within 800 m of Civic/Central and M1 Riverside NE. |
| 4 | E1 East Works | All industry, the Cargo Center, the power plants (the player already put Nuclear 31187 and Solar 45983 here) and a truck interchange on the E highway. It is east of the rail, so trucks never cross the core. |
| 5 | NE North Central | Downtown extension north of z 960 and the North station. Needs the tile purchase. |
| 6 | SW Westbank | Largest flat block (1.59 km² in SW1). Gets M1, the Airport and the Harbor. |
| 7 | N1 Northfield | North of the E–W highway. Gets M2. |
| 8 | CS, SF | Centre-South and Southfield, with M2 south, the South station and the Riverside Bridge. |

**Tile purchases** (player, Areas panel; the bridge can neither read nor buy tiles):
1. **NE now**, as soon as the game allows. Reasons: 2.8 km² flat; downtown can only grow north past z 960 there; the North station site is on straight track (29382 (1562,1878) → 9134 (1569,1976), turn 0.3°); it holds the job land the 6-tile plan lacks.
2. **SW**, if P5 falls short (1.75 km² flat south-west of Westbank; the Crowley highway is on its NE edge).
3. **NW** last (lake shore; 0.8 km² steep).

---

## 3. Transport master plan (the core)

### 3.1 Principles, from TAmpa's measured mechanics (tampa-master-plan §4.7.1) [M code]

- **Why cims ride:**
  - Transit wins because most cims have no car (car ownership 0–20%, set by age) and because every walking leg is capped at 1 km.
  - The only direct preference lever is Free Public Transport (×0.75 path cost). Turn it on the moment it unlocks (`set-policy FreeTransport`).
- **How riders are counted:** per boarding. Every feeder ends at a trunk station; no bus runs beside a trunk within ~150 m (Proven Rules B18/B27).
- **Stops and routing:**
  - Stops 200–250 m apart.
  - Route every bus stop order with U-turns forbidden (`tmp/tampa/p1t/route3.py` plus `b27/route2.py`) before any dry run.
- **Rail and metro geometry:**
  - Every node turn ≤ 40°, 30 m straight at each platform end.
  - One metro line per direction, each station once per line.
  - Plan legs with `tmp/tampa/metro/b16plan.py`; check bends with `bends.py`.
- **Trip throttle:** trips are throttled by the active vehicle count (p = 1 − vehicles/16,384). Every car trip moved to transit raises trips for everyone. Prefer offices over industry; keep fleets lean.

### 3.2 Trunk network

**Rail, on the existing mainline:**

| Station | Site [M track] | Prefab | Phase / trigger | Serves |
|---|---|---|---|---|
| **Central** | Hosted on the mainline between nodes 20344 (1511,851) and 17129 (1533,679). Centre ≈ (1522,765), heading ≈ 174°. Replaces host segments 15554 and 16776 (B11 pattern). **Confirmed at about city.md's site, moved onto the track:** a validated dry run at (1500,765) hits only track 15554/16776 and the player's power-line segments 7230/19485 [M]. | `Train Station` | P3, when `Train.unlocked` | Downtown, the Promenade, E1 (pedestrian bridge) |
| **South** | Hosted on the straight (1391,−2494) → (1346,−2645), turn 0.0° [M]. Centre ≈ (1368,−2570). | `Train Station` | P5 | Southfield (SF), SE1 |
| **North** | NE tile, hosted on 29382 (1562,1878) → 9134 (1569,1976), turn 0.3° [M] | `Train Station` | P4, after the NE purchase | NE North Central |
| Riverside East (optional) | (1721,78), turns 1.4° [M] | `Train Station` | P5, only if E1/NR riders justify it | E1 south, NR east |

- **Passenger line T1 (South ↔ Central ↔ North):** out-and-back; Train Stations have two platforms. Intercity trains use the E-edge (8644,6288) and S-edge (4009,−8644) connections [M]; Central becomes the intercity arrival hub.
- **Cargo is kept off the passenger platforms:**
  - `Cargo Center` (16x8) in E1, **east of the mainline, south of Central**, at about (1990,−170). A dry run at (1920,−250) collided with track 119/908 [M], so shift ~70 m east.
  - It connects by a spur (turnout ≤ 40°) off the straight between 16107 (1761,−13) and 9170 (1893,−278).
  - Cargo from the S edge then never reaches Central. Cargo from the E edge passes Central's station track; that is accepted, and watched in P4 (risk §7).
  - Trucks from the Cargo Center use the **E1 interchange** on the E highway, never the core.

**Metro.** Tunnels at elevation −12; M2 at −24 where it crosses M1. One line per direction. Station platform centre sits 12 m behind the entrance lot, ±72 m along it [M TAmpa].

**M1 East–West, "Promenade–Airport" (lines M1W and M1E):**

| # | Station | Entrance lot (road it fronts) | Spacing | Notes |
|---|---|---|---|---|
| 1 | Central-M1 | (1360,722), north side of Promenade South (z 710) | — | ~140 m from the rail platform |
| 2 | Civic | (1020,722), north side of Promenade South | 340 m | Middle of the Promenade |
| 3 | West Gate | (560,806), north side of Station Ave (z 790) | 470 m | West end of the Promenade. The north side is chosen so the far downtown corner (820,950) stays within 300 m. |
| 4 | Riverside NE | (200,330), on the NR grid's west street | 580 m | NE bank shore; starts the under-river leg |
| 5 | Riverside SW | (−560,−330) | ~1,000 m | Entirely under the river (the river spans x −320..320 at z 0) |
| 6 | Westbank Centre | (−1150,−350) | 590 m | |
| 7 | Westbank West | (−1750,−350) | 600 m | Harbor feeder terminal |
| 8 | Airport | (−2300,−360) | 550 m | ~90 m north of the Airport lot |

- The Central → Civic spacing (340 m) is below the 500 m guideline. It is deliberate: the player asked for stations at both ends and the middle of the Promenade. They cannot steal riders from each other because there is no parallel line.
- Phases: build Central → West Gate first (P3, 1.0 km), then to Riverside NE (P3/P4), then under the river to Westbank (P4), then to the Airport (P4).

**M2 North–South, "Northfield–South" (lines M2S and M2N):**

| # | Station | Site | Spacing |
|---|---|---|---|
| 1 | Northfield North | (−100,2400) | — |
| 2 | Northfield South | (−100,1850) | 550 m |
| 3 | North Gate | (450,1200) | 850 m (passes under the E–W highway) |
| 4 | Central-M2 | (1392,560), west side of Station Road x 1400, platform N–S at x ≈ 1368 | ~1,080 m (passes under DT at −24) |
| 5 | Riverside East | (1200,100) | 490 m |
| 6 | Centre-South | (700,−900) | ~1,120 m (under the river) |
| 7 | Southfield | (300,−1700) | 890 m |
| 8 | South | (1300,−2520) | ~1,240 m (at the South rail station) |

- Phases: North Gate → Central → Riverside East in P4; the north arm with N1 in P5; the south arm with CS/SF in P5–P6.
- Central is the one hub: rail, M1 and M2. Any station reaches any other with at most one transfer.

**River crossings:**

| Crossing | Type | Phase |
|---|---|---|
| Holmes/Underhill Bridge (existing, now Large Oneway Bridge) | road | now |
| M1 tunnel, Riverside NE → Riverside SW | metro | P4 |
| M2 tunnel, Riverside East → Centre-South | metro | P5 |
| **Riverside Bridge**: `Large Road with Tree Median and Bus Lanes Bridge`, from ~(1350,−760) on the NE bank to ~(800,−1150) in CS, ~680 m; keep ≥ 200 m from Water Intake 1428 (1127.8,−622.5) | road | P5 |
| Existing rail bridge (1952,−1432) → (1413,−2418) | rail | now |

Both banks become one city in P4 (M1 plus the existing couplet bridges). The Riverside Bridge gives the south a second road link in P5, so the couplet is not the only road across.

**Airport and harbor:**

| Hub | Site [M dry run] | Trunk link | Transfers to the Promenade |
|---|---|---|---|
| `Airport` (16x8) | (−2350,−450), angle 0, validated `canPlace:true`, no collisions [M] | M1 Airport station; road via a Crowley interchange at ~(−2300,−880) | **0** (M1 straight to Civic/Central) |
| `Harbor` (passenger) | (−2077.2,699.6), angle −151.7, **validated**: shore, 16 m above water, dock connects to the ship lane at (−3375,131) [M]. Three other shore probes failed (ShoreNotFound / CannotConnect / HeightTooHigh). | Harbor feeder bus H1 → M1 Westbank West (~1.1 km) | **1** (H1 → M1) |
| Rail (Central) | on the Promenade | — | **0** |

Place the Airport in P4, once Westbank and M1 reach it; airplane paths already exist at the W/E/S edges [M]. The Harbor goes in P4 with H1.

### 3.3 Road hierarchy

**Couplet (N–S arterial).** Holmes/Underhill: ~32 m apart, one-way, and now largely surface boulevard [M 14:17].
- No new at-grade junction on it south of the IC West junction until it is re-read.
- New district accesses use collectors that meet it only at interchanges or signal pairs ≥ 400 m apart.

**Interchanges** (full access, no new at-grade junctions on highway-class segments):

| Name | Where | Status / phase |
|---|---|---|
| **IC West (Station Avenue)** | Collector (Medium Road) 8986 (168,630, dead end) → 17764 (178,705) → 19942 (200,790) → 18508 (188,865) → 31116 (185,960) → 29000 (182,1060). | **BUILT 14:16–14:19** [M, progress-portville.md]. NB off `HighwayRamp` 13256 → 11640 → 5632 → 17764. NB on 18508 → 20511 → 23218 → 10012. SB access is an **at-grade crossover** (Medium Road) Holmes 24949 (74,1063) → Underhill 6104 (106,1069) → 29000 (segs 7258, 31467), which replaced the flyovers. The z ~700 SB on-ramp was not built. Fixed (orchestrator). |
| **IC North Gate** | Existing north interchange (Kent/Lafayette, x 0..240, z 1335..1545): a collector from D2's north row (z 1350) to its free ramp or a new half-diamond. | P3. Gives D2/NE a second access so Station Ave is not the only way in. |
| **IC East (E1 trucks)** | On Albert/Hunter in the E tile at about (2700,300) (highway at x ≈ 2720–2800 there [M map]) | P3/P4, before E1 industry is zoned |
| **IC Westbank** | Crowley at ~(−2300,−880) | P4 |
| **IC Southfield** | Diagonal highway at ~(600,−1900) | P5 |

**Arterials and collectors:**
- **Station Avenue:** x 200..520 stays `Medium Road` (car feed from IC West). x 520..1000 is rebuilt to `Small 4 Lane Road with Bus Lanes` ("Promenade West") in P1 while nothing fronts it. x 1000..1400 becomes the pedestrian Mall (§3.8).
- **Downtown Ring** (cars go round the Promenade):
  - West side: collector x 520 (z 470..1110).
  - Ring South: z 470, x 280..1400.
  - Ring East / Station Road: x 1400, z 470..950.
  - Ring North: z 950, x 1000..1400. D1's z 950 row stays Basic.
  - All new ring pieces are `Medium Road`, 80 m pieces with nodes on the lattice.
- **Collectors every 400–480 m:** x 520, x 1000 (D1 east edge), and z 470; the same pattern in every new district.
- **Locals:** `Basic Road`, 80 m lattice (16 m road + 2 × 32 m zone depth).
- **Roundabouts:** the bridge has no roundabout primitive. Build a ring of `Oneway Road` (R 30–40 m, 8 pieces), dry run first. Use them where a district collector meets a couplet or interchange collector (e.g. Dixon/D2, P3).
- **Bus lanes from day one on trunk bus corridors:** Promenade West (P1), Promenade North/South (P2), Ring South (P3 upgrade to `Small 4 Lane Road with Bus Lanes` while unfronted), and every district's main collector (`Large Road with Tree Median and Bus Lanes` for the Westbank and Northfield spines).
- **Pedestrian shortcuts** (`Pedestrian Pavement`; `Pedestrian Elevated` over barriers):
  - The Mall and the Promenade Cross (§3.8).
  - A path from every metro entrance to the nearest block interior.
  - `Pedestrian Elevated` over the mainline from Central's plaza to E1 at z ≈ 800 (P3).
  - `Pedestrian Elevated` over the couplet at z ≈ 790 from West Gate to a riverside park, x −200..0 (P3).

### 3.4 Bus

| Line | Phase / trigger | Route (stops 200–250 m, both directions, ≤ 20 stops) | Ends at |
|---|---|---|---|
| **B1 Promenade Loop** | Bus unlocked + `Bus Depot` (660,982) | city.md step 10 loop; x 600..1000 now uses Promenade West's bus lanes. P2: extended east on Promenade South/North to Station Road. | Central Plaza (Station Road x 1400) |
| **B2 Riverside** | P2, when NR rows z 310..470 open | Ring South → NR rows (z 390, z 230) → x 520 | Central Plaza (Station Road stop at z ≈ 700) |
| **B3 North Gate** | P2/P3, D2 open | D2 rows z 1190/1270 → collector x 520 | West Gate (M1) from P3; Central Plaza before |
| **C1 Promenade Circulator** | P2 end (after the Mall and flank streets) | Station Road SB (1396,760) → Promenade South WB (1300,714), (1140,714) → x 1000 NB → Promenade West WB (900,794), (700,794) → x 520 NB → Greenaway (z 870) EB → Promenade North EB (1080,866), (1240,866) → Station Road SB | Central Plaza / Civic / West Gate (circulator; ~2.4 km, ~8 stops) |
| H1 Harbor | P4 | Harbor (−2077,700) → W landmass → Westbank West | M1 Westbank West |
| District feeders | P4–P6 | One per new district (Westbank ×2, Northfield ×2, NE ×2, CS, SF), each **starting** at its M1/M2/rail station and never running within ~150 m of that trunk | Their station |

- **Depots:** `Bus Depot` 1 at (660,982) [M clean, city.md]; 2 in NR at ~(1300,390) (P3); 3 in Westbank (P4); 4 in Northfield (P5).
- **Bus lanes** on every road the circulator and feeders use near a trunk station.
- **Before the train and metro:** the buses are the trunk. They converge on Central Plaza, so the same stops become the rail and metro transfer points later with no line edits.

### 3.5 Staging by milestone unlock

The trigger for every transit mode is the live flag `/state/transit` → `transportPrefabs[].unlocked` [M]; poll it at every phase gate. Service unlocks cannot be read (`place-building` ignores unlocks [M docs]), so the player confirms each one. Milestone names and populations below are vanilla recollections [U].

| Stage | Opens [U] | Transport while waiting |
|---|---|---|
| 0 – Little Hamlet (~440) | clinic, elementary, landfill | Walkable D1: an 80 m lattice with shops (CL) on Promenade West within 400 m of every home. No transit. |
| Worthy Village (~990) → Tiny Town | fire, police, parks | Keep homes compact around x 600..1000. Build no road that parallels Station Ave. |
| **Bus** (flag; city.md said Boom Town) | `Bus Depot`, bus lines | **Same session:** Depot (660,982), B1, bus-lane check on Promenade West. FreeTransport is already set in P1 (unlocked now [M]). |
| Big Town (~7.5k) | high density, offices | Zone DT, the Promenade frontage and D1 upzoning (§5). |
| **Train** (flag) | `Train Station` | **Same session:** Central (B11 host pattern), T1 Central ↔ South. The South station can come first if SF exists; otherwise Central plus the outside connections carry intercity passengers. Re-terminate B1/B2/C1 at the station forecourt. |
| **Metro** (flag) | `Metro Entrance`, `Metro Track` | **Same session:** M1 Central → Civic → West Gate. Remember the "Metro Track Created" milestone quirk (lessons 2026-09-26; fixed in the bridge). |

### 3.6 Targets and vehicle budget

| Target | Value | Check |
|---|---|---|
| Bus stop coverage | ≥ 98% of growables within 250 m; 100% in each new district | `cov.py` (TAmpa) adapted |
| Trunk coverage | 100% of homes within 800 m of rail/metro | Station list vs growables |
| Downtown | every point of z 630..950, x 520..1400 within 300 m of a metro entrance [E geometry, 10 m sweep: worst (820,950) at 297 m]; Culture Quarter within 275 m of Central-M2 / Civic / Central-M1 [E]; every block within 150 m of a C1/B1 stop | growables vs stops |
| Modal share | riders per week ÷ population ≥ 12% at 100k, stretch 18% (TAmpa 8.6% today, 12% planned) [E] | `/state/transit` totals + player's population |
| Traffic flow | ≥ 75% (TAmpa 59%) | `/state/traffic` `trafficFlowPercent` |
| Vehicles | < 10k of 16,384 at 100k [E: TAmpa ~3.6k at 42k scaled ×2.4 = ~8.6k] | not readable (bridge gap) |

Transit fleet at 100k [E]: ~15 bus lines × ~8 = 120 buses; M1/M2 4 directions × 5 trains × 5 cars = 100 vehicle slots; T1 2 × 8 = 16. About 240 transit vehicles in all. Line budgets start at 100; raise one only where a stop has > 300 waiting **and** corridor density is < 80 (B18).

### 3.7 Interconnectivity: ≤ 1 transfer to every district

| From \ To | Promenade | Airport | Harbor | Rail (Central) |
|---|---|---|---|---|
| D1 / DT / NR | walk or C1, 0 | M1, 0 | M1 + H1, 1 | walk, 0 |
| E1 | ped bridge, 0 | ped bridge + M1, 1 | 2, accepted (jobs-only district) | 0 |
| Westbank | M1, 0 | M1, 0 | H1, 0/1 | M1, 0 |
| Northfield / North Gate | M2 → Central, 0 | M2 → M1 at Central, 1 | 2 (accepted) | M2, 0 |
| NE North Central | T1 North → Central, 0 | T1 + M1, 1 | 2 (accepted) | T1, 0 |
| CS / Southfield | M2 → Central, 0 | M2 → M1, 1 | 2 (accepted) | M2 or T1 South, 0 |

Visitors (airport, harbor, rail) reach the Promenade with at most one transfer: Airport and Rail with 0, Harbor with 1.

### 3.8 Downtown attractions corridor: the Central Promenade (player requirement, P2–P4)

> "Build a downtown corridor with attractions and monuments and easy connectivity to high density
> downtown. Downtown should be walkable and easily transportable." [G, player]

**Geometry** (80 m lattice continued from D1; road centre lines):

```
 z 950  ==== Ring North (Medium) =======================================  (tile edge 960; north growth needs the NE tile)
          |RH/CH/O|RH/CH/O|RH/CH/O|RH/CH/O|RH/CH/O|        <- 64 m blocks, zoned at high density
 z 870  ---- Promenade North: Small 4 Lane Road with Bus Lanes (C1 EB) ---
          [ uniques / plazas, <= 8 cells deep, fronting z 870, backs on the Mall ]
 z 790  ~~~~ THE MALL: Pedestrian Pavement, x 1000 -> 1400 (no cars) ~~~~~  + Promenade Cross (path) x 1200, z 710..870
          [ uniques / plazas, <= 8 cells deep, fronting z 710 ]
 z 710  ---- Promenade South: Small 4 Lane Road with Bus Lanes (C1 WB) ---   M1 entrances: Central-M1 (1360,722), Civic (1020,722)
          |RH/CH/O|RH/CH/O|RH/CH/O|RH/CH/O|RH/CH/O|
 z 630  ---- Basic row -------------------------------------------------
          [ CULTURE QUARTER: two superblocks x 1000..1200, 1200..1400, 144 m deep; no z 550 road ]
 z 470  ==== Ring South (Medium, bus lanes from P3) =====================
        x 1000 (D1 east edge / Civic)                           x 1400 Station Road (Ring East) | plaza | Central Station (hosted, ~x 1470..1530)
West of x 1000: "Promenade West" = Station Ave x 520..1000, Small 4 Lane Road with Bus Lanes, CL now -> CH at high density;
West Gate M1 entrance (560,806).
```

**Widths:**
- Roads 16 m; the Mall path ~6 m.
- Mall frontage blocks: north z 794..862, south z 718..786, i.e. 68 m ≈ 8.5 cells deep.
- Culture Quarter superblocks: 184 × 144 m (~23 × 18 cells).
- Plaza between Station Road and the station front: x 1408..~1465, width set by the P3 station lot.

**Walkability:**
- The Mall is car-free.
- Cross-mall N–S car streets are **not** built between z 710 and 870 in x 1000..1400; car stubs stop at the flank streets. The existing D1 cross streets (x 600..1000) stay.
- Pedestrian links: the Promenade Cross at x 1200; a path from each M1 entrance north to the Mall; the plaza path to the station; `Pedestrian Elevated` over the rail to E1 (P3).
- Blocks are 80 m.

**Transport:**
- Metro: M1 at both ends and the middle (Central-M1, Civic, West Gate); M2 at Central.
- Rail: at Central.
- Bus: C1 circulator on the bus-lane flank streets; B1/B2 end at Central Plaza.
- Cars: go round via the Downtown Ring. There is no parking prefab in the corridor. The `Decoration Concrete Parking Lot 0x` prefabs exist but are decoration only [M list]; do not place them on the Promenade.
- Taxis: **not available** (no taxi depot prefab) [M].

**Slot plan.** Each slot lists alternates; place the first one the player confirms unlocked. All names are verified in `/prefabs/buildings` [M]; sizes are width × depth in cells [M].

| Slot | Where (front road, x range) | Fits | Candidates in priority order |
|---|---|---|---|
| A1 Civic anchor | Promenade North, x 1008..1136 | 16 × 8 | `Opera House` 16x8, `Academic Library 01` 14x8, `Business Park` 16x8 |
| A2 | Promenade North, x 1144..1196 | ≤ 6 wide | `City Hall` 8x6 (monument) or `city_hall` 8x8 (if 8 wide fits x 1136..1200), else `Expensive Plaza` 5x7 |
| P-mid | Both sides of the Promenade Cross, x 1204..1250 | ≤ 6 wide | `Birthday Plaza 01` 6x8 (north), `Regular Plaza` 5x5 + `JapaneseGarden` 4x4 (south) |
| A3 | Promenade North, x 1256..1384 | 16 × 8 | `High Interest Tower` 16x8, `Stadium` 16x8 (event traffic; ring access) |
| A4 | Promenade South, x 1008..1100 | ≤ 11 × 8 | `shopping_center` 11x7, `Botanical garden` 11x8, `Regular Park` 9x8 |
| A5 | Promenade South, x 1108..1196 | ≤ 11 × 8 | `StatueOfWealth` 8x8, `gherkin` 8x8, `amsterdam_palace` 8x8, `Observatory` 7x6 |
| A6 | Promenade South, x 1256..1340 | ≤ 10 × 8 | `SeaAndSky Scraper` 7x6, `Fountain of LifeDeath` 7x7, `Friendly Neighborhood` 8x7, `Statue of Shopping` 6x7 |
| A7 | Promenade South, x 1344..1392 | ≤ 6 × 8 | `cinema` 4x7, `Official Park` 6x6, `Tropical Garden` 6x6 |
| Central Plaza | x 1408..~1465, z 700..880 | — | `Future Plaza` 9x9 or `Expensive Plaza` 5x7, plus `Pedestrian Pavement` |
| CQ-W | Culture Quarter west, x 1000..1200 (front Ring South z 470) | ≤ 23 × 18 | `highschool_EU` 12x13 (education on the trunk, §4) + `ScienceCenter` 11x14 / `Modern Art Museum` 8x16 / `ExpoCenter` 9x15 |
| CQ-E | Culture Quarter east, x 1200..1400 | ≤ 23 × 18 | `Cathedral of Plentitude` 11x14, `cathedral_of_cologne` 8x13, `Theater of Wonders` 12x13, `Grand Mall` 12x13, `Posh Mall` 11x14, `london_eye_anim` 14x10, `arena` 13x11 |
| Promenade West | Station Ave x 520..1000 (bus lanes), mid-block | ≤ 8 deep | `theatre` 6x12 is too deep; use `Library 01` 10x8 at Civic's west corner (x 920..1000, north side), `Oppression Office` 9x5 / `Lazaret Plaza` 7x5 / `Plaza of the Dead` 6x5 as they unlock; the rest CL → CH |
| NE extension (P4+) | North of z 960 after the purchase: a second Culture Quarter along a Mall continuation | deep | `Colossal Offices` 12x12, `SeaWorld` 12x12, `Transport Tower` 8x12, `Library` 14x11, `Court House` 11x14, `department_store`/`hypermarket`, `government_offices` 10x10, `Winter Market 01` 11x12, and the wonders `Eden Project` 13x12, `Plaza of Transference` 9x15, `Space Elevator` 15x9 |

**Unlocks [U].**
- The bridge cannot read building unlock state (gap), and I did not verify any unique's condition.
- Vanilla uniques unlock by milestone tier plus a per-building achievement. Wonders need several uniques first.
- The European landmark set (`amsterdam_palace` … `arena`) and the plazas/gardens from content packs have their own rules.
- **Order of placement:** the player reads the Unique Buildings panel at each phase gate. The builder fills slots in the order A1, A3, CQ, A2–A7, then the extension, always with the highest-priority unlocked candidate.
- Parks first, because they unlock early [U, city.md: Tiny Town]. The Promenade starts as a park street (`Regular Park`, `Regular Plaza`, `Regular Playground` in the A slots). The A slots are **left unzoned and reserved**: uniques replace the parks (demolishing a park is cheap and within authority).

**Rules:**
- Growables never go in A/CQ slots.
- Every attraction lies within 300 m of a metro/rail stop (all A/CQ slots are ≤ 300 m from Civic, Central-M1, West Gate or Central-M2) [E geometry].
- Keep noisy uniques (`Stadium`, `arena`) on the Ring side, away from RH.

---

## 4. Services, education, youth, tourism, sustainability

EU service prefab names verified in `/prefabs/buildings` [M]:
- Education: `Elementary_School_EU` 6x5, `highschool_EU` 12x13, `University_EU` 13x12, `Library 01` 10x8.
- Health: `medicalclinicEU` 5x4, `hospital_EU` 10x9, `Medical Center` 15x9 (wonder), `Child Health Center 01` 6x5, `Eldercare 01` 12x6.
- Fire: `firehouse_EU` 6x4, `Fire_Station_EU` 7x5.
- Police: `police_station_EU` 4x4, `Police Headquarters EU` 6x5.
- Deathcare: `Cemetery` 10x8, `Crematory` 3x3.
- Garbage: `Landfill Site` 10x8, `Combustion Plant` 5x4.
- Water: `Water Intake` 3x5, `Water Outlet` 2x8, `Water Treatment Plant` 8x9, `Water Tower` 2x2.
- Power: `Wind Turbine`, `Advanced Wind Turbine` (OnWater), `Solar Power Plant` 13x12, `Nuclear Power Plant` 15x9, `Fusion Power Plant` 16x8. Also `Coal Power Plant` and `Oil Power Plant`, which are **not to be used**.

| Phase | Utilities (sustainable only) | Education (highly educated) | Health / safety / death | Youth / parks | Tourism |
|---|---|---|---|---|---|
| P1 | Existing: 4 turbines [M], Nuclear 31187 + Solar 45983 (player) [M]; fix their Water problem. Intake 1428, Outlet 8035. | `Elementary_School_EU` in cell (880,910) | `medicalclinicEU` (880,670); `firehouse_EU` (640,670); `police_station_EU` (960,910); `Landfill Site` (440,1390); `Cemetery` ~(476,1030) facing x 520 | `Regular Playground` + `Regular Park` ~(560,1030) | Parks in the Promenade slots |
| P2 | 2nd `Water Intake` upstream ~(1250,−760) (validated shore dry run; upstream of Outlet 8035); 2nd `Water Outlet` beside 8035; `Water Tower` backup in D2 | 2nd Elementary (D1 west cell (400,590)); `highschool_EU` CQ-W ~(1060,530) (≤ 250 m from Civic); `Library 01` at Civic | 2nd clinic (D1 west), `Crematory` in D2; fire/police for D1 west and NR | Park or playground within 400 m of every family block; Promenade parks | Promenade slots as they unlock |
| P3 | `Water Treatment Plant` replaces the outlets (downstream NW shore, x −600..−400, z 560..650); `Combustion Plant` ×2 in E1 (garbage off the landfill) | `University_EU` at West Gate / NR north (≤ 250 m from West Gate); 2 Elementary in NR | `hospital_EU` in NR ~(900,300); `Fire_Station_EU`, `Police Headquarters EU` on Ring South; `Child Health Center 01` ×2; `Eldercare 01` | 4 parks in NR; riverside park (x −200..100, z 350..750) linked to West Gate | Central Plaza; CQ anchors |
| P4 | 2nd `Solar Power Plant` / `Advanced Wind Turbine`s (E1, lake) only if the Electricity view shows > 70% use; `Fusion Power Plant` when unlocked replaces Nuclear (player) | Westbank: 4 Elementary, `highschool_EU` at Westbank Centre, 2nd `University_EU` at Riverside SW; `Library 01` per district at its station | `hospital_EU` Westbank; fire/police per ~0.5 km² | 6 parks Westbank; `Expensive Park` at Riverside SW | Airport, Harbor; NE extension anchors |
| P5–P6 | Treatment plants downstream per district; intakes only upstream (SE) | Northfield, CS, SF: Elementary per ~650 homes, High school at each M2 station | `Medical Center` (wonder) at Central-M2 if unlocked | continuous staged housing | Wonders on the NE extension |

**Education and youth rules** (TAmpa §3b–3c mechanics [M code]):
- Coverage educates as much as seats do.
- Every `highschool_EU` and `University_EU` goes within 250 m of a rail or metro platform; Elementary goes inside the housing (≤ 500 m).
- EducationBoost policy on from P1 (unlocked now [M]); the Education budget stays ≥ 100% (the player confirms it; the bridge cannot read it).
- Youth: zone residential continuously, in ≤ 150-cell chunks per game week per district. Each new household is two adults plus children; staggered move-ins prevent death waves.

---

## 5. Zoning rules (every phase)

- **Demand gate (city.md):**
  - Residential: one chunk (≤ 150 cells, one lattice cell) per game week per district while R ≥ 40; stop below 20.
  - Commercial: one C cell per 3–4 R cells actually open, and only while C ≥ 30.
  - Industrial / Office: only while W ≥ 30. Industrial cells ≤ 20% of open residential cells.
  - **Never jobs ahead of homes** (W1 lesson: jobs ahead of homes went short of workers).
- **Pipes and power first**, on every road of the chunk, before painting it (M1 lessons). Zone before running power poles (poles block set-zone). Probe build-grid roads for lanes: `laneprobe.py` once Bus unlocks, `touch.py` until then.
- **Density follows stations:**
  - Within 400 m of Central / Civic / West Gate / any M1/M2 station: RH, CH or O only, zoned when high density unlocks. Before that, these cells stay unzoned. Exception: CL on Promenade West frontage.
  - 400–800 m: RH (RL only before Big Town, upzoned later by orchestrator decision).
  - Over 800 m: RL allowed.
- **Reserved, never zoned:**
  - Central strip x 1400..1600, z 470..950.
  - All Promenade A/CQ slots.
  - Service lots listed in §6.
  - Station entrance lots.
  - A 100 m buffer each side of the mainline where no station exists.
- **Industry** goes downwind of the core and next to cargo: E1 (east of the mainline, with the Cargo Center and the E interchange), plus a 3-cell starter in D2 west. It stays ≥ 160 m from housing; offices form the buffer. Wind direction is not exposed (gap), so treat "downwind" as "east of the rail".
- **Offices over industry** for new jobs (fewer trucks, more trips; §3.1).

---

## 6. Phases

Common acceptance calls:
- `/state/summary`, `/state/demand`, `/state/problems?limit=2000`, `/state/zones`, `/state/growables`
- `/state/transit?includeStops=true`, `/state/traffic`
- `/state/road-anomalies?includeDeadEnds=false` (must be 0)
- `/state/external-connections` (localRoadComponents 1, disconnected 0)
- `/state/zone-anomalies`, `/state/building-anomalies`

Save rule: overwrite `Portville` after every verified step, and confirm its mtime is newer than the request. Population comes from the player's UI.

### P1 — Seed town (0 → ~1k) — remainder, in order

Already built [M, progress-portville.md and 14:17 read]:
- Station Ave (15 segs, nodes 19942 … 28975).
- Collector x 520 (4 segs to (520,1110)).
- D1 grid (origin (600,630), 5x4).
- Turbines 10913, 24210, 29962, 6243.
- Intake 1428 and Outlet 8035.
- 80 pipes.
- IC West collector and NB ramps.
- Player-built: Nuclear 31187, Solar 45983, Advanced Wind Turbine 13588 (1191,−743), Wind Turbine 42033 (−543,689), ~30 power-line segments, 17 pipes.

1. **IC West: DONE** 14:16–14:19 [M]. `cityConnectedToOutside` true, roadComponents 1, anomalies 0. Remaining:
   - Link D2 to the IC's north collector: `Medium Road` 29000 (182,1060) → (200,1110) (D2's grid corner), built with D2 in step 6.
   - Watch the crossover (two signalised junctions on the couplet) once homes exist; if density there exceeds 80, build the z ~700 SB flyover from the 14:07 design.
2. **Utilities:**
   - (a) Nuclear 31187 (2250,643) and Solar 45983 (2317,562) carry Water MajorProblem. Measure the distance from each to the nearest Water Pipe node. If > 100 m, lay `Water Pipe` from the player's pipe run (x 1013..2307, z 435..635) to within 20 m of each lot, then re-read.
   - (b) Outlet 8035 WaterNotConnected: re-check once D1 has homes. If it persists, pipe to its landward end (TODOS).
   - (b2) `set-policy` FreeTransport and EducationBoost city-wide (both unlocked [M]); re-read `/state/policies`.
   - (c) `Water Pipe` and power (pole ends within 15–20 m) along collector x 520 (z 790..1110) and the IC collector (x 168..200, z 630..1060).
3. **Promenade West bus lanes** (before any zoning fronts it):
   - For each of segs 2145 (520→600), 10008, 13070, 36105, 11599, 12759 (920→1000): bulldoze with keepNodes, then rebuild `Small 4 Lane Road with Bus Lanes` on the same nodes (24490, 25598, 32386, 12315, 21314, 12192, 16748).
   - Every call must return `createdNodeIds == []`. Then touch the nodes for lanes, check anomalies 0, save.
   - Nothing fronts these segments (0 growables [M]), so no demolition.
4. **D1 zoning by the demand gate** (lattice-cell centres, radius 34, pipes and power first):
   - **RL order:** (720,910), (720,670), (800,910), (800,670), (640,910), (960,670). One chunk per game week while R ≥ 40.
   - **CL:** (800,750), then (800,830) (Promenade West frontage). One per 3–4 open R cells, only while C ≥ 30.
   - **Do not zone:** (640,750), (640,830), (720,750), (720,830), (880,750), (880,830), (960,750), (960,830). This is Promenade West frontage, reserved for high density and the West Gate/Civic plazas.
   - **Service lots:** (880,910) Elementary, (880,670) clinic, (640,670) fire, (960,910) police. The fire and police lots moved off city.md's (640,830)/(960,830), which are Promenade frontage now.
5. **Services on unlock** (player confirms; validated dry run plus the `clean` guard; try angles 0/90/180/270; lot faces a Basic row, never Promenade West):
   - Little Hamlet: `Elementary_School_EU` (880,910); `medicalclinicEU` (880,670); `Landfill Site` ~(440,1390), after D2's north row exists.
   - Worthy Village: `firehouse_EU` (640,670); `police_station_EU` (960,910).
   - Parks: `Regular Playground` + `Regular Park` ~(560,1030).
   - Boom Town: `Cemetery` ~(476,1030) facing x 520; `Bus Depot` (660,982) [M clean].
6. **D2 starter industry, only when W ≥ 30:**
   - build-grid `Basic Road` origin (200,1110), cols 6, rows 3, spacing 80, opId `portville-d2-grid-01` (x 200..680, z 1110..1350), plus the link 29000 → (200,1110). Run the lane probe or touch after it.
   - Industrial cells (**corrected centres**; city.md's list sat on grid lines): (240,1230), (320,1230), (400,1230), (240,1310), (320,1310), (400,1310). Cap at 20% of open R cells.
   - The east half (x 440..680) stays for offices (Big Town).
7. **Bus B1** when `Bus.unlocked`:
   - Depot first, then city.md step 10's loop. Route with route3.py/route2.py, dry run, and reject any stop with segmentId 0 or snap > 32 m.
   - Stops on x 600..1000 use Promenade West (bus lanes).
   - FreeTransport and EducationBoost are **already unlocked** [M `/state/policies` 14:2x: both `unlocked: true`, `cityPolicies` empty]. Set both city-wide in P1 now (step 2b below), not at Bus unlock.

**P1 acceptance:**
- `cityConnectedToOutside` true; localRoadComponents 1; road-anomalies 0; building-anomalies 0.
- No NoWater, NoElectricity or NoSewage after 2 game weeks, and the Nuclear/Solar Water problem gone.
- Every unlocked service placed and connected; R demand > 0 with no NoWorkers.
- Promenade West segments are `Small 4 Lane Road with Bus Lanes` with lanes.
- B1 (once Bus is unlocked): no LineNotConnected, vehicles = target, no stop with laneId 0.
- Save `Portville` verified by mtime.

### P2 — Town to ~7.5k (Big Town) plus the Promenade skeleton

Build in this order. Every call is dry-run first, 80 m pieces, nodes on the lattice, and `createdNodeIds` checked at every join.

1. **Ring South:** `Medium Road` z 470, (280,470) → (1400,470), 14 pieces.
2. **Ring East / Station Road:** `Medium Road` x 1400, (1400,470) → (1400,950), 6 pieces. It must reuse node 28975 (1400,790).
3. **Ring North:** `Medium Road` z 950, (1000,950) → (1400,950), 5 pieces, reusing D1's (1000,950) node.
4. **Promenade flank streets:** `Small 4 Lane Road with Bus Lanes` z 710 and z 870, each (1000,z) → (1400,z) in 5 pieces. They reuse D1's (1000,710)/(1000,870) nodes and meet Station Road at (1400,710)/(1400,870).
5. **Downtown locals** (`Basic Road`):
   - Row z 630, (1000,630) → (1400,630), 5 pieces.
   - N–S stubs x 1080, 1160, 1240, 1320: z 630 → 710 and z 870 → 950 (8 pieces). **No car street between z 710 and z 870.**
   - Culture Quarter: one N–S street x 1200, z 470 → 630 (2 pieces). **No z 550 road.**
6. **The Mall:**
   - Bulldoze Station Ave segs 30883, 17315, 36084, 14136, 32804 (x 1000..1400, z 790; nothing fronts them [M]).
   - Build `Pedestrian Pavement` (1000,790) → (1400,790) in 80 m pieces. Check that the ends join road nodes 16748 and 28975 (dry run `createdNodeIds`).
   - Promenade Cross: `Pedestrian Pavement` x 1200, z 718 → 862.
   - **Fallback:** if the bridge refuses a path on a road node, stop, keep the road segments, and report. Do not leave a dead-end path.
7. **D1 south and west** (also link IC collector dead end 8986 (168,630) → (280,630) with `Basic Road`, a second way into D1 west):
   - build-grid `Basic Road` origin (600,470), cols 5, rows 2 (reuses Ring South and the z 630 row).
   - build-grid `Basic Road` origin (280,470), cols 3, rows 6 (x 280..520, z 470..950; reuses Station Ave node 720 (280,790), 3384 (360,790), 20966 (440,790), 24490 (520,790) and the x 520 collector).
   - Keep x ≥ 280 so nothing touches the IC collector at x 168..200.
   - Lane probe or touch, then anomalies 0.
8. **NR Riverside NE, north half:** build-grid `Basic Road` origin (360,310), cols 13, rows 2 (x 360..1400, z 310..470). The outer row stays one block back from the shore slope (E1 lesson); check terrain cross-slope per piece.
9. **Utilities for every P2 road:**
   - Pipes under every new road (from D1 and the Ring South run).
   - Power poles off block centres, placed after zoning.
   - 2nd `Water Intake` ~(1250,−760) (validate, shore) + pipe.
   - 2nd `Water Outlet` beside 8035 (validate).
   - `Water Tower` in D2 east.
10. **Power-line reroute** (before P3; player's line in the Central strip, TODOS):
    - New `Power Line` from pole 24204 (1709,736) → (1700,448) → (1080,448) → the D1 south grid. Keep poles ≥ 20 m off Ring South's centre, on lattice-node lines (x 1080, 1160, …) so they sit on block edges. Do this after NR's z 390..470 blocks are zoned (poles block set-zone).
    - Then bulldoze the Central-strip and Promenade span: poles 21704 (1449,757), 3977 (1536,769), 27833 (1353,743), 1477 (1266,731), 25820 (1171,718) and their segments (incl. 34420, 7230, 19485).
    - Accept: ElectricityNotConnected count does not rise; D1 buildings keep power.
11. **Zoning** (demand gate):
    - D1 west cells (x 320, 400, 480; z 510..910), RL.
    - D1 south cells (x 640..960, z 510, 590), RL.
    - NR north cells (z 350, 430), RL.
    - CL 1 per 3–4 R cells on Ring South frontage.
    - **Promenade frontage, the Culture Quarter and DT blocks stay unzoned until Big Town.** At Big Town: CH on the flank streets' outer blocks, O on Ring North/Ring East, RH on DT rows z 630..710 and 870..950. The A/CQ slots are never zoned.
12. **Services (P2 row of §4):**
    - 2nd Elementary (400,590); `highschool_EU` CQ-W ~(1060,530) facing Ring South; `Library 01` at A-slot "Promenade West" (x 920..1000, north side).
    - 2nd clinic (D1 west ~(320,510)); `Crematory` D2; firehouse/police for D1 west (320,670)/(480,910) and NR.
    - Parks: every family block ≤ 400 m from one. The Promenade A slots get `Regular Park`/`Regular Plaza` placeholders.
13. **Transit:**
    - Extend B1 east over Promenade South/North to Station Road stops (1396,760)/(1404,820), i.e. Central Plaza.
    - B2 Riverside (Ring South + NR rows → Central Plaza).
    - C1 Promenade Circulator after steps 4–6.
    - All routed with U-turns forbidden, ≤ 20 stops, 200–250 m.
    - FreeTransport stays on (set in P1).
14. **Reserve check:** read facilities and networks in x 1400..1600, z 470..950. Only the plaza and roads may be there, no poles or growables.

**P2 acceptance:**
- Population ≥ 7,500 (UI).
- R > 0, W ≥ 0, NoWorkers ≤ 3.
- Elementary coverage 100% of homes within 500 m; a park within 400 m of every family block.
- Road anomalies 0, one local component; the Mall joined at both ends.
- B1/B2/C1 healthy (no LineNotConnected, vehicles = target, no laneId 0); every growable within 250 m of a stop.
- Central strip clear.
- Save verified.

### P3 — Central Station, M1 core, downtown density (~7.5k → 25k)

- **Train (flag):**
  - Central hosted (B11): dry-run validated; the only collisions allowed are host segs 15554/16776. Read the platform node positions; bends ≤ 40° at both platform ends.
  - Forecourt: `Basic Road` loop from Station Road x 1400 at z 700 and z 860 to the lot front (whatever the placed lot needs).
  - T1 Central ↔ outside first; add South in P5.
  - Re-terminate B1/B2/C1 at the forecourt.
- **Metro (flag):** M1 Central-M1 → Civic → West Gate (entrances §3.2), M1W/M1E lines, then extend to Riverside NE.
- `Pedestrian Elevated` Central → E1 over the rail.
- IC North Gate; IC East (before E1 industry).
- NR south half (shore-limited); E1 grid and industry/offices; D2 east offices.
- Ring South → `Small 4 Lane Road with Bus Lanes` while unfronted, or leave it and use the Upgrade tool (player).
- Services P3 row; `Bus Depot` 2 in NR.
- **Acceptance:**
  - Population ≥ 25k.
  - T1 and M1 both directions: 0 problems, vehicles = target, riders > 0 after 3 weeks.
  - Every home within 800 m of rail/metro in D1/DT/NR; downtown 300 m rule met.
  - `highschool_EU`/`University_EU` ≤ 250 m from a platform.
  - Traffic flow ≥ 75%; Station Ave density < 80 (else accelerate IC North Gate).

### P4 — Both banks, one city (25k → 55k)

- M1 under the river to Riverside SW → Westbank Centre → Westbank West → Airport.
- Westbank grids with `Large Road with Tree Median and Bus Lanes` spine along z ≈ −350.
- IC Westbank; `Airport` (−2350,−450); `Harbor` (−2077.2,699.6) + H1.
- NE tile: North station, NE downtown extension (second Culture Quarter).
- M2 North Gate → Central-M2 → Riverside East.
- **Acceptance:**
  - Population ≥ 55k; disconnected local components 0.
  - Airport/Harbor ≤ 1 transfer to the Promenade (line stop lists).
  - Modal share ≥ 10%.

### P5 — North and south (55k → 85k)

- N1 Northfield + M2 north arm (Northfield North/South).
- CS + Riverside Bridge + M2 south arm to Centre-South → Southfield; South `Train Station` + T1 extension.
- Treatment plants per district.
- **Acceptance:** population ≥ 85k; every district's feeder ≥ 75 riders/week after 4 periods; modal share ≥ 11%.

### P6 — 100k and beyond

- SF completion; level-up programme (education and park coverage; upzone RL → RH within 400 m of stations under full authority, main session).
- SE2 / SW tile only if short.
- **Acceptance:**
  - Population ≥ 100,000 (UI).
  - All lines healthy.
  - Modal share ≥ 12%; traffic flow ≥ 75%.
  - Elementary / high / university coverage ≥ 90 / 80 / 70% of homes.
  - Problems trending down; save verified.

---

## 7. Constraints, risks, bridge gaps, not verified

**Risks:**
1. **The couplet changed under the interchange** [M]. Between 14:07 and 14:16, an unknown party (TODOS: already filed) converted Underhill and parts of Holmes to Large Oneway boulevards. IC West now uses an at-grade SB crossover at 24949/6104.
   - Through traffic between the north and south highways meets at-grade junctions inside the city.
   - Keep new couplet junctions to a minimum (§3.3).
2. **Only one road access until IC North Gate (P3).** Station Ave x 520..1000 drops to one car lane each way when it gets bus lanes. Watch its density; bring IC North Gate forward if it exceeds 80.
3. **The Mall conversion depends on the bridge joining a `Pedestrian Pavement` to road nodes.** Not tested. P2 step 6 has a stop-and-report fallback.
4. **Central Station geometry is not known until placement.** The forecourt road is built after the dry run, in P3. The Station Road at x 1400 leaves 40–70 m to the lot front for a plaza and forecourt.
5. **Cargo from the E-edge connection passes Central's platforms.** A bypass (`Train Station Track Ground Bypass` exists [M]) or a second track is a P4 decision.
6. **The player keeps building** (plants, pipes, power lines, the couplet conversion). Diff live state before every step (lesson 2026-09-25); any unknown entity is the player's.
7. **Zoning RL near stations early means demolition when upzoning** (full authority allows it). Minimised by leaving the Promenade frontage and DT unzoned until Big Town.

**Bridge gaps:**
- Building unlock state (uniques, services) is not readable; `place-building` ignores unlocks and money.
- No population read (`citizens.count` 2,055 now includes ~1,100 pass-through travellers at 0 buildings [M]).
- No owned-tile read or purchase.
- No vehicle or citizen-instance count.
- No wind direction.
- No lane-validity read while Bus is locked (TODOS).
- No roundabout primitive.
- No zone-safe road upgrade.
- List endpoints cap at 5,000 rows (page by service).

**Not verified:**
- The milestone populations and every unique's unlock condition [U].
- Ramp direction on a built ramp (IC notes).
- That `Pedestrian Pavement` joins road nodes.
- The M1/M2 legs' bends (to be planned with b16plan.py).
- Cargo Center and Riverside Bridge sites (only a colliding dry run for Cargo Center; none for the bridge).
- The second intake site.
- TAmpa yields applied to a new city (households may start smaller than 4.0).
- Station-to-block distances were computed on centre points, not on growables.
- No blind second opinion was run on this plan (the orchestrator asked for an immediate report). A refute-only consult on §1 and the trunk network is recommended before P3.

## 8. Amendments after the refute-only review (2026-09-27, one voice: Cursor gpt-5.6-sol-high; codex/grok not run)

Checked by the orchestrator against the plan text and CS1 rules; adopted:
1. **T1 needs two in-city stations.** An outside rail connection is not a line stop. P3 builds Central **and** a second passenger station (Riverside East (1721,78) or North brought forward), then T1 between them.
2. **Landfill decoupled from W demand.** The D2 access road (from node 29000) and the `Landfill Site` are built in P1 regardless of W; D2 *zoning* stays gated on W >= 30.
3. **Single-access throat.** IC North Gate moves from P3 to P2. Bus lanes extend over Station Ave x 200..520 as well. SB flyover trigger lowered to crossover density >= 60 (was 80).
4. **No bus parallel to M1.** When M1 opens, B1 and C1 are re-routed as feeders ending at M1 stations (no segment within 150 m of M1 between stations).
5. **Metro geometry.** Every metro leg is planned with the Dubins planner (tmp/tampa/metro/b16plan.py pattern: 30 m straights, R >= 50, bends <= 40 deg); direct station-to-station bends measured 63 deg (West Gate), 78 deg (Central M2), 77 deg (Southfield). Gate before P3: dry-run M1 Central -> Civic -> West Gate -> Riverside NE with generated nodes; max bend <= 40.
6. **Targets.** "Modal share" is renamed **boardings per resident per week** (transfers count twice).
Noted, not yet acted on: NR/CS and CS/SF district boxes overlap (x 280..1300, z -400..-300; z -1500..-1300), so the capacity table may double-count; recomputed jobs without NE are 22.6% short (not 30%); office capacity depends on education levels; Ring South must be rebuilt before any frontage grows.

## 9. Player directives (2026-09-27, later)
- "Have highways and retail pads beside it like in Dallas TX": limited-access highways with one-way frontage roads, slip ramps, U-turns at interchanges, and commercial retail pads off the frontage roads backed by collectors. Builder: tmp/portville/hwy/.
- "Use the industrial space I have built as industrial and build/connect off of that": the player's industry east of the rail (x ~2030-2424, z ~340-686, Large Road with Median, beside Nuclear 31187 / Solar 45983) is the industrial core; extend it there with a direct truck route to a highway and a Cargo Train Terminal on the mainline (supersedes the E1 Cargo Center site in §3.2 where they conflict).
- "Put rail systems in place now for connectivity": Central Station + a second passenger station + the cargo terminal are built now, ahead of the Train unlock (bridge placement does not check unlocks); lines open at unlock. Builder: tmp/portville/rail/.
- "Make sure phase 4 preserves and expands upon Central Park. Do not put any residential/commercial/industrial zoning in Central Park." (2026-09-29): Central Park is protected. No zoning of any kind inside it, from any tool; it grows outward in Phase 4. Rules and steps: `portville-phase4-plan.md` §1; boundary in `city.md` → Protected areas.
- "Model Central Park after Central Park in NYC. Make it a very similar replica." (2026-09-29): layout in `portville-phase4-plan.md` §1.4.
- "You can buy any land you find strategic": a /state/areas + /commands/unlock-area bridge endpoint is being added; it goes live after one game restart (orchestrator's call, taken at a quiet point with a verified save first). NE tile first (§0).

## §10 P4 access-first plan (draft)

Read-only planner, 2026-09-27 17:4x-18:3x local, game 2032-02..03. Nothing was built, placed, bulldozed or edited: GET reads, `dryRun:true` build-network pieces and `validate:true` dry-run placements only. Scripts, inputs and every result: `tmp/portville/p4plan/` (plan.py, legs.py, dry.py, wfront.py, wbroads.py, covrun.py; stations_dry.json, legs.json, dry_legs.json, waterfront_dry.json, roads_dry.json, station_catch.json, terrain80.json, usable_w.json). Tags: [M] measured live, [E] estimate, [U] unverified. Coverage figures below were computed by this planner only; no consultant derived them.

### 10.0 What changed from §3.2 and why

- **M1 today is too short to be a trunk** [M, progress-portville.md "M1 fix"]: 3 stations, 908 m end to end, 4-18 riders/week (mean ~10) even after B1 stopped duplicating it. Every extension below adds hops walking cannot replace: a river crossing (WF->WBE, 1.18 km of tunnel), a couplet crossing (WG->WF), the rail line (Central->East Works), or a >1 km chain.
- **Civic stops being an M1 stop.** It is 236 m from Central-M1 and adds no coverage: build-out coverage is identical with and without it (80.9% homes / 80.7% jobs either way) [E]. Keep the building and platform as through track; remove the stop from lines 30/173 (line edit, no demolition). This also clears B1's stop 5 (1004,833), 214 m from Civic.
- **The M2 interchange moves from Civic to Central** (CM2 on Station Road). M1, M2 and T1 then sit within ~190 m of each other at one point. This is the "one walkable point" the brief asks for. Civic is 306 m from the T1 platform, so it does not qualify.
- **§3.2's "Riverside SW (-560,-330) entirely under the river" is wrong** [M]: (-560,-300) is dry land at y 125 (validated dry run, no water). The river at x -560 spans z ~160..640.
- **Westbank is a separate landmass** [M, 80 m survey `tmp/transitcity/survey/heights80.json` + terrain80.json]. It is bounded by the river (NE), the lake (N), Crowley highway (S, z -800..-960) and the Holmes/Underhill couplet on the SW bank (x -80..0). Its flat core has a moderate ridge at x -1440..-720, z -400..-160.

### 10.1 Station set (every lot `validate:true` dry run, stations_dry.json) [M]

| Key | Line | Entrance (x,z) a | Fronts | Platform ends | Dry run |
|---|---|---|---|---|---|
| WF Waterfront | M1 | (-408.6,700.1) 151.7 | new Waterfront Drive (shore-parallel, -28.3 deg), inland side | (-339.5,676.5) / (-466.3,744.8) | canPlace, clean |
| WBE Westbank East | M1 | (-668,-282) 180 | Westbank Boulevard z -310, north side | x -596..-740, z -270 | clean (plan spot -700 was SlopeTooSteep) |
| WBC Westbank Centre | M1 | (-1348,-282) 180 | same | x -1276..-1420 | clean (-1300 SlopeTooSteep) |
| WBW Westbank West | M1 | (-1850,-282) 180 | same | x -1778..-1922 | clean |
| AIR Airport | M1 | (-2330,-385) 180 | new Airport Road z -401, north side; the Airport front is 33 m south | x -2258..-2402, z -373 | clean |
| HAR Harbor | M1 | (-2050.2,628.5) 32.9 | new Harbor Road (bearing 32.9 deg); harbor across the road, ~40 m | (-2104.1,579.3)/(-1983.2,657.5) | clean (plan spot 22 m closer: SlopeTooSteep) |
| EW East Works | M1 | (2160.4,577.8) 201.8 | Sterling St seg 11033, north side | (2089.1,562.2)/(2222.8,615.7) | canPlace; **demolish 3109** (H1 4x4 Mediumfactory02). Alternatives: (2191.2,554.6) a21.8 -> 22689; (2157.7,541.2) -> 41903 |
| NEE1 / NEE2 / NEE3 | M1 | (2099.5, 1152 / 1752 / 2352) 270 | new East Central Avenue x 2120, west side | x 2087.5, +-72 | clean (NE east has no roads yet) |
| NCN NC North | M2 | (1063.5,2324) 270 | Victoria St extended north (x 1080), west side | x 1051.5, z 2252..2396 | clean |
| NCW NC West | M2 | (1063.5,1762) 270 | Victoria St seg 35970, west side | x 1051.5, z 1690..1834 | clean at 18:2x. East side (1096.5,1784) had grown RH 5483/32235 by then |
| NCS NC South | M2 | (1336.5,1286) 90 | Alexander St seg 34530, east side (a 90) | x 1348.5, z 1214..1358 | canPlace; clean at 17:5x, **by 18:2x demolish 10699** (H1_3x2 RH corner). Every lot on x 1320 now needs one demolition |
| CM2 Central-M2 | M2 | (1424.5,600) 90 | Station Road seg 15572, east side (24.5 m off centre) | x 1436.5, z 528..672 | clean; west side is SlopeTooSteep south of z 624 |
| NR Riverside NR | M2 | (888,326.5) 180 | Harris St seg 31289, north side | x 816..960, z 338.5 | clean |

- Existing: West Gate 29599 (west end node 27487), Central-M1 39708 (east end node 7706), Civic 43595 (kept as through track), T1 Central 41442 / Riverside East 39763 / North 518.
- **Lot race** [M]: the NC lots filled between 17:5x and 18:2x while the zoning stager ran, as the M1 lots did at 16:53. Either reserve NCW/NCS/NCN/CM2 first (place the entrances before lines, or unzone the 4 lots), or re-scan immediately before placing.
- M2 vs T1 spacing (the ~300 m rule): NCW-North 505 m, NCS-Central 551, NCN-North 662, NR-Riverside East 876. CM2-Central 187 m is the intended interchange. New M1 stations are all >500 m from T1 (EW-Riverside East 659).

### 10.2 A. M1 west: West Gate -> Waterfront -> under the river -> Westbank -> Airport -> Harbor

Legs were planned with b16plan.py (Dubins, R >= 50, 30 m straight at every platform end, 25-60 m chords). Every piece was dry-run (`Metro Track`, elevation -12; the -24 pieces are L9 only). Max turn includes the joins at both platforms [M, legs.json, dry_legs.json]:

| Leg | Length | Pieces | R | Max turn | Max grade | Dry run |
|---|---|---|---|---|---|---|
| L1 WG(27487) -> WF | 834.7 m | 15 | 50 | 28.9 | 10.3% | all ok |
| L2 WF -> WBE (river) | 1,178.1 m | 42 | 50 | 32.0 | 27.8% (the shore drop) | all ok; 18 pieces over water |
| L3 WBE -> WBC | 536.0 | 10 | 50 | 0.0 | 6.7% | ok |
| L4 WBC -> WBW | 358.0 | 7 | 270 | 0.0 | 2.9% | ok |
| L5 WBW -> AIR | 355.4 | 7 | 50 | 18.4 | 2.0% | ok |
| L6 AIR -> HAR | 1,094.6 | 39 | 50 | 32.0 | 7.5% | ok |

- **Station spacing:** WG-WF 959 m straight across the couplet, WF-WBE 1.0 km across the river, WBE-WBC 680, WBC-WBW 502, WBW-AIR 490, AIR-HAR 1.05 km.
- **Order and terminus:** the Airport sits mid-line and the Harbor is the terminus. The Airport has **0 transfers** to Central, over 8 stops without Civic. The Harbor also has 0 transfers.
- **Under-river unknown** [M/U]:
  - `build-network` sets y = `SampleRawHeightSmoothWithWater` + elevation (src/RoadCommands.cs:39). Over the river, -12 therefore means 12 m below the **water surface**, not the bed.
  - L2's water pieces sit at y ~91-100. The riverbed is y ~22 (terrain survey), so the tunnel runs through the water column.
  - I believe the game's own tool does the same, but this has not been verified in this bridge.
  - Gate: build L2 plus WBE, then a 2-stop test line WF<->WBE (bisect rule) before extending further.
  - Alternative if it fails: a pier-based `Metro Track Elevated`/Bridge crossing, not planned here.
- **Westbank district (bbox):** x -2800..-240, z -760..240. That is 2.36 km² usable (flat or <=10% slope, owned, not water, >=4 m above adjacent water, >40 m from highway/rail), of which 1.88 km² is flat [M survey].
  - Harbor peninsula: x -2320..-1360, z 240..640, 0.24 km² usable.
  - The south strip along Crowley (z -960..-800, 0.48 km²) should be jobs, parks or buffer, not homes.
- **Westbank roads, dry run** [M, roads_dry.json]:
  - `Large Road with Tree Median and Bus Lanes` spine z -310, x -2480..-160: 29 pieces ok, max grade 6.4%.
  - Airport Road z -401 plus collectors x -2480 / -2160: ok, <=3.8%.
  - Harbor Collector x -2000, z -310 -> 640: 12 pieces ok, **max grade 13.6%**.
  - Harbor Road: **35.6% on its last 20 m piece**. Re-profile it, or let the harbor lot's own grading absorb it.
  - Car access into Westbank (IC Westbank on Crowley, and/or a right-in/right-out onto Underhill NB) is not designed here.

### 10.3 B. M2 north-south, interchange at Central

- **M2:** NCN (1063.5,2324) -> NCW (1063.5,1762) -> NCS (1336.5,1286) -> CM2 (1424.5,600) -> NR (888,326.5). CS and SF follow in P5 under the river.

| Leg | Length | Pieces | Max turn | Max grade | Dry run |
|---|---|---|---|---|---|
| L14 NCW -> NCN | 418.0 | 8 | 0.0 | 3.1% | ok |
| L8 NCW -> NCS | 473.5 | 10 | 29.3 | 5.6% | ok |
| L9 NCS -> CM2 | 550.1 | 11 | 9.7 | 19.2% | ok. Crosses **under** the Central-M1 platform (seg 15176, y 164.2) at elevation -24 (y 153.0), 11.2 m below |
| L10 CM2 -> NR | 549.9 | 20 | 31.2 | 5.7% | ok |

- **Reviewer proposal tested** [E, covrun.py]: (1150,1750) -> (1330,1250) -> (1208,766) -> (1080,400).
  - Build-out coverage of the P4 districts: reviewer 79.0% homes / 79.9% jobs, with NR 60% of homes. This plan: 80.9% / 80.7%, NR 77%.
  - The reviewer's Civic interchange is 306 m from T1, and no Civic lot with a N-S platform was found (Downtown has no N-S frontage between x 1000 and 1400).
  - Its (1080,400) is 422 m from CM2.
- **Crossing M1, primary: depth separation.** The bridge creates segments without NetTool collision checks: `build-network` dry run checks only the path-on-road rule (RoadCommands.cs:47-66). So an M2 segment 11 m under M1 needs no node and touches no M1 lane. It is unverified in this game.
  - Move L9 node 8 (1427,756) at least 10 m (XZ) off the M1 platform chord before building.
  - Build L9's three -18/-24 pieces first, watch M1 (lines 30/173) for a game day, then the rest (the B19 staging rule).
- **Fallback 1:** a 4-way crossing node on M1 seg 13243 at x ~1359.5, with both lines straight (the B19 method, proven once). It needs CM2 on the west side of Station Road (1371.5, 624..672, a 270, clean) and splits M1 6 m from the Central platform end.
- **Fallback 2:** M2N (NCN..NCS -> a terminus north of M1) plus M2S (CM2 -> NR). Vanilla route choice has no transfer penalty (lessons 2026-09-26). This costs only walking between the two platforms.
- **Station Road North link** (x 1400, z 790 -> 1030, Medium, 3 pieces ok, <=3.7%) gives NCS and the NC grid a 240 m walk to Central instead of ~1.2 km. It needs **shop 15507** (L2 2x3 Shop15, (1392,822)) demolished.

### 10.4 M1 east: Central -> East Works -> NE East (added to meet the 80% / 70% targets)

| Leg | Length | Pieces | Max turn | Max grade | Dry run |
|---|---|---|---|---|---|
| L7 Central(7706) -> EW | 623.1 | 12 | 29.3 | 3.2% | ok |
| L11 EW -> NEE1 | 545.5 | 20 | 30.9 | 4.2% | ok |
| L12 NEE1 -> NEE2 | 456.0 | 9 | 0.0 | 1.2% | ok |
| L13 NEE2 -> NEE3 | 456.0 | 9 | 0.0 | 3.7% | ok |

- **Why:** without EW and NEE1-3 plus NCN, build-out coverage is 65.7% homes / 63.3% jobs. The NE tile is 3.35 km² usable and has stations only on its west edge.
- **East Central Avenue** (Medium, x 2120, z 1040 -> 2440, elevated +12 over the E-W highway at z 1360-1600) must come first: 17 pieces dry-run ok, max grade 15.3% on the ramp pieces.
- **Line shape:** M1 becomes one through line Harbor <-> NEE3, with 13 stations without Civic and ~9 km. Split it at Central into two lines if bunching appears; the two lines can share Central-M1.

### 10.5 C. Harbor and Airport

- **Airport** (-2350,-450) a 0: validated, canPlace, no collisions [M]. It reports **subBuildings 1** (the apron), so the bridge **refuses a real validated placement** (BuildingCommands.cs:555-560, TODOS line 250). The player places it with the in-game tool at exactly these coordinates, or the bridge learns sub-buildings.
  - Road: its front (z -418) faces Airport Road (z -401).
  - Link: M1 AIR, 33 m from the airport front, **0 transfers to Central**.
- **Harbor** (-2077.2,699.6): validated, canPlace, subBuildings 0 (bridge-placeable).
  - The snap varies between runs: -147.1 deg at (-2080.3,704.4) at 17:5x, with a map rock 45375 in the footprint; -163.5 deg at (-2078.6,705.0) at 18:2x with no collisions.
  - The dock connects to the ship lane at (-3375,131); water 91.2, 20 m above it.
  - Road: Harbor Road along its front.
  - Link: M1 HAR across that road, **0 transfers to Central**. H1 (the §3.4 harbor bus) is cancelled.

### 10.6 D. Bus restructure (after each trunk opens, not before; checked against live stops, 300 m) [M, transit0.json]

| Line | Duplication found | Action |
|---|---|---|
| B1 135 West Gate Feeder | WG stops 0/1/3/7, and stop 5 214 m from Civic | Keep. Once Civic is off M1 it touches only WG. |
| B3 164 North Gate D2 | stop 0 (1488,746) is 55 m from Central-M1, and stops 1 and 7 are 182 / 261 m from WG, so it **duplicates the Central-WG hop now** | **Cut stop 0** and terminate at WG. D2 feeder only. |
| B2 218 Riverside NR - D1 South | stops 0/1/11 (Central, Station Road) and stop 2 (1280,480) near CM2, plus stops 3/4/8/9 near NR, **duplicate CM2-NR** | When M2 opens, **cut stops 0, 1, 2, 11**. Re-terminate at NR (Harris St) as an NR/D1 South loop (x 400..1040). |
| N1 94 North Central Feeder | stops 3-12 are within 32-286 m of NCN/NCW/NCS; stop 0 is at North | When M2 opens, **rebuild** as a North-only feeder for NE north (z 1880-2300), >300 m from NCW/NCN. Cut stops 3-12. |
| New WF1 Waterfront | - | Loop on Waterfront Drive + Avenue with stops at SeaWorld, Balloon Tours, Expensive Park and Botanical garden. Touches WF only. |
| New WB-N, WB-S | - | One Westbank loop each, touching WBC and WBE respectively. Never two M1 stations. |
| New NEE feeder | - | NE east loop touching NEE2 only. |

### 10.7 E. Per-station catchment (500 m straight line; not exclusive between stations) [E, station_catch.json]

- "Now" = live growables (1,121 at 18:2x) x planning yields (§1.1).
- "Build-out" = §1.3 district totals spread over usable 80 m cells. WF was added (600 homes / 500 jobs), and CS was trimmed to z < -400 to remove the §8 overlap.
- Zoned-but-empty cells cannot be located: `/state/zones` returns totals only (a gap).

| Station | Homes now | Jobs now | Homes build-out | Jobs build-out | Walking access (side people live) | Demolitions |
|---|---|---|---|---|---|---|
| WF | 0 | 0 | 563 | 469 | Drive sidewalks, promenade cross-links, and the couplet footbridge from Pearl Blvd / D1 | 0 for the lot; waterfront utilities, see G |
| WBE | 0 | 0 | 1,675 | 756 | spine sidewalks; south side crosses at collector junctions | 0 |
| WBC | 0 | 0 | 1,951 | 880 | same | 0 |
| WBW | 0 | 0 | 2,141 | 966 | same | 0 |
| AIR | 0 | 0 | 2,106 | 950 | Airport Road plus collectors x -2480 / -2160 | 0 |
| HAR | 0 | 0 | 604 | 273 | Harbor Road plus Harbor Collector | 0 (map rock 45375 only if the -147 deg snap recurs) |
| EW | 0 | 999 | 117 | 2,674 | Sterling St; industry both sides | 3109 |
| NEE1 | 0 | 202 | 1,014 | 2,009 | East Central Ave (to be built) | 0 |
| NEE2 | 0 | 0 | 1,495 | 2,023 | same | 0 |
| NEE3 | 0 | 0 | 1,573 | 2,128 | same | 0 |
| NCN | 467 | 41 | 1,799 | 1,805 | Victoria St, extended north | 0 |
| NCW | 873 | 253 | 1,502 | 1,643 | Victoria St (NC grid both sides) | 0 now |
| NCS | 783 | 144 | 1,192 | 1,878 | Alexander St | 10699 |
| CM2 | 106 | 552 | 1,063 | 2,231 | Station Road; 170-190 m to Central-M1 and T1 | 0 |
| NR | 343 | 36 | 2,214 | 1,402 | Harris St, NR grid | 0 |

**Coverage (500 m to a rail/metro entrance)** [E]:

| Scenario | Homes | Jobs |
|---|---|---|
| Live growables, existing stations | 84.6% | 63.5% |
| Live growables, this plan | 98.6% | 88.1% |
| Build-out, P4 districts (D1, DT, D2, NR, E1, NE, WF, WB), existing stations | 21.3% | 37.4% |
| Build-out, P4 districts, this plan | **80.9%** | **80.7%** |

- Targets: >=80% of homes, >=70% of jobs. Homes pass by 0.9 points only.
- By district (homes / jobs): NE 81/81, WB 78/78, NR 77/77, D2 73/73, WF 94/94, D1 and DT 100.
- **Zoning rule that makes it hold:** paint R only within 500 m of a station. Put jobs, parks or buffers on the WB south strip, the NE far east (x > 2560) and NR south.
- N1, CS and SF need their P5 stations: all districts together are 50.1% / 61.0% without them.

### 10.8 G. Waterfront (player requirement 2026-09-27)

- **District:** NE bank terrace between the shore and the couplet, x -960..0, z 400..1040. That is 0.31 km² usable, 15-35 m above water (y ~119-138 vs water ~99-107) [M survey].
- **Shore contour** at y 112 [M, 15 transects, shore_transects.json] runs (-976,824) -> (-674,692) -> (-393,538) -> (-133,364) -> (18,298). Everything planned sits >=40 m inland of it, >=10 m above water.
- **Roads** [M dry runs, waterfront_dry.json; 21/22 ok, the 22nd a 2 m remainder]:
  - **Waterfront Avenue** (Medium): from Holmes Boulevard node 24949 (74,1063) west and down x -40 to z 474. 24949 is already the Dixon crossover junction, so this adds a leg to an existing junction, not a new couplet junction.
  - **Waterfront Drive** (`Small 4 Lane Road with Bus Lanes`): shore-parallel, 120 m inland, from (-40,474) to (-908,941).
- **Promenade** (`Pedestrian Pavement`): 13 pieces along contour +40 m, from (-19,369) to (-937,863). There are 5 cross-links to the Drive, each ending 14 m from the Drive's centre, i.e. within 16.5 m of its sidewalk lane (pathOnRoad rule).
- **Couplet footbridge** (`Pedestrian Elevated`, +12 m): from (214,818) by Young St / Pearl Blvd to (-26,818) by the Avenue, crossing Underhill and Holmes. All pieces dry-run ok (paths 23/23).
- **Attractions** (all validated and unlocked:true unless stated):

| Attraction | Lot | Result |
|---|---|---|
| **SeaWorld** (unique) | (-301.5,684.2) a151.7 | clean; front 103 m from WF. The first lot, (-230.1,648.0), was clean but 182 m away. §3.8 had it in the NE Culture Quarter. |
| 10thAnniversary Park | (-106.9,581.6) | clean |
| Expensive Park | (-538.1,813.9) | clean |
| ChirpyBirthday Balloon Tours | (-702.2,888.7) | clean |
| Botanical garden | (-809.7,942.0) | clean |
| Regular Plaza (WF forecourt) | (-391.0,623.3) | clean |
| Tennis_Court_EU | (-234.5,534.4) | clean |
| Expensive Plaza | (-667.6,763.2) | clean |
| MerryGoRound | (-804.6,846.1) | clean |
| JapaneseGarden | (-512.3,693.2) | **collides with Wind Turbine 42033** |
| Fishing Island (shoreline) | (-845.2,749.4) | canPlace, but subBuildings 1: the player places it |
| Floating Cafe (shoreline) | (-264.3,448.3) | canPlace, but subBuildings 1: the player places it |
| Floating Cafe (shoreline) | (-602.3,646.1) | **collides with Water Treatment Plant 14649** |

- **Not in this install:** Leisure/Tourism specialisation (`/state/policies` lists none), beach, pier and marina prefabs. Use CL/CH on the Drive's inland side, and HighriseBan on the shore blocks if the player wants low-rise.
- **Transit:**
  - WF sits on the Drive. SeaWorld (103 m), the WF plaza, Tennis and 10thAnniversary are within 300 m of WF.
  - The NW attractions (Expensive Park, Expensive Plaza, Balloon, Botanical, MerryGoRound) are 300-560 m from WF, so they get WF1 feeder stops.
  - The Westbank shore (x -560..-200, z -200..160) is within 500 m of WBE. The harbor quarter is at HAR.
- **Water pollution: move it before the waterfront opens** [M, live 18:1x]. On the promenade line, within 60-150 m of WF, are:
  - Water Outlet **8035** (-554,610);
  - a **Water Treatment Plant 14649** (-616,653);
  - Wind Turbines **6243** (-479,610) and **42033** (-543,689);
  - poles 31773 and 38166, and power line 19464.

  The water surface falls from 107.7 at (-560,560) to 98.2 at the junction, 91.0 at the harbor and 84.8-90.0 further west [M]. So sewage from 8035/14649 flows along the waterfront and past the Harbor.
- **Relocation site:** tile (0,2) shore (-3691.9,388.6), water 88.2, validated for both `Water Treatment Plant` (angle 36.4) and `Water Outlet`. It is downstream of the Harbor (91.2) and ~1.6 km from it.
  - Needs ~3.3 km of pipe from the NE-bank network.
  - Order: build the new plant and pipe, confirm sewage capacity, then demolish 8035, 14649 and the two turbines (replace their power).
  - The intakes 1428 (1128,-623) and 38338 (1854,-1320) are upstream and unaffected.

### 10.9 F. Build order and acceptance

1. **Now (no track):**
   - Remove Civic from lines 30/173.
   - Cut B3 stop 0.
   - Reserve the NC/CM2 lots (place the four entrances, or unzone the lots).
   - Save.
   - Accept when: line ids unchanged, both lines Complete with no problems; B3 has no stop within 300 m of two M1 stations.
2. **Waterfront utilities:** new treatment plant and outlet at the tile (0,2) shore, pipe, power; then demolish 8035, 14649, 6243, 42033 and poles 31773/38166.
   - Accept when: sewage capacity >= use, no Sewage or Electricity problems, no outlet on the NE bank.
3. **M1 west, stage 1:**
   - Waterfront Avenue, Drive and footbridge; WF entrance; L1.
   - Accept when: bends.py max <= 40; the WG/WF platform ends match the plan to <1 m; a 2-stop test line WG<->WF is Complete with no problems (then delete it).
4. **M1 west, stage 2:**
   - L2 plus WBE, then test line WF<->WBE (the under-river gate).
   - Then the spine plus WBC/WBW/AIR (L3-L5), the Airport (player), and HAR plus Harbor (L6).
   - Accept when: every piece is connected, the lines are extended by stop edits (or recreated per the lesson), trains = target, riders > 0 within 3 weeks, Airport and Harbor show 0 transfers to Central in the line stop lists.
5. **M1 east:** demolish 3109, EW, L7; then East Central Avenue, NEE1-3, L11-L13.
   - Accept when: bends <= 40 and the lines are Complete.
6. **M2:**
   - Station Road North link (demolish 15507), the M2 entrances.
   - L9's -24 crossing pieces first; watch M1 for a game day.
   - Then L8, L9 rest, L10, L14, and lines M2S/M2N (one per direction).
   - Accept when: M1 shows no problems through the day, M2 is Complete, M2 riders > 0.
6b. **Pollution clean-up (player rule, 10.11)**, before or with G:
   - relocate Combustion Plant 46375;
   - relocate Landfill 33150;
   - rezone the D2 industry that touches homes.
6c. **Phase H, monuments (10.12)**, only after the waterfront (G) is open.
7. **Bus restructure** per 10.6, each as its trunk opens; the new feeders last. Measure 4 periods before and after (m1fix method).
8. **Overall P4 acceptance** (adds to §6 P4):
   - build-out coverage recomputed from live growables >= 80% homes / 70% jobs;
   - M1 >= 150 riders/week per direction after 8 periods [E target];
   - no bus with stops within 300 m of two stations of the same trunk.

### 10.10 Risks and unknowns

1. **Under-river tunnel:** runs in the water column (elevation is water-surface relative). Untested; L2 gate.
2. **M2 crossing:** depth separation under the M1 platform is untested. Two fallbacks exist (10.3).
3. **Airport, Fishing Island and Floating Cafe have sub-buildings:** the bridge refuses them, so the player places them.
4. **Lot races with the zoning stager** (seen on NCW/NCS within ~30 min).
5. **Grades:** L2 27.8% and L9 19.2% (the M1 precedent was 27.8%, "cosmetic"). Harbor Road 35.6% and East Central Ave ramps 15.3% need re-profiling.
6. **Build-out homes coverage 80.9%** has a 0.9-point margin and rests on a uniform-density model. It depends on the zoning rule in 10.7.
7. **Westbank car access** (IC Westbank or an Underhill link) and **NE-east rail crossing** are not designed. §3.3 and the P3 NE notes list them as open.
8. **Harbor snap angle varies** between dry runs (-147 vs -163 deg).
9. **build-network dry runs check no collisions** (only the path-on-road rule). Track and road dry runs prove the prefab and Y only. Station lots were checked with validated placements, which do see tunnel collisions: CM2 at z >= 672 hit M1 seg 15176.
10. **Not done:**
    - no bends.py run on live track (nothing was built);
    - no bus routing (route3.py) for the new feeders;
    - no consultant review of this plan or its figures.

### 10.11 Pollution buffer (player rule 2026-09-27: no industry or landfill next to homes)

**Rule for every district in this plan.** Nothing below may be within **200 m** of a residential zone or growable:
- industrial zoning or industry buildings;
- landfill, incinerator/combustion or any other polluting site (cargo terminals, power plants other than wind/solar).

The buffer between them is offices, commercial or parks. Applied to this plan:
- **Westbank:** no industry at all. The Crowley strip (z -960..-800) is offices, commercial or parks.
  - The Airport and Harbor make noise, so lots within 200 m of them are offices or commercial, not homes. This lowers WBW/AIR/HAR home capacity a little; it is not in the 10.7 figures [E].
- **East Works / NE east:** EW's catchment is jobs only (its 117 build-out homes become 0).
  - NEE1 homes go only north of z ~900, >=200 m from E1 industry, Landfill 40367 (2360,683) and Nuclear 31187 (2250,643).
  - Offices fill z 700-900.
- **Waterfront:** the treatment plant, outlet and turbines leave (10.8). No industry.

**Existing violations** [M, live 18:3x; these belong in the TODO list]:
- **Combustion Plant 46375** (1360,285), in NR: nearest home 45 m (23401), 22 homes within 200 m.
  - Relocation site validated clean: (2330,250) or (2300,120), in E1, nearest home ~950 m.
- **Landfill Site 33150** (440,1391), in D2: nearest home 63 m (47822), 24 homes within 200 m.
  - Relocate to E1 beside Landfill 40367, or empty it (set-building-emptying) and replace it with recycling or combustion in E1.
- **D2 industry growables** 22451, 18736, 29750, 45377, 48256 (x 260-424, z 1230-1254) are 9-20 m from homes 15378, 19377, 25367 and 18644.
  - Fix: rezone that industrial band to offices, or rezone the homes beside it to commercial/offices.
- **Cargo Center 42520** (1643,441): nearest home 273 m, so outside the rule. Keep homes out of 200 m of it.

### 10.12 H. High-traffic monuments downtown, at stations (after G)

Every lot below is a `validate:true` dry run [M, monuments_dry.json]. Every entrance (lot front centre) is <=150 m straight-line from a metro or rail entrance. Order: waterfront (G) first, then H.

| Slot | Monument | Lot (x,z) a | Front -> station | Dry run |
|---|---|---|---|---|
| H1 | **Stadium** 16x8 | (1352,590) 270, Station Road west side, facing east | (1384,590) -> CM2 42 m | clean |
| H2 | **City Hall** 8x6 (or Friendly Neighborhood 8x7 at (1444,530), or Fountain of LifeDeath 7x7 at (1444,535)) | (1440,530) 90, Station Road east strip, facing west | (1416,530) -> CM2 71 m | clean |
| H3 | **SeaWorld** 12x12 (from G) | (-301.5,684.2) 151.7, Waterfront Drive | -> WF 103 m | clean |
| H4 (option) | Grand Mall / Theater of Wonders / ScienceCenter / Cathedral / Modern Art Museum / ExpoCenter / Colossal Offices / Winter Market | (1332..1320, 590..620) 270, Station Road west | -> CM2 49-71 m | all clean. **Same slot as H1**: Stadium or one of these, not both |

- **Culture Quarter** (Ring South, x 1200..1300): every §3.8 anchor tested is clean there (Grand Mall, Theater, ScienceCenter, Cathedral, Modern Art, Expo, Colossal, Winter Market, High Interest Tower, Library).
  - But the fronts are **204-277 m** from CM2, so they fail the 150 m rule.
  - Keep that quarter for the high school and parks (§3.8), or add an M2 infill stop there later.
- **North station forecourt** (x 1400..1470, z 1760 / 2000): every big-monument lot needs 6+ growables demolished and collides with road segments. Not recommended.
- **Station Road North link east side** (planned road): collides with Central's forecourt and track segments (9331, 10363, 11248). Not usable.
- **Overflow, not downtown:** the Westbank Boulevard south side at WBC is empty. Grand Mall at (-1348,-382) a0 is clean, 48 m to WBC. Theater (-1200) and Expo (-1500) are clean at 156-159 m.
- **Unlock state:** unlocked:true per /prefabs/buildings. StatueOfWealth returned AlreadyExists, so it is already built. The bridge cannot read the uniques' own achievement conditions (§3.8 note), so the player confirms in the Unique Buildings panel.
- **Access:** Stadium event crowds use CM2 and Central-M1 plus T1 (all within ~190 m). The Stadium sits on the Ring side, away from RH (§3.8), fronting Station Road (Medium).
  - Station Road is the only car access, so watch its density on event days.


### 10.13 Review amendments → city Phase 3 (orchestrator, 2026-09-27 18:5x)
Blind refute-only reviews of §10 by Cursor grok-4.7-medium and codex (gpt-5.6-sol), prompts in scratchpad p4review/. Both independently found: (1) the order builds 8 km of metro into unbuilt/walkable land before the waterfront the player asked for first; (2) the river tunnel (L2, track in the water column) is an unproven gate that strands six stations; (3) removing Civic or cutting B3/B2/N1 before a replacement hop is proven ships a worse state (B1's cut added ~10 metro riders and cost the bus ~25%); (4) stations in empty districts (NE East, Westbank West, Airport/Harbor) will repeat M1's near-zero usage until homes/jobs exist; (5) airport/harbor are provisional (Airport needs the player to place it; harbor angle unstable). Split: grok's cheapest test = the 2-stop river tunnel line; codex's = end B3 at West Gate and measure transfers. Both are adopted, in order.

**Phase 3 (this city's next phase), in order:**
- 3a Utilities first: surplus generation (running, "Power 2"); new Water Treatment Plant + Water Outlet at the validated west site (−3691.9,388.6) BEFORE removing 14649/8035; then remove turbines 6243/42033 and the promenade-blocking poles/line; demolish Landfill 33150 once empty.
- 3b Waterfront: Waterfront Avenue/Drive, the promenade (paths end at road edges, lane-linked), the couplet footbridge, first attractions (SeaWorld + parks/plazas at WF), WF1 feeder bus touching West Gate only; verify walking access on the ground (pedestrian lane links read from /state/networks).
- 3c Reserve M2 lots now (place NC North, NC West, NC South, CM2 entrances; no track) — lots regrow within a minute.
- 3d River gate: pause, build L2 + WF + WBE entrances only, 2-stop test line; LineNotConnected or 0 vehicles after one rebuild = stop the west plan.
- 3e Measure codex's feeder test: B3 ends at West Gate, Civic kept, 4 periods of M1 boardings vs the prior 4.
- Later (Phase 4): M2 track, Westbank grid + roads before its stations, M1 west beyond WBE, Harbor, Airport (player-placed), NE East after its roads, monuments (§10.12) after the waterfront is open. Civic stays in service until the Central cluster (CM2) exists.

### 10.14 Monument sites for walking and transit (2026-09-27, after the waterfront fix)

Rule: one draw per trunk stop, inside 150 m of that stop's entrance, on a lot a dry run cleared (no building collision, no road collision). A second unique at the same stop splits the same pedestrians and does not add a trip. Highways get no monuments. §10.12's downtown cluster is the earlier pass; this table is the one to build from.

The waterfront walk this plan sits on: Pedestrian Elevated 6783 joins promenade nodes 2764 (-690,808) and 14049 (-712,857) across Waterfront Drive, and 15638 joins 3863 (-559,775) and 5958 (-565,692). Both reused the existing nodes. The west promenade and the central promenade are one path. Sidewalks already run from the harbor landing (node 12092) to the waterfront metro in 6 hops and to the downtown footbridge road in 15 hops. The quay's 9 m gap to its path is the lane link, not a break.

| Stop | Entrance | Monument | Lot, validated clean unless noted | Walk |
|---|---|---|---|---|
| Waterfront metro | 3276 (-409,700) | SeaWorld 48833, already built | (-302,684) | 108 m |
| Waterfront metro | 3276 | Fountain of LifeDeath, not built yet | (-440,760) angle 0 | 67 m |
| West Gate metro | 29599 (544,810) | none | 18 houses inside 80 m, including 11256 at 16 m. No clean unique lot in x 420..700, z 700..940. Do not clear that block. Riders walk one stop to the waterfront, or use the z 818 footbridge | — |
| Civic metro | 43595 (1208,752) | City Hall 19581, already built | (1360,640), facing Station Road, active | 189 m |
| Central train and metro | 41442 (1514,764) and 39708 (1444,756) | Fountain of LifeDeath, not built yet | (1640,680) angle 0 | 151 m to the train doors |
| North train | 518 (1554,1881) | Fountain of LifeDeath, not built yet | (1634,1841) angle 0 | 89 m |
| Riverside East train | 39763 (1730,81) | cinema, not built yet | (1690,41) angle 0 | 57 m |
| West bank metro | 41930 (-668,-282) | none yet | Station still has garbage and crime and no road. A fountain goes on its entrance path only after that road exists | — |

Already built, and left where they are: SeaAndSky Scraper 30434, Transport Tower 48981, StatueOfWealth 36327, Lazaret Plaza 10384, Grand Mall 11940. They are not the forecourt of a trunk stop. Grand Mall is 315 m from the nearest stop; a closer bus stop is the fix, not a move.

### 10.15 Airport and the hubs (2026-09-27)

Decision: the airport waits until a passenger can leave it on a train. The asset site stays the one already validated, `Airport` at (−2350, −450), angle 0. The metro stop for it stays (−2300, −360). That is about 1.6 km west of the west-bank entrance 41930 (−668, −282), which is the current end of M1. 41930 still has garbage and crime and no road. Building the terminal before that road and that track would strand everyone who lands.

Order, and nothing earlier:

1. A road from the waterfront network to 41930, with water and power. The station's own problems have to clear.
2. Metro track from the west-bank station to (−2300, −360), same rules as the waterfront link: straight off the platform, turns under 40 degrees, then one stop added to both M1 lines.
3. Then place the Airport. Its road is the planned Airport Road at z −401, north of the building. A bus is not a substitute for the metro; the metro is the 0-transfer ride to Central.

Hubs, which already exist or are the only ones to add:

| Hub | Modes | Role |
|---|---|---|
| Central | Train T1, metro M1, buses on Station Road | The interchange. Every other hub is one ride from here. |
| Waterfront | Metro M1, buses H1 and WF1, the promenade | The walking hub. SeaWorld is the draw. |
| West Gate | Metro M1, buses B1 and B3, the z 818 footbridge | Transfer only. No monument on the houses. |
| East Works | Bus B4, the cargo track | Industry. B4 now reaches Oscar White Avenue and the south plants. |
| North train | Train T1, bus N1 | North homes. |
| Riverside East | Train T1, the east end of B4 | The south-east jobs and the cinema site. |
| Harbor | Bus H1 to the waterfront metro | One transfer to Central. Not a second airport. |
| Airport | Metro M1, after steps 1–3 | The last hub. Not a bus island. |

### 10.16 Side goal: every monument (2026-09-27)

Player, in chat: unlock and build all monuments and unique buildings as the city grows, without giving up connectivity.

Rule: one of each. The game refuses a second copy. A lot counts only when a validated placement has no building collision, no road collision, and the building is not `RoadNotConnected` after it appears. The fountain tried 52 m off Waterfront Drive failed that last test and was removed. Do not clear the West Gate houses for one. Place the next unique on a free road-fronting lot within 150 m of a stop that does not already have a draw.

Built (8): City Hall 19581, Grand Mall 11940, Lazaret Plaza 10384, SeaAndSky Scraper 30434, SeaWorld 48833, Statue of Shopping 10160, StatueOfWealth 36327, Transport Tower 48981.

Unlocked and not built: ExpoCenter, Oppression Office, High Interest Tower, Trash Mall, Theater of Wonders, Modern Art Museum, ScienceCenter, Cathedral of Plentitude, Colossal Offices, Fountain of LifeDeath, Friendly Neighborhood, Library, Stadium, Winter Market 01, Academic Library 01, Countdown Clock, Plaza of Transference, ChirpX Launch Control Center, ChirpX Launch Tower, cinema.

Locked on their own milestones, so they arrive as population and the special conditions are met, not by forcing them: Opera House, Posh Mall, Plaza of the Dead, Observatory, Business Park, Servicing Services, Court House, Official Park, Statue of Industry, Amsterdam Palace, Cathedral of Cologne, the European City Hall, Department Store, Gherkin, Government Offices, Hypermarket, London Eye, Shopping Center, Theatre, Arena.

### 10.17 Traffic check, each pass (2026-09-27)

Player, in chat: look at congestion as the city grows and fix access when the cause is local.

2026-09-27 22:06 game time, population 25,772, city flow 84. Sixteen segments were at density 60 or more. The worst is still the northbound ramp 27688 at 100, merging into Stephen Harris Avenue through the shops. Left as it was. Riverside East Forecourt 32290 is 95 on a 25 m basic road into large Walker Street. A medium-road replacement dry-ran, but shop 38198 sits 16 m from the north node, so widening it was not done. Alexander Street 15335 at 94 is an ordinary four-way. No change this pass.

## §11 Phase 4 plan

Superseded for Phase 4 by `portville-phase4-plan.md` (2026-09-29): §6 P4 and the §10.13 "Later (Phase 4)" list, updated with what is already built, plus the Central Park directive (§9).
