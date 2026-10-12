import { runSession } from "./session.js";

// Usage: npm run session -- --roles daily-commuter,timetable-planner --topic "Platform change alerts" --turns 6
function arg(name: string, fallback?: string): string {
  const i = process.argv.indexOf(`--${name}`);
  const v = i > -1 ? process.argv[i + 1] : fallback;
  if (v === undefined) throw new Error(`Missing --${name}`);
  return v;
}

async function main() {
  const roles = arg("roles").split(",").map((r) => r.trim());
  const topic = arg("topic");
  const maxTurns = Number(arg("turns", String(roles.length * 3)));
  const file = await runSession(roles, topic, maxTurns);
  console.log(`Done. Transcript: ${file}`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
