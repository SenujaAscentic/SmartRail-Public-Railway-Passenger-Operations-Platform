---
name: e2e-playwright
description: How to write and run SmartRail web end-to-end tests with Playwright (ops web and station displays). Use when creating, changing or debugging anything in e2e/.
---

## Rules
- Tests live in e2e/; reuse the global setup and helpers. Never touch port 5045 or the demo database.
- Each flow runs on its own stack (ports 55433, 5099, 5199, 5273, 5274) and must also run ALONE: set its own
  demo-clock start; no dependence on another file.
- Write the scenario as Given/When/Then first; every assertion names its source (rule R-xx, story, fixture S-xx).
- Selectors: getByRole / getByText / getByLabel; data-testid only when no accessible name exists.
- Expected wording comes from shared/status-fixtures.json – never invent text.
- Never use fixed sleeps; wait with expect(...).toBeVisible/toHaveText and a timeout.
- Measure user-visible latency (action → text visible) and write it to docs/qa/evidence/<flow>.json.
- One plain-English comment line above each step.
- Happy path first; negative checks only when asked.
- New npm packages: ask first (official, exact version at least one week old).
- After a change: run the flow alone, then the whole suite; report runtime and result.
