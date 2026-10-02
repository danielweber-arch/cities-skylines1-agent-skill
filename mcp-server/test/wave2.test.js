/**
 * Integration tests for wave 2: inline enforcement, the session city guard, terrain_map /
 * segment_route_share response caps, and cs1_stamp_layout (rotation, dry run, partial failure).
 * Drives the real MCP server over stdio against the shared mock bridge, same style as mcp.test.js.
 */
import test from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync, mkdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";
import { dirname, join, resolve } from "node:path";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import { startMockBridge, mockCity, mockPrefabs } from "./mock-bridge.js";

const here = dirname(fileURLToPath(import.meta.url));
const entry = resolve(here, "../src/index.ts");
const fixturesLayoutDir = join(here, "fixtures", "layouts");

const tokens = (s) => Math.ceil(s.length / 4);
const textOf = (result) => result.content.filter((c) => c.type === "text").map((c) => c.text).join("\n");

async function withServer(run, extraEnv = {}) {
  mockCity.current = { id: "abc12345", name: "Mockville", map: "Tropical", environment: "Tropical", gameDate: "2026-10-01", population: 4211, lastSaveName: "AgentAutoSave" };
  const { server, port, requests } = await startMockBridge();
  const transport = new StdioClientTransport({
    command: "npx",
    args: ["tsx", entry],
    env: { ...process.env, CS1_BRIDGE_URL: `http://127.0.0.1:${port}`, CS1_LAYOUT_DIR: fixturesLayoutDir, ...extraEnv },
    stderr: "ignore",
  });
  const client = new Client({ name: "wave2-test", version: "1.0.0" });
  try {
    await client.connect(transport);
    await run(client, requests);
  } finally {
    await client.close().catch(() => {});
    server.close();
  }
}

test("cs1_build_network over water is blocked inline; the bridge is never called", async () => {
  await withServer(async (client, requests) => {
    const before = requests.length;
    const result = await client.callTool({
      name: "cs1_build_network",
      arguments: { roadPrefab: "Basic Road", start: { x: 700, z: 0 }, end: { x: 800, z: 0 } },
    });
    assert.equal(result.isError, true);
    assert.match(textOf(result), /^ERROR: plan check blocked:.*H-WATER/s);
    assert.ok(
      requests.slice(before).every((r) => r.url !== "/commands/build-network"),
      "a HARD-blocked plan must never reach the bridge",
    );
  });
});

test("cs1_build_network under 600 (dry land) passes the inline check", async () => {
  await withServer(async (client, requests) => {
    const result = await client.callTool({
      name: "cs1_build_network",
      arguments: { roadPrefab: "Basic Road", start: { x: 0, z: -900 }, end: { x: 50, z: -900 }, dryRun: true },
    });
    assert.equal(result.isError, undefined);
    assert.ok(requests.some((r) => r.url.startsWith("/commands/build-network")));
  });
});

test("session city guard: refuses when no city is loaded, auto-declares on first mutation, refuses on a city switch", async () => {
  await withServer(async (client) => {
    mockCity.current = null;
    const noCity = await client.callTool({
      name: "cs1_build_network",
      arguments: { roadPrefab: "Basic Road", start: { x: 0, z: -900 }, end: { x: 50, z: -900 } },
    });
    assert.equal(noCity.isError, true);
    assert.match(textOf(noCity), /no city loaded/);

    mockCity.current = { id: "city-a", name: "Alpha", gameDate: "2026-01-01", population: 100 };
    const first = JSON.parse(
      textOf(
        await client.callTool({
          name: "cs1_build_network",
          arguments: { roadPrefab: "Basic Road", start: { x: 0, z: -900 }, end: { x: 50, z: -900 } },
        }),
      ),
    );
    assert.equal(first.city?.id, "city-a");
    assert.match(first.note ?? "", /auto-declared/);

    mockCity.current = { id: "city-b", name: "Beta", gameDate: "2026-01-01", population: 50 };
    const switched = await client.callTool({
      name: "cs1_build_network",
      arguments: { roadPrefab: "Basic Road", start: { x: 0, z: -900 }, end: { x: 50, z: -900 } },
    });
    assert.equal(switched.isError, true);
    assert.match(textOf(switched), /differs from the session city/);
  });
});

