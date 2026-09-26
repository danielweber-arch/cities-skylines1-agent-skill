# CS1 Public Transport: Expert Reference

For an agent reviewing and rebuilding a real city's transit through the bridge API.
Read `knowledge.md` first. Where this file disagrees with it, the reason is in §0.

**How to read the evidence tags**
- `[code: Class.Method]` means read directly from the game's own `Assembly-CSharp.dll` in the local Steam install
  (`.../Cities_Skylines/Cities.app/Contents/Resources/Data/Managed/`), decompiled with `ilspycmd` on 2026-09-26.
  This is the strongest evidence in the file. It describes the installed build only.
- `[S#]` is a web source (listed at the end).
- **UNCONFIRMED** means no source or code was found. Treat it as a heuristic and verify it in-game.
- **DERIVED** means my own arithmetic on cited inputs. Nobody else has checked it yet.
- Units: 1 zoning cell = 8 m. Distances in code are in metres.

---

## 0. Corrections to knowledge.md

| knowledge.md says | What the code shows | Verdict |
|---|---|---|
| "Transit is weighted as preferred over private cars." | `PathFind` has no general transit-preference multiplier. It compares cost ≈ distance ÷ speed. The transit-favouring terms that do exist are: Free Public Transport shortens transit lanes' pathfinding length ×0.75 `[code: TransportLineAI.GetCostMultiplier]`, cars pay +20 cost units on bus lanes, and bus-only road lanes get a −25% discount `[code: PathFind.ProcessItemCosts]`. Transit looks "preferred" mainly because most cims never have a car (§1.1). | **Qualify.** Transit is not weighted up by default. Free transit is the only city-wide boost. |
| "Cims pick routes by travel time, distance, and number of transfers. Fewer transfers win." | Vanilla has **no wait-time or per-transfer penalty** in pathfinding. The explicit transfer penalty exists only in the TM:PE mod ("realistic public transport", marked NON-STOCK) `[S1]`. Transfers still cost something in vanilla: each one adds a walking leg, and lane-connection walks carry extra terms (a fixed +100/(0.25·maxLength) when a same-type leg starts, and connection distance ×10 between two ticketed lanes) `[code: PathFind.ProcessItem*]`. How big these terms are in practice is **UNCONFIRMED**. | **Qualify.** Fewer transfers win indirectly, through walking cost, not through a transfer penalty. |
| "Vehicles on a line scale with line length and the per-line budget, not ridership." | Confirmed: `ceil(serviceBudget% × lineSlider% /100 × lineLength / (m_defaultVehicleDistance × 100))` `[code: TransportLine.CalculateTargetVehicleCount]`. | **Confirmed.** Also scales with the service budget. |
| "Free transit boosts it further." | Confirmed, with the mechanism: ticket = 0 and transit lane cost ×0.75 in the policy district `[code: BusAI.GetTicketPrice, TransportLineAI.GetCostMultiplier]`. | **Confirmed.** |

Everything else in knowledge.md's transit section agrees with this file.

---

## 1. How cims choose a mode

### 1.1 Car availability comes first, and it is fixed per citizen
`ResidentAI.GetVehicleInfo` seeds a `Randomizer` with the **citizen ID**, so each cim gets the same result on every trip. It then rolls car, bike, and taxi. Figures are the % chance for each age group `[code: ResidentAI.GetCarProbability/GetBikeProbability/GetTaxiProbability]`:

| Age | Car | Bike | Taxi (only when no car) |
|---|---|---|---|
| Child | 0 | 40 | 0 |
| Teen | 5 | 30 | 2 |
| Young adult | 15 | 20 | 2 |
| Adult | 20 | 10 | 4 |
| Senior | 10 | 0 | 6 |

- A successful bike roll overrides a car roll. **Encourage Biking** adds +10 percentage points to the bike chance for residents whose *home* is in the district `[code]`.
- Taxi is only usable if taxi capacity exists. Even then, it is considered only at night or on a 50% coin flip by day `[code: CitizenAI.StartPathFind]`.
- The same numbers appear as "game defaults" in the Lifecycle Rebalance mod `[S2]`.
- Tourists: car 20%, bike 20%, taxi 20%. Car rises to **90% at an Airports-DLC airport with the Car Rentals policy** `[code: TouristAI]`.
- **Implication (DERIVED):** roughly 7 in 10 adult cims never have a car or bike. For them the only options are walking or transit. For an adult, P(no bike and no car) = 0.9 × 0.8 = 0.72.

