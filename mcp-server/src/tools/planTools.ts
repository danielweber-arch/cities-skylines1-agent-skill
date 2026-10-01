import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { BridgeClient } from "../client.js";
import { capJson, fail, failMessage, text } from "./shared.js";
import { enforcePlan } from "../planGuard.js";
import { stampLayout } from "../stamp.js";
import type { Plan } from "../checker.js";
import { CityGuardError, guardSessionCity } from "../cityContext.js";

const point = z.object({ x: z.number(), z: z.number(), elevation: z.number().optional() });

const noBuildArea = z.object({
  name: z.string(),
  polygon: z.array(z.object({ x: z.number(), z: z.number() })).optional(),
  bbox: z.object({ minX: z.number(), minZ: z.number(), maxX: z.number(), maxZ: z.number() }).optional(),
});

const standingOrders = z.object({
  noBuild: z.array(noBuildArea),
  maxSpend: z.number().optional(),
});

const planSchema = z.object({
  roads: z.array(z.object({ prefab: z.string(), points: z.array(point).min(2), name: z.string().optional() })).optional(),
  zones: z.array(z.object({ zone: z.string(), center: z.object({ x: z.number(), z: z.number() }), radius: z.number() })).optional(),
  buildings: z
    .array(
      z.object({
        prefab: z.string(),
        position: z.object({ x: z.number(), z: z.number() }),
        angleDegrees: z.number().optional(),
        widthCells: z.number().optional(),
        lengthCells: z.number().optional(),
      }),
    )
    .optional(),
  standingOrders: standingOrders.optional(),
});

export function registerPlanCheckTools(server: McpServer, bridge: BridgeClient) {
  server.registerTool(
    "cs1_check_plan",
    {
      description:
        "Pure plan checker: run HARD and ADVISORY rules (water, standing-order no-build, metro/train " +
        "turn angle, road crossings, short segments, grade, zone buffers, sewage flow) against a " +
        "plan before building it. Returns the verdict only; the bridge is never called. Every " +
        "cs1_build_* / cs1_connect / cs1_place_building / cs1_set_zone / cs1_stamp_layout tool " +
        "already runs this inline and blocks on HARD findings, so call this directly only to " +
        "preview a plan or to see ADVISORY findings before committing.",
      inputSchema: { plan: planSchema },
    },
    async (args) => {
      try {
        const result = await enforcePlan(args.plan as Plan, bridge);
        // hard is never dropped; advisory goes first, then the bookkeeping fields.
        return text(capJson(result as unknown as Record<string, unknown>, 4000, ["advisory", "sources", "checked", "citations"]));
      } catch (error) {
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_stamp_layout",
    {
      description:
        "Stamp a layout template (templates/layouts/<name>.json) at an anchor and rotation: builds " +
        "the plan, runs cs1_check_plan (HARD blocks, no opt-out), dry-runs the batch, then builds " +
        "for real and reports a partial-build report on any failure plus road anomalies inside the " +
        "stamped footprint. World point = anchor + rotate(local, angleDeg) with x' = x*cos(a)+z*sin(a), " +
        "z' = -x*sin(a)+z*cos(a), a in radians (same convention as angleDegrees elsewhere).",
      inputSchema: {
        name: z.string().describe("Template name (file templates/layouts/<name>.json, no extension)."),
        anchor: z.object({ x: z.number(), z: z.number() }),
        angleDeg: z.number(),
        roads: z.record(z.string(), z.string()).optional().describe("Role name -> prefab override."),
        dryRun: z.boolean().optional(),
      },
    },
    async (args) => {
      try {
        const guard = await guardSessionCity(bridge);
        const result = await stampLayout(args as any, bridge);
        if (guard.note) (result as Record<string, unknown>).city = guard.city;
        if (!result.ok && typeof result.error === "string" && result.error.startsWith("plan check blocked")) {
          return failMessage(result.error);
        }
        return text(capJson(result as unknown as Record<string, unknown>, 3000, ["dryRunItems", "advisory", "waterCheck", "footprint"]));
      } catch (error) {
        return fail(error);
      }
    },
  );
}
