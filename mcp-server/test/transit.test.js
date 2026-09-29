/**
 * Transit / traffic / policy tools against a dedicated mock bridge. Payloads mirror the shapes
 * TransitState.cs, TrafficState.cs and TransitCommands.cs emit, so the summarizers and the
 * argument plumbing are exercised without the game. The mock records every request so the
 * tests can assert what actually reached the bridge.
 */
import test from "node:test";
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

const here = dirname(fileURLToPath(import.meta.url));
const entry = resolve(here, "../src/index.ts");
const textOf = (result) =>
  result.content.filter((c) => c.type === "text").map((c) => c.text).join("\n");

function stop(index, x, z, waiting) {
  return {
    index, nodeId: 3000 + index, x, y: 40, z, waitingPassengers: waiting, nodeFinalCounter: 3,
    fixedPlatform: true, laneId: 90000 + index, segmentId: 700 + index, stationBuildingId: 0, problems: "",
  };
}

function line(id, type, stops, extra = {}) {
  return {
    id, number: id, name: `${type} Line ${id}`, transportType: type, prefab: type, vehicleType: "Car",
    subService: `PublicTransport${type}`, color: "#2A7FFF", flags: ["Created", "Complete"], complete: true,
    activeDay: true, activeNight: true, stopCount: stops.length, lengthMeters: 2400, vehicleCount: 3,
    targetVehicleCount: 5, budget: 100, ticketPrice: 100, averageInterval: 40, depotBuildingId: 0,
    passengers: {
      residents: 310, tourists: 12, total: 322, carOwning: 40, source: "m_averageCount",
      lastPeriod: { residents: 300, tourists: 10, source: "m_finalCount" },
      current: { residents: 20, tourists: 1, source: "m_tempCount" },
    },
    problems: [],
    stops,
    ...extra,
  };
}

function transitPayload() {
  const lines = [];
  for (let i = 1; i <= 40; i++) {
    const stops = Array.from({ length: 12 }, (_, s) => stop(s, i * 50 + s * 90, s * 40, s));
    lines.push(line(i, i % 3 === 0 ? "Metro" : "Bus", stops, i === 7 ? { problems: ["LineNotConnected"], complete: false } : {}));
  }
  return {
    ok: true, typeFilter: "", includeStops: true, limit: 256, lineCount: 40, returned: 40, cityLineCount: 40,
    passengerSource: "TransportLine.m_passengers.<group>.m_averageCount (the line panel's weekly passenger figure)",
    lines,
    totalsByType: {
      Bus: { lines: 27, completeLines: 26, stops: 324, vehicles: 81, targetVehicles: 135, passengersLastWeek: 8694 },
      Metro: { lines: 13, completeLines: 13, stops: 156, vehicles: 39, targetVehicles: 65, passengersLastWeek: 4186 },
    },
    cityPassengersByType: { source: "TransportManager.m_passengers", Bus: { residents: 8000, tourists: 694, total: 8694 }, total: { residents: 12000, tourists: 880, total: 12880 } },
    budgets: [{ subService: "None", day: 100, night: 100 }, { subService: "PublicTransportBus", day: 100, night: 90 }],
    transportPrefabs: [{ name: "Bus", transportType: "Bus", vehicleType: "Car", defaultForType: true, netService: "Road/None", stationService: "PublicTransport/PublicTransportBus", unlocked: true, creatableByBridge: true }],
    facilityCount: 3, facilitiesReturned: 3,
    facilitiesBySubService: { PublicTransportBus: 1, PublicTransportMetro: 2 },
    facilities: [
      { id: 11, prefab: "Bus Depot", subService: "PublicTransportBus", ai: "DepotAI", lineType: "Bus", active: true, problems: "", position: { x: 0, y: 40, z: 0 }, maxVehicleCount: 20, vehicleCount: 12 },
      { id: 12, prefab: "Metro Entrance", subService: "PublicTransportMetro", ai: "TransportStationAI", lineType: "Metro", active: false, problems: "NoRoadAccess", position: { x: 300, y: 40, z: 10 }, passengerCount: 44 },
      { id: 13, prefab: "Metro Entrance", subService: "PublicTransportMetro", ai: "TransportStationAI", lineType: "Metro", active: true, problems: "", position: { x: 600, y: 40, z: 10 }, passengerCount: 9 },
    ],
    modalSplit: null,
    modalSplitNote: "CS1 does not track a public-transport share",
  };
}

