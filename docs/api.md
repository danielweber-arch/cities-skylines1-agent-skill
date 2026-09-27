# API Reference

[日本語版](ja/api.md)

Base URL:

```text
http://127.0.0.1:32123
```

While a city is loaded, every API request that touches game state also appears
in the CS1 UI as a short overlay notification. The overlay keeps the latest few
messages for several seconds, for example `API OK: Read city problems` or
`API OK: Build network Basic Road`. `/health` is intentionally excluded because
it can be called before a level exists.

![API notification overlay](assets/api-notification.jpg)

## Agent Workflow

The intended workflow is intentionally generic:

1. Read state from the API.
2. Decide which small change is needed.
3. Call one command API.
4. Let the simulation settle.
5. Re-read state.
6. Save and verify the save file.

![Agent-built starter city](assets/city-overview.jpg)

## GET /health

Returns bridge status without requiring a loaded city.

## GET /state/summary

Returns a small city snapshot: game time, build index, network counts, citizen count, and demand values.

## GET /state/demand

Returns the three demand bars shown in the CS1 UI: residential, commercial,
and workplace demand. Values are `0..100`.

```bash
curl -sS http://127.0.0.1:32123/state/demand
```

## GET /state/chirps

Returns recent Chirper/citizen messages from CS1's message manager, including
sender name, sender id, text, message type, and message metadata when available.
This is useful for reading citizen feedback such as housing demand, tax,
traffic, service, and city satisfaction comments without OCR.

```bash
curl -sS "http://127.0.0.1:32123/state/chirps?limit=50"
```

## GET /state/zones

Returns zoning cell counts and approximate area by zone type. CS1 zoning cells
are reported as 8m x 8m cells, so `areaSquareMeters` is approximate but useful
for comparing residential, commercial, industrial, office, and unzoned area.

```bash
curl -sS http://127.0.0.1:32123/state/zones
```

## GET /state/growables

Returns existing growable residential, commercial, industrial, and office
buildings with service, sub-service, footprint size, position, active/abandoned
state, and problem flags. Use this before zoning to avoid painting over already
developed blocks.

```bash
curl -sS "http://127.0.0.1:32123/state/growables?limit=500"
```

## GET /prefabs/roads

Returns loaded `NetInfo` prefabs that look like roads.

## GET /prefabs/networks

Returns loaded network prefabs. Optional service filter:

```http
GET /prefabs/networks?service=Water
```

## GET /prefabs/buildings

Returns loaded building prefabs. Optional service filter:

```http
GET /prefabs/buildings?service=Electricity
```

Known broken/blocked building assets are omitted from this list. The current
blocked family is `Block Services - ...`.

## GET /state/problems

Returns in-game notification/problem icons from CS1 data, without using screenshots or computer vision.

```http
GET /state/problems?limit=200
```

The response includes both the legacy combined `problems` string and
structured `problemNames`, `problem1Raw`, `problem2Raw`, and
`countsByProblem` fields so agents can match individual alerts such as
`TaxesTooHigh` even when CS1 marks the same building as major or fatal.
Building rows also surface alert-like flags such as `Abandoned`, `BurnedDown`,
`Collapsed`, `Flooded`, and `RoadAccessFailed`.

Scanned entity types:

- `building`
- `netNode`
- `netSegment`

## GET /state/economy

Returns the currently configured tax rates for zoned residential, commercial,
industrial, and office sub-services across levels. `aggregateTaxRates` mirrors
the six tax sliders shown in the CS1 budget UI.

```bash
curl -sS http://127.0.0.1:32123/state/economy
```

## GET /state/facilities

Returns current buildings grouped by CS1 service, with optional service
filtering. This is the API-friendly replacement for reading service icons from
the screen. Facility items include prefab footprint and rotation so an agent can
avoid placing large buildings through roads.

By default this excludes internal pipe helper buildings such as `Water Pipe
Junction` and `Heating Pipe Junction`; pass `includeMapObjects=true` when an
agent specifically needs raw map objects.

```bash
curl -sS "http://127.0.0.1:32123/state/facilities?limit=500"
curl -sS "http://127.0.0.1:32123/state/facilities?service=HealthCare"
curl -sS "http://127.0.0.1:32123/state/facilities?service=PoliceDepartment"
curl -sS "http://127.0.0.1:32123/state/facilities?includeMapObjects=true"
```

The response includes:

- `countsByService`
- `countsBySubService`
- `facilities[]` with `id`, `prefab`, `displayName`, `service`, `subService`, `level`, `width`, `length`, `angleDegrees`, `problems`, and `position`

Response shape:

```json
{
  "ok": true,
  "total": 17,
  "returned": 17,
  "countsByService": {
    "Water": 14,
    "HealthCare": 3
  },
  "facilities": [
    {
      "id": 123,
      "prefab": "Inland Water Treatment Plant 01",
      "service": "Water",
      "width": 5,
      "length": 7,
      "angleDegrees": 180,
      "problems": "",
      "position": { "x": 10, "y": 0, "z": 20 }
    }
  ]
}
```

