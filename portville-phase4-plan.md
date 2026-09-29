# Portville Phase 4 plan: finish P4 (52k → 55k+), Central Park kept at its size and rebuilt as NYC, set up P5

Revised 2026-09-29 from the player's five in-game screenshots (game date 13/10/2049), `portville-master-plan.md` (§6 P4, §10) and `progress-portville.md`. The bridge was not reachable when this was written. The first builder session re-measures §0 live before acting, and `scripts/phase4-orchestrator.sh` can refresh the whole plan from a live snapshot.

Tags:
- [S] read from the player's screenshots (no ids or coordinates);
- [M] measured earlier in the progress log;
- [E] estimate;
- [U] unverified;
- [P] player directive.

This is the master plan's §6 P4 ("Both banks, one city"), updated with what is already built and the §10.13 "Later (Phase 4)" list. Where this file and §6/§10 disagree, this file wins for Phase 4.

## 0. Where Phase 4 starts

### 0.1 Live state, game 13/10/2049 [S]

- **Population:** 51,907, rising (+79 in the bar). The §6 P4 target of 55k is about 3k away. Phase 4 therefore finishes P4's quality goals and lays the ground for P5, rather than chasing population alone.
- **Money:** the treasury shows **∞** (unlimited money), and the weekly balance reads **−₡267,997**. Money does not block building. The deficit is still a sign of overbuilt or overfunded services, so check it once (workstream J).
- **Districts** (named on the map):
  - **Downtown District**;
  - **Central Park**, just east and northeast of Downtown District;
  - **Sheffield Park**, north, by the rail curve;
  - **Vermont Heights**, south-east. Only its label and policy icons show, with no buildings: it is **named but still empty land**.
- **Transit, residents per week:**

  | Mode | Residents | Tourists |
  |---|---|---|
  | Bus | 2,373 | 190 |
  | Metro | 923 | 153 |
  | Train | 665 | 170 |
  | Ship | **0** | 0 |
  | Air | 32 | 69 |
  | **Total** | **3,993** | **582** |

  Ships carry nobody, and metro still trails bus by 2.6×.
- **Education:** uneducated 16%, educated 23%, well educated 10%, highly educated 51%. Libraries are badly underused: **147 users against capacity 1,100**.
- **Pollution view:** a large ground-pollution area over the NW industrial cluster. A **second polluted spot sits beside the south housing grid**, next to a building with a warning icon. That breaks the "no polluter next to homes" rule (master plan §10.11).
- **Traffic view:** many arterials in the dense grids show red and orange. Treat flow as still below the 75% target until it is measured live (it was 67% on 2045-12-11 [M]).
- **In-game chat panel:** reads **"AI offline – no agent is watching the chat."** The chat loop is not running.
- **Map:** a large open area east and south-east of the city, inside the highway loop, is unbuilt. That is where Vermont Heights and Central Park's expansion can go.

### 0.2 Built earlier [M, progress-portville.md up to 2026-09-28]

- **M1 west:** Central → West Gate → Waterfront → Westbank → Airport site. Lines 30/173 run 8/8 trains over 9,246 m.
- **Harbor bus:** H1, line 102.
- **Westbank Homes grid:** 1,910 high-density cells, served by the WB Downtown bus (line 171).
- **Downtown and the waterfront:** City Hall, SeaWorld, the waterfront promenade.
- **Still open then:**
  - the Airport building (the player places it; the bridge refuses sub-building prefabs);
  - the isolated metro station tracks 19205, 19709, 27014 and 36009;
  - the Holmes / Stephen Harris hotspots (589, 6129, 11388) and the Riverside East approach (15491, 32290);
  - M2, IC Westbank, the NE extension, and the Westbank service set.

The first live read confirms or clears each of these.

## 1. Central Park: player directive [P, 2026-09-29]

> "Make sure phase 4 preserves and expands upon Central Park. Do not put any residential/commercial/industrial zoning in Central Park."

> "Do not change the size of Central Park." [P, 2026-09-29, later: overrides any growth step below]

**Size lock:** Central Park keeps exactly the footprint it has today. Nothing is added to it and nothing is taken from it. "Expands upon" now means richer content inside the park (the NYC replica, §1.4), not more land.

