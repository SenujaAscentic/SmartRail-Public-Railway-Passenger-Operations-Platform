import { useEffect, useState } from "react";

// The time the page counts ages from, refreshed every second ("updated 30 s ago" keeps counting). Ages are shown, never compared
// with a device's own clock for any decision: the server decides what is stale.
export function useNow(intervalMs = 1000): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(timer);
  }, [intervalMs]);
  return now;
}