## GET /state/networks

Returns current network segments from CS1 data, without screenshots. Optional
service filtering is useful for checking roads, water pipes, heating pipes, and
power lines separately.

```bash
curl -sS "http://127.0.0.1:32123/state/networks?service=Road"
curl -sS "http://127.0.0.1:32123/state/networks?service=Water"
curl -sS "http://127.0.0.1:32123/state/networks?limit=1000"
```

Each segment includes `id`, `prefab`, `service`, `subService`, `problems`,
`name`, `startNodeId`, `endNodeId`, `start`, `end`, and `middle`.

## GET /state/road-anomalies

Detects road geometry that can look connected on screen but is not actually a
proper CS1 road graph connection.

```bash
curl -sS "http://127.0.0.1:32123/state/road-anomalies?nearMissDistance=18&shortSegmentLength=32&includeDeadEnds=true"
```

Detected anomaly types:

- `deadEndNearRoad`: a one-segment road endpoint is very close to another road segment, which often means the endpoint visually touches a road but did not create an intersection.
- `deadEndRoad`: a normal road dead end. This is legal in CS1, but useful for agent-side design QA because unwanted frontage/service-road stubs often look like this.
- `shortRoadStub`: a short road segment with a dead end, often left behind by failed frontage-road or service-road placement.
- `duplicateRoadSegments`: two road segments share the same pair of endpoint nodes, which usually means one should be removed.
- `overlappingRoadSegments`: two road segments run nearly on top of each other at the same height for a meaningful distance, which usually means a duplicate or accidental overlay.
- `roadCrossingWithoutNode`: two road segments cross at nearly the same height without sharing a node, which usually means they visually overlap but are not a real intersection.
- `roadTerrainCliff`: a ground road has a large height mismatch against nearby sampled terrain, which can indicate buried roads or terrain spikes/cliffs caused by bad road placement.
- `roadBelowLocalGrade`: an agent-built ground road sits far below the surrounding local road grade, which often means a sunken or buried road.

Each anomaly includes the affected node or segment IDs plus world coordinates,
so an agent can call `/commands/bulldoze` or add a connector road without using
image recognition.

Inspect, then repair within a bounded area:

```bash
curl -sS "http://127.0.0.1:32123/state/road-anomalies?limit=500&includeDeadEnds=false"

# Bulldoze a specific offender, then rebuild the connection properly.
curl -sS -X POST http://127.0.0.1:32123/commands/bulldoze \
  -H "Content-Type: application/json" \
  -d '{"entityType":"netSegment","id":21778,"keepNodes":false}'

curl -sS -X POST http://127.0.0.1:32123/commands/connect \
  -H "Content-Type: application/json" \
  -d '{"from":{"x":512,"z":-88},"toService":"Road","maxDistance":200}'
```

The bash scripts in `scripts/` wrap these endpoints (`inspect-road-anomalies.sh` prints
repair hints, `repair-road-anomalies.sh` bulldozes stubs in a bounding box); the `.ps1` files
are the legacy Windows originals.

## GET /state/external-connections

Checks whether the city's local road component is connected to CS1 outside road
nodes. This is useful when a city visually has highways nearby but no outside
cars enter because the local road graph is still separate from the highway
network.

```bash
curl -sS "http://127.0.0.1:32123/state/external-connections?limit=50"
```

The response includes `cityConnectedToOutside`,
`disconnectedLocalRoadComponents`, outside node counts, and sampled road
components.

## GET /state/building-anomalies

Detects service buildings whose footprint intersects a road segment. This is
for API-side QA when a building appears to be placed through a road, without
using screenshots.

```bash
curl -sS "http://127.0.0.1:32123/state/building-anomalies?limit=200"
```

## GET /state/zone-anomalies

Detects mottled zoning from CS1 zone blocks without using screenshots. This is
useful when circular or overlapping zone paint leaves residential, commercial,
industrial, office, and unzoned cells mixed inside the same block.

```bash
curl -sS "http://127.0.0.1:32123/state/zone-anomalies?limit=200&includeUnzonedHoles=true"
```

Detected anomaly types:

- `mixedZoneBlock`: one zoning block contains multiple non-empty zone types,
  such as residential cells mixed with commercial or industrial cells.
- `patchyUnzonedHoles`: one zoning block is mostly one zone type but contains
  many unzoned cells, which often means an agent left visible holes after
  repainting.

## POST /commands/build-network

Generic network creation. Use this for roads, water pipes, heating pipes, and
power lines. `roadPrefab` is kept as the request field name for compatibility
with the early bridge prototype; pass any loaded `NetInfo` prefab name.

Request:

```json
{
  "dryRun": true,
  "roadPrefab": "Basic Road",
  "start": { "x": 0, "z": 0 },
  "end": { "x": 80, "z": 0 },
  "name": "Agent Test Road"
}
```

