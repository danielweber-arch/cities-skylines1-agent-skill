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

## 3b. Education pillar: a very highly educated city

**What can be measured** [M]:
- The bridge does not expose the education mix (uneducated / educated / well educated / highly educated),
  the age mix, or the education budget. `/state/transit` shows budgets for transit only. These are bridge
  gaps: `DistrictManager` `m_educated0..3Data` and `m_child/teen/young/adult/seniorData`, and the
  Education `set-service-budget` value.
- The EducationBoost city policy is **on** (`/state/policies`). RecreationBoost, FreeTransport and
  BigBusiness are also on, and so are the low-density residential and low-density commercial tax cuts.
- School coverage of the 1,997 residential growables (straight line ≤ 500 m, the code's default
  `SchoolAI.m_educationRadius` = 500; the prefab values were not read):

| Prefab (verified in `/prefabs/buildings`) | Built | Homes within 500 m |
|---|---|---|
| `Elementary School` | 13 | 81.6% |
| `High School` | 8 | 66.9% |
| `University` | 7 | 59.9% |
| `Library 01` | 16 | 85.5% |

`Research Library 01` 1 and `Hadron Collider` 0 are also loaded.

**How citizens get educated** [M, decompiled `ResidentAI`]:
- Children (age < 15) seek elementary school, teens (15–44) high school, and young adults and adults
  (45–179) without a degree seek university (`Student1/2/3`).
- Separately, every simulation step a resident standing in education coverage whose local value is above
  1,000 can gain the level for free. The chance is (local − 1,000)/9,000 for elementary and high school,
  so **coverage educates as well as seats do**.
- **EducationBoost:** an unemployed young adult or adult who still lacks university keeps seeking
  university on 3 of every 5 ages instead of dropping out of the search.
- Move-ins bring their education with them. Only the two adults carry it; children arrive uneducated.

**Estimated mix today** [E]: with 82% elementary and 67% high-school coverage, most adults are
"educated" or better. The gap to "highly educated" is university coverage: 60% of homes lie inside a
university radius, and the rest depend on a seat. Target at 100k: every home inside all three radii.

**Seats against demand by phase** [E]:
- Student shares assumed: elementary 10% of population, high school 12%, university 12%.
- Seat capacities are vanilla wiki values, **not read**: elementary 300, high school 1,000, university 4,500.
- This table supersedes the Education row of §3, which scaled building counts instead.

| Population | Elementary: need / seats | High: need / seats | University: need / seats | Build in the phase |
|---|---|---|---|---|
| 41.8k now | 4.2k / 3.9k (13) | 5.0k / 8.0k (8) | 5.0k / 31.5k (7) | — |
| 55k (P1) | 5.5k / 5.7k | 6.6k / 8.0k | 6.6k / 31.5k | +6 Elementary, +2 Library 01 (NW1, HS) |
| 70k (P2) | 7.0k / 7.2k | 8.4k / 9.0k | 8.4k / 31.5k | +5 Elementary, +1 High School (W1-E) |
| 85k (P3) | 8.5k / 8.7k | 10.2k / 11.0k | 10.2k / 36.0k | +5 Elementary, +2 High School, +1 University (W1-C) |
| 100k (P4) | 10.0k / 10.2k | 12.0k / 12.0k | 12.0k / 36.0k | +5 Elementary, +1 High School (C1-R) |

Universities stay far above seat demand. Coverage, not seats, is why W1 still gets one.
Libraries: one `Library 01` per new district, placed at its station.

**Put schools on the trunk so students ride** [M distances to the nearest rail/metro stop today]:
- Universities 24099 (73 m), 30255 (82 m), 47155 (145 m), 33107 (166 m), 41670 (215 m), 13163 (400 m)
  and 15458 (415 m) are already on the trunk.
- High schools 26576 (37 m), 9285 (194 m), 33842 (214 m), 39883 (220 m), 16162 (309 m) and 12055 (345 m)
  are too. 30051 (588 m) and 288 (639 m) are not.
- New rule: every new `High School` and `University` goes within 250 m of a rail or metro platform:
  - W1-E: High School.
  - W1-C: University and Library 01.
  - NW-R and 1005: NW1 High School near 1005, north side.
  - C1-R: High School.
- Elementary schools stay inside the housing (children walk), within 500 m of every family block.

**Budget:** keep Education at 100% or above. It cannot be read or set through the bridge today, so the
player confirms it in the budget panel at each phase gate.

## 3c. Youth pillar: a young city

**Mechanics** [M code]:
- Every move-in household is two adults aged 90–104 (the start of the "adult" band) plus children aged
  0–14 (`OutsideConnectionAI`).
- Citizens then age through child < 15, teen < 45, young < 90, adult < 180, then senior.
- So the city stays young only while **new homes keep opening**. When growth stops, the cohort ages
  together and dies together (death waves, knowledge.md).
- The age mix is not readable through the bridge (gap), so youth is steered by supply, as follows:
  1. **Continuous, staged housing.** Zone residential in ≤ 50-cell chunks every two game weeks through all
     four phases, never in one burst. This keeps a steady inflow of young families and staggers the deaths.
  2. **Family housing next to schools, parks and transit.** In every new district, each RH and RL block
     lies within 500 m of an Elementary School, 400 m of a park or playground, and 400 m of a stop.
     Offices and commercial take the highway and runway edges.
  3. **Level-ups keep families.** Education and parks raise land value. Once a building levels up it has
     more homes, and those fill with new movers.

**Per-district school coverage plan** [E]:

