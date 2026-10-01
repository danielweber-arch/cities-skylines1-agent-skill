# CS1 Fundamentals

Before any build, read docs/api.md for the state reads that answer the question (terrain, prefabs, anomalies). Proven rules in lessons.md override this file.

Verified game mechanics. Treat as true until lessons.md proves otherwise.

## Hard limits
- Active vehicles cap at 16,384. Moving people (citizen instances) cap at 65,536. Transit lines cap at 256.
- Tourists consume citizen units too. A tourism-heavy city can hit limits early.
- Pipes, power lines, fences, and quays count as network nodes and segments. Don't waste them.
- At a limit, things stop spawning and the sim degrades. Watch these numbers.

## Roads
- This repo's prefab mapping: local = `Basic Road` on an 80 m lattice, 16 m road + 2x32 m zone depth (portville-master-plan.md:227); collector = `Medium Road` (portville-master-plan.md:212, 225); major spines and bridges = `Large Road with Tree Median and Bus Lanes` (portville-master-plan.md:229, 702). This is a project choice, not an engine-enforced tier.
- Road/ramp grade: builds in this repo hold to <= 8% even on steep terrain (ramp elevations tuned to stay <= 8% on 11-14% terrain, lessons.md:451); elevated replacements for level crossings target the same cap (lessons.md:714). A ramp needs about 100 m each side at 8% to clear an 8 m rail/road crossing (lessons.md:716).
- Metro and train track: every node turn <= 40 deg; the pathfinder refuses a lane across a turn of about 45.8 deg (lessons.md:6).
- Congestion: density = trafficBuffer*100/(lane length*16), capped at 100; a segment reads 100 at about (lane length / 14) cars present on average, so normalise by lane length before calling a segment jammed (lessons.md:191).
- A highway node that also carries a large/local road gets a junction even when the side road's own density is ~0; don't assume a signal-free crossing there (lessons.md:732).
- Per-road vehicle capacity is NOT measured in this repo; the bridge exposes counts and problems, not capacity, and any lane-capacity number is a vanilla wiki value, not read from a live install (tampa-master-plan.md:160).

## Traffic
- Everything is a real agent on a real path. Traffic is the sum of those paths.
- The AI takes the shortest route even when it's jammed. Give it good alternatives.
- Cars only change lanes at nodes. Junctions too close together trap cars in the wrong lane.
- Leave space between junctions. Backups spill into the previous intersection.
- Highway ramps too close together force weaving. Let traffic exit before new traffic enters.
- Road hierarchy by lane count (highway, arterial, collector, local) is general CS1 knowledge [UNVERIFIED] in this repo; see Roads above for the prefabs actually used here.
- Stuck vehicles despawn after sitting too long. If goods can't arrive, businesses suffer.
- Broken nodes cause constant despawning. Delete and rebuild the node.

## Public transit
- Vanilla route choice has no general transit-preference term and no transfer or waiting-time penalty; transfers cost only through the extra walking they add. The only transit cost multiplier is 0.75x under Free Public Transport (or the "Come One Come All" event), and 1/0.75x under High Ticket Prices (lessons.md:43-47). This supersedes an earlier claim here that transit is weighted as preferred and that fewer transfers win.
- A walking stretch longer than 1,000 m is rejected by pathfinding (lessons.md:46). This, not a preference term, is why transit wins trips beyond walking range: maximise coverage and trunk speed, and use Free Public Transport as the one direct lever.
- Keep observed transfers to 2-3; cims followed in this repo did not take more (traffic-guide.md:55-56).
- Vehicles on a line scale with line length and the per-line budget, not ridership.
- Build a hierarchy: buses and trolleys feed trams, metro, and monorail, which feed trains, ships, and planes.
- Buses work best on short routes with roughly 5 to 10 stops [UNVERIFIED, general CS1 advice].
- Solve the last mile. If riders need a car after the metro, they'll drive the whole way.
- Stops placed too close to a turn can block buses from changing lanes.
- Each direction needs a stop. Put stops on both sides of the street.

## Tourism
- Tourist volume is driven by city attractiveness, which rises with land value and monuments.
- T = floor(100*S/(S+200)), where S = global Attractiveness + average land value. One large monument (e.g. a Plaza of Transference, +1000 Attractiveness) can saturate T near its ceiling; after that, ordinary unique buildings move T by only 0-1 point each, and even all unbuilt uniques together move it only 1-2 points (lessons.md:234-239).
- T raises every outside-connection type alike, so it does not by itself shift the mix (e.g. airplane share); measure the week-to-week baseline swing before crediting any build with an effect (lessons.md:237-239).
- At game start tourists arrive by highway only. Train stations, harbors, and airports add big volume.
- Every arrival hub needs local transit or tourists get stranded.
- Tourism commercial pays more tax but brings noise and crime. Keep it off housing, add police.
- Tourism alone is not very profitable. Balance the budget with taxes and industry.