function trafficPayload(limit) {
  const segments = Array.from({ length: limit }, (_, i) => ({
    id: 100 + i, prefab: "Medium Road", name: "Main St", density: 100 - i, trafficBuffer: 5000,
    lengthMeters: 80, start: { x: i, y: 40, z: 0 }, end: { x: i + 80, y: 40, z: 0 }, middle: { x: i + 40, y: 40, z: 0 },
    lanes: { carLanes: 4, busLanes: 0, tramLanes: 0, trolleybusLanes: 0, pedestrianLanes: 2, parkingLanes: 2, lanesWithStops: 2 },
  }));
  return {
    ok: true, source: "NetSegment.m_trafficDensity", limit, minDensity: 0, roadSegments: 900, matching: 900,
    returned: segments.length, averageDensity: 31.5, lengthWeightedAverageDensity: 35.25,
    densityHistogram: { "0-19": 300, "20-39": 300, "40-59": 150, "60-79": 100, "80-100": 50 },
    trafficFlowPercent: 71, trafficFlowSource: "VehicleManager.m_lastTrafficFlow", segments,
  };
}

function startTransitMock() {
  const requests = [];
  const server = createServer((req, res) => {
    const url = new URL(req.url, "http://127.0.0.1");
    let body = "";
    req.on("data", (c) => (body += c));
    req.on("end", () => {
      const parsed = body ? JSON.parse(body) : undefined;
      requests.push({ method: req.method, path: url.pathname, query: Object.fromEntries(url.searchParams), body: parsed });
      const send = (status, payload) => {
        res.writeHead(status, { "content-type": "application/json" });
        res.end(JSON.stringify(payload));
      };

      if (url.pathname === "/state/transit") return send(200, transitPayload());
      if (url.pathname === "/state/traffic") return send(200, trafficPayload(Number(url.searchParams.get("limit") ?? 50)));
      if (url.pathname === "/state/policies") {
        return send(200, { ok: true, cityPolicies: ["SmokeDetectors"], districts: [{ id: 3, name: "Downtown", policies: ["HeavyTrafficBan"] }], available: [{ name: "FreeTransport", type: "Services", cityWide: true, unlocked: true }] });
      }
      if (url.pathname === "/commands/transit-line-create") {
        if (parsed?.stops?.some((s) => s.x > 9000)) {
          return send(500, { ok: false, error: "stops[1] at (9999, 0): no Road/None segment within 32 m." });
        }
        return send(200, { ok: true, dryRun: parsed?.dryRun === true, lineId: 42, echo: parsed });
      }
      if (url.pathname.startsWith("/commands/")) return send(200, { ok: true, echo: parsed });
      send(404, { ok: false, error: "Not found" });
    });
  });
  return new Promise((resolveStart) => {
    server.listen(0, "127.0.0.1", () => resolveStart({ server, port: server.address().port, requests }));
  });
}

async function withServer(run) {
  const { server, port, requests } = await startTransitMock();
  const transport = new StdioClientTransport({
    command: "npx",
    args: ["tsx", entry],
    env: { ...process.env, CS1_BRIDGE_URL: `http://127.0.0.1:${port}` },
    stderr: "ignore",
  });
  const client = new Client({ name: "transit-test", version: "1.0.0" });
  try {
    await client.connect(transport);
    await run(client, requests);
  } finally {
    await client.close().catch(() => {});
    server.close();
  }
}

/** A schema rejection may throw or come back as an error result, depending on SDK version. */
async function callExpectingFailure(client, name, args) {
  const result = await client.callTool({ name, arguments: args }).catch((error) => ({ thrown: error }));
  if (result.thrown) return String(result.thrown);
  assert.equal(result.isError, true, `expected ${name} to fail, got: ${textOf(result)}`);
  return textOf(result);
}

