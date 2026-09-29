import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { currentOrders, headings, planPath, readPlan, sectionText } from "../masterPlan.js";
import { fail, text } from "./shared.js";

const PLAN_URI = "cs1://master-plan";

export function registerPlanTools(server: McpServer): void {
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
      try {
        markdown = readPlan();
      } catch (error) {
        return fail(error instanceof Error ? new Error(`Master plan not readable at ${planPath()}: ${error.message}`) : error);
      }

      const titles = headings(markdown).map((heading) => heading.title);
      if (args.section) {
        const body = sectionText(markdown, args.section);
        if (!body) {
          return fail(new Error(`No plan heading matches "${args.section}". Headings: ${titles.join("; ")}`));
        }
        return text(JSON.stringify({ source: planPath(), section: args.section, text: body }));
      }

      if (args.full) {
        return text(JSON.stringify({ source: planPath(), headings: titles, text: markdown }));
      }

      return text(
        JSON.stringify({
          source: planPath(),
          note: "This is the current orders. Earlier sections are the original study; when they disagree, this text wins. Pass section to read one heading.",
          headings: titles,
          text: currentOrders(markdown),
        }),
      );
    },
  );
}
