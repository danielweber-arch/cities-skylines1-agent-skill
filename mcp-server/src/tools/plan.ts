import { z } from "zod";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { BridgeClient } from "../client.js";
import { currentOrders, headings, planPath, readPlan, sectionText } from "../masterPlan.js";
import { resolveCityDir, type CityIdentity } from "../cityContext.js";
import { fail, text } from "./shared.js";

const PLAN_URI = "cs1://master-plan";

/** When the loaded city resolves to a dir with plan.md, read that; else fall back to the old default. */
async function resolvePlan(bridge?: BridgeClient): Promise<{ markdown: string; source: string }> {
  if (bridge) {
    try {
      const health = (await bridge.get("/health")) as { city?: CityIdentity };
      const resolved = resolveCityDir(health?.city ?? null);
      if (resolved) {
        const planMd = join(resolved.dir, "plan.md");
        if (existsSync(planMd)) {
          return { markdown: readFileSync(planMd, "utf8"), source: planMd };
        }
      }
    } catch {
      /* fall through to the legacy default */
    }
  }
  return { markdown: readPlan(), source: planPath() };
}

export function registerPlanTools(server: McpServer, bridge?: BridgeClient): void {
  server.registerResource(
    "master-plan",
    PLAN_URI,
    {
      description:
        "The Portville city master plan. Later sections override earlier ones. Read this before building.",
      mimeType: "text/markdown",
    },
    async (uri) => {
      try {
        return {
          contents: [{ uri: uri.href, mimeType: "text/markdown", text: readPlan() }],
        };
      } catch (error) {
        return {
          contents: [
            {
              uri: uri.href,
              mimeType: "text/plain",
              text: `ERROR: master plan not readable at ${planPath()}: ${error instanceof Error ? error.message : String(error)}`,
            },
          ],
        };
      }
    },
  );

  server.registerTool(
    "cs1_master_plan",
    {
      description:
        "The Portville master plan from disk, so a new model session still knows the city. " +
        "Call this once before the first build in a session. With no arguments, returns the " +
        "current orders (player directives and §10); later text overrides earlier text. Pass " +
        "section to read one heading (for example \"10.15\" or \"Zoning\"). Pass full:true only " +
        "when you need the original study. Do not invent a different phase order, airport site, " +
        "or hub than this file states.",
      inputSchema: {
        section: z.string().min(1).optional().describe("Heading text to extract, case-insensitive."),
        full: z.boolean().optional().describe("Return the entire plan. Default false."),
      },
    },
    async (args) => {
      let markdown: string;
      let source: string;
      try {
        ({ markdown, source } = await resolvePlan(bridge));
      } catch (error) {
        return fail(error instanceof Error ? new Error(`Master plan not readable: ${error.message}`) : error);
      }

      const titles = headings(markdown).map((heading) => heading.title);
      if (args.section) {
        const body = sectionText(markdown, args.section);
        if (!body) {
          return fail(new Error(`No plan heading matches "${args.section}". Headings: ${titles.join("; ")}`));
        }
        return text(JSON.stringify({ source, section: args.section, text: body }));
      }

      if (args.full) {
        return text(JSON.stringify({ source, headings: titles, text: markdown }));
      }

      return text(
        JSON.stringify({
          source,
          note: "This is the current orders. Earlier sections are the original study; when they disagree, this text wins. Pass section to read one heading.",
          headings: titles,
          text: currentOrders(markdown),
        }),
      );
    },
  );
}
