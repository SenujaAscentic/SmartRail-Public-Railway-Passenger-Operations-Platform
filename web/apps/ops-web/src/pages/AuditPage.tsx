import { useState, type FormEvent } from "react";
import type { components } from "@smartrail/api-client";
import { Icon } from "../components/Icon.tsx";
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
      <div className="page-head">
        <h1>Audit log<span className="h1-note"> · read-only</span></h1>
      </div>
      <form className="filters" onSubmit={(event) => void search(event)} aria-label="Audit filters">
        <Filter id="audit-journey" label="Journey id" value={journeyId} onChange={setJourneyId} errors={fieldErrors.journeyId} />
        <Filter id="audit-staff" label="Staff number" value={staff} onChange={setStaff} errors={fieldErrors.staff} />
        <Filter id="audit-from" label="From (Sri Lanka time)" type="datetime-local" value={from} onChange={setFrom} errors={fieldErrors.from} />
        <Filter id="audit-to" label="To (Sri Lanka time)" type="datetime-local" value={to} onChange={setTo} errors={fieldErrors.to} />
        <div className="field"><button type="submit" disabled={busy} className="filter-button"><Icon name="filter" /><span>{busy ? "Searching…" : "Apply"}</span></button></div>
      </form>
      {error && <Message kind="error">{error}</Message>}
      {entries && (entries.length === 0 ? <p className="empty">Nothing recorded for these filters.</p> : (
        <div className="panel"><div className="panel-body flush">
        <table>
          <caption className="visually-hidden">The audit trail, newest first</caption>
          <thead>
            <tr>
              <th scope="col">When</th>
              <th scope="col">Staff</th>
              <th scope="col">Role</th>
              <th scope="col">Action</th>
              <th scope="col">Journey</th>
              <th scope="col">Reason</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => (
              <tr key={entry.id}>
                <td className="mono">{formatColombo(entry.at)}</td>
                <td>{entry.actor ? `${entry.actor.name} (${entry.actor.staffNumber})` : "unknown"}</td>
                <td>{entry.actor?.role ?? "—"}</td>
                <td>{entry.summary} <span className="source">({entry.action})</span></td>
                <td className="mono">{entry.targetType === "Journey" ? `${entry.targetId.slice(0, 8)}…` : "—"}</td>
                <td>{entry.reason ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
        </div></div>
      ))}
      {entries && entries.length > 0 && <p className="source">Showing {entries.length} · newest first · entries can't be edited or deleted</p>}
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
