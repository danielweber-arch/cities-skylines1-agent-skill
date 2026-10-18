// Pure scoring functions for the CS1 bench harness. No network, no LLM, no filesystem.
// Consumed by scripts/bench-score.mjs (the CLI) and bench/test/score.test.mjs (the unit tests).

/**
 * Metric names, and what each honestly means (see bench/README.md for the long version):
 * - transitBoardingsLegs: TransportManager/TransportLine weekly passenger counts summed across
 *   lines. These are BOARDINGS = LEGS, not unique trips: a rider who transfers once is counted
 *   twice. Never call this "ridership" or "trips".
 * - trafficFlowPercent: VehicleManager.m_lastTrafficFlow via /state/traffic `trafficFlowPercent`.
 *   City-wide, ignores any area filter.
 * - population: /state/summary `citizens.count`. This counts pass-through agents and tourists,
 *   not only residents (see root lessons.md "citizens.count rose ... with zero buildings").
 * - cashBalance: game money in whole currency units (cents / 100). /state/economy does not expose
 *   this (it only has tax rates); the CLI additionally reads /state/areas `cash` (cents) for this
 *   metric. Null when neither source has it.
 * - problemsTotal: /state/problems?limit=200 `total`.
 * - despawnIndicator: count of /state/problems rows whose problem name(s) contain "Despawn"
 *   (case-insensitive). This is a proxy, not a direct despawn counter: the bridge does not expose
 *   a despawn event count as of this wave, so rows are matched by problem name text only. Document
 *   this honestly wherever the metric is printed.
 */
export const METRIC_NAMES = [
  "transitBoardingsLegs",
  "trafficFlowPercent",
  "population",
  "cashBalance",
  "problemsTotal",
  "despawnIndicator",
];

/** mean of a non-empty array of numbers. */
function mean(xs) {
  return xs.reduce((a, b) => a + b, 0) / xs.length;
}

/** population stddev (not sample stddev): matches "spread of these K samples", not an estimator. */
function stddev(xs, m) {
  if (xs.length <= 1) return 0;
  const variance = xs.reduce((a, b) => a + (b - m) ** 2, 0) / xs.length;
  return Math.sqrt(variance);
}

/**
 * aggregate(samples): samples is an array of metric objects (as produced by extractMetrics, one
 * per sample taken during a run). Returns { [metric]: { mean, min, max, stddev, n, spread } } for
 * every metric present as a finite number in at least one sample. A metric that is null/undefined
 * in every sample is omitted (never fabricated as 0).
 */
export function aggregate(samples) {
  if (!Array.isArray(samples) || samples.length === 0) {
    throw new Error("aggregate: samples must be a non-empty array");
  }
  const out = {};
  for (const metric of METRIC_NAMES) {
    const xs = samples
      .map((s) => s && s[metric])
      .filter((v) => typeof v === "number" && Number.isFinite(v));
    if (xs.length === 0) continue;
    const m = mean(xs);
    const min = Math.min(...xs);
    const max = Math.max(...xs);
    out[metric] = {
      mean: m,
      min,
      max,
      stddev: stddev(xs, m),
      n: xs.length,
      spread: max - min,
      missing: samples.length - xs.length,
    };
  }
  return out;
}

/**
 * compare(run, baseline): run and baseline are aggregate() outputs (per-metric {mean,min,max,
 * stddev,spread,...}). Returns { [metric]: { runMean, baselineMean, delta, deltaPercent, signal,
 * reason } }.
 *
 * Signal rule (spec section 6): signal is true only when
 *   |delta| > baseline.spread (baseline max - min)  AND  |delta| > 2 * baseline.stddev
 * Both conditions use the BASELINE's own noise, never the candidate run's. When either baseline
 * stat is missing (metric absent from baseline), signal is false with reason "no baseline".
 *
 * mixed: true at the top level when trafficFlowPercent improved (signal, delta > 0) while
 * problemsTotal got worse (signal, delta > 0, since more problems is worse).
 */
