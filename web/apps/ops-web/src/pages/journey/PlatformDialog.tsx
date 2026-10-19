import { useEffect, useState, type FormEvent } from "react";
import type { components } from "@smartrail/api-client";
import { Message } from "../../components/Message.tsx";
import { formatClock } from "../../format.ts";
import { platformInfoText, platformText } from "../../status/status-text.ts";
import { Field, aheadStops, type FormContext } from "./ActionForms.tsx";

type Preview = components["schemas"]["PlatformPreviewResponse"];
type Platform = components["schemas"]["PlatformResponse"];

// The words for each lead time level (US-O5, R-11). The level itself is the backend's: the page never works out the lead time.
const LEVELS: Record<string, { title: string; text: string; warning: boolean }> = {
  Normal: { title: "Normal notice", text: "More than 15 minutes: a normal update everywhere.", warning: false },
  ShortNotice: { title: "Short notice", text: "5 to 15 minutes: station staff are also alerted and the announcement is repeated.", warning: false },
  Late: { title: "Late notice", text: "Less than 5 minutes: passengers have very little time. You can set a hold so the train waits.", warning: true },
};

// The platform change (US-O5, the most safety-critical screen). Choose the stop and the new platform, see the lead time, the
// conflicts and what passengers will see, then confirm. A late notice is a warning, never a block; only a REAL hold lets the
// screens say that the train will wait (A-113).
export function PlatformDialog({ api, detail, action, close, station: initialStation }: FormContext & { station: string }) {
  const stops = aheadStops(detail);
  const [station, setStation] = useState(initialStation || stops[0]?.stationId || "");
  const [platforms, setPlatforms] = useState<Platform[]>([]);
  const [platform, setPlatform] = useState("");
  const [preview, setPreview] = useState<Preview | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [setHold, setSetHold] = useState(false);
  const [holdMinutes, setHoldMinutes] = useState("5");
  const [acknowledged, setAcknowledged] = useState(false);
  const stop = detail.stops.find((s) => s.stationId === station);

  // The platforms in service at the chosen station.
  useEffect(() => {
    if (!station) return;
    let cancelled = false;
    setPlatform("");
    setPlatforms([]);
    api
      .GET("/api/v1/stations/{stationId}", { params: { path: { stationId: station } } })
      .then(({ data }) => {
        if (!cancelled && data) setPlatforms(data.platforms.filter((p) => p.inService && p.number !== stop?.platform));
      })
      .catch(() => {
        if (!cancelled) setPreviewError("The platforms of this station could not be loaded.");
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [station, api]);

  // The preview changes nothing: it asks the backend what this change would be (lead time, hold recommended, conflicts).
  useEffect(() => {
    setPreview(null);
    setPreviewError(null);
    setAcknowledged(false);
    setSetHold(false);
    if (!station || !platform) return;
    let cancelled = false;
    api
      .POST("/api/v1/ops/journeys/{journeyId}/stops/{stationId}/platform/preview", {
        params: { path: { journeyId: detail.journeyId, stationId: station } },
        body: { platform },
      })
      .then(({ data, response }) => {
        if (cancelled) return;
        if (data) setPreview(data);
        else setPreviewError(response.status === 422 ? "That platform is not in service at this station." : "The preview could not be loaded.");
      })
      .catch(() => {
        if (!cancelled) setPreviewError("The preview could not be loaded.");
      });
    return () => {
      cancelled = true;
    };
  }, [station, platform, detail.journeyId, api]);

  const conflicts = preview?.conflicts ?? [];
  const level = preview ? LEVELS[preview.leadTimeLevel] : undefined;
  const holdMinutesValid = !setHold || (Number(holdMinutes) >= 1 && Number(holdMinutes) <= 240);
  const canConfirm = preview !== null && holdMinutesValid && (conflicts.length === 0 || acknowledged);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (!preview) return;
    void action.run(() =>
      api.POST("/api/v1/ops/journeys/{journeyId}/stops/{stationId}/platform", {
        params: { path: { journeyId: detail.journeyId, stationId: station } },
        body: { platform, setHold, holdMinutes: setHold ? Number(holdMinutes) : null, conflictsAcknowledged: acknowledged, expectedVersion: detail.version },
      }),
    );
  };

  return (
    <form className="form action-form platform-dialog" onSubmit={submit} aria-label="Change platform">
      <h3>Change platform</h3>
      <Field id="platform-station" label="Stop">
        <select id="platform-station" value={station} onChange={(e) => setStation(e.target.value)}>
          {stops.map((s) => (
            <option key={s.stationId} value={s.stationId}>{s.name} · {platformText(s.platform, s.previousPlatform)}</option>
          ))}
        </select>
      </Field>
      <Field id="platform-new" label="New platform">
        <select id="platform-new" value={platform} onChange={(e) => setPlatform(e.target.value)}>
          <option value="">Choose a platform</option>
          {platforms.map((p) => <option key={p.number} value={p.number}>Platform {p.number}</option>)}
        </select>
      </Field>

      {previewError && <Message kind="error">{previewError}</Message>}

      {preview && level && (
        <section aria-label="Lead time">
          <p className={`lead lead-${preview.leadTimeLevel.toLowerCase()}`}>
            <strong>{level.title}</strong>: the train is expected in {preview.leadTimeMinutes} min. {level.text}
          </p>
          {level.warning && <Message kind="error">Warning: less than 5 minutes before the train arrives. Think about holding the train before you confirm.</Message>}

          <div className="field checkbox">
            <input id="platform-hold" type="checkbox" checked={setHold} onChange={(e) => setSetHold(e.target.checked)} />
            <label htmlFor="platform-hold">Hold the train at the new platform{preview.holdRecommended ? " (recommended)" : ""}</label>
          </div>
          {setHold && (
            <Field id="platform-hold-minutes" label="Planned length of the hold in minutes" error={action.problem?.fields.holdMinutes}>
              <input id="platform-hold-minutes" type="number" min={1} max={240} value={holdMinutes} onChange={(e) => setHoldMinutes(e.target.value)} />
            </Field>
          )}

          {conflicts.length > 0 && (
            <div role="alert" className="conflicts">
              {conflicts.map((c) => (
                <Message kind="error" key={c.journeyId}>
                  Platform {c.platform} is expected to be occupied by {c.serviceNumber} at {formatClock(c.expectedArrival ?? c.expectedDeparture)}.
                </Message>
              ))}
              <div className="field checkbox">
                <input id="platform-ack" type="checkbox" checked={acknowledged} onChange={(e) => setAcknowledged(e.target.checked)} />
                <label htmlFor="platform-ack">I have seen the conflict and want to go on</label>
              </div>
            </div>
          )}

          <div className="preview" aria-label="What people will see">
            <p><strong>What people will see</strong></p>
            <ul>
              <li>Passengers and displays: {platformText(platform, stop?.platform)}</li>
              <li>Information screens: {platformInfoText(setHold, platform)}</li>
            </ul>
          </div>
        </section>
      )}

      <p className="buttons">
        <button type="submit" disabled={action.busy || !canConfirm}>{action.busy ? "Working…" : "Confirm platform change"}</button>
        <button type="button" className="secondary" onClick={close}>Close</button>
      </p>
    </form>
  );
}
