/**
 * Layout template stamping (wave2-spec.md section 5). Pure transform (world = anchor +
 * rotate(local, angleDeg)) is exported separately so it can be unit tested without the bridge.
 */
import type { CheckResult } from "./checker.js";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import type { BridgeClient } from "./client.js";
import { rotate, type Plan, type PlanRoad } from "./checker.js";
import { enforcePlan, blockedMessage } from "./planGuard.js";
import { repoRoot } from "./cityContext.js";

/** docs/api.md POST /commands/batch: at most 32 commands per call. */
export const BATCH_LIMIT = 32;

type BatchItem = {
  index?: number;
  type?: string;
  skipped?: boolean;
  result?: { ok?: boolean; segmentId?: number; error?: string; waterCheck?: unknown };
};
type BatchResponse = { ok?: boolean; allOk?: boolean; executed?: number; results?: BatchItem[] };

function itemsOf(response: BatchResponse): BatchItem[] {
  return Array.isArray(response?.results) ? response.results : [];
}

export type LayoutRole = { default: string; allowed: string[] };
export type LayoutTemplate = {
  name: string;
  version: number;
  description: string;
  geometryStatus: string;
  anchor: string;
  roles: Record<string, LayoutRole>;
  nodes: Record<string, { x: number; z: number; elevation?: number }>;
  segments: Array<{ from: string; to: string; role: string; name?: string }>;
  footprint: { minX: number; maxX: number; minZ: number; maxZ: number };
  notes?: string[];
};

export function layoutDir(): string {
  return process.env.CS1_LAYOUT_DIR ?? join(repoRoot(), "templates", "layouts");
}

export function loadTemplate(name: string): LayoutTemplate {
  const dir = layoutDir();
  const filePath = join(dir, `${name}.json`);
  if (!existsSync(filePath)) {
    throw new Error(`No layout template "${name}" in ${dir}`);
  }
  return JSON.parse(readFileSync(filePath, "utf8")) as LayoutTemplate;
}

/** World point = anchor + rotate(local, angleDeg). Exported for a unit test fixing the convention. */
export function worldPoint(
  anchor: { x: number; z: number },
  local: { x: number; z: number },
  angleDeg: number,
): { x: number; z: number } {
  const r = rotate(local, angleDeg);
  return { x: anchor.x + r.x, z: anchor.z + r.z };
}

export function rotatedFootprintBbox(
  anchor: { x: number; z: number },
  footprint: { minX: number; maxX: number; minZ: number; maxZ: number },
  angleDeg: number,
): { minX: number; maxX: number; minZ: number; maxZ: number } {
  const corners = [
    { x: footprint.minX, z: footprint.minZ },
    { x: footprint.maxX, z: footprint.minZ },
    { x: footprint.maxX, z: footprint.maxZ },
    { x: footprint.minX, z: footprint.maxZ },
  ].map((c) => worldPoint(anchor, c, angleDeg));
  return {
    minX: Math.min(...corners.map((c) => c.x)),
    maxX: Math.max(...corners.map((c) => c.x)),
    minZ: Math.min(...corners.map((c) => c.z)),
    maxZ: Math.max(...corners.map((c) => c.z)),
  };
}

function buildPlanRoads(
  template: LayoutTemplate,
  anchor: { x: number; z: number },
  angleDeg: number,
  roadsOverride: Record<string, string> | undefined,
): { roads: PlanRoad[]; error?: string } {
  const roads: PlanRoad[] = [];
  for (const seg of template.segments) {
    const role = template.roles[seg.role];
    if (!role) return { roads: [], error: `Template "${template.name}" references unknown role "${seg.role}".` };
    const prefab = roadsOverride?.[seg.role] ?? role.default;
    if (!role.allowed.includes(prefab)) {
      return { roads: [], error: `Role "${seg.role}" prefab "${prefab}" is not in allowed list [${role.allowed.join(", ")}].` };
    }
    const fromNode = template.nodes[seg.from];
    const toNode = template.nodes[seg.to];
    if (!fromNode || !toNode) return { roads: [], error: `Segment references unknown node "${seg.from}" or "${seg.to}".` };
    const start = worldPoint(anchor, fromNode, angleDeg);
    const end = worldPoint(anchor, toNode, angleDeg);
    roads.push({
      prefab,
      name: seg.name,
      points: [
        { x: start.x, z: start.z, elevation: fromNode.elevation },
        { x: end.x, z: end.z, elevation: toNode.elevation },
      ],
    });
  }
  return { roads };
}

