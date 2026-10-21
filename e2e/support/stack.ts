import { execFileSync, spawn, type ChildProcess } from "node:child_process";
import { randomBytes } from "node:crypto";
import { closeSync, mkdirSync, openSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

// The stack the end-to-end flows run against: a scratch PostgreSQL in Docker, the API, RailBites (the catering provider), the ops web
// and the station display, all on ports of their own (never 5045, 5100, 5173 or 5174, which are the developer's demo). Everything is
// started fresh for a run and removed at the end, so there is no reset call and no data left over. The secrets are made up per run and
// reach the processes only through their environment.

const here = dirname(fileURLToPath(import.meta.url));
export const repo = resolve(here, "..", "..");
export const stateFile = join(here, "..", ".state", "stack.json");
const logs = join(here, "..", ".state", "logs");

export const ports = { postgres: 55433, api: 5099, railBites: 5199, opsWeb: 5273, display: 5274 };

// The demo clock: a Wednesday morning, ten minutes before the first train (2001, 06:00) leaves. It runs 5 times faster than real time, so a train
// moves between stations in minutes; every latency the flows measure is real wall-clock time, whatever the clock speed.
export const clock = { start: "2026-10-21T05:50:00+05:30", speed: 5 };

export interface Stack {
  api: string;
  railBites: string;
  opsWeb: string;
  display: string;
  demoPassword: string;
  cabSecrets: Record<string, string>;
  pids: Record<string, number>;
  clockSpeed: number;
}

const container = "sr-e2e-pg";

function secret(bytes = 16) {
  return randomBytes(bytes).toString("hex");
}

function run(command: string, args: string[], options: { cwd?: string } = {}) {
  return execFileSync(command, args, { cwd: options.cwd, encoding: "utf-8", stdio: ["ignore", "pipe", "pipe"], shell: process.platform === "win32" && command === "npm" });
}

async function waitFor(what: string, check: () => Promise<boolean>, seconds: number) {
  const until = Date.now() + seconds * 1000;
  while (Date.now() < until) {
    try {
      if (await check()) return;
    } catch {
      // not yet
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw new Error(`${what} did not come up within ${seconds} s. See e2e/.state/logs.`);
}

const answers = (url: string) => async () => (await fetch(url)).ok;

function start(name: string, command: string, args: string[], cwd: string, env: Record<string, string>): ChildProcess {
  mkdirSync(logs, { recursive: true });
  const out = openSync(join(logs, `${name}.log`), "w");
  const child = spawn(command, args, { cwd, env: { ...process.env, ...env }, stdio: ["ignore", out, out], windowsHide: true, shell: process.platform === "win32" && command === "npm" });
  closeSync(out);
  return child;
}

export function killTree(pid: number) {
  try {
    if (process.platform === "win32") execFileSync("taskkill", ["/PID", String(pid), "/T", "/F"], { stdio: "ignore" });
    else process.kill(-pid, "SIGKILL");
  } catch {
    // already gone
  }
}

const dll = (project: string, name: string) => join(repo, "backend", "src", project, "bin", "Debug", "net10.0", `${name}.dll`);

function apiEnv(s: { cabSecrets: Record<string, string>; demoPassword: string; jwtKey: string; catering: { secret: string; key: string } }, clockStart: string) {
  const env: Record<string, string> = {
    ASPNETCORE_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: `http://localhost:${ports.api}`,
    ConnectionStrings__SmartRail: `Host=localhost;Port=${ports.postgres};Database=smartrail_e2e;Username=smartrail;Password=e2e-scratch`,
    Jwt__SigningKey: s.jwtKey,
    Seed__DemoPassword: s.demoPassword,
    Catering__ProviderUrl: `http://localhost:${ports.railBites}`,
    Catering__SharedSecret: s.catering.secret,
    Catering__ProviderApiKey: s.catering.key,
    Clock__Start: clockStart,
    Clock__Speed: String(clock.speed),
  };
  for (const [id, value] of Object.entries(s.cabSecrets)) env[`Seed__CabDevice__${id}__Token`] = value;
  return env;
}

export function railBitesEnv(catering: { secret: string; key: string }, clockStart: string) {
  return {
    ASPNETCORE_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: `http://localhost:${ports.railBites}`,
    ConnectionStrings__RailBites: `Host=localhost;Port=${ports.postgres};Database=railbites_e2e;Username=smartrail;Password=e2e-scratch`,
    RailBites__SharedSecret: catering.secret,
    RailBites__ApiKey: catering.key,
    Clock__Start: clockStart,
    Clock__Speed: String(clock.speed),
  };
}

// The API's own clock, read from the Date header of an answer (the same way the apps do), so a process started later can join it.
export async function apiClockNow(api: string): Promise<string> {
  const response = await fetch(`${api}/api/v1/lines`);
  return new Date(response.headers.get("date")!).toISOString();
}

// Starts RailBites (also used by a flow that stops it and brings it back). It keeps its database, so what it received stays.
export function startRailBites(catering: { secret: string; key: string }, clockStart: string) {
  const project = join(repo, "provider", "RailBites");
  return start("railbites", "dotnet", [join(project, "bin", "Debug", "net10.0", "RailBites.dll")], project, railBitesEnv(catering, clockStart));
}

export async function startStack(): Promise<Stack> {
  const demoPassword = `E2e-${secret(8)}`;
  const jwtKey = secret(32);
  const catering = { secret: secret(), key: secret() };
  const cabSecrets = Object.fromEntries(["CAB-01", "CAB-02", "CAB-03", "CAB-04"].map((id) => [id, secret()]));

  // 1. build what is run (the dev servers need the design tokens)
  run("dotnet", ["build", join(repo, "backend", "src", "SmartRail.Api"), "--nologo", "-v", "quiet"]);
  run("dotnet", ["build", join(repo, "backend", "src", "SmartRail.Simulator"), "--nologo", "-v", "quiet"]);
  run("dotnet", ["build", join(repo, "provider", "RailBites"), "--nologo", "-v", "quiet"]);
  run("npm", ["run", "build", "-w", "@smartrail/tokens"], { cwd: join(repo, "web") });

  // 2. a scratch database that is gone at the end
  try {
    run("docker", ["rm", "-f", container]);
  } catch {
    // none
  }
  run("docker", ["run", "-d", "--name", container, "-e", "POSTGRES_USER=smartrail", "-e", "POSTGRES_PASSWORD=e2e-scratch", "-e", "POSTGRES_DB=smartrail_e2e", "-p", `${ports.postgres}:5432`, "postgres:17"]);
  await waitFor("PostgreSQL", async () => {
    run("docker", ["exec", container, "pg_isready", "-U", "smartrail", "-d", "smartrail_e2e"]);
    return true;
  }, 60);
  await new Promise((r) => setTimeout(r, 2000)); // pg_isready answers a moment before the init restart is over

  // 3. the API on the demo clock, then RailBites on the same clock (joined from the API's own clock)
  const api = start("api", "dotnet", [dll("SmartRail.Api", "SmartRail.Api")], join(repo, "backend", "src", "SmartRail.Api"), apiEnv({ cabSecrets, demoPassword, jwtKey, catering }, clock.start));
  const apiUrl = `http://localhost:${ports.api}`;
  await waitFor("the API", answers(`${apiUrl}/api/v1/lines`), 180);

  const railBites = startRailBites(catering, await apiClockNow(apiUrl));
  const railBitesUrl = `http://localhost:${ports.railBites}`;
  await waitFor("RailBites", answers(`${railBitesUrl}/events`), 120);

  // 4. the two web apps, pointed at this API
  const web = join(repo, "web");
  const ops = start("ops-web", "npm", ["run", "dev", "-w", "@smartrail/ops-web", "--", "--port", String(ports.opsWeb), "--strictPort"], web, { BACKEND_URL: apiUrl });
  const display = start("display", "npm", ["run", "dev", "-w", "@smartrail/station-display", "--", "--port", String(ports.display), "--strictPort"], web, { BACKEND_URL: apiUrl });
  await waitFor("the ops web", answers(`http://localhost:${ports.opsWeb}/`), 90);
  await waitFor("the station display", answers(`http://localhost:${ports.display}/`), 90);

  const stack: Stack = {
    api: apiUrl,
    railBites: railBitesUrl,
    opsWeb: `http://localhost:${ports.opsWeb}`,
    display: `http://localhost:${ports.display}`,
    demoPassword,
    cabSecrets,
    pids: { api: api.pid!, railBites: railBites.pid!, ops: ops.pid!, display: display.pid! },
    clockSpeed: clock.speed,
  };
  mkdirSync(dirname(stateFile), { recursive: true });
  writeFileSync(stateFile, JSON.stringify({ ...stack, catering }, null, 2));
  return stack;
}

export const simsFile = join(here, "..", ".state", "sims.json");

export function stopStack() {
  try {
    const state = JSON.parse(readFileSync(stateFile, "utf-8")) as { pids: Record<string, number> };
    for (const pid of Object.values(state.pids)) killTree(pid);
  } catch {
    // never started
  }
  try {
    for (const pid of JSON.parse(readFileSync(simsFile, "utf-8")) as number[]) killTree(pid); // simulators a flow left running
    writeFileSync(simsFile, "[]");
  } catch {
    // none
  }
  try {
    run("docker", ["rm", "-f", container]);
  } catch {
    // none
  }
}
