import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { BridgeClient } from "../client.js";
import { SUMMARY_CHAR_BUDGET, encode } from "../filters.js";
import { ZONE_NAMES, fail, image, text } from "./shared.js";

const point = z.object({
  x: z.number().describe("World X in metres."),
  z: z.number().describe("World Z in metres."),
});

const opId = z
  .string()
  .optional()
  .describe(
    "Idempotency key. If the call times out, retry with the same opId and the cached manifest " +
      "is returned instead of building a second overlapping copy. Always set one for grids and " +
      "neighborhoods.",
  );

const dryRun = z
  .boolean()
  .optional()
  .describe("Validate and return the plan (including blockCenters) without changing the city.");

export function registerCommandTools(server: McpServer, bridge: BridgeClient) {
  const run = async (path: string, body: unknown) => {
    try {
      return text(encode(await bridge.post(path, body), SUMMARY_CHAR_BUDGET));
    } catch (error) {
      return fail(error);
    }
  };

  // ------------------------------------------------------------ composite builders

  server.registerTool(
    "cs1_build_grid",
    {
      description:
        "Build a whole rectangular road lattice in one call: (cols+1)x(rows+1) nodes and every " +
        "segment between them. Nodes within snapDistance of existing geometry are reused, so the " +
        "grid joins the network properly instead of overlapping it. Returns blockCenters — the " +
        "coordinates of each city block — which is what you pass to cs1_set_zone next, so you " +
        "never have to do the arithmetic yourself. Verify with cs1_state_road_anomalies " +
        "afterwards; a correct build reports zero.",
      inputSchema: {
        roadPrefab: z.string().describe("Exact name from cs1_prefabs_roads, e.g. 'Basic Road'."),
        origin: point.describe("The lattice corner. Cells grow toward +x and +z from here."),
        cols: z.number().int().min(1).max(20).describe("Blocks across. cols*rows must be <= 400."),
        rows: z.number().int().min(1).max(20).describe("Blocks deep. cols*rows must be <= 400."),
        spacing: z
          .number()
          .min(32)
          .max(256)
          .optional()
          .describe(
            "Metres between parallel roads, default 80. CS1 zoning cells are 8m and zoneable " +
              "depth is 4 cells per side, so 80 fills the block from both sides. Larger values " +
              "leave an unzoneable dead strip down the middle.",
          ),
        rotationDegrees: z.number().optional().describe("Rotate the lattice about origin."),
        snapDistance: z
          .number()
          .min(0)
          .max(64)
          .optional()
          .describe("Node reuse radius in metres, default 8."),
        name: z.string().optional().describe("Names the created segments, useful for auditing."),
        opId,
        dryRun,
      },
    },
    async (args) => run("/commands/build-grid", args),
  );

  server.registerTool(
    "cs1_build_neighborhood",
    {
      description:
        "Build a grid, connect it to the existing road network, and zone every block from a mix — " +
        "one call instead of roughly eighty. The connection step runs before zoning, so a " +
        "neighborhood can never end up as a zoned orphan island; if it cannot reach a road the " +
        "grid is rolled back. Returns a manifest of which block got which zone.",
      inputSchema: {
        roadPrefab: z.string(),
        center: point.describe("Middle of the neighborhood. The lattice is centred on this."),
        cols: z.number().int().min(1).max(12).optional(),
        rows: z.number().int().min(1).max(12).optional(),
        radiusOrCols: z
          .number()
          .int()
          .min(1)
          .max(12)
          .optional()
          .describe("Shorthand for a square neighborhood when cols/rows are not given."),
        spacing: z.number().min(32).max(256).optional(),
        rotationDegrees: z.number().optional(),
        connectTo: point
          .optional()
          .describe(
            "A point on an existing road. The nearest lattice node is joined to the nearest real " +
              "road node near here. Omit only if the grid already overlaps the network.",
          ),
        connectMaxDistance: z.number().optional().describe("Search radius for connectTo, default 400m."),
        zoneMix: z
          .record(z.enum(ZONE_NAMES), z.number())
          .describe(
            "Zone name to relative weight, e.g. {\"ResidentialLow\":0.7,\"CommercialLow\":0.3}. " +
              "Weights are normalised. CS1 has no plain 'Commercial' or 'Residential' zone.",
          ),
        commercialPlacement: z
          .enum(["perimeter", "core", "corners"])
          .optional()
          .describe(
            "Where the minority zones go. perimeter (default) puts shops on the edges facing " +
              "through traffic; core clusters them centrally; corners is the most compact.",
          ),
        preserveOccupied: z
          .boolean()
          .optional()
          .describe("Default true. Leave true unless you intend to repaint developed blocks."),
        zoneRadius: z
          .number()
          .optional()
          .describe("Zone paint radius per block, default spacing/2."),
        opId,
        dryRun,
      },
    },
    async (args) => run("/commands/build-neighborhood", args),
  );

  server.registerTool(
    "cs1_connect",
    {
      description:
        "Join a point to the nearest existing network node of a service. Use it after every " +
        "service building placement — it eliminates the whole class of 'I built a water tower " +
        "and it has no road access' failures. Reports alreadyConnected:true when the point was " +
        "already on the network, which is a success, not a no-op to retry.",
      inputSchema: {
        from: point.describe("The stranded point, typically a building position."),
        toService: z
          .string()
          .optional()
          .describe("ItemClass service to connect to, default Road."),
        maxDistance: z.number().min(1).max(1000).optional().describe("Search radius, default 200m."),
        roadPrefab: z.string().optional().describe("Prefab for the connecting segment, default 'Basic Road'."),
        snapDistance: z.number().min(0).max(64).optional(),
        name: z.string().optional(),
        opId,
        dryRun,
      },
    },
    async (args) => run("/commands/connect", args),
  );

  // --------------------------------------------------------------- primitive builds

  server.registerTool(
    "cs1_build_network",
    {
      description:
        "Build a single segment between two points. Prefer cs1_build_grid for anything with more " +
        "than a few segments. Endpoints within snapDistance of an existing node reuse it, which " +
        "is what makes the result a real intersection rather than a crossing. A surface pedestrian " +
        "path is refused (reason 'pathOnRoad', with suggestedStart/suggestedEnd) when it runs onto a " +
        "surface road: the game never joins a path to a road node or splits a road for it. End paths " +
        "at the road edge; the path end links to the sidewalk by a lane connection within 16.5 m.",
      inputSchema: {
        roadPrefab: z.string(),
        start: point,
        end: point,
        snapDistance: z.number().min(0).max(64).optional(),
        name: z.string().optional(),
        allowRoadOverlap: z
          .boolean()
          .optional()
          .describe("Default false. Build a surface path onto a road anyway (non-vanilla geometry)."),
        dryRun,
      },
    },
    async (args) => run("/commands/build-network", args),
  );

  server.registerTool(
    "cs1_set_zone",
    {
      description:
        "Paint one zone type over every zone block within radius of a point. Feed it the " +
        "blockCenters returned by cs1_build_grid. Check cs1_state_growables first: preserveOccupied " +
        "defaults to true and should stay true unless you mean to bulldoze development.",
      inputSchema: {
        zone: z.enum(ZONE_NAMES),
        center: point,
        radius: z.number().min(1).max(256).describe("Metres. Roughly spacing/2 for one grid block."),
        preserveOccupied: z.boolean().optional().describe("Default true."),
        dryRun,
      },
    },
    async (args) => run("/commands/set-zone", args),
  );

  server.registerTool(
    "cs1_place_building",
    {
      description:
        "Place a building, including a unique or waterfront attraction. Locked prefabs are refused " +
        "by default; use ignoreUnlock only for an intentional test. Follow every placement with " +
        "cs1_connect on the same position when it needs road access.",
      inputSchema: {
        buildingPrefab: z.string().describe("Exact name from cs1_prefabs_buildings."),
        position: point,
        angleDegrees: z.number().optional(),
        validate: z.boolean().optional().describe("Run the in-game placement and collision checks."),
        elevation: z.number().optional().describe("Elevation step used by validated placement."),
        ignoreUnlock: z.boolean().optional().describe("Bypass the prefab milestone gate for an intentional test."),
        dryRun,
      },
    },
    async (args) => run("/commands/place-building", args),
  );

  server.registerTool(
    "cs1_set_building_active",
    {
      description:
        "Turn an existing service, station, or unique building on or off by id. The bridge uses " +
        "the game's production-rate setter on the simulation thread; re-read cs1_state_facilities " +
        "after a simulation step to verify Active and any remaining problem flags.",
      inputSchema: {
        id: z.number().int().min(1).describe("Building id from cs1_state_facilities."),
        active: z.boolean().describe("true to activate, false to deactivate."),
      },
    },
    async (args) => run("/commands/set-building-active", args),
  );

  server.registerTool(
    "cs1_move_building",
    {
      description: "Move an existing building by id to a new position.",
      inputSchema: {
        id: z.number().int().describe("Building id from cs1_state_facilities."),
        position: point,
        angleDegrees: z.number().optional(),
        dryRun,
      },
    },
    async (args) => run("/commands/move-building", args),
  );

  server.registerTool(
    "cs1_bulldoze",
    {
      description:
        "Delete one entity. keepNodes:false on a netSegment also removes its endpoints when they " +
        "become orphaned, which is usually what you want when rebuilding a bad connection.",
      inputSchema: {
        entityType: z.enum(["building", "netSegment", "netNode", "tree", "prop"]),
        id: z.number().int(),
        keepNodes: z.boolean().optional().describe("netSegment only. Default true."),
      },
    },
    async (args) => run("/commands/bulldoze", args),
  );

  server.registerTool(
    "cs1_unlock_area",
    {
      description:
        "Buy a map tile, as the game's area panel does: checks it is not owned, is edge-adjacent " +
        "to an owned tile, the city is under the max-area cap and has reached the next area " +
        "milestone, then pays the land price and unlocks it. Give tileX/tileZ (0-4, from " +
        "cs1_state_areas) or a world position x/z inside the tile. dryRun:true reports whether it " +
        "would succeed and the price.",
      inputSchema: {
        tileX: z.number().int().min(0).max(4).optional(),
        tileZ: z.number().int().min(0).max(4).optional(),
        x: z.number().optional().describe("World X in metres; used when tileX/tileZ are absent."),
        z: z.number().optional().describe("World Z in metres; used when tileX/tileZ are absent."),
        ignoreMaxAreaCount: z
          .boolean()
          .optional()
          .describe(
            "Raise the game's max-area cap (default 9) to 25 before buying, as tile mods do. " +
              "Does not bypass the area milestones. Not saved: a reload resets the cap to " +
              "max(9, owned tiles).",
          ),
        dryRun: z.boolean().optional().describe("Validate and report the price without buying."),
      },
    },
    async (args) => run("/commands/unlock-area", args),
  );

  // ------------------------------------------------------------------ repair + sim

  server.registerTool(
    "cs1_repair_zone_clusters",
    {
      description:
        "Fix blocks where zoning came out mottled, by snapping each cluster to its dominant zone. " +
        "Run cs1_state_zone_anomalies first and again afterwards to confirm it reached zero.",
      inputSchema: {
        preferGrowableZone: z
          .boolean()
          .optional()
          .describe("Match existing developed buildings rather than the raw cell majority."),
        dryRun,
      },
    },
    async (args) => run("/commands/repair-zone-clusters", args),
  );

  server.registerTool(
    "cs1_repair_zones_to_growables",
    {
      description: "Re-zone blocks to match the buildings that already grew on them.",
      inputSchema: { dryRun },
    },
    async (args) => run("/commands/repair-zones-to-growables", args),
  );

  server.registerTool(
    "cs1_set_simulation_speed",
    {
      description:
        "Pause or set simulation speed 1-3. Let the city run before judging whether a build " +
        "worked — demand, traffic, and problems need simulated time to appear.",
      inputSchema: {
        paused: z.boolean().optional(),
        speed: z.number().int().min(1).max(3).optional(),
      },
    },
    async (args) => run("/commands/set-simulation-speed", args),
  );

  server.registerTool(
    "cs1_set_tax_rate",
    {
      description:
        "Set the tax rate for a service. Typical range is 8-13; above that citizens complain and " +
        "cs1_state_problems starts reporting TaxesTooHigh.",
      inputSchema: {
        service: z.string().describe("Residential, Commercial, Industrial, or Office."),
        rate: z.number().int().min(0).max(29),
        subService: z.string().optional(),
        level: z.string().optional(),
      },
    },
    async (args) => run("/commands/set-tax-rate", args),
  );

  server.registerTool(
    "cs1_save",
    {
      description:
        "Save the city through the game's own save panel. Returns immediately — poll " +
        "cs1_state_saves until the file exists before claiming it succeeded. Names are sanitised " +
        "server-side; read saveName in the response. If the player has \"quit after saving\" " +
        "checked, this closes the game and the chat box dies. Do not save just to checkpoint " +
        "while someone is talking in the chat.",
      inputSchema: { name: z.string().optional().describe("Default AgentAutoSave.") },
    },
    async (args) => run("/commands/save", args),
  );

  // ------------------------------------------------------------------------ capture

  server.registerTool(
    "cs1_capture",
    {
      description:
        "Render a top-down image of an area and return it as an image. Vision is for VERIFICATION " +
        "ONLY — never plan layout from a picture, plan from the state tools and use this to confirm " +
        "what was built. Costs roughly 1-1.5K tokens per call, so capture after finishing a phase, " +
        "not after every command. Uses its own off-screen camera, so the player's view does not move.",
      inputSchema: {
        x: z.number().describe("World X of the view centre."),
        z: z.number().describe("World Z of the view centre."),
        size: z
          .number()
          .min(100)
          .max(10000)
          .optional()
          .describe("Metres covered edge to edge, default 1000."),
        pixels: z.number().int().min(256).max(1024).optional().describe("Square resolution, default 1024."),
        mode: z
          .string()
          .optional()
          .describe(
            "Info overlay, which is what makes the image legible: None (default — plain view, " +
              "and the one that shows zoning colours), Traffic, Water, Electricity, LandValue, " +
              "Pollution, NoisePollution, Health, Happiness, Density, Garbage, Education, " +
              "TerrainHeight, Transport. One mode per call. CS1 has no 'Zone' overlay; 'Zone' is " +
              "accepted and resolves to None.",
          ),
        settleFrames: z
          .number()
          .int()
          .min(1)
          .max(120)
          .optional()
          .describe("Frames to wait after switching overlay, default 8. Raise if an overlay looks faded."),
      },
    },
    async (args) => {
      try {
        const { bytes, headers } = await bridge.getBinary("/capture", {
          x: args.x,
          z: args.z,
          size: args.size,
          pixels: args.pixels,
          mode: args.mode,
          settleFrames: args.settleFrames,
        });

        const resolved = headers.get("x-bridge-info-mode") ?? args.mode ?? "None";
        const caption =
          `Top-down render centred on (${args.x}, ${args.z}), ` +
          `${args.size ?? 1000}m across, overlay ${resolved}.`;

        return image(Buffer.from(bytes).toString("base64"), "image/png", caption);
      } catch (error) {
        return fail(error);
      }
    },
  );
}
