import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join } from "node:path";

/** Where the last delivered chat id is kept so a different model can resume. */
export function cursorPath(): string {
  return process.env.CS1_CHAT_CURSOR ?? join(homedir(), ".cs1-bridge", "chat-cursor.json");
}

export function readCursor(path = cursorPath()): number {
  try {
    const raw = JSON.parse(readFileSync(path, "utf8")) as { lastId?: unknown };
    return typeof raw.lastId === "number" && raw.lastId >= 0 ? raw.lastId : 0;
  } catch {
    return 0;
  }
}

export function writeCursor(lastId: number, path = cursorPath()): void {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, JSON.stringify({ lastId }));
}

/**
 * The game keeps chat ids in memory and starts again at 1 after a reload.
 * A cursor from the previous city must not hide those new messages.
 */
export function effectiveAfter(
  requested: number | undefined,
  saved: number,
  latestId: number | undefined,
): { after: number; gameRestarted: boolean } {
  const base = requested ?? saved;
  if (latestId !== undefined && latestId < base) {
    return { after: 0, gameRestarted: true };
  }
  return { after: base, gameRestarted: false };
}
