# Passenger app end-to-end flows (Maestro, Android emulator)

Written to the project skill `.claude/skills/e2e-maestro/SKILL.md`. Stage 1: one happy-path flow, in slices:

| Slice | File | What it checks |
|---|---|---|
| M1 | `flows/m1-search-and-track.yaml` | Search Gampaha to Kandy, 1001 is listed (time, platform, status), open it, Track this train: line diagram, platform, "Updated N s ago" |
| M2 | `flows/m2-controller-delay.yaml` | The controller delays 1001 by 5 min through the API (`scripts/*.js`, `runScript`): the app shows "5 min late" and the reason |

`flows/m1-m2.yaml` runs M1 then M2. (M3 and M4 are added below when they are green.)

## Install (once)

- Maestro CLI 2.11.0 (official `mobile-dev-inc/maestro` release `cli-2.11.0`, 29 Sep 2026): download `maestro.zip`, check it against `checksums_sha256.txt` of the release, extract to `C:\maestro`, add `C:\maestro\bin` to the user Path, restart the terminal. Java 17 or higher (`java -version`). Runs natively on Windows, no WSL.
- Android emulator `Pixel_10`. On this PC the emulator must be started with software rendering, otherwise the Flutter app freezes it (system ANR): `emulator -avd Pixel_10 -gpu swiftshader_indirect`.
- Docker (the stack's PostgreSQL), the .NET SDK, Node 24 and Flutter.

## Every run

Open three terminals in `e2e-mobile/`.

1. **Stack** (leave it open; Ctrl+C stops it): `node support/stack.mjs start 06:20 5`
   Starts a scratch PostgreSQL (`sr-e2e-mobile-pg`, port 55434) and the API on **5098**, with a demo clock that starts **today** at 06:20 and runs 5 times faster than real time. Today, because the app searches by the emulator's date. It has its own ports and container (never 5045, never the Playwright stack of `e2e/` on 5099 and 55433, which would delete each other's database).
2. **Simulator** (the cab tablet, DRV-01 on CAB-01), once the API is up: `node support/simulator.mjs --delay 5`
   `--delay 5` runs the train 5 minutes late, so its real movement matches the controller delay of M2.
3. **The app** (once per stack port): from `mobile/apps/passenger`:
   `flutter build apk --debug --dart-define=API_BASE_URL=http://10.0.2.2:5098` then `adb install -r build/app/outputs/flutter-apk/app-debug.apk`.
   `10.0.2.2` is how the emulator reaches the PC's localhost.
4. **Run**: `node run.mjs flows/m1-m2.yaml`. It waits until the API says 1001 is live, then runs Maestro with the stack's passwords (read from `.state/stack.json`, git-ignored) and copies the screenshots.

Stop everything: Ctrl+C in the stack terminal, or `node support/stack.mjs stop`. A flow needs a **fresh** stack (the demo clock only moves forward).

## Where things are

- Screenshots: `docs/qa/evidence/mobile/` (Maestro 2.11 saves them in `.output/`; `run.mjs` copies them). The measured time of M2 (controller call to "5 min late" visible, in ms, including Maestro's own polling of about a second) is in the file name of `m2-02-5-min-late-after-<ms>ms.png`.
- Logs of the stack and the simulator: `.state/logs/`. The last Maestro output: `.state/logs/last-run.txt`.

## Things to know

- The Impeller flag: the debug app renders correctly on the software-rendered emulator without `--no-enable-impeller`; the freeze above came from the emulator's GPU, not from the flag. Maestro's `launchApp` `arguments` could pass the flag if it is ever needed.
- The two suggestion lists (From, To) are tapped by screen position: the keyboard's suggestion strip repeats the station name, so the text is ambiguous.
- Status cards are single accessibility elements (for example "Expected at Gampaha / 07:10 / On time / Platform / 1"), so the flows match them with `(?s).*...*` patterns.
- No Semantics labels were added to the app.