### 1.2 The walking cap
- A continuous walking leg is cut off at **1,000 m** (`m_methodDistance < 1000f`), about 125 cells. The cap resets when the cim changes lane type, for example on boarding `[code: PathFind]`. TM:PE raises it to 2,500 m `[S1]`.
- If a car-less cim's destination is beyond the walking cap and no transit path exists, pathfinding **fails**. The trip is dropped (`ArriveAtDestination(success:false)`). Only car users trigger the building "path failure" callback `[code: HumanAI.PathfindFailure]`.
- **So most new ridership on a line into an unserved area is latent demand becoming possible, not drivers switching.** What that does to employment and shopping is **UNCONFIRMED**.

### 1.3 Route cost
- One pathfind covers every allowed lane type: pedestrian, public transport, plus car/bike if the cim has one. The cheapest total `comparisonValue` wins. The search is capped at `maxLength` 20,000 (160,000 on tours) `[code: CitizenAI.StartPathFind]`.
- Each segment costs about length ÷ (lane speed × maxLength).
- A transit leg's "length" is `segmentLength × costMultiplier × 100 / pathSpeed`. `pathSpeed` is the average speed along the vehicle's route from road/track speed limits `[code: TransportLineAI]`. **Consequences:**
  - Routing a bus over faster roads makes the whole line look faster to riders.
  - Metro and train track speeds make those lines look much faster than buses.
  - Live road congestion is **not** in the transit cost.
- Car legs get a random traffic factor of 0.9 to about (1.0 + density × 0.01) per segment. Jammed roads can make car routes look up to about 2× costlier, which pushes car owners onto transit. The density range is **UNCONFIRMED**. `[code: PathFind.ProcessItemCosts]`
- Ticket price adds a small randomised cost (`ticketCost × rand(0..2000) × 3.92e-7`) `[code]`.
- **Frequency and waiting time are not in the route choice.** They act afterwards:
  - A waiting cim's counter rises with 25% probability per step.
  - After it saturates twice (`BoredOfWaiting`), the cim gets `CannotUseTransport` and re-paths without transit: by car if it has one, otherwise the trip fails `[code: HumanAI.SimulationStep]`.
  - Overcrowded or infrequent lines therefore **lose trips** rather than lose "attractiveness".

### 1.4 Outside connections (commuters, movers, shoppers leaving the city)
- A resident trip to or from a **road** outside connection sets `BorrowCar`, which forces car probability to 100%, **unless a Level-3 public-transport bus building exists**. That is almost certainly the Intercity Bus Station, but the class-level mapping is **UNCONFIRMED**.
- With one present, the car is still forced with probability (80 − 40 × busBudget/100)%. That is 40% at 100% bus budget and 20% at 150% (**DERIVED**) `[code: ResidentAI]`.
- Intercity bus stations and a high bus budget therefore directly cut highway car traffic.

### 1.5 Why a line with good coverage still gets zero riders
Rank-ordered, each with its mechanism:
1. **Nobody needs it.** The trips it serves are under 1,000 m and walkable, or it duplicates a faster path. Pedestrian paths make walking legs shorter `[S3, S4]`.
2. **It goes nowhere people are going.** Waiting riders only board a line that serves their destination `[S5]`. Check where passengers want to go, then route to it.
3. **It is slow on paper.** A winding route, low-speed roads, or detours make pathSpeed low, so cost is high (§1.3).
4. **One-directional or broken.** Missing return-direction stops (knowledge.md), trams unable to turn back `[S6]`, or a line not connected to a depot.
5. **Riders gave up.** Long waits (§1.3) or too few vehicles because of a short line or low budget (§5).
6. **A car is cheaper for the 20%, and the other 80% walk or bike.** Only car owners can shift. Measure this with *trips saved* (§5.4).

