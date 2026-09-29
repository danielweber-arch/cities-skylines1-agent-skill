import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { BridgeClient } from "../client.js";
import {
  FULL_CHAR_BUDGET,
  SUMMARY_CHAR_BUDGET,
  encode,
  summarizeAnomalies,
  summarizeFacilities,
  summarizeGrowables,
  summarizeNetworks,
  summarizePrefabs,
  summarizeProblems,
} from "../filters.js";
import { fail, text } from "./shared.js";

const detail = z
  .enum(["summary", "full"])
  .optional()
  .describe(
    "summary (default) returns counts plus only the rows you can act on. full returns every " +
      "row and is expensive — tens of thousands of tokens on a developed city. Use full only " +
      "when you need a specific field the summary drops.",
  );

type Summarizer = (payload: unknown) => unknown;

/** Wires one read endpoint as a tool, with the summary/full split applied consistently. */
function registerRead(
  server: McpServer,
  bridge: BridgeClient,
  options: {
    name: string;
    path: string;
    description: string;
    summarize?: Summarizer;
    extraSchema?: z.ZodRawShape;
    buildQuery?: (args: Record<string, unknown>) => Record<string, unknown>;
  },
) {
  const inputSchema: z.ZodRawShape = options.summarize
    ? { detail, ...(options.extraSchema ?? {}) }
    : { ...(options.extraSchema ?? {}) };

  server.registerTool(
    options.name,
    { description: options.description, inputSchema },
    async (args: Record<string, unknown>) => {
      try {
        const query = options.buildQuery ? options.buildQuery(args ?? {}) : {};
        const payload = await bridge.get(options.path, query);
        const wantFull = (args?.detail ?? "summary") === "full";

        if (!options.summarize || wantFull) {
          return text(encode(payload, wantFull ? FULL_CHAR_BUDGET : SUMMARY_CHAR_BUDGET));
        }

        return text(encode(options.summarize(payload), SUMMARY_CHAR_BUDGET));
      } catch (error) {
        return fail(error);
      }
    },
  );
}

