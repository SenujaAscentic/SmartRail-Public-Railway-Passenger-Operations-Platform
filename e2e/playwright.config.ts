import { defineConfig } from "@playwright/test";
import { join } from "node:path";

// One stack for the whole run (support/stack.ts), flows one after the other: they share one API, one clock and one database, so they
// must not run in parallel. Traces and screenshots of every flow are kept in docs/qa/evidence/ as evidence (strategy section 10).
export default defineConfig({
  testDir: "tests",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 240_000,
  expect: { timeout: 15_000 },
  reporter: [["list"]],
  globalSetup: "./support/global-setup.ts",
  outputDir: join("..", "docs", "qa", "evidence", "traces"),
  use: {
    trace: "on",
    screenshot: "on",
    viewport: { width: 1360, height: 900 },
  },
});