### 1.6 What actually moves riders (ordered by mechanism strength)
1. **Coverage of car-less trips beyond 1 km.** Unlocks trips that could not happen before.
2. **Fast trunk lines.** Metro and train speed lowers cost for everyone.
3. **Free Public Transport.** Transit lane cost ×0.75 and ticket 0 `[code]`. Reported city-wide transit share 40–45% → 57% at 120k pop after enabling it; the calculation method was not stated `[S7]`.
4. **Direct routes and short transfer walks.**
5. **Enough vehicles.** Stops mid-trip abandonment.
6. **Car friction.** Car-banned segments (`CarBan` flag, e.g. Old Town / pedestrian areas) cost ×7.5 for cars `[code: PathFind]`. The policy-to-flag mapping is **UNCONFIRMED**.
7. **Intercity bus station plus bus budget** for trips that cross the city boundary (§1.4).
- **Park-and-ride is not a vanilla mechanic.** It is a TM:PE Parking AI feature `[S8]`. With Parking AI, one player reported transit use *fell* to 2.6k of 78k `[S8]`.

---

## 2. Per-mode reference

Unlocks are from the milestone page `[S9]`. Population ranges there vary by map/difficulty. Capacities and costs are from `[S6]` unless tagged.

| Mode | DLC | Unlock | Capacity / vehicle | Build / upkeep | Best role |
|---|---|---|---|---|---|
| Bus | Base | Boom Town | 30 (default; DLC models 20–100) | Depot ₡30,000 (biofuel ₡40,000); stops free; "each bus costs ₡12 to run" | Feeder, last mile |
| Trolleybus | Sunset Harbor | Busy Town | 30 (variants 26–90) | Needs overhead wires on the **whole** route | Quiet feeder in housing |
| Intercity bus | Sunset Harbor | Big Town | 60 (150 double decker) | Automatic to road outside connections, no player routes | Cuts BorrowCar (§1.4) |
| Tram | Snowfall | Boom Town | 90 | "slightly more expensive to maintain" than buses; depot cost UNCONFIRMED | Busy corridors, CBD loops |
| Metro | Base | Big Town | 150 base; 120–500 newer models `[S10]` | Underground ₡15,000 / ₡240 wk; ground ₡10,000 / ₡180; elevated ₡30,000 / ₡360 | Urban trunk |
| Train (passenger) | Base | Small City | 240 | Station ₡45,000 / ₡960 wk | Regional + intercity |
| Monorail | Mass Transit | Small City | 180 (120 / 240 variants) | UNCONFIRMED; noisy | Elevated trunk over roads, CBD |
| Cable car | Mass Transit | Small City | 30 | UNCONFIRMED | Slopes, water crossings |
| Ferry | Mass Transit | Boom Town | 40–80 by model | Needs depot; piers dock 2 at once | Water maps only |
| Blimp | Mass Transit | Big Town | 35 | UNCONFIRMED | Tourist / niche; slow landing |
| Helicopter | Sunset Harbor | UNCONFIRMED | "incredibly low" | — | Terrain, tourist flights |
| Ship (passenger harbor) | Base | Capital City | 100 | "very high cost", noise, heavy water + power use | Tourists / intercity only |
| Airplane | Base (Airports DLC expands) | Metropolis | 200 | UNCONFIRMED | Tourists / business visitors |
| Taxi | After Dark | Boom Town | UNCONFIRMED; about 25 taxis per depot `[S4]` | UNCONFIRMED | Tourists (20% taxi chance), gaps |

**Default ticket prices (UNCONFIRMED; search snippet only):** bus ₡1, metro ₡2, train ₡2, ship ₡5, plane ₡10.

**Ticket slider.** Vanilla shows a ticket-price slider **only on tourist-bus lines** `[code: PublicTransportWorldInfoPanel]`. Regular lines are priced only through policies.

**Check what exists before planning.** Query `cs1_prefabs_buildings` / `cs1_prefabs_networks`. A 2026-09-25 bridge dump of this install listed only bus, metro (including Train Stations pack hubs), train, harbor and airport prefabs. It showed no tram, monorail, ferry, taxi or trolleybus. That dump may be filtered to unlocked prefabs.

### 2.1 Stop spacing, line length, stop count
All values are community heuristics and **UNCONFIRMED** unless tagged. The walking cap (1,000 m) is a hard *upper* bound on catchment, not a target.

| Mode | Stop spacing | Stops per line | Notes |
|---|---|---|---|
| Bus / trolleybus | ~20 "squares" `[S11]`; "40–50" in another thread; units ambiguous | 5–10 (knowledge.md) | Stops opposite each other at regular intervals `[S4]` |
| Tram | 20 squares `[S11]`; "75–100" elsewhere | UNCONFIRMED | Needs two counter-rotating loops or a bi-directional line; cannot reverse `[S6]` |
| Monorail | 30–40 squares `[S11]` | UNCONFIRMED | |
| Metro | Grid 120 units apart puts everyone within 60 units of two stations `[S12]` | 8–10 per line `[S13]` | Full loop so both platforms work `[S14]` |
| Train | "200–220" `[S12]` | Few | Through-stations; keep cargo off (§3.6) |

