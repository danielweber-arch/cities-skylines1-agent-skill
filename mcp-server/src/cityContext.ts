/**
 * Per-city context: resolves which `cities/<slug>/` directory belongs to the game's currently
 * loaded city, and declares a session-scoped city id so every mutation tool can refuse to act
 * against a different city than the one the session started with. See wave2-spec.md section 8.
 */
import { existsSync, readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import type { BridgeClient } from "./client.js";

export type CityIdentity = {
  id: string;
  name: string;
  map?: string;
  environment?: string;
  gameDate?: string;
  population?: number;
  lastSaveName?: string;
} | null;

export function repoRoot(): string {
  if (process.env.CS1_REPO_ROOT) return process.env.CS1_REPO_ROOT;
  // src/ and dist/ are both one level under mcp-server/, and cities/ is in the repo root.
  return join(dirname(fileURLToPath(import.meta.url)), "..", "..");
}

export function slugify(name: string, id?: string): string {
  const base = name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  if (id) return `${base}-${id.slice(0, 8)}`;
  return base;
}

type CityMdHeader = { id?: string; name?: string; map?: string; bindOnLoad?: boolean };

function parseHeader(text: string): CityMdHeader {
  const header: CityMdHeader = {};
  for (const rawLine of text.split("\n").slice(0, 20)) {
    const line = rawLine.trim();
    const idMatch = line.match(/^id:\s*(.+)$/i);
    if (idMatch) header.id = idMatch[1]!.trim();
    const nameMatch = line.match(/^name:\s*(.+)$/i);
    if (nameMatch) header.name = nameMatch[1]!.trim();
    const mapMatch = line.match(/^map:\s*(.+)$/i);
    if (mapMatch) header.map = mapMatch[1]!.trim();
    const bindMatch = line.match(/^bindOnLoad:\s*(true|false)/i);
    if (bindMatch) header.bindOnLoad = bindMatch[1]!.toLowerCase() === "true";
  }
  return header;
}

export type ResolvedCityDir = { dir: string; bound: boolean; note?: string } | null;

/** Why the last resolveCityDir returned null (ambiguous names, failed bind), for cs1_city_context. */
export let lastResolveNote: string | null = null;

/**
 * Scans each cities/<slug>/city.md header: a dir whose `id:` equals identity.id wins; else a
 * dir with `id: unknown` and a case-insensitive matching `name:` is BOUND in place (its id
 * line is rewritten to the real id and bindOnLoad set to false, dir name unchanged).
 */
export function resolveCityDir(identity: CityIdentity, root = repoRoot()): ResolvedCityDir {
  if (!identity) return null;
  const citiesDir = join(root, "cities");
  if (!existsSync(citiesDir)) return null;

  let entries: string[];
  try {
    entries = readdirSync(citiesDir).filter((e) => statSync(join(citiesDir, e)).isDirectory());
  } catch {
    return null;
  }

  const nameMatches: string[] = [];
  lastResolveNote = null;

  for (const entry of entries) {
    const cityMdPath = join(citiesDir, entry, "city.md");
    if (!existsSync(cityMdPath)) continue;
    let text: string;
    try {
      text = readFileSync(cityMdPath, "utf8");
    } catch {
      continue;
    }
    const header = parseHeader(text);
    if (header.id && header.id === identity.id) {
      return { dir: join(citiesDir, entry), bound: true };
    }
    // Only an EXPLICIT `id: unknown` header is bindable: a city.md with no id line cannot be
    // rewritten in place, so it must never silently absorb a city.
    if (header.id && header.id.toLowerCase() === "unknown" && header.name && header.name.toLowerCase() === identity.name.toLowerCase()) {
      nameMatches.push(entry);
    }
  }

  if (nameMatches.length > 1) {
    lastResolveNote = `ambiguous: ${nameMatches.length} dirs (${nameMatches.join(", ")}) have id: unknown and name "${identity.name}"; set id: ${identity.id} by hand in the right one`;
    return null;
  }
  const nameMatch = nameMatches[0];
  if (nameMatch) {
    const cityMdPath = join(citiesDir, nameMatch, "city.md");
    try {
      const text = readFileSync(cityMdPath, "utf8");
      const rewritten = text
        .replace(/^id:\s*.*$/im, `id: ${identity.id}`)
        .replace(/^bindOnLoad:\s*(true|false)/im, "bindOnLoad: false");
      writeFileSync(cityMdPath, rewritten);
      if (parseHeader(readFileSync(cityMdPath, "utf8")).id !== identity.id) {
        throw new Error("id line not rewritten");
      }
    } catch (e) {
      // The dir is the right one by name but the bind did not stick: use it, say so, and let
      // the next call try again rather than claiming it is bound.
      return { dir: join(citiesDir, nameMatch), bound: false, note: `bind failed: ${e instanceof Error ? e.message : String(e)}` };
    }
    return { dir: join(citiesDir, nameMatch), bound: true };
  }

  return null;
}

// ----------------------------------------------------------------------- session guard state

let declaredCityId: string | null = null;
let declaredCityName: string | null = null;

export function sessionCityId(): string | null {
  return declaredCityId;
}

export function resetSessionCity(): void {
  declaredCityId = null;
  declaredCityName = null;
}

export class CityGuardError extends Error {}

/**
 * HARD session guard, no opt-out (wave2-spec.md section 8). Call before every mutation tool.
 * Throws CityGuardError when the loaded city is missing or differs from the declared session
 * city; auto-declares on the first mutation otherwise.
 */
export async function guardSessionCity(bridge: BridgeClient): Promise<{ city: { id: string; name: string } | null; note?: string }> {
  const health = (await bridge.get("/health")) as { city?: CityIdentity };
  const city = health?.city ?? null;

  if (!city) {
    throw new CityGuardError("no city loaded");
  }

  if (declaredCityId && declaredCityId !== city.id) {
    throw new CityGuardError(
      `loaded city ${city.name} (${city.id}) differs from the session city ${declaredCityId}: stop, re-read context (cs1_city_context)`,
    );
  }

  if (!declaredCityId) {
    declaredCityId = city.id;
    declaredCityName = city.name;
    return { city: { id: city.id, name: city.name }, note: "auto-declared session city on first mutation" };
  }

  return { city: { id: declaredCityId, name: declaredCityName ?? city.name } };
}

export function declareSessionCity(identity: CityIdentity): void {
  if (!identity) return;
  declaredCityId = identity.id;
  declaredCityName = identity.name;
}

// ----------------------------------------------------------------------- cache stamping

export function cacheDirFor(resolved: ResolvedCityDir): string | null {
  if (!resolved) return null;
  return join(resolved.dir, "cache");
}

export function writeCacheFile(
  resolved: ResolvedCityDir,
  filename: string,
  payload: unknown,
  captured: { gameDate?: string; population?: number },
): string | null {
  const cacheDir = cacheDirFor(resolved);
  if (!cacheDir) return null;
  try {
    if (!existsSync(cacheDir)) return null; // silent no-op; cache dir not set up yet
    const wrapped = {
      capturedAt: { gameDate: captured.gameDate ?? null, population: captured.population ?? null, wallClock: new Date().toISOString() },
      payload,
    };
    const filePath = join(cacheDir, filename);
    writeFileSync(filePath, JSON.stringify(wrapped));
    return `cache/${filename}`;
  } catch {
    return null;
  }
}
