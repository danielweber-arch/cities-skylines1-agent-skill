/**
 * Pure checker.ts rule tests: no server, no bridge. Fixture io fetchers are hand-built per test.
 */
import test from "node:test";
import assert from "node:assert/strict";
import {
  checkPlan,
  parseStandingOrders,
  turnAngleDeg,
  segmentIntersection,
  rotate,
  segmentHitsNoBuild,
  footprintHitsNoBuild,
  footprintCorners,
  checkCrossRoadTurns,
  truncateName,
} from "../src/checker.ts";
import { buildGridPlan } from "../src/tools/commands.ts";
import { registerCommandTools } from "../src/tools/commands.ts";
import { resetSessionCity } from "../src/cityContext.ts";

/** Minimal fake McpServer that just records each registered tool's handler. */
function fakeServer() {
  const tools = new Map();
  return { registerTool: (name, _schema, handler) => tools.set(name, handler), tools };
}

/** Minimal fake bridge for commands.ts integration tests (no real HTTP, no mock-bridge.js). */
function fakeBridge({ cityId = "test-city", cityName = "Testville", get, post } = {}) {
  const calls = [];
  return {
    calls,
    get: async (path, query) => {
      calls.push({ method: "get", path, query });
      // A per-test override wins over the dry defaults below (it returns undefined to decline).
      if (get) {
        const custom = await get(path, query);
        if (custom !== undefined) return custom;
      }
      if (path === "/health") return { city: { id: cityId, name: cityName } };
      if (path === "/state/terrain") {
        const pts = String(query?.points ?? "")
          .split(";")
          .filter(Boolean)
          .map((pair) => {
            const [x, z] = pair.split(",").map(Number);
            return { x, z, hasWater: false, terrainHeight: 40 };
          });
        return { samples: pts };
      }
      if (path === "/state/networks") return { total: 0, returned: 0, segments: [] };
      if (path === "/state/growables") return { total: 0, returned: 0, growables: [] };
      if (path === "/state/facilities") return { total: 0, returned: 0, facilities: [] };
      return {};
    },
    post: async (path, body) => {
      calls.push({ method: "post", path, body });
      if (post) return post(path, body);
      return { ok: true };
    },
    getBinary: async () => ({ bytes: new Uint8Array(), headers: new Map() }),
  };
}

function emptyIo(overrides = {}) {
  return {
    terrainPoints: async (points) => points.map((p) => ({ x: p.x, z: p.z, hasWater: false, terrainHeight: 40 })),
    networksRoad: async () => ({ total: 0, returned: 0, segments: [] }),
    growablesResidential: async () => ({ total: 0, returned: 0, growables: [] }),
    facilitiesWater: async () => ({ total: 0, returned: 0, facilities: [] }),
    terrainGrid: async () => ({ flow: [] }),
    ...overrides,
  };
}

test("rotate() fixes the convention with a known point", () => {
  // 90 deg: x' = x*cos90 + z*sin90 = z; z' = -x*sin90 + z*cos90 = -x
  const r = rotate({ x: 10, z: 0 }, 90);
  assert.ok(Math.abs(r.x - 0) < 1e-9);
  assert.ok(Math.abs(r.z - (-10)) < 1e-9);
});

test("H-WATER blocks a ground road point over water", async () => {
  const io = emptyIo({
    terrainPoints: async (points) => points.map((p) => ({ x: p.x, z: p.z, hasWater: p.x > 100, terrainHeight: 40 })),
  });
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: 0 }, { x: 200, z: 0 }] }] };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "blocked");
  assert.ok(result.hard.some((h) => h.rule === "H-WATER"));
});

test("H-WATER exempts an elevated endpoint", async () => {
  const io = emptyIo({
    terrainPoints: async (points) => points.map((p) => ({ x: p.x, z: p.z, hasWater: true, terrainHeight: 40 })),
  });
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: 0, elevation: 10 }, { x: 10, z: 0, elevation: 10 }] }] };
  const result = await checkPlan(plan, io);
  assert.equal(result.hard.filter((h) => h.rule === "H-WATER").length, 0);
});

