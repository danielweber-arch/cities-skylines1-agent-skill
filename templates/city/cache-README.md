# cache/

Per-city cached tool payloads, written by the MCP server so repeated terrain/route
reads do not re-walk the same game state every call.

Files:
- `terrain-map-<x>_<z>_<radius>_<cell>.json` - written by `cs1_terrain_map`.
- `route-share-<segmentId>.json` - written by `cs1_segment_route_share`.
- `bench/<scenario>-<ts>.json` - written by `scripts/bench-score.mjs` when the
  scenario's manifest entry has a `cityDir`.

Every cache file wraps its payload as:

```json
{"capturedAt":{"gameDate":"2026-10-03","population":41800,"wallClock":"2026-10-03T12:00:00Z"},"payload":{...}}
```

Staleness: a reader should treat a cache entry as stale once `gameDate` is more
than a few in-game days behind the live `/state/summary` date, or once
`population` has drifted more than a few percent from the live value. Nothing
in this directory is authoritative; it is a cache, never a source of truth. It
is safe to delete the whole directory at any time.