### 2.2 Failure modes by mode
- **Bus.**
  - A stopped bus blocks traffic, including other buses. Stops too close to an intersection or roundabout block the lane `[S6]`.
  - Too many lines through one stop makes buses queue `[S4]`.
  - Bunching: vanilla unbunching holds a vehicle at a stop until the line's average interval has elapsed, capped at `waitTime ≥ 4` `[code: TransportLine.CanLeaveStop]`. That is weak, so vehicles still bunch after being spawned together.
  - The documented vanilla fix is to step the line's vehicle slider down to 1 vehicle, then back up one at a time `[S15]`.
- **Tram.** Cannot turn around; needs loops `[S6]`.
- **Metro.** Overlapping lines running the same direction cause congestion `[S6]`. Do not run many lines into one station; build adjacent stations and let people walk `[S4, S14]`.
- **Train.**
  - Every passenger station automatically creates intercity lines to **every** train outside connection `[code: TransportStationAI.CreateConnectionLines]`, so any station on the mainline takes intercity traffic and tourists.
  - Cargo trains stop at passenger stations that have no bypass `[S16]`.
- **Harbor / airport.** Their automatic intercity lines unload tourists who need local transit onward (§4).

---

## 3. Network design

1. **Hierarchy.** Bus/trolley feeds tram/metro/monorail, which feed trains/ships/planes (knowledge.md, `[S14]`). Long trips go by rail, change to metro for medium distance, then bus for the last block `[S14]`.
2. **Trunk first.** Metro track speed dominates route cost (§1.3). One good metro line beats several parallel bus lines `[S3]`.
3. **Transfer hubs.**
   - Put them where trunk lines cross and at district borders.
   - Keep the walk between platforms short, since every transfer is a walking leg (§0).
   - Integrated hubs (Hubs & Transport update: multi-level metro hub, harbor-bus hub) remove the walk `[S10]`.
4. **Lines vs loops.**
   - Two-way lines with stops on both sides are the default.
   - A one-way loop forces long return trips, which raises cost. Use loops only for short CBD circulators, or as two counter-rotating loops (the tram rule `[S6]`).
5. **Overlap.** Several lines sharing a stop make vehicles queue and block the curb lane `[S4, S6]`. Share *hubs*, not every stop.
6. **Bus lanes and bus-only roads.**
   - Cars pay +20 cost units on transport lanes, buses get ×0.95, and pure public-transport-road lanes get −25% cost `[code: PathFind]`.
   - Use them on the stretches where a line runs through congestion. Transit lane cost ignores live traffic, so riders keep choosing a jammed bus and then abandon it (§1.3).
7. **Tram on its own track.** Tram-only tracks avoid road traffic `[S4]`.
8. **Last mile.**
   - If a trip leg needs a car, the cim drives the whole way (knowledge.md).
   - Every trunk station needs feeder buses or a walk under 1,000 m to its catchment.
   - Pedestrian paths and shortcuts to stations shorten walking legs, and people walk further on paths than on pavements `[S4]`.
9. **Cargo vs passenger.**
   - Build separate passenger and cargo track networks.
   - Give each its own outside connections, and do not let them cross `[S16]`.
   - Give passenger stations bypass tracks.
   - Size rail junctions so a stopped train does not block the other line `[S16]`.

---

## 4. Airports and harbors

- **What they do.** Airports "bring tourists and business people". Harbors bring tourists by cruise ship `[S6]`. Both work through automatic intercity lines to outside connections `[code: TransportStationAI]`.
- **Arrivals need transit.** Knowledge.md: "every arrival hub needs local transit or tourists get stranded."
  - Tourists: 20% car, 20% taxi, 20% bike; the rest walk or ride transit `[code: TouristAI]`.
  - **Never enable Car Rentals** at an Airports-DLC airport if the goal is transit use, because it raises tourist car use to 90%.
- **Connect them.**
  - Put a metro or train station at the terminal. The Airports DLC adds airport bus, metro and train stations `[S17]`. Otherwise build a tram or subway terminal across the street `[S18]`.
  - Route that line straight to downtown and to the tourist zones.
