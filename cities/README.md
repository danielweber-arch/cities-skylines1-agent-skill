# cities/

Per-city context. One directory per city, created from `templates/city/`. Game data and plans are
specific to a map and a save, never shared across cities (user directive, 2026-10-01: "game data
should be specific to the map and current progress of the save").

## Layout

```
cities/<slug>/
  city.md          brief + Standing orders + Protected areas (see header block below)
  progress.md       phase state + createdEntities + openOrders
  plan.md           master plan
  lessons.md        city-specific lessons ONLY (global Proven Rules stay in root lessons.md)
  cache/
    README.md       what goes here, staleness stamp format
```

A city may also carry extra plan files (e.g. `plan-phase4.md`, `transit-review.md`) when the
migrated content didn't collapse cleanly into the five standard files; the slug directory is still
the single home for everything about that city.

## Slug rule

`slug` = the city's name, lower-cased, with every run of characters outside `[a-z0-9]` collapsed to
a single `-`, trimmed of leading/trailing `-`. When the game's stable per-city id
(`m_gameInstanceIdentifier`) is known, append `-` plus its first 8 characters, e.g.
`cities/portville-a1b2c3d4/`. When the id is not known (game not running, or never bound), the slug
has no id suffix, e.g. `cities/portville/`.

## city.md header block

The first lines of every `city.md` are a small machine-readable header:

```
id: <game instance id, or "unknown">
name: <city name as the game displays it>
map: <map name, or "unknown">
bindOnLoad: true | false
```

`bindOnLoad: true` means this directory's `id:` is not yet confirmed against a live game: the next
time a city with this `name:` loads, the MCP server's `resolveCityDir` (see
`mcp-server/src/cityContext.ts`) rewrites `id:` to the real value and flips `bindOnLoad` to `false`.
Once `bindOnLoad` is `false`, the directory is bound for good and only an exact id match resolves to
it, never a name match - this stops two different cities that happen to share a display name from
silently merging context.

## Bind rule

`cs1_city_context` resolves the loaded city to a directory by: (1) exact `id:` match, else (2) a
directory with `id: unknown` and a case-insensitive `name:` match, which it then binds as described
above. If neither matches, there is no context directory yet: create one from `templates/city/`
rather than reusing another city's files, even temporarily.

## Stub rule

A city's files used to live at the repo root before 2026-10-01 (`city.md`, `city-ashford.md`,
`progress.md`, `progress-portville.md`, `portville-master-plan.md`, `portville-phase4-plan.md`,
`tampa-master-plan.md`, `transit-progress.md`, `transit-review.md`). Each was moved with `git mv`
into its city's directory under `cities/`, and the old root path now holds a 3-line stub:

```
# Moved

Moved to cities/<slug>/<file> (per-city context, 2026-10-01).
```

Nothing should read those root paths going forward; they exist only so an old link or habit lands
somewhere useful instead of a 404.

## Current cities (migrated 2026-10-01)

- `cities/ashford/` - from `city-ashford.md` + `progress.md`. `id: unknown`, `name: Ashford Transit`.
- `cities/portville/` - from `city.md` + `progress-portville.md` + `portville-master-plan.md` (as
  `plan.md`) + `portville-phase4-plan.md` (as `plan-phase4.md`). `id: unknown`, `name: Portville`.
- `cities/tampa/` - from `tampa-master-plan.md` (as `plan.md`) + `transit-progress.md` (as
  `progress.md`) + `transit-review.md`. `id: unknown`, `name: TAmpa`. `city.md` has no source file
  (none existed at the root) and was written fresh from `plan.md`'s headline and `transit-review.md`;
  see that file for details.

Ids are unknown for all three because the game was not running during this migration
(2026-10-01). All three carry `bindOnLoad: true` until a live load binds them.
