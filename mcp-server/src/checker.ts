/**
 * Pure plan checker (no I/O) plus a thin async wrapper that reads what it needs from the
 * bridge through an `io` object, so the rules can be unit tested without a server.
 *
 * HARD rules block a mutation outright (never truncated in the response); ADVISORY rules are
 * reported but do not stop the bridge call. Every rule below carries a citation to the repo
 * doc or lesson it encodes, per wave2-spec.md section 4.
 */

export type Point = { x: number; z: number; elevation?: number };
export type PlanRoad = { prefab: string; points: Point[]; name?: string };
export type PlanZone = { zone: string; center: { x: number; z: number }; radius: number };
export type PlanBuilding = {
  prefab: string;
  position: { x: number; z: number };
  angleDegrees?: number;
  widthCells?: number;
  lengthCells?: number;
};

export type NoBuildArea = {
  name: string;
  polygon?: Array<{ x: number; z: number }>;
  bbox?: { minX: number; minZ: number; maxX: number; maxZ: number };
};

export type StandingOrders = {
  noBuild: NoBuildArea[];
  maxSpend?: number;
};

export type Plan = {
  roads?: PlanRoad[];
  zones?: PlanZone[];
  buildings?: PlanBuilding[];
  standingOrders?: StandingOrders;
};

export type Finding = { rule: string; msg: string; at?: { x: number; z: number }; count?: number; key?: string };

/**
 * Collapse HARD findings that share a rule and an entity (one road sampled every 8 m over water
 * would otherwise emit a line per sample). HARD is never truncated, so without this a 40-segment
 * layout in water blows the token cap. The first point is kept and `count` says how many samples hit.
 */
export function collapseFindings(findings: Finding[]): Finding[] {
  const out: Finding[] = [];
  const index = new Map<string, Finding>();
  for (const f of findings) {
    const k = f.key ? `${f.rule}|${f.key}` : null;
    const existing = k ? index.get(k) : undefined;
    if (existing) {
      existing.count = (existing.count ?? 1) + 1;
      continue;
    }
    const copy: Finding = f.at ? { rule: f.rule, msg: f.msg, at: f.at } : { rule: f.rule, msg: f.msg };
    if (k) index.set(k, copy);
    out.push(copy);
  }
  return out;
}

export type CheckResult = {
  verdict: "blocked" | "advisory" | "ok";
  hard: Finding[];
  advisory: Finding[];
  advisoryTotal: number;
  checked: { points: number; segments: number; zones: number; buildings: number };
  sources: { terrainCalls: number; standingOrders?: string };
  /** One citation per HARD rule that fired, so each finding line can stay short. */
  citations?: Record<string, string>;
};

export const HARD_CITATIONS: Record<string, string> = {
  "H-WATER": "docs/api.md Water guard; CLAUDE.md hard gate: never build in water",
  "H-WATER-UNVERIFIED": "docs/api.md Water guard; incomplete terrain sampling cannot clear a ground build",
  "H-NOBUILD": "CLAUDE.md hard gates: standing orders; the city's city.md NO-BUILD lines",
  "H-TURN": "lessons.md Proven Rules: every metro/rail node turn <= 40 deg (pathfinder refuses ~45.8 deg)",
  "H-CROSSING": "SKILL.md Known Gotchas: the bridge snaps endpoints within 8 m only; a mid-segment crossing is not an intersection",
};

// --------------------------------------------------------------------- geometry primitives

const CELL_M = 8; // CS1 zoning/footprint cell size, used throughout docs/api.md and SKILL.md.
const NAME_CAP = 40; // messages cap any user-supplied name to keep the output budget predictable.

/** Caps a user-supplied name (road name, building prefab, area name) for finding messages. */
export function truncateName(name: string | undefined | null, max = NAME_CAP): string {
  const s = name ?? "";
  return s.length <= max ? s : s.slice(0, max);
}

export function dist(a: { x: number; z: number }, b: { x: number; z: number }): number {
  return Math.hypot(a.x - b.x, a.z - b.z);
}

/**
 * Rotate a local point by angleDeg using the same convention as the game's angleDegrees and
 * the stamp tool (src/stamp.ts): x' = x*cos(a) + z*sin(a), z' = -x*sin(a) + z*cos(a).
 */
export function rotate(local: { x: number; z: number }, angleDeg: number): { x: number; z: number } {
  const a = (angleDeg * Math.PI) / 180;
  const cos = Math.cos(a);
  const sin = Math.sin(a);
  return { x: local.x * cos + local.z * sin, z: -local.x * sin + local.z * cos };
}

