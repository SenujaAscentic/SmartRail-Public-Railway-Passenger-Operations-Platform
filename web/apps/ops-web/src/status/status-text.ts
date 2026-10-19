import type { components } from "@smartrail/api-client";
import type { IconName } from "../components/Icon.tsx";
import { formatClock } from "../format.ts";

// The words and icons for a journey's state. ALL status wording lives here and comes from shared/status-fixtures.json
// (CLAUDE.md section 5, step 6): the test runs every case of that file against these functions. The backend decides WHAT the state
// is (kind, delay, hold); this file only says it in words, and never works out a delay or a status itself.
type Status = components["schemas"]["JourneyStatusResponse"];
type Freshness = components["schemas"]["JourneyDetailFreshnessResponse"];

// The names of the status tokens (shared/tokens.json -> CSS variables --status-<tone>-fg / -bg).
export type Tone = "on-time" | "early" | "late" | "held" | "cancelled" | "terminated-early" | "not-started" | "stale" | "lost";

export interface View {
  text: string;
  icon: IconName;
  tone: Tone;
}

// S-05: whole minutes below an hour, then hours ("59 min", "1 h", "1 h 15 min"); never "0 min".
const minutes = (n: number) => {
  const total = Math.abs(n);
  if (total < 60) return `${total} min`;
  const rest = total % 60;
  return rest === 0 ? `${Math.floor(total / 60)} h` : `${Math.floor(total / 60)} h ${rest} min`;
};

function delayView(kind: string, delayMinutes: number | null | undefined): View {
  const n = delayMinutes ?? 0;
  if (kind === "Late") return { text: `${minutes(n)} late`, icon: "clock-alert", tone: "late" };
  if (kind === "Early") return { text: `${minutes(n)} early`, icon: "arrow-up-left", tone: "early" };
  return { text: "On time", icon: "check-circle", tone: "on-time" };
}

// S-01 to S-09. `hold` is true while the train is held (the backend sends the hold apart from the kind): then the text is
// "Held · 3 min late" (S-08). `terminatesAt` is the station name for an early termination (S-07).
export function statusView(status: Status, extra: { terminatesAt?: string | null; hold?: boolean } = {}): View {
  const { kind, delayMinutes } = status;
  if (kind === "Cancelled") return { text: "Cancelled", icon: "x-circle", tone: "cancelled" };
  if (kind === "TerminatedEarly") {
    return { text: extra.terminatesAt ? `Terminates at ${extra.terminatesAt}` : "Terminates early", icon: "x-circle", tone: "terminated-early" };
  }
  if (kind === "NotStarted") return { text: "Not started", icon: "clock", tone: "not-started" };
  if (kind === "Completed") return { text: "Completed", icon: "calendar", tone: "not-started" };

  const delay = delayView(kind === "Held" ? "Late" : kind, delayMinutes);
  if (kind === "Held" || extra.hold) {
    // S-21: no delay to add, so just "Held"; S-08: "Held · 3 min late".
    return { text: (delayMinutes ?? 0) === 0 ? "Held" : `Held · ${delay.text}`, icon: "hand-stop", tone: "held" };
  }
  return delay;
}

// S-10 and S-11.
export function platformText(platform: string, previousPlatform: string | null | undefined): string {
  return previousPlatform ? `Platform ${platform} (was ${previousPlatform})` : `Platform ${platform}`;
}

// S-12 and S-13: a stop the train will not make, or an extra one.
export function stopText(stopStatus: string): string | null {
  if (stopStatus === "Skipped") return "Does not stop";
  if (stopStatus === "Added") return "Extra stop";
  return null;
}

// S-19 and S-20: what the information screens say after a platform change. "The train will wait" only with a real hold (A-113).
export function platformInfoText(holdSet: boolean, platform: string): string {
  return holdSet ? `The train will wait for passengers at Platform ${platform}` : `Now departing from Platform ${platform}`;
}

// S-14 to S-16: how fresh the position is. `nowMs` is the time the page counts from, in milliseconds.
export function freshnessView(freshness: Pick<Freshness, "tracking" | "updatedAt" | "lastStationName">, nowMs: number): View {
  const { tracking, updatedAt, lastStationName } = freshness;
  if (tracking === "Lost") return { text: "Live location unavailable – scheduled times shown", icon: "signal-off", tone: "lost" };
  if (tracking === "Stale") {
    const at = updatedAt ? formatClock(updatedAt) : null;
    const near = lastStationName ? ` near ${lastStationName}` : "";
    return { text: at ? `Last seen ${at}${near}` : "Last seen earlier", icon: "signal-weak", tone: "stale" };
  }
  if (tracking === "Live" && updatedAt) return { text: `Updated ${age(updatedAt, nowMs)} ago`, icon: "check-circle", tone: "on-time" };
  // S-25 to S-27: NotStarted, Ended and Live without an age have nothing to say; the status badge already does (decided 20 Oct).
  return { text: "", icon: "clock", tone: "not-started" };
}

// "30 s", "4 min", "1 h 5 min": the age of a value, counted from the time the page was told it.
export function age(isoUtc: string, nowMs: number): string {
  const seconds = Math.max(0, Math.floor((nowMs - Date.parse(isoUtc)) / 1000));
  if (seconds < 60) return `${seconds} s`;
  const mins = Math.floor(seconds / 60);
  return mins < 60 ? `${mins} min` : `${Math.floor(mins / 60)} h ${mins % 60} min`;
}

// Ops screens show where each value comes from (design handoff rule 4; US-O3 star criterion).
const SOURCES: Record<string, string> = { Gps: "GPS", Crew: "Driver", Controller: "Controller", Schedule: "Schedule" };
export const sourceLabel = (source: string | null | undefined): string => (source ? (SOURCES[source] ?? source) : "unknown");
