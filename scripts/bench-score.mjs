#!/usr/bin/env node
// CLI wrapper around bench/lib/score.mjs. No LLM involved anywhere in this file.
//
// Usage:
//   scripts/bench-score.mjs --scenario <id> [--base-url URL] [--baseline] [--compare <file>]
//                            [--settle-days N] [--samples N] [--interval SEC] [--manifest FILE]
//
//   --base-url URL     bridge base URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
//   --scenario ID       scenario id from bench/manifest.json (required)
//   --manifest FILE     path to the manifest (default: <repo>/bench/manifest.json)
//   --baseline          mark this run's output as the scenario's baseline (adds baseline:true)
//   --compare FILE       path to a previous run's JSON to compare this run against
//   --settle-days N      override the scenario's settleDays
//   --samples N           override the scenario's samples
//   --interval SEC        override the scenario's sampleIntervalSec
//   -h, --help            show this help and exit 0 (works without the game running)
//
// Flow: identity check (/health .city vs manifest expect) -> refuse on mismatch -> unpause at
// speed 3 -> wait settleDays in-game days (poll /state/summary gameTime) -> pause -> take N
// samples spaced intervalSec apart with the sim running -> aggregate -> optionally compare ->
// write results to <cityDir>/cache/bench/<scenario>-<ts>.json, or bench/results/ when the
// scenario's cityDir is null.

import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { aggregate, compare, extractMetrics } from "../bench/lib/score.mjs";

const HELP = `bench-score.mjs - score a CS1 bench run (no LLM)

Usage:
  scripts/bench-score.mjs --scenario <id> [options]

Options:
  --base-url URL      bridge base URL (default: $CS1_BRIDGE_URL or http://127.0.0.1:32123)
  --scenario ID        scenario id from bench/manifest.json (required)
  --manifest FILE       path to the manifest (default: <repo>/bench/manifest.json)
  --baseline            mark this run as the scenario's baseline
  --compare FILE         compare this run against a previous run's JSON file
  --settle-days N        override the scenario's settleDays
  --samples N             override the scenario's samples
  --interval SEC          override the scenario's sampleIntervalSec
  -h, --help              show this help and exit 0

This command works with --help even when the game is not running. Any other invocation requires
a live bridge at --base-url and will fail fast with a clear error if it cannot reach /health.
`;

function parseArgs(argv) {
  const args = { baseUrl: process.env.CS1_BRIDGE_URL || "http://127.0.0.1:32123" };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const next = () => argv[++i];
    switch (a) {
      case "-h":
      case "--help":
        args.help = true;
        break;
      case "--base-url":
        args.baseUrl = next();
        break;
      case "--scenario":
        args.scenario = next();
        break;
      case "--manifest":
        args.manifest = next();
        break;
      case "--baseline":
        args.baseline = true;
        break;
      case "--compare":
        args.compare = next();
        break;
      case "--settle-days":
        args.settleDays = Number(next());
        break;
      case "--samples":
        args.samples = Number(next());
        break;
      case "--interval":
        args.interval = Number(next());
        break;
      default:
        throw new Error(`unknown argument: ${a}`);
    }
  }
  return args;
}

function repoRoot() {
  // scripts/bench-score.mjs -> repo root is one level up.
  return path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
}

async function getJson(baseUrl, route) {
  const res = await fetch(`${baseUrl}${route}`);
  if (!res.ok) throw new Error(`GET ${route} -> HTTP ${res.status}`);
  return res.json();
}

