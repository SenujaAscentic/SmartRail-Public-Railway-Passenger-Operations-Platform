import { test } from "@playwright/test";
import { startStack, stopStack } from "./stack.ts";

// For the web flows in tests-web/: each file starts its own stack on its own demo-clock start and removes it at the end, so a flow runs alone
// and never depends on what another flow did (the five original flows share one stack instead, see playwright.config.ts).
// The returned function says how long the stack took to come up (real milliseconds); it is part of the flow's runtime.
export function useOwnStack(clockStart: string, clockSpeed?: number): () => number {
  let startupMs = 0;
  test.beforeAll(async () => {
    test.setTimeout(300_000);
    stopStack(); // leftovers of a run that was killed
    const began = Date.now();
    await startStack(clockStart, clockSpeed);
    startupMs = Date.now() - began;
  });
  test.afterAll(() => stopStack());
  return () => startupMs;
}
