import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import { LiveFeed, type FeedState } from "./feed/live-feed.ts";
import { loadLines } from "./loaders.ts";
import { useServices } from "./services.tsx";
import type { LineInfo, StationRef } from "./types.ts";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// The station named in the address: its code (FOT, GPH, KDT) or its id. Found from the lines (one public call), which
// list every station with its id, code and name. The code is compared without regard to upper or lower case.
export function findStation(lines: LineInfo[], codeOrId: string): StationRef | null {
  const wanted = codeOrId.trim().toLowerCase();
  const byId = GUID.test(wanted);
  for (const line of lines) {
    for (const route of line.routes) {
      for (const s of route.stations) {
        if (byId ? s.stationId.toLowerCase() === wanted : s.code.toLowerCase() === wanted) {
          return { id: s.stationId, code: s.code, name: s.name };
        }
      }
    }
  }
  return null;
}

export type StationState =
  | { kind: "loading" }
  | { kind: "ready"; station: StationRef; lines: LineInfo[] }
  | { kind: "unknown" }
  | { kind: "failed" };

const RETRY_MS = 5000;

// Resolves the station of the address. If the lines cannot be loaded, it tries again every 5 s: a display that was switched
// on before the network was up must come right by itself.
export function useStation(codeOrId: string): StationState {
  const { api } = useServices();
  const [state, setState] = useState<StationState>({ kind: "loading" });

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const attempt = async () => {
      try {
        const lines = await loadLines(api);
        if (cancelled) return;
        const station = findStation(lines, codeOrId);
        setState(station ? { kind: "ready", station, lines } : { kind: "unknown" });
      } catch {
        if (cancelled) return;
        setState({ kind: "failed" });
        timer = setTimeout(attempt, RETRY_MS);
      }
    };

    setState({ kind: "loading" });
    void attempt();
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [api, codeOrId]);

  return state;
}

// Keeps [load] up to date while the page is shown (stream, re-fetch, poll, offline: feed/live-feed.ts).
export function useLiveFeed<T>(stationId: string, load: () => Promise<T>): FeedState<T> {
  const { streamFetch, now } = useServices();
  // The feed is made again only when the station or the loader changes (the loader is made with useCallback by the page).
  const feed = useMemo(() => new LiveFeed<T>({ stationId, load, streamFetch, now }), [stationId, load, streamFetch, now]);

  useEffect(() => {
    feed.start();
    return () => feed.stop();
  }, [feed]);

  return useSyncExternalStore(feed.subscribe, feed.getSnapshot);
}

// The SERVER's time now, in milliseconds, renewed every second: for the clock in the header and for removing trains by the
// clock when the display is offline. Not the device's own clock, which can be wrong.
export function useNow(): number {
  const { serverNow } = useServices();
  const [value, setValue] = useState(() => serverNow());
  useEffect(() => {
    const timer = setInterval(() => setValue(serverNow()), 1000);
    return () => clearInterval(timer);
  }, [serverNow]);
  return value;
}
