import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router";
import { Dialog } from "../components/Dialog.tsx";
import { Icon, type IconName } from "../components/Icon.tsx";
import { FreshnessBanner, LiveIndicator } from "../components/LiveIndicator.tsx";
import { Message } from "../components/Message.tsx";
import { SourceAgeLabel } from "../components/SourceAgeLabel.tsx";
import { StatusBadge } from "../components/StatusBadge.tsx";
import { formatClock } from "../format.ts";
import type { Detail, Entry } from "../journeys/journey-store.ts";
import { useAction, type Conflict } from "../journeys/useAction.ts";
import { useJourneyBoard } from "../journeys/useJourneyBoard.ts";
import { useServices } from "../services-context.tsx";
import { useProfile } from "../session/useProfile.ts";
import { freshnessView, platformText, statusView, stopText } from "../status/status-text.ts";
import { useNow } from "../useNow.ts";
import {
  CancelForm, DelayForm, HoldForm, ProblemMessage, REASONS, SkipForm, TerminateForm, aheadStops, skippableStops,
} from "./journey/ActionForms.tsx";
import { PlatformDialog } from "./journey/PlatformDialog.tsx";

type Open = "delay" | "hold" | "cancel" | "terminate" | "skip" | "platform" | null;

// One journey (US-O3) with the controller's decisions (US-O4, US-O5), laid out as in the design: a header card, the action bar,
// and the stops. The values are the backend's; each shows where it came from and how old it is.
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
  entry: Entry; nowMs: number; canDecide: boolean; connection: Parameters<typeof LiveIndicator>[0]["connection"]; applyDetail: (detail: Detail) => void;
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
  const state = runState(detail);
  const isOpen = state !== "Closed";
  const ctx = { api, detail, action, close: closeForm };
  const choose = (next: Open) => {
    action.clear();
    setOpen(next);
  };
  const nextStop = aheadStops(detail)[0];
  const delayText = detail.status.delayMinutes === null ? "—" : `${Math.abs(detail.status.delayMinutes)} min`;
  const reasonName = (code: string) => REASONS.find(([value]) => value === code)?.[1] ?? code;
  const reasonLabel = detail.reason ? `${reasonName(detail.reason.code)}${detail.reason.note ? ` – ${detail.reason.note}` : ""}` : "—";

  return (
    <>
      <p className="back"><Link to="/"><Icon name="arrow-left" /> Dashboard</Link></p>

      <section className="panel journey-head" aria-labelledby="journey-title">
        <div className="page-head">
          <h1 id="journey-title">
            <span className="mono">{detail.serviceNumber}</span> {detail.name} · {detail.from.name} → {detail.to.name}
          </h1>
          <StatusBadge view={status} />
          <LiveIndicator connection={connection} />
        </div>
        {detail.terminatesAt && <p>The journey now ends at {detail.terminatesAt.name}.</p>}
        <FreshnessBanner connection={connection} />

        <dl className="facts">
          <div>
            <dt>Delay</dt>
            <dd>{delayText}</dd>
            <dd><SourceAgeLabel source={entry.delaySource} at={entry.delayAt} nowMs={nowMs} override /></dd>
          </div>
          <div>
            <dt>Tracking</dt>
            <dd><StatusBadge view={freshnessView(detail.freshness, nowMs)} /></dd>
            <dd><SourceAgeLabel source="Gps" at={detail.freshness.updatedAt} nowMs={nowMs} /></dd>
          </div>
          <div>
            <dt>Reason</dt>
            <dd>{reasonLabel}</dd>
            {detail.reason && <dd><SourceAgeLabel source="Controller" at={entry.delayAt} nowMs={nowMs} override /></dd>}
          </div>
          <div>
            <dt>Next stop</dt>
            <dd>{nextStop ? `${nextStop.name} · ${formatClock(nextStop.expectedArrival ?? nextStop.expectedDeparture)} · ${platformText(nextStop.platform, nextStop.previousPlatform)}` : "—"}</dd>
            {nextStop && <dd className="source">Scheduled {formatClock(nextStop.scheduledArrival ?? nextStop.scheduledDeparture)}</dd>}
          </div>
        </dl>
        {detail.hold && (
          <p className="hold-line">
            <Icon name="hand-stop" /> The train is held at {detail.stops.find((s) => s.stationId === detail.hold?.stationId)?.name ?? "a station"}
            {detail.hold.minutes ? ` for ${detail.hold.minutes} min` : ""}
            {detail.hold.reason ? ` (${reasonName(detail.hold.reason.code)}${detail.hold.reason.note ? `: ${detail.hold.reason.note}` : ""})` : ""}
          </p>
        )}
        <p className="source">Version {detail.version}</p>
      </section>

      {action.conflict && <ConflictBanner conflict={action.conflict} onReload={() => { if (action.conflict?.journey) applyDetail(action.conflict.journey); closeForm(); }} />}
      {action.done && <Message kind="notice">Done. The change is on its way to everyone.</Message>}
      {!open && <ProblemMessage action={action} />}

      {canDecide ? (
        <section aria-label="Decisions">
          <div className="action-bar" role="group" aria-label="Actions">
            {isOpen && <ActionButton icon="clock" label="Delay" active={open === "delay"} onClick={() => choose("delay")} />}
            {isOpen && !detail.hold && <ActionButton icon="hand-stop" label="Hold" active={open === "hold"} onClick={() => choose("hold")} />}
            {isOpen && detail.hold && (
              <ActionButton icon="hand-stop" label="Release the hold" busy={action.busy} onClick={() => void action.run(() => api.POST("/api/v1/ops/journeys/{journeyId}/hold/release", { params: { path: { journeyId: detail.journeyId } }, body: { expectedVersion: detail.version } }))} />
            )}
            {isOpen && aheadStops(detail).length > 0 && <ActionButton icon="arrows-exchange" label="Change platform" onClick={() => choose("platform")} />}
            {isOpen && skippableStops(detail).length > 0 && <ActionButton icon="ban" label="Skip stop" onClick={() => choose("skip")} />}
            {state === "Running" && aheadStops(detail).length > 0 && <ActionButton icon="skip-forward" label="Terminate early" danger onClick={() => choose("terminate")} />}
            {isOpen && <ActionButton icon="x-circle" label="Cancel journey" danger onClick={() => choose("cancel")} />}
            {isOpen && entry.delaySource === "Controller" && (
              <ActionButton icon="undo" label="Clear the delay" busy={action.busy} onClick={() => void action.run(() => api.DELETE("/api/v1/ops/journeys/{journeyId}/delay", { params: { path: { journeyId: detail.journeyId } }, body: { expectedVersion: detail.version } }))} />
            )}
            <ActionButton
              icon="undo"
              label="Reinstate"
              disabled={detail.status.kind !== "Cancelled"}
              busy={action.busy}
              onClick={() => void action.run(() => api.POST("/api/v1/ops/journeys/{journeyId}/reinstate", { params: { path: { journeyId: detail.journeyId } }, body: { expectedVersion: detail.version } }))}
            />
          </div>
          {detail.status.kind !== "Cancelled" && <p className="hint">Reinstate: only for cancelled journeys</p>}

          {open === "delay" && <div className="panel inline-panel"><DelayForm {...ctx} /></div>}
          {open === "hold" && <div className="panel inline-panel"><HoldForm {...ctx} /></div>}
          {open === "cancel" && (
            <Dialog title="Cancel this journey?" subtitle={subtitle(detail)} onClose={closeForm}><CancelForm {...ctx} /><ProblemMessage action={action} /></Dialog>
          )}
          {open === "terminate" && (
            <Dialog title="Terminate this journey early?" subtitle={subtitle(detail)} onClose={closeForm}><TerminateForm {...ctx} /><ProblemMessage action={action} /></Dialog>
          )}
          {open === "skip" && (
            <Dialog title="Skip a stop?" subtitle={subtitle(detail)} onClose={closeForm}><SkipForm {...ctx} /><ProblemMessage action={action} /></Dialog>
          )}
          {open === "platform" && (
            <Dialog title="Change platform" subtitle={`${subtitle(detail)} · ${status.text}`} onClose={closeForm}><PlatformDialog {...ctx} /><ProblemMessage action={action} /></Dialog>
          )}
        </section>
      ) : (
        <Message kind="notice">Only the controller can make decisions on a journey.</Message>
      )}

      <section className="panel" aria-labelledby="stops-h">
        <header className="panel-head"><Icon name="list" /><h2 id="stops-h">Stops</h2></header>
        <div className="panel-body flush">
          <table>
            <thead>
              <tr>
                <th scope="col">Station</th>
                <th scope="col">Platform</th>
                <th scope="col">Scheduled</th>
                <th scope="col">Expected</th>
                <th scope="col">Actual</th>
                <th scope="col">Stop status</th>
                <th scope="col">Source · age</th>
              </tr>
            </thead>
            <tbody>
              {detail.stops.map((stop) => {
                const sources = entry.stopSources[stop.stationId];
                const isNext = stop.stationId === nextStop?.stationId;
                const source = stop.actualDeparture ? sources?.departure : sources?.arrival;
                const actualAt = stop.actualDeparture ?? stop.actualArrival;
                return (
                  <tr key={stop.stationId} className={`stop-row${isNext ? " next" : ""}${stop.status === "Departed" || stop.status === "Passed" ? " past" : ""}`}>
                    <th scope="row">
                      {stop.name}
                      {isNext && <span className="next-badge">Next</span>}
                    </th>
                    <td className="mono">{platformText(stop.platform, stop.previousPlatform)}</td>
                    <td className="mono">{formatClock(stop.scheduledArrival)} / {formatClock(stop.scheduledDeparture)}</td>
                    <td className="mono">{formatClock(stop.expectedArrival)} / {formatClock(stop.expectedDeparture)}</td>
                    <td className="mono">{formatClock(stop.actualArrival)} / {formatClock(stop.actualDeparture)}</td>
                    <td>{stopText(stop.status) ?? stop.status}{stop.callType === "Pass" ? " · passes" : ""}</td>
                    <td>{actualAt ? <SourceAgeLabel source={source} at={actualAt} nowMs={nowMs} /> : <span className="source">—</span>}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>
    </>
  );
}

const subtitle = (detail: Detail) => `${detail.serviceNumber} ${detail.name} · ${detail.from.name} → ${detail.to.name}`;

// ActionBar buttons (design): icon + words; default, danger and disabled variants. A disabled one is explained next to the bar.
function ActionButton({ icon, label, onClick, danger = false, disabled = false, busy = false, active = false }: {
  icon: IconName; label: string; onClick: () => void; danger?: boolean; disabled?: boolean; busy?: boolean; active?: boolean;
}) {
  return (
    <button type="button" className={`action-button${danger ? " danger-outline" : ""}${active ? " active" : ""}`} disabled={disabled || busy} onClick={onClick}>
      <Icon name={icon} />
      <span>{label}</span>
    </button>
  );
}

// ConflictBanner (design): somebody else decided first. Dark text on the warning colour, icon and words, and a Reload button.
function ConflictBanner({ conflict, onReload }: { conflict: Conflict; onReload: () => void }) {
  return (
    <div className="banner banner-warning conflict" role="alert">
      <Icon name="warning" />
      <span>{conflict.text} – reload</span>
      <button type="button" onClick={onReload}>Reload</button>
    </div>
  );
}

// The detail has no "state" of its own besides the status kind: Planned = not started and not closed, Running = has data.
function runState(detail: Detail): "Planned" | "Running" | "Closed" {
  const kind = detail.status.kind;
  if (["Cancelled", "TerminatedEarly", "Completed"].includes(kind)) return "Closed";
  return kind === "NotStarted" ? "Planned" : "Running";
}
