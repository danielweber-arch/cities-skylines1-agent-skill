# City: Ashford Transit

Goal: 5,000 population, no unaddressed problems
Style: Transit-oriented core
Layout: one arterial spine from the highway entry; commercial and the densest residential within one block of the spine; low-density residential outward
Industry: generic, downwind of housing, beside the highway connection
Must have: power, water, sewage, schools, fire, police, health, parks, cemetery, garbage
Avoid: low ground near water; industry within 2 blocks of housing (offices as buffer after Big Town); more than 80% of funds committed at once (20% reserve)
Save cadence: after every phase, name `Ashford-P<n>`
Map: NewSave (fresh map, highway only) | Save file: NewSave.crp | Started: 2026-09-25

## Research summary
- One tile is 1.92 x 1.92 km. Starting funds ₡70,000. Milestone cash: Little Hamlet +20k, Worthy Village +20k, Tiny Town +20k, Boom Town +35k, Big Town +45k. Milestone populations are map-dependent (Boom Town 650–2,800; Big Town 1,800–8,000).
- Roads: `Basic Road` = Two-Lane, 16 m wide, ₡40 per 8 m cell. `Medium Road` = Four-Lane, ₡60/cell. Highway and Highway Ramp cannot be built before Boom Town; branch city roads off the pre-built highway end instead.
- Grid spacing 80 m = 16 m road + two 32 m zoning strips (4 cells deep each), 64% of land zoned. First junction after the highway interchange at least ~480 m away; prefer T-junctions; collectors and locals as Two-Lane so no traffic lights.
- A 500 x 500 m starter district is ~7 km of road ≈ ₡35k. A 2 x 3 km grid would be ~₡775k and cannot be paid for.
- Zoning: early block ratio R:I:C ≈ 5:2:1. Zone residential in chunks of ~50 cells (2–3 blocks), never more than 150, to avoid death waves. Industry at least 2 blocks from housing. Offices and high density only unlock at Big Town.
- Transit: `Bus Depot` (₡30k, Boom Town) is required before any bus runs. Bus stops 320–400 m apart, on both sides of the street, away from junctions, one line per stop. Metro only at Big Town. Before Boom Town, "transit-oriented" means: build the spine now, keep the densest zoning within one block of it, add the bus loop at Boom Town.
- Water: `Water Intake` (₡2.5k, upstream, touches water) and `Water Outlet` (₡2.5k, downstream shoreline). If the water is still (bay/lake), use `Water Tower` (₡3.5k, clean ground only) instead of an intake. Pipe coverage reaches 88 m from a `Water Pipe` (₡20/cell).
- Power: `Wind Turbine` ₡6k, noise 75, on high ground away from housing; `Coal Power Plant` ₡19k pollutes, industrial side only. `Power Line` ₡20/cell from the plant to the first zoned block; buildings pass power to neighbours, roads do not.
- Services by unlock: `Elementary School`, `Medical Clinic`, `Landfill Site` (Little Hamlet); `Fire House`, `Police Station` (Worthy Village); parks such as `Regular Park` (Tiny Town); `Cemetery`, `Bus Depot` (Boom Town). Landfill has ground pollution 100: keep it away from housing and the water intake. Landfill and cemetery on the arterial near the highway with two access routes; a full one cannot be demolished, so add a second before the first fills.
- Taxes: 9% default, up to 12% without complaints.
- UNCONFIRMED from research: numeric coverage radii for fire/police/health/school; whether the bridge enforces milestone locks; whether wind direction is readable.

## Site survey
- Fresh map: 0 buildings, 0 zones, 0 local roads, 0 pipes, 0 power lines. Sim paused. Population 0 (`citizens.count` 1049 is pass-through agents). Taxes 9% across the board. Money is not exposed by any endpoint; budget by the research costs and keep 20% of ₡70k (₡14k) uncommitted.
- Highway: the Layton/Stephanie Lee trunk runs from the SE edge (8648,-5916) through (0,0) north to the N edge (~1480,8648). A prebuilt interchange at ~(610,1960) feeds two one-way carriageways east: outbound "Edward Young Highway" ends at node 31748 (1504,1958) y≈176; inbound "Empire Highway" starts at node 28996 (1503,1998). These are the attachment points. Segments x 1008–1504 are a "Highway with Bus Only Lane Barrier" variant; nodes 7466 (1008,1952) and 11436 (1006,1992) carry pre-existing `RoadNotConnected` warnings. Left alone; see openIssues.
- Rail line along z≈4083–8552 (north). Keep the city south of z≈3800.
- Water: no water body visible near the interchange. Nearest known water is under the Highway Bridge at x≈47–111, z≈494–735 (deck y 160–177), ~1.5 km SW of D1. Ship paths exist only far south/east. Water extent and flow direction unknown: `/capture` renders sky only (bug logged), so the P3 worker must probe shoreline positions with `Water Outlet` placement and read the error.
- Prefabs confirmed on this install: Basic Road, Medium Road, Large Road, Highway, HighwayRamp (no space), Power Line, Water Pipe, Wind Turbine, Coal Power Plant, Water Intake, Water Outlet, Water Tower, Landfill Site, Medical Clinic, Cemetery, Fire House, Police Station, Elementary School, Regular Park, Bus Depot. No Inland Water Treatment Plant (no Sunset Harbor), so sewage needs a real shoreline.
- Anomalies at start: road 0, zone 0, building 0. Problems: only the 2 highway `RoadNotConnected` nodes.
- Limitation: the bridge has no command to create a transport line. The Boom Town bus loop must be drawn in-game by the operator; the plan reserves the stops and the depot site.