| District | Elementary School | High School | Parks / youth leisure |
|---|---|---|---|
| NW1 | 3 (one per ~2 grids) | 1 near 1005 | 4 |
| W1 | 9 across the three station areas | 2 (W1-E, W1-W) + University at W1-C | 10 |
| C1 | 3 | 1 at C1-R | 3 |
| N1 (mixed west part) | 2 | uses 12055 (345 m from rail) | 2 |
| E1 / SW1 (reserve) | 2 / 3 | 1 / 1 | 2 / 3 |

**Youth leisure prefabs loaded** [M in `/prefabs/buildings`]: `Regular Playground`, `Expensive Playground`,
`Basketball Court`, `bouncer_castle`, `MerryGoRound`, `dog-park-fence`, `Regular Park`, `Expensive Park`,
`Regular Plaza`, `JapaneseGarden`, and `Stadium` (already built: 6007).
- The bridge cannot read unlock state (TODOS B22), and `place-building` ignores unlocks. The player
  confirms each one is unlocked before it is placed.
- RecreationBoost is already on.

## 3d. Tourism pillar

**What no longer moves tourists** [M, B22/B25]:
- The tourism resource T = floor(100·S/(S+200)), where S is global attractiveness plus average land value.
  T is about 85–87 because the Plaza of Transference adds 1,000 to S.
- One unique building moves T by 0 or 1 point. Switching the Plaza off for 8 weeks changed airplane
  passengers by nothing measurable.

**What still moves them:**
1. **City size.** Tourist offers per connection scale with P = (100N + 20,000)/(N + 20,000), where N is
   homes + workplaces (B22).
   - Now: about 8.7k homes + 10.7k zoned jobs = 19.4k, before service jobs [M], so P ≈ 50.
   - At 100k: about 20.9k + 24k = 45k, so P ≈ 70 [E].
   - That is about **+40% tourist offers from growth alone**, the largest lever this plan pulls.
2. **Path success at the connections.** Offers scale with each connection's pathfinding success ratio.
   The Express, the rail links and the harbour links built in B19 serve this. Keep every arrival hub one
   transfer from every district (below).
3. **Land value.** Parks, education and transit raise the land-value half of S. Worth 0–2 points of T in
   total [E]; it matters for level-ups more than for tourists.
4. **Leisure / tourist commercial and hotels are not loaded** [M: `/prefabs/buildings` lists only
   CommercialLow/High, Office and Industrial sub-services]. They are not available as a lever.

**Tourist destinations on transit** [M, distance to the nearest rail/metro stop]:
- West CBD cluster, served by Blue/Express AP, TM, 1005 and bus 142:
  - Trash Mall 14054: 134 m.
  - Countdown Clock 21158: 176 m.
  - ExpoCenter 5581: 235 m.
  - Theater of Wonders 2891: 297 m.
  - Grand Mall 4851: 320 m.
  - Stadium 6007: 417 m.
- SE cluster, served by Express S3–S6 and rail S/SE:
  - SeaAndSky Scraper 7841: 48 m.
  - Statue of Shopping 11918: 91 m.
  - Botanical garden 41283: 174 m.
  - Transport Tower 19957: 244 m.
- Core cluster: Colossal Offices 30769 (103 m) and Cathedral of Plentitude 22750 (138 m).
- **Plaza of Transference 21038** is 530 m from the nearest trunk stop, but bus 203 stops 81 m away and
  97 Station Link 184 m away.

Rule for new destinations: any new park, plaza or unique goes within 300 m of a metro or rail stop,
preferably at the W1 stations (W1-C) and at N1/SR.

**Arrival hubs to tourist districts, at most one transfer:**

| From | West CBD | SE cluster | Plaza |
|---|---|---|---|
| Airport (AP) | Blue/Express, 0 | Express, 0 | Express → S1/S2, then bus 97/203, 1 |
| Harbours 23322/42184 (S6) | Express, 0 | Express, 0 | Express → S2, then bus 97, 1 |
| Plaza | bus 97 → rail 31333 / Express S1, 1 | bus 203, 0 | — |
| W1 (new) | Blue, 0 | Blue → Express, 1 | Blue → Express → bus, **2**: add a W1 feeder stop at W1-E and rely on the Express. Plaza stays at 2 unless line 97 is extended to S1 (line edit, player). |

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

### 4.7 10x transit strategy

Status: PLAN ONLY. Written 2026-09-27 (game 2039-06) from live GETs, `dryRun:true` POSTs and the
decompiled game code, while a phase-1 builder worked in the game. Nothing was built or changed.
Goal (player): "10x on transportation. No limits on anything at all." Baseline 3,435 riders/week in the
brief [G]; live read 3,563/week (bus 2,228, metro 729, train 476, ship 27, air 85, balloon 18;
residents 2,882, tourists 681) [M]. 10x is about **34,000/week**.
Scratch: `tmp/tampa/10x/` (`ceiling.py` feasibility model, `tripgen.py` trip-rate model, `demand.py`
home/job grid, `circ.py` circulator planner, `dry_*.json` dry runs, `consult_*` second opinion,
`dec/*.cs` decompiled sources; line numbers below refer to those files, produced with
`ilspycmd -t <Type> Assembly-CSharp.dll`).

#### 4.7.0 Verdict

