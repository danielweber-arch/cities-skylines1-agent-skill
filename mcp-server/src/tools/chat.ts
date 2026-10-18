import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { isBridgeDown, type BridgeClient } from "../client.js";
import { effectiveAfter, readCursor, writeCursor } from "../chatCursor.js";
import { fail, text } from "./shared.js";

type HistoryPayload = { latestId?: number; entries?: Array<{ id?: number }> };
type InboxPayload = { messages?: Array<{ id?: number }>; lastId?: number };

/**
 * The in-game chat panel. The player types in Cities: Skylines; whichever model is
 * connected reads those messages and writes replies back. Every chat endpoint only
 * touches the mod's in-memory chat store, so none of them waits on the game thread and all
 * of them work with no city loaded. The store is wiped when the city unloads.
 */
export function registerChatTools(server: McpServer, bridge: BridgeClient) {
  server.registerTool(
    "cs1_chat_listen",
    {
      description:
        "The play loop. Call this, reply, do one step of work, then call it again. Do not " +
        "exit the loop while the player is in the chat. Waits up to `wait` seconds for a new " +
        "player message, heartbeats the panel so it does not say offline, and remembers the " +
        "last id on disk so a different model can resume. If the game is reloading, this " +
        "returns bridge:\"down\" and you call it again — that is not a reason to stop. If the " +
        "city was reloaded, gameRestarted is true and the transcript starts over at id 1. " +
        "Reply to every returned player message with cs1_chat_say before you build.",
      inputSchema: {
        after: z
          .number()
          .int()
          .min(0)
          .optional()
          .describe("Only messages with id greater than this. Omit to use the saved cursor."),
        wait: z
          .number()
          .int()
          .min(0)
          .max(30)
          .optional()
          .describe("Seconds to wait, including a reload. Default 20. The panel goes offline after 120 s with no chat call."),
      },
    },
    async (args) => {
      const waitSeconds = args.wait ?? 20;
      const saved = readCursor();
      try {
        const history = (await bridge.getWhileDown(
          "/chat/history",
          { after: 0, limit: 1 },
          waitSeconds * 1000,
        )) as HistoryPayload;
        const { after, gameRestarted } = effectiveAfter(args.after, saved, history.latestId);
        const inbox = (await bridge.get("/chat/inbox", {
          after,
          wait: waitSeconds,
          unanswered: "true",
        })) as InboxPayload;
        const messages = inbox.messages ?? [];
        const lastId = messages.reduce((max, message) => Math.max(max, message.id ?? 0), after);
        writeCursor(lastId);
        return text(
          JSON.stringify({
            bridge: "up",
            gameRestarted,
            messages,
            lastId,
            action: messages.length
              ? "Reply with cs1_chat_say (set inReplyTo) before building. If you have not read cs1_master_plan this session, read it before the build. Then call cs1_chat_listen again."
              : "No new player message. If you have not read cs1_master_plan this session, read it before building. Then call cs1_chat_listen again.",
          }),
        );
      } catch (error) {
        if (isBridgeDown(error)) {
          return text(
            JSON.stringify({
              bridge: "down",
              lastId: saved,
              action:
                "The game is not answering. Call cs1_chat_listen again. Do not exit. A reload takes about a minute; quitting the loop is how the chat box goes silent.",
            }),
          );
        }
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_chat_inbox",
    {
      description:
        "Read messages the player typed in the in-game chat panel, oldest first. Prefer " +
        "cs1_chat_listen, which also resumes after a reload. Each " +
        "message carries the camera position (x,z) and the entity whose info panel was open " +
        "(building / segment / node / citizen / vehicle / district, with id, name, prefab and " +
        "position) at send time — 'this', 'here' and 'that school' usually mean those. Pass the " +
        "returned lastId as `after` next time. With wait > 0 it long-polls up to that many " +
        "seconds for a new message. Returned messages are marked seen, and calling this keeps the " +
        "panel's offline indicator off. source:'api' means the message came from " +
        "POST /chat/send (a terminal or another local process), not from the player typing in game.",
      inputSchema: {
        after: z.number().int().min(0).optional().describe("Only messages with id greater than this. Default 0."),
        wait: z
          .number()
          .int()
          .min(0)
          .max(30)
          .optional()
          .describe("Seconds to wait for a new message when there is none yet. Default 0 (return at once)."),
        unanswered: z
          .boolean()
          .optional()
          .describe("Skip messages already answered with a reply that named them in inReplyTo."),
      },
    },
    async (args) => {
      try {
        const payload = await bridge.get("/chat/inbox", {
          after: args.after ?? 0,
          wait: args.wait ?? 0,
          unanswered: args.unanswered ? "true" : undefined,
        });
        return text(JSON.stringify(payload));
      } catch (error) {
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_chat_say",
    {
      description:
        "Write into the in-game chat panel. kind 'reply' is the answer to a player message (set " +
        "inReplyTo to its id, which marks it answered); 'update' is a short progress line while " +
        "you work (shown muted — post one per meaningful step, not per tool call); 'status' is a " +
        "one-off notice. Plain text only, at most 4000 characters; the panel wraps lines. " +
        "Reply before a long build. A player who waits in silence will think you have stopped.",
      inputSchema: {
        text: z.string().min(1).max(4000),
        kind: z.enum(["reply", "update", "status"]).optional().describe("Default reply."),
        inReplyTo: z.number().int().min(1).optional().describe("Id of the player message this answers."),
      },
    },
    async (args) => {
      try {
        const payload = await bridge.post("/chat/say", {
          text: args.text,
          kind: args.kind ?? "reply",
          ...(args.inReplyTo === undefined ? {} : { inReplyTo: args.inReplyTo }),
        });
        return text(JSON.stringify(payload));
      } catch (error) {
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_chat_status",
    {
      description:
        "Set the status line under the chat panel's messages: 'thinking' when you start on a " +
        "message, 'working' (with a short text such as 'laying the metro line') during long " +
        "work, 'idle' when done. 'offline' signs off at once; otherwise the panel shows offline " +
        "by itself after 120 s without any chat call.",
      inputSchema: {
        state: z.enum(["idle", "thinking", "working", "offline"]),
        text: z.string().max(200).optional(),
      },
    },
    async (args) => {
      try {
        const payload = await bridge.post("/chat/status", { state: args.state, text: args.text ?? "" });
        return text(JSON.stringify(payload));
      } catch (error) {
        return fail(error);
      }
    },
  );

  server.registerTool(
    "cs1_chat_history",
    {
      description:
        "The chat transcript in both directions (player messages, replies, updates), oldest " +
        "first. Returns the newest `limit` entries with id greater than `after`. Use it to " +
        "recover context after a restart; use cs1_chat_inbox to receive new player messages.",
      inputSchema: {
        after: z.number().int().min(0).optional(),
        limit: z.number().int().min(1).max(500).optional().describe("Default 50."),
      },
    },
    async (args) => {
      try {
        const payload = await bridge.get("/chat/history", { after: args.after ?? 0, limit: args.limit ?? 50 });
        return text(JSON.stringify(payload));
      } catch (error) {
        return fail(error);
      }
    },
  );
}