Response:

```json
{
  "ok": true,
  "dryRun": true,
  "message": "Build-road validation passed."
}
```

When `dryRun` is `false`, the mod creates two nodes and one segment with `NetManager`.

Each point may carry an optional `elevation` in metres above the terrain (negative puts it below
the terrain), for example `"start": {"x": 0, "z": 0, "elevation": 12}`. Use it with elevated or
tunnel prefabs such as `Train Track Elevated`. New nodes store the height in `NetNode.m_elevation`.
Snapping onto an existing node ignores height, and no pillars are placed. `dryRun` reports the
resulting `startY` and `endY`.

## POST /commands/build-road

Compatibility alias for `/commands/build-network`.

## POST /commands/build-grid

Builds a whole rectangular road lattice: `(cols+1) x (rows+1)` nodes, then every segment
between them. Nodes are created first and segments second — interleaving them lets a segment
be created against a node a later snap would have merged away.

Endpoints within `snapDistance` (default 8m) of an existing node reuse it, so the grid joins
the existing network as real intersections rather than crossings.

```bash
curl -sS -X POST http://127.0.0.1:32123/commands/build-grid \
  -H "Content-Type: application/json" \
  -d '{"roadPrefab":"Basic Road","origin":{"x":200,"z":-300},
       "cols":6,"rows":4,"spacing":80,"rotationDegrees":0,"opId":"grid-downtown-01"}'
```

| field | default | notes |
|---|---|---|
| `roadPrefab` | `Basic Road` | must match a name from `/prefabs/roads` |
| `origin` | `{0,0}` | lattice corner; cells grow toward +x and +z |
| `cols`, `rows` | 4 | `cols * rows` must be <= 400 |
| `spacing` | 80 | metres, 32..256 |
| `rotationDegrees` | 0 | rotates the lattice about `origin` |
| `snapDistance` | 8 | node reuse radius, 0..64 |
| `opId` | none | idempotency key — a retry replays instead of rebuilding |
| `dryRun` | false | returns the plan and `blockCenters` without building |

Returns `nodeIds`, `createdNodeIds`, `reusedNodes`, `segmentIds`, `skippedSegments`, `bbox`,
and `blockCenters`. `blockCenters` is the payoff: the coordinate of every city block, ready to
pass straight to `/commands/set-zone`.

**Why 80m.** CS1 zoning cells are 8m and zoneable depth is 4 cells (32m) per side, so 80m of
spacing fills the block from both sides with 16m left for the road. 100m leaves an unzoneable
dead strip down the middle of every block.

Verify afterwards — a correct build reports zero:

```bash
curl -sS "http://127.0.0.1:32123/state/road-anomalies?limit=500&includeDeadEnds=false"
```

## POST /commands/build-neighborhood

Composes grid + connection + zoning. The connection runs before zoning, so a failure to reach
the road network rolls the grid back rather than leaving a zoned orphan island.

```bash
curl -sS -X POST http://127.0.0.1:32123/commands/build-neighborhood \
  -H "Content-Type: application/json" \
  -d '{"roadPrefab":"Basic Road","center":{"x":400,"z":200},"radiusOrCols":5,
       "connectTo":{"x":380,"z":90},
       "zoneMix":{"ResidentialLow":0.7,"CommercialLow":0.3},
       "commercialPlacement":"perimeter","opId":"hood-north-01"}'
```

Same fields as `build-grid`, except `center` names the middle of the neighborhood rather than
a corner, plus:

| field | default | notes |
|---|---|---|
| `radiusOrCols` | 4 | shorthand for a square neighborhood; `cols`/`rows` override it |
| `connectTo` | none | a point on an existing road; omit only if the grid already overlaps the network |
| `connectMaxDistance` | 400 | search radius for `connectTo` |
| `zoneMix` | required | zone name to relative weight, normalised |
| `commercialPlacement` | `perimeter` | `perimeter`, `core`, or `corners` |
| `preserveOccupied` | true | passed through to zoning |
| `zoneRadius` | `spacing/2` | paint radius per block |

`cols * rows` is capped at 144 here rather than 400, because zoning every block is the
expensive part.

Valid `zoneMix` keys, exactly: `ResidentialLow`, `ResidentialHigh`, `CommercialLow`,
`CommercialHigh`, `Industrial`, `Office`, `Unzoned`. CS1 has no plain `Commercial` or
`Residential` zone; the command rejects unrecognised keys with the valid list.

The minority zones are placed first, into whichever blocks the placement rule favours, and the
heaviest share fills the rest. The result is deterministic — the same request paints the same
blocks. Zoning failures are reported per block rather than rolled back: repainting is cheap and
non-destructive, bulldozing a built grid is not.

## POST /commands/connect

Joins a point to the nearest existing network node of a service. Use it after every service
building placement.

