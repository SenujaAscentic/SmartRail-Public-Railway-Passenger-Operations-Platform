// Starts the cab simulator for service 1001 (DRV-01 on CAB-01), the same console program the Playwright flows use, against this
// stack. It runs on in the background; its pid goes into .state/stack.json so `stack.mjs stop` ends it.
// Usage: node support/simulator.mjs [extra simulator arguments, e.g. --delay 6]
import { spawn } from "node:child_process";
import { closeSync, openSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { stateFile } from "./stack.mjs";

const repo = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const state = JSON.parse(readFileSync(stateFile, "utf-8"));
const dll = join(repo, "backend", "src", "SmartRail.Simulator", "bin", "Debug", "net10.0", "SmartRail.Simulator.dll");
const log = openSync(join(dirname(stateFile), "logs", "simulator-1001.log"), "w");

const child = spawn(
  "dotnet",
  [dll, "--api", state.api, "--device", "CAB-01", "--driver", "DRV-01", "--service", "1001", "--from", "FOT", "--to", "KDT", "--speed", String(state.clockSpeed), ...process.argv.slice(2)],
  { env: { ...process.env, SMARTRAIL_CAB_SECRET: state.cabSecrets["CAB-01"], SMARTRAIL_DEMO_PASSWORD: state.demoPassword }, stdio: ["ignore", log, log], windowsHide: true, detached: true },
);
closeSync(log);
child.unref();
writeFileSync(stateFile, JSON.stringify({ ...state, pids: [...state.pids, child.pid] }, null, 2));
console.log(`Simulator for 1001 started (pid ${child.pid}); log: e2e-mobile/.state/logs/simulator-1001.log`);