- **Why downtown airports and harbors jam traffic.**
  - Every arrival spawns ground trips at once.
  - Car-owning and taxi tourists add road trips.
  - Harbors are noisy and resource-heavy `[S6]`.
  - Place airports away from housing for noise `[S18]`.
  - Avoid mixing passenger harbors with cargo-harbor truck routes (**UNCONFIRMED** specifics).
- **Intercity passengers crowd stations.** They pour into the one station on the mainline. Give it its own feeder lines and do not also make it the city's busiest local transfer point (**UNCONFIRMED** heuristic, from `[S16]`'s advice to split tourist and internal routes).

---

## 5. Economics and levers

### 5.1 Vehicle count
- Target vehicles = `ceil(serviceBudget% × lineSlider% / 100 × L / (D × 100))`, where L is line length and D is `m_defaultVehicleDistance` (field default 1,000; per-mode prefab values **UNCONFIRMED**) `[code]`.
- At 100% service budget and 100% line slider, that is one vehicle per D metres.
- **Ridership does not add vehicles.** An overloaded short line stays starved. Raise its line slider or lengthen it.
- The line panel shows vehicles as "active / target" `[code: PublicTransportWorldInfoPanel]`.

### 5.2 Upkeep
- Upkeep per vehicle = `m_maintenanceCostPerVehicle + capacity × m_maintenanceCostPerPassenger` `[S19]`, so larger vehicles cost more.
- Per-mode constants are **UNCONFIRMED**.

### 5.3 Policies
All from `[code]` unless noted.

| Policy | Effect | Cost |
|---|---|---|
| Free Public Transport | Ticket 0 on bus, tram, metro, monorail and local train lines. Transit lane cost ×0.75 in the district | All ticket income for those modes |
| High Ticket Prices | Ticket ×1.25; transit lane cost ÷0.75 (about +33%) | Fewer riders |
| Prefer Ferries | Ferry lane cost ×0.75 | — |
| Encourage Biking | +10 percentage points bike probability for district residents | — |
| Old Town | Cars other than residents' and businesses' barred `[S20]` | Deliveries may suffer |
| Heavy Traffic Ban | No cargo trucks in the district `[S20]` | Deliveries may suffer |
| Tourist Travel Card | Increases intercity bus use `[S6]` | UNCONFIRMED |

- Free transport does **not** zero plane, ship or intercity-train fares: their `GetTicketPrice` ignores the policy, and trains only apply it on local lines.
- High Ticket Prices and Prefer Ferries sit next to Mass Transit policies in the enum. Their DLC gating is **UNCONFIRMED**.

### 5.4 Reading the numbers
- **Riders per week.** Per mode and per line, residents and tourists are shown as an **exponential moving average**: `avg ← (8·avg + lastCycle + thisCycle)/10` `[code: TransportPassengerData.Update]`. Roughly 80% of each new reading is the old average.
  - After a change, the gap to the new level shrinks about 0.8× per stats cycle (**DERIVED**). About 11% remains after 10 cycles.
  - Stats roll over once per 4,096-frame cycle `[code: TransportLine.SimulationStep]`. The frame-to-game-week mapping is **UNCONFIRMED**.
  - **Wait at least 10 cycles before judging a change.**
- **What counts as a rider.** A count is added each time a passenger *leaves* a vehicle `[code: HumanAI]`. One trip with a transfer counts on both lines, so do not add line counts together to get "people using transit".
- **Trips saved % (line panel).** Car-owning riders ÷ the car owners expected from the line's age mix, clamped 0–100 `[code]`. This is the only built-in measure of *mode shift*. A community benchmark is that 50% is good, but a high-volume line at 20% beats a small line at 50% `[S13]`.
- **Waiting passengers per stop.** `TransportLine.CalculatePassengerCount(stop)`. This is how to find piling-up stops.
- **Transit share.** Vanilla shows no modal-split % (**UNCONFIRMED**; none found in the info-view code). Players quote weekly transit riders ÷ population:
  - 10–15% is a noticeable effect and a common target `[S21]`.
  - About 20% is typical `[S7]`.
  - 40–45%, rising to 57% with free transit, at 120k pop `[S7]`.
  - **The 50–70% "well-built city" figure is UNCONFIRMED**, and no source was found for it.
  - Because riders are counted per leg, compare the ratio before and after in the same city, never across cities.

---

## 6. Diagnostic checklist (run from data)

