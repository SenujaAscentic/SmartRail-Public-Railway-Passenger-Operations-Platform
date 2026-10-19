import type { IconName } from "./components/Icon.tsx";
import { formatTime } from "./format.ts";
import type { BoardRow, JourneyStatus } from "./types.ts";

// ALL the wording of the display pages. Status wording (delay, platform, "does not stop", the notices of the platform
// display) comes only from shared/status-fixtures.json, and wording.test.ts runs every fixture against these functions
// (CLAUDE.md section 5.6). The wording that is not a status is further down, marked as such.

// ---- Status wording (the fixtures) ----

// "59 min", "1 h", "1 h 15 min": hours from 60 minutes (decided 12 Oct; fixtures S-05, S-05a to S-05c).
export function minutesText(minutes: number): string {
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
}

// S-01 to S-09 and S-21 to S-23. [terminatesAt] is the station where an early-ended train ends (S-07); without it, S-23.
export function statusText(status: JourneyStatus, terminatesAt?: string): string {
  const delay = status.delayMinutes;
  switch (status.kind) {
    case "OnTime":
      return "On time";
    case "Late":
      return `${minutesText(delay ?? 0)} late`;
    case "Early":
      return `${minutesText(Math.abs(delay ?? 0))} early`;
    case "Cancelled":
      return "Cancelled";
    case "TerminatedEarly":
      return terminatesAt ? `Terminates at ${terminatesAt}` : "Terminates early";
    case "Held":
      return delay !== null && delay > 0 ? `Held · ${minutesText(delay)} late` : "Held";
    case "NotStarted":
      return "Not started";
    case "Completed":
      return "Completed";
    default:
      // A kind this version does not know: say nothing rather than guess (never a false "On time").
      return status.kind;
  }
}

// S-10 and S-11: "Platform 5 (was 2)" or "Platform 3".
export function platformText(platform: string, previousPlatform: string | null): string {
  return previousPlatform === null ? `Platform ${platform}` : `Platform ${platform} (was ${previousPlatform})`;
}

// S-12 and S-13: a stop that is skipped or added; null for every other stop status.
export function stopStatusText(stopStatus: string): string | null {
  if (stopStatus === "Skipped") return "Does not stop";
  if (stopStatus === "Added") return "Extra stop";
  return null;
}

// S-18: the platform display, when the next train passes without stopping.
export const NEXT_TRAIN_DOES_NOT_STOP = "Next train does not stop here – stand back from the platform edge";

// S-19 and S-20: the large information screen and the platform display after a late platform change. Only a REAL hold
// promises that the train waits (S-19); without one it says only where the train departs from (S-20, A-113).
export function holdNotice(holdSet: boolean, platform: string): string {
  return holdSet ? `The train will wait for passengers at Platform ${platform}` : `Now departing from Platform ${platform}`;
}

// ---- How a status looks: the token name (CSS variables --status-<key>-fg / -bg) and the icon from shared/tokens.json ----

export type StatusKey =
  | "on-time"
  | "early"
  | "late"
  | "held"
  | "cancelled"
  | "terminated-early"
  | "platform-changed"
  | "not-started"
  | "scheduled";

export interface StatusStyle {
  key: StatusKey;
  icon: IconName;
}

const STYLES: Record<StatusKey, StatusStyle> = {
  "on-time": { key: "on-time", icon: "check-circle" },
  early: { key: "early", icon: "arrow-up-left" },
  late: { key: "late", icon: "clock-alert" },
  held: { key: "held", icon: "hand-stop" },
  cancelled: { key: "cancelled", icon: "x-circle" },
  "terminated-early": { key: "terminated-early", icon: "x-circle" },
  "platform-changed": { key: "platform-changed", icon: "arrows-exchange" },
  "not-started": { key: "not-started", icon: "clock" },
  scheduled: { key: "scheduled", icon: "calendar" },
};

export const STATUS_STYLES: readonly StatusStyle[] = Object.values(STYLES);

export function styleOf(key: StatusKey): StatusStyle {
  return STYLES[key];
}

// The style for a journey status kind. "Completed" has no token of its own: the neutral one is used (as in the passenger app).
export function styleForKind(kind: string): StatusStyle {
  switch (kind) {
    case "OnTime":
      return STYLES["on-time"];
    case "Early":
      return STYLES.early;
    case "Late":
      return STYLES.late;
    case "Held":
      return STYLES.held;
    case "Cancelled":
      return STYLES.cancelled;
    case "TerminatedEarly":
      return STYLES["terminated-early"];
    case "NotStarted":
      return STYLES["not-started"];
    default:
      return STYLES.scheduled;
  }
}

// ---- What one line of a board or a "then" list says ----

export interface RowStatus {
  text: string;
  style: StatusStyle;
  // "Expected 18:53": only when the expected time differs from the scheduled one and the train still runs.
  expected: string | null;
  // "Extra stop" next to the status.
  extra: string | null;
}

export const OFFLINE_STATUS_TEXT = "Scheduled";

// The status of a line. [offline] means there has been no contact for 2 minutes: then nothing live is known, so the line says
// "Scheduled" and never "On time" (US-D4, and the star rule of US-P4: no false "On time").
export function rowStatus(row: BoardRow, offline: boolean): RowStatus {
  if (offline) return { text: OFFLINE_STATUS_TEXT, style: STYLES.scheduled, expected: null, extra: null };

  const skipped = row.stopStatus === "Skipped";
  const extra = row.stopStatus === "Added" ? stopStatusText("Added") : null;
  if (skipped) return { text: "Does not stop", style: STYLES.cancelled, expected: null, extra };

  const kind = row.status.kind;
  const runs = kind !== "Cancelled" && kind !== "TerminatedEarly" && kind !== "NotStarted";
  const expectedDiffers = runs && row.expectedTime !== null && formatTime(row.expectedTime) !== formatTime(row.time);
  return {
    text: statusText(row.status, row.destination.name),
    style: styleForKind(kind),
    expected: expectedDiffers ? formatTime(row.expectedTime) : null,
    extra,
  };
}

// The platform cell: the number, and the words "Platform 5 (was 2)" when it changed. An empty platform (cancelled, not known) is a dash.
export interface PlatformCell {
  number: string;
  changedText: string | null;
}

export function platformCell(row: BoardRow): PlatformCell {
  if (row.platform === "") return { number: "–", changedText: null };
  return {
    number: row.platform,
    changedText: row.previousPlatform === null ? null : platformText(row.platform, row.previousPlatform),
  };
}

// ---- Wording that is NOT a status (the design, docs/design/display; for Senuja's approval, see NOTES.md) ----

export const TEXT = {
  departures: "Departures",
  towards: (terminus: string) => `Towards ${terminus}`,
  columnTime: "Time",
  columnScheduled: "Scheduled",
  columnDestination: "Destination",
  columnPlatform: "Platform",
  expected: "Expected",
  next: "Next",
  callingAt: "Calling at",
  then: "Then",
  live: "Live",
  offline: "Offline",
  connecting: "Connecting",
  noMoreDepartures: "No more departures today",
  noTrainsOnPlatform: "No trains on this platform",
  loading: "Loading",
  unknownStation: "This station is not known.",
  loadFailed: "The timetable could not be loaded. Please ask staff.",
  // "Train 1520 to Kandy now departs from Platform 5" (US-D2 AC3)
  movedAway: (service: string, destination: string, platform: string) => `Train ${service} to ${destination} now departs from Platform ${platform}`,
  offlineBanner: "Live information unavailable – check announcements or ask staff",
} as const;