## Zoning and growth
- Zone blocks are 4 cells (32 m) deep (portville-master-plan.md:227: 80 m lattice = 16 m road + 2 x 32 m zone depth). Thinner zoning spawns smaller buildings [UNVERIFIED].
- Buildings level 1 to 5 [UNVERIFIED, general CS1 knowledge]. Leveling needs land value, education, and service coverage.
- `set-zone` paints every whole zone block whose CENTRE lies inside the given radius, not individual cells (lessons.md:374). On an 80 m lattice a radius of 34 m at a cell centre matched 0 blocks; 40 m painted the cell's 8 blocks (lessons.md:461-462).
- A block is protected from repainting if any growable, service, park, boulder, or pole lies within max(w,l)*4+38 m of it (lessons.md:321).
- Residential pacing used in this repo: stage new growth in <= 150-cell chunks per game week per district to avoid death waves (portville-master-plan.md:389).
- A demand-gated pacer used here: R >= 70, zone every 1 game day; R 40-69, every 3 days; R < 40, hold (transit-progress.md:294).
- High density commercial noise buffer of about 4 cells from housing is general CS1 knowledge [UNVERIFIED] in this repo.
- Offices make a good buffer. No pollution, low traffic.
- Default tax of 9%, with many players running 12% without trouble, is general CS1 knowledge [UNVERIFIED] in this repo. Test changes 1% at a time.

## Pollution, land value, and wind
- An industry-to-housing buffer of about 8 cells (64 m) is general CS1 knowledge [UNVERIFIED] in this repo.
- This repo's own explicit, dated player rule governs instead: no industrial zoning or building, landfill, incinerator/combustion plant, or other polluting site (other than wind/solar) within 200 m of a residential zone or growable, with offices, commercial, or parks as the buffer (portville-master-plan.md:899-901). An earlier pass of the same plan used 160 m before this rule was written (portville-master-plan.md:411); treat 200 m as current since it is the later, explicitly-labeled standing rule.
- Wind direction is NOT exposed by the API. Approximate "downwind" from map geography instead (portville-master-plan.md:411, 609).

## Water and terrain
- The water surface is not stable: a sampled point can swing several metres (3-8 m measured) within a single minute (lessons.md:691; docs/api.md:108-110). Sample several times and never reuse a shore contour from an earlier session.
- A quay built at terrain height sat below the water's measured peaks and had to be removed and rebuilt with `elevation` 5 (lessons.md:686).
- A lattice row built along the top of a shore slope reads `roadTerrainCliff` even with a fine grade (21 pieces flagged, side-to-side delta 24-46 m); keep the outermost row one block back from the slope (lessons.md:400-404).
- The straight line to the nearest road can run through open water; grid-sample terrain first. One case read terrain y ~89 (water) on the direct line between a harbor and the nearest road (lessons.md:733-738).
- Elevation for tracks and roads is relative to terrain, not sea level (startY/endY = terrain + elevation; lessons.md:95). An under-river metro tunnel at elevation -12 worked fine; solve heights from a dry-run terrain read at each point, not an interpolated estimate (lessons.md:656-660, 93-97).
- Harbor and other Shoreline-class buildings require position.y - waterHeight <= 32, and the target point should be within about 50 m of the shore (docs/api.md:688-689, 695-696).
- Tooling added alongside this file: `GET /state/terrain` (also the `cs1_terrain_sample` tool) returns terrainHeight, waterHeight, hasWater, waterDepth, and shoreDistance for up to 64 points per call (docs/api.md:81-108). The bridge refuses to build a ground road, track, or pedestrian path, or place a building footprint, over water, erroring with "Cannot build on water:" (docs/api.md, Water guard). The MCP tools cannot override it; a refusal means re-plan or ask the player. Cross water instead with an elevated/bridge or tunnel prefab plus `elevation` on both points. Water pipes, power lines, quays, canals, flood walls, ship/ferry paths, pedestrian bridges, bridge and tunnel pieces, dams, and endpoints with |elevation| >= 1 m are exempt (below 1 m counts as ground).
- Pipe coverage in practice: a building that stays dry is typically more than about 95-100 m from the nearest pipe; this is an observed radius in this repo, not a documented constant (lessons.md:284, 704).
- A Water facility (tower, pumping station, treatment plant) connects only through its own pipe node (`m_netNode`); probe with a dry-run `connect` (toService Water, roadPrefab Water Pipe, maxDistance 45) before piping, and never release that node (lessons.md:18).
- Never `connect` toService Road from a Roadside building; it drives a stub road through the building's own footprint. Place the building 1-3 m off the road instead (lessons.md:16).

