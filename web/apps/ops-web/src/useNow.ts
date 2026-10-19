import { useEffect, useState } from "react";
import { useServices } from "./services-context.tsx";

// The time the page counts ages from, refreshed every second ("updated 30 s ago" keeps counting). It is the SERVER's time (see
// ServerClock), so a wrong device clock or the demo clock does not break the ages. Ages are shown, never used for a decision: the
// server decides what is stale.
export function useNow(intervalMs = 1000): number {
  const { clock } = useServices();
  const [now, setNow] = useState(() => clock.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(clock.now()), intervalMs);
    return () => clearInterval(timer);
  }, [intervalMs, clock]);
  return now;
}
