import { useMemo, useState, type CSSProperties } from "react";
import { Link } from "react-router";
import { Icon } from "../components/Icon.tsx";
import { FreshnessBanner, LiveIndicator } from "../components/LiveIndicator.tsx";
import { Message } from "../components/Message.tsx";
import { Panel } from "../components/Panel.tsx";
import { SourceAgeLabel } from "../components/SourceAgeLabel.tsx";
import { StatusBadge } from "../components/StatusBadge.tsx";
import { formatClock } from "../format.ts";
import { isDisrupted, journeyList, lateness, type Detail, type Entry } from "../journeys/journey-store.ts";
import { useJourneyBoard, type Line } from "../journeys/useJourneyBoard.ts";
import { useProfile } from "../session/useProfile.ts";
import { age, freshnessView, statusView } from "../status/status-text.ts";
import { useNow } from "../useNow.ts";

// The dashboard (US-O2, design "Dashboard"): problems first. Alerts, then the disrupted trains by lateness, then the line with every
// running train. Everything shown is what the backend sent; the page only sorts and arranges.
export function DashboardPage() {
  const board = useJourneyBoard();
  const profile = useProfile();
  const nowMs = useNow();
  const [station, setStation] = useState("");
  const [fromStation, setFromStation] = useState("");
  const [toStation, setToStation] = useState("");

  const entries = useMemo(
    () => journeyList(board.store).filter((entry) => callsAt(entry.detail, station) && coversSection(entry.detail, fromStation, toStation)),
    [board.store, station, fromStation, toStation],
  );
  const stations = useMemo(() => stationsOf(board.line), [board.line]);

  return (
    <>
      <div className="page-head">
        <h1>Dashboard<span className="h1-note"> · {board.line?.name ?? "Main Line"}</span></h1>
        <LiveIndicator connection={board.connection} />
      </div>
      <FreshnessBanner connection={board.connection} />
      {board.connection === "failed" && board.connectionDetail && <Message kind="error">{board.connectionDetail}</Message>}
      {board.lineError && <Message kind="error">{board.lineError}</Message>}
      {board.store.resyncs > 0 && <Message kind="notice">Some updates were missed, so the view was reloaded from a fresh snapshot.</Message>}
      {profile?.role === "StationManager" && (
        <Message kind="notice">You see the trains of your own station. Decisions on journeys are made by the controller.</Message>
      )}

      <form className="filters" aria-label="Filters" onSubmit={(event) => event.preventDefault()}>
        <Select label="Section from" value={fromStation} onChange={setFromStation} options={stations} />
        <Select label="Section to" value={toStation} onChange={setToStation} options={stations} />
        <Select label="Station" value={station} onChange={setStation} options={stations} />
      </form>

      <AlertsPanel entries={entries} nowMs={nowMs} />
      <DisruptedTable entries={entries} nowMs={nowMs} />
      {board.line && <LineView line={board.line} entries={entries} nowMs={nowMs} />}
    </>
  );
}

function Select(props: { label: string; value: string; onChange: (value: string) => void; options: { id: string; name: string }[] }) {
  const id = `filter-${props.label.replace(/\s+/g, "-").toLowerCase()}`;
  return (
    <div className="field">
      <label htmlFor={id}>{props.label}</label>
      <select id={id} value={props.value} onChange={(event) => props.onChange(event.target.value)}>
        <option value="">All stations</option>
        {props.options.map((option) => (
          <option key={option.id} value={option.id}>
            {option.name}
          </option>
        ))}
      </select>
    </div>
  );
}

const stationsOf = (line: Line | null) => {
  const seen = new Map<string, string>();
  for (const route of line?.routes ?? []) for (const s of route.stations) seen.set(s.stationId, s.name);
  return [...seen].map(([id, name]) => ({ id, name }));
};

const callsAt = (detail: Detail, station: string) => station === "" || detail.stops.some((s) => s.stationId === station && s.callType === "Stop");

// A section filter keeps the journeys that go from one chosen station to the other, in that order.
function coversSection(detail: Detail, from: string, to: string): boolean {
  const index = (id: string) => detail.stops.findIndex((s) => s.stationId === id);
  if (from !== "" && index(from) < 0) return false;
  if (to !== "" && index(to) < 0) return false;
  return from === "" || to === "" || index(from) < index(to);
}

const isRunning = (detail: Detail) => !["NotStarted", "Cancelled", "TerminatedEarly", "Completed"].includes(detail.status.kind);
const nameOf = (detail: Detail) => (
  <>
    <span className="mono">{detail.serviceNumber}</span> {detail.name}
  </>
);

