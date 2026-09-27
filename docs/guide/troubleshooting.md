# Troubleshooting

## The Build Script Cannot Find CS1

`scripts/build.sh` searches the usual Steam library locations for the game's `Managed` folder. If your install lives elsewhere, point it there with `CS1_GAME_DIR` (the Steam `common/Cities_Skylines` folder, or `Cities.app` itself) or `CS1_MANAGED` (the `Managed` folder directly, which skips all detection):

```bash
CS1_GAME_DIR="/path/to/steamapps/common/Cities_Skylines" ./scripts/build.sh
CS1_MANAGED="/path/to/Managed" ./scripts/build.sh
```

## The API Does Not Respond

Check these in order:

1. The mod is enabled in Content Manager -> Mods.
2. A city is loaded.
3. Port `32123` is not already in use.
4. `scripts/start-resume.sh` or `scripts/start-new-map.sh` finished with a `/health` response.

## Connection Refused vs. levelLoaded:false

`/health` answers from the main menu once the mod is enabled. A refused connection means the mod is not loaded, not that the city is still loading. If `/health` answers with `levelLoaded:false`, the mod is running but no city is open; every other endpoint returns `409` until one is.

## Roads Look Connected but Traffic Fails

CS1 network crossings are not intersections unless a real node exists. Use:

```bash
curl -sS "http://127.0.0.1:32123/state/road-anomalies?nearMissDistance=18&shortSegmentLength=32&includeDeadEnds=false"
```

Then remove the bad segment with `/commands/bulldoze` and rebuild with endpoints close enough to reuse the intended road nodes.

## Service Buildings Intersect Roads

Use:

```bash
curl -sS "http://127.0.0.1:32123/state/building-anomalies?limit=200"
```

Move or replace the building with `/commands/move-building` or `/commands/place-building`, then re-check the anomaly endpoint.

## Saves Are Not Visible Yet

`/commands/save` uses CS1's normal save panel path and writes asynchronously. Poll `/state/saves` after requesting a save, or use:

```bash
./scripts/save-city.sh --name AgentAutoSave-clean
```