| Question | Answer |
|---|---|
| Is 34k/week reachable at 100k residents? | **Not on the estimates** [E]. It needs 5.0–6.6x more boardings per resident trip than today (the blind second opinion derived 4.9x under its own assumptions); the vanilla design levers give ~1.4–2.5x, and the theoretical top is ~3.5–4.5x. Not proven impossible: the gap rests on unmeasured inputs (4.7.5). |
| At 150k? | Only in the most favourable vehicle-count case (needs M 3.6–4.0) **and** with a near-theoretical network (almost every trip on transit, ~2 legs each) [E]. Not a plannable target. |
| What is plannable? | **~11–13k/week at 100k (3–3.6x), stretch ~16k (4.5x)**; ~13–16k at 150k, stretch ~20k [E]. This matches the 12% (12k/week) target in §4.6. |
| Why the ceiling | (1) Each resident gets **one** trip decision per counted week. (2) Trip generation is **throttled by the active vehicle count** (DoRandomMove), so trips per resident fall as the city grows. (3) Car-less residents already have no choice but transit for any trip longer than a 1 km walk, so most of the easy share is already taken. [M code, E size] |
| Biggest honest lever | Riders are counted **per boarding**: a forced transfer counts a trip twice. A feeder/trunk network raises the count without moving more people. Say so when reporting. |
| Decisive unknown | Today's trip count and mode split. The bridge cannot read the vehicle count, the citizen-instance count or trips. Adding that read settles the verdict (see 4.7.5). |

#### 4.7.1 Mechanics that decide ridership (from the game code)

**Mode choice: who can drive.**
- Car ownership depends on age only: child 0%, teen 5%, young adult 15%, adult 20%, senior 10%
  (`ResidentAI.cs:3202-3213`). It is drawn with `new Randomizer(citizenID)`, so the same citizen always
  gets the same answer (`ResidentAI.cs:3158-3160`). Wealth, education and policies do not enter; only the forced-car flag below overrides it.
  Electric Cars only changes car type (`3260-3272`). [M]
- Bicycles: 10–40% by age (`3215-3241`) but only if a bicycle vehicle is loaded; the After Dark policies
  are not in `/state/policies`, so bicycles are probably absent [not established]. No taxi depot, so no
  taxis (`CitizenAI.cs:1054-1066` needs `m_finalTaxiCapacity`). [M]
- **Trips to or from a road outside connection are forced to car** (`BorrowCar`, `ResidentAI.cs:2653-2675`):
  100% unless an Intercity Bus Station (bus Level3) exists, then (80 − 0.4 × bus budget)%, i.e. 20% at budget 150. That building is not
  in `/prefabs/buildings` [M]. Train, plane and ship outside trips are not forced.
- **A car owner can still take transit.** The path request always carries Pedestrian | Vehicle |
  PublicTransport lanes (`CitizenAI.cs:1066-1110`) and the cheapest path wins, so park-and-walk,
  walk-and-ride and park-and-ride are all legal. Measured: 281 of 3,385 line riders (8.3%) are car
  owners [M]; they ride at roughly half the rate of non-owners [E, age mix not readable].
- Tourists: car 20% (90% only inside an airport area with Car Rentals, an Airports-DLC feature not present here), bike 20%,
  taxi 20% if taxis exist (`TouristAI.cs:1435-1457`). A tourist trip to or from a road connection is
  forced to car 80% of the time with an intercity bus station, 100% without (`TouristAI.cs:895-925`).

**Path cost (what a cim compares).** Cost per metre, relative units (`PathFind.cs:777-793, 913-1128,
1131-1250`; `TransportLineAI.cs:405-416, 472-492`):

| Leg | Cost per metre | Notes |
|---|---|---|
| Walking | 4.0 | speed 0.25. **Each walking leg is capped at 1,000 m** (`PathFind.cs:860, 1058, 1211`). Each walking leg also pays a fixed ~100 m of walking once it has more than one piece (`1205-1208`). |
| Metro or train platform walkway | 20–40 | Avoid-flag lanes: speed ×0.2, ×0.1 in the avoided direction (`777-793`). About 150–300 m of walking per station visit (metro diagnosis). |
| Car, Basic / Medium / Large / Highway | 1.25 / 1.0 / 0.83 / ~0.5 (highway lane speed 2.0 not read from the assets) | × a random factor 0.9 to (1.0 + density/100) per segment (`969`), so a jammed road costs up to ~2x. Cars entering a bus lane pay a constant equal to ~20 m of driving (`1106-1109`). |
| Bus (Free Transport on) on Basic / Medium / Large road | 0.94 / 0.75 / 0.63 | `m_averageLength × 0.75 × 100/m_speed`; m_speed from the road speed limits. |
| Metro or train (Free Transport on) | 0.29 | m_speed clamps at 255, so metro and train cost the same per metre. |

- **No waiting, frequency, crowding, ticket or transfer term** in route choice. A transfer costs only
  its walk (plus the ~100 m walking-leg constant). [M]
- **But waiting too long loses the rider**: after two full wait counters the cim is flagged
  `CannotUseTransport` and re-paths without transit (`HumanAI.cs:430-452`). A car-less cim with a trip
  over 1 km then has no path. So frequency matters only where queues build (jammed buses). [M code]
- Consequences: (a) a car-less resident's trip over ~1 km has only transit; (b) with stops right at both
  ends, a bus beats walking from about 0.4–0.65 km (walk 4d vs 4×(a+b) + ~800 + 0.9d, a+b = 200–400 m) [E];
  (c) a car owner picks transit mainly when roads are jammed or a metro/rail station sits at both ends
  (4 km trip: car ~5,500 vs metro ~5,300 units with 250 m walks each end) [E].