/** Interior turn angle (degrees, XZ only) between segment a->b and b->c. 0 = straight. */
export function turnAngleDeg(a: Point, b: Point, c: Point): number {
  const v1 = { x: b.x - a.x, z: b.z - a.z };
  const v2 = { x: c.x - b.x, z: c.z - b.z };
  const len1 = Math.hypot(v1.x, v1.z);
  const len2 = Math.hypot(v2.x, v2.z);
  if (len1 === 0 || len2 === 0) return 0;
  const dot = (v1.x * v2.x + v1.z * v2.z) / (len1 * len2);
  const clamped = Math.max(-1, Math.min(1, dot));
  return (Math.acos(clamped) * 180) / Math.PI;
}

/** Footprint corners for a building with widthCells/lengthCells (8 m cells), rotated by angleDegrees. */
export function footprintCorners(building: PlanBuilding): Array<{ x: number; z: number }> {
  const w = (building.widthCells ?? 0) * CELL_M;
  const l = (building.lengthCells ?? 0) * CELL_M;
  if (w === 0 && l === 0) return [];
  const halfW = w / 2;
  const halfL = l / 2;
  const angle = building.angleDegrees ?? 0;
  const locals = [
    { x: -halfW, z: -halfL },
    { x: halfW, z: -halfL },
    { x: halfW, z: halfL },
    { x: -halfW, z: halfL },
  ];
  return locals.map((p) => {
    const r = rotate(p, angle);
    return { x: building.position.x + r.x, z: building.position.z + r.z };
  });
}

/** Point-in-polygon, ray casting. */
export function pointInPolygon(p: { x: number; z: number }, polygon: Array<{ x: number; z: number }>): boolean {
  let inside = false;
  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const pi = polygon[i];
    const pj = polygon[j];
    if (!pi || !pj) continue;
    const intersect =
      pi.z > p.z !== pj.z > p.z &&
      p.x < ((pj.x - pi.x) * (p.z - pi.z)) / (pj.z - pi.z) + pi.x;
    if (intersect) inside = !inside;
  }
  return inside;
}

function distToSegment(p: { x: number; z: number }, a: { x: number; z: number }, b: { x: number; z: number }): number {
  const abx = b.x - a.x;
  const abz = b.z - a.z;
  const len2 = abx * abx + abz * abz;
  if (len2 === 0) return dist(p, a);
  let t = ((p.x - a.x) * abx + (p.z - a.z) * abz) / len2;
  t = Math.max(0, Math.min(1, t));
  return dist(p, { x: a.x + t * abx, z: a.z + t * abz });
}

function distToPolygon(p: { x: number; z: number }, polygon: Array<{ x: number; z: number }>): number {
  let min = Infinity;
  for (let i = 0; i < polygon.length; i++) {
    const a = polygon[i];
    const b = polygon[(i + 1) % polygon.length];
    if (!a || !b) continue;
    min = Math.min(min, distToSegment(p, a, b));
  }
  return min;
}

/** True when a circle (center, radius) overlaps a no-build bbox or polygon. */
export function circleHitsNoBuild(center: { x: number; z: number }, radius: number, area: NoBuildArea): boolean {
  if (area.bbox) {
    const cx = Math.max(area.bbox.minX, Math.min(center.x, area.bbox.maxX));
    const cz = Math.max(area.bbox.minZ, Math.min(center.z, area.bbox.maxZ));
    if (dist(center, { x: cx, z: cz }) <= radius) return true;
  }
  if (area.polygon && area.polygon.length >= 3) {
    if (pointInPolygon(center, area.polygon)) return true;
    if (distToPolygon(center, area.polygon) <= radius) return true;
  }
  return false;
}

/** A no-build area's boundary as a closed polygon, whether it was declared as a bbox or a polygon. */
function areaPolygon(area: NoBuildArea): Array<{ x: number; z: number }> | null {
  if (area.polygon && area.polygon.length >= 3) return area.polygon;
  if (area.bbox) {
    const { minX, minZ, maxX, maxZ } = area.bbox;
    return [
      { x: minX, z: minZ },
      { x: maxX, z: minZ },
      { x: maxX, z: maxZ },
      { x: minX, z: maxZ },
    ];
  }
  return null;
}

/**
 * True when a road SEGMENT a-b enters a no-build area: either endpoint is inside, or the
 * segment crosses any area edge. Catches thin/small areas that fall between 8 m samples.
 */
export function segmentHitsNoBuild(a: { x: number; z: number }, b: { x: number; z: number }, area: NoBuildArea): boolean {
  if (circleHitsNoBuild(a, 0, area) || circleHitsNoBuild(b, 0, area)) return true;
  const poly = areaPolygon(area);
  if (!poly) return false;
  for (let i = 0; i < poly.length; i++) {
    const p1 = poly[i];
    const p2 = poly[(i + 1) % poly.length];
    if (!p1 || !p2) continue;
    if (segmentIntersection(a, b, p1, p2)) return true;
  }
  return false;
}

/**
 * True when a building FOOTPRINT polygon overlaps a no-build area: any area vertex inside the
 * footprint (catches an area wholly inside the footprint, which has no corner to sample), any
 * footprint corner inside the area, or any edge pair intersecting.
 */
