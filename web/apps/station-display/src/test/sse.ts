// A fake event stream for tests: a Response whose body the test writes to, like a real, long-lived stream.
const encoder = new TextEncoder();

export interface FakeConnection {
  url: string;
  headers: Record<string, string>;
  // True while the page has not aborted it.
  open: boolean;
  controller: ReadableStreamDefaultController<Uint8Array>;
}

export function fakeStream() {
  const connections: FakeConnection[] = [];
  // While true the server answers 503 (the stream is down, but the rest of the API may work).
  const state = { refuse: false };

  const fetch = async (input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> => {
    if (state.refuse) return new Response("down", { status: 503 });
    let connection!: FakeConnection;
    const body = new ReadableStream<Uint8Array>({
      start(controller) {
        connection = { url: String(input), headers: { ...(init.headers as Record<string, string>) }, open: true, controller };
        init.signal?.addEventListener("abort", () => {
          connection.open = false;
          controller.error(new DOMException("Aborted", "AbortError"));
        });
      },
    });
    connections.push(connection);
    return new Response(body, { status: 200, headers: { "content-type": "text/event-stream" } });
  };

  const last = () => connections[connections.length - 1]!;
  const write = (text: string) => last().controller.enqueue(encoder.encode(text));

  return {
    fetch: fetch as typeof globalThis.fetch,
    connections,
    state,
    last,
    // One event, like the backend sends it.
    event: (name: string, data: unknown, id?: number) => write(`${id === undefined ? "" : `id: ${id}\n`}event: ${name}\ndata: ${JSON.stringify(data)}\n\n`),
    // The heartbeat comment line every 15 s.
    heartbeat: () => write(": keep-alive\n\n"),
    // The server ends the stream.
    close: () => {
      last().open = false;
      last().controller.close();
    },
  };
}
