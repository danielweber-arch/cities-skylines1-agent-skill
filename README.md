<p align="center">
  <img src="docs/assets/readme-header.png" width="100%" alt="Cities: Skylines Agent Bridge header">
</p>

<h1 align="center">cities-skylines1-agent-skill</h1>

<p align="center">
  Claude skill, MCP server, and Cities: Skylines 1 mod for API-driven city inspection, repair, building, zoning, and saving.
</p>

<p align="center">
  <a href="README.ja.md">日本語 README</a> ·
  <a href="https://sunwood-ai-labs.github.io/cities-skylines1-agent-skill/">Docs</a> ·
  <a href="docs/api.md">API Reference</a> ·
  <a href="CONTRIBUTING.md">Contributing</a>
</p>

<p align="center">
  <a href="https://github.com/Sunwood-ai-labs/cities-skylines1-agent-skill/actions/workflows/docs.yml"><img alt="Docs workflow" src="https://github.com/Sunwood-ai-labs/cities-skylines1-agent-skill/actions/workflows/docs.yml/badge.svg"></a>
  <a href="https://github.com/Sunwood-ai-labs/cities-skylines1-agent-skill/actions/workflows/pages.yml"><img alt="Pages workflow" src="https://github.com/Sunwood-ai-labs/cities-skylines1-agent-skill/actions/workflows/pages.yml/badge.svg"></a>
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-green.svg"></a>
  <img alt="Platform: macOS" src="https://img.shields.io/badge/platform-macOS-blue.svg">
  <img alt="Game: Cities Skylines 1" src="https://img.shields.io/badge/game-Cities%3A%20Skylines%201-2ec4b6.svg">
</p>

The goal is simple: stop relying on screenshots for city state. The bridge exposes useful Cities: Skylines 1 data as local API responses, then lets agents make small explicit changes such as deleting a segment, building a road, placing a service, painting a zone, changing simulation speed, and saving the city.

![API notification overlay in Cities: Skylines 1](docs/assets/api-notification.jpg)

## ✨ What It Does

- Runs a CS1 mod that listens on `http://127.0.0.1:32123`.
- Exposes city state APIs for problems, facilities, networks, road anomalies, building placement anomalies, zoning anomalies, saves, and prefabs.
- Exposes focused command APIs for network creation, zoning, building placement, building movement, bulldozing, simulation speed, batch helpers, and saving.
- Shows in-game API activity in a persistent console with timestamps, clear, and minimize controls.
- Exposes composite commands so a neighborhood is one call instead of eighty: grid, connect, zone.
- Renders orthographic top-down PNGs with CS1 info-view overlays, for verification without screenshot-driving.
- Ships an MCP server that gives the model typed tools, validated arguments, and filtered responses.
- Ships as a Claude skill through [SKILL.md](SKILL.md) and [agents/openai.yaml](agents/openai.yaml).

## 🖼️ Screenshot Tour

### In-Game API Console

Every game-state API request is appended to a compact CS1 UI console, so recent agent activity stays visible while you work.

![API notification overlay](docs/assets/api-notification.jpg)

### Agent-Built Starter City

The bridge can resume a save, inspect city data, repair infrastructure, and keep developing the city without starting over.

![Agent-built starter city](docs/assets/city-overview.jpg)

### Road Repair Workflow

Road issues are detected from CS1 network data, not image recognition. The agent can then call separate APIs to bulldoze bad segments and rebuild clean connections.

## 🚀 Quick Start

macOS. Requires Mono to compile the mod:

```bash
brew install mono
./scripts/build.sh
```

The script finds the CS1 assemblies automatically (override with `CS1_MANAGED` or
`CS1_GAME_DIR`), compiles `SkylinesAgentBridge.dll` against the game's own .NET 3.5
assemblies, and installs it to:

```text
~/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/SkylinesAgentBridge
```

Enable the mod in the CS1 content manager, launch the game, then:

```bash
curl -sS http://127.0.0.1:32123/health
curl -sS http://127.0.0.1:32123/state/summary
```

The API answers from the main menu, so a refused connection means the mod is not loaded.

### MCP server

For agent use, register the typed tool layer instead of driving curl. `.mcp.json` in the
repository root wires it up for Claude Code automatically:

