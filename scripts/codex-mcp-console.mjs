#!/usr/bin/env node
// Keep one MCP connection open for an interactive Codex game session.
// Chat messages arrive as JSON lines on stdout. Send a JSON line on stdin to
// call any cs1_* tool: {"id":"1","tool":"cs1_chat_say","arguments":{...}}.
import { createInterface } from "node:readline";
import { createServer } from "node:http";
import { randomBytes } from "node:crypto";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname } from "node:path";
import { networkInterfaces } from "node:os";
import { spawn } from "node:child_process";
import { Client } from "../mcp-server/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js";
import { StdioClientTransport } from "../mcp-server/node_modules/@modelcontextprotocol/sdk/dist/esm/client/stdio.js";

const node = process.execPath;
const tsx = new URL("../mcp-server/node_modules/tsx/dist/cli.mjs", import.meta.url).pathname;
const server = new URL("../mcp-server/src/index.ts", import.meta.url).pathname;
const transport = new StdioClientTransport({
  command: node,
  args: [tsx, server],
  env: { ...process.env, CS1_BRIDGE_URL: process.env.CS1_BRIDGE_URL ?? "http://127.0.0.1:32123" },
});
const client = new Client({ name: "codex-portville-console", version: "1.0.0" });
const phonePort = Number(process.env.CS1_PHONE_PORT ?? 32124);
const tokenPath = new URL("../tmp/portville/phone-chat-token", import.meta.url).pathname;
const modelPath = new URL("../tmp/portville/selected-model", import.meta.url).pathname;
let phoneToken;
try { phoneToken = readFileSync(tokenPath, "utf8").trim(); } catch {}
if (!phoneToken) {
  phoneToken = randomBytes(24).toString("hex");
  mkdirSync(dirname(tokenPath), { recursive: true });
  writeFileSync(tokenPath, phoneToken + "\n", { mode: 0o600 });
}
let selectedModel = process.env.CS1_CODEX_MODEL ?? process.env.CODEX_MODEL ?? "";
if (!selectedModel) {
  try { selectedModel = readFileSync(modelPath, "utf8").trim(); } catch {}
}

function setSelectedModel(model) {
  selectedModel = model.trim();
  mkdirSync(dirname(modelPath), { recursive: true });
  writeFileSync(modelPath, selectedModel + "\n", { mode: 0o600 });
}

function modelRequest(text) {
  const slash = text.match(/^\/model(?:\s+(.+))?$/i);
  const natural = text.match(/^(?:use|switch to|change to|set)\s+(.+?)\s+model$/i);
  if (!slash && !natural) return null;
  const value = (slash?.[1] ?? natural?.[1] ?? "").trim();
  return value.length ? value : selectedModel;
}
let bridgeUp = false;
let lastListenAt = 0;
let lastLoadedAt = 0;
let gameEpoch = 0;
const pendingMessages = [];
const queuedIds = new Set();
let processingMessage = false;
let workStatus = "Connected to Portville";

function lanAddress() {
  for (const [name, addresses] of Object.entries(networkInterfaces())) {
    if (name !== "en0") continue;
    const address = addresses?.find((entry) => entry.family === "IPv4" && !entry.internal);
    if (address) return address.address;
  }
  for (const addresses of Object.values(networkInterfaces())) {
    const address = addresses?.find((entry) => entry.family === "IPv4" && !entry.internal);
    if (address) return address.address;
  }
  return "127.0.0.1";
}

async function phoneRequest(req, res) {
  const url = new URL(req.url ?? "/", `http://127.0.0.1:${phonePort}`);
  res.setHeader("Cache-Control", "no-store");
  res.setHeader("X-Content-Type-Options", "nosniff");
  if (url.searchParams.get("token") !== phoneToken) {
    res.writeHead(401, { "Content-Type": "text/plain; charset=utf-8" });
    res.end("Invalid phone chat link");
    return;
  }
  if (req.method === "GET" && url.pathname === "/") {
    res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
    res.end(readFileSync(new URL("./phone-chat.html", import.meta.url)));
    return;
  }
  if (req.method === "GET" && url.pathname === "/api/state") {
    let gameLoaded = Date.now() - lastLoadedAt < 30000;
    try {
      const response = await fetch("http://127.0.0.1:32123/health", { signal: AbortSignal.timeout(5000) });
      if ((await response.json()).levelLoaded) {
        lastLoadedAt = Date.now();
        gameLoaded = true;
      }
    } catch {}
    res.writeHead(200, { "Content-Type": "application/json; charset=utf-8" });
    res.end(JSON.stringify({ online: bridgeUp && Date.now() - lastListenAt < 45000 && gameLoaded, gameLoaded }));
    return;
  }
  if ((req.method === "GET" && url.pathname === "/api/history") ||
      (req.method === "POST" && url.pathname === "/api/send")) {
    try {
      let body;
      if (req.method === "POST") {
        const chunks = [];
        for await (const chunk of req) {
          chunks.push(chunk);
          if (chunks.reduce((sum, item) => sum + item.length, 0) > 8192) throw new Error("Message too large");
        }
        const text = JSON.parse(Buffer.concat(chunks).toString("utf8")).text;
        if (typeof text !== "string" || !text.trim()) throw new Error("Message is empty");
        body = JSON.stringify({ text: text.slice(0, 4000) });
      }
      const path = req.method === "GET" ? "/chat/history?after=0&limit=200" : "/chat/send";
      const response = await fetch(`http://127.0.0.1:32123${path}`, {
        method: req.method,
        headers: body ? { "Content-Type": "application/json" } : undefined,
        body,
        signal: AbortSignal.timeout(5000),
      });
      const result = await response.text();
      res.writeHead(response.status, { "Content-Type": "application/json; charset=utf-8" });
      res.end(result);
    } catch (error) {
      res.writeHead(503, { "Content-Type": "application/json; charset=utf-8" });
      res.end(JSON.stringify({ ok: false, error: String(error) }));
    }
    return;
  }
  res.writeHead(404);
  res.end();
}

