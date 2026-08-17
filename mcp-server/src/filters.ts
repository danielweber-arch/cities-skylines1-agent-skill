/**
 * Payload reduction.
 *
 * This is the single biggest cost lever in the project. /state/networks?limit=1000 returns
 * every segment in the city; raw, that is tens of thousands of tokens per inspection, and
 * the agent needs almost none of it. Each summarizer keeps counts, extremes, and the rows
 * the agent can actually act on, and drops the rest.
 *
 * Every summary carries the fields needed to ask for more: `total` says how much exists,
 * and every tool takes detail:"full" to bypass the filter entirely.
 */

/** ~2K tokens. A state tool should never exceed this without being asked to. */
export const SUMMARY_CHAR_BUDGET = 8_000;
export const FULL_CHAR_BUDGET = 400_000;

type Row = Record<string, unknown>;

/** Pulls the row array out of a bridge response without assuming one fixed key. */
function rowsOf(payload: unknown, ...candidates: string[]): Row[] {
  if (!payload || typeof payload !== "object") return [];
  const record = payload as Record<string, unknown>;
  for (const key of candidates) {
    const value = record[key];
    if (Array.isArray(value)) return value as Row[];
  }
  // Fall back to the first array-valued property, so a renamed key degrades to
  // "slightly wrong summary" instead of "silently empty".
  for (const value of Object.values(record)) {
    if (Array.isArray(value)) return value as Row[];
  }
  return [];
}

function countBy(rows: Row[], key: string): Record<string, number> {
  const counts: Record<string, number> = {};
  for (const row of rows) {
    const value = row[key];
    if (value === undefined || value === null || value === "") continue;
    const label = String(value);
    counts[label] = (counts[label] ?? 0) + 1;
  }
  return counts;
}

function boundingBox(rows: Row[], key = "position"): Row | null {
  let minX = Infinity;
  let maxX = -Infinity;
  let minZ = Infinity;
  let maxZ = -Infinity;
  let seen = 0;

  for (const row of rows) {
    const point = (row[key] ?? row.start ?? row.center) as Row | undefined;
    if (!point) continue;
    const x = Number(point.x);
    const z = Number(point.z);
    if (!Number.isFinite(x) || !Number.isFinite(z)) continue;
    seen++;
    minX = Math.min(minX, x);
    maxX = Math.max(maxX, x);
    minZ = Math.min(minZ, z);
    maxZ = Math.max(maxZ, z);
  }

  return seen === 0 ? null : { minX, maxX, minZ, maxZ };
}

function pick(row: Row, keys: string[]): Row {
  const out: Row = {};
  for (const key of keys) {
    if (row[key] !== undefined && row[key] !== null && row[key] !== "") out[key] = row[key];
  }
  return out;
}

function hasProblem(row: Row): boolean {
  const problems = row.problems;
  if (typeof problems === "string") return problems.length > 0 && problems !== "None";
  const names = row.problemNames;
  return Array.isArray(names) && names.length > 0;
}

// --------------------------------------------------------------------- summarizers

export function summarizeNetworks(payload: unknown) {
  const rows = rowsOf(payload, "segments", "networks");
  const problems = rows.filter(hasProblem);

  // A segment with a zero end node is dangling — that is the actionable subset.
  const dangling = rows.filter((r) => r.startNodeId === 0 || r.endNodeId === 0);

  return {
    total: (payload as Row)?.total ?? rows.length,
    returned: rows.length,
    byPrefab: countBy(rows, "prefab"),
    byService: countBy(rows, "service"),
    bbox: boundingBox(rows, "start"),
    danglingCount: dangling.length,
    dangling: dangling
      .slice(0, 40)
      .map((r) => pick(r, ["id", "prefab", "startNodeId", "endNodeId", "start", "end"])),
    withProblemsCount: problems.length,
    withProblems: problems
      .slice(0, 40)
      .map((r) => pick(r, ["id", "prefab", "problems", "start"])),
    note: "Counts and actionable rows only. Pass detail:'full' for every segment.",
  };
}

export function summarizeFacilities(payload: unknown) {
  const record = (payload ?? {}) as Row;
  const rows = rowsOf(payload, "facilities");
  const broken = rows.filter((r) => hasProblem(r) || r.active === false || r.blockedAsset === true);

  return {
    total: record.total ?? rows.length,
    returned: rows.length,
    countsByService: record.countsByService ?? countBy(rows, "service"),
    countsBySubService: record.countsBySubService ?? countBy(rows, "subService"),
    needsAttentionCount: broken.length,
    needsAttention: broken
      .slice(0, 50)
      .map((r) => pick(r, ["id", "prefab", "service", "active", "blockedAsset", "problems", "position"])),
    note: "Healthy facilities are collapsed into countsByService. Pass detail:'full' for all rows.",
  };
}

