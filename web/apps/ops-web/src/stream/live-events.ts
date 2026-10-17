import type { StreamConnection, StreamFrame } from "./stream-client.ts";

// How many recent events the page keeps. It is a live view, not a log.
export const MAX_EVENTS = 50;

// What the page shows about one event. Only the fields EVERY event has (the envelope, domain model 8.1) are read;
// the payload stays opaque JSON (`raw`) until the events have a generated schema.
export interface LiveEvent {
  key: number;
  name: string;
  sequence: number | null;
  occurredAt: string | null;
  journeyId: string | null;
  raw: string;
}

const asString = (value: unknown): string | null => (typeof value === "string" ? value : null);

// The server is not trusted to match the examples exactly: a missing or odd field becomes null, never an exception.
export function describeFrame(frame: StreamFrame, key: number): LiveEvent {
  let data: Record<string, unknown> = {};
  try {
    const parsed: unknown = JSON.parse(frame.data);
    if (typeof parsed === "object" && parsed !== null && !Array.isArray(parsed)) data = parsed as Record<string, unknown>;
  } catch {
    // not JSON: keep the raw text, read nothing from it
  }

  // The frame's id: line is the sequence number. A snapshot may have none, so fall back to the data's own "sequence".
  const fromId = /^\d+$/.test(frame.id) ? Number(frame.id) : null;
  const fromData = typeof data.sequence === "number" && Number.isInteger(data.sequence) ? data.sequence : null;

  return {
    key,
    name: frame.name || "message",
    sequence: fromId ?? fromData,
    occurredAt: asString(data.occurredAt),
    journeyId: asString(data.journeyId),
    raw: frame.data,
  };
}

export interface LiveState {
  connection: StreamConnection;
  detail: string | undefined;
  events: LiveEvent[];   // newest first, at most MAX_EVENTS
  received: number;      // all frames since the page opened
  snapshots: number;
  resyncs: number;
}

export const initialLiveState: LiveState = { connection: "connecting", detail: undefined, events: [], received: 0, snapshots: 0, resyncs: 0 };

export type LiveAction =
  | { type: "connection"; state: StreamConnection; detail?: string | undefined }
  | { type: "frame"; frame: StreamFrame };

export function liveReducer(state: LiveState, action: LiveAction): LiveState {
  if (action.type === "connection") {
    return { ...state, connection: action.state, detail: action.detail };
  }

  const event = describeFrame(action.frame, state.received + 1);
  const received = state.received + 1;

  // "resync": the server could not replay what was missed, and sends a fresh snapshot next. What is on screen may be
  // stale, so it is dropped; the snapshots that follow fill the page again.
  if (event.name === "resync") {
    return { ...state, events: [event], received, resyncs: state.resyncs + 1 };
  }

  return {
    ...state,
    events: [event, ...state.events].slice(0, MAX_EVENTS),
    received,
    snapshots: state.snapshots + (event.name === "snapshot" ? 1 : 0),
  };
}
