import { Icon, type IconName } from "./Icon.tsx";
import type { StreamConnection } from "../stream/stream-client.ts";

// LiveIndicator (design component sheet): how the live connection is doing. Words AND an icon AND a border, in the colours of the
// shared status tokens, so it never depends on colour alone (tokens.json rule, Q-4): Live = the OnTime style, Reconnecting = Stale,
// Not connected = Lost.
const STATES: Record<StreamConnection, { status: string; icon: IconName; text: string }> = {
  connecting: { status: "scheduled", icon: "clock", text: "Connecting…" },
  live: { status: "on-time", icon: "pulse", text: "Live · connected" },
  reconnecting: { status: "stale", icon: "signal-weak", text: "Reconnecting…" },
  failed: { status: "lost", icon: "signal-off", text: "Not connected" },
  closed: { status: "lost", icon: "signal-off", text: "Not connected" },
};

export function LiveIndicator({ connection }: { connection: StreamConnection }) {
  const { status, icon, text } = STATES[connection];
  // role="status": a screen reader announces the change politely.
  return (
    <p className={`badge badge-${status}`} role="status">
      <Icon name={icon} />
      <span>{text}</span>
    </p>
  );
}

// FreshnessBanner (design): shown over the values when the connection is not live, because what is on screen may be out of date.
export function FreshnessBanner({ connection }: { connection: StreamConnection }) {
  if (connection === "live" || connection === "connecting") return null;
  return (
    <p className="banner banner-stale" role="alert">
      <Icon name="signal-weak" />
      <span>{connection === "reconnecting" ? "Reconnecting… values below may be out of date." : "No live connection. Values below may be out of date."}</span>
    </p>
  );
}
