/**
 * Response caps never break JSON: capJson drops optional fields, then trims arrays, then falls
 * back to a tiny valid object. A sliced JSON prefix would be unparsable by the model.
 */
import test from "node:test";
import assert from "node:assert/strict";
import { capJson } from "../src/cap.ts";

test("capJson returns the value unchanged when it fits", () => {
  const text = capJson({ ok: true, a: 1 }, 100, ["a"]);
  assert.deepEqual(JSON.parse(text), { ok: true, a: 1 });
});

test("capJson drops optional keys in order and records what it dropped", () => {
  const big = "x".repeat(2500);
  const text = capJson({ ok: true, segment: { id: 1 }, topPairs: [], meaning: big, byClass: { car: 1 } }, 2000, ["topPairs", "meaning", "byClass"]);
  const parsed = JSON.parse(text);
  assert.equal(parsed.ok, true);
  assert.equal(parsed.meaning, undefined);
  assert.deepEqual(parsed.dropped, ["topPairs", "meaning"]);
  assert.ok(text.length <= 2000);
});

test("capJson trims arrays with a count before giving up", () => {
  const rows = Array.from({ length: 200 }, (_, i) => ({ i, label: "row-" + i }));
  const text = capJson({ ok: true, rows }, 400, []);
  const parsed = JSON.parse(text);
  assert.equal(parsed.rows.length, 3);
  assert.equal(parsed.rowsTruncated, 197);
});

test("capJson falls back to a small valid object when nothing else fits", () => {
  const text = capJson({ ok: true, segment: { id: 17, prefab: "X".repeat(2100) }, topPairs: [] }, 2000, ["topPairs"]);
  const parsed = JSON.parse(text);
  assert.equal(parsed.truncated, true);
  assert.ok(parsed.sizeChars > 2000);
  assert.deepEqual(parsed.keys, ["ok", "segment", "topPairs"]);
  assert.ok(text.length <= 2000);
});