## Districts
Spine: `Medium Road` along z=1978 from the merge at (1560,1978) east to (2120,1978), built as seven 80 m pieces so grid nodes snap onto it. Merge: (1504,1958)->(1560,1978) and (1503,1998)->(1560,1978), both `Medium Road`, snapping onto nodes 31748 and 28996.

### D1 — Transit core
- bbox: x[1720,2120] z[1818,2138] (build_grid origin (1720,1818), cols 5, rows 4, spacing 80, `Basic Road`; the z=1978 row reuses the spine)
- zones: blocks touching the spine (rows z 1898–2058): CommercialLow on the 4 blocks nearest x=1720–1880, ResidentialLow elsewhere; outer rows ResidentialLow. Target ~5 R : 1 C blocks. High density and offices added along the spine after Big Town.
- residential painted in chunks of <= 3 blocks per pass, then 2 sim weeks before the next chunk
- future bus stops: spine nodes at x=1720, 2040 (320 m apart), both sides; Bus Depot site reserved at (1600,1900) south of the spine near the highway
- depends on: P1 spine

### D2 — Generic industry
- bbox: x[1560,1800] z[1498,1658] (build_grid origin (1560,1498), cols 3, rows 2, spacing 80, `Basic Road`)
- zones: Industrial only
- 160 m (2 blocks) gap to D1's south edge; nearest the highway; collector `Basic Road` from (1640,1658) north to the spine node (1640,1978) with nodes every 80 m
- depends on: P2 D1, only after workplace demand > 0 or Little Hamlet, whichever first

### Utilities sites
- Power: `Wind Turbine` #1 at (1920,2260) north of D1 (>= 120 m from the nearest housing edge z=2138); `Power Line` south to the D1 node (1920,2138). Second turbine only when NoElectricity appears.
- Water supply: `Water Tower` at (1640,2100), clean ground north-west of D1, `cs1_connect` to the collector/spine.
- Sewage: `Water Outlet` on the shoreline near the bridge, first candidates around (130,600) and (20,600); probe with place-building and read the error. `Water Pipe` trunk from the outlet to (1720,1978), then one branch under each D1 row.
- Services (as milestones unlock): Elementary School and Medical Clinic inside D1 on the spine's north side; Landfill Site south of D2 at (1680,1420) with two road links; Fire House and Police Station at D1's west end; Regular Park x2 in the outer rows; Cemetery on the collector at (1560,1740); Bus Depot at (1600,1900).

## Phases
- [x] P1 Highway connection — merge segments onto nodes 31748/28996, spine to (2120,1978) as 80 m `Medium Road` pieces; accept: cs1_state_external_connections.localRoadComponents == 1 and disconnectedLocalRoadComponents == 0, road_anomalies(includeDeadEnds=false).total == 0, problems.RoadNotConnected count unchanged (2, the pre-existing highway nodes)
- [x] P2 Core roads — accept: road_anomalies.total == 0, D1 grid built with blockCenters recorded, spine junctions are T-junctions
- [x] P3 Utilities — accept: power source + water source + sewage outlet exist, each cs1_connect'ed, building_anomalies.total == 0, pipes and power line reach D1
- [ ] P4 Zoning — accept: zone_anomalies.total == 0, D1 zoned per mix with residential in <= 50-cell chunks per pass, industry only inside D2
- [ ] P5 Services — accept: every must-have service placed and connected as its milestone unlocks; after 2 simulated weeks no NoWater/NoElectricity/NoSewage problems, and no service-coverage problems for unlocked services
- [ ] P6 Grow to goal — accept: cs1_state_summary population >= 5000, problems.total trending down over 3 checks, saves verified. Loops P4/P5 as demand and milestones allow, keeping 20% reserve

## Amendments
- 2026-09-25 P2: the operator is building in-game concurrently (Richardson Avenue north from the merge node, zoning in D1 west, a Water Outlet at (3405,2284)). Plan adapts: P3 powers and pipes that outlet instead of placing one; water is confirmed east at x≈3400 (y 67) and north under the Richardson bridge (y≈130). Never bulldoze operator-built entities.
- 2026-09-25 P0: highway attachment moved from the survey's (1008,1952)/(1006,1992) to the true carriageway ends (1504,1958)/(1503,1998) after tracing the segments node by node.
- 2026-09-25 P0: bus loop cannot be created through the API; operator draws it in-game at Boom Town.
