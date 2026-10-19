import { useState, type FormEvent, type ReactNode } from "react";
import type { ApiClient } from "@smartrail/api-client";
import { ConfirmImpact } from "../../components/ConfirmImpact.tsx";
import { Message } from "../../components/Message.tsx";
import type { Detail } from "../../journeys/journey-store.ts";
import type { ActionState } from "../../journeys/useAction.ts";
import { statusView, stopText } from "../../status/status-text.ts";

// The controller's decisions as small forms (US-O4). Each form only collects what the person decides and sends it with the journey's
// version (R-18); what happens next is the backend's rule. Cancel, terminate and skip are shown in a ConfirmImpactDialog that lists
// what they will affect before they go.
export interface FormContext {
  api: ApiClient;
  detail: Detail;
  action: ActionState;
  close: () => void;
}

// The reason codes the backend accepts (agreed 18 Oct). The words are for the person; the code is what is sent.
export const REASONS: [code: string, label: string][] = [
  ["SignalFailure", "Signal failure"],
  ["TrackWork", "Track work"],
  ["TrainFault", "Train fault"],
  ["Weather", "Weather"],
  ["CrewShortage", "Crew shortage"],
  ["Congestion", "Congestion"],
  ["Other", "Other"],
];

export const MAX_NOTE = 300;

export function Field({ id, label, error, children }: { id: string; label: string; error?: string[] | undefined; children: ReactNode }) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      {children}
      {error && error.length > 0 && <p className="field-text-error" id={`${id}-error`}>{error.join(" ")}</p>}
    </div>
  );
}