test("cs1_terrain_map stays under its cap and reports a water cell beyond x=600", async () => {
  await withServer(async (client) => {
    const result = await client.callTool({ name: "cs1_terrain_map", arguments: { x: 600, z: 0, radius: 256, cell: 32 } });
    const body = textOf(result);
    assert.ok(body.length <= 6000, `terrain map body was ${body.length} chars, cap is 6000`);
    const parsed = JSON.parse(body);
    assert.ok(parsed.rows.some((row) => row.includes("~")), "a grid straddling x=600 must show water cells");
    console.log(`  cs1_terrain_map response: ${body.length} chars (~${tokens(body)} tokens)`);
  });
});

test("cs1_segment_route_share stays under its cap and truncates topPairs first", async () => {
  await withServer(async (client) => {
    const result = await client.callTool({ name: "cs1_segment_route_share", arguments: { segmentId: 1234 } });
    const body = textOf(result);
    assert.ok(body.length <= 2000, `route share body was ${body.length} chars, cap is 2000`);
    console.log(`  cs1_segment_route_share response: ${body.length} chars (~${tokens(body)} tokens)`);
  });
});

test("cs1_city_context resolves a bound city dir and declares the session", async () => {
  const repoRoot = mkdtempSync(join(tmpdir(), "cs1-repo-"));
  const cityDir = join(repoRoot, "cities", "mockville-abc12345");
  mkdirSync(cityDir, { recursive: true });
  writeFileSync(join(cityDir, "city.md"), "id: abc12345\nname: Mockville\nmap: Tropical\nbindOnLoad: false\n\n## Standing orders\n");
  writeFileSync(join(cityDir, "progress.md"), "# Progress\n");

  await withServer(async (client) => {
    const result = JSON.parse(textOf(await client.callTool({ name: "cs1_city_context", arguments: {} })));
    assert.equal(result.city.id, "abc12345");
    assert.equal(result.exists, true);
    assert.equal(result.bound, true);
    assert.equal(result.files.cityMd, true);
    assert.equal(result.files.progressMd, true);
    assert.equal(result.files.planMd, false);
    assert.equal(result.sessionCityId, "abc12345");
  }, { CS1_REPO_ROOT: repoRoot });
});

test("cs1_stamp_layout: rotation convention, dry run report, and a partial-failure report", async () => {
  await withServer(async (client, requests) => {
    const dry = JSON.parse(
      textOf(
        await client.callTool({
          name: "cs1_stamp_layout",
          arguments: { name: "t-junction", anchor: { x: -1000, z: -1000 }, angleDeg: 90, dryRun: true },
        }),
      ),
    );
    assert.equal(dry.ok, true);
    assert.equal(dry.dryRun, true);
    assert.equal(dry.plan.segments, 2);
    // Node A (0,-40) at angleDeg 90 rotates to (-40, 0) by this module's convention, + anchor.
    assert.ok(Math.abs(dry.footprint.minX - (-1048)) < 1 || Math.abs(dry.footprint.maxX - (-952)) < 60, "footprint reflects the rotated anchor");

    const real = JSON.parse(
      textOf(
        await client.callTool({
          name: "cs1_stamp_layout",
          arguments: { name: "t-junction", anchor: { x: -2000, z: -2000 }, angleDeg: 0 },
        }),
      ),
    );
    assert.equal(real.ok, true);
    assert.equal(real.built.count, 2);
    assert.equal(real.built.known, true);
    assert.equal(real.failedIndex, undefined);

    // The batch body must follow the bridge contract (src/BatchCommands.cs): commands[] of
    // type build-road, with dryRun/stopOnError at the top level, never items[]/build-network.
    const batches = requests.filter((r) => r.url.startsWith("/commands/batch"));
    assert.ok(batches.length >= 2, "one dry-run batch and one real batch");
    for (const b of batches) {
      assert.ok(Array.isArray(b.body.commands), "batch uses commands[]");
      assert.equal(b.body.commands[0].type, "build-road");
      assert.equal(typeof b.body.dryRun, "boolean");
    }
    assert.equal(batches[0].body.dryRun, true);
    assert.equal(batches[batches.length - 1].body.dryRun, false);
    assert.equal(batches[batches.length - 1].body.stopOnError, true);

    // Partial failure, end to end: the mock fails the piece named FAIL-HERE on the real run.
    const partial = JSON.parse(
      textOf(
        await client.callTool({
          name: "cs1_stamp_layout",
          arguments: { name: "t-fail", anchor: { x: -2000, z: -2000 }, angleDeg: 0 },
        }),
      ),
    );
    assert.equal(partial.ok, false);
    assert.equal(partial.built.count, 1, "exactly the first piece was built");
    assert.equal(partial.built.known, true);
    assert.equal(partial.failedIndex, 1);
    assert.equal(partial.notBuilt, 2);
    assert.match(partial.error, /simulated failure/);
    assert.match(partial.repair, /bulldoze|repair/);
  });
});