test("transit tools are registered with real descriptions", async () => {
  await withServer(async (client) => {
    const { tools } = await client.listTools();
    const names = tools.map((t) => t.name);
    for (const required of [
      "cs1_state_transit", "cs1_state_traffic", "cs1_state_policies", "cs1_transit_line_create",
      "cs1_transit_line_edit", "cs1_transit_line_delete", "cs1_set_service_budget", "cs1_set_policy",
    ]) {
      assert.ok(names.includes(required), `missing tool ${required}`);
      const tool = tools.find((t) => t.name === required);
      assert.ok(tool.description.length > 40, `${required} needs a real description`);
      assert.equal(tool.inputSchema.type, "object");
    }
  });
});

test("cs1_state_transit summary keeps per-line numbers and drops stops", async () => {
  await withServer(async (client, requests) => {
    const body = textOf(await client.callTool({ name: "cs1_state_transit", arguments: {} }));
    const parsed = JSON.parse(body);

    assert.equal(parsed.lineCount, 40);
    assert.equal(parsed.lines.length, 40, "every line gets a compact row");
    assert.equal(parsed.lines[0], "#1 Bus 'Bus Line 1' stops=12 veh=3/5 budget=100 pax=322(310+12) len=2400");
    assert.ok(!body.includes("waitingPassengers"), "stops must not leak into the summary");
    assert.match(parsed.lines[6], /^#7 .*\[INCOMPLETE, LineNotConnected\]$/, "broken lines keep their problems");
    assert.equal(parsed.totalsByType.Metro.lines, 13);
    assert.equal(parsed.facilityCount, 3);
    assert.equal(parsed.facilitiesNeedingAttentionCount, 1);
    assert.equal(parsed.facilitiesNeedingAttention[0].id, 12);
    assert.ok(body.length <= 16000 + 200, `summary is ${body.length} chars`);
    assert.equal(requests.at(-1).path, "/state/transit");
  });
});

test("cs1_state_transit forwards type/includeStops and full returns stops", async () => {
  await withServer(async (client, requests) => {
    const full = textOf(
      await client.callTool({ name: "cs1_state_transit", arguments: { type: "Metro", includeStops: true, detail: "full", limit: 5 } }),
    );
    assert.deepEqual(requests.at(-1).query, { type: "Metro", includeStops: "true", limit: "5" });
    assert.ok(full.includes("waitingPassengers"), "full output carries stop rows");

    const message = await callExpectingFailure(client, "cs1_state_transit", { type: "Hovercraft" });
    assert.match(message, /Hovercraft|invalid|enum|expected/i);
  });
});

test("cs1_state_traffic summarises and validates the area filter", async () => {
  await withServer(async (client, requests) => {
    const parsed = JSON.parse(textOf(await client.callTool({ name: "cs1_state_traffic", arguments: { limit: 10, minDensity: 60 } })));
    assert.equal(parsed.segments.length, 10);
    assert.equal(parsed.segments[0].density, 100);
    assert.equal(parsed.segments[0].carLanes, 4);
    assert.equal(parsed.trafficFlowPercent, 71);
    assert.equal(parsed.segments[0].start, undefined, "start/end only with detail:full");
    assert.deepEqual(requests.at(-1).query, { limit: "10", minDensity: "60" });

    await client.callTool({ name: "cs1_state_traffic", arguments: { x: 100, z: -50, radius: 300 } });
    assert.deepEqual(requests.at(-1).query, { limit: "50", x: "100", z: "-50", radius: "300" });

    const before = requests.length;
    const message = await callExpectingFailure(client, "cs1_state_traffic", { x: 100 });
    assert.match(message, /x and z/);
    assert.equal(requests.length, before, "a half-specified area never reaches the bridge");
  });
});

test("cs1_transit_line_create validates before the game sees it", async () => {
  await withServer(async (client, requests) => {
    const before = requests.length;

    let message = await callExpectingFailure(client, "cs1_transit_line_create", { transportType: "Bus", stops: [{ x: 0, z: 0 }] });
    assert.match(message, /stops|2|too_small|invalid/i);

    message = await callExpectingFailure(client, "cs1_transit_line_create", {
      transportType: "Bus", stops: [{ x: 0, z: 0 }, { x: 100, z: 0 }], color: "blue",
    });
    assert.match(message, /RRGGBB|color|invalid/i);

    message = await callExpectingFailure(client, "cs1_transit_line_create", { transportType: "Taxi", stops: [{ x: 0, z: 0 }, { x: 1, z: 1 }] });
    assert.match(message, /Taxi|invalid|enum|expected/i);

    message = await callExpectingFailure(client, "cs1_transit_line_create", { stops: [{ x: 0, z: 0 }, { x: 100, z: 0 }] });
    assert.match(message, /transportType or prefab/);

    assert.equal(requests.length, before, "none of the invalid calls may reach the bridge");
  });
});

test("cs1_transit_line_create forwards a valid request and surfaces bridge errors", async () => {
  await withServer(async (client, requests) => {
    const args = {
      transportType: "Bus",
      stops: [{ x: 0, z: 0 }, { x: 240, z: 0 }, { x: 240, z: 240 }],
      name: "Crosstown",
      color: "#FF8800",
      budget: 120,
      roadSnapDistance: 40,
      ignoreUnlock: true,
      dryRun: true,
    };
    const parsed = JSON.parse(textOf(await client.callTool({ name: "cs1_transit_line_create", arguments: args })));
    assert.equal(parsed.ok, true);
    assert.deepEqual(requests.at(-1).body, args, "the body reaches the bridge unchanged");
    assert.equal(requests.at(-1).path, "/commands/transit-line-create");

    const failed = await client.callTool({
      name: "cs1_transit_line_create",
      arguments: { transportType: "Bus", stops: [{ x: 0, z: 0 }, { x: 9999, z: 0 }] },
    });
    assert.equal(failed.isError, true);
    assert.match(textOf(failed), /no Road\/None segment within 32 m/);
  });
});

test("cs1_transit_line_edit forwards stop edits; delete/budget/policy are plumbed", async () => {
  await withServer(async (client, requests) => {
    const edit = {
      lineId: 7,
      removeStopIndexes: [4, 1],
      moveStops: [{ index: 0, x: 10, z: 20 }],
      addStops: [{ x: 50, z: 60 }, { index: 2, x: 70, z: 80 }],
      ticketPrice: 200,
    };
    await client.callTool({ name: "cs1_transit_line_edit", arguments: edit });
    assert.equal(requests.at(-1).path, "/commands/transit-line-edit");
    assert.deepEqual(requests.at(-1).body, edit);

    let message = await callExpectingFailure(client, "cs1_transit_line_edit", { lineId: 0, name: "x" });
    assert.match(message, /lineId|too_small|invalid|>=/i);

    await client.callTool({ name: "cs1_transit_line_delete", arguments: { lineId: 7, dryRun: true } });
    assert.deepEqual(requests.at(-1).body, { lineId: 7, dryRun: true });

    const before = requests.length;
    message = await callExpectingFailure(client, "cs1_set_service_budget", { service: "PublicTransport" });
    assert.match(message, /day and\/or night/);
    message = await callExpectingFailure(client, "cs1_set_service_budget", { service: "PublicTransport", day: 200 });
    assert.match(message, /150|too_big|invalid/i);
    assert.equal(requests.length, before);

    await client.callTool({ name: "cs1_set_service_budget", arguments: { service: "PublicTransport", subService: "PublicTransportBus", day: 120, night: 80 } });
    assert.deepEqual(requests.at(-1).body, { service: "PublicTransport", subService: "PublicTransportBus", day: 120, night: 80 });

    await client.callTool({ name: "cs1_set_policy", arguments: { policy: "FreeTransport", enabled: true } });
    assert.equal(requests.at(-1).path, "/commands/set-policy");
    assert.deepEqual(requests.at(-1).body, { policy: "FreeTransport", enabled: true });

    const policies = JSON.parse(textOf(await client.callTool({ name: "cs1_state_policies", arguments: {} })));
    assert.deepEqual(policies.cityPolicies, ["SmokeDetectors"]);
  });
});
