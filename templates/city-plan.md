# City Plan: <name>

Vision: <one sentence, verbatim from the user>
Map: <name> | Starting funds: <n> | Save: <filename>

<!--
Copy this file to the repository root as city-plan.md and fill it in during the planning
phase. The rigid structure is the point: it is what a session with no memory of the original
conversation reads to recover the intent.
-->

## Constraints

- Highway connection at (x, z)
- Water flow direction: <n/s/e/w>   [industry goes downstream of residential]
- Prevailing wind: <direction>       [industry goes downwind of residential]
- Buildable bounds: x[min,max] z[min,max]

## Road hierarchy

| tier | prefab | spacing | role |
|---|---|---|---|
| arterial | Medium Road | — | district spine, connects to the highway |
| collector | Basic Road | 160m | district internal |
| local | Basic Road | 80m | block grid |

<!--
80m is not arbitrary. CS1 zoning cells are 8m and zoneable depth is 4 cells (32m) per side,
so 80m of spacing fills the block from both sides with 16m left for the road. Wider spacing
leaves an unzoneable dead strip down the middle of every block.
-->

## Districts

### D1 — Downtown Core

- bbox: x[380,700] z[120,400]
- zones: CommercialLow 60%, ResidentialHigh 40%
- rationale: adjacent to the highway ramp, walkable to transit
- depends on: none

### D2 — North Residential

- bbox: x[380,700] z[400,720]
- zones: ResidentialLow 80%, CommercialLow 20% (perimeter)
- depends on: D1

<!--
Valid zone names, exactly: ResidentialLow, ResidentialHigh, CommercialLow, CommercialHigh,
Industrial, Office, Unzoned. CS1 has no plain "Commercial" or "Residential" zone.
-->

## Phases

- [ ] P1 — Arterial spine + highway connection
      accept: cs1_state_road_anomalies total == 0, spine reaches every district bbox
- [ ] P2 — D1 grid + zoning
      accept: cs1_state_zone_anomalies total == 0, >80% of the D1 bbox zoned
- [ ] P3 — Core services (power, water, sewage) for D1
      accept: cs1_state_problems reports no NoPower/NoWater inside the D1 bbox
- [ ] P4 — Simulate 4 weeks, verify growth
      accept: population > 1000, nothing unaddressed in cs1_state_problems
- [ ] P5 — D2 grid + zoning + service extension

<!--
An acceptance criterion must be a literal API assertion. "Looks fine" is not a criterion.
Record the passing values in progress.json when you close a phase.
-->

## Amendments

<!--
Append-only. Every deviation from the plan gets a dated line here, with what changed and why.
Never silently improvise around a blocked plan — amend it, then proceed.
-->
