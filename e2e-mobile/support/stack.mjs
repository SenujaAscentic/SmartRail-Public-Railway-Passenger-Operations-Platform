// The stack the passenger-app flows run against: a scratch PostgreSQL in Docker plus the API, all on ports of their own.
// Why not e2e/support/stack.ts? The Playwright flows use a fixed container name and ports (55433, 5099, ...), so the two suites
// would delete each other's database when run at the same time (seen on 28 Oct). This stack uses 55434 and 5098 and the
// container sr-e2e-mobile-pg; it never touches the demo (5045) or the Playwright stack. RailBites is not started: the flows
// do not use catering, and the API treats the provider as unavailable.
// Usage: node support/stack.mjs start <HH:mm> [speed]   (stays running; leave the terminal open, stop with Ctrl+C)
//        node support/stack.mjs stop
import { execFileSync, spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { closeSync, mkdirSync, openSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, "..", "..");
const stateDir = join(here, "..", ".state");
export const stateFile = join(stateDir, "stack.json");
const logs = join(stateDir, "logs");
export const ports = { postgres: 55434, api: 5098 };
const container = "sr-e2e-mobile-pg";

const secret = (bytes = 16) => randomBytes(bytes).toString("hex");
const run = (command, args) => execFileSync(command, args, { encoding: "utf-8", stdio: ["ignore", "pipe", "pipe"] });

function killTree(pid) {
  try {
    execFileSync("taskkill", ["/PID", String(pid), "/T", "/F"], { stdio: "ignore" });
  } catch {
    // already gone
  }
}

async function waitFor(what, check, seconds) {
  const until = Date.now() + seconds * 1000;
  while (Date.now() < until) {
    try {
      if (await check()) return;
    } catch {
      // not yet
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw new Error(`${what} did not come up within ${seconds} s. See e2e-mobile/.state/logs.`);
}

export function stop() {
  try {
    const state = JSON.parse(readFileSync(stateFile, "utf-8"));
    for (const pid of state.pids) killTree(pid);
  } catch {
    // never started
  }
  try {
    run("docker", ["rm", "-f", container]);
  } catch {
    // none
  }
}

async function start(time, speed) {
  if (!/^\d\d:\d\d$/.test(time)) throw new Error(`Time must be HH:mm, got "${time}"`);
  // The app searches for "today" by the emulator's date, so the demo clock starts today (Asia/Colombo) at the given time.
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Colombo" }).format(new Date());
  const clockStart = `${today}T${time}:00+05:30`;
  const demoPassword = `E2e-${secret(8)}`;
  const cabSecrets = Object.fromEntries(["CAB-01", "CAB-02", "CAB-03", "CAB-04"].map((id) => [id, secret()]));

  stop(); // leftovers of an earlier run of THIS stack
  run("dotnet", ["build", join(repo, "backend", "src", "SmartRail.Api"), "--nologo", "-v", "quiet"]);
  run("dotnet", ["build", join(repo, "backend", "src", "SmartRail.Simulator"), "--nologo", "-v", "quiet"]);

  run("docker", ["run", "-d", "--name", container, "-e", "POSTGRES_USER=smartrail", "-e", "POSTGRES_PASSWORD=e2e-scratch", "-e", "POSTGRES_DB=smartrail_e2e", "-p", `${ports.postgres}:5432`, "postgres:17"]);
  await waitFor("PostgreSQL", async () => {
    run("docker", ["exec", container, "pg_isready", "-U", "smartrail", "-d", "smartrail_e2e"]);
    return true;
  }, 60);
  await new Promise((r) => setTimeout(r, 2000)); // pg_isready answers a moment before the init restart is over

  const env = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: `http://localhost:${ports.api}`,
    ConnectionStrings__SmartRail: `Host=localhost;Port=${ports.postgres};Database=smartrail_e2e;Username=smartrail;Password=e2e-scratch`,
    Jwt__SigningKey: secret(32),
    Seed__DemoPassword: demoPassword,
    Catering__ProviderUrl: "http://localhost:5198", // nothing listens here on purpose
    Catering__SharedSecret: secret(),
    Catering__ProviderApiKey: secret(),
    Clock__Start: clockStart,
    Clock__Speed: String(speed),
  };
  for (const [id, value] of Object.entries(cabSecrets)) env[`Seed__CabDevice__${id}__Token`] = value;

  mkdirSync(logs, { recursive: true });
  const out = openSync(join(logs, "api.log"), "w");
  const apiDir = join(repo, "backend", "src", "SmartRail.Api");
  const api = spawn("dotnet", [join(apiDir, "bin", "Debug", "net10.0", "SmartRail.Api.dll")], { cwd: apiDir, env, stdio: ["ignore", out, out], windowsHide: true });
  closeSync(out);
  const apiUrl = `http://localhost:${ports.api}`;
  await waitFor("the API", async () => (await fetch(`${apiUrl}/api/v1/lines`)).ok, 180);

  writeFileSync(stateFile, JSON.stringify({ api: apiUrl, clockStart, clockSpeed: Number(speed), demoPassword, cabSecrets, pids: [api.pid] }, null, 2));
  console.log(`Stack is up: API ${apiUrl}, demo clock starts ${clockStart}, speed ${speed}.`);
  console.log("Passwords and cab secrets are in e2e-mobile/.state/stack.json (git-ignored). Leave this terminal open; Ctrl+C stops the stack.");
  const shutdown = () => { stop(); process.exit(0); };
  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
  api.on("exit", (code) => { console.error(`The API stopped (exit ${code}). See e2e-mobile/.state/logs/api.log`); stop(); process.exit(1); });
  setInterval(() => {}, 60_000);
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [command, time = "06:05", speed = "5"] = process.argv.slice(2);
  if (command === "start") await start(time, speed);
  else if (command === "stop") { stop(); console.log("Stack stopped."); }
  else console.log("Usage: node support/stack.mjs start <HH:mm> [speed] | stop");
}