```bash
cd mcp-server && npm install
```

32 tools, arguments validated before the game sees them, and state responses filtered from
tens of thousands of tokens down to hundreds — a 1500-segment `/state/networks` goes from
~57,000 tokens raw to ~1,200 filtered.

Development uses a lightweight Git Flow model: feature branches target `develop`, while releases and hotfixes target `main`. See [CONTRIBUTING.md](CONTRIBUTING.md) for the branch and AI review workflow.

Launch Cities: Skylines through Steam and load a city from the Resume / Load Game menu.
There is no scripted launcher on macOS.

## 🧭 Agent Repair Pattern

Keep the workflow generic. Prefer separate commands over a magical repair endpoint:

1. Inspect with `/state/problems`, `/state/road-anomalies`, `/state/building-anomalies`, `/state/facilities`, and `/state/networks`.
2. Remove bad objects with `/commands/bulldoze`.
3. Rebuild with `/commands/build-network`, `/commands/place-building`, `/commands/move-building`, and `/commands/set-zone`.
4. Let the simulation settle with `/commands/set-simulation-speed`.
5. Re-check state APIs.
6. Save with `/commands/save`, then verify with `/state/saves`.

## 🔌 API Surface

Read APIs:

- `GET /health`
- `GET /state/summary`
- `GET /state/problems`
- `GET /state/chirps`
- `GET /state/zones`
- `GET /state/economy`
- `GET /state/facilities`
- `GET /state/networks`
- `GET /state/road-anomalies`
- `GET /state/building-anomalies`
- `GET /state/zone-anomalies`
- `GET /state/saves`
- `GET /prefabs/roads`
- `GET /prefabs/networks`
- `GET /prefabs/buildings`

Command APIs:

- `POST /commands/build-network`
- `POST /commands/build-road` compatibility alias
- `POST /commands/set-zone`
- `POST /commands/place-building`
- `POST /commands/move-building`
- `POST /commands/bulldoze`
- `POST /commands/save`
- `POST /commands/set-simulation-speed`
- `POST /commands/set-tax-rate`
- `POST /commands/batch` optional convenience wrapper

Composite command APIs:

- `POST /commands/build-grid` whole road lattice, returns `blockCenters`
- `POST /commands/build-neighborhood` grid + network connection + zoning from a mix
- `POST /commands/connect` join a point to the nearest network node of a service

Render API:

- `GET /capture` orthographic top-down PNG with an optional CS1 info-view overlay

See [docs/api.md](docs/api.md) for request examples and response shapes.

## 🧩 Skill Usage

This repository is also a Claude skill. The root [SKILL.md](SKILL.md) tells an agent how to operate CS1 through this bridge.

Example prompt:

```text
Use $cities-skylines1-agent-skill to resume my CS1 city, inspect current problems, repair road/infrastructure issues with separate API calls, save the city, and report what changed.
```

## 📚 Documentation

- [Docs site](https://sunwood-ai-labs.github.io/cities-skylines1-agent-skill/)
- [Getting Started](docs/guide/getting-started.md)
- [Agent Workflow](docs/guide/usage.md)
- [Architecture](docs/guide/architecture.md)
- [Troubleshooting](docs/guide/troubleshooting.md)
- [API Reference](docs/api.md)
- [Japanese experiment article](docs/articles/building-cities-skylines-with-ai-agents-ja.md)

## 🗂️ Repository Layout

```text
.
├── SKILL.md                 # Claude skill instructions
├── .mcp.json                # MCP server registration for Claude Code
├── agents/openai.yaml       # Skill UI metadata
├── src/                     # CS1 mod source
├── mcp-server/              # Typed MCP tool layer + payload filters
├── templates/               # city-plan.md and progress.json starting points
├── scripts/                 # build.sh, review.sh, and legacy Windows helpers
├── docs/                    # VitePress docs and API reference
└── .github/workflows/       # Docs validation and Pages deployment
```

## ⚠️ Status

This is experimental and built for CS1 on macOS. Test on throwaway saves first. The bridge mutates live CS1 simulation objects through game-thread queued commands, so keep changes small and verify after each step.

## 📄 License

MIT. See [LICENSE](LICENSE).