test("H-NOBUILD blocks a point inside a standing-order bbox", async () => {
  const io = emptyIo();
  const plan = {
    roads: [{ prefab: "Basic Road", points: [{ x: 50, z: 50 }, { x: 60, z: 60 }] }],
    standingOrders: { noBuild: [{ name: "Old Town", bbox: { minX: 0, minZ: 0, maxX: 100, maxZ: 100 } }] },
  };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "blocked");
  assert.ok(result.hard.some((h) => h.rule === "H-NOBUILD" && h.msg.includes("Old Town")));
});

test("parseStandingOrders reads NO-BUILD bbox/polygon and MAX-SPEND, ignores prose", () => {
  const md = [
    "## Standing orders",
    "",
    "Some prose the model should read but not parse.",
    "- NO-BUILD Harbor: bbox 100,200 300,400",
    "- NO-BUILD Park: polygon 0,0;10,0;10,10;0,10",
    "- MAX-SPEND 50000",
    "",
    "## Next section",
    "- NO-BUILD Ignored: bbox 0,0 1,1",
  ].join("\n");
  const orders = parseStandingOrders(md);
  assert.equal(orders.noBuild.length, 2);
  assert.equal(orders.noBuild[0].name, "Harbor");
  assert.deepEqual(orders.noBuild[0].bbox, { minX: 100, minZ: 200, maxX: 300, maxZ: 400 });
  assert.equal(orders.noBuild[1].polygon.length, 4);
  assert.equal(orders.maxSpend, 50000);
});

test("H-TURN blocks a metro track node turning more than 40 deg", async () => {
  const io = emptyIo();
  // A sharp 90 deg turn: straight north then straight east.
  const plan = {
    roads: [
      {
        prefab: "Metro Track",
        points: [
          { x: 0, z: 0 },
          { x: 0, z: 100 },
          { x: 100, z: 100 },
        ],
      },
    ],
  };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "blocked");
  assert.ok(result.hard.some((h) => h.rule === "H-TURN"));
});

test("turnAngleDeg is 0 for a straight line and ~90 for a right angle", () => {
  assert.ok(turnAngleDeg({ x: 0, z: 0 }, { x: 10, z: 0 }, { x: 20, z: 0 }) < 1e-6);
  const right = turnAngleDeg({ x: 0, z: 0 }, { x: 10, z: 0 }, { x: 10, z: 10 });
  assert.ok(Math.abs(right - 90) < 1e-6);
});

test("H-CROSSING blocks a plan segment crossing an existing one away from any node", async () => {
  const io = emptyIo({
    networksRoad: async () => ({
      total: 1,
      returned: 1,
      segments: [{ id: 1, prefab: "Basic Road", start: { x: -50, z: 0 }, end: { x: 50, z: 0 } }],
    }),
  });
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: -50 }, { x: 0, z: 50 }] }] };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "blocked");
  assert.ok(result.hard.some((h) => h.rule === "H-CROSSING"));
});

test("H-CROSSING downgrades to advisory when the network is too large to check", async () => {
  const io = emptyIo({ networksRoad: async () => ({ total: 9000, returned: 5000, segments: [] }) });
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: 0 }, { x: 10, z: 0 }] }] };
  const result = await checkPlan(plan, io);
  assert.equal(result.hard.length, 0);
  assert.ok(result.advisory.some((a) => a.rule === "H-CROSSING" && /too large/.test(a.msg)));
});

test("segmentIntersection finds the crossing point of two segments", () => {
  const hit = segmentIntersection({ x: -10, z: 0 }, { x: 10, z: 0 }, { x: 0, z: -10 }, { x: 0, z: 10 });
  assert.ok(hit);
  assert.ok(Math.abs(hit.x) < 1e-9 && Math.abs(hit.z) < 1e-9);
  assert.equal(segmentIntersection({ x: 0, z: 0 }, { x: 1, z: 0 }, { x: 0, z: 1 }, { x: 1, z: 1 }), null);
});

test("A-SHORT flags a segment under 32 m", async () => {
  const io = emptyIo();
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: 0 }, { x: 10, z: 0 }] }] };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "advisory");
  assert.ok(result.advisory.some((a) => a.rule === "A-SHORT"));
});