export function footprintHitsNoBuild(corners: Array<{ x: number; z: number }>, area: NoBuildArea): boolean {
  if (corners.length === 0) return false;
  const poly = areaPolygon(area);
  if (!poly) return false;
  for (const v of poly) {
    if (pointInPolygon(v, corners)) return true;
  }
  for (const c of corners) {
    if (pointInPolygon(c, poly) || circleHitsNoBuild(c, 0, area)) return true;
  }
  for (let i = 0; i < corners.length; i++) {
    const a = corners[i];
    const b = corners[(i + 1) % corners.length];
    if (!a || !b) continue;
    for (let j = 0; j < poly.length; j++) {
      const p1 = poly[j];
      const p2 = poly[(j + 1) % poly.length];
      if (!p1 || !p2) continue;
      if (segmentIntersection(a, b, p1, p2)) return true;
    }
  }
  return false;
}

/** Proper 2D segment intersection; returns the crossing point or null. */
export function segmentIntersection(
  p1: { x: number; z: number },
  p2: { x: number; z: number },
  p3: { x: number; z: number },
  p4: { x: number; z: number },
): { x: number; z: number } | null {
  const d1x = p2.x - p1.x;
  const d1z = p2.z - p1.z;
  const d2x = p4.x - p3.x;
  const d2z = p4.z - p3.z;
  const denom = d1x * d2z - d1z * d2x;
  if (Math.abs(denom) < 1e-9) return null; // parallel
  const t = ((p3.x - p1.x) * d2z - (p3.z - p1.z) * d2x) / denom;
  const u = ((p3.x - p1.x) * d1z - (p3.z - p1.z) * d1x) / denom;
  if (t < 0 || t > 1 || u < 0 || u > 1) return null;
  return { x: p1.x + t * d1x, z: p1.z + t * d1z };
}

/** Sample points along a centreline no more than `spacing` m apart, including both endpoints. */
export function sampleCenterline(a: Point, b: Point, spacing = 8): Point[] {
  const length = dist(a, b);
  if (length === 0) return [a];
  const n = Math.max(1, Math.ceil(length / spacing));
  const out: Point[] = [];
  for (let i = 0; i <= n; i++) {
    const t = i / n;
    out.push({
      x: a.x + (b.x - a.x) * t,
      z: a.z + (b.z - a.z) * t,
      elevation: (a.elevation ?? 0) + ((b.elevation ?? 0) - (a.elevation ?? 0)) * t,
    });
  }
  return out;
}

/** Ground point: |elevation| < 1 m (FindOrCreateNode rounds any non-zero elevation up to 1 m - docs/api.md Water guard). */
export function isGroundPoint(p: Point, prefab: string): boolean {
  const elevation = p.elevation ?? 0;
  if (Math.abs(elevation) >= 1) return false;
  if (/bridge|elevated|tunnel/i.test(prefab)) return false;
  return true;
}

// --------------------------------------------------------------------- cross-road track graph

/**
 * Builds a node graph across ALL track roads in the plan (prefab matches /metro|train/i), merging
 * nodes within 1 m and dropping zero-length pieces, then flags any node with exactly two incident
 * pieces whose turn exceeds 40 deg. This catches a turn hidden across two separate stamp-template
 * segments or two separate build_network calls, which H-TURN's intra-road check cannot see because
 * each such road is only a 2-point road on its own.
 */
export function checkCrossRoadTurns(roads: PlanRoad[]): Finding[] {
  type Piece = { nodeA: number; nodeB: number; farFromA: Point; farFromB: Point; label: string };
  const nodes: Array<{ x: number; z: number }> = [];

  const findOrAddNode = (p: { x: number; z: number }): number => {
    for (let i = 0; i < nodes.length; i++) {
      const n = nodes[i];
      if (n && dist(n, p) <= 1) return i;
    }
    nodes.push({ x: p.x, z: p.z });
    return nodes.length - 1;
  };

  const pieces: Piece[] = [];
  for (const road of roads) {
    if (!/metro|train/i.test(road.prefab)) continue;
    const label = truncateName(road.name ?? road.prefab);
    for (let i = 0; i < road.points.length - 1; i++) {
      const a = road.points[i];
      const b = road.points[i + 1];
      if (!a || !b) continue;
      if (dist(a, b) < 1e-9) continue; // drop zero-length pieces
      const nodeA = findOrAddNode(a);
      const nodeB = findOrAddNode(b);
      pieces.push({ nodeA, nodeB, farFromA: b, farFromB: a, label });
    }
  }

  const incident = new Map<number, Piece[]>();
  for (const piece of pieces) {
    incident.set(piece.nodeA, [...(incident.get(piece.nodeA) ?? []), piece]);
    incident.set(piece.nodeB, [...(incident.get(piece.nodeB) ?? []), piece]);
  }

  const findings: Finding[] = [];
  for (const [nodeIndex, atNode] of incident) {
    if (atNode.length !== 2) continue;
    const node = nodes[nodeIndex];
    const p1 = atNode[0];
    const p2 = atNode[1];
    if (!node || !p1 || !p2) continue;
    const far1 = p1.nodeA === nodeIndex ? p1.farFromA : p1.farFromB;
    const far2 = p2.nodeA === nodeIndex ? p2.farFromA : p2.farFromB;
    const angle = turnAngleDeg(far1, node, far2);
    if (angle > 40) {
      findings.push({
        rule: "H-TURN",
        msg: `Track "${p1.label}" meets "${p2.label}" at a shared node (${node.x.toFixed(1)},${node.z.toFixed(1)}) and turns ${angle.toFixed(1)} deg (> 40 deg)`,
        at: node,
      });
    }
  }
  return findings;
}

