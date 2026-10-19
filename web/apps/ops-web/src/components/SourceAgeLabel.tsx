import { age, sourceLabel } from "../status/status-text.ts";
import { Icon, type IconName } from "./Icon.tsx";

// SourceAgeLabel (design component sheet): where a value came from and how old it is, "GPS · 15 s ago" (design rule 4, US-O3 star
// criterion). The source is only known from the ops stream. A controller's value is labelled as an override. The icon repeats the
// words; it never carries the meaning alone.
const ICONS: Record<string, IconName> = { Gps: "satellite", Crew: "driver", Controller: "controls", Schedule: "clock" };

export function SourceAgeLabel({ source, at, nowMs, override = false }: {
  source: string | null | undefined; at?: string | null | undefined; nowMs: number; override?: boolean;
}) {
  const label = source === "Controller" && override ? "Controller override" : sourceLabel(source);
  return (
    <span className="source">
      {source && ICONS[source] && <Icon name={ICONS[source]} />}
      {label}
      {at ? ` · ${age(at, nowMs)} ago` : ""}
    </span>
  );
}
