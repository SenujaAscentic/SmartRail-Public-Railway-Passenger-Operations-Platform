# Mobile notes (passenger app first)

Design decisions are in `docs/develop/04-technology-decisions.md` ADR-12. This file holds what is still open.

## Open points

1. **Ops-web and the backend do not run `shared/status-fixtures.json` yet** (no reference to the file in `web/` or `backend/`), so S-05 to S-05c and S-21 to S-28 are only checked by the Flutter tests. Senuja will fix it in the ops-web phase.
2. **`GET /journeys/{id}` is missing from `backend/openapi.yaml`** (asked B). Step 3 (train details) builds against the contract shape and the fake backend until it lands; then the field check against `openapi.yaml` is added for the detail models, like the one for the search models.
3. **Train position between stations:** comes from `TrainPositionUpdated` (B's Phase 2b); B is asked to also put the last position in `GET /journeys/{id}`. `LineDiagram` takes the position from its caller and never computes it.
4. **OpenAPI marks scheduled times as nullable.** The app treats a null scheduled time as an error (the list fails to load), because a card without its time shows nothing useful. An empty `platform` string means "no platform".
5. **Date picker range:** today to 60 days ahead (the picker needs an end date; the stories give none).
6. **Real backend tried on 18 Oct (emulator, 10.0.2.2:5045):** type-ahead, date picker and search work; the debug-only cleartext config works. Two bugs found and fixed in the same session: the highlighted type-ahead option was unreadable (the theme's `focusColor` was the opaque token colour) and a wide badge such as "Not started" made the card's times wrap onto two lines.
7. **The server clock runs from `Clock__Start`**, so a train that has already left is correctly hidden from today's search ("past departures hidden", US-P1 AC4). On 20 Oct at 06:33 the search Colombo Fort to Kandy gave the empty state because 1001 (06:30) had gone. The `Date` header of every response carries the server's (simulated) time, which step 4 uses for the age label.
