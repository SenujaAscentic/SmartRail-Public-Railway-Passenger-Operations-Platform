import { useEffect, useId, useRef, type ReactNode } from "react";

const FOCUSABLE = 'a[href], button, input, select, textarea, [tabindex]:not([tabindex="-1"])';

// A modal dialog over a scrim (design: ConfirmImpactDialog and PlatformChangeDialog sit in one). Focus moves into it when it opens
// and Escape closes it; Tab stays inside it (a focus trap) and focus returns to the button that opened it when it closes; the page
// behind is not read as the active place (aria-modal).
export function Dialog({ title, subtitle, onClose, children }: { title: string; subtitle?: string | undefined; onClose: () => void; children: ReactNode }) {
  const titleId = useId();
  const ref = useRef<HTMLDivElement>(null);

  // `onClose` is read through a ref: its identity changes whenever the page re-renders (every stream event), and an effect that
  // depended on it would move focus back to the dialog each time, which closes an open native <select> (rehearsal bug #6).
  const closeRef = useRef(onClose);
  closeRef.current = onClose;

  useEffect(() => {
    const opener = document.activeElement;   // the button that opened the dialog: focus goes back to it when the dialog closes
    ref.current?.focus();                    // once, when the dialog opens
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        closeRef.current();
        return;
      }
      if (event.key !== "Tab" || !ref.current) return;

      // Tab stays inside the dialog: from the last control it wraps to the first, and Shift+Tab the other way round.
      const controls = Array.from(ref.current.querySelectorAll<HTMLElement>(FOCUSABLE)).filter((el) => !el.hasAttribute("disabled"));
      if (controls.length === 0) {
        event.preventDefault();
        return;
      }
      const first = controls[0]!;
      const last = controls[controls.length - 1]!;
      const active = document.activeElement;
      if (event.shiftKey && (active === first || active === ref.current)) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && active === last) {
        event.preventDefault();
        first.focus();
      } else if (!ref.current.contains(active)) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("keydown", onKey);
      if (opener instanceof HTMLElement && opener.isConnected) opener.focus();
    };
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
