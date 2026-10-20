import { useEffect, useState } from "react";
import { LiveIndicator } from "../components/LiveIndicator.tsx";
import { Message } from "../components/Message.tsx";
import { readMainLineId } from "../config.ts";
import { formatColombo } from "../format.ts";
import { useServices } from "../services-context.tsx";
import { useLiveStream } from "../stream/useLiveStream.ts";

// A bare live view: it connects to the event stream for the Main Line and lists what arrives. It does not interpret the
// events yet (the dashboard comes later); it proves the connection, the reconnect and the resync.
// The Main Line is the first line of GET /lines (there is one line); VITE_MAIN_LINE_ID is an optional override.
export function LivePage() {
  const setting = readMainLineId();
  const { api } = useServices();
  const [fetched, setFetched] = useState<{ lineId: string } | "failed" | null>(null);

  useEffect(() => {
    if (setting.kind !== "missing") return;
    let cancelled = false;
    api
      .GET("/api/v1/lines")
      .then(({ data }) => {
        if (!cancelled) setFetched(data && data[0] ? { lineId: data[0].lineId } : "failed");
      })
      .catch(() => {
        if (!cancelled) setFetched("failed");
      });
    return () => {
      cancelled = true;
    };
  }, [setting.kind, api]);

  return (
    <>
      <h1>Live events</h1>
      {setting.kind === "ok" && <LiveView lineId={setting.lineId} />}
      {setting.kind === "missing" && fetched === null && <p>Loading the line…</p>}
      {setting.kind === "missing" && fetched === "failed" && <Message kind="error">The Main Line could not be loaded from the server, so there is nothing to connect to.</Message>}
      {setting.kind === "missing" && fetched !== null && fetched !== "failed" && <LiveView lineId={fetched.lineId} />}
      {setting.kind === "invalid" && <Message kind="error">VITE_MAIN_LINE_ID is not a valid id (a GUID such as 5d1c8e0a-2b47-4f6a-9c33-71a0e4d8b2f6). Remove it to use the line the server reports.</Message>}
    </>
  );
}

function LiveView({ lineId }: { lineId: string }) {
  const state = useLiveStream(lineId);
  const latest = state.events[0];

  return (
    <>
      <LiveIndicator connection={state.connection} />
      {state.connection === "failed" && state.detail && <Message kind="error">{state.detail}</Message>}
      {state.resyncs > 0 && (
        <Message kind="notice">Some updates were missed, so the view was reloaded from a fresh snapshot.</Message>
      )}

      <p>
        Received {state.received} {state.received === 1 ? "message" : "messages"} ({state.snapshots} {state.snapshots === 1 ? "snapshot" : "snapshots"}).
      </p>

      {state.events.length === 0 ? (
        <p>No events yet.</p>
      ) : (
        <table>
          <caption>Most recent events, newest first</caption>
          <thead>
            <tr>
              <th scope="col">Sequence</th>
              <th scope="col">Event</th>
              <th scope="col">Occurred (Sri Lanka time)</th>
              <th scope="col">Journey</th>
            </tr>
          </thead>
          <tbody>
            {state.events.map((event) => (
              <tr key={event.key}>
                <td>{event.sequence ?? "—"}</td>
                <td>{event.name}</td>
                <td>{formatColombo(event.occurredAt)}</td>
                <td>{event.journeyId ? `${event.journeyId.slice(0, 8)}…` : "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {latest && (
        <details>
          <summary>Data of the latest event</summary>
          {/* React shows this as text, never as HTML: what a note or announcement says cannot become markup (CLAUDE.md section 6). */}
          <pre>{latest.raw}</pre>
        </details>
      )}
    </>
  );
}
