import type { ReactNode } from "react";
import type { Connection } from "../feed/live-feed.ts";
import { formatClock } from "../format.ts";
import { TEXT, type StatusStyle } from "../wording.ts";
import { Icon, type IconName } from "./Icon.tsx";

// Small pieces both display pages use.

// A status: icon + text + colour, never colour alone (design handoff rule 1). The colours are the status tokens
// (CSS variables --status-<key>-fg and -bg); the page styles pick them up from the class.
export function StatusBadge({ text, style }: { text: string; style: StatusStyle }) {
  return (
    <span className={`status-badge status-${style.key}`}>
      <Icon name={style.icon} />
      <span>{text}</span>
    </span>
  );
}

// "Live" or "Offline" (or "Connecting") in the corner, in words and with an icon.
export function ConnectionBadge({ connection, offline }: { connection: Connection; offline: boolean }) {
  const [kind, icon, text]: [string, IconName, string] = offline
    ? ["offline", "offline", TEXT.offline]
    : connection === "live"
      ? ["live", "live", TEXT.live]
      : ["connecting", "signal-weak", TEXT.connecting];
  return (
    <span className={`connection connection-${kind}`} role="status">
      <Icon name={icon} />
      <span>{text}</span>
    </span>
  );
}

// The blue bar on top: the station and what this screen shows (left), the platform when it is a platform display, the
// connection and the clock (right). [children] are the extra items between the title and the connection.
export function DisplayHeader({
  station,
  subtitle,
  connection,
  offline,
  nowMillis,
  children,
}: {
  station: string;
  subtitle: string | null;
  connection: Connection;
  offline: boolean;
  nowMillis: number;
  children?: ReactNode;
}) {
  return (
    <header className="display-header">
      <h1>
        <span className="station-name">{station}</span>
        {subtitle !== null && <span className="subtitle">{subtitle}</span>}
      </h1>
      <div className="header-right">
        {children}
        <ConnectionBadge connection={connection} offline={offline} />
        <time className="clock">{formatClock(nowMillis)}</time>
      </div>
    </header>
  );
}

// The amber bar when the display has been out of contact for 2 minutes (US-D4). The exact words come from the story.
export function OfflineBanner() {
  return (
    <div className="offline-banner" role="alert">
      <Icon name="warning" />
      <span>{TEXT.offlineBanner}</span>
    </div>
  );
}

// A message in the middle of the screen (nothing to show, not loaded yet, station unknown).
export function CenterMessage({ icon, text }: { icon: IconName; text: string }) {
  return (
    <main className="center-message">
      <Icon name={icon} />
      <p>{text}</p>
    </main>
  );
}

// "Calling at": the stations in one line. A long line scrolls slowly (the text is repeated so that the loop has no gap);
// people who asked their system for less motion get the text wrapped instead (see styles.css).
export function CallingAt({ stations }: { stations: string[] }) {
  const text = stations.join(" · ");
  const long = text.length > 60;
  return (
    <div className="calling-at">
      <span className="label">{TEXT.callingAt}</span>
      <div className={`calling-list${long ? " scrolling" : ""}`}>
        <span className="calling-text">{text}</span>
        {long && (
          <span className="calling-text" aria-hidden="true">
            {text}
          </span>
        )}
      </div>
    </div>
  );
}
