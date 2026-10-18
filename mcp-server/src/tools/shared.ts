import { BridgeError } from "../client.js";

type ToolResult = {
  content: Array<{ type: "text"; text: string } | { type: "image"; data: string; mimeType: string }>;
  isError?: boolean;
};

export function text(body: string): ToolResult {
  return { content: [{ type: "text", text: body }] };
}

export function image(base64: string, mimeType: string, caption: string): ToolResult {
  return {
    content: [
      { type: "text", text: caption },
      { type: "image", data: base64, mimeType },
    ],
  };
}

/**
 * Errors come back as tool results rather than thrown exceptions so the agent sees the
 * bridge's own message — "Network prefab was not found: Basic Rd" is directly actionable,
 * an MCP transport error is not.
 */
/** Like fail(), but for a message that is already the full user-facing text (no `Error:` prefix). */
export function failMessage(message: string): ToolResult {
  return { content: [{ type: "text", text: `ERROR: ${message}` }], isError: true };
}

export function fail(error: unknown): ToolResult {
  const message =
    error instanceof BridgeError
      ? error.message
      : error instanceof Error
        ? `${error.name}: ${error.message}`
        : String(error);

  return { content: [{ type: "text", text: `ERROR: ${message}` }], isError: true };
}

/** Zone names CS1 actually accepts. There is no plain "Commercial" or "Residential". */
export const ZONE_NAMES = [
  "ResidentialLow",
  "ResidentialHigh",
  "CommercialLow",
  "CommercialHigh",
  "Industrial",
  "Office",
  "Unzoned",
] as const;

export { capJson } from "../cap.js";
