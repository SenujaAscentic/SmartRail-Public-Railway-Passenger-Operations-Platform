import type { ReactNode } from "react";
import { Icon, type IconName } from "./Icon.tsx";

// A message to the person. It always has THREE cues, so no one depends on colour alone (Q-4, tokens.json rule):
// an icon, the words themselves (with "Error" / "Notice" spoken to screen readers), and a border.
type Kind = "error" | "notice";

const KINDS: Record<Kind, { icon: IconName; label: string; role: "alert" | "status" }> = {
  error: { icon: "x-circle", label: "Error", role: "alert" },     // announced at once
  notice: { icon: "clock", label: "Notice", role: "status" },     // announced politely
};

export function Message({ kind, id, children }: { kind: Kind; id?: string; children: ReactNode }) {
  const { icon, label, role } = KINDS[kind];
  return (
    <p className={`message message-${kind}`} role={role} id={id}>
      <Icon name={icon} />
      <span>
        <span className="visually-hidden">{label}: </span>
        {children}
      </span>
    </p>
  );
}