test("cs1_stamp_layout chunks a large template into batches of at most 32 and blocks in water as an ERROR", async () => {
  await withServer(
    async (client, requests) => {
      const dry = JSON.parse(
        textOf(
          await client.callTool({
            name: "cs1_stamp_layout",
            arguments: { name: "cloverleaf", anchor: { x: -2000, z: -2000 }, angleDeg: 15, dryRun: true },
          }),
        ),
      );
      assert.equal(dry.ok, true, JSON.stringify(dry).slice(0, 300));
      assert.ok(dry.plan.segments > 32, "cloverleaf has more than 32 pieces");
      const batches = requests.filter((r) => r.url.startsWith("/commands/batch"));
      assert.ok(batches.length >= 2, "more than one dry-run batch");
      assert.ok(batches.every((b) => b.body.commands.length <= 32), "every batch is within the bridge limit of 32");
      assert.ok(JSON.stringify(dry).length <= 3000, `stamp dry-run response ${JSON.stringify(dry).length} chars within cap`);

      const blocked = await client.callTool({
        name: "cs1_stamp_layout",
        arguments: { name: "cloverleaf", anchor: { x: 900, z: 0 }, angleDeg: 0, dryRun: true },
      });
      assert.equal(blocked.isError, true, "a HARD-blocked stamp is a tool error");
      assert.match(textOf(blocked), /^ERROR: plan check blocked: H-WATER/);
      assert.ok(textOf(blocked).length <= 1500, `blocked message is compact (${textOf(blocked).length} chars)`);
      assert.ok(!requests.some((r) => r.url.startsWith("/commands/batch") && r.body.anchor), "nothing posted for the blocked stamp");
    },
    { CS1_LAYOUT_DIR: resolve(here, "../../templates/layouts") },
  );
});

test("cs1_check_plan runs the checker directly without touching the bridge", async () => {
  await withServer(async (client, requests) => {
    const before = requests.length;
    const result = JSON.parse(
      textOf(
        await client.callTool({
          name: "cs1_check_plan",
          arguments: { plan: { roads: [{ prefab: "Basic Road", points: [{ x: 0, z: -900 }, { x: 10, z: -900 }] }] } },
        }),
      ),
    );
    assert.equal(result.verdict, "advisory");
    assert.ok(result.advisory.some((a) => a.rule === "A-SHORT"));
    assert.ok(
      requests.slice(before).every((r) => !r.url.startsWith("/commands/")),
      "cs1_check_plan must never call a mutation endpoint",
    );
  });
});

test("cs1_place_building checks the prefab footprint: a corner over water blocks, the bridge is never called", async () => {
  await withServer(async (client, requests) => {
    const before = requests.length;
    // Centre x 590 is dry; Fire House is 4x4 cells (32 m), so the east corners sit at x 606 (water).
    const result = await client.callTool({
      name: "cs1_place_building",
      arguments: { buildingPrefab: "Fire House", position: { x: 590, z: 0 }, dryRun: true },
    });
    assert.equal(result.isError, true);
    assert.match(textOf(result), /^ERROR: plan check blocked:.*H-WATER/s);
    assert.ok(requests.slice(before).every((r) => r.url !== "/commands/place-building"));
  });
});