Central Park is a **district** in the live save, painted just east and northeast of Downtown District [S]. The land inside and around it is mostly open, and a few buildings stand near its label. The bridge reads district names but not district paint, so step CP0 below records the boundary before any other Phase 4 command runs.

### 1.1 Hard rules (every command, every builder, every phase from now on)

1. **No zoning inside Central Park.** Inside the recorded boundary every zone cell stays `Unzoned`. No `ResidentialLow/High`, `CommercialLow/High`, `Industrial` or `Office`. Offices are included because they are a job zone, and the rule's intent is "park, not development."
2. **Every zoning path obeys rule 1:**
   - `set-zone`, `build-grid` and `build-neighborhood`;
   - `repair-zone-clusters`, which fills unzoned holes by default: pass it only centers outside the park;
   - `repair-zones-to-growables`;
   - the demand-gated RL stager (`tmp/portville/p2/stager.py`);
   - the in-game chat assistant.

   Any circle a zoning command paints must lie wholly outside the boundary: center-to-boundary distance must exceed the radius.
3. **Nothing inside the park is removed or shrunk:** no demolition of its park buildings, and no service, industrial or transit building on its land. Pedestrian paths, park and plaza prefabs, and trees and decoration are allowed.
   - **Roads, as in NYC:** no surface car road crosses the park. The only car roads inside it are the four **sunken transverse roads** (§1.4). The loop drives are car-free pedestrian paths, as NYC's have been since June 27, 2018. The perimeter avenues run outside the boundary.
4. **Metro may run under the park, not on it.** Tunnels underneath are fine. Surface entrances go on the park's edge roads, outside the boundary.
5. **Pollution buffer (extends §10.11):** no industry, landfill, incinerator or power plant within 300 m of the boundary [E: the same spacing the plan uses for homes].
6. **Before any dry run touches cells inside the boundary, stop.** Report it to the player and wait.

### 1.2 CP0 Locate and fence (first step of Phase 4; reads plus unzoning only)

1. **Find it.** `GET /state/policies` → `districts[]` gives the id of the district named "Central Park". For its position:
   - ask the player in chat to click the district name and send "this is Central Park". The message carries the district's name location (`selected`);
   - or capture around the Downtown District's east edge and find the label.

   Then capture its area at `size` 1200 in modes `None` and `LandValue` to see the painted extent.
2. **Record the boundary** in `city.md` under "Protected areas":
   - a polygon of world (x, z) corners, confirmed by the player in chat;
   - the edge road segment ids;
   - the park buildings inside, with their ids and prefabs.

   The bridge cannot read district paint [U: no district-bounds endpoint], so the polygon comes from edge roads and the player's confirmation.
3. **Audit** with these reads:
   - `GET /state/growables`: count residential, commercial, industrial and office buildings whose position lies inside the polygon. The target is 0.
   - Cover the polygon with `set-zone` circles that stay inside it. For each circle, run `{"dryRun": true, "zone": "Unzoned", "preserveOccupied": false}` and log what it would change [U: check that the dry run reports a changed-cell count].
4. **Fence.** For each circle whose dry run shows zoned cells and no growables, run it for real with the same body.
   - If a growable is inside, do not unzone its block. List it and ask the player, because unzoning under a building abandons it.
   - Blocks on the park's edge roads that face into the park are unzoned the same way.
5. **Save** the `Portville` working save, then check that its mtime is newer.
6. **Accept when:**
   - 0 zoned cells inside the boundary (every audit dry run reports no change);
   - 0 R/C/I/O growables inside, or each remaining one is listed and awaiting the player;
   - the boundary is written in `city.md`.

### 1.3 CP1–CP4 Enrich Central Park inside its boundary (runs alongside the workstreams in §2)

- **CP1 Fill the park.** On empty land inside the boundary:
  - place `Regular Park`, `Regular Plaza` and `Regular Playground`;
  - place `Expensive Park`, `Botanical garden` and `Tropical Garden` where unlocked;
  - add `Official Park` when it unlocks (§10.16).

  Validate every lot first with `validate:true`. Paths end at road edges, lane-linked (the promenade lesson, progress 2026-09-27 21:4x).
  - Accept when every placement is Active with no problems.