test("A-INDUSTRY-BUFFER flags an industrial zone within 200 m of an existing residential growable", async () => {
  const io = emptyIo({
    growablesResidential: async () => ({ total: 1, returned: 1, growables: [{ position: { x: 100, z: 0 } }] }),
  });
  const plan = { zones: [{ zone: "Industrial", center: { x: 0, z: 0 }, radius: 10 }] };
  const result = await checkPlan(plan, io);
  assert.ok(result.advisory.some((a) => a.rule === "A-INDUSTRY-BUFFER"));
});

test("A-SEWAGE flags a still water body between a plan outflow and an existing intake", async () => {
  const io = emptyIo({
    facilitiesWater: async () => ({ total: 1, returned: 1, facilities: [{ prefab: "Water Intake", position: { x: 200, z: 0 } }] }),
    terrainGrid: async () => ({ flow: [{ cells: 10, meanVelocity: { x: 0, z: 0 }, speed: 0, still: true }] }),
  });
  const plan = { buildings: [{ prefab: "Sewage Outlet", position: { x: 0, z: 0 } }] };
  const result = await checkPlan(plan, io);
  assert.ok(result.advisory.some((a) => a.rule === "A-SEWAGE"));
});

test("advisory list is capped at 20 and the whole payload stays under 4000 chars", async () => {
  const io = emptyIo();
  const roads = Array.from({ length: 40 }, (_, i) => ({
    prefab: "Basic Road",
    points: [{ x: i * 1000, z: 0 }, { x: i * 1000 + 10, z: 0 }],
  }));
  const result = await checkPlan({ roads }, io);
  assert.ok(result.advisory.length <= 20);
  assert.ok(result.advisoryTotal >= result.advisory.length);
  assert.ok(JSON.stringify(result).length <= 4000);
});

test("ok verdict when nothing fires", async () => {
  const io = emptyIo();
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: 0 }, { x: 100, z: 0 }] }] };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "ok");
  assert.equal(result.hard.length, 0);
  assert.equal(result.advisoryTotal, 0);
});

// --------------------------------------------------------------------- item 1: H-NOBUILD thin/footprint areas

test("H-NOBUILD catches a thin area between 8 m samples via segment-vs-area test", () => {
  // Road (0,0)->(16,0): 8 m samples land at x=0,8,16, none inside bbox x[3,5] z[-1,1], but the
  // segment itself passes straight through it.
  const hit = segmentHitsNoBuild({ x: 0, z: 0 }, { x: 16, z: 0 }, { name: "Sliver", bbox: { minX: 3, minZ: -1, maxX: 5, maxZ: 1 } });
  assert.equal(hit, true);
  const miss = segmentHitsNoBuild({ x: 0, z: 0 }, { x: 16, z: 0 }, { name: "Elsewhere", bbox: { minX: 3, minZ: 5, maxX: 5, maxZ: 7 } });
  assert.equal(miss, false);
});

test("H-NOBUILD blocks a road segment that passes through a thin area missed by point sampling", async () => {
  const io = emptyIo();
  const plan = {
    roads: [{ prefab: "Basic Road", points: [{ x: 0, z: 0 }, { x: 16, z: 0 }] }],
    standingOrders: { noBuild: [{ name: "Sliver", bbox: { minX: 3, minZ: -1, maxX: 5, maxZ: 1 } }] },
  };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "blocked");
  assert.ok(result.hard.some((h) => h.rule === "H-NOBUILD" && h.msg.includes("Sliver")));
});

test("footprintHitsNoBuild catches a no-build area wholly inside the footprint (no corner inside it)", () => {
  const building = { prefab: "Big Building", position: { x: 0, z: 0 }, widthCells: 10, lengthCells: 10 }; // 80x80 footprint
  const corners = footprintCorners(building);
  const hit = footprintHitsNoBuild(corners, { name: "Inner", bbox: { minX: -5, minZ: -5, maxX: 5, maxZ: 5 } });
  assert.equal(hit, true);
});

test("H-NOBUILD blocks a building whose footprint contains a no-build area with no corner sampled", async () => {
  const io = emptyIo();
  const plan = {
    buildings: [{ prefab: "Big Building", position: { x: 0, z: 0 }, widthCells: 10, lengthCells: 10 }],
    standingOrders: { noBuild: [{ name: "Inner", bbox: { minX: -5, minZ: -5, maxX: 5, maxZ: 5 } }] },
  };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "blocked");
  assert.ok(result.hard.some((h) => h.rule === "H-NOBUILD" && h.msg.includes("Inner") && h.msg.includes("footprint")));
});