createServer((req, res) => { void phoneRequest(req, res); }).listen(phonePort, "0.0.0.0", () => {
  emit({ type: "phone_ready", url: `http://${lanAddress()}:${phonePort}/?token=${phoneToken}` });
  void fetch(`http://127.0.0.1:${phonePort}/api/state?token=${phoneToken}`)
    .then(async (response) => emit({ type: "phone_self_test", status: response.status, data: await response.json() }))
    .catch((error) => emit({ type: "phone_self_test", error: String(error) }));
});

function emit(value) {
  process.stdout.write(JSON.stringify(value) + "\n");
}

function payload(result) {
  const content = result.content?.find((item) => item.type === "text")?.text;
  if (!content) return result;
  try { return JSON.parse(content); } catch { return content; }
}

async function call(name, args = {}) {
  const result = await client.callTool({ name, arguments: args });
  if (result.isError) throw new Error(String(payload(result)));
  return payload(result);
}

function runCodex(message) {
  return new Promise((resolve) => {
    const outputPath = `/private/tmp/cs1-codex-reply-${process.pid}-${message.id}.txt`;
    const prompt = `You are the autonomous Codex agent controlling the live Portville city in Cities: Skylines 1. The player is using the in-game/phone chat and cannot use this terminal. You have explicit permission to control the game and its save without asking. The current goal is Phase 3, a highly interconnected city with tourism transit, pedestrian riverfront, and growth toward 100,000 residents. Read portville-master-plan.md and progress-portville.md, then inspect the live save with cs1-bridge MCP tools. Keep the game chat active: post short cs1_chat_say updates at meaningful steps, use cs1_chat_status working, and continue the cs1_chat_listen loop as needed. The host already acknowledged this player message and will post your final answer in the game chat. Work on the request now, verify changes and save when appropriate. If the game reloads, wait and reconnect. Do not ask the player for permissions.\n\nPlayer message #${message.id} (${message.source ?? "panel"}, game time ${message.gameTime ?? "unknown"}): ${message.text}\nCamera: ${JSON.stringify(message.camera ?? null)}. Selected: ${JSON.stringify(message.selected ?? null)}.\n\nThe host will place your final response in the game chat. Give a concise, truthful final status including any work still open.`;
    const args = ["exec", "--json", "-o", outputPath, "-C", new URL("..", import.meta.url).pathname, "--approve-for-me"];
    if (selectedModel) args.push("--model", selectedModel);
    args.push(prompt);
    const child = spawn("codex", args, { stdio: ["ignore", "pipe", "pipe"] });
    let stream = "";
    let stderr = "";
    let threadId = null;
    child.stdout.on("data", (chunk) => {
      stream += chunk.toString();
      let end;
      while ((end = stream.indexOf("\n")) >= 0) {
        const line = stream.slice(0, end);
        stream = stream.slice(end + 1);
        try {
          const event = JSON.parse(line);
          if (event.type === "thread.started") threadId = event.thread_id;
        } catch {}
      }
    });
    child.stderr.on("data", (chunk) => { stderr = (stderr + chunk.toString()).slice(-2000); });
    child.on("error", (error) => resolve({ ok: false, error: String(error), threadId }));
    child.on("close", (code) => {
      let reply = "";
      try { reply = readFileSync(outputPath, "utf8").trim(); } catch {}
      resolve({ ok: code === 0, reply, code, error: stderr, threadId });
    });
  });
}

