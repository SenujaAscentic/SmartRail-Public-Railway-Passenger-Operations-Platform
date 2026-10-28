// Runs a Maestro flow against the running mobile stack and copies the screenshots to docs/qa/evidence/mobile/.
// Usage: node run.mjs flows/m1-m2.yaml       (the stack and the simulator must be running, see README)
import { execFileSync, spawnSync } from "node:child_process";
import { copyFileSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const flow = process.argv[2];
if (!flow) throw new Error("Usage: node run.mjs <flow.yaml>");
const state = JSON.parse(readFileSync(join(here, ".state", "stack.json"), "utf-8"));
// Maestro starts only once the API reports train 1001 as live (the simulator is sending positions): without a position the app has no
// "Updated N s ago" yet, and how soon that happens depends on the demo clock, not on the app.
async function waitForLive() {
  const get = async (path) => (await fetch(state.api + path)).json();
  const id = async (q) => (await get("/api/v1/stations?query=" + q))[0].id;
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Colombo" }).format(new Date());
  const found = await get("/api/v1/journeys/search?from=" + (await id("Gampaha")) + "&to=" + (await id("Kandy")) + "&date=" + today + "&limit=50");
  const journeyId = found.find((j) => j.serviceNumber === "1001").journeyId;
  const until = Date.now() + 240_000;
  while (Date.now() < until) {
    if ((await get("/api/v1/journeys/" + journeyId)).freshness?.tracking === "Live") return;
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw new Error("Train 1001 is not live after 240 s: is the simulator running? See .state/logs/simulator-1001.log");
}
await waitForLive();

const outDir = join(here, ".output");
rmSync(outDir, { recursive: true, force: true });

// Maestro 2.11 saves a screenshot under <output dir>/<flow>/takeScreenshot/, so the run copies them out afterwards.
const maestro = spawnSync(
  "maestro",
  ["test", "--test-output-dir", outDir, "-e", `API=${state.api}`, "-e", `CONTROLLER_PASSWORD=${state.demoPassword}`, flow],
  { cwd: here, encoding: "utf-8", shell: true, env: { ...process.env, MAESTRO_CLI_NO_ANALYTICS: "1", MAESTRO_CLI_ANALYSIS_NOTIFICATION_DISABLED: "true" } },
);
const text = maestro.stdout + maestro.stderr;
process.stdout.write(text);
mkdirSync(join(here, ".state", "logs"), { recursive: true });
writeFileSync(join(here, ".state", "logs", "last-run.txt"), text);

const evidence = resolve(here, "..", "docs", "qa", "evidence", "mobile");
mkdirSync(evidence, { recursive: true });
const walk = (dir) => readdirSync(dir).flatMap((n) => (statSync(join(dir, n)).isDirectory() ? walk(join(dir, n)) : [join(dir, n)]));
for (const file of walk(outDir).filter((f) => f.endsWith(".png") && basename(dirname(f)) === "takeScreenshot")) copyFileSync(file, join(evidence, basename(file)));
process.exit(maestro.status ?? 1);
