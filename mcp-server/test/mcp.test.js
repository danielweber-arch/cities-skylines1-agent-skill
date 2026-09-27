/**
 * Drives the real MCP server over stdio against the mock bridge. This is the acceptance
 * test for section 0: typed tools exist, arguments are validated before the game sees them,
 * responses stay inside the token budget, and errors arrive as readable text.
 */
import test from "node:test";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import { startMockBridge } from "./mock-bridge.js";

const here = dirname(fileURLToPath(import.meta.url));
const entry = resolve(here, "../src/index.ts");

/** Rough but consistent: ~4 characters per token. */
const tokens = (s) => Math.ceil(s.length / 4);
const textOf = (result) =>
  result.content.filter((c) => c.type === "text").map((c) => c.text).join("\n");

async function withServer(run) {
  const { server, port } = await startMockBridge();
  const transport = new StdioClientTransport({
    command: "npx",
    args: ["tsx", entry],
    env: { ...process.env, CS1_BRIDGE_URL: `http://127.0.0.1:${port}` },
    stderr: "ignore",
  });
  const client = new Client({ name: "test", version: "1.0.0" });

  try {
    await client.connect(transport);
    await run(client);
  } finally {
    await client.close().catch(() => {});
    server.close();
  }
}

test("exposes typed tools with descriptions", async () => {
  await withServer(async (client) => {
    const { tools } = await client.listTools();
    const names = tools.map((t) => t.name).sort();

    for (const required of [
      "cs1_health",
      "cs1_state_summary",
      "cs1_state_networks",
      "cs1_state_road_anomalies",
      "cs1_build_grid",
      "cs1_build_neighborhood",
      "cs1_connect",
      "cs1_capture",
      "cs1_save",
    ]) {
      assert.ok(names.includes(required), `missing tool ${required}`);
    }

    for (const tool of tools) {
      assert.ok(tool.description && tool.description.length > 40, `${tool.name} needs a real description`);
      assert.equal(tool.inputSchema.type, "object", `${tool.name} must publish an object schema`);
    }

    console.log(`  ${tools.length} tools registered`);
  });
});

test("state summaries stay inside the token budget", async () => {
  await withServer(async (client) => {
    // The raw payload is the thing we are protecting the context window from.
    const raw = await fetch(`${process.env.MOCK_URL ?? ""}`).catch(() => null);
    void raw;

    const result = await client.callTool({ name: "cs1_state_networks", arguments: {} });
    const body = textOf(result);
    const used = tokens(body);

    assert.ok(used < 2000, `summary used ~${used} tokens, budget is 2000`);

    const parsed = JSON.parse(body);
    assert.equal(parsed.total, 1500, "summary must still report the true total");
    assert.ok(parsed.byPrefab["Basic Road"] > 0, "counts by prefab must survive filtering");
    assert.ok(parsed.danglingCount > 0, "dangling segments are the actionable subset");
    assert.ok(parsed.bbox, "bounding box must survive filtering");

    console.log(`  /state/networks 1500 segments -> ~${used} tokens`);
  });
});

test("detail:full bypasses the filter", async () => {
  await withServer(async (client) => {
    const summary = textOf(await client.callTool({ name: "cs1_state_networks", arguments: {} }));
    const full = textOf(await client.callTool({ name: "cs1_state_networks", arguments: { detail: "full" } }));

    assert.ok(full.length > summary.length * 10, "full must return substantially more than the summary");
    console.log(`  summary ${tokens(summary)} tokens vs full ${tokens(full)} tokens`);
  });
});

test("problems are grouped rather than repeated", async () => {
  await withServer(async (client) => {
    const parsed = JSON.parse(textOf(await client.callTool({ name: "cs1_state_problems", arguments: {} })));

    assert.equal(parsed.total, 42);
    assert.equal(parsed.byProblem.NoPower.count, 30);
    assert.ok(parsed.byProblem.NoPower.examples.length <= 3, "at most 3 examples per problem");
    assert.ok(tokens(JSON.stringify(parsed)) < 2000);
  });
});

