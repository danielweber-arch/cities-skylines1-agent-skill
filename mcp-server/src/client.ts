/**
 * Thin HTTP client for the Skylines Agent Bridge mod.
 *
 * The mod binds 127.0.0.1 and ::1 explicitly, so "localhost" is safe here, but the
 * numeric address is used by default anyway to keep one less name-resolution step
 * between a failing tool call and its cause.
 */

const DEFAULT_BASE_URL = "http://127.0.0.1:32123";

export class BridgeError extends Error {
  constructor(
    message: string,
    readonly status?: number,
  ) {
    super(message);
    this.name = "BridgeError";
  }
}

export class BridgeClient {
  constructor(
    private readonly baseUrl: string = process.env.CS1_BRIDGE_URL ?? DEFAULT_BASE_URL,
    private readonly timeoutMs: number = Number(process.env.CS1_BRIDGE_TIMEOUT_MS ?? 130_000),
  ) {}

  get url(): string {
    return this.baseUrl;
  }

  async get(path: string, query: Record<string, unknown> = {}): Promise<unknown> {
    return this.parse(await this.request("GET", path, query));
  }

  async post(path: string, body: unknown): Promise<unknown> {
    return this.parse(await this.request("POST", path, {}, body));
  }

  /** Returns raw bytes plus the response headers, for /capture. */
  async getBinary(
    path: string,
    query: Record<string, unknown> = {},
  ): Promise<{ bytes: Uint8Array; headers: Headers }> {
    const response = await this.request("GET", path, query);

    if (!response.ok) {
      // A failed capture comes back as JSON even though the happy path is a PNG.
      const text = await response.text();
      throw new BridgeError(extractError(text) ?? text, response.status);
    }

    return {
      bytes: new Uint8Array(await response.arrayBuffer()),
      headers: response.headers,
    };
  }

  private async request(
    method: string,
    path: string,
    query: Record<string, unknown>,
    body?: unknown,
  ): Promise<Response> {
    const url = new URL(path, this.baseUrl);
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null) {
        url.searchParams.set(key, String(value));
      }
    }

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);

    try {
      return await fetch(url, {
        method,
        signal: controller.signal,
        headers: body === undefined ? {} : { "content-type": "application/json" },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    } catch (error) {
      throw new BridgeError(explainTransportFailure(error, this.baseUrl));
    } finally {
      clearTimeout(timer);
    }
  }

  private async parse(response: Response): Promise<unknown> {
    const text = await response.text();

    let parsed: unknown;
    try {
      parsed = JSON.parse(text);
    } catch {
      throw new BridgeError(
        `The bridge returned a non-JSON response (HTTP ${response.status}): ${text.slice(0, 400)}`,
        response.status,
      );
    }

    if (!response.ok) {
      throw new BridgeError(extractError(text) ?? `HTTP ${response.status}`, response.status);
    }

    return parsed;
  }
}

function extractError(text: string): string | undefined {
  try {
    const parsed = JSON.parse(text) as { error?: unknown };
    return typeof parsed.error === "string" ? parsed.error : undefined;
  } catch {
    return undefined;
  }
}

function explainTransportFailure(error: unknown, baseUrl: string): string {
  const message = error instanceof Error ? error.message : String(error);

  if (error instanceof Error && error.name === "AbortError") {
    return (
      `The bridge at ${baseUrl} did not answer in time. Large composite builds can hold the ` +
      `game thread for a while — check the in-game console, and retry with the same opId so ` +
      `the operation is not applied twice.`
    );
  }

  if (/ECONNREFUSED|fetch failed/i.test(message)) {
    return (
      `Could not reach the bridge at ${baseUrl}. Cities: Skylines must be running with the ` +
      `"Skylines Agent Bridge" mod enabled in the content manager. The API answers from the ` +
      `main menu, so if this fails the mod is not loaded.`
    );
  }

  return `Request to ${baseUrl} failed: ${message}`;
}
