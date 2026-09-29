import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { BridgeClient } from "../client.js";
import {
  FULL_CHAR_BUDGET,
  SUMMARY_CHAR_BUDGET,
  TRANSIT_SUMMARY_CHAR_BUDGET,
  encode,
  summarizeTraffic,
  summarizeTransit,
} from "../filters.js";
import { fail, text } from "./shared.js";

/** TransportInfo.TransportType names, as the mod accepts them for ?type= filtering. */
export const TRANSPORT_TYPES = [
  "Bus",
  "Metro",
  "Train",
  "Ship",
  "Airplane",
  "Taxi",
  "Tram",
  "EvacuationBus",
  "Monorail",
  "CableCar",
  "Pedestrian",
  "TouristBus",
  "HotAirBalloon",
  "Post",
  "Trolleybus",
  "Fishing",
  "Helicopter",
] as const;

/** The subset a player draws as a free-standing line; the mod refuses the others. */
export const CREATABLE_TRANSPORT_TYPES = [
  "Bus",
  "Metro",
  "Train",
  "Ship",
  "Airplane",
  "Tram",
  "Monorail",
  "CableCar",
  "Trolleybus",
] as const;

const detail = z
  .enum(["summary", "full"])
  .optional()
  .describe(
    "summary (default) returns one compact row per line plus totals. full returns the raw " +
      "payload (stops, facilities, prefabs) and can be large on a developed city.",
  );

const dryRun = z
  .boolean()
  .optional()
  .describe("Validate and snap everything, report exactly what would happen, change nothing.");

const stopPoint = z.object({
  x: z.number().describe("World X in metres."),
  z: z.number().describe("World Z in metres."),
});