```bash
curl -sS -X POST http://127.0.0.1:32123/commands/connect \
  -H "Content-Type: application/json" \
  -d '{"from":{"x":512,"z":-88},"toService":"Road","maxDistance":200,"roadPrefab":"Basic Road"}'
```

| field | default | notes |
|---|---|---|
| `from` | `{0,0}` | the stranded point, typically a building position |
| `toService` | `Road` | any `ItemClass.Service` name |
| `maxDistance` | 200 | search radius, 1..1000 |
| `roadPrefab` | `Basic Road` | prefab for the connecting segment |

`alreadyConnected:true` means the point was already on the network. That is a success, not a
condition to retry.

## GET /capture

Renders an orthographic top-down view into an off-screen texture and returns `image/png`.
Deliberately not the player's camera: the agent gets a deterministic frame of a named area, and
the human's view does not move as a side effect.

```bash
curl -sS "http://127.0.0.1:32123/capture?x=400&z=200&size=1200&pixels=1024&mode=None" -o review.png
```

| query | default | notes |
|---|---|---|
| `x`, `z` | 0 | world centre of the view |
| `size` | 1000 | metres covered edge to edge |
| `pixels` | 1024 | square resolution, capped at 1024 |
| `mode` | `None` | CS1 `InfoManager.InfoMode` name |
| `settleFrames` | 8 | frames to wait after switching overlay |
| `allowBlank` | false | return the image even if it rendered as one flat colour |

Response headers `X-Bridge-Info-Mode` and `X-Bridge-Distinct-Colors` report the overlay that
was actually used and how varied the output was.

Switching info view is not instantaneous in CS1 — the overlay fades in over several frames — so
a capture is a state machine driven from the game thread across frames, not a single call. The
previous overlay is always restored.

Useful modes: `None` (plain view, and the one that shows zoning colours), `Traffic`, `Water`,
`Electricity`, `LandValue`, `Pollution`, `NoisePollution`, `Health`, `Happiness`, `Density`,
`Garbage`, `Education`, `TerrainHeight`, `Transport`. CS1 has no `Zone` overlay; the name is
accepted and resolves to `None`.

A response reporting a single flat colour means the off-screen camera did not pick up the
scene. That is surfaced as an error rather than a valid PNG of nothing.

## POST /commands/set-zone

Request:

```json
{
  "dryRun": true,
  "preserveOccupied": true,
  "zone": "ResidentialLow",
  "center": { "x": 40, "z": 0 },
  "radius": 32
}
```

Supported zones:

- `Unzoned`
- `ResidentialLow`
- `ResidentialHigh`
- `CommercialLow`
- `CommercialHigh`
- `Industrial`
- `Office`

The command paints existing zone blocks near `center`. It works best after roads have created zoning blocks.
`preserveOccupied` defaults to `true` and skips zone blocks that already contain
residential, commercial, industrial, office, service, park, or monument
buildings, so broad zoning commands do not overwrite developed city blocks.

## POST /commands/repair-zones-to-growables

Repairs zone blocks that contain existing growable buildings by aligning
non-empty zoning cells with the nearest residential, commercial, industrial, or
office building. Blocks with ambiguous mixed-use occupancy are skipped.

```json
{
  "dryRun": true
}
```

## POST /commands/repair-zone-clusters

Repairs larger 80m zoning clusters when a whole city block is visually mottled.
The command can fill unzoned holes and, by default, prefers the nearest existing
growable building's zone for occupied blocks so cluster repair does not blindly
convert developed buildings to the cluster's dominant zone.

```json
{
  "dryRun": true,
  "includePatchy": true,
  "fillUnzoned": true,
  "preferGrowableZone": true,
  "gridSize": 80
}
```

## POST /commands/place-building

Request:

```json
{
  "dryRun": true,
  "buildingPrefab": "Wind Turbine",
  "position": { "x": 300, "z": 200 },
  "angleDegrees": 0
}
```

### Placement validation (`"validate"`)

Without validation, `place-building` creates the building exactly where it is told
through `BuildingManager.CreateBuilding`, and a dry run only checks that the prefab
exists. That skips everything the in-game building tool does: no shoreline snap,
no harbor height rule, no dock-to-ship-lane check, no collision test. A harbor
placed that way can sit on dry land with a dock that never reaches a ship lane.

`"validate": true` runs the same checks as the game's `BuildingTool.SimulationStep`
on the **simulation thread** (`SimulationManager.AddAction`), then places the
building at the **adjusted** position and angle:

- Shoreline and ShorelineOrGround prefabs (harbors, cargo harbors): `BuildingTool.SnapToCanal`
  (40 m), then `TerrainManager.GetShorePos` twice within 50 m of `position`, offset
  by the prefab's `m_placementOffset`; the angle comes from the shore direction, so
  `angleDegrees` is ignored. Then `info.m_buildingAI.CheckBuildPosition(0, ref pos,
  ref angle, waterHeight, elevation, ref connectionSegment, out _, out cost)`, and
  `BuildingTool.CheckSpace(..., test: true, ...)` for collisions (test mode, so
  nothing is released). For `HarborAI` this includes the rule `position.y - waterHeight
  <= 32` (else `HeightTooHigh`) and `ShipDockAI.FindConnectionPath` + `TerrainManager.HasWater`
  on the dock-to-lane line (else `CannotConnect`).
