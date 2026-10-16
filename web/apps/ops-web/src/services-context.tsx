import { createContext, useContext, useEffect, useSyncExternalStore, type ReactNode } from "react";
import type { Services } from "./services.ts";
import type { SessionState } from "./session/session.ts";

const ServicesContext = createContext<Services | null>(null);

// Provides the services to every screen, restores the session when the page loads, and tells the session when the
// person is active (so it can refresh, and so it knows when they have been away).
export function ServicesProvider({ services, children }: { services: Services; children: ReactNode }) {
  const { session } = services;

  useEffect(() => {
    void session.restore();   // a second call (React's development double-run) shares the first one's request
  }, [session]);

  useEffect(() => {
    const events = ["pointerdown", "pointermove", "keydown", "scroll"] as const;
    const onActivity = () => session.noteActivity();
    for (const name of events) window.addEventListener(name, onActivity, { passive: true });
    return () => {
      for (const name of events) window.removeEventListener(name, onActivity);
    };
  }, [session]);

  return <ServicesContext.Provider value={services}>{children}</ServicesContext.Provider>;
}

export function useServices(): Services {
  const services = useContext(ServicesContext);
  if (!services) throw new Error("useServices must be used inside <ServicesProvider>.");
  return services;
}

export function useSessionState(): SessionState {
  const { session } = useServices();
  return useSyncExternalStore(session.subscribe, session.getSnapshot);
}
