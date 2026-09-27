#!/usr/bin/env node
/**
 * MCP server for the Cities: Skylines 1 Agent Bridge.
 *
 * The mod stays a dumb HTTP server; this is the translation layer that gives the model typed
 * arguments, validation before the game ever sees a request, and filtered responses. It runs
 * on the Mac next to the game, over stdio.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { BridgeClient } from "./client.js";
import { registerStateTools } from "./tools/state.js";
import { registerCommandTools } from "./tools/commands.js";
import { registerTransitTools } from "./tools/transit.js";
import { registerChatTools } from "./tools/chat.js";

async function main() {
  const bridge = new BridgeClient();

  const server = new McpServer(
    { name: "cs1-bridge", version: "0.4.0" },
    {
      instructions:
        "Controls a running Cities: Skylines 1 city through the Skylines Agent Bridge mod.\n\n" +
        "Plan from the state tools, never from images. Inspect before acting, act in small " +
        "explicit steps, then verify with the same state tools you planned from.\n\n" +
        "Prefer cs1_build_grid and cs1_build_neighborhood over dozens of cs1_build_network " +
        "calls. After any road work, cs1_state_road_anomalies must report zero. After placing " +
        "a service building, always cs1_connect it. Save at phase boundaries and confirm the " +
        "file exists with cs1_state_saves.",
    },
  );

  registerStateTools(server, bridge);
  registerCommandTools(server, bridge);
  registerTransitTools(server, bridge);
  registerChatTools(server, bridge);

  // stdout is the MCP transport — anything written there that is not a protocol message
  // corrupts the session, so diagnostics go to stderr.
  process.stderr.write(`[cs1-bridge] connecting tools to ${bridge.url}\n`);

  await server.connect(new StdioServerTransport());
}

main().catch((error) => {
  process.stderr.write(`[cs1-bridge] fatal: ${error instanceof Error ? error.stack : String(error)}\n`);
  process.exit(1);
});