// --------------------------------------------------------------------- item 2: H-TURN across separate roads

test("checkCrossRoadTurns catches a 90 deg turn hidden across two separate 2-point track roads", () => {
  const roads = [
    { prefab: "Metro Track", points: [{ x: 0, z: 0 }, { x: 100, z: 0 }] },
    { prefab: "Train Track", points: [{ x: 100, z: 0 }, { x: 100, z: 100 }] },
  ];
  const findings = checkCrossRoadTurns(roads);
  assert.ok(findings.some((f) => f.rule === "H-TURN"));
});

test("H-TURN blocks a plan where two separate metro/train roads form a sharp turn at a shared node", async () => {
  const io = emptyIo();
  const plan = {
    roads: [
      { prefab: "Metro Track", points: [{ x: 0, z: 0 }, { x: 100, z: 0 }] },
      { prefab: "Train Track", points: [{ x: 100, z: 0.3 }, { x: 100, z: 100 }] }, // within 1 m merge tolerance
    ],
  };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "blocked");
  assert.ok(result.hard.some((h) => h.rule === "H-TURN" && /shared node/.test(h.msg)));
});

test("checkCrossRoadTurns does not flag a node with 3+ incident pieces (a real junction, not a through-turn)", () => {
  const roads = [
    { prefab: "Metro Track", points: [{ x: 0, z: 0 }, { x: 100, z: 0 }] },
    { prefab: "Metro Track", points: [{ x: 100, z: 0 }, { x: 100, z: 100 }] },
    { prefab: "Metro Track", points: [{ x: 100, z: 0 }, { x: 200, z: 0 }] },
  ];
  const findings = checkCrossRoadTurns(roads);
  assert.equal(findings.length, 0);
});

// --------------------------------------------------------------------- item 3: H-CROSSING elevation

test("H-CROSSING skips an elevated plan segment crossing a ground road mid-segment", async () => {
  const io = emptyIo({
    networksRoad: async () => ({
      total: 1,
      returned: 1,
      segments: [{ id: 1, prefab: "Basic Road", start: { x: -50, z: 0 }, end: { x: 50, z: 0 } }],
    }),
  });
  const plan = {
    roads: [{ prefab: "Bridge", points: [{ x: 0, z: -50, elevation: 12 }, { x: 0, z: 50, elevation: 12 }] }],
  };
  const result = await checkPlan(plan, io);
  assert.equal(result.hard.filter((h) => h.rule === "H-CROSSING").length, 0);
});

test("H-CROSSING still fires for a ground-level crossing even when the plan road's prefab is not elevated", async () => {
  const io = emptyIo({
    networksRoad: async () => ({
      total: 1,
      returned: 1,
      segments: [{ id: 1, prefab: "Basic Road", start: { x: -50, z: 0 }, end: { x: 50, z: 0 } }],
    }),
  });
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: -50 }, { x: 0, z: 50 }] }] };
  const result = await checkPlan(plan, io);
  assert.ok(result.hard.some((h) => h.rule === "H-CROSSING"));
});

// --------------------------------------------------------------------- item 4: H-CROSSING partial network rows

test("H-CROSSING still runs on the rows returned when the network is incomplete, and adds the advisory", async () => {
  const io = emptyIo({
    networksRoad: async () => ({
      total: 5000,
      returned: 1,
      segments: [{ id: 1, prefab: "Basic Road", start: { x: -50, z: 0 }, end: { x: 50, z: 0 } }],
    }),
  });
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: -50 }, { x: 0, z: 50 }] }] };
  const result = await checkPlan(plan, io);
  assert.ok(result.hard.some((h) => h.rule === "H-CROSSING"), "must still flag a hit found in the returned rows");
  assert.ok(result.advisory.some((a) => a.rule === "H-CROSSING" && /too large|incomplete/i.test(a.msg)));
});

// --------------------------------------------------------------------- item 7: standing orders parsing/union

