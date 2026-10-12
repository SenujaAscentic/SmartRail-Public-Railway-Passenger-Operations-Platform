import { buildGraph } from "./graph.js";
import { listPersonaSlugs, loadPersonas } from "./personas.js";
import { writeSession } from "./writers.js";

/** Runs one discussion and writes the transcript and summaries. Returns the transcript path. */
export async function runSession(
  roles: string[],
  topic: string,
  maxTurns: number,
  style: "discovery" | "day" = "discovery",
): Promise<string> {
  const available = await listPersonaSlugs();
  const unknown = roles.filter((r) => !available.includes(r));
  if (unknown.length) {
    throw new Error(`No persona file for: ${unknown.join(", ")}. Available: ${available.join(", ") || "(none)"}`);
  }
  const graph = buildGraph(await loadPersonas(roles, style));
  const result = await graph.invoke({ topic, roles, maxTurns, style });
  return writeSession(result);
}
