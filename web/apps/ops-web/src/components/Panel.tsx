import { useId, type ReactNode } from "react";
import { Icon, type IconName } from "./Icon.tsx";

// A card with a header: icon, title and a short note ("3 · newest and most severe first"), as every block of the design has.
export function Panel({ icon, title, note, children }: { icon: IconName; title: string; note?: string | undefined; children: ReactNode }) {
  const id = useId();
  return (
    <section className="panel" aria-labelledby={id}>
      <header className="panel-head">
        <Icon name={icon} />
        <h2 id={id}>{title}</h2>
        {note && <span className="panel-note">· {note}</span>}
      </header>
      <div className="panel-body">{children}</div>
    </section>
  );
}