test("parseStandingOrders normalises a bbox given as max-then-min corners", () => {
  const md = ["## Standing orders", "- NO-BUILD Flipped: bbox 100,100 0,0"].join("\n");
  const orders = parseStandingOrders(md);
  assert.deepEqual(orders.noBuild[0].bbox, { minX: 0, minZ: 0, maxX: 100, maxZ: 100 });
});

test("parseStandingOrders reads every Standing orders / Protected areas section, not just the first", () => {
  const md = [
    "## Standing orders",
    "- NO-BUILD First: bbox 0,0 10,10",
    "## Some other section",
    "prose",
    "## Protected areas",
    "- NO-BUILD Second: bbox 20,20 30,30",
  ].join("\n");
  const orders = parseStandingOrders(md);
  assert.equal(orders.noBuild.length, 2);
  assert.ok(orders.noBuild.some((a) => a.name === "First"));
  assert.ok(orders.noBuild.some((a) => a.name === "Second"));
});

test("checkPlan unions plan-supplied standingOrders on top of the city's, never replacing them", async () => {
  const io = emptyIo();
  const cityMd = ["## Standing orders", "- NO-BUILD CityArea: bbox 0,0 10,10"].join("\n");
  const plan = {
    buildings: [{ prefab: "Shed", position: { x: 5, z: 5 } }], // inside the CITY's area only
    standingOrders: { noBuild: [{ name: "PlanArea", bbox: { minX: 100, minZ: 100, maxX: 110, maxZ: 110 } }] },
  };
  const result = await checkPlan(plan, io, cityMd);
  assert.equal(result.verdict, "blocked");
  assert.ok(result.hard.some((h) => h.msg.includes("CityArea")), "the city's own order must still apply even though the plan supplied its own standingOrders");
});

test("CheckResult.sources.standingOrders surfaces the source label when passed through", async () => {
  const io = emptyIo();
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: 0 }, { x: 100, z: 0 }] }] };
  const result = await checkPlan(plan, io, undefined, "none: no city context dir resolved");
  assert.equal(result.sources.standingOrders, "none: no city context dir resolved");
});

// --------------------------------------------------------------------- item 8: A-SEWAGE region membership

test("A-SEWAGE only warns when outflow and intake share the SAME sampled water region bbox", async () => {
  const io = emptyIo({
    facilitiesWater: async () => ({ total: 1, returned: 1, facilities: [{ prefab: "Water Intake", position: { x: 50, z: 0 } }] }),
    terrainGrid: async () => ({
      flow: [
        { cells: 10, meanVelocity: { x: 0, z: 0 }, speed: 0, still: true, bbox: { minX: 1000, maxX: 2000, minZ: 1000, maxZ: 2000 } }, // unrelated still pond
        { cells: 10, meanVelocity: { x: 0, z: 0 }, speed: 0, still: true, bbox: { minX: -10, maxX: 60, minZ: -10, maxZ: 10 } }, // the region containing both facilities
      ],
    }),
  });
  const plan = { buildings: [{ prefab: "Sewage Outlet", position: { x: 0, z: 0 } }] };
  const result = await checkPlan(plan, io);
  const sewage = result.advisory.filter((a) => a.rule === "A-SEWAGE");
  assert.equal(sewage.length, 1, "the unrelated still pond must not also produce a warning");
  assert.match(sewage[0].msg, /same/i);
});

test("A-SEWAGE reports inconclusive instead of warning when a facility isn't on any sampled region", async () => {
  const io = emptyIo({
    facilitiesWater: async () => ({ total: 1, returned: 1, facilities: [{ prefab: "Water Intake", position: { x: 9999, z: 9999 } }] }),
    terrainGrid: async () => ({
      flow: [{ cells: 10, meanVelocity: { x: 0, z: 0 }, speed: 0, still: true, bbox: { minX: -10, maxX: 10, minZ: -10, maxZ: 10 } }],
    }),
  });
  const plan = { buildings: [{ prefab: "Sewage Outlet", position: { x: 0, z: 0 } }] };
  const result = await checkPlan(plan, io);
  assert.ok(result.advisory.some((a) => a.rule === "A-SEWAGE" && /inconclusive/i.test(a.msg)));
});

// --------------------------------------------------------------------- item 9: H-WATER sampling failure

