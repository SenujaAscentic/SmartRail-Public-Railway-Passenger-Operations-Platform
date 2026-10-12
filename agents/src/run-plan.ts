import { existsSync } from "node:fs";
import { readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { runSession } from "./session.js";

interface PlannedSession {
  id: string;
  roles: string[];
  turns: number;
  topic: string;
  why: string;
  style?: "discovery" | "day";
}

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
// Usage: npm run plan [-- --limit 3] [-- --only <id>] [-- --plan day-in-the-life-plan.json]
function opt(name: string): string | undefined {
  const i = process.argv.indexOf(`--${name}`);
  return i > -1 ? process.argv[i + 1] : undefined;
}

const planFile = opt("plan") ?? "session-plan.json";
const PLAN = resolve(root, planFile);
const STATE = resolve(root, planFile.replace(/\.json$/, ".state.json")); // { done: { [id]: transcriptPath } }

async function main() {
  const plan: PlannedSession[] = JSON.parse(await readFile(PLAN, "utf8"));
  const state: { done: Record<string, string> } = existsSync(STATE)
    ? JSON.parse(await readFile(STATE, "utf8"))
    : { done: {} };

  const only = opt("only");
  const limit = Number(opt("limit") ?? Infinity);
  const todo = plan.filter((s) => (only ? s.id === only : !state.done[s.id])).slice(0, limit);
  console.log(`${Object.keys(state.done).length}/${plan.length} done. Running ${todo.length} now.`);

  for (const s of todo) {
    console.log(`\n=== ${s.id}: ${s.roles.join(", ")}\n    ${s.topic}`);
    try {
      state.done[s.id] = await runSession(s.roles, s.topic, s.turns, s.style);
      await writeFile(STATE, JSON.stringify(state, null, 2));
      console.log(`    saved ${state.done[s.id]}`);
    } catch (e) {
      console.error(`\nStopped at ${s.id}: ${(e as Error).message?.slice(0, 300)}`);
      console.error("Quota is probably used up. Run `npm run plan` again later to continue.");
      process.exit(2);
    }
  }
  console.log("\nAll requested sessions finished.");
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
