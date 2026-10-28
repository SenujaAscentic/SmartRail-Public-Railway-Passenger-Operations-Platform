# SmartRail end-to-end flows (Playwright)

`npm install`, `npm run install-browser` once; then `npm run e2e`. Needs Docker, the .NET SDK and Node (the same as the demo).

The run starts its **own** stack and removes it at the end (`support/stack.ts`): a scratch PostgreSQL container `sr-e2e-pg` on port 55433, the API on 5099, RailBites (shown as RailCantima) on 5199, the ops web on 5273 and the station display on 5274 (never the demo's 5045, 5100, 5173, 5174). The demo clock starts on Wednesday 21 Oct 2026 at 06:20 and runs 5 times faster than real time; every time a flow measures is real wall-clock time. Secrets are made up per run and reach the processes through their environment only.

Playwright drives the ops web and the displays and checks API state and events; the passenger app side is covered by the Flutter fixture tests (strategy K-2, option C). The simulator acts as the cab tablet. Each flow writes what it measured to `docs/qa/evidence/<flow>.json`; traces and screenshots go to `docs/qa/evidence/`. The flows and the measured times are in `docs/qa/strategy.md` section 5.