**What counts as a passenger.** The counter increments when a citizen **gets off** a line's vehicle
(`HumanAI.cs:962-976`, called from `BusAI.cs:601-625` and `PassengerTrainAI`), per line and per mode
type. A trip with one transfer counts **twice**. The "week" is 4,096 frames: lines roll their counters
once per 4,096 frames (`TransportLine.cs:1690, 2092-2097`; lines are stepped every 256 frames), which is
7.0 calendar days at 147.66 s per frame (`SimulationManager.m_timePerFrame`). [M]

**How many trips exist.** Each citizen's AI runs **once per 4,096 frames** (`CitizenManager.cs:1710-1730`,
256 citizens per frame × 4,096 = the 1,048,576 citizen buffer). So one decision per resident per
counted week. At home (`ResidentAI.cs:1695-1726`; shopping first at 1695, gate at 1700): leisure trip with p = 1/40, commute with p =
x²/0.5 where x is how far the day clock is from 8:00 (0.5 at 8:00, 0 at 20:00); at work the same around
16:00 (`1804-1830`); from a visit, home with p = 1/4 (`1958-1975`). Shopping trips come from a household
goods counter (−20 per week, a shopper below 200: `ResidentAI.cs:509-521`). A day/night cycle is 65,536
frames = 16 counted weeks, so every resident sees every hour. `tripgen.py` gives **0.17 trip starts per
week for a resident with a workplace or school, 0.046 without**, before shopping and before throttling [E]
(the blind second opinion derived 0.19 and 0.025 per step in the eligible state, the same terms without the
state dynamics).

**The throttle.** Every commute, leisure and return move above (not shopping departures) is gated by `DoRandomMove`
(`ResidentAI.cs:1570-1579`): p = 1 − max(active vehicles / 16,384, citizen instances / 65,536). Today's
road traffic implies about 3,000–3,460 vehicles on roads (sum of density × lane length / 14 m over all
1,324 road segments, the B20 rule) plus ~364 rail and metro cars (every trailer is a vehicle): **about
3.6k vehicles, p ≈ 0.78** [E] (second opinion, independently: 3,384 vehicles, p 0.79). This holds only
if citizen instances stay under 4 × vehicles (~14k); that is not measured. As population grows, vehicles grow and p falls, so **trips per resident
fall**. A car trip moved to transit frees a vehicle slot and raises p for everyone; a bus adds one slot
per 30 seats, a metro train 5 slots per 150, a train 8 per 240 (same 30 seats per slot).

**Capacity and fleet.** Bus 30, metro 5 cars × 30 = 150, train 8 cars × 30 = 240 (vehicle prefabs in
sharedassets11.assets: `veh2.py`) [M]. Vehicles per line = ceil(serviceBudget × lineBudget/100 × length
/ (d × 100)) (`TransportLine.cs:2166-2168`), with d fitted on the live lines: bus 600 m, metro ~2,930–3,120 m,
train ~4,110–4,530 m [E fit]. Hard caps: 16,384 vehicles in total (cars, trucks, services, through
traffic and every transit car), 65,536 citizen instances, 256 lines; depots have no vehicle cap
(`maxVehicleCount` 100,000) [M]. Line budget goes to 500% through the bridge.

**Policies.** Free Transport (×0.75 on transit cost) is already on. High Ticket Prices makes transit worse.
**HeavyTrafficBan** adds cost only for paths flagged heavy (`PathFind.cs:238-242`); citizen paths are never
heavy (`CitizenAI.cs:1110`), so it moves no one out of a car; it only keeps trucks off district streets.
No vanilla car-deterrent policy is loaded (Old Town, bike policies and pedestrian zones are DLC).

#### 4.7.2 Feasibility arithmetic

Riders(R) = B0 × vol × M + T0 × (R/R0) × √M, with B0 = 2,882 resident and T0 = 681 tourist boardings per week
[M] and R0 = 41.8k residents [G]. vol = (R/R0) × ((1−g)·p + g) / ((1−g)·p0 + g) is the resident trip volume
relative to today: a share g = 0.25 of trips (shopping departures) is not gated by DoRandomMove [E]. p comes
from the vehicle model v = a·R + b·R·((1−g)·p + g) (a: trucks, services, through traffic, transit; b: resident
car trips, which shrink when p falls), with today's v at 1.8k / 3.65k / 5.5k and 45% of it resident car trips,
plus one row per population where all of today's v is car trips (the second opinion's assumption) [E].
**M** is the design multiplier on boardings per resident trip (share of trips on transit × legs per transit
trip) relative to today. `ceiling.py` (output in `ceiling_out.txt`):

