// Fake Server-Sent Events for tests: a Response whose body is event-stream text.
const encoder = new TextEncoder();

export type Responder = (init: RequestInit) => Response | Promise<Response>;

// A stream that sends `text`. With `hold` it stays open until the client aborts (like a real, quiet stream).
export function sse(text: string, options: { hold?: boolean } = {}): Responder {
  return (init) => {
    const body = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(encoder.encode(text));
        if (!options.hold) {
          controller.close();
        } else {
          init.signal?.addEventListener("abort", () => controller.error(new DOMException("Aborted", "AbortError")));
        }
      },
    });
    return new Response(body, { status: 200, headers: { "content-type": "text/event-stream" } });
  };
}

// One SSE message: optional `id:`, `event:` and a one-line JSON `data:`, then a blank line.
export const frame = (name: string, data: unknown, id?: number): string =>
  `${id === undefined ? "" : `id: ${id}\n`}event: ${name}\ndata: ${JSON.stringify(data)}\n\n`;

// A stream that stays open and can be given more messages later: a stream event arriving while a dialog is open.
export function openSse(first: string): { responder: Responder; push: (text: string) => void } {
  let controller: ReadableStreamDefaultController<Uint8Array> | null = null;
  const responder: Responder = (init) => {
    const body = new ReadableStream<Uint8Array>({
      start(c) {
        controller = c;
        c.enqueue(encoder.encode(first));
        init.signal?.addEventListener("abort", () => c.error(new DOMException("Aborted", "AbortError")));
      },
    });
    return new Response(body, { status: 200, headers: { "content-type": "text/event-stream" } });
  };
  return { responder, push: (text) => controller?.enqueue(encoder.encode(text)) };
}
