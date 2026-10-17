import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { BridgeClient } from "../client.js";
import { fail, failMessage, text } from "./shared.js";

type MapRow = {
  ref?: string;
  mapName?: string;
  displayName?: string;
  packageName?: string;
  environment?: string;
  builtin?: boolean;
  workshop?: boolean;
  playable?: boolean;
  requiredDlc?: string;
  buildableArea?: number;
  connections?: Record<string, number>;
  resources?: Record<string, number>;
};

/**
 * Game lifecycle: list maps, start a new city, load a save. All three work from the main
 * menu (the mod pumps them on its own main-thread dispatcher), so an unattended supervisor can
 * start a fresh city or recover from a crash with no clicking. The mod refuses while a level
 * is loading, while saving, and while any built-in cheat mod (Unlimited Money, Unlock All,
 * Unlimited Oil And Ore, Unlimited Soil) is enabled. Nothing here enables a cheat.
 */
export function registerLifecycleTools(server: McpServer, bridge: BridgeClient) {
  server.registerTool(
    "cs1_state_maps",
    {
      description:
        "List the maps the game's New Game panel offers (works at the main menu). Each row has " +
        "`ref` (pass it to cs1_new_game), mapName, environment/theme, builtin vs workshop, whether " +
        "its DLC is owned (playable), buildableArea, and road/rail/ship/plane connection counts. " +
        "Use it before cs1_new_game; only playable maps can be started.",
      inputSchema: {
        playableOnly: z.boolean().optional().describe("Drop maps whose required DLC is not owned. Default true."),
        builtinOnly: z.boolean().optional().describe("Only the maps that ship with the game or its DLC."),
        environment: z
          .string()
          .optional()
          .describe("Only maps with this environment, e.g. Temperate, Tropical, Boreal, Winter, Europe."),
      },
    },
    async (args) => {
      try {
        const res = (await bridge.get("/state/maps")) as { maps?: MapRow[]; count?: number; levelLoaded?: boolean };
        const playableOnly = args.playableOnly ?? true;
        const env = args.environment?.toLowerCase();
        const maps = (res.maps ?? [])
          .filter((m) => !playableOnly || m.playable)
          .filter((m) => !args.builtinOnly || m.builtin)
          .filter((m) => !env || (m.environment ?? "").toLowerCase() === env)
          .map((m) => ({
            ref: m.ref,
            mapName: m.mapName,
            displayName: m.displayName,
            environment: m.environment,
            builtin: m.builtin,
            workshop: m.workshop,
            playable: m.playable,
            ...(m.playable ? {} : { requiredDlc: m.requiredDlc }),
            buildableArea: m.buildableArea,
            connections: m.connections,
          }));
        return text(JSON.stringify({ levelLoaded: res.levelLoaded, total: res.count, returned: maps.length, maps }));
      } catch (error) {
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_new_game",
    {
      description:
        "Start a brand-new city on a map, exactly like the New Game panel's Start button. Only " +
        "from the main menu: refused if a city is loaded or loading, or if a cheat mod is enabled. " +
        "Run with dryRun:true first to resolve the map. Returns as soon as loading starts; poll " +
        "cs1_health until levelLoaded is true (about a minute) before any other tool.",
      inputSchema: {
        map: z.string().min(1).describe("Map ref from cs1_state_maps (preferred), or its mapName/displayName."),
        cityName: z
          .string()
          .trim()
          .min(1)
          .max(64)
          // The mod's JSON reader stops at the first quote, so a quote or backslash would
          // silently truncate the name.
          .regex(/^[^"\\]+$/, "cityName may not contain quotes or backslashes.")
          .describe("Name for the new city."),
        invertTraffic: z
          .boolean()
          .optional()
          .describe("Left-hand traffic. Default false, the New Game panel's unchecked default."),
        dryRun: z.boolean().optional().describe("Resolve and validate only; do not start loading."),
      },
    },
    async (args) => {
      try {
        const result = await bridge.post("/commands/new-game", {
          map: args.map,
          cityName: args.cityName,
          invertTraffic: args.invertTraffic ?? false,
          dryRun: args.dryRun ?? false,
        });
        return text(JSON.stringify(result));
      } catch (error) {
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_load_save",
    {
      description:
        "Load a saved city by name, like the Load panel. Works from the main menu (crash " +
        "recovery) or in game (switches city; unsaved progress is lost). Refused while loading, " +
        "while saving, or if a cheat mod is enabled. Names match cs1_state_saves. Run dryRun:true " +
        "first. Returns as soon as loading starts; poll cs1_health until levelLoaded is true, then " +
        "call cs1_city_context.",
      inputSchema: {
        name: z.string().min(1).describe("Save name, e.g. AgentAutoSave."),
        dryRun: z.boolean().optional().describe("Resolve and validate only; do not start loading."),
      },
    },
    async (args) => {
      if (args.name.includes("/") || args.name.includes("\\")) {
        return failMessage("name is a save name, not a path.");
      }
      try {
        const result = await bridge.post("/commands/load-save", { name: args.name, dryRun: args.dryRun ?? false });
        return text(JSON.stringify(result));
      } catch (error) {
        return fail(error);
      }
    },
  );
}