| Residents | Today v, car share | Active vehicles | p | Trip volume vs today | Riders M 1.0 | M 1.65 | M 2.5 | M needed for 34k |
|---|---|---|---|---|---|---|---|---|
| 55,000 | 1,800, 0.45 | 2,340 | 0.86 | x1.28 | 4,586 | 7,240 | 10,642 | 8.5 |
| 55,000 | 3,650, 0.45 | 4,680 | 0.71 | x1.24 | 4,473 | 7,054 | 10,360 | 8.8 |
| 55,000 | 5,500, 0.45 | 6,948 | 0.58 | x1.20 | 4,352 | 6,854 | 10,057 | 9.1 |
| 55,000 | 3,650, 1.0 | 4,562 | 0.72 | x1.25 | 4,498 | 7,094 | 10,422 | 8.7 |
| 70,000 | 1,800, 0.45 | 2,937 | 0.82 | x1.58 | 5,693 | 8,976 | 13,184 | 6.8 |
| 70,000 | 3,650, 0.45 | 5,789 | 0.65 | x1.48 | 5,399 | 8,492 | 12,450 | 7.3 |
| 70,000 | 5,500, 0.45 | 8,460 | 0.48 | x1.37 | 5,093 | 7,986 | 11,684 | 7.8 |
| 70,000 | 3,650, 1.0 | 5,493 | 0.66 | x1.50 | 5,478 | 8,622 | 12,647 | 7.1 |
| 85,000 | 1,800, 0.45 | 3,519 | 0.79 | x1.86 | 6,743 | 10,619 | 15,584 | 5.7 |
| 85,000 | 3,650, 0.45 | 6,837 | 0.58 | x1.68 | 6,219 | 9,755 | 14,274 | 6.3 |
| 85,000 | 5,500, 0.45 | 9,846 | 0.40 | x1.49 | 5,687 | 8,878 | 12,945 | 7.0 |
| 85,000 | 3,650, 1.0 | 6,329 | 0.61 | x1.73 | 6,382 | 10,025 | 14,683 | 6.1 |
| 100,000 | 1,800, 0.45 | 4,085 | 0.75 | x2.12 | 7,738 | 12,172 | 17,848 | 5.0 |
| 100,000 | 3,650, 0.45 | 7,829 | 0.52 | x1.84 | 6,940 | 10,856 | 15,854 | 5.7 |
| 100,000 | 5,500, 0.45 | 11,122 | 0.32 | x1.57 | 6,153 | 9,556 | 13,885 | 6.6 |
| 100,000 | 3,650, 1.0 | 7,084 | 0.57 | x1.94 | 7,223 | 11,322 | 16,560 | 5.4 |
| 150,000 | 1,800, 0.45 | 5,869 | 0.64 | x2.86 | 10,686 | 16,739 | 24,471 | 3.6 |
| 150,000 | 3,650, 0.45 | 10,786 | 0.34 | x2.18 | 8,730 | 13,511 | 19,579 | 4.6 |
| 150,000 | 5,500, 0.45 | 14,725 | 0.10 | x1.56 | 6,949 | 10,573 | 15,128 | 6.2 |
| 150,000 | 3,650, 1.0 | 9,144 | 0.44 | x2.51 | 9,663 | 15,051 | 21,913 | 4.0 |

What M can reach [E]:
- Legs: splitting cross-city buses at trunk stations and ending every feeder at a station: ×1.3–1.6.
- Share: short trips pulled off walking by 200–250 m stop spacing, car owners pulled by metro/rail at both
  ends of jammed corridors: ×1.1–1.3.
- Together **M ≈ 1.4–2.1; 2.5 is a stretch**. The theoretical top, if today ~25–35% of resident trips use
  transit (0.39–0.52 boardings per resident trip: 2,882 over ~5.5–7.3k trip starts/week, from `tripgen.py`
  with 57% of residents holding a job or school place, p 0.78, plus up to 3.5k shopping starts) and every trip
  rode with 2 legs, is **M ≈ 3.5–4.5** [E]. 34k needs **M 5.0–6.6 at 100k and 3.6–6.2 at 150k**.
- Every figure in this table is an estimate. The weakest inputs are today's vehicle count (from density,
  ±50%) and the trip rate (shopping only bounded, trip durations ignored). The table spans the vehicle range; see 4.7.7 for what
  the second opinion found.

#### 4.7.3 The 10x network (vanilla modes only)

Design rules from 4.7.1:
1. **Feeders end at trunk stations; nothing runs across a trunk.** Every cross-city trip becomes
   feeder + trunk (+ feeder). This is where most of the count gain comes from, and it is an accounting
   gain: the same people, counted per boarding.
2. **Shared hub stops.** The two halves of a split line stop at exactly the same positions at the hub, so
   the transfer is a few metres of walking (cost ~100 m of walking), not a street crossing.
3. **Stop spacing 200–250 m** on feeders in mixed home/job areas (line 246's pattern: 15 stops on 2.3 km,
   241 riders/week = 106 per km, against 30–59 per km on the long lines [M]).
4. **Metro and rail only where they link homes to jobs that buses do not already serve**; a line beside
   another line within ~150 m takes its riders (B18, metro diagnosis). New metro adds riders mainly by
   serving new districts and by pulling car owners off jammed roads; between two transit options it only
   moves riders.
5. **Lean fleets, few trucks.** Vehicle slots throttle trips (4.7.1). Budget 100 on new feeders; raise a
   line only where one stop has > 300 waiting and its corridor density is < 80 (B18: more buses on a
   jammed road added nobody). Offices over industry in new zoning (fewer trucks).
6. **Coverage is not the constraint**: 99.3% of homes within 400 m of a bus stop, 98.9% within 800 m of a
   metro/rail stop; jobs 96.2% / 98.1% [M, `demand.py`].

**Phase 1 lines (42k → 55k), all dry-run with `dryRun:true`, every stop `via: segment`, max snap 0.6 m, 0
segment-0 snaps** (`dry_splits.json`, `dry_10x_North_Circulator.json`):

