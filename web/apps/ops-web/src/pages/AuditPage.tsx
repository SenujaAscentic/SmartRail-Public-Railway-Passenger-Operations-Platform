import { useState, type FormEvent } from "react";
import type { components } from "@smartrail/api-client";
import { Message } from "../components/Message.tsx";
import { formatColombo } from "../format.ts";
import { toProblem } from "../journeys/useAction.ts";
import { useServices } from "../services-context.tsx";
import { useProfile } from "../session/useProfile.ts";

type Entry = components["schemas"]["AuditEntryResponse"];

// The audit trail (US-O10, O-10): who did what, when, read-only, newest first. Filters: journey, staff number, time. Controller only.
// Times typed here are Sri Lanka time (the same clock as every other screen) and are sent as UTC.
export function AuditPage() {
  const { api } = useServices();
  const profile = useProfile();
  const [journeyId, setJourneyId] = useState("");
  const [staff, setStaff] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [entries, setEntries] = useState<Entry[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [busy, setBusy] = useState(false);

  if (profile && profile.role !== "Controller") {
    return <><h1>Audit log</h1><Message kind="notice">Only the controller can read the audit log.</Message></>;
  }

  const search = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setFieldErrors({});
    try {
      const { data, error: problem, response } = await api.GET("/api/v1/ops/audit", {
        params: {
          query: {
            ...(journeyId.trim() ? { journeyId: journeyId.trim() } : {}),
            ...(staff.trim() ? { staff: staff.trim() } : {}),
            ...(from ? { from: toUtc(from) } : {}),
            ...(to ? { to: toUtc(to) } : {}),
          },
        },
      });
      if (data) {
        setEntries(data);
      } else {
        const found = toProblem(problem, response.status);
        setFieldErrors(found.fields);
        setError(Object.keys(found.fields).length > 0 ? "Please correct the filters." : found.text);
      }
    } catch {
      setError("The audit log could not be loaded.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <h1>Audit log</h1>
      <form className="filters" onSubmit={(event) => void search(event)} aria-label="Audit filters">
        <Filter id="audit-journey" label="Journey id" value={journeyId} onChange={setJourneyId} errors={fieldErrors.journeyId} />
        <Filter id="audit-staff" label="Staff number" value={staff} onChange={setStaff} errors={fieldErrors.staff} />
        <Filter id="audit-from" label="From (Sri Lanka time)" type="datetime-local" value={from} onChange={setFrom} errors={fieldErrors.from} />
        <Filter id="audit-to" label="To (Sri Lanka time)" type="datetime-local" value={to} onChange={setTo} errors={fieldErrors.to} />
        <div className="field"><button type="submit" disabled={busy}>{busy ? "Searching…" : "Show"}</button></div>
      </form>
      {error && <Message kind="error">{error}</Message>}
      {entries && (entries.length === 0 ? <p>Nothing recorded for these filters.</p> : (
        <table>
          <caption>Newest first</caption>
          <thead>
            <tr>
              <th scope="col">When</th>
              <th scope="col">Who</th>
              <th scope="col">Action</th>
              <th scope="col">Target</th>
              <th scope="col">Reason</th>
              <th scope="col">What</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => (
              <tr key={entry.id}>
                <td>{formatColombo(entry.at)}</td>
                <td>{entry.actor ? `${entry.actor.name} (${entry.actor.staffNumber})` : "unknown"}</td>
                <td>{entry.action}</td>
                <td>{entry.targetType} {entry.targetId.slice(0, 8)}…</td>
                <td>{entry.reason ?? "—"}</td>
                <td>{entry.summary}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ))}
    </>
  );
}

function Filter(props: { id: string; label: string; value: string; onChange: (v: string) => void; type?: string; errors: string[] | undefined }) {
  return (
    <div className="field">
      <label htmlFor={props.id}>{props.label}</label>
      <input id={props.id} type={props.type ?? "text"} value={props.value} onChange={(e) => props.onChange(e.target.value)} aria-invalid={Boolean(props.errors)} />
      {props.errors && <p className="field-text-error">{props.errors.join(" ")}</p>}
    </div>
  );
}

// "2026-10-14T10:00" typed in Sri Lanka time (UTC+05:30, no daylight saving) -> UTC for the API.
const toUtc = (local: string): string => new Date(`${local}:00+05:30`).toISOString();
