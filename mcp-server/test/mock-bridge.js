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

const ROUTES = {
  "/health": () => ({
    ok: true,
    mod: "Skylines Agent Bridge",
    levelLoaded: true,
    port: 32123,
    capabilities: ["composite-commands", "capture", "node-snapping", "idempotent-ops"],
    defaultSpacing: 80,
    snapDistance: 8,
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

  const server = createServer((req, res) => {
    const url = new URL(req.url, "http://127.0.0.1");
    const path = url.pathname;

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
    res.end(JSON.stringify(handler()));
  });

  return new Promise((resolve) => {
    server.listen(port, "127.0.0.1", () => {
      resolve({ server, port: server.address().port });
    });
  });
}