export function registerStateTools(server: McpServer, bridge: BridgeClient) {
  registerRead(server, bridge, {
    name: "cs1_health",
    path: "/health",
    description:
      "Check that the bridge is reachable and whether a city is loaded. Answers from the main " +
      "menu too, so use it to confirm the mod is running before anything else. levelLoaded:false " +
      "means every other tool will refuse with 'No city is loaded'.",
  });

  registerRead(server, bridge, {
    name: "cs1_state_summary",
    path: "/state/summary",
    description:
      "One-screen city snapshot: game time, node/segment/lane counts, citizen count, and the " +
      "three demand bars. Start every inspection here.",
  });

  registerRead(server, bridge, {
    name: "cs1_state_demand",
    path: "/state/demand",
    description:
      "Residential, commercial, and workplace demand, 0..100. Zone toward whichever bar is high; " +
      "zoning against demand produces empty lots and wasted money.",
  });

  registerRead(server, bridge, {
    name: "cs1_state_economy",
    path: "/state/economy",
    description:
      "Current tax rates per zoned sub-service and level, mirroring the six budget sliders. " +
      "Check this before changing taxes so you know the baseline you are moving from.",
  });

  registerRead(server, bridge, {
    name: "cs1_state_zones",
    path: "/state/zones",
    description:
      "Zoning cell counts and approximate area per zone type. Use it to see the residential / " +
      "commercial / industrial balance without listing individual blocks.",
  });

  registerRead(server, bridge, {
    name: "cs1_state_problems",
    path: "/state/problems",
    description:
      "Every in-game problem icon, read from simulation data rather than screenshots. The summary " +
      "groups identical problems — twenty buildings with NoPower is one line, not twenty.",
    summarize: summarizeProblems,
    extraSchema: { limit: z.number().int().min(1).max(2000).optional() },
    buildQuery: (args) => ({ limit: args.limit ?? 200 }),
  });

  registerRead(server, bridge, {
    name: "cs1_state_chirps",
    path: "/state/chirps",
    description:
      "Recent Chirper messages. Citizen sentiment about taxes, traffic, and services, without OCR.",
    extraSchema: { limit: z.number().int().min(1).max(200).optional() },
    buildQuery: (args) => ({ limit: args.limit ?? 50 }),
  });

  registerRead(server, bridge, {
    name: "cs1_state_facilities",
    path: "/state/facilities",
    description:
      "Service buildings. The summary returns counts by service plus only the facilities that " +
      "need attention (inactive, blocked asset, or flagged with a problem).",
    summarize: summarizeFacilities,
    extraSchema: {
      limit: z.number().int().min(1).max(2000).optional(),
      service: z
        .string()
        .optional()
        .describe("Filter by ItemClass service, e.g. Electricity, Water, HealthCare, Garbage."),
      includeMapObjects: z
        .boolean()
        .optional()
        .describe(
          "Include raw helper objects such as pipe junctions. Only needed when diagnosing why a " +
            "service building will not connect.",
        ),
    },
    buildQuery: (args) => ({
      limit: args.limit ?? 500,
      service: args.service,
      includeMapObjects: args.includeMapObjects ? "true" : undefined,
    }),
  });

  registerRead(server, bridge, {
    name: "cs1_state_growables",
    path: "/state/growables",
    description:
      "Buildings that grew on zoned land. Check this before repainting a zone — the summary " +
      "reports how much is already developed and which buildings are abandoned or in trouble.",
    summarize: summarizeGrowables,
    extraSchema: {
      limit: z.number().int().min(1).max(2000).optional(),
      service: z.string().optional(),
    },
    buildQuery: (args) => ({ limit: args.limit ?? 500, service: args.service }),
  });

  registerRead(server, bridge, {
    name: "cs1_state_networks",
    path: "/state/networks",
    description:
      "Network segments. The summary returns counts by prefab, the bounding box, and any segment " +
      "that is dangling or flagged — never the whole road network, which is enormous.",
    summarize: summarizeNetworks,
    extraSchema: {
      limit: z.number().int().min(1).max(4000).optional(),
      service: z.string().optional().describe("e.g. Road, Water, Electricity, PublicTransport."),
    },
    buildQuery: (args) => ({ limit: args.limit ?? 1000, service: args.service }),
  });

  registerRead(server, bridge, {
    name: "cs1_state_road_anomalies",
    path: "/state/road-anomalies",
    description:
      "Roads that look connected but are not: near-miss nodes, crossings without a shared node, " +
      "suspiciously short segments, disconnected components. This is the acceptance check after " +
      "any road building — a healthy build returns zero. Dead ends are not errors; leave " +
      "includeDeadEnds false unless you are specifically hunting stubs.",
    summarize: summarizeAnomalies,
    extraSchema: {
      limit: z.number().int().min(1).max(2000).optional(),
      nearMissDistance: z.number().optional(),
      shortSegmentLength: z.number().optional(),
      includeDeadEnds: z.boolean().optional(),
    },
    buildQuery: (args) => ({
      limit: args.limit ?? 500,
      nearMissDistance: args.nearMissDistance,
      shortSegmentLength: args.shortSegmentLength,
      includeDeadEnds: args.includeDeadEnds === true ? "true" : "false",
    }),
  });

  registerRead(server, bridge, {
    name: "cs1_state_building_anomalies",
    path: "/state/building-anomalies",
    description: "Buildings placed where they overlap roads or otherwise sit wrong.",
    summarize: summarizeAnomalies,
    extraSchema: { limit: z.number().int().min(1).max(2000).optional() },
    buildQuery: (args) => ({ limit: args.limit ?? 200 }),
  });

  registerRead(server, bridge, {
    name: "cs1_state_zone_anomalies",
    path: "/state/zone-anomalies",
    description:
      "Blocks with mixed or missing zoning — the cause of mottled blue/green/yellow blocks. " +
      "Repair what this reports with cs1_repair_zone_clusters.",
    summarize: summarizeAnomalies,
    extraSchema: {
      limit: z.number().int().min(1).max(2000).optional(),
      includeUnzonedHoles: z.boolean().optional(),
    },
    buildQuery: (args) => ({
      limit: args.limit ?? 200,
      includeUnzonedHoles: args.includeUnzonedHoles === false ? "false" : "true",
    }),
  });

  registerRead(server, bridge, {
    name: "cs1_state_external_connections",
    path: "/state/external-connections",
    description:
      "Highway, rail, ship, and air connections to outside the map. A city with no working road " +
      "connection will not grow no matter how well it is zoned.",
    summarize: summarizeAnomalies,
    extraSchema: { limit: z.number().int().min(1).max(500).optional() },
    buildQuery: (args) => ({ limit: args.limit ?? 50 }),
  });

  registerRead(server, bridge, {
    name: "cs1_state_areas",
    path: "/state/areas",
    description:
      "Map tiles (the 5x5 grid of 1920 m tiles): owned, purchasable now, world bounds, and the " +
      "price of buying each one next (game cents; priceDisplay is in the UI's money). Also the " +
      "owned count, the max-area cap (maxAreaCount), and whether the next area milestone is " +
      "reached. Read this before cs1_unlock_area.",
  });

  registerRead(server, bridge, {
    name: "cs1_state_saves",
    path: "/state/saves",
    description:
      "Local .crp save files with timestamps and sizes. Poll this after cs1_save to confirm the " +
      "file actually landed — the save command returns before the write completes.",
  });

  registerRead(server, bridge, {
    name: "cs1_prefabs_roads",
    path: "/prefabs/roads",
    description:
      "Exact road prefab names. roadPrefab arguments must match one of these strings, so read " +
      "this before the first build rather than guessing at 'Basic Road'.",
    summarize: summarizePrefabs,
  });

  registerRead(server, bridge, {
    name: "cs1_prefabs_networks",
    path: "/prefabs/networks",
    description: "Network prefab names, optionally filtered by service (Water, Electricity, ...).",
    summarize: summarizePrefabs,
    extraSchema: { service: z.string().optional() },
    buildQuery: (args) => ({ service: args.service }),
  });

  registerRead(server, bridge, {
    name: "cs1_prefabs_buildings",
    path: "/prefabs/buildings",
    description:
      "Building prefab names, optionally filtered by service. buildingPrefab arguments must " +
      "match one of these exactly. Known-broken assets are already excluded.",
    summarize: summarizePrefabs,
    extraSchema: { service: z.string().optional() },
    buildQuery: (args) => ({ service: args.service }),
  });
}