- Other placement modes: terrain height at `position`, then `CheckBuildPosition`,
  `CheckSpace` and the tool's slope rule (`maxY - minY > m_maxHeightOffset` gives
  `SlopeTooSteep`). The zone-grid snap of Roadside growables is not reproduced.
- Always: `BuildingManager.CheckLimits()` (else `TooManyObjects`).

Validation is **on by default** for Shoreline and ShorelineOrGround prefabs, and
off for everything else. Pass `"validate": false` to force the old unchecked path,
or `"validate": true` for any prefab. Optional `"elevation"` (default 0) is the
tool's elevation step for elevated stations.

Put `position` within about 50 m of the water's edge on the side you want. With
`"dryRun": true` nothing is placed and the response reports:

```json
{
  "ok": true, "dryRun": true, "buildingPrefab": "Harbor",
  "validated": true, "placementMode": "Shoreline", "branch": "shore",
  "requested": { "x": 4300, "y": 0, "z": -1400 },
  "position":  { "x": 4312.5, "y": 61.2, "z": -1388.0 },
  "snapDistance": 19.1, "angleDegrees": 131.6,
  "shoreFound": true, "canalSnap": false,
  "waterHeight": 55.0, "heightAboveWater": 6.2,
  "connection": { "a": { "x": 0, "y": 0, "z": 0 }, "b": { "x": 0, "y": 0, "z": 0 } },
  "constructionCost": 0,
  "toolErrors": [], "subBuildings": 0, "canPlace": true
}
```

(Values illustrative.) `toolErrors` are `ToolBase.ToolErrors` names, for example
`ShoreNotFound`, `HeightTooHigh`, `CannotConnect`, `ObjectCollision`. `connection`
is the dock-to-ship-lane segment `CheckBuildPosition` found (null when it found
none). `branch` is `canal`, `shore`, `shore-lost`, `no-shore`, `ground-fallback`
or `ground`. A real call (`dryRun` false) with any tool error places nothing and
returns `ok:false` with the same fields. On success it returns `buildingId` plus
the same fields, and `position`/`angleDegrees` are where the building actually went.

Prefabs with sub-buildings (for example `Harbor02`, the Harbor-Bus Hub) are
refused on a real validated call: `BuildingManager.CreateBuilding` does not create
sub-buildings (the game tool adds them one by one), so the result would be a
partial building. The dry run still reports the check, with `subBuildings` > 0.

If the simulation thread does not pick the job up within 10 s, or has not reached
the create step 30 s after that, the job is abandoned and cannot place anything
later; the response then says nothing was changed.

Not checked: money (`NotEnoughMoney`), unlock/milestone state, sub-building
positions (`CheckSubBuildingPosition`), and the
`CanBeBuiltOnlyOnce` monument rule beyond what `CheckBuildPosition` itself reports.
`CheckBuildPosition` can show or hide the game's tutorial placement hints, as
hovering the tool does. `GET /prefabs/buildings` lists each prefab's
`placementMode` and `ai` so you can tell which prefabs validate by default.

## POST /commands/move-building

Recreates an existing building at a new position with the same prefab and
deletes the old building. This is intentionally separate from detection and
from save operations so agents can make small, explicit repair steps.

```json
{
  "dryRun": false,
  "id": 123,
  "position": { "x": 340, "z": 200 },
  "angleDegrees": 180
}
```

## POST /commands/set-building-active

Turns an existing building on or off by id.

```json
{
  "id": 123,
  "active": false
}
```

## POST /commands/disable-blocked-assets

Disables known broken assets in CS1's package asset state so the game should not
use them. The current blocked family is `Block Services - ...`.

```bash
curl -sS -X POST http://127.0.0.1:32123/commands/disable-blocked-assets
```

## POST /commands/bulldoze

Deletes a problem entity by API. Useful for agent-side repair loops after
reading `/state/problems`.

```bash
curl -sS -X POST http://127.0.0.1:32123/commands/bulldoze \
  -H "Content-Type: application/json" \
  -d '{"entityType":"netSegment","id":21778,"keepNodes":false}'
```

Supported `entityType` values:

- `building`
- `netSegment`
- `netNode`

## POST /commands/save

Requests an in-game save through CS1's `SavePanel.SaveGame`, the same code path
used by the normal UI save button. The game writes the `.crp` package
asynchronously, so poll `/state/saves` until the returned file appears.