// --------------------------------------------------------------------- standing orders parsing

const NO_BUILD_BBOX = /^-\s*NO-BUILD\s+([^:]+):\s*bbox\s+(-?[\d.]+),(-?[\d.]+)\s+(-?[\d.]+),(-?[\d.]+)/i;
const NO_BUILD_POLY = /^-\s*NO-BUILD\s+([^:]+):\s*polygon\s+(.+)$/i;
const MAX_SPEND = /^-\s*MAX-SPEND\s+(-?[\d.]+)/i;

/**
 * Parses every `## Standing orders` (or `## Protected areas`) section from a city.md - a city.md
 * may have more than one such section and all are read. Only the machine lines documented in
 * wave2-spec.md section 4 are read; everything else in a section is prose for the human/model
 * reviewer and is ignored here. bbox corners are normalised (min/max of the two points given), so
 * either corner order works. Cites CLAUDE.md "Standing orders".
 */
export function parseStandingOrders(markdown: string): StandingOrders {
  const noBuild: NoBuildArea[] = [];
  let maxSpend: number | undefined;

  const headingRe = /^##\s+(Standing orders|Protected areas)\s*$/gim;
  let headingMatch: RegExpExecArray | null;
  while ((headingMatch = headingRe.exec(markdown))) {
    const rest = markdown.slice(headingMatch.index + headingMatch[0].length);
    const nextHeading = rest.search(/^##\s+/m);
    const section = nextHeading >= 0 ? rest.slice(0, nextHeading) : rest;

    for (const rawLine of section.split("\n")) {
      const line = rawLine.trim();
      const bbox = line.match(NO_BUILD_BBOX);
      if (bbox) {
        const [, name, x1, z1, x2, z2] = bbox;
        const nx1 = Number(x1);
        const nz1 = Number(z1);
        const nx2 = Number(x2);
        const nz2 = Number(z2);
        noBuild.push({
          name: (name ?? "").trim(),
          bbox: {
            minX: Math.min(nx1, nx2),
            minZ: Math.min(nz1, nz2),
            maxX: Math.max(nx1, nx2),
            maxZ: Math.max(nz1, nz2),
          },
        });
        continue;
      }
      const poly = line.match(NO_BUILD_POLY);
      if (poly) {
        const [, name, pointsStr] = poly;
        const polygon = (pointsStr ?? "")
          .split(";")
          .map((pair) => pair.trim())
          .filter(Boolean)
          .map((pair) => {
            const [x, z] = pair.split(",").map(Number);
            return { x: x ?? 0, z: z ?? 0 };
          });
        noBuild.push({ name: (name ?? "").trim(), polygon });
        continue;
      }
      const spend = line.match(MAX_SPEND);
      if (spend) maxSpend = Number(spend[1]);
    }
  }

  return { noBuild, maxSpend };
}

// --------------------------------------------------------------------- IO contract

export type TerrainPointResult = { x: number; z: number; hasWater: boolean; terrainHeight: number; waterHeight?: number };
export type NetworkSegment = { id: number; prefab: string; start: { x: number; z: number }; end: { x: number; z: number } };
export type NetworksResult = { total: number; returned: number; segments: NetworkSegment[] };
export type GrowableRow = { position: { x: number; z: number } };
export type GrowablesResult = { total: number; returned: number; growables: GrowableRow[] };
export type FacilityRow = { prefab: string; position: { x: number; z: number } };
export type FacilitiesResult = { total: number; returned: number; facilities: FacilityRow[] };
export type TerrainGridResult = {
  cols: number;
  rows: number;
  rows_: string[]; // avoids clashing with the `rows` count field; see checkPlan mapping
  flow: Array<{
    cells: number;
    meanVelocity: { x: number; z: number };
    speed: number;
    still: boolean;
    bbox?: { minX: number; maxX: number; minZ: number; maxZ: number };
  }>;
  origin: { x: number; z: number };
  cell: number;
};

export type CheckerIO = {
  terrainPoints: (points: Array<{ x: number; z: number }>) => Promise<TerrainPointResult[]>;
  networksRoad: () => Promise<NetworksResult>;
  growablesResidential: () => Promise<GrowablesResult>;
  facilitiesWater: () => Promise<FacilitiesResult>;
  terrainGrid: (x: number, z: number, radius: number, cell: number) => Promise<any>;
};

// --------------------------------------------------------------------- the checker

const ADVISORY_CAP = 20;
const OUTPUT_CHAR_CAP = 4000;

/** True when a point lies inside a sampled water-flow region's bbox. Regions without a bbox never match. */
function regionContains(region: { bbox?: { minX: number; maxX: number; minZ: number; maxZ: number } }, p: { x: number; z: number }): boolean {
  const bbox = region.bbox;
  if (!bbox) return false;
  return p.x >= bbox.minX && p.x <= bbox.maxX && p.z >= bbox.minZ && p.z <= bbox.maxZ;
}

export async function checkPlan(
  plan: Plan,
  io: CheckerIO,
  standingOrdersMd?: string,
  standingOrdersSource?: string,
): Promise<CheckResult> {
  const hard: Finding[] = [];
  const advisory: Finding[] = [];
  let terrainCalls = 0;
  let pointsChecked = 0;

  // The city's standing orders ALWAYS apply; any plan-supplied standingOrders are unioned on
  // top and can never replace them (CLAUDE.md "Standing orders" is binding regardless of what
  // a caller passes inline).
  const cityOrders: StandingOrders = standingOrdersMd ? parseStandingOrders(standingOrdersMd) : { noBuild: [] };
  const planOrders: StandingOrders = plan.standingOrders ?? { noBuild: [] };
  const standingOrders: StandingOrders = {
    noBuild: [...cityOrders.noBuild, ...planOrders.noBuild],
    maxSpend: planOrders.maxSpend ?? cityOrders.maxSpend,
  };

  const roads = plan.roads ?? [];
  const zones = plan.zones ?? [];
  const buildings = plan.buildings ?? [];

  // ---- H-NOBUILD (also runs against zone/building points sampled below) ----
  function checkNoBuild(p: { x: number; z: number }, rule: string, extra: string) {
    for (const area of standingOrders.noBuild) {
      if (circleHitsNoBuild(p, 0, area)) {
        const areaName = truncateName(area.name);
        const key = `${extra.replace(/ point \(.*$/, "")}|${areaName}`;
        hard.push({ rule, msg: `${extra} inside no-build area "${areaName}"`, at: p, key });
      }
    }
  }
  function checkNoBuildCircle(center: { x: number; z: number }, radius: number, rule: string, extra: string) {
    for (const area of standingOrders.noBuild) {
      if (circleHitsNoBuild(center, radius, area)) {
        hard.push({ rule, msg: `${extra} overlaps no-build area "${truncateName(area.name)}"`, at: center });
      }
    }
  }

  // ---- gather all ground sample points for H-WATER, batched in 64s ----
  const waterCheckPoints: Array<{ p: { x: number; z: number }; label: string }> = [];

  for (const road of roads) {
    const roadLabel = truncateName(road.name ?? road.prefab);
    for (let i = 0; i < road.points.length - 1; i++) {
      const a = road.points[i];
      const b = road.points[i + 1];
      if (!a || !b) continue;

      // Segment-level H-NOBUILD: catches thin/small areas that fall between 8 m samples.
      for (const area of standingOrders.noBuild) {
        if (segmentHitsNoBuild(a, b, area)) {
          const areaName = truncateName(area.name);
          hard.push({
            rule: "H-NOBUILD",
            msg: `Road "${roadLabel}" segment ${i} crosses no-build area "${areaName}"`,
            at: a,
            key: `Road "${roadLabel}"|${areaName}`,
          });
        }
      }

      const samples = sampleCenterline(a, b, 8);
      for (const s of samples) {
        pointsChecked++;
        if (isGroundPoint(s, road.prefab)) {
          waterCheckPoints.push({ p: { x: s.x, z: s.z }, label: `road "${roadLabel}" centreline` });
        }
        checkNoBuild({ x: s.x, z: s.z }, "H-NOBUILD", `Road "${roadLabel}" point (${s.x},${s.z})`);
      }
    }
  }

  for (const building of buildings) {
    const corners = footprintCorners(building);
    const points = corners.length > 0 ? [building.position, ...corners] : [building.position];
    const buildingLabel = truncateName(building.prefab);

    // Footprint-level H-NOBUILD: catches an area that lies inside the footprint but has no
    // corner (point sampling alone would miss it).
    for (const area of standingOrders.noBuild) {
      if (corners.length > 0 && footprintHitsNoBuild(corners, area)) {
        const areaName = truncateName(area.name);
        hard.push({
          rule: "H-NOBUILD",
          msg: `Building "${buildingLabel}" footprint overlaps no-build area "${areaName}"`,
          at: building.position,
          key: `Building "${buildingLabel}"|${areaName}`,
        });
      }
    }

    for (const p of points) {
      pointsChecked++;
      waterCheckPoints.push({ p, label: `building "${buildingLabel}"` });
      checkNoBuild(p, "H-NOBUILD", `Building "${buildingLabel}" point (${p.x},${p.z})`);
    }
  }

  for (const zone of zones) {
    pointsChecked++;
    waterCheckPoints.push({ p: zone.center, label: `zone "${zone.zone}" centre` });
    checkNoBuildCircle(zone.center, zone.radius, "H-NOBUILD", `Zone "${zone.zone}" circle (centre ${zone.center.x},${zone.center.z}, r=${zone.radius})`);
  }

  // ---- H-WATER: batch terrain sampling, 64 points per call. A short or failed response never
  // lets a build pass silently: it becomes a HARD H-WATER-UNVERIFIED finding instead. ----
  for (let i = 0; i < waterCheckPoints.length; i += 64) {
    const batch = waterCheckPoints.slice(i, i + 64);
    let results: TerrainPointResult[] = [];
    try {
      results = await io.terrainPoints(batch.map((b) => b.p));
      terrainCalls++;
    } catch {
      results = [];
    }
    if (results.length < batch.length) {
      const firstUnsampled = batch[results.length];
      hard.push({
        rule: "H-WATER-UNVERIFIED",
        msg:
          `H-WATER-UNVERIFIED: terrain sampling incomplete (${results.length} of ${batch.length} points); ` +
          `water cannot be ruled out, starting at (${firstUnsampled?.p.x},${firstUnsampled?.p.z})`,
        at: firstUnsampled?.p,
        key: "terrain-sampling-incomplete",
      });
    }
    results.forEach((r, idx) => {
      const entry = batch[idx];
      if (r?.hasWater && entry) {
        hard.push({
          rule: "H-WATER",
          msg: `${entry.label} over water at (${r.x},${r.z})`,
          at: { x: r.x, z: r.z },
          key: entry.label,
        });
      }
    });
  }

  // ---- H-TURN: metro/train track node turns > 40 deg, within a road ----
  for (const road of roads) {
    if (!/metro|train/i.test(road.prefab)) continue;
    for (let i = 1; i < road.points.length - 1; i++) {
      const a = road.points[i - 1];
      const b = road.points[i];
      const c = road.points[i + 1];
      if (!a || !b || !c) continue;
      const angle = turnAngleDeg(a, b, c);
      if (angle > 40) {
        hard.push({
          rule: "H-TURN",
          msg: `Track "${truncateName(road.name ?? road.prefab)}" turns ${angle.toFixed(1)} deg at node ${i} (> 40 deg)`,
          at: { x: b.x, z: b.z },
        });
      }
    }
  }

  // ---- H-TURN: across ALL track roads (every stamp template segment / build_network call can
  // be its own 2-point road, so the turn can be hidden at a shared node between two roads) ----
  hard.push(...checkCrossRoadTurns(roads));

  // ---- H-CROSSING: plan segments vs existing road network ----
  let networksTooLarge = false;
  try {
    const existing = await io.networksRoad();
    for (const road of roads) {
      const roadLabel = truncateName(road.name ?? road.prefab);
      for (let i = 0; i < road.points.length - 1; i++) {
        const pa = road.points[i];
        const pb = road.points[i + 1];
        if (!pa || !pb) continue;
        const segLen = dist(pa, pb);
        for (const seg of existing.segments) {
          const hit = segmentIntersection(pa, pb, seg.start, seg.end);
          if (!hit) continue;
          const farFromPlanEnds = dist(hit, pa) > 8 && dist(hit, pb) > 8;
          const farFromExistingNodes = dist(hit, seg.start) > 8 && dist(hit, seg.end) > 8;
          if (!farFromPlanEnds || !farFromExistingNodes) continue;

          // Elevation: a non-ground plan piece (elevated/bridge/tunnel, or an interpolated
          // |elevation| >= 1 m at the crossing point) is a legitimate grade separation.
          // Existing-network elevation is unknown to this checker.
          const t = segLen === 0 ? 0 : dist(pa, hit) / segLen;
          const elevA = pa.elevation ?? 0;
          const elevB = pb.elevation ?? 0;
          const interpElevation = elevA + (elevB - elevA) * t;
          const nonGround = Math.abs(interpElevation) >= 1 || /bridge|elevated|tunnel/i.test(road.prefab);
          if (nonGround) continue;

          hard.push({
            rule: "H-CROSSING",
            msg:
              `Road "${roadLabel}" crosses existing segment ${seg.id} ("${truncateName(seg.prefab)}") ` +
              `at (${hit.x.toFixed(1)},${hit.z.toFixed(1)}), > 8 m from any node ` +
              `(existing-network elevation is unknown to this checker)`,
            at: hit,
          });
        }
      }
    }
    if (existing.total > existing.returned) {
      networksTooLarge = true;
    }
  } catch {
    networksTooLarge = true;
  }
  if (networksTooLarge) {
    advisory.push({
      rule: "H-CROSSING",
      msg:
        "Network too large to check for crossings completely (returned < total); the crossing check ran only " +
        "on the rows that were returned. Rely on cs1_state_road_anomalies after build.",
    });
  }

  // ---- A-SHORT ----
  for (const road of roads) {
    for (let i = 0; i < road.points.length - 1; i++) {
      const a = road.points[i];
      const b = road.points[i + 1];
      if (!a || !b) continue;
      const length = dist(a, b);
      if (length < 32) {
        advisory.push({
          rule: "A-SHORT",
          msg: `Road "${truncateName(road.name ?? road.prefab)}" segment ${i} is ${length.toFixed(1)} m (< 32 m default shortSegmentLength, SKILL.md Inspection Loop).`,
          at: a,
        });
      }
    }
  }

  // ---- A-GRADE ----
  for (const road of roads) {
    for (let i = 0; i < road.points.length - 1; i++) {
      const a = road.points[i];
      const b = road.points[i + 1];
      if (!a || !b) continue;
      const length = dist(a, b);
      if (length === 0) continue;
      let terrainA = 0;
      let terrainB = 0;
      try {
        const sampled = await io.terrainPoints([a, b]);
        terrainCalls++;
        terrainA = sampled[0]?.terrainHeight ?? 0;
        terrainB = sampled[1]?.terrainHeight ?? 0;
      } catch {
        continue;
      }
      const dElevation = (b.elevation ?? 0) - (a.elevation ?? 0);
      const dTerrain = terrainB - terrainA;
      const grade = Math.abs(dElevation + dTerrain) / length;
      if (grade > 0.08) {
        advisory.push({
          rule: "A-GRADE",
          msg: `Road "${truncateName(road.name ?? road.prefab)}" segment ${i} grade ${(grade * 100).toFixed(1)}% exceeds the 8% hold (knowledge.md Roads; lessons.md:451).`,
          at: a,
        });
      }
    }
  }

  // ---- A-HIERARCHY ----
  const highwayEndpoints: Array<{ x: number; z: number }> = [];
  for (const road of roads) {
    if (/highway/i.test(road.prefab)) {
      const first = road.points[0];
      const last = road.points[road.points.length - 1];
      if (first) highwayEndpoints.push(first);
      if (last) highwayEndpoints.push(last);
    }
  }
  for (const road of roads) {
    if (!/basic road/i.test(road.prefab)) continue;
    const first = road.points[0];
    const last = road.points[road.points.length - 1];
    for (const end of [first, last]) {
      if (!end) continue;
      for (const hwEnd of highwayEndpoints) {
        if (dist(end, hwEnd) <= 8) {
          advisory.push({
            rule: "A-HIERARCHY",
            msg: `"Basic Road" ("${truncateName(road.name ?? road.prefab)}") shares an endpoint with a Highway segment (knowledge.md Traffic: hierarchy is UNVERIFIED).`,
            at: end,
          });
        }
      }
    }
  }

  // ---- A-INDUSTRY-BUFFER ----
  const residentialZones = zones.filter((z) => /residential/i.test(z.zone));
  const industrialZones = zones.filter((z) => /industrial/i.test(z.zone));
  for (const ind of industrialZones) {
    for (const res of residentialZones) {
      const edgeGap = dist(ind.center, res.center) - ind.radius - res.radius;
      if (edgeGap < 200) {
        advisory.push({
          rule: "A-INDUSTRY-BUFFER",
          msg: `Industrial zone at (${ind.center.x},${ind.center.z}) is within 200 m of a planned residential zone (portville-master-plan.md:899-901, knowledge.md Pollution).`,
          at: ind.center,
        });
      }
    }
    try {
      const growables = await io.growablesResidential();
      for (const g of growables.growables) {
        const edgeGap = dist(ind.center, g.position) - ind.radius;
        if (edgeGap < 200) {
          advisory.push({
            rule: "A-INDUSTRY-BUFFER",
            msg: `Industrial zone at (${ind.center.x},${ind.center.z}) is within 200 m of an existing residential growable at (${g.position.x},${g.position.z}) (knowledge.md Pollution, 200 m repo rule).`,
            at: ind.center,
          });
        }
      }
    } catch {
      /* best-effort; skip if the bridge cannot list growables */
    }
  }

  // ---- A-HDCOMM-BUFFER ----
  const commercialHigh = zones.filter((z) => /commercialhigh/i.test(z.zone));
  for (const ch of commercialHigh) {
    for (const res of residentialZones) {
      const edgeGap = dist(ch.center, res.center) - ch.radius - res.radius;
      if (edgeGap < 32) {
        advisory.push({
          rule: "A-HDCOMM-BUFFER",
          msg: `CommercialHigh zone at (${ch.center.x},${ch.center.z}) is within 32 m (4 cells) of a residential zone (knowledge.md Zoning: UNVERIFIED).`,
          at: ch.center,
        });
      }
    }
  }

  // ---- A-SEWAGE: a facility belongs to a sampled water region only when its position falls
  // inside that region's bbox; warn only when outflow and intake share the SAME region. Either
  // facility missing a region membership is inconclusive, not a blanket warning. ----
  const allFacilities: FacilityRow[] = [];
  try {
    const water = await io.facilitiesWater();
    allFacilities.push(...water.facilities);
  } catch {
    /* best-effort */
  }
  const planFacilities: FacilityRow[] = buildings.map((b) => ({ prefab: b.prefab, position: b.position }));
  const candidates = [...allFacilities, ...planFacilities];
  const outflows = candidates.filter((f) => /outlet|drain|treatment/i.test(f.prefab));
  const intakes = candidates.filter((f) => /intake|pumping/i.test(f.prefab));
  for (const outflow of outflows) {
    for (const intake of intakes) {
      const midpoint = { x: (outflow.position.x + intake.position.x) / 2, z: (outflow.position.z + intake.position.z) / 2 };
      const half = dist(outflow.position, intake.position) / 2;
      const radius = Math.min(2048, half + 128);
      const cell = 64;
      const n = Math.ceil((2 * radius) / cell);
      if (n > 64) continue; // capped per section 1
      try {
        const grid = await io.terrainGrid(midpoint.x, midpoint.z, radius, cell);
        terrainCalls++;
        const flows: Array<{ meanVelocity: { x: number; z: number }; still: boolean; bbox?: { minX: number; maxX: number; minZ: number; maxZ: number } }> =
          Array.isArray(grid?.flow) ? grid.flow : [];
        const outflowRegion = flows.find((r) => regionContains(r, outflow.position));
        const intakeRegion = flows.find((r) => regionContains(r, intake.position));
        if (!outflowRegion || !intakeRegion) {
          advisory.push({
            rule: "A-SEWAGE",
            msg: "sewage check inconclusive: facility not on a sampled water region (knowledge.md Water and sewage).",
            at: midpoint,
            key: `A-SEWAGE-inconclusive|${truncateName(outflow.prefab)}|${truncateName(intake.prefab)}`,
          });
        } else if (outflowRegion === intakeRegion) {
          if (outflowRegion.still) {
            advisory.push({
              rule: "A-SEWAGE",
              msg: `Outflow and intake are in the same still water body (knowledge.md Water and sewage; flow is indicative only).`,
              at: midpoint,
            });
          } else {
            const toIntake = { x: intake.position.x - outflow.position.x, z: intake.position.z - outflow.position.z };
            const dot = toIntake.x * outflowRegion.meanVelocity.x + toIntake.z * outflowRegion.meanVelocity.z;
            if (dot > 0) {
              advisory.push({
                rule: "A-SEWAGE",
                msg: `Outflow appears upstream of intake along the same sampled flow region (indicative only; knowledge.md Water and sewage).`,
                at: midpoint,
              });
            }
          }
        }
      } catch {
        /* best-effort */
      }
    }
  }

  const advisoryTotal = advisory.length;
  const cappedAdvisory = advisory.slice(0, ADVISORY_CAP);

  const verdict: CheckResult["verdict"] = hard.length > 0 ? "blocked" : advisoryTotal > 0 ? "advisory" : "ok";

  const segmentsChecked = roads.reduce((sum, r) => sum + Math.max(0, r.points.length - 1), 0);

  const result: CheckResult = {
    verdict,
    hard: collapseFindings(hard),
    advisory: cappedAdvisory,
    advisoryTotal,
    checked: { points: pointsChecked, segments: segmentsChecked, zones: zones.length, buildings: buildings.length },
    sources: standingOrdersSource ? { terrainCalls, standingOrders: standingOrdersSource } : { terrainCalls },
  };
  if (result.hard.length > 0) {
    const citations: Record<string, string> = {};
    for (const h of result.hard) {
      const c = HARD_CITATIONS[h.rule];
      if (c) citations[h.rule] = c;
    }
    result.citations = citations;
  }

  // Enforce the 4000-char cap by trimming advisory further; hard is never cut.
  let encoded = JSON.stringify(result);
  while (encoded.length > OUTPUT_CHAR_CAP && result.advisory.length > 0) {
    result.advisory.pop();
    encoded = JSON.stringify(result);
  }

  return result;
}