| # | Line | Stops (x, z) in order | Est. length | Buses (service 150, line budget 100) | Replaces / notes |
|---|---|---|---|---|---|
| 1a | **94N Robert North feeder** (hub 1005) | (1455,2693) (1640,2940) (2057,3204) (2246,3449) (2457,3502) (2655,3598) (2612,3777) (2662,4072) (2488,3944) (2316,3901) (2177,3768) (2156,3264) (1588,2557) | ~6.5 km | 29 at line budget 175 | = line 94 stops 4–16. Hub stops 4 (1455,2693) and 16 (1588,2557) are 33 m and 208 m from metro 1005 (1423,2683). |
| 1b | **94W Airport–Core2 feeder** (hub 1005) | (1588,2557) (1067,2751) (950,2369) (1180,2353) (1531,2328) (1561,2173) (1589,2224) (1455,2693) | ~3.8 km | 17 at line budget 175 | = line 94 stops 16, 17, 18, 0–4. Then delete line 94 (43 buses at budget 175). Keep 94's budget: stop 4 already has 155 waiting, so the split must not cut capacity. |
| 2a | **4N Laurel North feeder** (hub HIT) | (2329,3653) (2585,3716) (2905,3770) (3086,3676) (2914,3030) (2937,3021) (3110,3674) (2905,3794) (2582,3725) (2326,3661) | ~3.8 km | 10 | = line 4 stops 0–4, 15–19. Hub (2914,3030)/(2937,3021) is ~170 m from Red HIT (2886,2862). |
| 2b | **4S Laurel Middle feeder** (HIT → 31333) | (2914,3030) (2661,2399) (2546,2177) (2522,1863) (2723,1527) (2925,1191) (2945,1203) (2744,1539) (2542,1876) (2567,2166) (2684,2390) (2937,3021) | ~5.2 km | 13 | = line 4 stops 4–15. Then delete line 4 (23 buses). |
| 3 | **North Circulator** (Red SH feeder) | (2668.5,3747.2) Garland (2412.4,3683.7) Garland (2313.4,3512.9) Wright (2202.6,3400.2) Pierce Blake (2392.4,3396.3) Crest (2647.8,3465.2) Crest (2750.0,3621.8) Clarke | 1.68 km (road graph) | 5 | New. First stop ~80 m from SH (2741,3782). |
| 4 | Metro Red 7/227 budget 100 → 50 | — | — | 6 → 3 trains each | Cost and 30 vehicle slots only; waiting 2–6 per stop, and wait does not enter route choice. Expect no rider change. |

Build notes: save first and record 4–8 counted weeks of the combined riders on 4, 94, 245 and Red 7/227
plus waiting at the hub stops. Create the new lines, wait for no `LineNotConnected` and vehicles at target,
then delete the old line; diff line ids around every create and delete (lessons B2). Stops snap (dry run),
but **four closing legs are new paths the dry run cannot check**: 94N 16→4, 94W 4→16, 4N 4→15 and
4S 15→4 (old stop indexes). If one shows `LineNotConnected`, move that hub pair one stop along the line.
Keep or revert on the combined count of all affected lines over 4–8 weeks against the ±30% spread, with
no growing queue at the hubs. Keep ≤ 20 stops per line and closed loops (player rules); each half must stay
above 75 riders/week after 4 periods.

**Phase 2–4 trunk and feeders (as §4.3/§4.4, with the 10x rules added):**

