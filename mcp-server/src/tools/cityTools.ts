import { z } from "zod";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { BridgeClient } from "../client.js";
import { fail, text } from "./shared.js";
import { declareSessionCity, lastResolveNote, resolveCityDir, sessionCityId, slugify, type CityIdentity } from "../cityContext.js";

const CAP = 2000;

export function registerCityTools(server: McpServer, bridge: BridgeClient) {
  server.registerTool(
    "cs1_city_context",
    {
      description:
        "Identifies the currently loaded city and resolves its cities/<slug>/ context directory " +
        "(city.md, progress.md, plan.md, lessons.md, cache/). Calling this DECLARES the session " +
        "city id: every mutation tool afterwards refuses if the loaded city changes. Call this " +
        "first, every session and after any load/restart.",
      inputSchema: {},
    },
    async () => {
      try {
        const health = (await bridge.get("/health")) as { city?: CityIdentity };
        const city = health?.city ?? null;
        declareSessionCity(city);

        if (!city) {
          return text(
            JSON.stringify({ city: null, dir: null, exists: false, bound: false, sessionCityId: sessionCityId(), note: "no city loaded" }),
          );
        }

        const resolved = resolveCityDir(city);
        const dir = resolved?.dir ?? null;
        const files = dir
          ? {
              cityMd: existsSync(join(dir, "city.md")),
              progressMd: existsSync(join(dir, "progress.md")),
              planMd: existsSync(join(dir, "plan.md")),
              lessonsMd: existsSync(join(dir, "lessons.md")),
            }
          : { cityMd: false, progressMd: false, planMd: false, lessonsMd: false };

        let cache: Array<{ file: string; capturedGameDate?: string; capturedPopulation?: number }> = [];
        let cacheTruncated = 0;
        if (dir) {
          const cacheDir = join(dir, "cache");
          if (existsSync(cacheDir)) {
            try {
              const entries = readdirSync(cacheDir).filter((f) => f.endsWith(".json"));
              cacheTruncated = Math.max(0, entries.length - 20);
              cache = entries.slice(0, 20).map((f) => {
                try {
                  const parsed = JSON.parse(readFileSync(join(cacheDir, f), "utf8"));
                  return {
                    file: f,
                    capturedGameDate: parsed?.capturedAt?.gameDate ?? undefined,
                    capturedPopulation: parsed?.capturedAt?.population ?? undefined,
                  };
                } catch {
                  return { file: f };
                }
              });
            } catch {
              /* ignore */
            }
          }
        }

        const note = dir
          ? undefined
          : `no context dir: create cities/${slugify(city.name, city.id)}/ from templates/city/, never reuse another city's files`;

        const body = {
          city,
          dir,
          exists: dir !== null,
          bound: resolved?.bound ?? false,
          ...(resolved?.note ? { bindNote: resolved.note } : {}),
          ...(!resolved && lastResolveNote ? { resolveNote: lastResolveNote } : {}),
          files,
          cache,
          cacheTruncated,
          sessionCityId: sessionCityId(),
          ...(note ? { note } : {}),
        };

        let encoded = JSON.stringify(body);
        if (encoded.length > CAP) {
          encoded = JSON.stringify({ ...body, cache: body.cache.slice(0, 5), cacheTruncated: body.cache.length - 5 });
        }
        return text(encoded);
      } catch (error) {
        return fail(error);
      }
    },
  );
}