async function workQueue() {
  if (processingMessage) return;
  processingMessage = true;
  try {
    while (pendingMessages.length) {
      const message = pendingMessages.shift();
      workStatus = `Working on message #${message.id}: ${message.text.slice(0, 100)}`;
      emit({ type: "codex_started", id: message.id, text: message.text });
      try { await call("cs1_chat_status", { state: "working", text: workStatus }); } catch {}
      const result = message.result ?? await runCodex(message);
      message.result = result;
      emit({ type: "codex_finished", id: message.id, ok: result.ok, threadId: result.threadId, error: result.ok ? undefined : result.error });
      const answer = result.reply || (result.ok
        ? "I finished this turn without a written summary. I am still connected; send a follow-up for a status check."
        : "I hit a temporary Codex error while working. The chat loop is still online and I will respond to your next message.");
      try {
        const replyToCurrentGame = Number.isInteger(message.id) && message.epoch === gameEpoch;
        await call("cs1_chat_say", { kind: replyToCurrentGame ? "reply" : "update", ...(replyToCurrentGame ? { inReplyTo: message.id } : {}), text: answer.slice(0, 4000) });
        workStatus = result.ok ? `Finished message #${message.id}` : `Codex error on message #${message.id}`;
        queuedIds.delete(message.id);
      } catch (error) {
        emit({ type: "reply_deferred", id: message.id, error: String(error) });
        pendingMessages.unshift(message);
        await new Promise((resolve) => setTimeout(resolve, 5000));
      }
    }
  } finally {
    processingMessage = false;
    try { await call("cs1_chat_status", { state: "idle", text: workStatus }); } catch {}
  }
}

async function enqueue(message) {
  message.epoch ??= gameEpoch;
  if (queuedIds.has(message.id)) return;
  queuedIds.add(message.id);
  const requestedModel = modelRequest(message.text.trim());
  if (requestedModel !== null) {
    if (requestedModel !== selectedModel) setSelectedModel(requestedModel);
    const label = selectedModel || "Codex default";
    emit({ type: "model_changed", model: selectedModel || null });
    await call("cs1_chat_say", {
      kind: "reply",
      ...(Number.isInteger(message.id) ? { inReplyTo: message.id } : {}),
      text: `Model set to ${label}. Future chat requests will use it.`,
    });
    workStatus = `Listening with ${label}`;
    queuedIds.delete(message.id);
    return;
  }
  if (/^(status|update|progress update\??|are you online\??|online\??)$/i.test(message.text.trim())) {
    await call("cs1_chat_say", { kind: "reply", inReplyTo: message.id, text: `Codex is online. ${workStatus}. ${pendingMessages.length} request(s) queued.` });
    queuedIds.delete(message.id);
    return;
  }
  pendingMessages.push(message);
  await call("cs1_chat_say", { kind: "update", text: `I received message #${message.id} and am working on it. ${pendingMessages.length > 1 ? `${pendingMessages.length - 1} request(s) are ahead of it.` : "I will post progress here."}` });
  void workQueue();
}

await client.connect(transport);
emit({ type: "ready", tools: (await client.listTools()).tools.length });
try {
  const backlog = await call("cs1_chat_inbox", { after: 0, wait: 0, unanswered: true });
  for (const message of backlog.messages ?? []) await enqueue(message);
} catch {}

const input = createInterface({ input: process.stdin, terminal: false });
input.on("line", async (line) => {
  let request;
  try {
    request = JSON.parse(line);
    if (typeof request.task === "string" && request.task.trim()) {
      const message = { id: request.id ?? `task-${Date.now()}`, text: request.task, source: "operator" };
      await enqueue(message);
      emit({ type: "task_queued", id: message.id });
      return;
    }
    if (typeof request.tool !== "string" || !request.tool.startsWith("cs1_")) {
      throw new Error("tool must be a cs1_* name");
    }
    const result = await client.callTool({ name: request.tool, arguments: request.arguments ?? {} });
    emit({ type: "result", id: request.id ?? null, tool: request.tool, error: Boolean(result.isError), data: payload(result) });
  } catch (error) {
    emit({ type: "error", id: request?.id ?? null, message: String(error) });
  }
});

let wasUp = false;
let everUp = false;
for (;;) {
  try {
    const result = payload(await client.callTool({ name: "cs1_chat_listen", arguments: { wait: 20 } }));
    lastListenAt = Date.now();
    bridgeUp = result.bridge === "up";
    if (result.bridge === "up" && !wasUp) {
      wasUp = true;
      emit({ type: "bridge", state: "up", gameRestarted: result.gameRestarted });
      if (result.gameRestarted && everUp) {
        gameEpoch++;
        queuedIds.clear();
      }
      everUp = true;
    } else if (result.bridge === "down" && wasUp) {
      wasUp = false;
      emit({ type: "bridge", state: "down" });
    }
    if (Array.isArray(result.messages) && result.messages.length) {
      emit({ type: "messages", messages: result.messages });
      for (const message of result.messages) {
        try { await enqueue(message); }
        catch (error) { emit({ type: "enqueue_error", id: message.id, error: String(error) }); }
      }
    }
  } catch (error) {
    emit({ type: "listen_error", message: String(error) });
    await new Promise((resolve) => setTimeout(resolve, 2000));
  }
}