async function readBundle(baseUrl) {
  const [summary, transit, problems, traffic, economy, areas] = await Promise.all([
    getJson(baseUrl, "/state/summary"),
    getJson(baseUrl, "/state/transit"),
    getJson(baseUrl, "/state/problems?limit=200"),
    getJson(baseUrl, "/state/traffic"),
    getJson(baseUrl, "/state/economy"),
    // /state/areas is additional to the spec's literal endpoint list, needed only for an honest
    // cashBalance figure (see bench/README.md "Known gaps"). Never let its absence fail a run.
    getJson(baseUrl, "/state/areas").catch(() => null),
  ]);
  return { summary, transit, problems, traffic, economy, areas };
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function checkIdentity(health, scenario, cityDir, root) {
  const city = health && health.city;
  if (!city) {
    throw new Error("identity check failed: /health has no .city (no level loaded)");
  }
  const expectName = scenario.expect && scenario.expect.cityName;
  if (expectName && city.name !== expectName) {
    throw new Error(
      `identity check failed: loaded city "${city.name}" does not match manifest expect.cityName "${expectName}"`
    );
  }
  if (cityDir) {
    const cityMdPath = path.join(root, cityDir, "city.md");
    if (existsSync(cityMdPath)) {
      const text = readFileSync(cityMdPath, "utf8");
      const idMatch = text.match(/^id:\s*(.+)$/m);
      const boundId = idMatch && idMatch[1].trim();
      if (boundId && boundId !== "unknown" && city.id && boundId !== city.id) {
        throw new Error(
          `identity check failed: loaded city id "${city.id}" does not match ${cityDir}/city.md id "${boundId}"`
        );
      }
    }
  }
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.help || !args.scenario) {
    process.stdout.write(HELP);
    process.exit(args.help ? 0 : 2);
  }

  const root = repoRoot();
  const manifestPath = args.manifest || path.join(root, "bench", "manifest.json");
  const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
  const scenario = manifest.scenarios.find((s) => s.id === args.scenario);
  if (!scenario) {
    throw new Error(`scenario "${args.scenario}" not found in ${manifestPath}`);
  }

  const settleDays = args.settleDays ?? scenario.settleDays;
  const samples = args.samples ?? scenario.samples;
  const intervalSec = args.interval ?? scenario.sampleIntervalSec;

  const health = await getJson(args.baseUrl, "/health");
  checkIdentity(health, scenario, scenario.cityDir, root);

  // Unpause at speed 3.
  await fetch(`${args.baseUrl}/commands/set-simulation-speed`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ speed: 3, paused: false }),
  });

  // Wait settleDays in-game days, polling gameTime.
  const startSummary = await getJson(args.baseUrl, "/state/summary");
  const startDate = new Date(startSummary.gameTime);
  const targetMs = startDate.getTime() + settleDays * 24 * 60 * 60 * 1000;
  for (;;) {
    const s = await getJson(args.baseUrl, "/state/summary");
    if (new Date(s.gameTime).getTime() >= targetMs) break;
    await sleep(5000);
  }

  // Pause, then take `samples` snapshots `intervalSec` apart with the sim running.
  await fetch(`${args.baseUrl}/commands/set-simulation-speed`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ paused: true }),
  });
  await fetch(`${args.baseUrl}/commands/set-simulation-speed`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ speed: 3, paused: false }),
  });

  const rawSamples = [];
  for (let i = 0; i < samples; i++) {
    rawSamples.push(await readBundle(args.baseUrl));
    if (i < samples - 1) await sleep(intervalSec * 1000);
  }

  const metricSamples = rawSamples.map(extractMetrics);
  const aggregated = aggregate(metricSamples);

  const ts = new Date().toISOString().replace(/[:.]/g, "-");
  const result = {
    scenario: scenario.id,
    baseline: Boolean(args.baseline),
    takenAt: new Date().toISOString(),
    settleDays,
    samples,
    sampleIntervalSec: intervalSec,
    city: health.city,
    aggregate: aggregated,
  };

  if (args.compare) {
    const baselineRun = JSON.parse(readFileSync(args.compare, "utf8"));
    result.comparedAgainst = args.compare;
    const { metrics, mixed } = compare(aggregated, baselineRun.aggregate);
    result.compare = metrics;
    result.mixed = mixed;
  }

  let outPath;
  if (scenario.cityDir) {
    const cacheDir = path.join(root, scenario.cityDir, "cache", "bench");
    mkdirSync(cacheDir, { recursive: true });
    outPath = path.join(cacheDir, `${scenario.id}-${ts}.json`);
  } else {
    const resultsDir = path.join(root, "bench", "results");
    mkdirSync(resultsDir, { recursive: true });
    outPath = path.join(resultsDir, `${scenario.id}-${ts}.json`);
  }
  writeFileSync(outPath, JSON.stringify(result, null, 2));
  process.stdout.write(`${JSON.stringify({ ok: true, outPath, result }, null, 2)}\n`);
}

main().catch((err) => {
  process.stderr.write(`bench-score.mjs: ${err.message}\n`);
  process.exit(1);
});