test("bad arguments are rejected before the game sees them", async () => {
  await withServer(async (client) => {
    // cols is capped at 20 by the schema, so this never reaches the bridge.
    const result = await client
      .callTool({ name: "cs1_build_grid", arguments: { roadPrefab: "Basic Road", origin: { x: 0, z: 0 }, cols: 9999, rows: 1 } })
      .catch((error) => ({ thrown: error }));

    const message = result.thrown ? String(result.thrown) : textOf(result);
    assert.match(message, /cols|20|invalid|validation/i, `expected a validation failure, got: ${message}`);
  });
});

test("a wrong zone name is caught by the schema", async () => {
  await withServer(async (client) => {
    // "Commercial" is the mistake a model makes every time; CS1 only has CommercialLow/High.
    const result = await client
      .callTool({
        name: "cs1_build_neighborhood",
        arguments: {
          roadPrefab: "Basic Road",
          center: { x: 0, z: 0 },
          zoneMix: { Commercial: 1 },
        },
      })
      .catch((error) => ({ thrown: error }));

    const message = result.thrown ? String(result.thrown) : textOf(result);
    assert.match(message, /Commercial|invalid|expected/i, `expected zone validation, got: ${message}`);
  });
});

test("build-grid returns block centres ready to zone", async () => {
  await withServer(async (client) => {
    const parsed = JSON.parse(
      textOf(
        await client.callTool({
          name: "cs1_build_grid",
          arguments: { roadPrefab: "Basic Road", origin: { x: 200, z: -300 }, cols: 3, rows: 3, spacing: 80, opId: "t1" },
        }),
      ),
    );

    assert.equal(parsed.ok, true);
    assert.equal(parsed.blockCenters.length, 9, "one centre per block");
    assert.deepEqual(parsed.blockCenters[0], { x: 240, z: -260 }, "first centre is origin + half a cell");
    assert.equal(parsed.segmentIds.length, 24, "3x3 lattice has 24 segments");
  });
});

test("retrying with the same opId does not build twice", async () => {
  await withServer(async (client) => {
    const args = {
      name: "cs1_build_grid",
      arguments: { roadPrefab: "Basic Road", origin: { x: 0, z: 0 }, cols: 2, rows: 2, opId: "same-key" },
    };
    const first = JSON.parse(textOf(await client.callTool(args)));
    const second = JSON.parse(textOf(await client.callTool(args)));

    assert.deepEqual(first.segmentIds, second.segmentIds, "the retry must replay, not rebuild");
  });
});

test("capture returns a real image block", async () => {
  await withServer(async (client) => {
    const result = await client.callTool({ name: "cs1_capture", arguments: { x: 400, z: 200, size: 1200, mode: "Zone" } });
    const imageBlock = result.content.find((c) => c.type === "image");

    assert.ok(imageBlock, "expected an image content block");
    assert.equal(imageBlock.mimeType, "image/png");

    const bytes = Buffer.from(imageBlock.data, "base64");
    assert.deepEqual([...bytes.subarray(0, 4)], [0x89, 0x50, 0x4e, 0x47], "must be a real PNG");

    // The mod resolves the non-existent "Zone" overlay to None; the caption must say so.
    assert.match(textOf(result), /overlay None/);
  });
});

test("bridge errors arrive as readable text, not transport failures", async () => {
  await withServer(async (client) => {
    const result = await client.callTool({ name: "cs1_capture", arguments: { x: 0, z: 0, mode: "Nonsense" } });
    assert.equal(result.isError, true);
    assert.match(textOf(result), /Unknown info mode/);
  });
});

test("an unreachable bridge explains itself", async () => {
  const transport = new StdioClientTransport({
    command: "npx",
    args: ["tsx", entry],
    // Nothing is listening here.
    env: { ...process.env, CS1_BRIDGE_URL: "http://127.0.0.1:1" },
    stderr: "ignore",
  });
  const client = new Client({ name: "test", version: "1.0.0" });

  try {
    await client.connect(transport);
    const result = await client.callTool({ name: "cs1_health", arguments: {} });
    assert.equal(result.isError, true);
    assert.match(textOf(result), /Cities: Skylines must be running|Could not reach/);
  } finally {
    await client.close().catch(() => {});
  }
});
