import { useMemo, useState } from "react";
import { Link } from "react-router";
import { ConnectionBadge } from "../components/ConnectionBadge.tsx";
import { Message } from "../components/Message.tsx";
import { SourceAge } from "../components/SourceAge.tsx";
import { StatusBadge } from "../components/StatusBadge.tsx";
import { isDisrupted, journeyList, lateness, type Detail, type Entry } from "../journeys/journey-store.ts";
import { useJourneyBoard, type Line } from "../journeys/useJourneyBoard.ts";
import { useProfile } from "../session/useProfile.ts";
import { freshnessView, statusView } from "../status/status-text.ts";
import { formatClock } from "../format.ts";
import { useNow } from "../useNow.ts";

// The dashboard (US-O2): problems first. Alerts, then the disrupted trains by lateness, then the line with every running train.
// Everything shown is what the backend sent; the page only sorts and arranges.
export function DashboardPage() {
  const board = useJourneyBoard();
  const profile = useProfile();
  const nowMs = useNow();
  const [station, setStation] = useState("");
  const [fromStation, setFromStation] = useState("");
  const [toStation, setToStation] = useState("");

  const entries = useMemo(() => {
    const all = journeyList(board.store);
    return all.filter((entry) => callsAt(entry.detail, station) && coversSection(entry.detail, fromStation, toStation));
  }, [board.store, station, fromStation, toStation]);

  const stations = useMemo(() => stationsOf(board.line), [board.line]);

  return (
    <>
      <h1>Dashboard</h1>
      <ConnectionBadge connection={board.connection} />
      {board.connection === "failed" && board.connectionDetail && <Message kind="error">{board.connectionDetail}</Message>}
      {board.lineError && <Message kind="error">{board.lineError}</Message>}
      {board.store.resyncs > 0 && <Message kind="notice">Some updates were missed, so the view was reloaded from a fresh snapshot.</Message>}
      {profile?.role === "StationManager" && (
        <Message kind="notice">You see the trains of your own station. Decisions on journeys are made by the controller.</Message>
      )}

      <form className="filters" aria-label="Filters" onSubmit={(event) => event.preventDefault()}>
        <Select label="Station" value={station} onChange={setStation} options={stations} />
        <Select label="Section from" value={fromStation} onChange={setFromStation} options={stations} />
        <Select label="Section to" value={toStation} onChange={setToStation} options={stations} />
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

// Alerts, most severe then newest first (handoff 4b). The alerts of this build are the ones the backend can raise: a running train
// whose position has gone stale or lost.
function AlertsPanel({ entries, nowMs }: { entries: Entry[]; nowMs: number }) {
  const alerts = entries
    .filter((entry) => isRunning(entry.detail) && ["Stale", "Lost"].includes(entry.detail.freshness.tracking))
    .sort((a, b) => severity(b.detail) - severity(a.detail) || (b.detail.freshness.updatedAt ?? "").localeCompare(a.detail.freshness.updatedAt ?? ""));

  return (
    <section aria-labelledby="alerts-h">
      <h2 id="alerts-h">Alerts</h2>
      {alerts.length === 0 ? (
        <p>No alerts.</p>
      ) : (
        <ul className="plain">
          {alerts.map(({ detail }) => (
            <li key={detail.journeyId}>
              <Link to={`/journeys/${detail.journeyId}`}>
                {detail.serviceNumber} {detail.name}
              </Link>{" "}
              <StatusBadge view={freshnessView(detail.freshness, nowMs)} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

const severity = (detail: Detail) => (detail.freshness.tracking === "Lost" ? 2 : 1);

function DisruptedTable({ entries, nowMs }: { entries: Entry[]; nowMs: number }) {
  const rows = entries.filter((entry) => isDisrupted(entry.detail)).sort((a, b) => lateness(b.detail) - lateness(a.detail));

  return (
    <section aria-labelledby="disrupted-h">
      <h2 id="disrupted-h">Disrupted trains</h2>
      {rows.length === 0 ? (
        <p>No disrupted trains.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th scope="col">Train</th>
              <th scope="col">Status</th>
              <th scope="col">Source</th>
              <th scope="col">Next departure</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((entry) => (
              <tr key={entry.detail.journeyId}>
                <td>
                  <Link to={`/journeys/${entry.detail.journeyId}`}>
                    {entry.detail.serviceNumber} {entry.detail.name}
                  </Link>
                </td>
                <td>
                  <StatusBadge view={statusView(entry.detail.status, { terminatesAt: entry.detail.terminatesAt?.name ?? null, hold: entry.detail.hold !== null })} />
                </td>
                <td>
                  <SourceAge source={entry.delaySource} at={entry.delayAt} nowMs={nowMs} />
                </td>
                <td>{formatClock(entry.detail.from.expectedDeparture)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

// The line as a list of stations, one for each direction, with every running train between the stations it is between. The place
// of a train is what the backend announced (last station, next station, how far between them); the page does not work it out.
function LineView({ line, entries, nowMs }: { line: Line; entries: Entry[]; nowMs: number }) {
  return (
    <section aria-labelledby="line-h">
      <h2 id="line-h">Line view</h2>
      <div className="line-view">
        {line.routes.map((route) => {
          const trains = entries.filter(({ detail }) => detail.from.stationId === route.stations[0]?.stationId && isRunning(detail) && detail.freshness.lastPosition);
          return (
            <div key={route.routeId}>
              <h3>{route.name}</h3>
              <ol className="line">
                {route.stations.map((station) => (
                  <li key={station.stationId}>
                    <span className="station">{station.name}</span>
                    <ul className="plain">
                      {trains
                        .filter(({ detail }) => detail.freshness.lastPosition?.lastStationId === station.stationId)
                        .map((entry) => (
                          <Marker key={entry.detail.journeyId} entry={entry} nowMs={nowMs} />
                        ))}
                    </ul>
                  </li>
                ))}
              </ol>
            </div>
          );
        })}
      </div>
    </section>
  );
}

function Marker({ entry, nowMs }: { entry: Entry; nowMs: number }) {
  const { detail } = entry;
  const position = detail.freshness.lastPosition!;
  const next = detail.stops.find((s) => s.stationId === position.nextStationId)?.name ?? "";
  return (
    <li className="train-marker">
      <Link to={`/journeys/${detail.journeyId}`}>{detail.serviceNumber}</Link>{" "}
      <StatusBadge view={statusView(detail.status, { terminatesAt: detail.terminatesAt?.name ?? null, hold: detail.hold !== null })} />{" "}
      <SourceAge source={entry.delaySource} at={entry.delayAt} nowMs={nowMs} /> ·{" "}
      <SourceAge source="Gps" at={detail.freshness.updatedAt} nowMs={nowMs} /> · {Math.round(position.fractionToNext * 100)}% of the way to {next}
    </li>
  );
}
