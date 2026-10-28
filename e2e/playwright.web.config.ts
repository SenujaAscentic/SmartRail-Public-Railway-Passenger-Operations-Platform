import { defineConfig } from "@playwright/test";
import { join } from "node:path";

// The web flows of stage 1 (tests-web/). No global stack: every file starts its own (support/own-stack.ts) on its own demo-clock start,
// one file after the other, because they all use the same ports. Run one alone with:  npx playwright test -c playwright.web.config.ts <file>
export default defineConfig({
  testDir: "tests-web",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 240_000,
  expect: { timeout: 15_000 },
  reporter: [["list"]],
  outputDir: join("..", "docs", "qa", "evidence", "traces-web"),
  use: {
    trace: "on",
    screenshot: "on",
    viewport: { width: 1360, height: 900 },
  },
});
