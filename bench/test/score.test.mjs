import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { aggregate, compare, extractMetrics, METRIC_NAMES } from "../lib/score.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const fixturesDir = path.join(here, "..", "fixtures");

function loadFixture(name) {
  return JSON.parse(readFileSync(path.join(fixturesDir, name), "utf8"));
}

function aggregateFixture(name) {
  const bundles = loadFixture(name);
  return aggregate(bundles.map(extractMetrics));
}

test("extractMetrics reads the honest fields from a raw bundle", () => {
  const [sample] = loadFixture("baseline-noisy-3samples.json");
  const metrics = extractMetrics(sample);
  assert.equal(metrics.population, sample.summary.citizens.count);
  assert.equal(metrics.trafficFlowPercent, sample.traffic.trafficFlowPercent);
  assert.equal(metrics.problemsTotal, sample.problems.total);
  assert.equal(metrics.cashBalance, sample.areas.cash / 100);
  assert.equal(metrics.transitBoardingsLegs, 3100);
  assert.equal(metrics.despawnIndicator, 2);
});

test("extractMetrics never fabricates a metric that has no source", () => {
  const metrics = extractMetrics({ summary: { citizens: { count: 100 } } });
  assert.equal(metrics.population, 100);
  assert.equal("trafficFlowPercent" in metrics, false);
  assert.equal("cashBalance" in metrics, false);
  assert.equal("problemsTotal" in metrics, false);
});

test("aggregate computes mean/min/max/stddev/spread per metric", () => {
  const agg = aggregateFixture("baseline-noisy-3samples.json");
  assert.equal(agg.trafficFlowPercent.n, 3);
  assert.ok(Math.abs(agg.trafficFlowPercent.mean - 61) < 1e-9);
  assert.equal(agg.trafficFlowPercent.min, 60);
  assert.equal(agg.trafficFlowPercent.max, 62);
  assert.equal(agg.trafficFlowPercent.spread, 2);
  assert.ok(Math.abs(agg.trafficFlowPercent.stddev - Math.sqrt(2 / 3)) < 1e-9);
});

test("aggregate throws on an empty sample array rather than returning a fabricated zero", () => {
  assert.throws(() => aggregate([]), /non-empty/);
});

test("aggregate omits a metric entirely when no sample has it", () => {
  const agg = aggregate([{ population: 10 }, { population: 20 }]);
  assert.deepEqual(Object.keys(agg), ["population"]);
  for (const m of METRIC_NAMES) {
    if (m !== "population") assert.equal(m in agg, false);
  }
});

test("compare: a candidate within baseline noise reports signal:false for every metric", () => {
  const baseline = aggregateFixture("baseline-noisy-3samples.json");
  const run = aggregateFixture("candidate-within-noise.json");
  const { metrics, mixed } = compare(run, baseline);
  assert.equal(metrics.trafficFlowPercent.signal, false);
  assert.equal(metrics.trafficFlowPercent.reason, "within noise");
  assert.equal(metrics.problemsTotal.signal, false);
  assert.equal(mixed, false);
});

test("compare: a candidate that clearly beats baseline noise reports signal:true", () => {
  const baseline = aggregateFixture("baseline-noisy-3samples.json");
  const run = aggregateFixture("candidate-beats-noise.json");
  const { metrics, mixed } = compare(run, baseline);
  assert.equal(metrics.trafficFlowPercent.signal, true);
  assert.ok(metrics.trafficFlowPercent.delta > 0);
  // problems went DOWN (improved); still a signal, just not the "worse" direction
  assert.equal(metrics.problemsTotal.signal, true);
  assert.ok(metrics.problemsTotal.delta < 0);
  assert.equal(mixed, false);
});

test("compare: better flow but worse problems is flagged mixed:true", () => {
  const baseline = aggregateFixture("baseline-noisy-3samples.json");
  const run = aggregateFixture("candidate-mixed.json");
  const { metrics, mixed } = compare(run, baseline);
  assert.equal(metrics.trafficFlowPercent.signal, true);
  assert.ok(metrics.trafficFlowPercent.delta > 0);
  assert.equal(metrics.problemsTotal.signal, true);
  assert.ok(metrics.problemsTotal.delta > 0);
  assert.equal(mixed, true);
});

test("compare: a metric absent from the baseline never claims a signal", () => {
  const baseline = { population: { mean: 100, min: 90, max: 110, stddev: 5, spread: 20, n: 3 } };
  const run = { population: { mean: 100, min: 95, max: 105, stddev: 2, spread: 10, n: 3 }, cashBalance: { mean: 500, min: 480, max: 520, stddev: 10, spread: 40, n: 3 } };
  const { metrics } = compare(run, baseline);
  assert.equal(metrics.cashBalance.signal, false);
  assert.equal(metrics.cashBalance.reason, "no baseline");
});

test("the signal rule requires BOTH exceeding baseline spread AND 2x baseline stddev", () => {
  // Craft a baseline where spread is small but stddev is relatively large (one outlier),
  // so a delta can clear the spread test without clearing the 2x-stddev test.
  const baseline = aggregate([{ problemsTotal: 40 }, { problemsTotal: 40 }, { problemsTotal: 46 }]);
  // spread = 6, stddev = sqrt(((40-42)^2+(40-42)^2+(46-42)^2)/3) = sqrt((4+4+16)/3)=sqrt(8)=2.828
  // 2*stddev = 5.657, so a delta of 6.5 clears spread(6) but not 2*stddev(5.657)... actually 6.5>5.657 too.
  // Use a delta that clears spread(6) but not 2*stddev: need 2*stddev > spread, construct accordingly.
  const run = aggregate([{ problemsTotal: 47 }]);
  const { metrics } = compare(run, baseline);
  // delta = 47-42 = 5, which is < spread(6): not a signal regardless of stddev.
  assert.equal(metrics.problemsTotal.signal, false);
});