Thresholds marked H are **UNCONFIRMED heuristics**. Calibrate them against the city's own median line.

| # | Check | Data | Flag when | Fix, in priority order |
|---|---|---|---|---|
| 1 | Unreachable demand | Dense residential/commercial/office buildings vs nearest stop | Beyond 1,000 m walk from any stop (hard, `[code]`); H: beyond ~300 m for dense zones | Extend or add a feeder stop, then add pedestrian shortcuts |
| 2 | Empty line | Weekly riders | H: under 10% of city median line | Check it goes somewhere (§1.5), then re-route to a hub, then delete and redeploy its budget |
| 3 | Starved line | Waiting passengers at stops; vehicles active/target | Waiting grows each cycle, or active < target | Raise line slider, then split into two shorter lines, then upgrade mode (bus → tram/metro) |
| 4 | Productivity | Riders ÷ (vehicles × capacity); riders ÷ km | H: bottom quartile | Straighten route onto faster roads, then cut dead-end tails, then lower slider |
| 5 | Dead stop | Waiting count near zero over 10 cycles | — | Move toward density, or delete (fewer stops means a faster line) |
| 6 | Duplicate lines | Lines sharing over 50% of stops in the same direction (H) | — | Merge, or split one to a parallel street |
| 7 | Queueing stop | Over 2–3 lines on one stop (H), or vehicles backing up | — | Spread lines to adjacent stops, then build a station/hub |
| 8 | Jammed line | Line segments crossing high road-traffic segments | — | Bus lane or bus-only road, then re-route, then move the corridor to tram/metro |
| 9 | Orphan station | Trunk station with no feeder stop within walk | — | Add feeders; connect paths |
| 10 | Missing hub | Two trunk lines crossing without a shared or adjacent station | — | Add an interchange |
| 11 | Low mode shift | Trips saved % | H: under 20% on a line with high riders | Speed it up; car friction on its corridor |
| 12 | Arrival hub stranded | Airport / harbor / intercity station | No metro/train/bus within walk | Direct trunk to downtown |
| 13 | Cargo on passenger rail | Cargo trains through passenger stations | — | Separate track or add bypass |
| 14 | Boundary car trips | No intercity bus station; bus budget | Any | Build an intercity bus station; bus budget ≥ 100% |

---

## 7. Optimisation playbook (5k–100k pop)

1. **Baseline.**
   - Save the game.
   - Record, per line: riders (residents/tourists), vehicles active/target, length, stop count, trips saved %, and waiting count per stop.
   - Record city-wide: riders per mode and population, plus road traffic hotspots.
2. **Fix broken lines first.** Missing return stops, lines with no depot, trams that cannot turn, stops blocking intersections. These are free wins.
3. **Starved and queueing lines.** Raise sliders, split long lines, spread stops.
4. **Coverage gaps (checks 1 and 9).** Add feeders from dense unserved blocks to the nearest trunk station.
5. **Trunk.**
   - If there is no metro/rail spine, build one line through the densest corridor.
   - Put stations at the centres of density, not at their edges.
   - Connect it to arrival hubs (§4).
6. **Car friction and policies last.**
   - Bus lanes on jammed corridors.
   - Free Public Transport only when the budget can absorb lost fares.
   - Intercity bus station.
7. **Measure.**
   - Change **one line (or one corridor) at a time**.
   - Wait at least 10 stats cycles (§5.4), then compare against the baseline.
   - Keep the change only if total riders or trips saved rose and road hotspots did not get worse.
8. **Do not break what works.**
   - When replacing a line, build the new one alongside it.
   - Keep the old one until the new one carries riders for about 10 cycles, then delete it.
   - Never delete a line that has waiting passengers without first giving those stops another line.
   - Save before each change.

---

## Rules (ranked by impact)

1. Serve every dense area within walking range (hard cap 1,000 m) of a stop. Car-less cims simply cannot make longer trips without transit.
2. Build a fast trunk (metro/train) through the densest corridor. Speed is the pathfinding cost.
3. Feed every trunk station with buses or paths. Solve the last mile.
4. Put enough vehicles on busy lines. Vehicle count follows length × budget, not riders, and riders abandon after long waits.
5. Keep buses out of jams with bus lanes or re-routing. The pathfinder does not see road congestion, so a jammed line loses riders mid-trip.
6. Make routes direct and two-way, with stops on both sides. Avoid long one-way loops.
7. Use hubs for transfers, and do not pile every line onto one stop.
8. Connect airports, harbors and intercity stations to downtown by rail or metro. Never enable Car Rentals.
9. Separate cargo and passenger rail. Give stations bypasses.
10. Enable Free Public Transport when affordable. It gives about 25% lower transit cost plus a zero fare.
11. Build an intercity bus station and keep the bus budget up. This cuts forced car trips at the city boundary.
12. Change one thing at a time, wait at least 10 stats cycles, and judge by riders plus trips saved.