export function summarizeGrowables(payload: unknown) {
  const record = (payload ?? {}) as Row;
  const rows = rowsOf(payload, "growables");
  const abandoned = rows.filter((r) => r.abandoned === true);
  const problems = rows.filter(hasProblem);

  return {
    total: record.total ?? rows.length,
    returned: rows.length,
    countsByService: record.countsByService ?? countBy(rows, "service"),
    countsBySubService: record.countsBySubService ?? countBy(rows, "subService"),
    countsByLevel: countBy(rows, "level"),
    bbox: boundingBox(rows),
    abandonedCount: abandoned.length,
    withProblemsCount: problems.length,
    withProblems: problems
      .slice(0, 40)
      .map((r) => pick(r, ["id", "prefab", "service", "problems", "position"])),
    note: "Developed buildings are counted, not listed. Pass detail:'full' for all rows.",
  };
}

export function summarizeProblems(payload: unknown) {
  const record = (payload ?? {}) as Row;
  const rows = rowsOf(payload, "problems");

  // Problems repeat heavily — twenty buildings with NoPower is one fact, not twenty.
  const byName: Record<string, { count: number; examples: Row[] }> = {};
  for (const row of rows) {
    const names = Array.isArray(row.problemNames)
      ? (row.problemNames as unknown[]).map(String)
      : [String(row.problems ?? "Unknown")];
    for (const name of names) {
      if (!name || name === "None") continue;
      byName[name] ??= { count: 0, examples: [] };
      byName[name].count++;
      if (byName[name].examples.length < 3) {
        byName[name].examples.push(pick(row, ["entityType", "id", "prefab", "position", "isFatal"]));
      }
    }
  }

  return {
    total: record.total ?? rows.length,
    returned: rows.length,
    countsByProblem: record.countsByProblem ?? undefined,
    fatalCount: rows.filter((r) => r.isFatal === true).length,
    byProblem: byName,
    note: "Grouped by problem name with up to 3 examples each. Pass detail:'full' for every row.",
  };
}

export function summarizeAnomalies(payload: unknown) {
  const record = (payload ?? {}) as Row;
  const rows = rowsOf(payload, "anomalies");

  const byType: Record<string, { count: number; examples: Row[] }> = {};
  for (const row of rows) {
    const type = String(row.type ?? "unknown");
    byType[type] ??= { count: 0, examples: [] };
    byType[type].count++;
    if (byType[type].examples.length < 5) {
      byType[type].examples.push(
        pick(row, [
          "type", "segmentId", "segmentAId", "segmentBId", "nodeId", "blockId", "buildingId",
          "distance", "length", "position", "center", "bounds", "dominantZone",
        ]),
      );
    }
  }

  return {
    total: record.total ?? rows.length,
    returned: rows.length,
    counts: record.counts ?? undefined,
    byType,
    note: "Grouped by anomaly type with up to 5 examples each. Pass detail:'full' for every row.",
  };
}

export function summarizePrefabs(payload: unknown) {
  const rows = rowsOf(payload, "roads", "networks", "buildings");
  return {
    total: rows.length,
    names: rows.map((r) => String(r.name ?? r.displayName ?? "")).filter(Boolean),
    note: "Names only — these are what roadPrefab and buildingPrefab expect.",
  };
}

// ------------------------------------------------------------------ output shaping

/**
 * Last line of defence. Even a correct summarizer can be handed an unexpected shape, and
 * an unbounded blob in the context window is a much worse failure than a truncated one.
 */
export function encode(value: unknown, budget: number): string {
  let text = JSON.stringify(value, null, 2);
  if (text.length <= budget) return text;

  const trimmed = trimArrays(value, budget);
  text = JSON.stringify(trimmed, null, 2);
  if (text.length <= budget) return text;

  return (
    text.slice(0, budget) +
    `\n... [truncated at ${budget} characters. Narrow the query with limit/service filters, ` +
    `or ask for a specific id.]`
  );
}

function trimArrays(value: unknown, budget: number): unknown {
  const perArray = Math.max(3, Math.floor(budget / 800));

  const walk = (node: unknown): unknown => {
    if (Array.isArray(node)) {
      const kept = node.slice(0, perArray).map(walk);
      if (node.length > perArray) {
        kept.push(`... ${node.length - perArray} more rows omitted`);
      }
      return kept;
    }
    if (node && typeof node === "object") {
      const out: Record<string, unknown> = {};
      for (const [key, child] of Object.entries(node as Row)) out[key] = walk(child);
      return out;
    }
    return node;
  };

  return walk(value);
}
