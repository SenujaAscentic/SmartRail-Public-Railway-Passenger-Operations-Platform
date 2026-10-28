import { test } from "@playwright/test";
import { execFileSync } from "node:child_process";
import { killTree, ports, startStack, stopStack } from "./stack.ts";

// A run that ended badly can leave its API or web servers listening on the e2e ports (once seen: "address already in use", then a flow that
// could not sign in). Before a flow starts its stack, whatever listens on the e2e ports is stopped. Only these ports: never the demo's.
function freeE2ePorts() {
  if (process.platform !== "win32") return;
  const listening = execFileSync("netstat", ["-ano", "-p", "TCP"], { encoding: "utf-8" }).split("\n");
  // not PostgreSQL: its port belongs to the Docker container (removed by stopStack), never to a process of ours
  const wanted = [ports.api, ports.railBites, ports.opsWeb, ports.display].map((p) => `:${p}`);
  const pids = new Set<number>();
  for (const line of listening) {
    const columns = line.trim().split(/\s+/);
    if (columns[0] === "TCP" && columns[3] === "LISTENING" && wanted.some((p) => columns[1]!.endsWith(p))) pids.add(Number(columns[4]));
  }
  for (const pid of pids) killTree(pid);
}

// For the web flows in tests-web/: each file starts its own stack on its own demo-clock start and removes it at the end, so a flow runs alone
// and never depends on what another flow did (the five original flows share one stack instead, see playwright.config.ts).
// The returned function says how long the stack took to come up (real milliseconds); it is part of the flow's runtime.
export function useOwnStack(clockStart: string, clockSpeed?: number): () => number {
  let startupMs = 0;
  test.beforeAll(async () => {
    test.setTimeout(300_000);
    stopStack(); // leftovers of a run that was killed
    freeE2ePorts();
    const began = Date.now();
    await startStack(clockStart, clockSpeed);
    startupMs = Date.now() - began;
  });
  test.afterAll(() => stopStack());
  return () => startupMs;
}