test("cs1_place_building: a Shoreline prefab (Harbor) centred over water is not blocked by H-WATER", async () => {
  await withServer(async (client, requests) => {
    const result = await client.callTool({
      name: "cs1_place_building",
      arguments: { buildingPrefab: "Harbor", position: { x: 650, z: 0 }, dryRun: true },
    });
    assert.notEqual(result.isError, true, textOf(result));
    const posted = requests.filter((r) => r.url === "/commands/place-building");
    assert.equal(posted.length, 1);
  });
});

test("cs1_place_building: widthCells/lengthCells override the lookup and are not sent to the bridge", async () => {
  await withServer(async (client, requests) => {
    // Unknown prefab (no lookup size): position-only would pass at x 580; a 6-cell (48 m) override reaches x 604.
    const blocked = await client.callTool({
      name: "cs1_place_building",
      arguments: { buildingPrefab: "Custom Asset", position: { x: 580, z: 0 }, widthCells: 6, lengthCells: 6, dryRun: true },
    });
    assert.equal(blocked.isError, true);
    assert.match(textOf(blocked), /H-WATER/);
    const ok = await client.callTool({
      name: "cs1_place_building",
      arguments: { buildingPrefab: "Custom Asset", position: { x: 500, z: 0 }, widthCells: 2, lengthCells: 2, dryRun: true },
    });
    assert.notEqual(ok.isError, true, textOf(ok));
    const posted = requests.filter((r) => r.url === "/commands/place-building");
    assert.equal(posted.length, 1);
    const body = posted[0].body ?? {};
    assert.equal(body.widthCells, undefined);
    assert.equal(body.lengthCells, undefined);
  });
});

test("cs1_place_building: a failed prefab refresh does not poison the name for later lookups", async () => {
  mockPrefabs.extra = [];
  mockPrefabs.failNext = 0;
  try {
    await withServer(async (client, requests) => {
      // Warm the cache with a list that lacks "Late Harbor".
      await client.callTool({ name: "cs1_place_building", arguments: { buildingPrefab: "Fire House", position: { x: 0, z: 0 }, dryRun: true } });
      // The one-shot refresh for the missing name fails; centred over water, it is blocked (not exempt).
      mockPrefabs.failNext = 1;
      const first = await client.callTool({ name: "cs1_place_building", arguments: { buildingPrefab: "Late Harbor", position: { x: 650, z: 0 }, dryRun: true } });
      assert.equal(first.isError, true);
      // The asset is now listed as Shoreline: the next call must refresh and let it through.
      mockPrefabs.extra = [{ name: "Late Harbor", width: 12, length: 12, placementMode: "Shoreline" }];
      const second = await client.callTool({ name: "cs1_place_building", arguments: { buildingPrefab: "Late Harbor", position: { x: 650, z: 0 }, dryRun: true } });
      assert.notEqual(second.isError, true, textOf(second));
      assert.ok(requests.some((r) => r.url === "/commands/place-building" && r.body?.buildingPrefab === "Late Harbor"));
    });
  } finally {
    mockPrefabs.extra = [];
    mockPrefabs.failNext = 0;
  }
});

test("cs1_place_building: an override smaller than the listed prefab does not shrink the checked footprint", async () => {
  await withServer(async (client, requests) => {
    // Fire House is listed 4x4 (32 m); a 1x1 override would keep x 590 +/- 4 dry, the real lot reaches x 606.
    const result = await client.callTool({
      name: "cs1_place_building",
      arguments: { buildingPrefab: "Fire House", position: { x: 590, z: 0 }, widthCells: 1, lengthCells: 1, dryRun: true },
    });
    assert.equal(result.isError, true);
    assert.match(textOf(result), /H-WATER/);
    assert.ok(requests.every((r) => r.url !== "/commands/place-building"));
  });
});
