# "HOW TO TRAFFIC": distilled rules

Source: Steam guide 410236188, a cross-post of Reddit user drushkey's guide (a real-life traffic
engineer), city "Victoria", 75,000 cims, 2015 (base game era, pre Mass Transit / TM:PE).
https://steamcommunity.com/sharedfiles/filedetails/?id=410236188
Where this guide's observations disagree with the decompiled game code in `transit.md`, the
code wins; those spots are marked **[vs code]**.

## Road hierarchy
- Three levels: local (2-lane) → connecting (4- and 6-lane) → regional (highways, or any road
  with no driveways and intersections "hundreds of meters apart").
- Local roads are an extension of the driveway. Most people live on them; they lead nowhere.
- Connecting roads get priority at intersections; you can still live and work on them.
- Cims pick routes by time-to-destination assuming no traffic, so they funnel onto the larger
  roads. That keeps local roads nearly empty. (Consistent with the code: route choice ignores
  live congestion.)
- Signals behave like detectors: green stays with the last direction that had traffic for about
  5 s or until a conflicting vehicle arrives. A big road crossing a quiet local road therefore
  gets most of the green: measured 32 s green for the avenue per 5 s for the local road.
  "Traffic lights only matter when they're red": a dense grid works if the roads people actually
  want are the big ones and the side streets only carry the 1–2 cars per block that live there.
- Alternating one-way local streets help a little; they cost service vehicles detours.
- Do not put intersections too close together; backups spill into the previous junction
  (e.g. between a roundabout and an avenue).
- Roundabouts work and suit highway connections; they cost space.

## Trucks and cargo
- Industry traffic concentrates on a few routes; those are the dangerous ones.
- District policy **Heavy Traffic Ban** is free and 100% effective, but trucks still need a route
  somewhere.
- **Cargo train stations** route goods between each other automatically. A cheap, out-of-the-way
  cargo rail network removes many trucks from important roads.
- Trucks at a cargo station enter on the right and exit on the left (facing its front, right-hand
  traffic). Put it on a one-way road running that way, or feed it with a pair of ramps instead of
  a 90° junction so trucks keep speed.
- A cargo train station next to the cargo harbor makes heavy truck traffic between the two;
  give that pair dedicated ramps.
- When capacity fixes fail, reduce demand: move trip generators (e.g. a cluster of shops) to
  where the network can serve them.

## Public transit
- Every rider is one car fewer: up to 30 per bus, 240 per train (2015 numbers).
- Transit stations count as a service for happiness; high-level offices need transit access.
- **Most trips are A → B → A.** A two-way line (stops on both sides of the street, straight path)
  beats a one-way loop. Measured "trips saved": straight two-way 32%, one-way coverage loop 26%,
  a sprawling line covering the most zones 12%.
- A loop is fine where the road is a loop, if a second line runs the other direction; two
  opposite loops reached 100% trips saved.
- "If it's not immediately obvious where people would use the line to go, it's not going to be
  efficient."
- Hierarchy: trains/metro = regional backbone (fast, straight, stops far apart); buses =
  within districts, feeding stations; walking = the local level.
- An ideal network gathers people toward commercial zones (buses) and pumps them to the main
  workplaces (rail/metro). Main trips: home↔work, home↔shops, home→work→shops→home.
- Keep transfers to 2–3; followed cims did not take more. **[vs code: vanilla route choice has
  no transfer penalty; transfers cost through walking, and waits cause trip abandonment.]**
- Put the rail backbone along the highways: highways were placed where car trips want to go, so
  transit must be at least as direct to the same places.
- Serve big trip generators with high-capacity transit: universities (up to 4,500 students, put
  them on transfer stations), airports (~100 at once) and passenger harbors (~200 at once; they
  release everyone together), the Space Elevator (must be adjacent to rail/metro).
- Train vs metro in-game: train is slightly cheaper to build, more to run, crosses terrain on
  bridges, slightly higher capacity. For inner-city backbone, metro is often the better pick.
- A bus line that runs through a station acts as two feeder lines, one per direction.
- Short lines reduce bunching ("an hour's wait, then six buses"); long lines reduce transfers.
- Special cases:
  - a straight bus line between two consecutive metro stations serves a long linear strip well;
  - a bus terminal next to a rail/metro station, connected by pedestrian paths, lets many lines
    converge without blocking the road (about 2 lines per stop at most);
  - an express bus can act as a regional line where rail cannot reach (e.g. across deep water).

## Pedestrian paths ("the oft-forgotten transit option")
- Paths extend a station's walking catchment almost for free: they are narrow, can be steep
  (cims accept 100 m climbs), and fit between buildings.
- Use them to connect hillside or long-block neighbourhoods to stations instead of adding a bus.
- Check that road rebuilding has not cut pedestrian access from a station to its destination
  (cims will not cross open asphalt without a crossing); a path over the road fixes it.

## Buses cause congestion
- Buses dwell a fixed time at stops and block the lane behind them. Where many lines converge,
  use several adjacent stops: one stop per two lines.

## Problem-solving method
1. Localise: find where the queue starts.
2. Identify the behaviour at that point (e.g. trucks making sharp forced lane changes at a merge).
3. What changed recently? New development usually.
4. Confirm by checking where the vehicles come from and go to.
5. Try fixing the junction; if it cannot be fixed, add capacity on an alternative route.
6. **Wait** for the city to re-equilibrate before judging (vehicles re-path only when they
   spawn or re-route).
7. If capacity cannot solve it, change the demand (move the trip generators).

## Early game
- Decide the growth direction first. Leave room for the future highway; start with an avenue
  connected by one-way ramps; build connector roads early.
- Along the highway: put industry near it; add connecting roads parallel to the highway,
  because few internal trips use the highway itself.

## How this applies under the "add, don't change" rule
Adding is allowed: new lines, new stations and tracks, new pedestrian paths, bus terminals by
existing stations, cargo rail that takes trucks off roads, new parallel connecting roads.
Changing what exists (re-routing a line, moving stops, upgrading or rebuilding roads, the Heavy
Traffic Ban, moving shops) goes into the proposals list for the user instead.
