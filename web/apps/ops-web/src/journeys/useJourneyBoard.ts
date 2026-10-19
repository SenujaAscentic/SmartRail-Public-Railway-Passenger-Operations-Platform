import { useCallback, useEffect, useReducer, useState } from "react";
import type { components } from "@smartrail/api-client";
import { useServices } from "../services-context.tsx";
import { connectStream, type StreamConnection } from "../stream/stream-client.ts";
import { emptyStore, storeReducer, type Detail, type Store } from "./journey-store.ts";

export type Line = components["schemas"]["LineResponse"];

export interface Board {
  line: Line | null;
  lineError: string | null;
  store: Store;
  connection: StreamConnection;
  connectionDetail: string | undefined;
  // Puts a journey the API just answered with (an action's result, or the journey inside a 409) into the screens.
  applyDetail: (detail: Detail) => void;
}

// What every ops screen stands on: the Main Line, and the staff event stream for it. A snapshot fills the store with every journey of
// today; every later event keeps it current (see journey-store.ts). One connection per screen that uses it, closed when the person leaves.
export function useJourneyBoard(): Board {
  const { api, session, streamFetch } = useServices();
  const [store, dispatch] = useReducer(storeReducer, emptyStore);
  const [line, setLine] = useState<Line | null>(null);
  const [lineError, setLineError] = useState<string | null>(null);
  const [connection, setConnection] = useState<StreamConnection>("connecting");
  const [connectionDetail, setConnectionDetail] = useState<string | undefined>(undefined);

  // The line comes from GET /lines (public): the first line is the Main Line while there is only one.
  useEffect(() => {
    let cancelled = false;
    api
      .GET("/api/v1/lines")
      .then(({ data }) => {
        if (cancelled) return;
        if (data && data[0]) setLine(data[0]);
        else setLineError("The line could not be loaded.");
      })
      .catch(() => {
        if (!cancelled) setLineError("The line could not be loaded.");
      });
    return () => {
      cancelled = true;
    };
  }, [api]);

  const lineId = line?.lineId;
  useEffect(() => {
    if (!lineId) return;
    const controller = new AbortController();
    void connectStream({
      lineIds: [lineId],
      getToken: () => session.getAccessToken(),
      signal: controller.signal,
      fetch: streamFetch,
      onFrame: (frame) => {
        if (!controller.signal.aborted) dispatch({ type: "frame", frame });
      },
      onConnection: (state, detail) => {
        if (controller.signal.aborted) return;
        setConnection(state);
        setConnectionDetail(detail);
      },
    });
    return () => controller.abort();
  }, [lineId, session, streamFetch]);

  // A journey an event changed is read again from the API: the backend's answer is the truth (the screens do not recalculate).
  const stale = Object.values(store.journeys).filter((entry) => entry.needsRefresh).map((entry) => entry.detail.journeyId);
  const staleKey = stale.join(",");
  useEffect(() => {
    for (const journeyId of staleKey === "" ? [] : staleKey.split(",")) {
      dispatch({ type: "refreshing", journeyId });
      api
        .GET("/api/v1/journeys/{journeyId}", { params: { path: { journeyId } } })
        .then(({ data }) => {
          if (data) dispatch({ type: "detail", detail: data });
        })
        .catch(() => {
          // The next event will ask again; the screen keeps what it has.
        });
    }
  }, [staleKey, api]);

  const applyDetail = useCallback((detail: Detail) => dispatch({ type: "detail", detail }), []);

  return { line, lineError, store, connection, connectionDetail, applyDetail };
}
