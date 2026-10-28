# SmartRail end-to-end flows (Playwright)

`npm install`, `npm run install-browser` once; then `npm run e2e`. Needs Docker, the .NET SDK and Node (the same as the demo).

The run starts its **own** stack and removes it at the end (`support/stack.ts`): a scratch PostgreSQL container `sr-e2e-pg` on port 55433, the API on 5099, RailBites (shown as RailCantima) on 5199, the ops web on 5273 and the station display on 5274 (never the demo's 5045, 5100, 5173, 5174). The demo clock starts on Wednesday 21 Oct 2026 at 06:20 and runs 5 times faster than real time; every time a flow measures is real wall-clock time. Secrets are made up per run and reach the processes through their environment only.

Playwright drives the ops web and the displays and checks API state and events; the passenger app side is covered by the Flutter fixture tests (strategy K-2, option C). The simulator acts as the cab tablet. Each flow writes what it measured to `docs/qa/evidence/<flow>.json`; traces and screenshots go to `docs/qa/evidence/`. The flows and the measured times are in `docs/qa/strategy.md` section 5.

## Web flows W1 to W3 (stage 1, `tests-web/`)

Three happy-path flows for the ops web and the station displays, written to the project skill `.claude/skills/e2e-playwright/SKILL.md`:

| File | Flow | Demo clock start |
|---|---|---|
| `w1-delay-to-boards.spec.ts` | the controller delays 1002 by 8 minutes with a reason; the board and the platform display say "8 min late" | 09:30 |
| `w2-platform-change-then-hold.spec.ts` | a platform change under 5 minutes' notice, then a hold; "will wait" appears only with the hold | 06:20 |
| `w3-cancel-then-reinstate.spec.ts` | the controller cancels 2002 and reinstates it; the board follows; the audit log has both rows | 09:50 |

Each file starts its **own** stack on its **own** demo-clock start (`support/own-stack.ts`) and removes it at the end, so each runs alone and none depends on another. They use `playwright.web.config.ts`, not the global stack of the five original flows:

- one flow alone: `npx playwright test -c playwright.web.config.ts tests-web/w2`
- the three web flows: `npm run e2e:web`
- the five original flows, then the three web flows: `npm run e2e:all`

Latencies (click to text visible) go to `docs/qa/evidence/w1.json` to `w3.json`.