---

## Sources

- `[code]` Cities: Skylines `Assembly-CSharp.dll`, local Steam install, decompiled with ilspycmd 11.1 on 2026-09-26. Classes: ResidentAI, TouristAI, HumanAI, CitizenAI, PathFind, TransportLine, TransportLineAI, TransportStationAI, TransportPassengerData, PublicTransportWorldInfoPanel, BusAI, PassengerTrainAI, PassengerPlaneAI, PassengerShipAI.
- [S1] TM:PE pathfinding source and config: https://github.com/VictorPhilipp/Cities-Skylines-Traffic-Manager-President-Edition/blob/master/TLM/TLM/Custom/PathFinding/CustomPathFind.cs ; https://github.com/CitiesSkylinesMods/TMPE/blob/master/TLM/TLM/State/ConfigData/PathFinding.cs
- [S2] Lifecycle Rebalance Revisited, game-default travel XML: https://github.com/algernon-A/Lifecycle-Rebalance-Revisited/blob/master/XML_Files/WG_GameDefaults.xml
- [S3] Steam: Transport Preferences: https://steamcommunity.com/app/255710/discussions/0/523890681419543685/
- [S4] Love Cities: Skylines, every type of public transport: https://www.lovecitiesskylines.com/quick-guide-every-type-public-transport/
- [S5] Web-search summary of Steam CS1 discussions (claim: waiting riders board only lines serving their destination); exact thread UNCONFIRMED, candidates: https://steamcommunity.com/app/255710/discussions/0/2972897380393377307 , https://steamcommunity.com/app/255710/discussions/0/1742264681377999587
- [S6] Paradox wiki, Transportation: https://skylines.paradoxwikis.com/Transportation
- [S7] Steam: Percentage of people using your public transport?: https://steamcommunity.com/app/255710/discussions/0/611701999539712811/
- [S8] TM:PE issues on park & ride: https://github.com/CitiesSkylinesMods/TMPE/issues/342 ; Steam TM:PE discussion: https://steamcommunity.com/workshop/filedetails/discussion/1637663252/3810659955912327029
- [S9] Paradox wiki, Milestones: https://skylines.paradoxwikis.com/Milestones
- [S10] Colossal Order, Hubs & Transport dev diary: https://colossalorder.fi/?p=1295
- [S11] Steam: Ideal stop spacing for each transit method: https://steamcommunity.com/app/255710/discussions/0/1699415798774512030/
- [S12] Steam stop-spacing threads (search summary): https://steamcommunity.com/app/255710/discussions/0/3720565944169227048 ; https://steamcommunity.com/app/255710/discussions/0/343786195659345342
- [S13] Cities Skylines Tips, public transport guide: https://citiesskylinestips.com/cities-skylines-public-transport/
- [S14] Steam: how to optimise public transport: https://steamcommunity.com/app/255710/discussions/0/3041607712747992131/
- [S15] Steam: Busses don't space out: https://steamcommunity.com/app/255710/discussions/0/1696043263501578217/
- [S16] Steam: cargo trains stopping at passenger stations / outside train connections: https://steamcommunity.com/app/255710/discussions/0/3131667021981052291/ ; https://steamcommunity.com/app/255710/discussions/0/1488861734124235000/
- [S17] Paradox wiki, Airports DLC: https://skylines.paradoxwikis.com/Airports
- [S18] Steam: Excessive airport & harbour traffic: https://steamcommunity.com/app/255710/discussions/0/2958292387838776124/ ; GameRant airports tips: https://gamerant.com/cities-skylines-airports-tips-guide/
- [S19] Improved Public Transport 2 README (vanilla maintenance formula): https://github.com/roberto-naharro/ImprovedPublicTransport
- [S20] Paradox wiki, Policies: https://skylines.paradoxwikis.com/Policies
- [S21] Steam: Public Transport Usage: https://steamcommunity.com/app/255710/discussions/0/1456202492169300153/
