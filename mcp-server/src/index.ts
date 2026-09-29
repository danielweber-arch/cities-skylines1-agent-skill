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
import { registerPlanTools } from "./tools/plan.js";

async function main() {
  const bridge = new BridgeClient();

  const server = new McpServer(
    { name: "cs1-bridge", version: "0.4.0" },
    {
      instructions:
        "You are playing Cities: Skylines 1. Any model connected to this server can play. " +
        "The player talks to you in the in-game chat box.\n\n" +
        "Stay in this loop until the player says stop:\n" +
        "1. cs1_chat_listen. It waits for the player and keeps the panel from showing offline. " +
        "If it returns bridge:\"down\", call it again. A game reload takes about a minute. " +
        "Leaving the loop is how the box goes silent.\n" +
        "2. On your first listen this session, call cs1_master_plan before you build. " +
        "That file is the city plan. Later sections override earlier ones. Do not invent " +
        "a different phase order, airport site, or hub.\n" +
        "3. Reply to every player message with cs1_chat_say, inReplyTo set, before you build.\n" +
        "4. cs1_chat_status working while you act, then idle, then listen again.\n" +
        "Two minutes with no chat call and the panel says you are offline.\n\n" +
        "Plan from the state tools, not from pictures. Prefer cs1_build_grid and " +
        "cs1_build_neighborhood. After roads, cs1_state_road_anomalies must be zero. After a " +
        "service building, cs1_connect it. cs1_save uses the game's save panel: if " +
        "\"quit after saving\" is checked, that save closes the game and the chat dies.",
    },
  );

  registerStateTools(server, bridge);
  registerCommandTools(server, bridge);
  registerTransitTools(server, bridge);
  registerChatTools(server, bridge);
  registerPlanTools(server);

  // stdout is the MCP transport — anything written there that is not a protocol message
  // corrupts the session, so diagnostics go to stderr.
  process.stderr.write(`[cs1-bridge] connecting tools to ${bridge.url}\n`);

  await server.connect(new StdioServerTransport());
}

main().catch((error) => {
  process.stderr.write(`[cs1-bridge] fatal: ${error instanceof Error ? error.stack : String(error)}\n`);
  process.exit(1);
});