```bash
curl -sS -X POST http://127.0.0.1:32123/commands/save \
  -H "Content-Type: application/json" \
  -d '{"name":"AgentAutoSave-20260512-1900"}'

# The file appears a few seconds later; poll until it exists.
curl -sS http://127.0.0.1:32123/state/saves
```

## GET /state/saves

Lists local `.crp` saves with paths, timestamps, and file sizes.

```bash
curl -sS http://127.0.0.1:32123/state/saves
```

## POST /commands/set-simulation-speed

Request:

```json
{
  "paused": false,
  "speed": 3
}
```

`speed` is clamped to `1..3`. Use `paused: true` to pause the
simulation while keeping the selected speed in a valid UI state.

## POST /commands/set-tax-rate

Sets tax rates for zoned services. Omit `service`, `subService`, or `level` to
apply the rate broadly; pass `dryRun: true` to preview the affected tax rows.

```json
{
  "dryRun": false,
  "service": "Commercial",
  "rate": 9
}
```

Useful services are `Residential`, `Commercial`, `Industrial`, and `Office`.
`rate` must be between `0` and `29`.

## POST /commands/batch

Request:

```json
{
  "dryRun": true,
  "stopOnError": true,
  "commands": [
    {
      "type": "build-road",
      "roadPrefab": "Basic Road",
      "start": { "x": 120, "z": 0 },
      "end": { "x": 200, "z": 0 },
      "name": "Agent Batch Road"
    },
    {
      "type": "set-zone",
      "zone": "ResidentialLow",
      "center": { "x": 160, "z": 0 },
      "radius": 48
    }
  ]
}
```

Supported command types:

- `build-road`
- `set-zone`

If an item does not include `dryRun`, it inherits the batch-level `dryRun` value. Batches are limited to 32 commands.

## Public transport

Read and change public transport lines, service budgets and policies, and see where the roads are congested. Every field below comes from a named game member, and the response says which one. Line and stop edits run on the command queue like every other command. They honour `dryRun` and return `ok:false` with an error rather than throwing.

These endpoints have **not been exercised in a running game yet**. The member names and the stop-snapping logic were taken from the installed `Assembly-CSharp.dll` (decompiled `TransportTool`, `TransportLine`, `TransportManager`, `EconomyManager`, `DistrictManager`), and the mod compiles against them. Treat the first in-game runs as verification.

### GET /state/transit

Query: `type` (optional, one `TransportInfo.TransportType`: `Bus`, `Metro`, `Train`, `Ship`, `Airplane`, `Taxi`, `Tram`, `Monorail`, `CableCar`, `Trolleybus`, ...), `includeStops=true`, `limit` (lines, default and max 256).

```bash
curl "http://127.0.0.1:32123/state/transit?type=Bus&includeStops=true"
```

Response fields:

- `lines[]`:
  - Identity and state: `id`, `number` (`m_lineNumber`), `name` (`GetLineName`), `transportType`, `prefab` (the `TransportInfo` name), `vehicleType`, `subService`, `color` (`#RRGGBB`, from `GetLineColor`), `flags` (set `TransportLine.Flags` names), `complete`, `activeDay`, `activeNight`.
  - Size and service: `stopCount`, `lengthMeters` (`m_totalLength`), `vehicleCount` (`CountVehicles`), `targetVehicleCount` (`CalculateTargetVehicleCount`, which already includes the service budget), `budget` (`m_budget`, per-line percent), `ticketPrice` (`m_ticketPrice`, cents), `averageInterval` (`m_averageInterval`), `depotBuildingId` (`m_building`).
  - `passengers`: `residents`, `tourists`, `total` and `carOwning` come from `m_passengers.<group>.m_averageCount`, which is the weekly figure the line info panel shows. `lastPeriod` is `m_finalCount` and `current` is `m_tempCount`.
  - `problems`: the distinct stop-node problems on the line. `LineNotConnected` means the path between two stops failed. `TooLong` means the line is too long.
  - `stops[]` (only with `includeStops`):
    - `index`, `nodeId`, `x`/`y`/`z`, `fixedPlatform` (`NetNode.Flags.Fixed`).
    - `waitingPassengers`: `TransportLine.CalculatePassengerCount(stop)`, the same call the line panel's stop list makes.
    - `nodeFinalCounter`: `NetNode.m_finalCounter`, rolled over by `TransportLineAI` every 4096 frames. Its exact meaning is not confirmed.
    - `laneId`, `segmentId`, `stationBuildingId` (the owner of an untouchable platform segment), `problems`.