export function compare(run, baseline) {
  const out = {};
  const metrics = new Set([...Object.keys(run || {}), ...Object.keys(baseline || {})]);
  for (const metric of metrics) {
    const r = run && run[metric];
    const b = baseline && baseline[metric];
    if (!r || !b) {
      out[metric] = {
        runMean: r ? r.mean : null,
        baselineMean: b ? b.mean : null,
        delta: null,
        deltaPercent: null,
        signal: false,
        reason: !b ? "no baseline" : "no run data",
      };
      continue;
    }
    const delta = r.mean - b.mean;
    const deltaPercent = b.mean !== 0 ? (delta / Math.abs(b.mean)) * 100 : null;
    const beatsSpread = Math.abs(delta) > b.spread;
    const beatsStddev = Math.abs(delta) > 2 * b.stddev;
    const signal = beatsSpread && beatsStddev;
    out[metric] = {
      runMean: r.mean,
      baselineMean: b.mean,
      delta,
      deltaPercent,
      signal,
      reason: signal ? "exceeds baseline spread and 2x stddev" : "within noise",
    };
  }
  const flow = out.trafficFlowPercent;
  const problems = out.problemsTotal;
  const mixed = Boolean(
    flow && flow.signal && flow.delta > 0 && problems && problems.signal && problems.delta > 0
  );
  return { metrics: out, mixed };
}

/**
 * extractMetrics(bundle): bundle is the raw set of endpoint payloads for one sample, shaped like
 * the real bridge (see docs/api.md and mcp-server/test/mock-bridge.js) and like
 * bench/fixtures/*.json:
 *   { summary, transit, problems, traffic, economy, areas? }
 * Returns one metric object per METRIC_NAMES. A field whose source is missing is left out (not 0,
 * not null-as-zero) so aggregate() can report `missing` honestly.
 */
export function extractMetrics(bundle) {
  const out = {};
  const { summary, transit, problems, traffic, areas } = bundle || {};

  if (summary && summary.citizens && typeof summary.citizens.count === "number") {
    out.population = summary.citizens.count;
  }

  if (traffic && typeof traffic.trafficFlowPercent === "number") {
    out.trafficFlowPercent = traffic.trafficFlowPercent;
  }

  if (transit) {
    // totalsByType[*].passengersLastWeek summed across every transport type = total boardings
    // (legs) for the week, counting a transfer twice. If totalsByType is absent, fall back to
    // summing lines[].passengers.total (also a per-line boarding count).
    if (transit.totalsByType && typeof transit.totalsByType === "object") {
      let sum = 0;
      let any = false;
      for (const t of Object.values(transit.totalsByType)) {
        if (t && typeof t.passengersLastWeek === "number") {
          sum += t.passengersLastWeek;
          any = true;
        }
      }
      if (any) out.transitBoardingsLegs = sum;
    } else if (Array.isArray(transit.lines)) {
      let sum = 0;
      let any = false;
      for (const line of transit.lines) {
        if (line && line.passengers && typeof line.passengers.total === "number") {
          sum += line.passengers.total;
          any = true;
        }
      }
      if (any) out.transitBoardingsLegs = sum;
    }
  }

  // cashBalance: /state/economy carries no cash figure (tax rates only); /state/areas `cash` is
  // in cents. Accept either a pre-divided `cashBalance`/`cash` on economy (future bridge versions)
  // or areas.cash/100, in that order, so the fixtures and a real bridge both work.
  const economy = bundle && bundle.economy;
  if (economy && typeof economy.cashBalance === "number") {
    out.cashBalance = economy.cashBalance;
  } else if (economy && typeof economy.cash === "number") {
    out.cashBalance = economy.cash / 100;
  } else if (areas && typeof areas.cash === "number") {
    out.cashBalance = areas.cash / 100;
  }

  if (problems && typeof problems.total === "number") {
    out.problemsTotal = problems.total;
  }

  if (problems && Array.isArray(problems.problems)) {
    let despawn = 0;
    for (const row of problems.problems) {
      const names = Array.isArray(row.problemNames)
        ? row.problemNames
        : typeof row.problems === "string"
          ? [row.problems]
          : [];
      if (names.some((n) => typeof n === "string" && n.toLowerCase().includes("despawn"))) {
        despawn += 1;
      }
    }
    out.despawnIndicator = despawn;
  } else if (problems && problems.countsByProblem) {
    let despawn = 0;
    for (const [name, count] of Object.entries(problems.countsByProblem)) {
      if (name.toLowerCase().includes("despawn") && typeof count === "number") despawn += count;
    }
    out.despawnIndicator = despawn;
  }

  return out;
}