- **CP2 Grow outward: WITHDRAWN** by the size lock (§1). The park keeps its current footprint.
- **CP3 Make it a destination.** Park-type uniques go in Central Park as they unlock: `Botanical garden`, `Tropical Garden`, `Official Park`, `Expensive Plaza`.
  - Commercial uniques (`shopping_center`, `department_store`, `hypermarket`, `Posh Mall`) stay outside the boundary.
  - Accept when each placed unique is Active and connected: no RoadNotConnected.
- **CP4 Reach it by transit and on foot.**
  - Put a bus stop, or an existing metro or rail stop, within 300 m of each main park entrance, as edits to existing lines first (the §10.6 rule: no stop within 300 m of two stations on the same trunk).
  - Keep sidewalks continuous from the nearest station.
  - Accept when every entrance is within 300 m of a stop and walkable to it (pedestrian lane links read from `/state/networks`).
- **Edge frontage (outside the park, allowed).** `ResidentialHigh` and `Office` may face the park from across its edge roads, within the zoning rules in §5 of the master plan. That raises land value next to the park without putting any zoning in it.

### 1.4 Central Park as a replica of New York's Central Park [P, 2026-09-29]

> "Model Central Park after Central Park in NYC. Make it a very similar replica."

The NYC layout is fitted **inside Central Park's current boundary**. The size lock in §1 applies: no land is added or removed. CP1, CP3 and CP4 still apply, CP2 is withdrawn, and the §1.1 hard rules still hold.

**The real park** [W, sources at the end of this section]:
- **Size:** 843 acres (about 3.4 km²), about 2.5 mi long (≈ 4.0 km) and 0.5 mi wide (≈ 0.8 km), so roughly **5 : 1**.
- **Edges:** 59th Street (south) to 110th Street (north), and Central Park West (west) to Fifth Avenue (east).
- **Transverse roads:** four sunken roads at **65th, 79th, 86th and 97th** carry cross-town traffic below the park's surface.
- **Drives:** East, West, Center and Terrace Drives have been car-free since June 27, 2018.

**Fitting it to the existing park (decided at CP0, from the live boundary):**
- **Axis:** the park's longest dimension is the NYC north–south axis. The end nearest Downtown District is 59th Street, the way Midtown sits against the real park. Record the south-west corner S, the unit vectors u (along the length, away from downtown) and v (across it, "west" → "east"), the length L and the width W, all in `city.md`.
- **Scale:** each axis is scaled on its own to fill the existing footprint: k_u = L / 4,020 m along the length and k_v = W / 800 m across it. If the park is not 5 : 1, the layout stretches or squeezes to fit it; **the boundary never moves**.
- **Irregular boundary:** if the painted district is not a rectangle, use the largest rectangle inside it for the table below. The corners outside that rectangle become extra lawn or woodland.
- **Street → position:** Manhattan streets run about 20 per mile, so one street ≈ 80 m. A feature at street n, at a fraction f of the width from the west edge (0 = CPW, 1 = Fifth Ave), goes at

  **P = S + u · (n − 59) · 80 · k_u + v · f · W**

  with P in game metres. Check every lot with a `validate:true` dry run before placing it.
- **Small park:** if L comes out under about 600 m, features closer together than 60 m merge. Keep, in this order: the loop path, Bethesda and the Lake, the Mall, the Great Lawn, the Reservoir, Belvedere. Drop the rest before crowding them.
- **Transverses:** only if there is room for them *under* the park (a tunnel prefab). None may widen or cut the footprint.

**Layout, south to north** (the in-game stand-ins are the vanilla prefabs this repo has already used; check each against `/prefabs/buildings` live):