| Phase | Trunk | Feeders (each ends at a station, 200–250 m stops) | Extra 10x rule |
|---|---|---|---|
| 2 (55–70k) | Blue West tunnel NO → W1-E (320,2000); edit 135/207 | W1 North Feeder → W1-E/W1-C | RH within 500 m of W1-E; offices on the highway edge |
| 3 (70–85k) | Blue West W1-C (−150,2200), W1-W (−550,1600); line 245 into N1 | W1 South Feeder → W1-W; N1 feeder → N 20505 | Split line 203 at SW 17218 (it carries 175 on 30 buses with 537–775 waiting at (1472,−415)/(2002,−133): check the corridor first) |
| 4 (85–100k) | C1-R on the SW branch; E1-R if E1 opens | C1 Feeder, E1 Feeder | Upzone RL → RH within 400 m of existing stations (main session, player's words) |

Considered and **not recommended for the count**:
- **Red ↔ Airport Express through-link (NR → S2)** and **FN → FL "Purple"** (metro diagnosis fixes 1–2):
  they turn today's two-leg trips into one-leg trips (count-neutral or negative) and mostly move riders
  from bus 94 and Blue. Build only as a phase-3 experiment if car-owner capture is wanted, after the
  bridge can read the mode split.
- **Removing car-friendly shortcuts**: only ~15–20% of trips have a car to give up, and longer car trips
  keep more vehicles on the map, which lowers p and so trips for everyone. The facts do not support it.
- **More buses on long queues** without a road fix (B18), **a second airport** (B19), **uniques for
  tourists** (B22), **HeavyTrafficBan for mode shift** (4.7.1).

#### 4.7.4 Phasing and rider targets

| Phase | Residents | M by then | Target riders/week | Stretch (M) | Check (last 4 periods, judged against the no-change spread) |
|---|---|---|---|---|---|
| now | 41.8k | 1.0 | 3,563 [M] | — | — |
| 1 | 55k | 1.3 (splits 1–2, circulator 3) | **5,700** | 7,100 (1.65) | 94N+94W above 94's periods ×1.3; 4N+4S above 4's 221–397 ×1.3 |
| 2 | 70k | 1.45 | **7,500** | 9,200 (1.8) | W1-E and the W1 feeder ≥ 75 each |
| 3 | 85k | 1.6 | **9,500** | 11,600 (2.0) | boardings per resident ≥ 0.11/week |
| 4 | 100k | 1.65–2.0 | **10,900–12,900** | 15,900 (2.5) | ≥ 3x today |

Targets use the middle vehicle case (today ~3.65k vehicles, 45% resident car trips); with 1.8k or 5.5k
vehicles the 100k target at M 1.65 moves to 12.2k or 9.6k [E].

#### 4.7.5 Constraints

- **Bridge gaps** (belong in TODOS.md; this task could only write this file):
  (1) no `VehicleManager.m_vehicleCount` or `CitizenManager.m_instanceCount` read: the trip throttle p
  cannot be measured; (2) no mode-split read: counting citizen instances by state (walking, waiting,
  in a transit vehicle, in a car) would give the live share and settle the verdict; (3) no population
  read (`citizens.count` 43,415 includes tourists); (4) no line-path read, so a split's turnaround can
  only be checked after creation; (5) no vehicle positions.
- **Vehicle limit**: 16,384 including every train car. Watch it from phase 3 on; it is the ceiling on
  trips, not only on traffic.
- **Traffic**: lines 4, 13, 94, 142 run on the jammed Laurel/Richardson corridors (§4.1). Bus lanes there
  need road rebuilds with fronting buildings (B20): player or main session.
- **Classifier rule**: any step that demolishes occupied buildings or rezones built blocks (station-area
  upzoning, bus-lane rebuilds with fronting buildings) runs in the main session right after the player's
  own approval of that exact step.
- **Player rules**: ≤ 20 stops per bus line, closed loops, lines < 75 riders/week redesigned or removed.
- Observed during this read: two `Overground Metro Station 01` buildings (18470 at (1614,2295), 32802 at
  (848,1919)) with no lines, presumably the phase-1 builder's work in progress; line 18's budget is 100
  although B27 recorded 100 → 75.

#### 4.7.6 Top 10 actions for the builder, ranked

Expected riders/week are increments over today, each measured over 4 periods against the no-change spread
(bus lines swing ±30% week to week) [E unless marked]:

| Rank | Action | Phase | Expected riders/week |
|---|---|---|---|
| 1 | **Add the missing read** (vehicle count, instance count, citizen instances by state) to the bridge, then re-run `ceiling.py` with measured inputs. Decides whether 34k is even possible. | now | 0 (decision value) |
| 2 | **Split line 94 at 1005** into 94N + 94W (table 1a/1b) | 1 | +230–340 (through trips counted twice), if the closing legs path and the hub queue does not grow |
| 3 | **Split line 4 at HIT** into 4N + 4S (2a/2b) | 1 | +80–130 (plus some riders moving to Red) |
| 4 | **North Circulator** feeding Red SH (3) | 1 | +60–150 bus, +30–80 on Red |
| 5 | **Blue West + W1 feeders ending at W1 stations**, RH within 500 m of W1-E/W1-C/W1-W | 2–3 | +2,500–3,500 at ~30k W1 residents (0.069 × M 1.3–1.65 per resident) |
| 6 | **Every new district's feeder ends at its station, 200–250 m stops** (NW1, N1, C1, E1 per §4.4) | 1–4 | +1,000–2,000 by 100k |
| 7 | **Offices over industry** for new jobs (fewer trucks = more trips); no road-capacity projects that exist only for cars | 1–4 | each 1,000 vehicles avoided at 100k ≈ +6% trips ≈ +400–700 |
| 8 | **Densify stops** to 200–250 m on the split halves and on 13, 142, 203 in mixed cells (edit addStops, ≤ 20 stops) | 1–2 | +100–300 |
| 9 | **Split line 203 at SW 17218** after reading its corridor (537–775 waiting at two stops) | 3 | +50–150 |
| 10 | **Bus lanes on Laurel/Richardson** (player/main session) to cut queue abandonment | 2–3 | 0–10% on lines 4/13/94/142, i.e. +0–120 |
| — | Red 7/227 budget 100 → 50 | 1 | 0 (frees 30 vehicle slots and upkeep) |

Sum of 2–10 by 100k: about **+4,000–7,500 on top of population growth**, which is what the 10.9–12.9k
target in 4.7.4 assumes.

#### 4.7.7 Not tested, and the second opinion

Not tested: any line in game (dry runs check stop snapping only, not paths); the vehicle count (inferred
from traffic density with the B20 rule); the trip rate (`tripgen.py` ignores shopping and trip duration);
today's legs per trip (assumed ~1.2–1.3); bicycles; the effect of any split on car owners and short trips;
whether citizen instances, not vehicles, bind the throttle. The 0.37 boardings per trip and the M ceiling
rest on those.

Second opinion: one blind, refute-only consult (Cursor `gpt-5.6-sol-high`; prompt and answer in
`consult_prompt.md` / `consult_out_sol.txt`; code excerpts and live figures pasted by script, my numbers
withheld). grok was not run: this is a plan, nothing is irreversible or about money. What it found:
- **ESTABLISHED:** C3 (forced car to road connections without an intercity bus station) and C4 (one count
  per alighting, so transfers count twice). The other claims came back CONSISTENT because the prompt left
  out the proving lines; I have since checked them: `CitizenAI.cs:1066-1072` adds the Vehicle lanes for a
  car owner (C2); `ResidentAI.cs:435-455` calls `UpdateLocation` on every citizen step (C5);
  `CitizenAI.cs:1123` passes `isHeavyVehicle: false`, and `PathFind.cs:238-242` adds HeavyBan only for
  heavy paths (C9).
- **C6 was overstated**: shopping departures bypass `DoRandomMove`, and the throttle follows citizen
  instances instead of vehicles if instances exceed ~4 × vehicles. The model now leaves 25% of trips
  ungated (`g`), and 4.7.5 lists the instance count as unmeasured.
- **Independent figures**: 0.19 / 0.025 trip starts per eligible step (mine 0.17 / 0.046 with state
  dynamics), 3,384 vehicles and p 0.79 (mine ~3.65k, 0.78), and a needed multiplier of **4.87x at 100k and
  3.70x at 150k** on total boardings with all vehicles throttled (mine 5.4x / 4.0x in the same case,
  5.0–6.6x / 3.6–6.2x across cases). Agreement on order of magnitude; the gap is the assumptions named.
- **Disagreement left open**: it put today at ~1.26 boardings per resident trip (11k workers, shopping and
  returns left out, which it flags as biased upward); I get 0.39–0.52 (57% of residents with a job or
  school place, shopping included). If its figure were right there would be almost no share headroom and
  34k would be further away. Its verdict: "mechanically possible, but not established"; weakest link the
  citizen-instance count. Cheapest settling experiment (both of us): read `VehicleManager.m_vehicleCount`
  and `CitizenManager.m_instanceCount` (action 1).


A second blind consult on the phase-1 design (`consult2b_prompt.md` / `consult2b_out_sol.txt`; the first
attempt, `consult2_out_sol.txt`, answered something else and was discarded) found:
- **My claim that the splits reuse only existing paths was wrong**: the four closing legs above are new, and
  the circulator is all new. Build notes now say so.
- **Capacity risk on 94**: 94's stop 4 has 155 waiting; my first draft cut 94's corridor from 43 to 27 buses.
  Waiting does not enter route choice, but abandonment after ~8 weeks does lose riders. The halves now keep
  94's line budget (175): 29 + 17 buses.
- **The circulator may mainly redistribute** riders from 4, 94, 245 and Red (as line 60 did to Blue in B18);
  judge it on the combined total of those lines, not on its own count.
- K1 (splits raise the count) and K4 (fewer buses on 4 lose nothing) came back only CONSISTENT: neither is
  established until the before/after count exists.

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
| Education / youth / tourism | +6 `Elementary School` (3 in NW1, 3 in existing gaps with the lowest coverage), 2 `Library 01` (NW1 at 1005, HS); NW1 `High School` within 250 m of 1005; 4 parks/playgrounds in NW1 so every family block is ≤ 400 m from one; residential in ≤ 50-cell chunks. Feeder and NW-R open **before** NW1 is zoned. |
| Acceptance | population ≥ 55k (UI); homes ≥ 11.5k (`cap2.py`); Water, Electricity, Death problems each ≤ 5; LandfillFull 0; R demand > 0 and W demand ≥ 0; road anomalies 0 and disconnectedLocalRoadComponents 0; coverage ≥ 98%; all lines healthy. |
| Acceptance (E/Y/T) | NW1: 100% of residential growables within 500 m of an Elementary School and within 800 m of rail/metro (`tmp/tampa/master/schoolcov.py` + `cov.py`); city elementary coverage ≥ 85% (now 81.6%); NoEducatedWorkers ≤ 3; EducationBoost still in `/state/policies`; Education budget ≥ 100% confirmed by the player (not readable). |
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
| Education / youth / tourism | +5 `Elementary School` in W1 east, `High School` at W1-E (≤ 250 m), 5 parks/playgrounds; W1-E and the W1 North Feeder open before W1 zoning; new parks/plazas on the W1-E station square. |
| Acceptance | population ≥ 70k; homes ≥ 14.7k; Blue 135/207 no problems and vehicles = target within 1 game day; W1 growables 100% within 400 m of a stop; highway flow not below today's 59%. |
| Acceptance (E/Y/T) | W1 east: 100% of homes within 500 m of an Elementary School, 400 m of a stop, 800 m of W1-E; high-school coverage ≥ 70%; airplane + ship passengers not below their no-change range (B22: air 54–104/wk). |
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
| Education / youth / tourism | +5 `Elementary School`, `University` + `Library 01` at W1-C, `High School` at W1-W, 2nd N1 `Elementary School`; 5 parks in W1 west; tourist-facing plaza/park at W1-C. |
| Acceptance | population ≥ 85k; homes ≥ 17.8k; Red 7/227 riders up (compare 4 periods before/after N1 opens against its no-change range); modal share ≥ 10%. |
| Acceptance (E/Y/T) | university coverage ≥ 65% of homes (now 59.9%); high-school coverage ≥ 72%; every new High School/University ≤ 250 m from a platform; tourist passengers on transit (`cityPassengersByType.total.tourists`, 741/wk now) not below the pre-phase range. |
| Agents | as phase 2. |
| Risks | Vehicle and citizen-instance limits start to matter (not readable, see §6); office jobs level only with education. |
| Player decisions | South highway junction consolidation; bus line. |

### Phase 4 — 85k → 100k

| | Work |
|---|---|
| Roads/zoning | C1 (grid x 900–1700, z 200–840); level-up programme (education, parks, land value near stations). If short: E1, then SW1. Optional: upzone RL → RH within 400 m of metro stations (player). |
| Services | Remaining §3 counts, placed where problems appear. |
| Transit | C1-R station on the SW branch, C1 Feeder; E1-R and E1 Feeder if E1 opens; SWB and line 90 extension only if SW1 opens. |
| Education / youth / tourism | +5 `Elementary School`, `High School` at C1-R, 3 parks in C1; any new unique or park within 300 m of a trunk stop. |
| Acceptance | population ≥ 100k (UI); homes ≥ 20.9k; problems total ≤ today's 81 with no FatalProblem cluster; modal share ≥ 12%; coverage ≥ 98%; all lines healthy; trafficFlowPercent ≥ 55. |
| Acceptance (E/Y/T) | elementary coverage ≥ 90%, high ≥ 75%, university ≥ 65% of residential growables; NoEducatedWorkers 0; tourist transit riders ≥ 1,000/wk [E target, +35% on 741]; every arrival hub ≤ 1 transfer to West CBD and SE clusters (check line stop lists). |
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
