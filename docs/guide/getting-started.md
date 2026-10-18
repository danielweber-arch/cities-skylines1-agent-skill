# Getting Started

This guide assumes a Mac with Cities: Skylines 1 installed through Steam.

## Requirements

- Cities: Skylines 1 via Steam.
- Mono, for the C# compiler: `brew install mono`.
- jq, used by the helper scripts: `brew install jq`.
- curl 7.76 or newer (macOS ships 8.x).

## Build and Install

From the repository root:

```bash
./scripts/build.sh
```

The script finds the CS1 managed assemblies in your Steam library automatically. If your install is somewhere unusual, point it at the game with one of these overrides:

```bash
CS1_GAME_DIR="/path/to/steamapps/common/Cities_Skylines" ./scripts/build.sh
CS1_MANAGED="/path/to/Managed" ./scripts/build.sh
```

`CS1_GAME_DIR` takes the Steam `common/Cities_Skylines` folder or `Cities.app` itself. `CS1_MANAGED` takes the `Managed` folder directly and skips all detection.

The script compiles `SkylinesAgentBridge.dll` and copies it into:

```text
~/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/SkylinesAgentBridge
```

Enable the mod once in Content Manager -> Mods before loading a city.

## Resume a City

The normal loop starts from the latest local save:

```bash
./scripts/start-resume.sh
```

The script builds the mod unless `--skip-build` is passed, stops a running `Cities` process unless `--skip-kill` is passed, and launches the game through Steam. macOS has no scripted mouse clicks, so it then prints a prompt asking you to click Play in the Paradox launcher and load the city with Resume / Load Game. While you do that, it polls `/health` until `levelLoaded` is `true`.

Flags:

```bash
./scripts/start-resume.sh --skip-build
./scripts/start-resume.sh --skip-kill
./scripts/start-resume.sh --api-port 32123
./scripts/start-resume.sh --steam-app-id 255710
./scripts/start-resume.sh --launcher-timeout 90 --game-load-timeout 300
```

`--launcher-timeout` is how long to wait for `/health` to first answer before warning that the mod is probably not enabled. `--game-load-timeout` is the total wait, from launch, for `levelLoaded` to become `true`. Set `CS1_NO_LAUNCH=1` to skip the Steam launch when the game is already running.

## Start a Fresh Map

For clean experiments:

```bash
./scripts/start-new-map.sh
```

It launches through Steam the same way, then prompts you through New Game and Start in the game before polling `/health`. It takes the same flags as `start-resume.sh`, plus `--skip-new-map`, which only launches and leaves the New Game steps out of the prompt:

```bash
./scripts/start-new-map.sh --skip-build
./scripts/start-new-map.sh --skip-new-map
```

## Verify the Bridge

Once a city is loaded:

```bash
curl -sS http://127.0.0.1:32123/health
curl -sS http://127.0.0.1:32123/state/summary
```

Use `./scripts/smoke-test.sh` for a broader read and dry-run command check. Nothing is built.
