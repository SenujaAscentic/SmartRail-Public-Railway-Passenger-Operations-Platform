import { useEffect, useState, type FormEvent } from "react";
import type { components } from "@smartrail/api-client";
import { Icon } from "../../components/Icon.tsx";
import { formatClock } from "../../format.ts";
import { platformInfoText, platformText } from "../../status/status-text.ts";
import { Field, aheadStops, type FormContext } from "./ActionForms.tsx";

type Preview = components["schemas"]["PlatformPreviewResponse"];
type Platform = components["schemas"]["PlatformResponse"];

// LeadTimeNotice (design): the words for each lead time level (US-O5, R-11). The level itself is the backend's: the page never works
// out the lead time. Late is a warning with a hold option, never a block (decided 18 Oct).
const LEVELS: Record<string, { title: string; text: string; warning: boolean }> = {
  Normal: { title: "Normal", text: "more than 15 min: a normal update everywhere.", warning: false },
  ShortNotice: { title: "Short notice", text: "5 to 15 min: station staff are also alerted and the announcement is repeated.", warning: false },
  Late: { title: "Late", text: "under 5 min: passengers may not reach the new platform before the train arrives.", warning: true },
};

// PlatformChangeDialog (US-O5, the most safety-critical screen). Choose the stop and the new platform, see the lead time, the
// conflicts and what people will see, then confirm. A late notice is a warning, never a block; only a REAL hold lets the screens say
// that the train will wait (A-113). The dialog frame (title, scrim) is the page's Dialog; this is its content.
export function PlatformDialog({ api, detail, action, close, station: initialStation }: FormContext & { station?: string }) {
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

  // All the platforms of the chosen station: the current one and the ones out of service are shown but cannot be chosen.
  useEffect(() => {
    if (!station) return;
    let cancelled = false;
    setPlatform("");
    setPlatforms([]);
    api
      .GET("/api/v1/stations/{stationId}", { params: { path: { stationId: station } } })
      .then(({ data }) => {
        if (!cancelled && data) setPlatforms(data.platforms);
      })
      .catch(() => {
        if (!cancelled) setPreviewError("The platforms of this station could not be loaded.");
      });
    return () => {
      cancelled = true;
    };
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
      <Field id="platform-station" label="Stop">
        <select id="platform-station" value={station} onChange={(e) => setStation(e.target.value)}>
          {stops.map((s) => (
            <option key={s.stationId} value={s.stationId}>{s.name} · {platformText(s.platform, s.previousPlatform)}</option>
          ))}
        </select>
      </Field>

      <div className="platform-choice">
        <span className="current">
          <span className="label">Current</span>
          <strong>{stop?.platform ?? "—"}</strong>
        </span>
        <Icon name="arrows-exchange" />
        <div role="group" aria-label="New platform" className="platform-buttons">
          {platforms.map((p) => {
            const isCurrent = p.number === stop?.platform;
            const unusable = isCurrent || !p.inService;
            return (
              <button
                key={p.number}
                type="button"
                className={p.number === platform ? "chosen" : "secondary"}
                aria-pressed={p.number === platform}
                disabled={unusable}
                aria-label={`Platform ${p.number}${isCurrent ? ", current" : !p.inService ? ", out of service" : ""}`}
                onClick={() => setPlatform(p.number)}
              >
                {p.number}
              </button>
            );
          })}
        </div>
      </div>

      {previewError && <p className="banner banner-warning" role="alert"><Icon name="warning" /><span>{previewError}</span></p>}

      {preview && level && (
        <section aria-label="Lead time">
          <p className={`lead lead-${preview.leadTimeLevel.toLowerCase()}`}>
            <Icon name={level.warning ? "warning" : "clock"} />
            <span>
              <strong>{level.title}</strong> · {level.text} Lead time = expected arrival − now = <strong>{preview.leadTimeMinutes} min</strong>.
            </span>
          </p>

          <div className="field checkbox hold-choice">
            <input id="platform-hold" type="checkbox" checked={setHold} onChange={(e) => setSetHold(e.target.checked)} />
            <label htmlFor="platform-hold">Set a hold at the new platform{preview.holdRecommended ? " (recommended)" : ""}</label>
          </div>
          {setHold && (
            <Field id="platform-hold-minutes" label="Planned length of the hold in minutes" error={action.problem?.fields.holdMinutes}>
              <input id="platform-hold-minutes" type="number" min={1} max={240} value={holdMinutes} onChange={(e) => setHoldMinutes(e.target.value)} />
            </Field>
          )}

          {conflicts.length > 0 && (
            <div role="alert" className="conflict-banner">
              {conflicts.map((c) => (
                <p key={c.journeyId} className="conflict-text">
                  <Icon name="warning" />
                  <strong>Platform {c.platform} is expected to be occupied by {c.serviceNumber} at {formatClock(c.expectedArrival ?? c.expectedDeparture)}.</strong>
                </p>
              ))}
              <div className="field checkbox">
                <input id="platform-ack" type="checkbox" checked={acknowledged} onChange={(e) => setAcknowledged(e.target.checked)} />
                <label htmlFor="platform-ack">I have seen the conflict and want to go on</label>
              </div>
            </div>
          )}

          <div className="preview" aria-label="What people will see">
            <p><strong>Preview</strong></p>
            <div className="preview-cards">
              <div className="preview-card">
                <p className="label"><Icon name="phone" /> Passenger app</p>
                <p><strong>{platformText(platform, stop?.platform)}</strong></p>
              </div>
              <div className="preview-card">
                <p className="label"><Icon name="monitor" /> {stop?.name ?? "Station"} displays</p>
                <p><strong>{platformInfoText(setHold, platform)}</strong></p>
              </div>
            </div>
          </div>
        </section>
      )}

      <p className="buttons">
        <button type="button" className="secondary" onClick={close}>Cancel</button>
        <button type="submit" disabled={action.busy || !canConfirm}>{action.busy ? "Working…" : setHold ? "Confirm change and hold" : "Confirm platform change"}</button>
      </p>
    </form>
  );
}
