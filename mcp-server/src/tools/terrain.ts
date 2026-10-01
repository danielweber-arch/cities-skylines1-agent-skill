import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { BridgeClient } from "../client.js";
import { capJson, fail, text } from "./shared.js";
import { resolveCityDir, writeCacheFile, type CityIdentity } from "../cityContext.js";

const TERRAIN_MAP_CAP = 6000;
const ROUTE_SHARE_CAP = 2000;

async function cityCacheContext(bridge: BridgeClient): Promise<{ resolved: ReturnType<typeof resolveCityDir>; gameDate?: string; population?: number }> {
  try {
    const health = (await bridge.get("/health")) as { city?: CityIdentity };
    const city = health?.city ?? null;
    const resolved = resolveCityDir(city);
    return { resolved, gameDate: city?.gameDate, population: city?.population };
  } catch {
    return { resolved: null };
  }
}

export function registerTerrainTools(server: McpServer, bridge: BridgeClient) {
  server.registerTool(
    "cs1_terrain_map",
    {
      description:
        "ASCII terrain/water grid centred on a point: '.' dry buildable, '^' dry too steep, " +
        "'~' water, '?' mixed shore. Also reports flow regions for large water bodies (indicative " +
        "surface velocity only). Use before planning a wide area instead of many cs1_terrain_sample " +
        "calls. Cached to the loaded city's cache/ dir when one resolves.",
      inputSchema: {
        x: z.number().describe("World X centre in metres."),
        z: z.number().describe("World Z centre in metres."),
        radius: z.number().min(16).max(2048).optional().describe("Metres, default 512."),
        cell: z.number().min(16).optional().describe("Cell size in metres, default 64."),
        steep: z.number().optional().describe("Grade percent threshold for '^', default 8."),
      },
    },
    async (args) => {
      try {
        const payload = (await bridge.get("/state/terrain/grid", {
          x: args.x,
          z: args.z,
          radius: args.radius,
          cell: args.cell,
          steep: args.steep,
        })) as Record<string, unknown>;

        const { resolved, gameDate, population } = await cityCacheContext(bridge);
        const cached = resolved
          ? writeCacheFile(resolved, `terrain-map-${args.x}_${args.z}_${args.radius ?? 512}_${args.cell ?? 64}.json`, payload, { gameDate, population })
          : null;
        // flow goes first (indicative anyway), then the legend; the rows grid is the payload.
        return text(capJson(cached ? { ...payload, cached } : payload, TERRAIN_MAP_CAP, ["flow", "flowNote", "flowTruncated", "legend", "rowOrder"]));
      } catch (error) {
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_segment_route_share",
    {
      description:
        "Vehicles whose CURRENT remaining route includes a road segment, at this instant. Not " +
        "throughput. Give segmentId, or x/z/radius to pick the busiest segment in an area. Cached " +
        "to the loaded city's cache/ dir when one resolves.",
      inputSchema: {
        segmentId: z.number().int().optional(),
        x: z.number().optional(),
        z: z.number().optional(),
        radius: z.number().optional(),
        limit: z.number().int().min(1).max(50).optional().describe("Top OD pairs, default 10."),
        budget: z.number().int().min(1).max(1_000_000).optional().describe("Path units walked, default 150000."),
      },
    },
    async (args) => {
      try {
        if (args.segmentId === undefined && (args.x === undefined || args.z === undefined)) {
          return fail(new Error("Give segmentId, or x and z (with optional radius)."));
        }
        const payload = (await bridge.get("/state/segment-route-share", {
          segment: args.segmentId,
          x: args.x,
          z: args.z,
          radius: args.radius,
          limit: args.limit,
          budget: args.budget,
        })) as Record<string, unknown>;

        const { resolved, gameDate, population } = await cityCacheContext(bridge);
        const segId = (payload.segment as Record<string, unknown> | undefined)?.id ?? args.segmentId ?? "area";
        const cached = resolved ? writeCacheFile(resolved, `route-share-${segId}.json`, payload, { gameDate, population }) : null;
        // Trim topPairs entry by entry first (the only open-ended list), then let capJson drop
        // optional fields; it never slices JSON.
        let working: Record<string, unknown> = cached ? { ...payload, cached } : { ...payload };
        if (JSON.stringify(working).length > ROUTE_SHARE_CAP && Array.isArray(working.topPairs)) {
          const pairs = working.topPairs as unknown[];
          let kept = pairs.length;
          while (kept > 0 && JSON.stringify(working).length > ROUTE_SHARE_CAP) {
            kept--;
            working = { ...working, topPairs: pairs.slice(0, kept), pairsTruncated: pairs.length - kept };
          }
        }
        return text(capJson(working, ROUTE_SHARE_CAP, ["topPairs", "meaning", "byClass"]));
      } catch (error) {
        return fail(error);
      }
    },
  );
}
