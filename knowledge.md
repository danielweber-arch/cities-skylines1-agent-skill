# CS1 Fundamentals

Verified game mechanics. Treat as true until lessons.md proves otherwise.

## Hard limits
- Active vehicles cap at 16,384. Moving people (citizen instances) cap at 65,536. Transit lines cap at 256.
- Tourists consume citizen units too. A tourism-heavy city can hit limits early.
- Pipes, power lines, fences, and quays count as network nodes and segments. Don't waste them.
- At a limit, things stop spawning and the sim degrades. Watch these numbers.

## Traffic
- Everything is a real agent on a real path. Traffic is the sum of those paths.
- The AI takes the shortest route even when it's jammed. Give it good alternatives.
- Cars only change lanes at nodes. Junctions too close together trap cars in the wrong lane.
- Leave space between junctions. Backups spill into the previous intersection.
- Highway ramps too close together force weaving. Let traffic exit before new traffic enters.
- Intersections of two small 2-lane roads get no traffic lights. Larger roads do.
- Road hierarchy: highway, arterial (6-lane), collector (4-lane), local (2-lane).
- Stuck vehicles despawn after sitting too long. If goods can't arrive, businesses suffer.
- Broken nodes cause constant despawning. Delete and rebuild the node.

## Public transit
- Transit is weighted as preferred over private cars. Free transit boosts it further.
- Cims pick routes by travel time, distance, and number of transfers. Fewer transfers win.
- Vehicles on a line scale with line length and the per-line budget, not ridership.
- Build a hierarchy: buses and trolleys feed trams, metro, and monorail, which feed trains, ships, and planes.
- Buses work best on short routes with roughly 5 to 10 stops.
- Solve the last mile. If riders need a car after the metro, they'll drive the whole way.
- Stops placed too close to a turn can block buses from changing lanes.
- Each direction needs a stop. Put stops on both sides of the street.

## Tourism
- Tourist volume is driven by city attractiveness, which rises with land value and monuments.
- At game start tourists arrive by highway only. Train stations, harbors, and airports add big volume.
- Every arrival hub needs local transit or tourists get stranded.
- Tourism commercial pays more tax but brings noise and crime. Keep it off housing, add police.
- Tourism alone is not very profitable. Balance the budget with taxes and industry.

## Zoning and growth
- Zone blocks are 4 cells deep. Thinner zoning spawns smaller buildings.
- Buildings level 1 to 5. Leveling needs land value, education, and service coverage.
- High density commercial is noisy: keep about 4 cells from housing. Industry: about 8 cells.
- Offices make a good buffer. No pollution, low traffic.
- Default tax is 9%. Many players run 12% without trouble. Test changes 1% at a time.

## Water and sewage
- Pumps go upstream. Sewage goes downstream, ideally at a map edge where water flows off.
- Pumps can reverse a current and pull sewage toward themselves, especially in still water.
- A bay is still water. Assume sewage in the bay will reach bay pumps.
- Safer on a bay map: water towers on clean high ground, and inland treatment plants if Sunset Harbor is owned.
- Keep towers away from ground pollution.

## Population and services
- Every cim has the same fixed lifespan. Mass move-ins cause mass deaths later (death waves).
- Zone residential gradually. Never open huge residential areas all at once.
- Hearses and garbage trucks are vehicles. If traffic blocks them, bodies and garbage pile up and buildings abandon.
- Give service buildings back-street access, not jammed arterials.
- Keep a cash reserve for death waves and tax dips.

## Sources
- Steam CS1 discussions: limits, despawning, traffic, transit, death waves (steamcommunity.com/app/255710/discussions)
- Paradox dev diaries: zoning, outside connections, tourism (forum.paradoxplaza.com)
- guidestrats.com: traffic, sewage, tourism guides
