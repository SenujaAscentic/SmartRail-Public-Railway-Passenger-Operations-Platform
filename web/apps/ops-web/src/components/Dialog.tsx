import { useEffect, useId, useRef, type ReactNode } from "react";

// A modal dialog over a scrim (design: ConfirmImpactDialog and PlatformChangeDialog sit in one). Focus moves into it when it opens
// and Escape closes it; the page behind is not read as the active place (aria-modal).
export function Dialog({ title, subtitle, onClose, children }: { title: string; subtitle?: string | undefined; onClose: () => void; children: ReactNode }) {
  const titleId = useId();
  const ref = useRef<HTMLDivElement>(null);

  // `onClose` is read through a ref: its identity changes whenever the page re-renders (every stream event), and an effect that
  // depended on it would move focus back to the dialog each time, which closes an open native <select> (rehearsal bug #6).
  const closeRef = useRef(onClose);
  closeRef.current = onClose;

  useEffect(() => {
    ref.current?.focus();   // once, when the dialog opens
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") closeRef.current();
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  return (
    <div className="scrim">
      <div className="dialog" role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1} ref={ref}>
        <h2 id={titleId}>{title}</h2>
        {subtitle && <p className="dialog-sub">{subtitle}</p>}
        {children}
      </div>
    </div>
  );
}