| NYC feature | Street, side | Stand-in | Notes |
|---|---|---|---|
| Columbus Circle (SW corner), Grand Army Plaza (SE corner) | 59th, W / E | Roundabouts on the perimeter roads | Outside the boundary; they help traffic too |
| The Pond, Central Park Zoo, Wollman Rink | 59th–65th, E [U] | Pond: see water below. Zoo: `Expensive Park`. Rink: a sports prefab if unlocked [U] | |
| **65th St transverse** | 65th, full width [W] | Sunken road (tunnel prefab) | Car road, below grade |
| Sheep Meadow | ~66th–69th, W [W: west side] | Open lawn: leave the land empty and unzoned, with a path ring | |
| **The Mall / Literary Walk** | 66th–72nd, E [W] | Straight `Pedestrian Pavement` promenade, tree-lined (trees by the player) | |
| **Bethesda Terrace and Fountain** | 72nd, mid-E [W] | `Regular Plaza` + a fountain prefab (fountain lots were validated before [M]) | Opens onto the Lake |
| **The Lake**, **Bow Bridge** | ~72nd–78th, mid [W: between the 66th and 79th transverses; Bow Bridge at 74th] | Water (see below); Bow Bridge = `Pedestrian Elevated` over it | |
| Strawberry Fields | 72nd, W [W] | `Regular Park` (memorial garden) | |
| Conservatory Water | 74th, E [W] | Fountain or `Regular Plaza` (model-boat pond) | |
| **The Ramble** | ~73rd–79th, mid [W: north of the Lake] | Woodland, planted by the player with the tree tool; gravel paths | |
| **79th St transverse** | 79th [W] | Sunken road | |
| **Belvedere Castle** on Vista Rock | ~79th, mid [W: official weather station since 1919] | `Observatory` unique when unlocked (§10.16), on the highest point; `Regular Plaza` placeholder until then | |
| Turtle Pond, Delacorte Theater | ~79th–80th, mid [U] | Small water; `Theatre` unique when unlocked | |
| **Great Lawn** | ~80th–85th, mid [W: 55 acres] | Open lawn (empty, unzoned); `Regular Playground`s at the edges as ballfields | |
| Metropolitan Museum of Art | ~80th–84th, E edge [U] | `Modern Art Museum` unique | On the east edge, facing the Fifth Ave stand-in |
| **86th St transverse** | 86th [W] | Sunken road | |
| **The Reservoir** + running track | ~86th–96th, full width [W: 106 acres] | Water (see below), ringed by a `Pedestrian Gravel` or `Pedestrian Pavement` loop | The largest single feature |
| **97th St transverse** | 97th [W] | Sunken road | |
| North Meadow | ~97th–102nd, mid [U] | Open lawn + playgrounds | |
| North Woods | ~101st–110th, W [U] | Woodland (tree tool) | |
| **Conservatory Garden** | ~104th–106th, E [W: NE corner, 6 acres, the only formal garden] | `Botanical garden` | |
| Harlem Meer | ~106th–110th, E [W: NE corner] | Water (see below) | |
| Frederick Douglass Circle (NW corner) | 110th, W [U] | Roundabout on the perimeter | Outside the boundary |

**Loop drives (car-free):** one path loop runs just inside the boundary, like East and West Drives, as `Pedestrian Pavement`. Paths end at road edges and are lane-linked (the promenade lesson). The transverse roads pass *under* the path loop.

**Perimeter, outside the boundary (existing edge roads first):**
- Use the park's current edge roads as the avenue stand-ins. Upgrade or add a road only on land outside the park; never shift the boundary to make room.
- A Fifth Ave stand-in on the east edge and a Central Park West stand-in on the west edge: `Medium Road` or larger, per the road hierarchy. South and north edge roads stand in for 59th and 110th.
- Frontage across those roads: `ResidentialHigh` and `Office` only, which is allowed and raises land value. Never inside the park.

**Transit (CP4, NYC-style):** a metro line under the west edge road, with stations at the SW corner (Columbus Circle), near 81st (west, by the museum side), near 96th, and at 110th, plus a bus on the Fifth Ave stand-in. Every entrance sits outside the boundary.