## Water and sewage (pumps and pipes)
- Pumps go upstream. Sewage goes downstream, ideally at a map edge where water flows off.
- Pumps can reverse a current and pull sewage toward themselves, especially in still water.
- A bay is still water. Assume sewage in the bay will reach bay pumps.
- Safer on a bay map: water towers on clean high ground, and inland treatment plants if Sunset Harbor is owned.
- Keep towers away from ground pollution.

## Power
- A small or isolated power grid (e.g. one turbine serving a few houses) can run short and cause newly-grown houses to abandon before it is joined to the main grid (lessons.md:465-468).
- Incinerators and combustion plants are power plants too; removing one without adding surplus generation first can cause a city-wide electricity shortage (lessons.md:647).
- The bridge cannot read power or water/sewage capacity versus consumption, only counts and problem flags (tampa-master-plan.md:158; progress-portville.md:589). Plan with margin and re-check `/state/problems` a few minutes after any change.

## Service coverage
- Trunk (rail/metro) coverage target used in this repo: growables within 800 m of a stop (tampa-master-plan.md:453; portville-master-plan.md:269).
- Bus stop coverage targets vary by plan in this repo: 400 m in one (tampa-master-plan.md:450; transit-progress.md:128), 250 m in another (portville-master-plan.md:269). These are project-chosen planning targets, not a fixed engine walking radius.
- Elementary school: the game's own default `SchoolAI.m_educationRadius` is 500 m (verified in the game assembly, SchoolAI constructor; tampa-master-plan.md:187-188 notes the per-prefab values were not read), and this repo uses 500 m as its coverage target (portville-master-plan.md:532).
- Park: used as a 400 m target from residential/family blocks (portville-master-plan.md:532).
- Hospital, police, and fire coverage radii are NOT measured in this repo. [UNVERIFIED]

## Milestones
- Metro needs two milestones in sequence: "Metro Track Requirements", then "Metro Track Created". The second is only triggered by actually building a Metro Track piece, not by meeting the prerequisite alone (lessons.md:88-90; progress-portville.md:457-466).
- Area/tile purchases are milestone-gated too: `GET /state/areas` reports `nextAreaMilestone` and `nextAreaMilestoneReached` before a purchase is attempted (docs/api.md:898-908).
- Population thresholds tied to specific milestones are NOT measured in this repo; treat any specific number as general recollection, not verified (portville-master-plan.md:253).

## Budget
- `place-building` does not charge or check construction cost; it only reports the would-be `constructionCost` in a validated response (docs/api.md:664-665).
- `unlock-area` deducts the tile price from cash even when the purchase is then refused (e.g. over the area cap); the bridge peeks the balance first for exactly this reason (docs/api.md:935).
- City service budgets (`set-service-budget`) run 0-150% day/night per service; a transit line's own `budget` field runs 0-500% per line. These are two different sliders; do not confuse them (docs/api.md:1033, 1091).
- At least one observed save had cash reading Int64.MaxValue (unlimited money); don't assume money is actually constrained without checking (progress-portville.md:340, 480).

## Population and services
- Every cim has the same fixed lifespan. Mass move-ins cause mass deaths later (death waves).
- Zone residential gradually. Never open huge residential areas all at once.
- Hearses and garbage trucks are vehicles. If traffic blocks them, bodies and garbage pile up and buildings abandon.
- Give service buildings back-street access, not jammed arterials.
- Keep a cash reserve for death waves and tax dips.

## Sources
- This repo: lessons.md, docs/api.md, and the master plans/progress logs (portville-master-plan.md, tampa-master-plan.md, transit-progress.md, progress-portville.md, traffic-guide.md) - cited inline above wherever a number comes from them.
- Steam CS1 discussions: limits, despawning, traffic, transit, death waves (steamcommunity.com/app/255710/discussions)
- Paradox dev diaries: zoning, outside connections, tourism (forum.paradoxplaza.com)
- guidestrats.com: traffic, sewage, tourism guides