// AlertRow (design): a kind badge, the train and what is wrong, how long ago, and a button. The alerts of this build are the ones the
// backend can raise: a running train whose position has gone stale or lost (crew messages and station reports are deferred).
function AlertsPanel({ entries, nowMs }: { entries: Entry[]; nowMs: number }) {
  const alerts = entries
    .filter((entry) => isRunning(entry.detail) && ["Stale", "Lost"].includes(entry.detail.freshness.tracking))
    .sort((a, b) => severity(b.detail) - severity(a.detail) || (b.detail.freshness.updatedAt ?? "").localeCompare(a.detail.freshness.updatedAt ?? ""));

  return (
    <Panel icon="bell" title="Alerts" note={`${alerts.length} · newest and most severe first`}>
      {alerts.length === 0 ? (
        <p className="empty">No alerts.</p>
      ) : (
        <ul className="rows">
          {alerts.map(({ detail }) => {
            const view = freshnessView(detail.freshness, nowMs);
            const lost = detail.freshness.tracking === "Lost";
            return (
              <li key={detail.journeyId} className="alert-row">
                <span className={`status status-${lost ? "lost" : "stale"}`}>
                  <Icon name={lost ? "signal-off" : "signal-weak"} />
                  <span>{lost ? "GPS lost" : "GPS stale"}</span>
                </span>
                <span className="alert-text">
                  <strong>{nameOf(detail)}</strong> · {view.text}
                </span>
                <span className="source">{detail.freshness.updatedAt ? `${age(detail.freshness.updatedAt, nowMs)} ago` : ""}</span>
                <Link className="button-link" to={`/journeys/${detail.journeyId}`}>
                  Open journey
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </Panel>
  );
}

const severity = (detail: Detail) => (detail.freshness.tracking === "Lost" ? 2 : 1);

// DisruptedTrainRow (design): train, route, status, source and age, the next station.
function DisruptedTable({ entries, nowMs }: { entries: Entry[]; nowMs: number }) {
  const rows = entries.filter((entry) => isDisrupted(entry.detail)).sort((a, b) => lateness(b.detail) - lateness(a.detail));

  return (
    <Panel icon="warning" title="Disrupted trains" note={`${rows.length} · cancelled first, then by lateness`}>
      {rows.length === 0 ? (
        <p className="empty">No disrupted trains.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th scope="col">Train</th>
              <th scope="col">Route</th>
              <th scope="col">Status</th>
              <th scope="col">Source · age</th>
              <th scope="col">Next station</th>
              <th scope="col"><span className="visually-hidden">Open</span></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((entry) => {
              const { detail } = entry;
              const next = detail.stops.find((s) => s.status === "Scheduled" || s.status === "Arrived");
              const closed = detail.status.kind === "Cancelled" || detail.status.kind === "TerminatedEarly";
              return (
                <tr key={detail.journeyId}>
                  <th scope="row">{nameOf(detail)}</th>
                  <td>{detail.from.name} → {detail.to.name}</td>
                  <td>
                    <StatusBadge view={statusView(detail.status, { terminatesAt: detail.terminatesAt?.name ?? null, hold: detail.hold !== null })} />
                  </td>
                  <td>
                    <SourceAgeLabel source={entry.delaySource} at={entry.delayAt} nowMs={nowMs} override />
                  </td>
                  <td className="mono">{!closed && next ? `${next.name} · ${formatClock(next.expectedArrival ?? next.expectedDeparture)}` : "—"}</td>
                  <td>
                    <Link to={`/journeys/${detail.journeyId}`}>
                      Open<span className="visually-hidden"> {detail.serviceNumber}</span>
                    </Link>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </Panel>
  );
}

// LineView + TrainMarker (design): the line as one track of stations. Trains going the way of the first route sit above it, the
// others below. The place of a train is what the backend announced (last station, next station, how far between them); the page only
// turns that into a position on the track.
function LineView({ line, entries, nowMs }: { line: Line; entries: Entry[]; nowMs: number }) {
  const track = line.routes[0]?.stations ?? [];
  if (track.length < 2) return null;
  const index = (id: string) => track.findIndex((s) => s.stationId === id);
  const first = track[0]!;
  const last = track[track.length - 1]!;

  const placed = entries
    .filter(({ detail }) => isRunning(detail) && detail.freshness.lastPosition && index(detail.freshness.lastPosition.lastStationId) >= 0)
    .map((entry) => {
      const position = entry.detail.freshness.lastPosition!;
      const from = index(position.lastStationId);
      const to = index(position.nextStationId);
      const where = to < 0 ? from : from + (to - from) * position.fractionToNext;
      return { entry, percent: (where / (track.length - 1)) * 100, towardsEnd: entry.detail.from.stationId === first.stationId };
    });

  return (
    <Panel icon="chart" title="Line view" note="select a train to open its journey">
      <div className="line-view">
        <p className="direction">→ Towards {last.name}</p>
        <div className="markers markers-above">
          {placed.filter((p) => p.towardsEnd).map((p) => <TrainMarker key={p.entry.detail.journeyId} {...p} nowMs={nowMs} />)}
        </div>
        <ol className="track" style={{ gridTemplateColumns: `repeat(${track.length}, 1fr)`, "--cols": track.length } as CSSProperties}>
          {track.map((s) => (
            <li key={s.stationId}>
              <span className="node" aria-hidden="true" />
              <span className="station">{s.name}</span>
            </li>
          ))}
        </ol>
        <div className="markers markers-below">
          {placed.filter((p) => !p.towardsEnd).map((p) => <TrainMarker key={p.entry.detail.journeyId} {...p} nowMs={nowMs} />)}
        </div>
        <p className="direction">← Towards {first.name}</p>
      </div>
    </Panel>
  );
}

function TrainMarker({ entry, percent, nowMs }: { entry: Entry; percent: number; nowMs: number }) {
  const { detail } = entry;
  const position = detail.freshness.lastPosition!;
  const name = (id: string) => detail.stops.find((s) => s.stationId === id)?.name ?? "";
  const where = position.fractionToNext > 0 ? `between ${name(position.lastStationId)} and ${name(position.nextStationId)}` : `at ${name(position.lastStationId)}`;
  const view = statusView(detail.status, { terminatesAt: detail.terminatesAt?.name ?? null, hold: detail.hold !== null });

  return (
    <Link className={`train-marker marker-${view.tone}`} to={`/journeys/${detail.journeyId}`} style={{ left: `${percent}%` }}>
      <span className="marker-title">
        <span className="mono">{detail.serviceNumber}</span> · {view.text}
      </span>
      <span className="source">{where}</span>
      <SourceAgeLabel source={entry.delaySource} at={entry.delayAt} nowMs={nowMs} override />
      <SourceAgeLabel source="Gps" at={detail.freshness.updatedAt} nowMs={nowMs} />
    </Link>
  );
}