export type StampArgs = {
  name: string;
  anchor: { x: number; z: number };
  angleDeg: number;
  roads?: Record<string, string>;
  dryRun?: boolean;
};

export type StampResult = {
  ok: boolean;
  template?: string;
  verdict?: string;
  error?: string;
  hardCount?: number;
  advisory?: unknown[];
  advisoryTotal?: number;
  dryRun?: boolean;
  plan?: { segments: number };
  waterCheck?: unknown;
  built?: { count: number; ids: number[]; known: boolean };
  failedIndex?: number;
  notBuilt?: number;
  repair?: string;
  anomaliesInFootprint?: number;
  footprint?: { minX: number; maxX: number; minZ: number; maxZ: number };
};

/** Advisories grouped per rule: a roundabout emits one A-SHORT per piece, the model needs one line. */
function groupAdvisories(check: CheckResult): { advisory?: unknown[]; advisoryTotal?: number } {
  if (check.advisory.length === 0) return {};
  const groups = new Map<string, { rule: string; count: number; first: string }>();
  for (const a of check.advisory) {
    const g = groups.get(a.rule);
    if (g) g.count++;
    else groups.set(a.rule, { rule: a.rule, count: 1, first: a.msg });
  }
  return { advisory: [...groups.values()], advisoryTotal: check.advisoryTotal };
}

