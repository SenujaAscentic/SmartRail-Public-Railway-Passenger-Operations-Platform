import { ConnectionBadge } from "../components/ConnectionBadge.tsx";
import { Message } from "../components/Message.tsx";
import { readMainLineId } from "../config.ts";
import { formatColombo } from "../format.ts";
import { useLiveStream } from "../stream/useLiveStream.ts";

// A bare live view: it connects to the event stream for the Main Line and lists what arrives. It does not interpret the
// events yet (the dashboard comes later); it proves the connection, the reconnect and the resync.
export function LivePage() {
  const line = readMainLineId();

  return (
    <>
      <h1>Live events</h1>
      {line.kind === "ok" && <LiveView lineId={line.lineId} />}
      {line.kind === "missing" && (
        <Message kind="error">
          The Main Line id is not set. For development, copy apps/ops-web/.env.example to .env.local, set VITE_MAIN_LINE_ID, and restart the dev server.
        </Message>
      )}
      {line.kind === "invalid" && <Message kind="error">VITE_MAIN_LINE_ID is not a valid id (a GUID such as 5d1c8e0a-2b47-4f6a-9c33-71a0e4d8b2f6).</Message>}
    </>
  );
}

function LiveView({ lineId }: { lineId: string }) {
  const state = useLiveStream(lineId);
  const latest = state.events[0];

  return (
    <>
      <ConnectionBadge connection={state.connection} />
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
