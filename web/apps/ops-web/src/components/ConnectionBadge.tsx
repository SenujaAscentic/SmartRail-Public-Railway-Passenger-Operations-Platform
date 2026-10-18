import { Icon, type IconName } from "./Icon.tsx";
import type { StreamConnection } from "../stream/stream-client.ts";

// How the live connection is doing. Words AND an icon AND a border, in the colours of the shared status tokens, so it
// never depends on colour alone (tokens.json rule, Q-4). The connection "Live" uses the OnTime style, "Reconnecting" the
// Stale style, "Not connected" the Lost style: the same look the rest of SmartRail gives those meanings.
const BADGES: Record<StreamConnection, { status: string; icon: IconName; text: string }> = {
  connecting: { status: "scheduled", icon: "clock", text: "Connecting…" },
  live: { status: "on-time", icon: "check-circle", text: "Live" },
  reconnecting: { status: "stale", icon: "signal-weak", text: "Reconnecting…" },
  failed: { status: "lost", icon: "signal-off", text: "Not connected" },
  closed: { status: "lost", icon: "signal-off", text: "Not connected" },
};

export function ConnectionBadge({ connection }: { connection: StreamConnection }) {
  const { status, icon, text } = BADGES[connection];
  // role="status": a screen reader announces the change politely.
  return (
    <p className={`badge badge-${status}`} role="status">
      <Icon name={icon} />
      <span>{text}</span>
    </p>
  );
}