function ReasonFields({ idPrefix, code, setCode, note, setNote, errors, optional }: {
  idPrefix: string; code: string; setCode: (v: string) => void; note: string; setNote: (v: string) => void;
  errors: Record<string, string[]>; optional?: boolean;
}) {
  return (
    <>
      <Field id={`${idPrefix}-reason`} label={optional ? "Reason code (optional)" : "Reason code"} error={errors.reasonCode}>
        <select id={`${idPrefix}-reason`} value={code} onChange={(e) => setCode(e.target.value)} aria-invalid={Boolean(errors.reasonCode)}>
          <option value="">{optional ? "No reason" : "Choose a reason"}</option>
          {REASONS.map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>
      </Field>
      <Field id={`${idPrefix}-note`} label={`Note (optional, plain text, at most ${MAX_NOTE} characters)`} error={errors.note}>
        <input id={`${idPrefix}-note`} value={note} maxLength={MAX_NOTE} onChange={(e) => setNote(e.target.value)} />
      </Field>
    </>
  );
}

// The stops that are still ahead of the train, as the backend says (Scheduled, or Arrived: the train is standing there).
export const aheadStops = (detail: Detail) => detail.stops.filter((s) => s.status === "Scheduled" || s.status === "Arrived");

// The stops the controller may skip: still Scheduled, a real stop (not a pass), neither the origin nor the terminus (R-15).
export const skippableStops = (detail: Detail) =>
  detail.stops.filter((s, index) => s.status === "Scheduled" && s.callType === "Stop" && index > 0 && index < detail.stops.length - 1);

function Buttons({ busy, label, close, closeLabel = "Close", danger = false, disabled = false }: { busy: boolean; label: string; close: () => void; closeLabel?: string; danger?: boolean; disabled?: boolean }) {
  return (
    <p className="buttons">
      <button type="submit" disabled={busy || disabled} className={danger ? "danger" : undefined}>{busy ? "Working…" : label}</button>
      <button type="button" className="secondary" onClick={close}>{closeLabel}</button>
    </p>
  );
}

const noteOrNull = (note: string) => (note.trim() === "" ? null : note.trim());

export function DelayForm({ api, detail, action, close }: FormContext) {
  const [minutes, setMinutes] = useState("");
  const [code, setCode] = useState("");
  const [note, setNote] = useState("");
  const errors = action.problem?.fields ?? {};

  const submit = (event: FormEvent) => {
    event.preventDefault();
    void action.run(() =>
      api.POST("/api/v1/ops/journeys/{journeyId}/delay", {
        params: { path: { journeyId: detail.journeyId } },
        body: { minutes: minutes === "" ? null : Number(minutes), reasonCode: code === "" ? null : code, note: noteOrNull(note), expectedVersion: detail.version },
      }),
    );
  };

  return (
    <form className="form action-form inline-form" onSubmit={submit} aria-label="Set a delay">
      <Field id="delay-minutes" label="Delay in minutes" error={errors.minutes}>
        <input id="delay-minutes" type="number" min={0} max={1440} step={1} value={minutes} onChange={(e) => setMinutes(e.target.value)} aria-invalid={Boolean(errors.minutes)} />
      </Field>
      <ReasonFields idPrefix="delay" code={code} setCode={setCode} note={note} setNote={setNote} errors={errors} />
      <Buttons busy={action.busy} label="Set delay" close={close} />
      <p className="hint">The delay stays until you clear it; crew and GPS times do not replace it.</p>
    </form>
  );
}

export function HoldForm({ api, detail, action, close }: FormContext) {
  const options = aheadStops(detail).filter((s) => s.scheduledDeparture !== null);
  const [station, setStation] = useState(options[0]?.stationId ?? "");
  const [minutes, setMinutes] = useState("");
  const [code, setCode] = useState("");
  const [note, setNote] = useState("");
  const errors = action.problem?.fields ?? {};

  const submit = (event: FormEvent) => {
    event.preventDefault();
    void action.run(() =>
      api.POST("/api/v1/ops/journeys/{journeyId}/hold", {
        params: { path: { journeyId: detail.journeyId } },
        body: { stationId: station, minutes: minutes === "" ? null : Number(minutes), reasonCode: code === "" ? null : code, note: noteOrNull(note), expectedVersion: detail.version },
      }),
    );
  };

  return (
    <form className="form action-form inline-form" onSubmit={submit} aria-label="Hold the train">
      <Field id="hold-station" label="Station" error={errors.stationId}>
        <select id="hold-station" value={station} onChange={(e) => setStation(e.target.value)}>
          {options.map((s) => <option key={s.stationId} value={s.stationId}>{s.name}</option>)}
        </select>
      </Field>
      <Field id="hold-minutes" label="Planned length in minutes" error={errors.minutes}>
        <input id="hold-minutes" type="number" min={1} max={240} step={1} value={minutes} onChange={(e) => setMinutes(e.target.value)} aria-invalid={Boolean(errors.minutes)} />
      </Field>
      <ReasonFields idPrefix="hold" code={code} setCode={setCode} note={note} setNote={setNote} errors={errors} optional />
      <Buttons busy={action.busy} label="Hold train" close={close} />
    </form>
  );
}

export function CancelForm({ api, detail, action, close }: FormContext) {
  const [code, setCode] = useState("");
  const [note, setNote] = useState("");
  const errors = action.problem?.fields ?? {};

  const submit = (event: FormEvent) => {
    event.preventDefault();
    void action.run(() =>
      api.POST("/api/v1/ops/journeys/{journeyId}/cancel", {
        params: { path: { journeyId: detail.journeyId } },
        body: { reasonCode: code === "" ? null : code, note: noteOrNull(note), expectedVersion: detail.version },
      }),
    );
  };

  return (
    <form className="form action-form" onSubmit={submit} aria-label="Cancel the journey">
      <ReasonFields idPrefix="cancel" code={code} setCode={setCode} note={note} setNote={setNote} errors={errors} />
      <ConfirmImpact stations={aheadStops(detail).map((s) => s.name)} badge={statusView({ kind: "Cancelled", delayMinutes: null })} displayText="Cancelled" />
      <Buttons busy={action.busy} label="Cancel journey" close={close} closeLabel="Back" danger />
    </form>
  );
}

export function TerminateForm({ api, detail, action, close }: FormContext) {
  const options = aheadStops(detail);
  const [station, setStation] = useState(options[0]?.stationId ?? "");
  const [code, setCode] = useState("");
  const [note, setNote] = useState("");
  const errors = action.problem?.fields ?? {};
  const chosen = options.findIndex((s) => s.stationId === station);
  const skipped = chosen < 0 ? [] : options.slice(chosen + 1).map((s) => s.name);
  const name = options[chosen]?.name ?? "";

  const submit = (event: FormEvent) => {
    event.preventDefault();
    void action.run(() =>
      api.POST("/api/v1/ops/journeys/{journeyId}/terminate", {
        params: { path: { journeyId: detail.journeyId } },
        body: { atStationId: station === "" ? null : station, reasonCode: code === "" ? null : code, note: noteOrNull(note), expectedVersion: detail.version },
      }),
    );
  };

  return (
    <form className="form action-form" onSubmit={submit} aria-label="Terminate the journey early">
      <Field id="terminate-station" label="The journey ends at" error={errors.atStationId ?? errors.stationId}>
        <select id="terminate-station" value={station} onChange={(e) => setStation(e.target.value)}>
          {options.map((s) => <option key={s.stationId} value={s.stationId}>{s.name}</option>)}
        </select>
      </Field>
      <ReasonFields idPrefix="terminate" code={code} setCode={setCode} note={note} setNote={setNote} errors={errors} />
      <ConfirmImpact stations={skipped} badge={statusView({ kind: "TerminatedEarly", delayMinutes: null }, { terminatesAt: name })} displayText={`Terminates at ${name}`} />
      <Buttons busy={action.busy} label="Terminate early" close={close} closeLabel="Back" danger />
    </form>
  );
}

export function SkipForm({ api, detail, action, close }: FormContext) {
  const options = skippableStops(detail);
  const [station, setStation] = useState(options[0]?.stationId ?? "");
  const stop = detail.stops.find((s) => s.stationId === station);
  const text = stopText("Skipped") ?? "";   // S-12: "Does not stop"

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (!station) return;
    void action.run(() =>
      api.POST("/api/v1/ops/journeys/{journeyId}/stops/{stationId}/skip", {
        params: { path: { journeyId: detail.journeyId, stationId: station } },
        body: { expectedVersion: detail.version },
      }),
    );
  };

  return (
    <form className="form action-form" onSubmit={submit} aria-label="Skip the stop">
      <Field id="skip-station" label="Stop to skip">
        <select id="skip-station" value={station} onChange={(e) => setStation(e.target.value)}>
          {options.map((s) => <option key={s.stationId} value={s.stationId}>{s.name}</option>)}
        </select>
      </Field>
      <ConfirmImpact stations={stop ? [stop.name] : []} badge={{ text, icon: "ban", tone: "cancelled" }} displayText={text} />
      <p className="hint">The time the train would have waited there is taken off the delay of the stops after it.</p>
      <Buttons busy={action.busy} disabled={!station} label="Skip stop" close={close} closeLabel="Back" danger />
    </form>
  );
}

export function ProblemMessage({ action }: { action: ActionState }) {
  const { problem } = action;
  if (!problem) return null;
  const fields = Object.values(problem.fields).flat();
  return <Message kind="error">{fields.length > 0 ? fields.join(" ") : problem.text}</Message>;
}