- `totalsByType`: for each type, `lines`, `completeLines`, `stops`, `vehicles`, `targetVehicles` and `passengersLastWeek` (the sum of the lines' `m_averageCount`).
- `cityPassengersByType`: `TransportManager.m_passengers[type]` residents and tourists (`m_averageCount`), which are the counters the Public Transport info view shows. Note that the view adds `Airplane` and `Helicopter` together.
- `budgets`: `EconomyManager.GetBudget` day and night values for `PublicTransport` and each transit sub-service.
- `transportPrefabs`: every loaded `TransportInfo`, with `transportType`, `vehicleType`, `defaultForType`, net and station services, `unlocked`, and `creatableByBridge`. Use a `name` from here as `prefab` when a type has several prefabs (ship vs ferry, airplane vs blimp).
- `facilities[]`: every `PublicTransport` building.
  - `id`, `prefab`, `subService`, `ai` (the AI class name), `lineType` and `secondaryLineType`, `subBuilding`, `active`, `angleDegrees`, `problems`, `position`.
  - For depots: `maxVehicleCount` and `vehicleCount` (`DepotAI.GetVehicleCount`). For stations: `passengerCount` (`TransportStationAI.GetPassengerCount`).
  - `facilityCount` and `facilitiesBySubService` summarise the list.
- `modalSplit: null`. CS1 does not track a city-wide public-transport share anywhere; this was checked in `District`, `StatisticsManager` and the info panels. The bridge does not invent one.

### GET /state/traffic

Query: `limit` (default 50, max 2000), `minDensity` (0..100), and optionally `x`, `z`, `radius` (metres, default 500) to restrict to segments whose middle is inside that circle.

Only segments whose network AI is `RoadBaseAI` are included, because only those maintain `NetSegment.m_trafficDensity` (0..100, the value the Traffic overlay colours by). Rows are sorted by density, highest first: `id`, `prefab`, `name`, `density`, `trafficBuffer` (the raw per-step accumulator), `lengthMeters`, `start`, `end`, `middle`, and `lanes` (`carLanes`, `busLanes`, `tramLanes`, `trolleybusLanes`, `pedestrianLanes`, `parkingLanes`, `lanesWithStops`, all counted from the prefab's `NetInfo.m_lanes`; a bus lane is a car lane with `LaneType.TransportVehicle`).

The response also carries:

- `averageDensity` (unweighted) and `lengthWeightedAverageDensity` over the matching road segments. The bridge computes both; they are not game figures.
- `densityHistogram`.
- `trafficFlowPercent`: `VehicleManager.m_lastTrafficFlow`, the "average traffic flow" in the Traffic info view. It is city-wide and ignores the area filter.

### GET /state/policies

Returns:

- `cityPolicies`: the policies set in district 0 (`IsCityPolicySet`).
- `districts[]`: `id`, `name`, and the `policies` set in that district.
- `available[]`: every policy this game has loaded (`IsPolicyLoaded`) that `set-policy` accepts, with its `type`, `cityWide` and `unlocked` (`UnlockManager.Unlocked`).

### POST /commands/transit-line-create

```json
{
  "transportType": "Bus",
  "stops": [{ "x": 0, "z": 0 }, { "x": 240, "z": 0 }, { "x": 240, "z": 240 }],
  "name": "Crosstown",
  "color": "#FF8800",
  "budget": 100,
  "dryRun": true
}
```

- Give `transportType` (one of `Bus`, `Metro`, `Train`, `Ship`, `Airplane`, `Tram`, `Monorail`, `CableCar`, `Trolleybus`; this uses `TransportManager.GetTransportInfo(type)`) or `prefab` (an exact `TransportInfo` name). The prefab must be unlocked.
- `stops` needs 2 to 64 points, in travel order. Do not repeat the first stop: the line is closed back to it automatically. A last stop that snaps onto the first stop is dropped (`droppedDuplicateClosingStop: true`). Any other stop that snaps onto the first stop is an error.
- Optional: `name`, `color` (`#RRGGBB`), `budget` (per-line percent, 0..500), `roadSnapDistance` (default 32, max 128), `stationSnapDistance` (default 64, max 256).

**Stop snapping** is a port of `TransportTool.GetStopPosition`, the in-game line tool:

- The tool raycasts under the mouse. The bridge instead gathers every segment within `roadSnapDistance` that the tool's raycast filter would accept (the prefab's net service, sub-service and layer, or its secondary net service) and every building within `stationSnapDistance` of its centre that matches the prefab's station service. It tries them nearest first, up to 64.
- This is an **approximation**, not the tool's raycast. The tool takes the one object under a 3D camera ray and fails if that object is unsuitable. The bridge measures flat distance (for stations, to the building's pivot) and falls through to the next candidate. So a point on open ground next to a station can still snap to it, and on stacked networks at the same x/z the elevation chosen is not controlled.
- **Road types** (Bus, Trolleybus, Tram): the stop goes on the side of the segment nearest the point, at the middle of the segment (the tool always uses lane offset 128/255). The lane must allow stops (`NetLane.Flags.Stops` must be compatible with `TransportInfo.m_stopFlag`). `fixedPlatform` is true.
- **Station types** (Metro, Train, Monorail, Ship, Airplane, CableCar): the stop goes on one of the station building's spawn positions (`CalculateSpawnPosition` with 12 seeds). Where the prefab sets `m_avoidSameStopPlatform`, the bridge prefers the platform used by the fewest existing lines of that type; metro takes the platform nearest the point. A point on a station's own platform segment redirects to that station. `fixedPlatform` is false, as in the game.

