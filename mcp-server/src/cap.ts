/** Dependency-free so unit tests can import it under plain Node type stripping. */
/**
 * Fit a value under a character cap WITHOUT ever returning broken JSON. Order: as-is; drop the
 * optional keys in `dropOrder` one by one; trim every array to its first 3 entries plus a count;
 * last resort, a tiny valid object naming what was dropped. Returns the JSON text to send.
 */
export function capJson(value: Record<string, unknown>, cap: number, dropOrder: string[] = []): string {
  let working: Record<string, unknown> = { ...value };
  let body = JSON.stringify(working);
  if (body.length <= cap) return body;
  const dropped: string[] = [];
  for (const key of dropOrder) {
    if (!(key in working)) continue;
    const { [key]: _gone, ...rest } = working;
    working = { ...rest, dropped: [...dropped, key] };
    dropped.push(key);
    body = JSON.stringify(working);
    if (body.length <= cap) return body;
  }
  const trimmed: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(working)) {
    if (Array.isArray(v) && v.length > 3) {
      trimmed[k] = v.slice(0, 3);
      trimmed[`${k}Truncated`] = v.length - 3;
    } else {
      trimmed[k] = v;
    }
  }
  body = JSON.stringify(trimmed);
  if (body.length <= cap) return body;
  return JSON.stringify({
    ok: (value.ok as boolean | undefined) ?? true,
    truncated: true,
    sizeChars: JSON.stringify(value).length,
    cap,
    keys: Object.keys(value),
    note: "response exceeded the cap even after dropping optional fields; narrow the query (smaller radius, lower limit)",
  });
}
