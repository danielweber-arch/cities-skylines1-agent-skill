import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const HEADING = /^(#{2,3}) (.+)$/gm;

export function planPath(): string {
  if (process.env.CS1_MASTER_PLAN) return process.env.CS1_MASTER_PLAN;
  // src/ and dist/ are both one level under mcp-server/, and the plan is in the repo root.
  return join(dirname(fileURLToPath(import.meta.url)), "..", "..", "portville-master-plan.md");
}

export function readPlan(path = planPath()): string {
  return readFileSync(path, "utf8");
}

export type PlanHeading = { level: number; title: string; start: number };

export function headings(markdown: string): PlanHeading[] {
  const found: PlanHeading[] = [];
  for (const match of markdown.matchAll(HEADING)) {
    const marks = match[1];
    const title = match[2];
    if (match.index === undefined || marks === undefined || title === undefined) continue;
    found.push({ level: marks.length, title, start: match.index });
  }
  return found;
}

/** Text of the first heading whose title contains `query`, through the next heading of the same or higher rank. */
export function sectionText(markdown: string, query: string): string | undefined {
  const all = headings(markdown);
  const needle = query.toLowerCase();
  const hit = all.find((heading) => heading.title.toLowerCase().includes(needle));
  if (!hit) return undefined;
  const next = all.find((heading) => heading.start > hit.start && heading.level <= hit.level);
  return markdown.slice(hit.start, next ? next.start : markdown.length).trim();
}

/**
 * Player directives and the §10 build orders. Earlier sections are the original
 * study; when they disagree, this tail wins.
 */
export function currentOrders(markdown: string): string {
  const marker = markdown.indexOf("## 9. Player directives");
  return (marker < 0 ? markdown : markdown.slice(marker)).trim();
}
