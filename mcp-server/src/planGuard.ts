/**
 * Wires checker.ts's pure rules to the live bridge: builds the `io` fetchers, resolves which
 * city.md to read standing orders from, and gives commands.ts/transit.ts/stamp.ts one helper
 * to convert their args into a Plan and enforce it before the bridge is ever called.
 */
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import type { BridgeClient } from "./client.js";
import { checkPlan, type CheckerIO, type CheckResult, type Plan } from "./checker.js";
import { resolveCityDir, type CityIdentity } from "./cityContext.js";

export function buildCheckerIO(bridge: BridgeClient): CheckerIO {
  return {
    terrainPoints: async (points) => {
      const query = { points: points.map((p) => `${p.x},${p.z}`).join(";") };
      const payload = (await bridge.get("/state/terrain", query)) as { samples?: unknown[] };
      return (payload.samples ?? []) as any;
    },
    networksRoad: async () => {
      const payload = (await bridge.get("/state/networks", { service: "Road", limit: 5000 })) as {
        total?: number;
        returned?: number;
        segments?: unknown[];
      };
      return {
        total: payload.total ?? 0,
        returned: payload.returned ?? (payload.segments ?? []).length,
        segments: (payload.segments ?? []) as any,
      };
    },
    growablesResidential: async () => {
      const payload = (await bridge.get("/state/growables", { service: "Residential", limit: 5000 })) as {
        total?: number;
        returned?: number;
        growables?: unknown[];
      };
      return {
        total: payload.total ?? 0,
        returned: payload.returned ?? (payload.growables ?? []).length,
        growables: (payload.growables ?? []) as any,
      };
    },
    facilitiesWater: async () => {
      const payload = (await bridge.get("/state/facilities", { service: "Water", limit: 5000 })) as {
        total?: number;
        returned?: number;
        facilities?: unknown[];
      };
      return {
        total: payload.total ?? 0,
        returned: payload.returned ?? (payload.facilities ?? []).length,
        facilities: (payload.facilities ?? []) as any,
      };
    },
    terrainGrid: async (x, z, radius, cell) => {
      return bridge.get("/state/terrain/grid", { x, z, radius, cell });
    },
  };
}

/**
 * `CS1_CITY_FILE` env overrides; else the loaded city's cities/<slug>/city.md. There is
 * deliberately NO fallback to a repo-root city.md: that file is a 3-line stub and could belong
 * to a different city than the one currently loaded, so a missing per-city file means empty
 * standing orders, not someone else's. `source` always describes what was used (or why not),
 * so the caller can see it in CheckResult.sources.standingOrders.
 */
export async function loadStandingOrders(bridge: BridgeClient): Promise<{ markdown?: string; source: string }> {
  if (process.env.CS1_CITY_FILE) {
    try {
      return { markdown: readFileSync(process.env.CS1_CITY_FILE, "utf8"), source: `CS1_CITY_FILE: ${process.env.CS1_CITY_FILE}` };
    } catch {
      return { source: `none: CS1_CITY_FILE unreadable (${process.env.CS1_CITY_FILE})` };
    }
  }
  try {
    const health = (await bridge.get("/health")) as { city?: CityIdentity };
    const resolved = resolveCityDir(health?.city ?? null);
    if (resolved) {
      const cityMd = join(resolved.dir, "city.md");
      if (existsSync(cityMd)) {
        try {
          return { markdown: readFileSync(cityMd, "utf8"), source: `city dir: ${cityMd}` };
        } catch {
          return { source: `none: city.md unreadable (${cityMd})` };
        }
      }
      return { source: "none: city dir resolved but has no city.md" };
    }
  } catch {
    /* fall through to "no city dir resolved" */
  }
  return { source: "none: no city context dir resolved" };
}

/**
 * Runs the checker for a plan built from mutation-tool args. Returns the CheckResult; callers
 * decide what to do (HARD => block, ADVISORY => attach to the response). No opt-out for HARD.
 * The city's own standing orders are always loaded and applied - a caller-supplied
 * `plan.standingOrders` is unioned on top inside checkPlan, never a replacement.
 */
export async function enforcePlan(plan: Plan, bridge: BridgeClient): Promise<CheckResult> {
  const io = buildCheckerIO(bridge);
  const { markdown, source } = await loadStandingOrders(bridge);
  return checkPlan(plan, io, markdown, source);
}

export function blockedMessage(result: CheckResult): string {
  // Compact on purpose: this string is what the model reads. Per rule, the first 5 findings
  // (with their count when samples were collapsed), then how many more; the citation once.
  const byRule = new Map<string, typeof result.hard>();
  for (const h of result.hard) {
    const list = byRule.get(h.rule) ?? [];
    list.push(h);
    byRule.set(h.rule, list);
  }
  const parts: string[] = [];
  for (const [rule, list] of byRule) {
    const shown = list.slice(0, 5).map((h) => (h.count && h.count > 1 ? `${h.msg} (x${h.count} samples)` : h.msg));
    const more = list.length > 5 ? `; +${list.length - 5} more ${rule}` : "";
    const cite = result.citations?.[rule] ? ` [${result.citations[rule]}]` : "";
    parts.push(`${rule} (${list.length}): ${shown.join("; ")}${more}${cite}`);
  }
  return `plan check blocked: ${parts.join(" | ")}. Re-plan; the tools cannot override a HARD rule.`;
}
