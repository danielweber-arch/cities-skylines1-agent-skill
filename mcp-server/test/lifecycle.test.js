/**
 * Game lifecycle tools (cs1_state_maps, cs1_new_game, cs1_load_save) against a dedicated mock
 * bridge. Payloads mirror src/LifecycleCommands.cs; the mock records every request so the tests
 * assert what actually reached the bridge, and replays the mod's 409 refusals.
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

function map(name, env, extra = {}) {
  return {
    ref: `${name}.${name}_Data`, mapName: name, displayName: name, assetName: `${name}_Data`, packageName: name,
    environment: env, mapThemeRef: null, builtin: true, workshop: false, publishedFileId: null,
    requiredDlc: "None", dlcOwned: true, playable: true, buildableArea: 25,
    resources: { oil: 1, ore: 2, forest: 3, fertileLand: 4, water: 5, maxResourceAmount: 10 },
    connections: { roadsIn: 2, roadsOut: 2, trainIn: 1, trainOut: 1, shipIn: 0, shipOut: 0, planeIn: 1, planeOut: 1 },
    ...extra,
  };
}

const MAPS = [
  map("Green Plains", "Temperate"),
  map("Lagoon Shore", "Tropical"),
  map("Frozen Fjord", "Winter", { requiredDlc: "SnowFall", dlcOwned: false, playable: false }),
  map("Workshop Isle", "Europe", { builtin: false, workshop: true, publishedFileId: 123456 }),
];

function startMock(state) {
  const requests = [];
  const server = createServer((req, res) => {
    let body = "";
    req.on("data", (c) => (body += c));
    req.on("end", () => {
      const path = new URL(req.url, "http://127.0.0.1").pathname;
      const parsed = body ? JSON.parse(body) : undefined;
      requests.push({ method: req.method, path, body: parsed });
      const send = (status, payload) => {
        res.writeHead(status, { "content-type": "application/json" });
        res.end(JSON.stringify(payload));
      };
      if (path === "/health") return send(200, { ok: true, levelLoaded: false, enabledMods: state.mods });
      if (path === "/state/maps") return send(200, { ok: true, levelLoaded: false, count: MAPS.length, maps: MAPS });
      if (path === "/commands/new-game" || path === "/commands/load-save") {
        if (state.mods.includes("Unlimited Money")) {
          return send(409, { ok: false, error: "Refused: cheat mods are enabled (Unlimited Money). Disable them in Content Manager > Mods; the bridge will not start or load a city with them on." });
        }
        if (path === "/commands/new-game") {
          return send(200, { ok: true, dryRun: parsed.dryRun, cityName: parsed.cityName, map: MAPS[0] });
        }
        return send(200, { ok: true, dryRun: parsed.dryRun, name: parsed.name, matches: 1 });
      }
      send(404, { ok: false, error: "Unknown route" });
    });
  });
  return new Promise((r) => server.listen(0, "127.0.0.1", () => r({ server, port: server.address().port, requests })));
}

async function withServer(state, run) {
  const { server, port, requests } = await startMock(state);
  const transport = new StdioClientTransport({
    command: "npx",
    args: ["tsx", entry],
    env: { ...process.env, CS1_BRIDGE_URL: `http://127.0.0.1:${port}` },
    stderr: "ignore",
  });
  const client = new Client({ name: "test", version: "1.0.0" });
  try {
    await client.connect(transport);
    await run(client, requests);
  } finally {
    await client.close().catch(() => {});
    server.close();
  }
}

test("lifecycle tools are registered with real descriptions", async () => {
  await withServer({ mods: [] }, async (client) => {
    const { tools } = await client.listTools();
    for (const name of ["cs1_state_maps", "cs1_new_game", "cs1_load_save"]) {
      const tool = tools.find((t) => t.name === name);
      assert.ok(tool, `missing ${name}`);
      assert.ok(tool.description.length > 40);
    }
  });
});

test("cs1_state_maps hides unplayable maps by default and filters", async () => {
  await withServer({ mods: [] }, async (client) => {
    const all = JSON.parse(textOf(await client.callTool({ name: "cs1_state_maps", arguments: {} })));
    assert.equal(all.total, 4);
    assert.deepEqual(all.maps.map((m) => m.mapName), ["Green Plains", "Lagoon Shore", "Workshop Isle"]);

    const withLocked = JSON.parse(textOf(await client.callTool({ name: "cs1_state_maps", arguments: { playableOnly: false } })));
    const locked = withLocked.maps.find((m) => m.mapName === "Frozen Fjord");
    assert.equal(locked.requiredDlc, "SnowFall");

    const builtin = JSON.parse(textOf(await client.callTool({ name: "cs1_state_maps", arguments: { builtinOnly: true, environment: "tropical" } })));
    assert.deepEqual(builtin.maps.map((m) => m.mapName), ["Lagoon Shore"]);
  });
});

test("cs1_new_game forwards map, cityName, invertTraffic and dryRun", async () => {
  await withServer({ mods: [] }, async (client, requests) => {
    const result = await client.callTool({
      name: "cs1_new_game",
      arguments: { map: "Green Plains.Green Plains_Data", cityName: "  Agentburg ", dryRun: true },
    });
    assert.ok(!result.isError, textOf(result));
    const sent = requests.find((r) => r.path === "/commands/new-game");
    assert.deepEqual(sent.body, { map: "Green Plains.Green Plains_Data", cityName: "Agentburg", invertTraffic: false, dryRun: true });
  });
});

test("cs1_new_game rejects names the mod's JSON reader would truncate, before the bridge", async () => {
  await withServer({ mods: [] }, async (client, requests) => {
    const result = await client.callTool({ name: "cs1_new_game", arguments: { map: "Green Plains", cityName: 'The "Big" City' } });
    assert.ok(result.isError);
    assert.equal(requests.filter((r) => r.path === "/commands/new-game").length, 0);
  });
});

test("cheat-mod refusal reaches the agent as a readable error", async () => {
  await withServer({ mods: ["Skylines Agent Bridge", "Unlimited Money"] }, async (client) => {
    for (const [name, args] of [
      ["cs1_new_game", { map: "Green Plains", cityName: "Agentburg" }],
      ["cs1_load_save", { name: "AgentAutoSave" }],
    ]) {
      const result = await client.callTool({ name, arguments: args });
      assert.ok(result.isError, `${name} should fail`);
      assert.match(textOf(result), /Unlimited Money/);
    }
  });
});

test("cs1_load_save forwards the name and refuses paths", async () => {
  await withServer({ mods: [] }, async (client, requests) => {
    const ok = await client.callTool({ name: "cs1_load_save", arguments: { name: "AgentAutoSave", dryRun: true } });
    assert.ok(!ok.isError, textOf(ok));
    assert.deepEqual(requests.find((r) => r.path === "/commands/load-save").body, { name: "AgentAutoSave", dryRun: true });

    const bad = await client.callTool({ name: "cs1_load_save", arguments: { name: "../evil" } });
    assert.ok(bad.isError);
    assert.equal(requests.filter((r) => r.path === "/commands/load-save").length, 1);
  });
});
