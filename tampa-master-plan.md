# TAmpa master plan: 41.8k to 100k with maximum interconnectivity

Status: PLAN ONLY. Nothing in this file has been built. Written 2026-09-27 from live bridge reads
(game time 2038-11-21) while another agent worked on airport transit.
Scratch data and scripts: `tmp/tampa/master/` (`cap2.py`, `model.py`, `land.py`, `comps.py`,
`cov.py`, `emptyblocks.py`, `zoom.py`, and the JSON snapshots they read).

Tags: **[M]** measured from the live game or the decompiled game code in this session.
**[E]** estimated (a model or rule of thumb; the assumption is named). **[G]** given in the brief,
not verified.

---

## 0. Headline

| Item | Value |
|---|---|
| Population now | 41.8k [G]. The bridge cannot read population. `citizens.count` 42,828 [M] also counts tourists. |
| Homes (households) now | **8,737** [M] (the game's `CalculateHomeCount` applied to all 1,997 residential growables) |
| Residents per home | **4.78** [E from G/M]. Each home holds at most 5 citizens, so the city is at about **96% of its housing ceiling** (43.7k). Growth needs new homes; nothing else will do it. |
| New homes needed for 100k | **12.2k** at 4.78/home, **14.6k** if new households average 4.0 [E]. Plan to 14.6k. |
| New jobs needed | about **13–14k** zoned jobs [E] (current ratio of 1.22 zoned jobs per home, minus today's ~10% job surplus). |
| Land | All **9 of 9** buildable tiles are already owned [M, inferred]. No more land can be bought. Free flat plateau inside them: **11.1 km²** [M, 40 m grid heuristic], of which 8.2 km² sits in blocks of 0.2 km² or more. |
| Is land the limit? | No, **if** the new districts are mostly high density. Core districts NW1 + W1 + C1 hold about 12.7k homes [E]; E1 and SW1 add 6.3k in reserve [E]. With low density only, 12k homes would need about 7 km² gross [E], i.e. almost all the free land. |
| Transit coverage now | 98.3% of growables within 400 m of a stop; 98.3% within 800 m of a rail or metro stop [M]. |
| Transit use now | 3,591 riders/week = 8.6% of population [M riders, G population]. Target at 100k: **12%** (12k/week), stretch 15% [E]. |

---

## 1. Capacity math

### 1.1 What the current zoning holds [M]

Source: `/state/growables` (3,122 buildings), `/state/zones`, and `ResidentialBuildingAI.CalculateHomeCount`
(decompiled): homes = max(100, w·l·k + rand(0..99)) / 100 with k = 20/25/30/35/40 (low, L1–L5) and
60/100/130/150/160 (high, L1–L5). Script `tmp/tampa/master/cap2.py` takes the exact expectation over the
random term.

| Zone | Zoned cells | Buildings | Footprint cells | Fill | Homes | Homes per zoned cell | Homes per footprint cell |
|---|---|---|---|---|---|---|---|
| ResidentialLow | 23,424 | 1,675 (75 L1, 24 L2, 205 L3, 470 L4, 901 L5) | 13,604 | 0.58 | 4,957 | 0.212 | 0.364 |
| ResidentialHigh | 3,876 | 322 (27 L1, 21 L2, 31 L3, 146 L4, 97 L5) | 2,709 | 0.70 | 3,780 | 0.975 | 1.395 |
| **Total** | 27,300 | 1,997 | | | **8,737** | | |

Jobs in zoned buildings, same method (`Commercial/Office/IndustrialBuildingAI.CalculateWorkplaceCount`):

| Zone | Zoned cells | Buildings | Fill | Jobs | Jobs per zoned cell |
|---|---|---|---|---|---|
| CommercialLow | 4,800 | 407 | 0.63 | ~2,940 | 0.61 |
| CommercialHigh | 2,798 | 280 | 0.73 | ~2,660 | 0.95 |
| Office | 1,566 | 140 | 0.66 | ~1,210 | 0.77 |
| Industrial | 3,311 | 298 | 0.75 | ~3,890 | 1.17 |
| **Total** | 12,475 | 1,125 | | **~10,700** | |

Other zone facts [M]: 22,137 cells in existing zone blocks are unzoned, but a dry-run sweep of
`set-zone` over the whole city found only **193 of 2,748 blocks with no building at all**. Most of those
(~100) are low ground on the south harbor front (x 3400–4600, z −2500…−1100). So existing roads offer little
new zoning.

### 1.2 Growth that needs no new roads [E]

| Source | Homes | People | Basis |
|---|---|---|---|
| Infill of existing zoned residential | +1,294 | +6.2k | RL fill 0.58 → 0.70, RH 0.70 → 0.75 (0.70–0.75 is what the most built-out zones here reach). |
| Every residential building reaching L5 | +1,139 | +5.4k | Same footprints, k at L5 [M formula]. Needs education, land value and services; plan for about half by 100k. |
| Upzoning RL → RH around stations | up to +0.77 homes per converted cell | — | **Player decision** (zone change demolishes). Not counted. |

### 1.3 What 100k needs, and in what ratio [E]

Demand mechanics (decompiled `ZoneManager.Calculate*Demand`) and what today's R 62 / C 50 / W 29 imply
(values read live; the smoothing moves them ±2 per step toward the target):
- **Workplace demand** = clamp(occupied homes, 0, 50) + clamp(200·(unemployed − empty jobs)/jobs, ±50).
  W 29 means empty jobs exceed the unemployed by about **10% of all jobs**. Jobs track workers.
- **Residential demand** = 50 (big city) + 200·(empty jobs − unemployed)/jobs + 200·(homeless − empty homes)/homes.
  62 = 50 + 21 − 9, i.e. about **4.5% of homes are empty**.
- **Commercial demand** balances 1.6 × commercial jobs against 0.2 × homes, i.e. about 1 shop job per 8 homes,
  plus a visitor (tourist) term. Today there are ~5,600 commercial jobs against 1,100 needed for residents,
  so commercial here is **tourist-driven** (the Plaza of Transference, B22). Resident-driven commercial for
  14.6k new homes is only ~1.8k jobs.

Targets for the new zoning:

| | New homes | New jobs | Zoned cells (planning yields) |
|---|---|---|---|
| Residential | 14.6k (≈12.2k in new districts after infill and level-ups) | — | RH ~17k, RL ~5k |
| Commercial | — | ~3.5k (1.8k resident + tourism growth) | ~5k (CH/CL) |
| Office | — | ~6.5k | ~11k |
| Industrial | — | ~3.5k | ~4k |

**Ratio for new zoning, by zoned cells: R 100 : C 22 : I 18 : O 50 (RH:RL about 3:1).** Offices get the
largest job share because they add no pollution and little truck traffic, and the city already has two
jammed truck corridors (south harbor, Richardson).

Planning yields (`model.py`): RH 0.70 homes and RL 0.18 homes per zoned cell (72% and 85% of today's
mature values, because new districts start at L1); jobs at 80% of today's per-cell values. Zoned share of
gross land 50% (80 m grid = 64% before arterials, services, parks and stations), 45% on hilly land.

---

## 2. Land survey

### 2.1 Method [M]
- Terrain: `build-network` dry run, `Basic Road`, elevation 0, on an 80 m grid over every owned tile:
  2,677 calls, 5,353 heights (`heights80.json`, 2.6 min). `startY` includes the water surface.
- Occupancy on a 40 m grid: every road, track, quay and path segment buffered by its half width (tunnels
  and virtual line paths excluded), and every growable and facility footprint (boulders, poles and pipe
  junctions ignored). Classes: water (y < 62), occupied, low ground (y < 100, below the plateau), steep
  (> 12% over ±40 m), free flat.
- Tiles: CS1 tiles are 1,920 m. Water pipes, power lines and growables exist in exactly nine tiles —
  (2,3), (3,1), (3,2), (3,3), (3,4), (4,1), (4,2), (4,3), (4,4) in 1,920 m tile indices from (−4800,−4800) —
  and the only networks outside them are the pre-built highways. That is the vanilla maximum of 9.
  The bridge cannot read owned tiles (gap). Owned area: x 960…4800, z −2880…4800, plus x −960…960,
  z 960…2880. Zone cells outside owned tiles are invalid (`ZoneBlock.CalculateBlock` → `QuadOutOfArea`),
  so growth outside is impossible even though the bridge could lay roads there.
- External connections [M]: highway at the north edge (1465/1503, 8649) and the south-east edge
  (8645/8649, −5873/−5915); train connections at (8644, 7463) and (−5319, 8644).

Owned land by class [M]: free flat 11.1 km², steep 3.1, low ground 3.8, occupied 7.7, water 7.5.

### 2.2 Candidate growth districts

Areas are free flat cells in connected blocks, from `comps.py`; "usable" keeps only cells that fit a
120 m block. Heights and slopes from the 80 m survey.

| Id | Name | Bounding box (x0,z0 – x1,z1) | Usable km² | y min/mean/max | Slope mean / p90 | Nearest station | Verdict |
|---|---|---|---|---|---|---|---|
| **NW1** | Robert North | 960,2760 – 1680,4120 | 0.76 | 121/133/151 | 4.3% / 6.8% | Metro 1005 (1423,2683), 750 m from centre | **Core, phase 1.** Clean. Highway on the west, rail mainline on the north (z ≈ 4080). Access off Robert Blvd. |
| **W1** | Westfield | −960,960 – 560,2880 | 2.31 | 129/167/191 | 5.1% / 9.5% | Overground metro 32802 (848,1919), 1,050 m, across the highway | **Core, phases 2–3.** Largest clean block, no track inside. West of the highway; the existing Thornton interchange's west end (516,1994) is the gateway. East edge is 400 m from the runway (noise): offices there. |
| **C1** | Sophie South | 960,−720 – 2000,800 (usable part x 900–1700, z 200–840) | 0.68 | 122/149/166 | 5.4% / 9.8% | SW 17218 (2089,−289), 920 m | **Core, phase 4.** The SW rail branch runs at x ≈ 2200 on its east side. |
| **N1** | North Rail | 1680,4120 – 3640,4800 (clean part x 1700–2700) | 0.80 | 100/127/150 | 4.6% / 7.6% | Train N 20505 (2728,4192), 390 m; metro SR (3225,4310) | **Work district, phase 3.** Tracks only in its east end near the Cargo Center (3382,4471). Gives Metro Red jobs at its north terminus. |
| E1 | East Shore | 3160,960 – 4160,2160 | 0.69 | 100/108/127 | 4.6% / 9.3% | 31333 (2994,1177), 800 m | **Reserve.** One N–S track (line 180's route) splits it at x ≈ 3150–3350. Low (y ≈ 100–110). |
| SW1 | South Bluffs | 960,−2880 – 2360,−640 | 1.61 | 100/129/217 | 6.3% / 11.0% | SW 17218, 1,550 m | **Reserve.** Across the highway from the city; rises 87 m over the 854 m from (1650,−1450) to (1350,−2250), about 10%, too steep for rail (8% cap) between them. Needs grade-separated road crossings and a rail extension. |
| — | smaller pockets | (1640,3160–2360,4080), (2320,−200–2800,960), (2560,1360–3200,2200), (3120,3240–3520,4120) | 0.26 / 0.31 / 0.30 / 0.22 | 106–166 | 3–7% | 350–840 m | Services, parks, depots. |
| HS | Harbor South infill | x 3400–4600, z −2500…−1100 | ~4.2k zone cells, 0.27 km² (the 193 empty blocks, mostly here) | 60–100 | — | SE 36774, harbors | **Jobs infill, phase 1.** Existing roads, next to Cargo Harbor 47308 and the highway. |
| — | Unsuitable | low ground x 2400–4700 along the bay; everything outside the 9 tiles; steep bands in W1's south-west and SW1's south | — | <100 or >12% | — | — | Not for zoning. Shore sites only for water intakes and treatment. |

District capacity from `model.py` [E]:

| District | Zoned cells | Mix (by cells) | Homes | Jobs | People at 4.0/home |
|---|---|---|---|---|---|
| NW1 | 5.9k | RH 60, RL 10, CH 10, O 20 | 2.6k | 1.2k | 10.4k |
| W1 | 18.0k | RH 60, RL 15, CH 10, O 15 | 8.1k | 3.0k | 32.4k |
| C1 | 5.3k | RH 50, RL 15, CL 10, O 25 | 2.0k | 1.1k | 8.0k |
| N1 | 6.3k | RH 35, CL 10, O 30, I 25 | 1.5k | 2.9k | 6.1k |
| HS infill | 4.2k | O 50, I 50 | — | 3.2k | — |
| **Core subtotal** | 39.7k | | **14.2k** | **11.4k** | **57k** |
| E1 (reserve) | 5.4k | RH 55, RL 15, CL 10, O 20 | 2.2k | 0.9k | 8.9k |
| SW1 (reserve) | 11.3k | RH 45, RL 25, CL 10, O 10, I 10 | 4.1k | 2.3k | 16.3k |

With infill (+1.3k homes) and half the level-ups (+0.57k), the core reaches about 105k at 4.0 residents per new home; the reserves cover a
shortfall if new households stay smaller or RH levels slowly. Service jobs (schools, hospitals, stations)
close the remaining job gap.

---

## 3. Services for 100k

The bridge exposes counts and problems, not capacity or consumption (gap). "Today" is the live count
[M]. "Need" scales today's count by homes (12.2k new vs 8,737 now, ×1.39 added) [E]; placement follows
coverage. Capacities in brackets are vanilla wiki values, **not read from this install** [E]. Every prefab
name below is in `/prefabs/buildings` [M].

| Service | Today [M] | Live problems [M] | Add for 100k [E] | Prefab | Placement zones |
|---|---|---|---|---|---|
| Power | Nuclear Power Plant 1 (1,600 MW), Wind Turbine 16, Advanced Wind Turbine 4, Solar Power Plant 1 | 5 buildings with Electricity (4 in a cluster at x 2645–2685, z 723–788), 2 ElectricityNotConnected nodes (3000,2180) and (3868,−856): local gaps, not supply | 1 Nuclear Power Plant in phase 3, earlier if the Electricity info view shows use > 70% of production | `Nuclear Power Plant` | N1 east industrial strip (x 2900–3300, z 4500–4760), away from homes. Power lines along the new arterials. |
| Water supply | Water Intake 6, Water Tower 4 | 33 buildings with Water: **26 in one cluster at x 3900–4260, z 70–230 (16 already abandoned)**, 3 at (3301–3345, 4092–4129) | +9 intake/tower equivalents: 5 `Water Intake` + 4 `Water Tower` | `Water Intake`, `Water Tower` | Intakes on the E1 shore (x ≈ 4000–4150, z 1300–2000), upstream of sewage. W1 has no water body: towers on clean high ground inside W1 (y ≈ 180) plus a pipe trunk under the highway from the Thornton area. |
| Sewage | Water Treatment Plant 3, Water Outlet 1 | DirtyWater 2 at (2630,2374), (2905,2888): source still unknown | +5 `Water Treatment Plant` | `Water Treatment Plant` | South bay low ground (x 2300–2500, z −1400…−2000), far from the intakes. The bay is still water (knowledge.md): never put an outlet near an intake. |
| Garbage | Combustion Plant 8, Landfill Site 3 | LandfillFull 1 (8062 at 1458,1806); Garbage 5 | +12 `Combustion Plant`; empty 8062 (player UI: Empty button) | `Combustion Plant` | HS infill, N1 east, C1 south edge; service road access, not arterials. |
| Deathcare | Crematory 11, Cemetery 1 | **Death 14** today (already short) | +16 `Crematory`, +1 `Cemetery` (3 crematories in phase 1 for today's backlog) | `Crematory`, `Cemetery` | One per district on a collector; the smaller pockets. |
| Health | Hospital 2, Medical Clinic 3, Child Health Center 01 14, Eldercare 01 1 | none | +3 `Hospital`, +5 `Medical Clinic`, +20 `Child Health Center 01`, +2 `Eldercare 01` | as listed | Hospital in W1 and NW1/C1; clinics per district. |
| Education | Elementary School 13, High School 8, University 7, Library 01 16 | NoEducatedWorkers 3 | +19 `Elementary School`, +12 `High School`, +1 `University` (7 is already many) | as listed | Elementary per ~650 homes inside each district; high schools on collectors; one university in W1. Education drives level-ups (§1.2). |
| Fire | Fire House 7, Fire Station 3 | none | +10 `Fire House`, +5 `Fire Station` | as listed | One Fire House per ~0.5 km² of new district; Fire Station in W1, NW1, C1. |
| Police | Police Station 8, Police Headquarters 3 | Crime 1 | +12 `Police Station`, +5 `Police Headquarters` | as listed | Same pattern as fire. |
| Parks | ~25 park buildings (ParkAI) | Noise 9, Pollution 4 | +35 | `Regular Park`, `Expensive Park`, `Regular Playground`, `Regular Plaza` | Buffers between W1 offices and housing, along the highway edge, and on the smaller pockets. |

Acceptance for services is problem-based (§5): the counts above are budgets, not targets.

---

## 4. Master interconnectivity plan

### 4.1 Road hierarchy

Rules (traffic-guide.md): highway → arterial (6-lane) → collector (4-lane) → local (2-lane). New junctions
on arterials at least 160 m apart (never under 80 m). Locals attach to collectors, not to arterials.
Roundabouts where a new district meets the highway system. The bridge has no roundabout prefab: build one as
a ring of `Oneway Road` pieces (R 30–40 m, 8 pieces), dry run first.

**Existing jams to relieve first (measured today, `/state/traffic`, flow 59%):**

| Where | Measured | Fix | Needs |
|---|---|---|---|
| Richardson Ave at Robert Blvd 28504 (1610,2690) and Graham Blvd 14343, 49 m apart | 34930 density 100, 18873 100, 7404 97, 14977 97, 427 84; Robert 7372 85 | Realign Graham into 28504 (one 4-way), or a roundabout at 28504 | **Player** (fronting buildings 168, 21250; lines 94 and 142) |
| Spine node 23540 (Merge A/B, Richardson, Spine 1) | Merge B 18762 100 (already 6-lane since B23), Merge A 22179 95 (still `Medium Road`) | Merge A → `Large Road` | **Player** (17881 fronts it; line 246 runs on it) |
| Laurel / Evans at station S (node 11365) | 1653 100, 3812 100, 8858 100 (bus lanes since B26) | A parallel collector east of Laurel is blocked by the track 40 m away; relieve by giving C1/SW1 traffic its own highway access instead of Laurel | Phase 4 |
| At-grade junctions **on the highway** | West: Robert (637,2782), Empire (588,2129), Finch (631,2120), Kent (634,1817). South: Parker/Kathleen Murray (3446,−1864), Parker (3467,−1832), Laurel (3744,−2017), Finch Blvd (4068,−2240), Harvey (3872,−2146 and 3891,−2118), Wilson (4054,−2278) | Consolidate each cluster into one interchange (ramps + roundabout); remove the others | **Player** (removes road links); phase 2 (west), phase 3 (south) |

**New districts:**

- **NW1 Robert North (phase 1).** Collector "Sophie North": continue Sophie Blvd north from Robert Blvd
  junction 13951 (1250,2724), making it a 4-way (Robert and Sophie both straight through; 325 m to 19306,
  360 m to Richardson). `Medium Road` in 80 m pieces to (1250,3940). Local grid west of it:
  `build-grid` `Basic Road`, origin (1000,2900), cols 5, rows 6, spacing 80 (dry run returned 42 nodes,
  71 segments, 30 blocks [M]); a second grid rows z 3380–3860 in phase 2. East-west collector at
  z ≈ 3380 (`Medium Road`) to the steep band at x ≈ 1480. Second exit later: north over the mainline
  (`Medium Road Elevated`) to N1 in phase 3, so NW1 does not load Robert/Richardson alone.
- **W1 Westfield (phases 2–3).** Gateway roundabout at the Thornton Street west end (516,1994), which the
  existing ramps (5878, 13980, 24547) already feed. Arterial "Westfield Parkway", `Large Road with Tree
  Median`, west from the roundabout along z ≈ 2000 to x −900 (1.4 km), junctions at x 360, 120, −200,
  −520, −840 (240–320 m apart). Collectors (`Medium Road`) north–south at x 120 and −520, z 1200–2800.
  Local grids between collectors, 80 m. Second access: a collector north to Robert Blvd's highway end
  (637,2782) **only after** that at-grade junction is replaced (decision above). Offices and commercial
  on the east edge (x 200–560) against the highway and runway noise; RH around the three metro stations;
  RL in the south-west.
- **C1 Sophie South (phase 4).** Collector from Murray Blvd 19306 south end (919,2665) is too far;
  instead link to the existing grid at z ≈ 200 (C1 zoom) and to the SW branch station site (2200,500).
  Local grid x 900–1700, z 200–840.
- **N1 North Rail (phase 3).** Collector from station N's access road 34413 west along z ≈ 4350 to
  x 1750; offices/industry east of x 2300 (near the Cargo Center), mixed west of it.
- **Reserves:** E1 collector from 31333's road east along z ≈ 1500; SW1 needs two road crossings over the
  highway (`Medium Road Elevated`, clearance ≥ 12 m) at about (1300,−700) and (2300,−1250).

### 4.2 Rail (stations within 800 m of every new district)

Physical track facts [M]: all passenger track is one connected network (418 segments, `nets.json`),
including the northern mainline along z ≈ 4080–4160 and the N–S track through E1. No track crosses W1.
Rules: every node turn ≤ 40° (Proven Rule), platform ends straight, grades ≤ 8% on track built by the
bridge, survey then solve (lessons 2026-09-26).

| New station | Site | Serves | Line | Build |
|---|---|---|---|---|
| NW-R | on the mainline at about (1250,4080), y ≈ 127 | NW1 north half (south half is within 800 m of metro 1005) | extend line 180 from N 20505 west (2 stops added; 180 has 8, cap 20) | Station hosted on the mainline (B11 pattern: bulldoze the host segments, place `Train Station`, link both platform ends). Check the bends at both ends first. |
| E1-R (reserve) | on line 180's N–S track at about (3230,1750), y ≈ 108 | E1 | line 180 | Same pattern. |
| C1-R (phase 4) | on the SW branch at about (2200,500), y ≈ 162 | C1 east half; C1 west half by feeder | line 90 | Same pattern on elevated branch track: check the branch is ground or elevated at that point before choosing the prefab. |
| SWB (reserve) | (1650,−1450), y ≈ 100 | SW1 north | extend line 90 from SW 17218's free SW end (node 32431) south across the highway, elevated | New elevated track; a second station at (1350,−2250) is **not** feasible by rail (87 m rise in 854 m). |

N1 is already within 800 m of N 20505, 47366 and metro SR. W1 gets metro, not rail (no track nearby).

### 4.3 Metro (only where it links homes to jobs that bus or rail do not serve)

Today [M]: Red 7/227 (SR→NR, 10 stations), Blue 135/207 (NO→AP→1005→TM→C2→FN), Airport Express 108/109
(NO…FN, S1…S6, 12 stations). Green deleted (B24).

1. **Blue West extension (phases 2–3) — the one new metro build.** W1 homes to the west job cluster
   (the largest zoned-job area: ~3,400 jobs in x 500–1500, z 1000–2500 [M, `CalculateWorkplaceCount` on a 500 m grid]), the airport (AP) and Core2
   (C2), with no bus or rail in W1 today. Tunnel from NO 26937's free S platform end (node 19800,
   y ≈ 176) south, then west under the highway (x ≈ 640, y ≈ 173) to three `Metro Entrance` stations:

   | Station | Site | y [M] | Spacing |
   |---|---|---|---|
   | W1-E | (320,2000) | 182 | ~1.1 km from NO |
   | W1-C | (−150,2200) | 174 | ~510 m |
   | W1-W | (−550,1600) | 180 | ~720 m |

   Lines: edit 135 (Eastbound) to start W1-W → W1-C → W1-E → NO …, and 207 (Westbound) to end … NO → W1-E →
   W1-C → W1-W. Each station once per line (Proven Rule). Two-ended Dubins planning with 30 m straight at
   every platform end, R ≥ 50 m, bends ≤ 40° (`tmp/tampa/metro/b16plan.py`), clearance ≥ 14 m from the
   Blue/Express tunnels that leave NO's north end. Phase 2 builds W1-E only (1.1 km), phase 3 the other two.
2. **Red: give it jobs instead of track.** The diagnosis found Red's four northern stations have no jobs
   within 500 m. N1's offices and industry sit at Red's SR terminus, so Red's southern homes (CP/CO/NR
   area, ~1,350 homes [M]) get a job destination without new track. No Red extension.
3. **Airport Express:** unchanged. **No metro for NW1, C1, E1, SW1**: rail and 1005 cover them, and a line
   beside existing rail or bus gets nothing (lesson B18, metro diagnosis).

Metro after the plan: 3 line pairs, 22 + 3 = 25 stations. Station spacing 500–1,100 m.

### 4.4 Bus

One feeder per new district, each ending at a rail or metro station, 5–10 stops, stops on both sides,
starting at the trunk station rather than running beside it (lesson B18):

| Line | District | Ends at | Stops (est.) | New or extension |
|---|---|---|---|---|
| W1 North Feeder | W1 north of the parkway | W1-C / W1-E | 8–10 | **new** (phase 2) |
| W1 South Feeder | W1 south | W1-W | 8–10 | **new** (phase 3) |
| NW1 Feeder | NW1 | 1005 and NW-R | 8 | **new** (phase 1) |
| C1 Feeder | C1 west | C1-R or SW 17218 | 7 | **new** (phase 4) |
| E1 Feeder (reserve) | E1 east | E1-R / 31333 | 7 | **new** |
| North Rail Feeder 245 + N1 | N1 west | N 20505 | 9 → 13 | extension |
| SW Rail Feeder 11 + SW1 (reserve) | SW1 north | SW 17218 / SWB | 8 → 14 | extension |

**New lines: 4 core + 1 reserve = up to 5.** The player's cap of 6 new bus lines is already used, so
**every one of these needs the player's permission.** Measured today: 11 bus lines exist (the brief says 13;
`/state/transit` lists 11 bus, 6 metro, 3 train = 20 of cityLineCount 21).

### 4.5 Airport and harbors: at most one transfer

| From | To airport (AP) | To harbors 23322 / 42184 (via Express S6) |
|---|---|---|
| W1 | Blue, 0 transfers | Blue → Express at NO/AP/1005/TM/C2/FN, 1 |
| NW1 | 1005 on Express and Blue, 0 (NW-R users: line 180 → 31333 → S1, 1) | 1005 → Express, 0 |
| C1 | line 90 → Core2 → C2 Express, 1 | same, 1 |
| N1 | line 180 → 31333, walk ~106 m to S1 Express, 1; or Red → NR, walk 80 m to S1, 1 | same, 1 |
| E1 | line 180 → 31333 → S1, 1 | 1 |
| SW1 | line 90 → Core2 → C2, 1 | 1 |

### 4.6 Targets

- **Stop coverage:** every growable within 400 m of a stop. Today 3,068 / 3,122 = 98.3% [M]
  (`tmp/tampa/master/cov.py`: straight-line distance to any line's stop). Target: ≥ 98% city-wide and
  100% in each new district.
- **Trunk coverage:** growables within 800 m of a rail or metro stop: 98.3% today [M]; ≥ 95% at 100k.
- **Modal share:** weekly transit riders ÷ population (the only measure the game exposes; legs are
  counted, so compare only within this city): 8.6% today; **12% at 100k (12,000/week), stretch 15%** [E].
  Free Public Transport is already on (B19), so the lever is coverage and trunk speed.
- **Line health:** every line complete, no LineNotConnected, vehicles = target, and no line under
  75 riders/week after 4 periods (player rule; judge by `lastPeriod`, lesson B18).

---

## 5. Phasing

Rules for every phase: dry run every command; save after each batch with the verified-mtime rule; one
working save (the one the player has loaded); zone residential in chunks of ≤ 50 cells, two game weeks
apart (death waves); services before zoning in each district.

Common acceptance calls (orchestrator):
`/state/summary` (citizens.count, demand), `/state/demand`, `/state/problems?limit=2000`
(`countsByProblem`), `/state/zones`, `/state/growables?limit=20000` (then `cap2.py` for homes),
`/state/transit?includeStops=true` (then `cov.py`), `/state/traffic?minDensity=80`,
`/state/road-anomalies`, `/state/external-connections`, `/state/zone-anomalies`.
Population itself must be read from the game UI by the player until the bridge exposes it.

### Phase 1 — 42k → 55k

| | Work |
|---|---|
| Services first | 3 `Crematory` (Death 14 today); fix the east water outage (26 buildings at x 3900–4260, z 70–230) and the Electricity cluster (x 2645–2685, z 723–788); player empties Landfill 8062. |
| Roads | Sophie North collector from 13951 to (1250,3380) as 80 m `Medium Road` pieces; `build-grid` origin (1000,2900), cols 5, rows 6, spacing 80, `Basic Road`. |
| Zoning | NW1 first grid: RH on the blocks within 400 m of 1005 and along Sophie North, CH 10% at the Robert Blvd end, O 20% on the west edge by the highway, RL on the outer row. HS infill: Office and Industrial on the ~100 empty blocks at x 3400–4600, z −2500…−1100 (jobs to keep W demand from going negative). |
| Services | NW1: 1 `Elementary School`, 1 `Fire House`, 1 `Police Station`, 1 `Medical Clinic`, 2 parks, water pipes and power along the grid. |
| Transit | NW1 Feeder (needs permission); NW-R station on the mainline + line 180 extension. |
| Acceptance | population ≥ 55k (UI); homes ≥ 11.5k (`cap2.py`); Water, Electricity, Death problems each ≤ 5; LandfillFull 0; R demand > 0 and W demand ≥ 0; road anomalies 0 and disconnectedLocalRoadComponents 0; coverage ≥ 98%; all lines healthy. |
| Agents | Opus build agent (cs1-city skill) for roads, zoning and services; Opus transit agent (cs1-transit skill) for NW-R and the feeder; Sonnet only for the service-placement list once sites are decided. Fable verifies. |
| Risks | NW1 traffic loads Robert Blvd into the Richardson jam; death wave 50–60 game years after a mass move-in (zone in chunks); the NW-R host-track split can leave an End-signature segment (lesson B16). |
| Player decisions | New bus line (over the cap); Richardson/Graham realignment; Merge A upgrade; NW-R track split. |

### Phase 2 — 55k → 70k

| | Work |
|---|---|
| Roads | Thornton west-end roundabout; Westfield Parkway (east half, x 516 → −200); collector x 120; local grids in W1 east. West highway at-grade cluster (Empire, Finch, Kent) consolidated into the Thornton interchange (decision). NW1 second grid (z 3380–3860). |
| Zoning | W1 east half (~1.1 km²): offices on x 200–560, RH around W1-E and W1-C, commercial on the parkway. |
| Services | `Nuclear Power Plant` if the power view shows > 70% use; 2 `Water Tower` in W1 + trunk under the highway; `Hospital`, `Fire Station`, `Police Headquarters`, 3 `Elementary School`, 1 `High School`, 2 `Crematory`, 2 `Combustion Plant` in or near W1. |
| Transit | Blue West tunnel NO S end → W1-E (1.1 km) and edit 135/207; W1 North Feeder (permission). |
| Acceptance | population ≥ 70k; homes ≥ 14.7k; Blue 135/207 no problems and vehicles = target within 1 game day; W1 growables 100% within 400 m of a stop; highway flow not below today's 59%. |
| Agents | Opus metro agent (b16 planner, bends.py) for Blue West; Opus build agent for W1 roads/zoning. |
| Risks | Blue West tunnel must clear the Blue/Express legs at NO's north end and the highway foundations are irrelevant underground; runway noise on W1's east edge; a single W1 gateway until the second access exists. |
| Player decisions | West at-grade junction consolidation; bus line; budget for the metro build. |

### Phase 3 — 70k → 85k

| | Work |
|---|---|
| Roads | Westfield Parkway west half; collector x −520; W1 west grids; NW1 → N1 elevated collector over the mainline; N1 collector along z ≈ 4350; south highway at-grade cluster consolidated (decision). |
| Zoning | W1 west half (RH at W1-W, RL in the south-west); N1 (offices/industry east, mixed west). |
| Services | 2nd `Nuclear Power Plant` if not done; 3 `Water Intake` on the E1 shore; 3 `Water Treatment Plant` on the south bay; `University` in W1; the district packages from §3. |
| Transit | Blue West W1-C and W1-W; W1 South Feeder (permission); line 245 extended into N1. |
| Acceptance | population ≥ 85k; homes ≥ 17.8k; Red 7/227 riders up (compare 4 periods before/after N1 opens against its no-change range); modal share ≥ 10%. |
| Agents | as phase 2. |
| Risks | Vehicle and citizen-instance limits start to matter (not readable, see §6); office jobs level only with education. |
| Player decisions | South highway junction consolidation; bus line. |

### Phase 4 — 85k → 100k

| | Work |
|---|---|
| Roads/zoning | C1 (grid x 900–1700, z 200–840); level-up programme (education, parks, land value near stations). If short: E1, then SW1. Optional: upzone RL → RH within 400 m of metro stations (player). |
| Services | Remaining §3 counts, placed where problems appear. |
| Transit | C1-R station on the SW branch, C1 Feeder; E1-R and E1 Feeder if E1 opens; SWB and line 90 extension only if SW1 opens. |
| Acceptance | population ≥ 100k (UI); homes ≥ 20.9k; problems total ≤ today's 81 with no FatalProblem cluster; modal share ≥ 12%; coverage ≥ 98%; all lines healthy; trafficFlowPercent ≥ 55. |
| Player decisions | Upzoning (demolition); SW1 highway crossings and rail; any further bus lines. |

---

## 6. Honest constraints

1. **Land is fixed at 9 tiles** [M inferred]. The vanilla game caps owned tiles at 9 (`GameAreaManager.MaxAreaCount`
   defaults to 9) and no tile mod is installed (only the bridge mod). The bridge can neither read nor unlock
   tiles. 100k fits only with mostly high-density zoning; at today's low-density yield (0.21 homes per zoned
   cell) the new homes would need about 7 km² gross.
2. **Households are capped at 5 citizens** [M code]. At 4.78 per home now, every new resident needs a new
   home. If new households average 3.5, the city needs 16.6k new homes and the reserves (E1, SW1) are used.
3. **Vehicle cap 16,384, citizen instances 65,536** (knowledge.md) [E risk]. The bridge does not expose
   live counts (gap), so the risk at 100k cannot be measured. Mitigation is the transit share target and
   offices over industry.
4. **Budget** [unknown]. No money read; `place-building` does not charge construction cost or check unlocks
   (TODOS B22). Every build in this plan would be free through the bridge unless the player decides otherwise.
5. **Transit line cap 256**: not a constraint (21 lines).
6. **Terrain**: SW1 rises 87 m in 854 m (rail infeasible there); W1 and NW1 are flat enough (p90 slope ≤ 9.5%).

Bridge gaps that block or weaken steps:

| Gap | Blocks |
|---|---|
| No population read (`m_populationData`) | Every phase acceptance on population (player reads the UI) |
| No owned-tile read or unlock | Confirming the land limit |
| No line-path read (`/state/transit`) | Knowing which track and roads a line uses (E1-R, C1-R siting; road fixes) |
| No zone-safe road upgrade | Richardson, Merge A, any arterial upgrade with fronting buildings |
| No roundabout primitive | Build as a ring of `Oneway Road` pieces |
| No level crossings; `build-network` cannot join shared road/rail nodes | Crossings must be elevated |
| No service capacity/consumption read (power, water, garbage, deathcare, education seats) | Service sizing stays estimated |
| No vehicle / citizen-instance counts | Limit risk (§6.3) |
| No spatial zone read (`/state/zones` gives totals only) | Infill siting (used a `set-zone` dry-run sweep instead) |
| `place-building` free and unlock-blind | Budget realism |
| Metro dry run cannot check out-and-back platforms | Keep one line per direction (Proven Rule) |

## 7. Findings from this survey that belong in TODOS.md (not filed here: this task could only write this file)

1. **Water outage, east:** 26 buildings with Water problems at x 3900–4260, z 70–230, 16 of them already
   Abandoned (FatalProblem); 3 more at (3301–3345, 4092–4129). `/state/problems` 2038-11-21.
2. **Electricity:** 4 buildings with Electricity MajorProblem at x 2645–2685, z 723–788; ElectricityNotConnected
   nodes 20301 (3000,2180) and 20718 (3868,−856).
3. **Deathcare short:** 14 Death problems city-wide.
4. **Brief vs live:** 11 bus lines exist, not 13.
5. **Residents per home 4.78** against the 5 cap, if the 41.8k population is right; worth a UI check of
   "households" in the city info panel.

## 8. Independent check of the capacity figures

One blind consult (Cursor `gpt-5.6-sol-high`, refute-only, my numbers withheld; prompt and answer in
`tmp/tampa/master/consult_capacity*.{md,txt}`). grok was not run: this is a plan, and nothing here is
irreversible or about money. Derived independently from the same raw inputs, the consult got:
- homes 8,737 (3,780.1 high + 4,956.9 low); 4.78 residents per home; 95.7% of the 5-per-home ceiling;
- 12,165 more homes at the same ratio, or 14,550 at 4.0 per new home;
- empty workplaces exceed the unemployed by 10.5–11% of jobs, and the housing term is −9.
All of these match this plan. What it found that the plan did not say:
- The demand term gives (empty homes − homeless) / homes = 4.5–5%, not the empty-home rate. It only
  equals the empty-home rate if nobody is homeless.
- If about 4.5% of homes are empty, a full 5 per occupied home houses about 41.7k, which is roughly 80 below
  the reported 41.8k. That is not a contradiction (home counts are expected values and the population is
  approximate), but it means **the city is effectively full**. Read population and households from the
  city info panel before phase 1 is accepted.
- The demand inversion assumes `m_DemandWrapper` is inert (no mod changes demand). Only the bridge is
  installed, so that holds here.
