import { useCallback, useEffect, useState } from "react";
import { useParams } from "react-router";
import { ConnectionBadge } from "../components/ConnectionBadge.tsx";
import { Message } from "../components/Message.tsx";
import { SourceAge } from "../components/SourceAge.tsx";
import { StatusBadge } from "../components/StatusBadge.tsx";
import { formatClock } from "../format.ts";
import type { Detail, Entry } from "../journeys/journey-store.ts";
import { useAction } from "../journeys/useAction.ts";
import { useJourneyBoard } from "../journeys/useJourneyBoard.ts";
import { useServices } from "../services-context.tsx";
import { useProfile } from "../session/useProfile.ts";
import { freshnessView, platformText, statusView, stopText } from "../status/status-text.ts";
import { useNow } from "../useNow.ts";
import {
  CancelForm, DelayForm, HoldForm, ProblemMessage, SkipForm, TerminateForm, aheadStops,
} from "./journey/ActionForms.tsx";
import { PlatformDialog } from "./journey/PlatformDialog.tsx";

type Open = { kind: "delay" | "hold" | "cancel" | "terminate" } | { kind: "skip" | "platform"; station: string } | null;

// One journey (US-O3) with the controller's decisions (US-O4, US-O5). The values are the backend's; each shows where it came from.
export function JourneyPage() {
  const { journeyId = "" } = useParams();
  const { api } = useServices();
  const board = useJourneyBoard();
  const profile = useProfile();
  const nowMs = useNow();
  const [fetched, setFetched] = useState<Detail | null>(null);
  const [missing, setMissing] = useState(false);

  // A journey the stream does not hold (another day) is read once from the API.
  const held = board.store.journeys[journeyId];
  useEffect(() => {
    if (held) return;
    let cancelled = false;
    api
      .GET("/api/v1/journeys/{journeyId}", { params: { path: { journeyId } } })
      .then(({ data, response }) => {
        if (cancelled) return;
        if (data) setFetched(data);
        else if (response.status === 404) setMissing(true);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [held, api, journeyId]);

  if (missing) return <><h1>Journey</h1><Message kind="error">There is no such journey.</Message></>;
  const entry: Entry | null = held ?? (fetched ? { detail: fetched, delaySource: null, delayAt: null, stopSources: {}, needsRefresh: false } : null);
  if (!entry) return <><h1>Journey</h1><p>Loading…</p></>;

  return (
    <JourneyView
      entry={entry}
      nowMs={nowMs}
      canDecide={profile?.role === "Controller"}
      connection={board.connection}
      applyDetail={(detail) => (held ? board.applyDetail(detail) : setFetched(detail))}
    />
  );
}

function JourneyView({ entry, nowMs, canDecide, connection, applyDetail }: {
  entry: Entry; nowMs: number; canDecide: boolean; connection: Parameters<typeof ConnectionBadge>[0]["connection"]; applyDetail: (detail: Detail) => void;
}) {
  const { api } = useServices();
  const { detail } = entry;
  const [open, setOpen] = useState<Open>(null);
  const action = useAction(applyDetail);
  const closeForm = useCallback(() => {
    setOpen(null);
    action.clear();
  }, [action]);

  // A decision that worked closes its form.
  useEffect(() => {
    if (action.done) setOpen(null);
  }, [action.done]);

  const status = statusView(detail.status, { terminatesAt: detail.terminatesAt?.name ?? null, hold: detail.hold !== null });
  const isOpen = ["Planned", "Running"].includes(runState(detail));
  const ahead = aheadStops(detail);
  const ctx = { api, detail, action, close: closeForm };
  const choose = (next: Open) => {
    action.clear();
    setOpen(next);
  };

  return (
    <>
      <h1>
        {detail.serviceNumber} {detail.name}
      </h1>
      <p>{detail.from.name} → {detail.to.name}{detail.terminatesAt ? ` (ends at ${detail.terminatesAt.name})` : ""}</p>
      <ConnectionBadge connection={connection} />

      <section aria-labelledby="state-h">
        <h2 id="state-h">State</h2>
        <ul className="plain">
          <li>
            <StatusBadge view={status} /> <SourceAge source={entry.delaySource} at={entry.delayAt} nowMs={nowMs} />
          </li>
          <li>
            <StatusBadge view={freshnessView(detail.freshness, nowMs)} /> <SourceAge source="Gps" at={detail.freshness.updatedAt} nowMs={nowMs} />
          </li>
          {detail.hold && (
            <li>
              The train is held at {detail.stops.find((s) => s.stationId === detail.hold?.stationId)?.name ?? "a station"}
              {detail.hold.minutes ? ` for ${detail.hold.minutes} min` : ""}
              {detail.hold.reason ? ` (${detail.hold.reason.code}${detail.hold.reason.note ? `: ${detail.hold.reason.note}` : ""})` : ""}
            </li>
          )}
          {detail.reason && <li>Reason: {detail.reason.code}{detail.reason.note ? `: ${detail.reason.note}` : ""}</li>}
          <li>Version {detail.version}</li>
        </ul>
      </section>

      {action.conflict && (
        <div role="alert">
          <Message kind="error">{action.conflict.text} – reload</Message>
          <button
            type="button"
            onClick={() => {
              if (action.conflict?.journey) applyDetail(action.conflict.journey);
              closeForm();
            }}
          >
            Reload
          </button>
        </div>
      )}
      {action.done && <Message kind="notice">Done. The change is on its way to everyone.</Message>}
      <ProblemMessage action={action} />

      {canDecide ? (
        <section aria-labelledby="actions-h">
          <h2 id="actions-h">Decisions</h2>
          <p className="buttons">
            {isOpen && <button type="button" onClick={() => choose({ kind: "delay" })}>Delay…</button>}
            {isOpen && entry.delaySource === "Controller" && (
              <button type="button" className="secondary" disabled={action.busy} onClick={() => void action.run(() => api.DELETE("/api/v1/ops/journeys/{journeyId}/delay", { params: { path: { journeyId: detail.journeyId } }, body: { expectedVersion: detail.version } }))}>
                Clear the delay
              </button>
            )}
            {isOpen && !detail.hold && <button type="button" onClick={() => choose({ kind: "hold" })}>Hold…</button>}
            {isOpen && detail.hold && (
              <button type="button" className="secondary" disabled={action.busy} onClick={() => void action.run(() => api.POST("/api/v1/ops/journeys/{journeyId}/hold/release", { params: { path: { journeyId: detail.journeyId } }, body: { expectedVersion: detail.version } }))}>
                Release the hold
              </button>
            )}
            {isOpen && <button type="button" onClick={() => choose({ kind: "cancel" })}>Cancel journey…</button>}
            {detail.status.kind === "Cancelled" && (
              <button type="button" disabled={action.busy} onClick={() => void action.run(() => api.POST("/api/v1/ops/journeys/{journeyId}/reinstate", { params: { path: { journeyId: detail.journeyId } }, body: { expectedVersion: detail.version } }))}>
                Reinstate
              </button>
            )}
            {runState(detail) === "Running" && ahead.length > 0 && <button type="button" onClick={() => choose({ kind: "terminate" })}>Terminate early…</button>}
          </p>
          {open?.kind === "delay" && <DelayForm {...ctx} />}
          {open?.kind === "hold" && <HoldForm {...ctx} />}
          {open?.kind === "cancel" && <CancelForm {...ctx} />}
          {open?.kind === "terminate" && <TerminateForm {...ctx} />}
          {open?.kind === "skip" && <SkipForm {...ctx} station={open.station} />}
          {open?.kind === "platform" && <PlatformDialog {...ctx} station={open.station} />}
        </section>
      ) : (
        <Message kind="notice">Only the controller can make decisions on a journey.</Message>
      )}

      <section aria-labelledby="stops-h">
        <h2 id="stops-h">Stops</h2>
        <table>
          <thead>
            <tr>
              <th scope="col">Stop</th>
              <th scope="col">Scheduled</th>
              <th scope="col">Expected</th>
              <th scope="col">Actual</th>
              <th scope="col">Platform</th>
              <th scope="col">State</th>
              {canDecide && isOpen && <th scope="col">Decisions</th>}
            </tr>
          </thead>
          <tbody>
            {detail.stops.map((stop, index) => {
              const sources = entry.stopSources[stop.stationId];
              const first = index === 0;
              const last = index === detail.stops.length - 1;
              const canSkip = stop.status === "Scheduled" && stop.callType === "Stop" && !first && !last;
              const canMove = stop.status === "Scheduled" || stop.status === "Arrived";
              return (
                <tr key={stop.stationId}>
                  <th scope="row">{stop.name}{stop.callType === "Pass" ? " (passes)" : ""}</th>
                  <td>{formatClock(stop.scheduledArrival)} / {formatClock(stop.scheduledDeparture)}</td>
                  <td>{formatClock(stop.expectedArrival)} / {formatClock(stop.expectedDeparture)}</td>
                  <td>
                    {formatClock(stop.actualArrival)} / {formatClock(stop.actualDeparture)}
                    {(stop.actualArrival || stop.actualDeparture) && (
                      <> · <SourceAge source={stop.actualDeparture ? sources?.departure : sources?.arrival} nowMs={nowMs} /></>
                    )}
                  </td>
                  <td>{platformText(stop.platform, stop.previousPlatform)}</td>
                  <td>{stopText(stop.status) ?? stop.status}</td>
                  {canDecide && isOpen && (
                    <td>
                      {canMove && <button type="button" className="secondary" onClick={() => choose({ kind: "platform", station: stop.stationId })}>Platform…<span className="visually-hidden"> at {stop.name}</span></button>}
                      {canSkip && <button type="button" className="secondary" onClick={() => choose({ kind: "skip", station: stop.stationId })}>Skip…<span className="visually-hidden"> {stop.name}</span></button>}
                    </td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      </section>
    </>
  );
}

// The detail has no "state" of its own besides the status kind: Planned = not started and not closed, Running = has data.
function runState(detail: Detail): "Planned" | "Running" | "Closed" {
  const kind = detail.status.kind;
  if (["Cancelled", "TerminatedEarly", "Completed"].includes(kind)) return "Closed";
  return kind === "NotStarted" ? "Planned" : "Running";
}
