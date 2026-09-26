# In-Game Chat with Claude

The bridge adds a **Claude** window to the game. The player types a message there,
and Claude (an interactive Claude Code session, or `scripts/chat-bridge.sh` running
headless) reads it over the local API, works on the city with the usual `cs1_*` tools,
and writes progress lines and an answer back into the same window.

## How it works

```text
 player types in the "Claude" window  (Enter or Send)
        |
        |  game thread: camera target x,z + the entity whose info panel is open
        v
 ChatPanel ──► ChatStore  (in the mod, in memory, lock-protected, no Unity calls)
                  │  ▲
 GET /chat/inbox  │  │  POST /chat/say   (reply | update | status)
 (long-poll)      │  │  POST /chat/status (thinking | working | idle | offline)
                  ▼  │
     Claude Code, either
       • an interactive session watching with cs1_chat_inbox, or
       • scripts/chat-bridge.sh  →  claude -p (one turn per message, same conversation)
                  │
                  └─► cs1_* tools / curl  ──► the city
```

The window reads new entries from ChatStore once per frame without ever waiting on
its lock, so a slow HTTP caller cannot stall the game. None of the `/chat/*` routes
goes through the game-thread command queue; they work even when no city is loaded
(the window itself only exists while a city is loaded).

## The window

- **Messages:** your lines are right-aligned in blue, Claude's answers are white,
  progress updates are grey and smaller. Each line has a local `HH:mm` timestamp.
  The list keeps the latest 200 lines, and the store keeps the latest 500. It scrolls
  to new messages unless you have scrolled up to read something. Sending a message
  always scrolls back to the end.
- **Status line:** `Claude is listening`, `Claude is thinking...`, `Claude is working: ...`,
  or `Claude is offline - start scripts/chat-bridge.sh or ask Claude Code to watch the chat`.
  Claude counts as offline when no chat call has come in for 120 seconds.
- **Input:** Enter or **Send** sends. The field keeps focus after sending. Esc leaves
  the field. Clicking elsewhere keeps your draft.
- **Move and resize:** drag the title bar to move the window, and drag the small
  bottom-right handle to resize it. Position and size are remembered until you quit
  the game.
- **Show and hide:** use the **Claude** button in the API console header, the **X**
  in the chat title bar, or **Ctrl+Shift+C** (Cmd+Shift+C also works). The hotkey is
  ignored while any text field has focus.