After snapping, the bridge checks that no two consecutive stops are within 1 m of each other (the rule `TransportLine.CanAddStop` enforces), then checks the line limits (`TransportManager.CheckLimits` and `NetManager.CheckLimits`). `dryRun` stops here and returns the snapped stops.

**Creating the line** follows the same steps as the tool:

1. `CreateLine(..., newNumber: true)`.
2. For each stop, `CanAddStop` and then `AddStop(-1, position, fixedPlatform)`.
3. Close the loop by adding a stop at the first stop's exact position. `AddStop` sees it is within 2.5 m, joins the last stop to the first, and sets `Complete` itself; the bridge never sets that flag.

If any step fails, the whole line is removed with `ReleaseLine`.

The response contains `lineId`, `line` (a summary with the stop node ids), and `stops[]`. Each stop has `requested`, `resolved` (x/y/z), `snapDistance`, `via` (`segment` or `station`), `segmentId` or `buildingId`, `fixedPlatform` and `candidatesTried`. The live response also includes the stop's `nodeId`.

The paths between stops are worked out **after** the call returns. Let the simulation run, then re-read `/state/transit?includeStops=true`: a `LineNotConnected` problem on a stop means that leg failed.

### POST /commands/transit-line-edit

```json
{
  "lineId": 7,
  "name": "Harbor Loop",
  "color": "#00AA55",
  "budget": 120,
  "ticketPrice": 200,
  "removeStopIndexes": [4, 1],
  "moveStops": [{ "index": 0, "x": 10, "z": 20 }],
  "addStops": [{ "x": 50, "z": 60 }, { "index": 2, "x": 70, "z": 80 }],
  "dryRun": false
}
```

Edits are applied in this order:

1. **Removals.** Indexes refer to the current line and are applied from the highest down. An edit that would leave fewer than 2 stops is refused; delete the line instead.
2. **Moves.** Indexes refer to the line after the removals. Each uses `CanMoveStop`, then `MoveStop`.
3. **Adds.** Each stop is inserted before the stop at `index`, counted after the earlier edits. Omit `index` or pass -1 to append. On a complete loop, `0`, `-1` and the stop count all insert between the last stop and the first, which is how `TransportLine.AddStop` walks the loop; on an incomplete line, `0` inserts a new first stop. Each uses `CanAddStop`, then `AddStop`. Unlike the in-game tool, appending to an **incomplete station line** at its first station does not reuse the first stop and does not close the line.
4. **Properties**: `name` (an empty string restores the generated name), `color`, `budget`, `ticketPrice` (cents).

Before anything changes, every point is snapped, every index is checked, and the whole sequence is replayed on a copy of the stop list using the game's rule that a stop may not sit within 1 m of the stop before or after it. That catches, for example, two adjacent adds at the same point. The game can still refuse a later step for other reasons, and earlier steps are **not** rolled back. When that happens the response is `ok:false` with `applied[]` and the current `line`.

`dryRun` reports the snapped points and a `canApply` flag for each move and add. Those flags come from `CanMoveStop` and `CanAddStop` run against the line as it is now, so when the edit includes removals, or more than one move/add, they are approximate (`canChecksExact: false`). When the checks are exact and one fails, `dryRun` returns `ok:false`.

### POST /commands/transit-line-delete

`{ "lineId": 7, "dryRun": false }`. Calls `TransportManager.ReleaseLine`, which removes the stops and sends the line's vehicles away. The response includes the summary of the deleted line.

### POST /commands/set-service-budget

`{ "service": "PublicTransport", "subService": "PublicTransportBus", "day": 120, "night": 80, "dryRun": false }`

- Calls `EconomyManager.SetBudget(service, subService, value, night)`. `day` and `night` are each optional, 0..150.
- `PublicTransport` without a `subService` cascades to every transit sub-service, just like the budget panel's parent slider. The response lists every affected sub-service.
- A service or sub-service with no budget slider is refused.
- `dryRun: true` with no values returns the current budget.

### POST /commands/set-policy

`{ "policy": "FreeTransport", "districtId": 0, "enabled": true, "dryRun": false }`

- `districtId` 0 (the default) means the whole city and uses `SetCityPolicy` or `UnsetCityPolicy`. Any other id uses `SetDistrictPolicy` or `UnsetDistrictPolicy`. These are the calls the policies panel makes.
- Services, Taxation and CityPlanning policies can be set city-wide or per district. Specialization policies are per district only. Special, Event and Park policies are refused.
- The policy must be loaded (the DLC is present) and unlocked.
- The response gives `before`, `after` and `changed`.

## In-game chat

See [chat.md](chat.md) for the `/chat/*` endpoints behind the in-game Claude panel.
