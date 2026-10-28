---
name: e2e-maestro
description: How to write and run SmartRail passenger-app end-to-end tests with Maestro on the Android emulator. Use when creating, changing or debugging anything in e2e-mobile/.
---

## Rules
- Flows live in e2e-mobile/ as YAML; appId lk.smartrail.smartrail_passenger.
- Run against the e2e test stack (never port 5045). Build the app with
  --dart-define=API_BASE_URL=http://10.0.2.2:<test API port>; run Flutter with --no-enable-impeller.
- Find elements by visible text (wording from shared/status-fixtures.json). Add a Semantics label in the app only
  where Maestro cannot find an element, and list each one in the report.
- Controller actions happen through the test API with runScript (JavaScript HTTP calls), never by hand.
- Wait with extendedWaitUntil (visible text + timeout); no fixed waits.
- takeScreenshot after each step into docs/qa/evidence/mobile/.
- Build in slices; each slice green before the next. Happy path first.
- Report: Maestro version, runtime, screenshots, any Semantics labels added.