test("H-WATER-UNVERIFIED fires HARD when terrain sampling returns fewer rows than requested", async () => {
  const io = emptyIo({
    terrainPoints: async (points) => points.slice(0, points.length - 1).map((p) => ({ x: p.x, z: p.z, hasWater: false, terrainHeight: 40 })),
  });
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: 0 }, { x: 100, z: 0 }] }] };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "blocked");
  assert.ok(result.hard.some((h) => h.rule === "H-WATER-UNVERIFIED"));
});

test("H-WATER-UNVERIFIED fires HARD when terrain sampling throws", async () => {
  const io = emptyIo({
    terrainPoints: async () => {
      throw new Error("bridge unreachable");
    },
  });
  const plan = { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: 0 }, { x: 100, z: 0 }] }] };
  const result = await checkPlan(plan, io);
  assert.equal(result.verdict, "blocked");
  assert.ok(result.hard.some((h) => h.rule === "H-WATER-UNVERIFIED"));
});

// --------------------------------------------------------------------- item 10: message caps + segment counting

test("truncateName caps a name to 40 chars", () => {
  const long = "x".repeat(60);
  assert.equal(truncateName(long).length, 40);
  assert.equal(truncateName("short"), "short");
});

test("a 40+ char road/area name is capped in the H-NOBUILD finding message", async () => {
  const io = emptyIo();
  const longName = "N".repeat(60);
  const plan = {
    roads: [{ prefab: "Basic Road", name: longName, points: [{ x: 5, z: 5 }, { x: 6, z: 6 }] }],
    standingOrders: { noBuild: [{ name: longName, bbox: { minX: 0, minZ: 0, maxX: 10, maxZ: 10 } }] },
  };
  const result = await checkPlan(plan, io);
  const finding = result.hard.find((h) => h.rule === "H-NOBUILD");
  assert.ok(finding);
  assert.ok(!finding.msg.includes(longName), "the full 60-char name must not appear verbatim");
  assert.ok(finding.msg.includes("N".repeat(40)));
});

test("checked.segments counts road PIECES (points-1 per road), not road count", async () => {
  const io = emptyIo();
  const plan = {
    roads: [
      { prefab: "Basic Road", points: [{ x: 0, z: 0 }, { x: 100, z: 0 }, { x: 100, z: 100 }, { x: 100, z: 200 }] }, // 3 pieces
      { prefab: "Basic Road", points: [{ x: 0, z: 0 }, { x: 100, z: 0 }] }, // 1 piece
    ],
  };
  const result = await checkPlan(plan, io);
  assert.equal(result.checked.segments, 4);
});

// --------------------------------------------------------------------- item 5: lattice-piece grid plans

test("buildGridPlan emits one PlanRoad per lattice piece, each exactly `spacing` long", () => {
  const plan = buildGridPlan({ origin: { x: 0, z: 0 }, cols: 2, rows: 1, spacing: 80, roadPrefab: "Basic Road" });
  assert.ok(plan && plan.roads);
  assert.equal(plan.roads.length, (1 + 1) * 2 + (2 + 1) * 1); // horizontal + vertical pieces
  for (const r of plan.roads) {
    const [a, b] = r.points;
    assert.ok(Math.abs(Math.hypot(b.x - a.x, b.z - a.z) - 80) < 1e-9);
  }
});

test("buildGridPlan lattice pieces land on real nodes, so an existing road meeting the lattice exactly at a node is not a false H-CROSSING", async () => {
  const io = emptyIo({
    networksRoad: async () => ({
      total: 1,
      returned: 1,
      segments: [{ id: 1, prefab: "Basic Road", start: { x: 80, z: -50 }, end: { x: 80, z: 50 } }],
    }),
  });
  const plan = buildGridPlan({ origin: { x: 0, z: 0 }, cols: 2, rows: 1, spacing: 80, roadPrefab: "Basic Road" });
  const result = await checkPlan(plan, io);
  assert.equal(result.hard.filter((h) => h.rule === "H-CROSSING").length, 0);
});

// --------------------------------------------------------------------- item 11: session-bound prefix on a block