const hexColor = z
  .string()
  .regex(/^#[0-9a-fA-F]{6}$/, "color must be #RRGGBB")
  .describe("Line colour as #RRGGBB.");

const lineId = z.number().int().min(1).max(255).describe("Line id from cs1_state_transit.");

const lineBudget = z
  .number()
  .int()
  .min(0)
  .max(500)
  .describe("Per-line vehicle budget percent (TransportLine.m_budget; the line panel slider). 100 = default.");

const snapDistances = {
  roadSnapDistance: z
    .number()
    .min(1)
    .max(128)
    .optional()
    .describe("Road-based types: how far (m) to look for a stop-capable segment. Default 32."),
  stationSnapDistance: z
    .number()
    .min(1)
    .max(256)
    .optional()
    .describe("Station-based types: how far (m) from a station building's centre. Default 64."),
};

export function registerTransitTools(server: McpServer, bridge: BridgeClient) {
  const run = async (path: string, body: unknown) => {
    try {
      return text(encode(await bridge.post(path, body), SUMMARY_CHAR_BUDGET));
    } catch (error) {
      return fail(error);
    }
  };

  server.registerTool(
    "cs1_state_transit",
    {
      description:
        "Public transport review: every line (type, stops, vehicles vs the game's target " +
        "vehicle count, per-line budget, weekly passengers, stop problems such as " +
        "LineNotConnected), totals per transport type, the city passenger counters from the " +
        "Public Transport info view, transit budgets, and station/depot counts. Start every " +
        "transit review here. detail:'full' plus includeStops:true adds each stop's position " +
        "and waiting passengers.",
      inputSchema: {
        detail,
        type: z.enum(TRANSPORT_TYPES).optional().describe("Only this transport type."),
        includeStops: z.boolean().optional().describe("Include per-stop rows (only visible with detail:'full')."),
        limit: z.number().int().min(1).max(256).optional().describe("Max lines returned, default 256."),
      },
    },
    async (args) => {
      try {
        const payload = await bridge.get("/state/transit", {
          type: args.type,
          includeStops: args.includeStops ? "true" : undefined,
          limit: args.limit,
        });
        if ((args.detail ?? "summary") === "full") {
          return text(encode(payload, FULL_CHAR_BUDGET));
        }
        return text(encode(summarizeTransit(payload), TRANSIT_SUMMARY_CHAR_BUDGET));
      } catch (error) {
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_state_traffic",
    {
      description:
        "Most congested road segments by the game's traffic density (0..100, what the Traffic " +
        "overlay colours), plus the city average and the info view's traffic-flow percent. " +
        "Use it to find where a bus lane, tram or metro line would take cars off the road.",
      inputSchema: {
        detail,
        limit: z.number().int().min(1).max(2000).optional().describe("Top N segments, default 50."),
        minDensity: z.number().int().min(0).max(100).optional().describe("Only segments at or above this density."),
        x: z.number().optional().describe("Area centre X (needs z)."),
        z: z.number().optional().describe("Area centre Z (needs x)."),
        radius: z.number().positive().max(10000).optional().describe("Area radius in metres, default 500."),
      },
    },
    async (args) => {
      try {
        if ((args.x === undefined) !== (args.z === undefined)) {
          return fail(new Error("x and z must be given together."));
        }
        const payload = await bridge.get("/state/traffic", {
          limit: args.limit ?? 50,
          minDensity: args.minDensity,
          x: args.x,
          z: args.z,
          radius: args.radius,
        });
        if ((args.detail ?? "summary") === "full") {
          return text(encode(payload, FULL_CHAR_BUDGET));
        }
        return text(encode(summarizeTraffic(payload), SUMMARY_CHAR_BUDGET));
      } catch (error) {
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_state_policies",
    {
      description:
        "Active city-wide policies, per-district policies, and every policy this game can set " +
        "(with whether it is unlocked). Read before cs1_set_policy.",
      inputSchema: {},
    },
    async () => {
      try {
        return text(encode(await bridge.get("/state/policies"), SUMMARY_CHAR_BUDGET * 2));
      } catch (error) {
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_transit_line_create",
    {
      description:
        "Create a closed public transport line from ordered points. Each point snaps the way the " +
        "in-game line tool does: road types (Bus, Trolleybus, Tram) to the middle of the nearest " +
        "stop-capable segment; station types (Metro, Train, Monorail, Ship, Airplane, CableCar) " +
        "to a platform of the nearest matching station building. The line closes back to the " +
        "first stop automatically, so do not repeat it. Any failure rolls the line back. Run " +
        "with dryRun first to see snapping distances. Paths are computed after creation: re-check " +
        "cs1_state_transit for LineNotConnected before relying on the line.",
      inputSchema: {
        transportType: z
          .enum(CREATABLE_TRANSPORT_TYPES)
          .optional()
          .describe("Uses the game's default prefab for the type. Give this or prefab."),
        prefab: z
          .string()
          .optional()
          .describe("Exact TransportInfo name from cs1_state_transit detail:'full' transportPrefabs (e.g. ferry vs ship)."),
        stops: z.array(stopPoint).min(2).max(64).describe("Stops in travel order; at least 2."),
        name: z.string().max(64).optional(),
        color: hexColor.optional(),
        budget: lineBudget.optional(),
        ignoreUnlock: z
          .boolean()
          .optional()
          .describe(
            "Skip the bridge's unlock check (the line-tool milestone the game's Public Transport panel " +
              "tests). A refusal names the milestone and its requirement (e.g. a depot); the response " +
              "says unlockIgnored:true when the check was skipped. Default false.",
          ),
        ...snapDistances,
        dryRun,
      },
    },
    async (args) => {
      if (!args.transportType && !args.prefab) {
        return fail(new Error("Give transportType or prefab."));
      }
      return run("/commands/transit-line-create", args);
    },
  );

  server.registerTool(
    "cs1_transit_line_edit",
    {
      description:
        "Change a line: name, color, per-line budget, ticket price, and stops. Order of stop " +
        "edits: removals (by current index, applied highest first), then moves (indexes after " +
        "removals), then adds (insert before the stop at index; -1 or omitted appends). Every " +
        "point is snapped and validated before anything changes, but if the game rejects a step " +
        "midway the earlier steps stay applied; the response lists what was applied.",
      inputSchema: {
        lineId,
        name: z.string().max(64).optional().describe("Empty string restores the generated name."),
        color: hexColor.optional(),
        budget: lineBudget.optional(),
        ticketPrice: z
          .number()
          .int()
          .min(0)
          .max(65535)
          .optional()
          .describe("Cents (TransportLine.m_ticketPrice). The game only exposes this slider for tourist buses."),
        removeStopIndexes: z.array(z.number().int().min(0)).max(64).optional(),
        moveStops: z
          .array(stopPoint.extend({ index: z.number().int().min(0) }))
          .max(64)
          .optional(),
        addStops: z
          .array(stopPoint.extend({ index: z.number().int().min(-1).optional() }))
          .max(64)
          .optional(),
        ...snapDistances,
        dryRun,
      },
    },
    async (args) => run("/commands/transit-line-edit", args),
  );

  server.registerTool(
    "cs1_transit_line_delete",
    {
      description:
        "Delete a transport line and its stops; its vehicles return to the depot. Irreversible " +
        "outside a save reload, so dryRun first and confirm the id with cs1_state_transit.",
      inputSchema: { lineId, dryRun },
    },
    async (args) => run("/commands/transit-line-delete", args),
  );

  server.registerTool(
    "cs1_set_service_budget",
    {
      description:
        "Set a service budget slider (day and/or night, 0..150%) via EconomyManager. " +
        "service PublicTransport without subService sets every transit sub-service at once, the " +
        "way the budget panel's parent slider does. dryRun returns the current values.",
      inputSchema: {
        service: z.string().describe("ItemClass service, e.g. PublicTransport."),
        subService: z
          .string()
          .optional()
          .describe("e.g. PublicTransportBus, PublicTransportMetro, PublicTransportTrain."),
        day: z.number().int().min(0).max(150).optional(),
        night: z.number().int().min(0).max(150).optional(),
        dryRun,
      },
    },
    async (args) => {
      if (!args.dryRun && args.day === undefined && args.night === undefined) {
        return fail(new Error("Give day and/or night, or dryRun:true to read the current budget."));
      }
      return run("/commands/set-service-budget", args);
    },
  );

  server.registerTool(
    "cs1_set_policy",
    {
      description:
        "Enable or disable a policy city-wide (districtId 0, default) or in one district, e.g. " +
        "FreeTransport, HighTicketPrices, EncourageBiking, HeavyTrafficBan. Names come from " +
        "cs1_state_policies. Returns before/after state.",
      inputSchema: {
        policy: z.string().min(1).describe("DistrictPolicies.Policies name, e.g. FreeTransport."),
        districtId: z.number().int().min(0).max(127).optional().describe("0 = whole city (default)."),
        enabled: z.boolean(),
        dryRun,
      },
    },
    async (args) => run("/commands/set-policy", args),
  );
}
