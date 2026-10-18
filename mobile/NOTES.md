# Mobile notes (passenger app first)

Design decisions are in `docs/develop/04-technology-decisions.md` ADR-12. This file holds what is still open.

## Open points

1. **Ops-web and the backend do not run `shared/status-fixtures.json` yet** (no reference to the file in `web/` or `backend/`), so S-05 to S-05c and S-21 to S-28 are only checked by the Flutter tests. Senuja will fix it in the ops-web phase.
2. **`GET /journeys/{id}` is missing from `backend/openapi.yaml`** (asked B). Step 3 (train details) builds against the contract shape and the fake backend until it lands; then the field check against `openapi.yaml` is added for the detail models, like the one for the search models.
3. **Train position between stations:** comes from `TrainPositionUpdated` (B's Phase 2b); B is asked to also put the last position in `GET /journeys/{id}`. `LineDiagram` takes the position from its caller and never computes it.
4. **OpenAPI marks scheduled times as nullable.** The app treats a null scheduled time as an error (the list fails to load), because a card without its time shows nothing useful. An empty `platform` string means "no platform".
5. **Date picker range:** today to 60 days ahead (the picker needs an end date; the stories give none).
6. **Real backend not tried yet:** it was not running on 18 Oct. The app was run on the Pixel 10 emulator (shows the search form and today's date); the search itself has so far only been tested against the fake backend.
