import { useEffect, useReducer } from "react";
import { useServices } from "../services-context.tsx";
import { initialLiveState, liveReducer, type LiveState } from "./live-events.ts";
import { connectStream } from "./stream-client.ts";

// Opens the live connection while the page is shown and closes it when the person leaves. Everything that arrives
// goes through the reducer, which keeps what the page shows.
export function useLiveStream(lineId: string): LiveState {
  const { session, streamFetch } = useServices();
  const [state, dispatch] = useReducer(liveReducer, initialLiveState);

  useEffect(() => {
    const controller = new AbortController();

    // Whatever an already-closed connection still reports (React's development double-run, or leaving the page) is ignored.
    void connectStream({
      lineIds: [lineId],
      getToken: () => session.getAccessToken(),   // refreshes the token first if it is about to expire (see Session)
      signal: controller.signal,
      fetch: streamFetch,
      onFrame: (frame) => {
        if (!controller.signal.aborted) dispatch({ type: "frame", frame });
      },
      onConnection: (connection, detail) => {
        if (!controller.signal.aborted) dispatch({ type: "connection", state: connection, detail });
      },
    });

    return () => controller.abort();
  }, [lineId, session, streamFetch]);

  return state;
}
