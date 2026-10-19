import { useCallback } from "react";
import { useParams } from "react-router";
import { Icon } from "../components/Icon.tsx";
import { CenterMessage, DisplayHeader, OfflineBanner, StatusBadge } from "../components/parts.tsx";
import { useLiveFeed, useNow } from "../hooks.ts";
import { loadBoard } from "../loaders.ts";
import { boardRows, groupByDirection, type Direction } from "../rows.ts";
import { useServices } from "../services.tsx";
import { formatTime } from "../format.ts";
import type { BoardRow, LineInfo, StationRef } from "../types.ts";
import { platformCell, rowStatus, TEXT } from "../wording.ts";
import { StationGate } from "./StationGate.tsx";

// The main departures board (US-D1): the next departures of the station, one column for each direction. Address: /board/FOT.
export function BoardPage() {
  const { station = "" } = useParams();
  return <StationGate code={station}>{(found, lines) => <Board station={found} lines={lines} />}</StationGate>;
}

function Board({ station, lines }: { station: StationRef; lines: LineInfo[] }) {
  const { api } = useServices();
  const load = useCallback(() => loadBoard(api, station.id), [api, station.id]);
  const feed = useLiveFeed(station.id, load);
  const now = useNow();

  const rows = feed.data === null ? null : boardRows(feed.data, now, feed.offline);
  const columns = rows === null ? [] : groupByDirection(rows, lines, station.id);

  return (
    <div className="display board-page">
      <DisplayHeader station={station.name} subtitle={TEXT.departures} connection={feed.connection} offline={feed.offline} nowMillis={now} />
      {feed.offline && <OfflineBanner />}
      {rows === null ? (
        feed.offline ? null : <CenterMessage icon="clock" text={TEXT.loading} />
      ) : rows.length === 0 ? (
        <CenterMessage icon="calendar-x" text={TEXT.noMoreDepartures} />
      ) : (
        <main className="columns">
          {columns.map((column) => (
            <BoardColumn key={column.key} column={column} offline={feed.offline} />
          ))}
        </main>
      )}
    </div>
  );
}

function BoardColumn({ column, offline }: { column: Direction; offline: boolean }) {
  return (
    <section className="column" aria-label={TEXT.towards(column.terminus)}>
      <h2 className="column-heading">
        <Icon name={column.arrow === "right" ? "arrow-right" : "arrow-left"} />
        <span>{TEXT.towards(column.terminus)}</span>
      </h2>
      <div className="column-labels" aria-hidden="true">
        <span>{offline ? TEXT.columnScheduled : TEXT.columnTime}</span>
        <span>{TEXT.columnDestination}</span>
        <span>{TEXT.columnPlatform}</span>
      </div>
      <ol className="lines">
        {column.rows.map((row) => (
          <BoardLine key={`${row.journeyId}-${row.time}`} row={row} offline={offline} />
        ))}
      </ol>
    </section>
  );
}

// One train: the scheduled time with the time to expect under it (when that differs), where it goes and what type it is, its
// status, and its platform ("Platform 5 (was 2)" when it changed). Offline it only says "Scheduled": nothing live is known.
function BoardLine({ row, offline }: { row: BoardRow; offline: boolean }) {
  const status = rowStatus(row, offline);
  const platform = platformCell(row);
  const cancelled = row.status.kind === "Cancelled" && !offline;
  const changed = platform.changedText !== null && !cancelled;

  return (
    <li className="line">
      <div className="line-time-cell">
        <time className="line-time">{formatTime(row.time)}</time>
        {status.expected !== null && (
          <span className="expected">
            {TEXT.expected} <time>{status.expected}</time>
          </span>
        )}
      </div>
      <div className="line-main">
        <div className="line-destination">
          <span className="destination">{row.destination.name}</span>
          <span className="train-type">{row.trainType}</span>
        </div>
        <div className="line-status">
          <StatusBadge text={status.text} style={status.style} />
          {status.extra !== null && <span className="extra">{status.extra}</span>}
        </div>
      </div>
      <div className="line-platform">
        {cancelled ? (
          <span className="platform-none" aria-label="No platform">
            –
          </span>
        ) : changed ? (
          // The full words ("Platform 5 (was 2)", fixture S-10) are the label; the cell is too narrow to show them, so it shows the
          // number and "(was 2)" under it, with the changed-platform icon and colours.
          <span className="platform-changed" role="img" aria-label={platform.changedText ?? undefined}>
            <span className="platform-changed-top">
              <Icon name="arrows-exchange" />
              <span className="platform-number">{platform.number}</span>
            </span>
            <span className="platform-was">{platform.wasText}</span>
          </span>
        ) : (
          <span className="platform-number">{platform.number}</span>
        )}
      </div>
    </li>
  );
}