**What the bridge cannot do (the player's part)** [U until tried]:
1. **Water.** The Lake, the Reservoir, the Pond, Turtle Pond and Harlem Meer need terraforming. The bridge has no terrain or water commands. The player digs them with the in-game landscaping tool; a water body needs a water source. Until then, mark each outline with a path ring and leave the ground as lawn.
2. **Trees.** The Ramble and the North Woods need the player's tree brush.
3. **Sunken transverses.** If `/prefabs/roads` lists no tunnel or sunken road the bridge can build, leave the transverses out for now. Never build them at ground level through the park.
4. **Sub-building uniques.** Some uniques refuse bridge placement, as the Airport did. The player places those.

**Build order:**
1. Pin S, u, v, L, W, k_u and k_v at CP0 (`city.md`). Record the park's area from the boundary polygon; it is the size to hold.
2. Build the perimeter roads with their corner roundabouts, then the path loop.
3. Mark the lawns: unzone them and leave them empty.
4. Place the Mall, Bethesda, Strawberry Fields and Conservatory Water / Garden.
5. The transverses, if possible.
6. The water outlines, for the player to dig, and the tree areas, for the player to plant.
7. Landmark uniques as they unlock.
8. Transit last.

Save after each step. Capture the park (`size` = 1.2 × its length, mode `None`) after steps 2, 4 and 7, and compare against a Central Park map.

**Accept when:**
- the park's boundary and area are unchanged from CP0;
- it has 0 zoned cells;
- the loop path is one pedestrian component;
- every table row is either built or listed as player work in `TODOS.md`;
- every perimeter station is within 300 m of a park entrance.

**Sources:**
- size, bounds and dimensions: [centralpark.com](https://www.centralpark.com/visitor-info/where-is-central-park/), [Britannica](https://www.britannica.com/place/Central-Park-New-York-City);
- Reservoir and Great Lawn: [NYC Parks](https://www.nycgovparks.org/parks/central-park/highlights/6455);
- Belvedere weather station: [Wikipedia: Belvedere Castle](https://en.wikipedia.org/wiki/Belvedere_Castle);
- Conservatory Garden: [Wikipedia: Conservatory Garden](https://en.wikipedia.org/wiki/Conservatory_Garden);
- feature streets: [centralpark.org map](https://centralpark.org/central-park-map/), [Wikipedia: The Ramble and Lake](https://en.wikipedia.org/wiki/The_Ramble_and_Lake);
- transverse roads: [michaelminn.net](https://michaelminn.net/newyork/parks/central-park/bridges/transverse-roads/index.html);
- car-free drives: [Central Park Conservancy](https://www.centralparknyc.org/press/car-free-park), [NYC Mayor's Office](https://www.nyc.gov/office-of-the-mayor/news/206-18/mayor-de-blasio-central-park-world-s-most-iconic-greenspace-will-become-permanently).

Tags: [W] means confirmed in those sources; [U] means from general knowledge, not yet checked.

## 2. Phase 4 workstreams (in order; each ends with a save and an mtime check)

Every workstream first checks its footprint against the Central Park boundary and buffer (§1.1). If a route, lot or zoning circle would cross them, it is rerouted, not the park.

| # | Workstream | Source | Accept when |
|---|---|---|---|
| A | **Central Park CP0**, then CP1/CP4 | §1 above | §1.2 acceptance |
| B | **Traffic to ≥ 75% flow.** Fix the Holmes/Stephen Harris corridor (589, 6129, 11388) and the Riverside East approach (15491, 32290). Try a locally validated access fix first; for the shop blocking the Riverside East widening, ask the player first. | progress 2026-09-28, §10.17 | Flow ≥ 75% over 2 in-game weeks; no new problems on the touched segments |
| C | **Airport.** The player places `Airport` at the M1 airport site, near Metro Entrance 21346 at (−2228,−330), with the in-game tool. The builder then connects its road and checks the line stop lists. | §6 P4, progress 22:3x | Airport Active; Airport → Promenade ≤ 1 transfer |
| D | **Isolated station tracks** 19205, 19709, 27014 and 36009: connect each to M1 or M2 per §10.2–§10.4, or remove any with no planned line. | progress 21:28 | Every station track belongs to a Complete line, or is removed |
| E | **Westbank services and access.** Build the §4 P4 service set in Westbank: 4 Elementary, `highschool_EU` at Westbank Centre, `hospital_EU`, fire and police at about one per 0.5 km², and 6 parks. Then IC Westbank (Crowley ~(−2300,−880)). No industry in Westbank; the Crowley strip gets offices, commercial or parks only. | §4, §6 P4, §10.10 #7, §10.11 | Elementary coverage 100% of Westbank homes; no Westbank service problems; Westbank has a second road link besides the couplet |
| F | **M2**, North Gate → Central-M2 (CM2 on Station Road) → Riverside East, in §10.9 step 6 order: the L9 crossing pieces first, then watch M1 for a game day. | §10.3, §10.9 | M2 Complete, riders > 0; M1 shows no problems through the day |
| G | **NE tile.** Buy the tile (`/commands/unlock-area`, §9), then the North station on the mainline, then roads before any station (§10.13), then the second Culture Quarter (§3.8 NE extension). | §3.2, §3.8, §9 | North station on a Complete train line; the NE roads have 0 disconnected components |
| H | **Bus restructure** per §10.6, each change as its trunk opens (M2, NE); measure 4 periods before and after (the m1fix method). | §10.6 | No bus with stops within 300 m of two stations on the same trunk |
| J | **Budget sanity** [S: −₡267,997/week]. Read `/state/economy`, find the three largest expense lines, trim service budgets over 100% where coverage is already full, and check tax rates. Money is unlimited, so this is housekeeping. | §0.1 | Weekly balance improves; no service coverage lost |
| K | **Chat online** [S: "AI offline"]. Run `./scripts/install-chat-bridge-agent.sh install` on the Mac. | §0.1 | Panel shows the agent listening; a test message gets a reply |
| L | **Pollution beside south homes** [S]. Find the polluter by the south grid (`/state/facilities` near the polluted spot, likely a landfill or an industrial lot) and move or replace it (§10.11). Also check the NW industrial pollution against the nearest homes. | §10.11 | No home inside a pollution area; no pollution problem icons on homes |
| M | **Ships at 0 riders** [S]. Read `/state/transit` for passenger ship lines. If none exists, a 0 is expected; the Harbor then relies on H1 and M1. If one exists, fix its route or stops when it is broken, or delete it when it duplicates M1/H1. | §0.1 | Every remaining line has riders > 0 |
| N | **Libraries and education** [S: 147/1,100]. Libraries are placed where few people reach them: put `Library 01` at stations with the highest ridership (§4) instead of adding capacity. | §4 | Library users rise; highly educated share ≥ 51% |
| O | **Vermont Heights, P5 groundwork** [S: named, empty]. Plan its streets and transit before zoning (§10.13): a spine road from the nearest arterial, water, power, one bus feeder to the nearest metro or train stop. Mixed RH/RL/C by the zoning rules. It must not touch Central Park or its 300 m pollution buffer. | §5, §6 P5 | Roads with 0 disconnected components; pipes and power in; the first cells zoned only after the feeder runs |
| I | **Utilities check.** Add a 2nd `Solar Power Plant` or `Advanced Wind Turbine`s only if electricity use is above 70%; `Fusion Power Plant` replaces Nuclear when unlocked (player). | §4 P4 | No Electricity or Water problems |

Order:
1. K (so the chat works while the rest runs), then A (Central Park CP0).
2. L and B.
3. C (waits on the player), D and M.
4. E, then F.
5. N and J in quiet periods.
6. O (Vermont Heights) and G (the NE tile) last, as P5 groundwork. O comes first because its land is already owned and empty.
7. H and I as their triggers occur.

CP1–CP4 run alongside, in quiet periods between the bigger builds.

## 3. Phase 4 acceptance (adds to §6 P4)

- Population ≥ 55,000 (51,907 on 13/10/2049 [S]); 0 disconnected local road components.
- No home inside a pollution area; every transit line has riders > 0.
- Traffic flow ≥ 75%.
- Airport and Harbor each ≤ 1 transfer to the Promenade (line stop lists); modal share ≥ 10%.
- **Central Park:**
  - 0 zoned cells and 0 R/C/I/O growables inside the recorded boundary;
  - boundary and area unchanged from CP0 (size lock);
  - every entrance within 300 m of a transit stop;
  - no polluter within 300 m.
- Save `Portville` overwritten, and its mtime newer than the last command.

## 4. Risks and unknowns

1. **No district-bounds read.** The Central Park boundary relies on the player's confirmation and on edge roads [U]. A `/state/districts` bounds endpoint would make the audit automatic; it is listed in TODOS as a candidate.
2. **`set-zone` circles.** Near a curved or irregular edge, the circles inside the park may not cover every cell. Finish those cells with smaller circles, or set them by hand in game.
3. **Automatic zoning.** The RL stager and `repair-zone-clusters` (`fillUnzoned` defaults to true) are the likeliest ways zoning creeps into the park. Give both an exclusion for the park polygon before they run again.
4. **Traffic fixes vs the park.** Widening a road on the park's edge takes park land. Park edge roads keep their current width unless the player agrees.
5. **Carried over:** §10.10 (the under-river tunnel is already built and working [M]; the M2 depth crossing is untested; airport and harbor sub-buildings).