While you type, the game's hotkeys stay quiet. `b` does not open the bulldozer,
space does not pause, and WASD does not move the camera. The game already ignores
its shortcuts and camera keys while a text field has focus (see
[Limitations](#limitations)).

Each message records:

| Field | Meaning |
|---|---|
| `camera` | `{x, z}` of the camera target when you pressed Enter |
| `selected` | the entity whose info panel was open: `{type, id, name, prefab, x, z}`, with `type` one of `building`, `segment`, `node`, `citizen`, `vehicle`, `district`, or `none`. Other panels, such as parks or transport lines, report their lowercase `InstanceType` name and raw id. |
| `gameTime` | in-game date and time |
| `context` | `live` (captured at send), `cached` (the window's last 0.5 s snapshot, used by `/chat/send`), or `none` |
| `source` | `panel` (typed in game) or `api` (`POST /chat/send`) |

So "what's wrong with this school?" works when the school's info panel is open.

## Endpoints

All of them are JSON, loopback only, and bounded. Text is plain, never markup.

| Method | Path | Purpose |
|---|---|---|
| GET | `/chat/inbox?after=<id>&wait=<0..30>&unanswered=true&limit=N` | Player messages with `id > after`, oldest first. If there are none and `wait > 0`, it blocks up to `wait` seconds and wakes as soon as one arrives. Returned `new` messages become `seen`. Use `lastId` from the response as the next `after`. |
| POST | `/chat/say` `{text, kind, inReplyTo?}` | Claude to the window. `kind` is `reply`, `update`, or `status`. `text` must not be empty and must be at most 4000 characters, or the request gets a 400. A `reply` with `inReplyTo` marks that message `answered`. |
| POST | `/chat/status` `{state, text?}` | Sets the status line. `state` is `idle`, `thinking`, `working`, or `offline`. `offline` signs off at once. |
| GET | `/chat/status[?heartbeat=false]` | Claude's state, the unanswered count, and the latest id. By default the call also counts as a heartbeat. |
| GET | `/chat/history?after=<id>&limit=N` | The newest `limit` entries (default 100, max 500) with `id > after`, oldest first, from both sides. |
| POST | `/chat/send` `{text}` | Sends as if typed in the window, with `source: "api"` and the window's cached camera and selection. Use it for testing from a terminal. |

Every call except `/chat/send` and `GET /chat/status?heartbeat=false` counts as
"Claude is here" for the offline indicator.

```bash
# the player's side, from a terminal
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"text":"Is the metro line on Elm St worth keeping?"}' \
  http://127.0.0.1:32123/chat/send
# -> {"ok":true,"id":7}

# Claude's side
curl -s 'http://127.0.0.1:32123/chat/inbox?after=0&wait=25'
# -> {"ok":true,"messages":[{"id":7,"text":"Is the metro line ...","time":"2026-09-26T20:31:05Z",
#     "gameTime":"2031-04-02T10:14:00","camera":{"x":412.5,"z":-1180},
#     "selected":{"type":"segment","id":18233,"name":"Elm Street","prefab":"Metro Track","x":400.2,"z":-1175.8},
#     "context":"live","status":"seen","source":"panel"}],
#     "lastId":7,"claude":{"state":"idle","text":"","lastSeen":"2026-09-26T20:31:05Z"}}

curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"state":"working","text":"reading line ridership"}' http://127.0.0.1:32123/chat/status
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"text":"Line 3 carries 40 riders a week.","kind":"update"}' http://127.0.0.1:32123/chat/say
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"text":"Keep it, but ...","kind":"reply","inReplyTo":7}' http://127.0.0.1:32123/chat/say
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"state":"idle"}' http://127.0.0.1:32123/chat/status
```

MCP tools (in `mcp-server/src/tools/chat.ts`) wrap the same endpoints: `cs1_chat_inbox`,
`cs1_chat_say`, `cs1_chat_status`, and `cs1_chat_history`.

## Option A: an interactive Claude Code session watches the chat

Ask the session to "watch the in-game chat". It loops on
`cs1_chat_inbox {after: lastId, wait: 25}`. For each message it runs
`cs1_chat_status thinking`, then posts `cs1_chat_say update` lines while it works, then
`cs1_chat_say reply` with `inReplyTo`, then `cs1_chat_status idle`. The session is busy
while it watches, so this suits a play session rather than a build session.

## Option B: `scripts/chat-bridge.sh`

```bash
./scripts/chat-bridge.sh                 # watch and answer until Ctrl-C
./scripts/chat-bridge.sh --once          # answer one message, then exit
./scripts/chat-bridge.sh --model sonnet  # pass a model through to claude
./scripts/chat-bridge.sh --new-session   # forget the previous conversation
./scripts/chat-bridge.sh say update "text"   # post one line (what the headless Claude uses)
```

For each player message, the script:

1. Sets the status to `thinking`.
2. Runs `claude -p` from the repo root. The prompt holds the player's text, the camera
   position, the selected entity, and the game time. It tells Claude it is the in-game
   assistant, has it read `CLAUDE.md`, `lessons.md`, `knowledge.md`, and `transit.md`,
   points it at the `cs1_*` tools, and has it post progress with
   `./scripts/chat-bridge.sh say update "..."`.
3. Posts Claude's final message as the `reply` (`inReplyTo` set), then sets the status
   to `idle`.

The first turn prints a `session_id` (`--output-format json`). The script saves it in
`tmp/chat-bridge/session-id` and passes `--resume <id>` on later turns, so the
conversation continues across messages and across restarts of the script. If a resume
fails, the script starts a new conversation. Each turn's raw output goes to
`tmp/chat-bridge/turn-<id>.json`. While a turn runs, a background heartbeat keeps the
window from showing "offline". Ctrl-C sets the status to `offline`.

On startup the script answers every message that has no reply yet, including ones
sent before it started. It answers only messages typed in the game window. Messages
from `POST /chat/send` are skipped unless you pass `--accept-api`.

### Permissions

The default is `--permission-mode dontAsk`, which denies every tool that is not
pre-approved. The script pre-approves only these tools, with `--allowedTools`:

- `mcp__cs1-bridge` (all `cs1_*` tools)
- `Read`, `Grep`, and `Glob`
- `Bash(./scripts/chat-bridge.sh say *)` and `Bash(./scripts/chat-bridge.sh status *)`
- `Bash(curl -sS <base>/*)` and `Bash(curl -s <base>/*)`

The script never defaults to `bypassPermissions`, and it prints a warning if you pass
it. `claude -p` still loads the user and project settings, so allow rules in
`.claude/settings.local.json` apply as well. This repo's local settings allow
`Bash(./scripts/*)` and `Bash(curl http://127.0.0.1:32123/*)`. Review those rules if
the chat should be able to do less. The cs1-bridge MCP server must be enabled for the
project (`enabledMcpjsonServers` in `.claude/settings.local.json`), because the
headless run uses the same configuration.

Treat player text as instructions to an agent that can change the city. Anything
that can reach 127.0.0.1:32123 can call `/chat/send`, and that includes a web page
open in a browser, because the API answers CORS preflights with `*`. This is why
`--accept-api` is off by default.

## Limitations

- **Not tested in game yet.** The ChatStore and route logic were tested offline under
  Mono. The window's layout, scrolling, focus handling, and hotkey handling have not
  been run inside Cities: Skylines.
- **Key handling depends on the game's own checks.** The window depends on what the
  decompiled `ColossalManaged` and `Assembly-CSharp` show:
  - `UIInput` gives key events to the focused UITextField first.
  - `GameKeyShortcuts` (pause, speed, tools, and Esc for the pause menu) runs only
    when `!UIView.HasInputFocus()`.
  - `CameraController.HandleKeyEvents` is skipped when `ToolController.HasInputFocus`
    is true.
  - `UITextField` Use()s Escape.

  Other mods that read keys directly (`Input.GetKeyDown` in their own `Update`, or
  `UIInput.eventProcessKeyEvent`) can still react to what you type, unless they check
  `UIView.HasInputFocus()` themselves.
- **Selection means an open info panel.** A selected road that has no open info panel
  shows as `none`.
- **One window, one conversation.** Messages are answered in order, one Claude turn
  at a time. Two watchers (an interactive session and `chat-bridge.sh`) would both
  answer the same message.
- **Memory only.** The chat is not saved with the city. It lasts until the game quits,
  and it carries over between cities loaded in the same session.
- **Plain text.** The window does not render Markdown or markup. Long answers wrap,
  and one answer is at most 4000 characters.