test("a blocked plan-check error is prefixed with the auto-declared session city", async () => {
  resetSessionCity();
  const bridge = fakeBridge({
    cityId: "zzz",
    cityName: "Prefixville",
    get: (path, query) => {
      if (path === "/state/terrain") {
        const pts = String(query?.points ?? "")
          .split(";")
          .filter(Boolean)
          .map((pair) => {
            const [x, z] = pair.split(",").map(Number);
            return { x, z, hasWater: true, terrainHeight: 40 };
          });
        return { samples: pts };
      }
      return undefined;
    },
  });
  const server = fakeServer();
  registerCommandTools(server, bridge);
  const handler = server.tools.get("cs1_build_network");
  const result = await handler({ roadPrefab: "Basic Road", start: { x: 0, z: 0 }, end: { x: 10, z: 0 } });
  assert.equal(result.isError, true);
  assert.match(result.content[0].text, /^ERROR: plan check blocked:.*\[session bound to Prefixville \(zzz\)\]$/s);
});

// --------------------------------------------------------------------- item 6: cs1_connect dry-run plan check

test("cs1_connect (toService Road) plan-checks the bridge's real dry-run target and blocks on water before the real POST", async () => {
  resetSessionCity();
  const bridge = fakeBridge({
    cityId: "city-1",
    cityName: "Connectville",
    get: (path, query) => {
      if (path === "/state/terrain") {
        const pts = String(query?.points ?? "")
          .split(";")
          .filter(Boolean)
          .map((pair) => {
            const [x, z] = pair.split(",").map(Number);
            return { x, z, hasWater: x >= 700, terrainHeight: 40 };
          });
        return { samples: pts };
      }
      return undefined;
    },
    post: (path, body) => {
      if (path === "/commands/connect") {
        return { ok: true, dryRun: body.dryRun === true, alreadyConnected: false, targetPosition: { x: 700, z: 0 } };
      }
      return { ok: true };
    },
  });
  const server = fakeServer();
  registerCommandTools(server, bridge);
  const handler = server.tools.get("cs1_connect");
  const result = await handler({ from: { x: 690, z: 0 }, toService: "Road", roadPrefab: "Basic Road" });
  assert.equal(result.isError, true);
  assert.match(result.content[0].text, /H-WATER/);
  const realConnectCalls = bridge.calls.filter((c) => c.method === "post" && c.path === "/commands/connect" && c.body.dryRun !== true);
  assert.equal(realConnectCalls.length, 0, "a HARD-blocked connect plan must never reach the real (non-dry-run) connect call");
});

test("cs1_connect falls back to no plan check when the dry run reports alreadyConnected", async () => {
  resetSessionCity();
  const bridge = fakeBridge({
    cityId: "city-2",
    cityName: "Fallbackville",
    post: (path, body) => ({ ok: true, dryRun: body.dryRun === true, alreadyConnected: true, targetPosition: { x: 10, z: 10 } }),
  });
  const server = fakeServer();
  registerCommandTools(server, bridge);
  const handler = server.tools.get("cs1_connect");
  const result = await handler({ from: { x: 0, z: 0 }, toService: "Road" });
  assert.notEqual(result.isError, true);
  const connectPosts = bridge.calls.filter((c) => c.method === "post" && c.path === "/commands/connect");
  assert.equal(connectPosts.length, 2, "probe dry-run, then the real call");
  assert.equal(connectPosts[0].body.dryRun, true);
  assert.notEqual(connectPosts[1].body.dryRun, true);
});

test("cs1_connect stays exempt from the plan check for non-Road services (pipes/power)", async () => {
  resetSessionCity();
  const bridge = fakeBridge({
    cityId: "city-3",
    cityName: "Pipeville",
    post: (path) => (path === "/commands/connect" ? { ok: true, dryRun: false, alreadyConnected: false } : { ok: true }),
  });
  const server = fakeServer();
  registerCommandTools(server, bridge);
  const handler = server.tools.get("cs1_connect");
  const result = await handler({ from: { x: 0, z: 0 }, toService: "Water", roadPrefab: "Water Pipe" });
  assert.notEqual(result.isError, true);
  const connectPosts = bridge.calls.filter((c) => c.method === "post" && c.path === "/commands/connect");
  assert.equal(connectPosts.length, 1, "no dry-run probe for a non-Road service");
});
