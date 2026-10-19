import { useCallback } from "react";
import { useParams } from "react-router";
import { Icon } from "../components/Icon.tsx";
import { CallingAt, CenterMessage, DisplayHeader, OfflineBanner, StatusBadge } from "../components/parts.tsx";
import { formatTime } from "../format.ts";
import { useLiveFeed, useNow } from "../hooks.ts";
import { loadPlatform, type PlatformData } from "../loaders.ts";
import { callingPointsAfter, platformDirection, platformView } from "../rows.ts";
import { useServices } from "../services.tsx";
import type { BoardRow, LineInfo, StationRef } from "../types.ts";
import { holdNotice, NEXT_TRAIN_DOES_NOT_STOP, rowStatus, shownStatus, statusText, styleForKind, styleOf, TEXT, timeLine } from "../wording.ts";
import { StationGate } from "./StationGate.tsx";

// The platform display (US-D2): the next train on one platform with the stations it still calls at, and the following two.
// Address: /platform/FOT/5.
export function PlatformPage() {
  const { station = "", platform = "" } = useParams();
  return <StationGate code={station}>{(found, lines) => <Platform station={found} lines={lines} platform={platform} />}</StationGate>;
}

function Platform({ station, lines, platform }: { station: StationRef; lines: LineInfo[]; platform: string }) {
  const { api } = useServices();
  const load = useCallback(() => loadPlatform(api, station.id, platform), [api, station.id, platform]);
  const feed = useLiveFeed<PlatformData>(station.id, load);
  const now = useNow();

  const view = feed.data === null ? null : platformView(feed.data.rows, platform, now, feed.offline);
  const direction = view === null ? null : platformDirection(view.next, lines, station.id);
  // The calling points belong to the journey that was loaded for the next train; if the next train changed since, they would be wrong.
  const journey = feed.data?.journey ?? null;
  const calling = view?.next && journey && journey.journeyId === view.next.journeyId ? callingPointsAfter(journey, station.id) : [];

  return (
    <div className="display platform-page">
      <DisplayHeader
        station={station.name}
        subtitle={direction === null ? null : TEXT.towards(direction)}
        connection={feed.connection}
        offline={feed.offline}
        nowMillis={now}
      >
        <span className="platform-box" aria-label={`${TEXT.columnPlatform} ${platform}`}>
          <span className="platform-label">{TEXT.columnPlatform}</span>
          <span className="platform-big">{platform}</span>
        </span>
      </DisplayHeader>
      {feed.offline && <OfflineBanner />}

      {view === null ? (
        feed.offline ? null : <CenterMessage icon="clock" text={TEXT.loading} />
      ) : (
        <main className="platform-main">
          {view.next?.callType === "Pass" && (
            <div className="notice notice-warning" role="alert">
              <Icon name="warning" />
              <span>{NEXT_TRAIN_DOES_NOT_STOP}</span>
            </div>
          )}
          {view.movedAway.map((row) => (
            <MovedAwayNotice key={row.journeyId} row={row} offline={feed.offline} />
          ))}

          {view.next === null ? (
            view.movedAway.length === 0 && <CenterMessage icon="calendar-x" text={TEXT.noTrainsOnPlatform} />
          ) : (
            <>
              <NextTrain row={view.next} calling={calling} offline={feed.offline} />
              {view.then.length > 0 && (
                <section className="then" aria-label={TEXT.then}>
                  <h2>{TEXT.then}</h2>
                  <ol>
                    {view.then.map((row) => (
                      <ThenLine key={`${row.journeyId}-${row.time}`} row={row} offline={feed.offline} />
                    ))}
                  </ol>
                </section>
              )}
            </>
          )}
        </main>
      )}
    </div>
  );
}

// "Train 1520 to Kandy now departs from Platform 5" (US-D2 AC3), with S-19 or S-20 below it: the train waits only when a REAL
// hold is set (the board row says so); otherwise it says only where the train departs from. Offline, nothing live is known,
// so the notice is left out (it could be out of date).
function MovedAwayNotice({ row, offline }: { row: BoardRow; offline: boolean }) {
  if (offline) return null;
  const held = row.hold;
  return (
    <div className="notice notice-moved" role="alert">
      <div className="notice-line">
        <Icon name="arrows-exchange" />
        <span>{TEXT.movedAway(row.serviceNumber, row.destination.name, row.platform)}</span>
        <span className="notice-platform">{row.platform}</span>
      </div>
      <div className="notice-line notice-hold">
        <Icon name={held ? "hand-stop" : "walk"} />
        <span>{holdNotice(held, row.platform)}</span>
        {held && <StatusBadge text={statusText(shownStatus(row))} style={styleOf("held")} />}
      </div>
    </div>
  );
}

// The next train: scheduled time, destination and type, status, the time to expect, and where it still calls.
function NextTrain({ row, calling, offline }: { row: BoardRow; calling: string[]; offline: boolean }) {
  const status = rowStatus(row, offline);
  const passes = row.callType === "Pass";
  return (
    <section className="next-train" aria-label={TEXT.next}>
      <div className="next-top">
        <span className="label">{TEXT.next}</span>
        <time className="next-time">{formatTime(row.time)}</time>
        <span className="next-destination">
          <span className="destination">{row.destination.name}</span> <span className="train-type">{row.trainType}</span>
        </span>
        <StatusBadge text={passes && !offline ? "Does not stop" : status.text} style={passes && !offline ? styleForKind("Cancelled") : status.style} />
      </div>
      {timeLine(row, offline) !== null && (
        <div className="next-expected">
          <span className="label">{timeLine(row, offline)?.label}</span>
          <time className="next-time">{timeLine(row, offline)?.time}</time>
          {row.hold && !passes && <span className="hold-line">{holdNotice(true, row.platform)}</span>}
        </div>
      )}
      {!passes && calling.length > 0 && <CallingAt stations={calling} />}
    </section>
  );
}

function ThenLine({ row, offline }: { row: BoardRow; offline: boolean }) {
  const status = rowStatus(row, offline);
  const passes = row.callType === "Pass" && !offline;
  return (
    <li className="then-line">
      <time className="then-time">{formatTime(row.time)}</time>
      <span className="then-destination">
        <span className="destination">{row.destination.name}</span> <span className="train-type">{row.trainType}</span>
      </span>
      {timeLine(row, offline) !== null && (
        <span className="then-expected">
          {timeLine(row, offline)?.label} <time>{timeLine(row, offline)?.time}</time>
        </span>
      )}
      <StatusBadge text={passes ? "Does not stop" : status.text} style={passes ? styleForKind("Cancelled") : status.style} />
    </li>
  );
}
