/**
 * Stand-in for the CS1 mod. Serves payloads shaped exactly like the real bridge, including a
 * deliberately enormous /state/networks, so the filters can be proven to do their job without
 * the game running.
 */
import { createServer } from "node:http";
import { deflateSync } from "node:zlib";

function crc32(bytes) {
  let c;
  const table = [];
  for (let n = 0; n < 256; n++) {
    c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    table[n] = c >>> 0;
  }
  let crc = 0xffffffff;
  for (const b of bytes) crc = table[(crc ^ b) & 0xff] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, "ascii"), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body));
  return Buffer.concat([length, body, crc]);
}

/** A minimal but genuinely valid 8x8 PNG, so image decoding is actually exercised. */
export function fakePng() {
  const size = 8;
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 2; // truecolour
  const raw = [];
  for (let y = 0; y < size; y++) {
    raw.push(0);
    for (let x = 0; x < size; x++) raw.push((x * 31) % 256, (y * 17) % 256, 128);
  }
  const idat = deflateSync(Buffer.from(raw));
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", ihdr),
    chunk("IDAT", idat),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

function segments(count) {
  const rows = [];
  for (let i = 1; i <= count; i++) {
    rows.push({
      id: i,
      prefab: i % 7 === 0 ? "Medium Road" : "Basic Road",
      displayName: i % 7 === 0 ? "Medium Road" : "Basic Road",
      service: "Road",
      subService: "None",
      problems: i % 97 === 0 ? "RoadNotConnected" : "",
      startNodeId: i % 53 === 0 ? 0 : i,
      endNodeId: i + 1,
      start: { x: i * 8, y: 40, z: i * 4 },
      end: { x: i * 8 + 80, y: 40, z: i * 4 },
      middle: { x: i * 8 + 40, y: 40, z: i * 4 },
    });
  }
  return rows;
}

/** Mutable so tests can flip the loaded city / unload it without restarting the mock. */
export const mockCity = {
  current: { id: "abc12345", name: "Mockville", map: "Tropical", environment: "Tropical", gameDate: "2026-10-01", population: 4211, lastSaveName: "AgentAutoSave" },
};

const ROUTES = {
  "/health": () => ({
    ok: true,
    mod: "Skylines Agent Bridge",
    levelLoaded: true,
    port: 32123,
    capabilities: ["composite-commands", "capture", "node-snapping", "idempotent-ops"],
    defaultSpacing: 80,
    snapDistance: 8,
    city: mockCity.current,
  }),
  "/state/summary": () => ({
    ok: true,
    gameTime: "2026-03-04T09:12:00",
    buildIndex: 90210,
    simulation: { paused: false, selectedSpeed: 2, finalSpeed: 2 },
    network: { nodes: 1204, segments: 1500, lanes: 6100 },
    citizens: { count: 4211 },
    demand: { residential: 62, commercial: 41, workplace: 28 },
  }),
  "/state/networks": () => {
    const rows = segments(1500);
    return { ok: true, total: rows.length, returned: rows.length, limit: 1500, serviceFilter: "Road", countsByService: { Road: rows.length }, segments: rows };
  },
  "/state/problems": () => ({
    ok: true,
    total: 42,
    returned: 42,
    limit: 200,
    counts: { building: 40, netSegment: 2 },
    countsByProblem: { NoPower: 30, TaxesTooHigh: 10, RoadNotConnected: 2 },
    problems: Array.from({ length: 42 }, (_, i) => ({
      entityType: i < 40 ? "building" : "netSegment",
      id: 500 + i,
      prefab: "Residential Low",
      displayName: "House",
      problems: i < 30 ? "NoPower" : i < 40 ? "TaxesTooHigh" : "RoadNotConnected",
      problemNames: [i < 30 ? "NoPower" : i < 40 ? "TaxesTooHigh" : "RoadNotConnected"],
      isMajor: i % 3 === 0,
      isFatal: i % 11 === 0,
      position: { x: i * 12, y: 40, z: i * 6 },
    })),
  }),
  "/state/road-anomalies": () => ({
    ok: true,
    total: 0,
    returned: 0,
    limit: 500,
    counts: {},
    anomalies: [],
  }),
  "/state/facilities": () => ({
    ok: true,
    total: 3,
    returned: 3,
    limit: 500,
    countsByService: { Electricity: 1, Water: 2 },
    countsBySubService: { None: 3 },
    facilities: [
      { id: 1, prefab: "Wind Turbine", service: "Electricity", active: true, problems: "", position: { x: 0, y: 40, z: 0 } },
      { id: 2, prefab: "Water Tower", service: "Water", active: false, problems: "NoRoadAccess", position: { x: 120, y: 40, z: -220 } },
      { id: 3, prefab: "Water Pumping Station", service: "Water", active: true, problems: "", position: { x: 300, y: 40, z: 10 } },
    ],
  }),
  "/prefabs/roads": () => ({
    ok: true,
    roads: [
      { name: "Basic Road", displayName: "Two-Lane Road" },
      { name: "Medium Road", displayName: "Four-Lane Road" },
    ],
  }),
  "/state/terrain": (url) => {
    // Deterministic: x > 600 is water (matches /state/terrain/grid below), everything else dry.
    const sampleAt = (x, z) =>
      x > 600
        ? { x, z, terrainHeight: 30, waterHeight: 35.5, hasWater: true, waterDepth: 5.5, shoreDistance: 0, shoreWaterHeight: 35.5 }
        : { x, z, terrainHeight: 42, waterHeight: 42, hasWater: false, waterDepth: 0, shoreDistance: 18.4, shoreWaterHeight: 40.1 };
    const pointsParam = url.searchParams.get("points");
    if (pointsParam) {
      const requested = pointsParam.split(";").filter(Boolean);
      const samples = requested.map((pair) => {
        const [x, z] = pair.split(",").map(Number);
        return sampleAt(x, z);
      });
      return { ok: true, count: samples.length, samples };
    }
    const x = Number(url.searchParams.get("x") ?? 0);
    const z = Number(url.searchParams.get("z") ?? 0);
    return { ok: true, count: 1, samples: [sampleAt(x, z)] };
  },
  "/state/terrain/grid": (url) => {
    const x = Number(url.searchParams.get("x") ?? 0);
    const z = Number(url.searchParams.get("z") ?? 0);
    const radius = Math.min(2048, Number(url.searchParams.get("radius") ?? 512));
    const cell = Math.max(16, Number(url.searchParams.get("cell") ?? 64));
    const n = Math.ceil((2 * radius) / cell);
    if (n > 64) {
      return { ok: false, error: "reduce radius or raise cell" };
    }
    const origin = { x: x - radius, z: z - radius };
    const rows = [];
    const counts = { dry: 0, steep: 0, water: 0, mixed: 0 };
    for (let r = 0; r < n; r++) {
      let rowStr = "";
      for (let c = 0; c < n; c++) {
        const cx = origin.x + (c + 0.5) * cell;
        // Deterministic pattern: cx > 600 is water; a diagonal band (|cx-cz| < cell) is steep.
        const cz = origin.z + (2 * radius - (r + 0.5) * cell);
        let ch;
        if (cx > 600) {
          ch = "~";
          counts.water++;
        } else if (Math.abs(cx - cz) < cell) {
          ch = "^";
          counts.steep++;
        } else {
          ch = ".";
          counts.dry++;
        }
        rowStr += ch;
      }
      rows.push(rowStr);
    }
    return {
      ok: true,
      center: { x, z },
      radius,
      cell,
      cols: n,
      rowCount: n,
      origin,
      rowOrder: "row 0 = north (max z), col 0 = west (min x)",
      legend: { ".": "dry, grade <= 8%", "^": "dry, grade > 8%", "~": "water (all 5 samples)", "?": "mixed shore (some samples wet)" },
      rows,
      minHeight: 12.3,
      maxHeight: 88,
      counts,
      flow: counts.water > 0 ? [{ id: 1, cells: counts.water, sampled: counts.water, bbox: { minX: Math.max(600, x - radius), maxX: x + radius, minZ: z - radius, maxZ: z + radius }, meanVelocity: { x: 0.2, z: -1.1 }, speed: 1.1, still: false }] : [],
      flowNote: "instantaneous surface velocity, indicative only",
      flowTruncated: 0,
    };
  },
  "/state/segment-route-share": (url) => {
    const segmentId = url.searchParams.get("segment");
    return {
      ok: true,
      segment: { id: segmentId ? Number(segmentId) : 1234, prefab: "Highway", density: 87, start: { x: 0, z: 0 }, end: { x: 80, z: 0 } },
      meaning: "vehicles whose CURRENT remaining route includes this segment at this instant; not throughput",
      scanned: 9000,
      totalActive: 16384,
      truncated: true,
      sample: true,
      pathUnitsWalked: 150000,
      budget: 150000,
      matched: 312,
      byClass: { passengerCar: 200, cargoTruck: 80, transit: 12, service: 10 },
      outsideToOutside: 40,
      fromOutside: 60,
      toOutside: 55,
      topPairs: Array.from({ length: 20 }, (_, i) => ({ from: `District${i}`, to: "outside", count: 30 - i })),
      pairsTotal: 57,
      pairsTruncated: 37,
    };
  },
  "/state/growables": (url) => ({
    ok: true,
    total: 2,
    returned: 2,
    countsByService: { Residential: 2 },
    growables: [
      { id: 1, prefab: "Low Residential", service: "Residential", position: { x: 50, z: 50 } },
      { id: 2, prefab: "Low Residential", service: "Residential", position: { x: 1700, z: 50 } },
    ],
  }),
  "/state/saves": () => ({
    ok: true,
    directory: "/Users/x/Library/Application Support/Colossal Order/Cities_Skylines/Saves",
    saves: [{ name: "AgentAutoSave", path: "/tmp/AgentAutoSave.crp", lastWriteTimeUtc: "2026-08-16T18:00:00", length: 7646765 }],
  }),
};

const chatLog = [
  { id: 1, from: "player", source: "panel", text: "build a road here", kind: "message" },
];

export function startMockBridge(port = 0) {
  const seenOps = new Map();
  const requests = [];

  const server = createServer((req, res) => {
    const url = new URL(req.url, "http://127.0.0.1");
    const path = url.pathname;
    const requestEntry = { method: req.method, url: req.url };
    requests.push(requestEntry);

    if (path === "/capture") {
      if (url.searchParams.get("mode") === "Nonsense") {
        res.writeHead(500, { "content-type": "application/json" });
        res.end(JSON.stringify({ ok: false, error: "Unknown info mode: Nonsense. Valid modes: None, Electricity, ..." }));
        return;
      }
      const png = fakePng();
      res.writeHead(200, {
        "content-type": "image/png",
        "content-length": png.length,
        "x-bridge-info-mode": url.searchParams.get("mode") === "Zone" ? "None" : (url.searchParams.get("mode") ?? "None"),
        "x-bridge-distinct-colors": "9",
      });
      res.end(png);
      return;
    }

    if (req.method === "POST") {
      let body = "";
      req.on("data", (c) => (body += c));
      req.on("end", () => {
        let parsed = {};
        try {
          parsed = JSON.parse(body || "{}");
        } catch {
          /* mirror the mod: a bad body just yields defaults */
        }
        requestEntry.body = parsed;

        if (path === "/commands/build-grid") {
          if (parsed.opId && seenOps.has(parsed.opId)) {
            res.writeHead(200, { "content-type": "application/json" });
            res.end(seenOps.get(parsed.opId));
            return;
          }
          const cols = parsed.cols ?? 4;
          const rows = parsed.rows ?? 4;
          const spacing = parsed.spacing ?? 80;
          if (cols * rows > 400) {
            res.writeHead(500, { "content-type": "application/json" });
            res.end(JSON.stringify({ ok: false, error: `cols * rows must be at most 400 (asked for ${cols * rows}).` }));
            return;
          }
          const blockCenters = [];
          for (let r = 0; r < rows; r++)
            for (let c = 0; c < cols; c++)
              blockCenters.push({
                x: (parsed.origin?.x ?? 0) + (c + 0.5) * spacing,
                z: (parsed.origin?.z ?? 0) + (r + 0.5) * spacing,
              });
          const payload = JSON.stringify({
            ok: true,
            dryRun: false,
            roadPrefab: parsed.roadPrefab,
            cols,
            rows,
            spacing,
            nodeIds: Array.from({ length: (cols + 1) * (rows + 1) }, (_, i) => 1000 + i),
            createdNodeIds: Array.from({ length: (cols + 1) * (rows + 1) }, (_, i) => 1000 + i),
            reusedNodes: 0,
            segmentIds: Array.from({ length: cols * (rows + 1) + rows * (cols + 1) }, (_, i) => 5000 + i),
            skippedSegments: 0,
            bbox: { minX: 0, maxX: cols * spacing, minZ: 0, maxZ: rows * spacing },
            blockCenters,
          });
          if (parsed.opId) seenOps.set(parsed.opId, payload);
          res.writeHead(200, { "content-type": "application/json" });
          res.end(payload);
          return;
        }

        if (path === "/commands/connect") {
          res.writeHead(200, { "content-type": "application/json" });
          res.end(JSON.stringify({ ok: true, dryRun: false, alreadyConnected: false, nodeId: 2001, targetNodeId: 1400, targetPosition: { x: 380, z: 90 }, segmentIds: [7001], createdNodeIds: [2001], distance: 42.5 }));
          return;
        }

        if (path === "/commands/set-zone") {
          res.writeHead(200, { "content-type": "application/json" });
          res.end(JSON.stringify({ ok: true, dryRun: false, zone: parsed.zone, preserveOccupied: parsed.preserveOccupied ?? true, touchedBlocks: 4, skippedOccupiedBlocks: 0, changedCells: 64 }));
          return;
        }

        // /commands/build-network and /commands/place-building fall through to the generic
        // echo handler below (unchanged, so existing echo-shaped tests keep passing). The
        // water guard itself is exercised at the inline TS layer, which must never let a
        // water-crossing plan reach these routes at all (see the "blocked before bridge" test).

        if (path === "/commands/batch") {
          // Mirrors src/BatchCommands.cs: {dryRun, stopOnError, commands:[{type,...}]}, at most 32,
          // results [{index, type, result}] with the build-road response inside `result`. A
          // command whose name contains "FAIL-HERE" fails (non-dry) so partial builds are testable;
          // ?failAt=N on the URL does the same by index.
          const commands = Array.isArray(parsed.commands) ? parsed.commands : [];
          if (commands.length > 32) {
            res.writeHead(500, { "content-type": "application/json" });
            res.end(JSON.stringify({ ok: false, error: "Batch command limit is 32." }));
            return;
          }
          const batchDry = parsed.dryRun === true;
          const stopOnError = parsed.stopOnError !== false;
          const failAt = Number(url.searchParams.get("failAt") ?? -1);
          const results = [];
          let allOk = true;
          let executed = 0;
          for (let i = 0; i < commands.length; i++) {
            const cmd = commands[i];
            const dry = cmd.dryRun === undefined ? batchDry : cmd.dryRun === true;
            const type = cmd.type ?? "";
            let result;
            if (type !== "build-road" && type !== "set-zone") {
              result = { ok: false, error: `Unsupported command type: ${type}` };
            } else if (!dry && (i === failAt || String(cmd.name ?? "").includes("FAIL-HERE"))) {
              result = { ok: false, error: `simulated failure at item ${i}` };
            } else if (type === "build-road") {
              const wet = [cmd.start, cmd.end].some((pt) => pt && Number(pt.x) > 600 && Math.abs(Number(pt.elevation ?? 0)) < 1);
              result = wet
                ? { ok: false, error: "Cannot build on water: endpoint" }
                : { ok: true, dryRun: dry, segmentId: 9500 + i, createdNodeIds: [], waterCheck: { onWater: false } };
            } else {
              result = { ok: true, dryRun: dry, changedCells: 8 };
            }
            results.push({ index: i, type, result });
            if (result.ok) {
              executed++;
            } else {
              allOk = false;
              if (stopOnError) {
                for (let j = i + 1; j < commands.length; j++) results.push({ index: j, type: commands[j].type ?? "", skipped: true });
                break;
              }
            }
          }
          res.writeHead(200, { "content-type": "application/json" });
          res.end(JSON.stringify({ ok: allOk, results, executed, allOk }));
          return;
        }

        res.writeHead(200, { "content-type": "application/json" });
        res.end(JSON.stringify({ ok: true, echo: parsed }));
      });
      return;
    }

    if (path === "/chat/history" || path === "/chat/inbox") {
      const after = Number(url.searchParams.get("after") ?? 0);
      const limit = Number(url.searchParams.get("limit") ?? 50);
      const entries = chatLog.filter((message) => message.id > after).slice(0, limit);
      const latestId = chatLog.length ? chatLog[chatLog.length - 1].id : 0;
      const body =
        path === "/chat/inbox"
          ? { ok: true, messages: entries.filter((message) => message.from === "player"), lastId: latestId }
          : { ok: true, entries, latestId, returned: entries.length };
      res.writeHead(200, { "content-type": "application/json" });
      res.end(JSON.stringify(body));
      return;
    }

    const handler = ROUTES[path];
    if (!handler) {
      res.writeHead(404, { "content-type": "application/json" });
      res.end(JSON.stringify({ ok: false, error: "Not found" }));
      return;
    }

    res.writeHead(200, { "content-type": "application/json" });
    res.end(JSON.stringify(handler(url)));
  });

  return new Promise((resolve) => {
    server.listen(port, "127.0.0.1", () => {
      resolve({ server, port: server.address().port, requests });
    });
  });
}
