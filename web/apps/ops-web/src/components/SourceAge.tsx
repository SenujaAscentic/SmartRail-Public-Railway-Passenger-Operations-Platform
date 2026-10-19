import { age, sourceLabel } from "../status/status-text.ts";

// "GPS · 15 s ago": where a value came from and how old it is (design rule 4). The source is only known from the ops stream.
export function SourceAge({ source, at, nowMs }: { source: string | null | undefined; at?: string | null | undefined; nowMs: number }) {
  return (
    <span className="source">
      {sourceLabel(source)}
      {at ? ` · ${age(at, nowMs)} ago` : ""}
    </span>
  );
}
