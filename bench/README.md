# bench/

A no-LLM A/B bench harness for CS1 city state: run a save for a while, sample a handful of state
endpoints, and say whether a candidate change moved a metric by more than the baseline's own noise.

**Status: live runs UNVERIFIED, game not running 2026-10-01.** Nothing below has been exercised
against the real bridge. The unit tests (`bench/test/score.test.mjs`) exercise the pure scoring
code against recorded-shape fixtures only; they do not prove the CLI talks to a real game correctly.

## Layout

- `manifest.json` - the scenarios (which save, which city context dir, settle time, sample plan).
- `lib/score.mjs` - pure functions: `aggregate(samples)`, `compare(run, baseline)`,
  `extractMetrics(bundle)`. No network, no filesystem, no game. Unit-tested.
- `fixtures/*.json` - payload bundles shaped like the real endpoints (see docs/api.md and
  `mcp-server/test/mock-bridge.js`), used only by the unit tests.
- `test/score.test.mjs` - `node --test` tests against the fixtures.
- `results/` - where `scripts/bench-score.mjs` writes a run's output when the scenario has no
  `cityDir` (scenarios with a `cityDir` write to `<cityDir>/cache/bench/` instead).

## How to run

Unit tests only (no game required):

```bash
node --test bench/test/*.test.mjs
```

A real bench run (game must be installed, `scripts/restart-game.sh` and the bridge mod working):

```bash
scripts/bench-run.sh tampa-transit            # candidate / live run
scripts/bench-score.mjs --scenario tampa-transit --baseline   # mark this run as the baseline
scripts/bench-score.mjs --scenario tampa-transit --compare bench/results/tampa-transit-<ts>.json
```

`bench-run.sh` never touches the original `.crp` files: it copies the scenario's save to
`bench-<scenario>-<ts>.crp` in the same Saves folder (so `--continuelastsave` picks it up), refuses
if that name already exists, then calls `restart-game.sh` and the scorer.

## What the numbers mean

`bench-score.mjs` reads `/health`, `/state/summary`, `/state/transit`, `/state/economy`,
`/state/problems?limit=200`, `/state/traffic`, and (for `cashBalance` specifically, since
`/state/economy` only carries tax rates) `/state/areas`. It unpauses at speed 3, waits
`settleDays` in-game days (polling `gameTime` from `/state/summary`), pauses, then takes `samples`
snapshots spaced `sampleIntervalSec` apart with the sim running, and aggregates them.

Metric names are deliberately literal about what they measure, not what you'd want them to mean:

| Metric | What it actually is |
|---|---|
| `transitBoardingsLegs` | Sum of weekly passenger boardings across transit lines. A rider who transfers once is counted twice. This is **boardings = legs, not unique trips.** |
| `trafficFlowPercent` | `VehicleManager.m_lastTrafficFlow`, city-wide. Ignores any area filter you might apply elsewhere. |
| `population` | `/state/summary` `citizens.count`. Includes pass-through agents and tourists, not only residents (see root `lessons.md`: this has been seen to rise with zero buildings present). |
| `cashBalance` | Game money in whole currency units, from `/state/areas` `cash` (cents) / 100, because `/state/economy` does not expose a cash figure as of this wave. Null if neither source has it; never fabricated as 0. |
| `problemsTotal` | `/state/problems?limit=200` `total`. |
| `despawnIndicator` | Count of problem rows whose problem name(s) contain "Despawn" (case-insensitive). This is a text-match **proxy**, not a direct despawn-event counter: the bridge does not expose one. Treat it as indicative only. |

### Aggregation

For each metric, `aggregate()` computes `mean`, `min`, `max`, `stddev` (population stddev of the
samples actually taken, not a sample-variance estimator), `spread` (`max - min`), and `n`. A metric
missing from every sample is left out of the result entirely rather than reported as 0.

### Comparison and the signal rule

`compare(run, baseline)` reports, per metric, `delta = run.mean - baseline.mean` and a boolean
`signal`. **`signal` is true only when both hold, using the BASELINE's own noise:**

```
|delta| > baseline.spread        (baseline max - min)
|delta| > 2 * baseline.stddev
```

Otherwise `signal: false, reason: "within noise"`. A metric absent from the baseline is always
`signal: false, reason: "no baseline"` - it is never guessed.

`mixed: true` at the top level means `trafficFlowPercent` improved (signal, delta > 0) while
`problemsTotal` got worse (signal, delta > 0) in the same run: a flow win that cost you something
elsewhere, worth a second look before calling the candidate a win.

### Identity check

Before scoring, the CLI compares the live `/health` `.city.name` against the scenario's
`expect.cityName`, and (when the scenario has a `cityDir` and that city's `city.md` header has a
known `id:`) the live `.city.id` against it. A mismatch refuses to score rather than silently
mixing one city's numbers into another's baseline.

## Known gaps (2026-10-01)

- `expect.cityName` / `populationMin` / `populationMax` in `manifest.json` are all `null`: nobody
  has read these four saves' actual city identity or population live. Fill them in from the first
  real run, not from a guess.
- `cashBalance` depends on `/state/areas`, which the literal endpoint list in the wave-2 spec for
  this harness did not name (it named `/state/economy`). This is a deliberate, documented deviation
  made because `/state/economy` has no cash field at all and "honest metric names" (the same spec
  section) rules out inventing one; see the table above.
- `despawnIndicator` is unverified against a real despawn event; it is a problem-name text match
  only, confirmed nowhere in this wave against actual despawning vehicles.
