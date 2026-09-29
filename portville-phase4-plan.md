# Portville Phase 4 plan: finish P4 (52k → 55k+), Central Park kept and grown, set up P5

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
3. **Nothing inside the park is removed or shrunk:** no roads through it, no demolition of its park buildings, and no service, industrial or transit building on its land. Pedestrian paths, park and plaza prefabs, and trees and decoration are allowed.
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

### 1.3 CP1–CP4 Expand Central Park (runs alongside the workstreams in §2)

- **CP1 Fill the park.** On empty land inside the boundary:
  - place `Regular Park`, `Regular Plaza` and `Regular Playground`;
  - place `Expensive Park`, `Botanical garden` and `Tropical Garden` where unlocked;
  - add `Official Park` when it unlocks (§10.16).

  Validate every lot first with `validate:true`. Paths end at road edges, lane-linked (the promenade lesson, progress 2026-09-27 21:4x).
  - Accept when every placement is Active with no problems.
- **CP2 Grow outward.** Survey a 1-block ring (80 m) around the boundary with `/state/growables` and `/state/zones`, and add to the park the lots that are unzoned or empty.
  - Proposed target: at least one 80 m block on each side that has free land [E; set after CP0 measures the park].
  - Occupied homes or shops are taken only with the player's OK in chat, as in the City Hall precedent.
  - Update the boundary polygon in `city.md` after each addition.
  - Accept when the park area has grown, the new boundary is recorded, and the CP0 audit passes on the new boundary.
- **CP3 Make it a destination.** Park-type uniques go in Central Park as they unlock: `Botanical garden`, `Tropical Garden`, `Official Park`, `Expensive Plaza`.
  - Commercial uniques (`shopping_center`, `department_store`, `hypermarket`, `Posh Mall`) stay outside the boundary.
  - Accept when each placed unique is Active and connected: no RoadNotConnected.
- **CP4 Reach it by transit and on foot.**
  - Put a bus stop, or an existing metro or rail stop, within 300 m of each main park entrance, as edits to existing lines first (the §10.6 rule: no stop within 300 m of two stations on the same trunk).
  - Keep sidewalks continuous from the nearest station.
  - Accept when every entrance is within 300 m of a stop and walkable to it (pedestrian lane links read from `/state/networks`).
- **Edge frontage (outside the park, allowed).** `ResidentialHigh` and `Office` may face the park from across its edge roads, within the zoning rules in §5 of the master plan. That raises land value next to the park without putting any zoning in it.

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
  - area larger than at CP0;
  - every entrance within 300 m of a transit stop;
  - no polluter within 300 m.
- Save `Portville` overwritten, and its mtime newer than the last command.

## 4. Risks and unknowns

1. **No district-bounds read.** The Central Park boundary relies on the player's confirmation and on edge roads [U]. A `/state/districts` bounds endpoint would make the audit automatic; it is listed in TODOS as a candidate.
2. **`set-zone` circles.** Near a curved or irregular edge, the circles inside the park may not cover every cell. Finish those cells with smaller circles, or set them by hand in game.
3. **Automatic zoning.** The RL stager and `repair-zone-clusters` (`fillUnzoned` defaults to true) are the likeliest ways zoning creeps into the park. Give both an exclusion for the park polygon before they run again.
4. **Traffic fixes vs the park.** Widening a road on the park's edge takes park land. Park edge roads keep their current width unless the player agrees.
5. **Carried over:** §10.10 (the under-river tunnel is already built and working [M]; the M2 depth crossing is untested; airport and harbor sub-buildings).