/** Steps 1-5 of wave2-spec.md section 5. */
export async function stampLayout(args: StampArgs, bridge: BridgeClient): Promise<StampResult> {
  let template: LayoutTemplate;
  try {
    template = loadTemplate(args.name);
  } catch (error) {
    return { ok: false, error: error instanceof Error ? error.message : String(error) };
  }

  const { roads, error } = buildPlanRoads(template, args.anchor, args.angleDeg, args.roads);
  if (error) return { ok: false, error };

  const plan: Plan = { roads };
  const check = await enforcePlan(plan, bridge);
  if (check.verdict === "blocked") {
    return { ok: false, error: blockedMessage(check), hardCount: check.hard.length };
  }

  // The bridge's batch contract (src/BatchCommands.cs, docs/api.md POST /commands/batch):
  // body {dryRun, stopOnError, commands:[{type:"build-road", roadPrefab, start, end, name}]},
  // at most 32 commands per call, response {ok, allOk, executed, results:[{index, type, result}]}
  // where result is the build-road response ({ok, segmentId, waterCheck, error, ...}).
  const commands = roads.map((road) => ({
    type: "build-road",
    roadPrefab: road.prefab,
    start: road.points[0],
    end: road.points[1],
    name: road.name,
  }));
  const chunks: Array<typeof commands> = [];
  for (let i = 0; i < commands.length; i += BATCH_LIMIT) chunks.push(commands.slice(i, i + BATCH_LIMIT));
  const footprint = rotatedFootprintBbox(args.anchor, template.footprint, args.angleDeg);

  // Step 3: dry run every piece (all chunks) before anything is built.
  const dryItems: BatchItem[] = [];
  for (const chunk of chunks) {
    let response: BatchResponse;
    try {
      response = (await bridge.post("/commands/batch", { dryRun: true, stopOnError: false, commands: chunk })) as BatchResponse;
    } catch (e) {
      return { ok: false, error: `dry-run batch failed: ${e instanceof Error ? e.message : String(e)}`, footprint };
    }
    dryItems.push(...itemsOf(response));
  }
  const dryFailed = dryItems.filter((it) => it.result?.ok === false);
  const onWater = dryItems.filter((it) => (it.result?.waterCheck as { onWater?: boolean } | undefined)?.onWater === true).length;
  const dryRunSummary = {
    pieces: commands.length,
    dryRunOk: dryItems.length - dryFailed.length,
    dryRunFailed: dryFailed.length,
    onWater,
    firstFailures: dryFailed.slice(0, 5).map((it) => ({ index: it.index, error: it.result?.error ?? "failed" })),
  };

  if (args.dryRun || dryFailed.length > 0) {
    return {
      ok: dryFailed.length === 0,
      template: template.name,
      verdict: check.verdict,
      ...groupAdvisories(check),
      dryRun: true,
      ...(dryFailed.length > 0 ? { error: `dry run refused ${dryFailed.length} of ${commands.length} pieces; nothing built` } : {}),
      plan: { segments: roads.length },
      waterCheck: dryRunSummary,
      footprint,
    };
  }

  // Step 4: the real batches, in order, stopOnError. Never retry silently; on any failure
  // report exactly what was built so the agent can repair instead of re-stamping.
  const builtIds: number[] = [];
  let failedIndex = -1;
  let failError: string | undefined;
  let builtKnown = true;
  for (let c = 0; c < chunks.length && failedIndex < 0; c++) {
    const chunk = chunks[c]!;
    const base = c * BATCH_LIMIT;
    let response: BatchResponse;
    try {
      response = (await bridge.post("/commands/batch", { dryRun: false, stopOnError: true, commands: chunk })) as BatchResponse;
    } catch (e) {
      // The whole call failed: the bridge executes sequentially, so some of this chunk may be
      // built and we cannot tell which. Say so instead of claiming a count.
      failedIndex = base;
      failError = `batch call failed: ${e instanceof Error ? e.message : String(e)}`;
      builtKnown = false;
      break;
    }
    for (const it of itemsOf(response)) {
      const r = it.result;
      if (r && r.ok !== false && typeof r.segmentId === "number") {
        builtIds.push(r.segmentId);
      } else if (r && r.ok === false) {
        failedIndex = base + (typeof it.index === "number" ? it.index : 0);
        failError = r.error ?? "batch item failed";
        break;
      } else if (it.skipped) {
        break;
      }
    }
  }
  // Step 5: anomalies in footprint, only after a real run.
  let anomaliesInFootprint: number | undefined;
  try {
    const anomalies = (await bridge.get("/state/road-anomalies", { limit: 500, includeDeadEnds: "false" })) as {
      anomalies?: Array<{ position?: { x: number; z: number }; center?: { x: number; z: number } }>;
    };
    anomaliesInFootprint = (anomalies.anomalies ?? []).filter((a) => {
      const p = a.position ?? a.center;
      if (!p) return false;
      return p.x >= footprint.minX && p.x <= footprint.maxX && p.z >= footprint.minZ && p.z <= footprint.maxZ;
    }).length;
  } catch {
    anomaliesInFootprint = undefined;
  }

  return {
    ok: failedIndex < 0,
    template: template.name,
    verdict: check.verdict,
    ...groupAdvisories(check),
    dryRun: false,
    built: { count: builtIds.length, ids: builtIds.slice(0, 20), known: builtKnown },
    ...(failedIndex >= 0
      ? {
          failedIndex,
          error: failError ?? "batch item failed",
          notBuilt: commands.length - builtIds.length,
          repair: builtKnown
            ? "repair or bulldoze the built ids above before stamping again"
            : "the failing batch call executed sequentially; read cs1_state_networks and cs1_state_road_anomalies inside footprint to see what exists before touching anything",
        }
      : {}),
    anomaliesInFootprint,
    footprint,
  };
}
