import { connectStationStream } from "./stream.ts";

// How the display stays up to date (US-D4, edge cases E-32 and E-33), the same design as the passenger app:
//  - the data is loaded once, and again after ANY stream event (debounced), after every (re)connect, and by a poll every
//    30 s while the stream is down. The display never works out a change itself: it reads the board again.
//  - "in contact" means: a load worked, or bytes (an event or a heartbeat) arrived. Without contact for 2 minutes the display
//    is OFFLINE: the pages say so and show scheduled times only.
export const OFFLINE_AFTER_MS = 2 * 60 * 1000;
export const POLL_MS = 30_000;
export const REFETCH_DELAY_MS = 300;

export type Connection = "connecting" | "live" | "reconnecting";

export interface FeedState<T> {
  // The last data that was loaded; null until the first load worked.
  data: T | null;
  connection: Connection;
  // No contact for 2 minutes: nothing live is known.
  offline: boolean;
  // The last load failed (the old data is kept).
  loadFailed: boolean;
}

export interface FeedOptions<T> {
  stationId: string;
  // Loads the data (the board, and for the platform display the journey of the next train).
  load: () => Promise<T>;
  streamFetch?: typeof globalThis.fetch;
  now?: () => number;
  // For tests.
  streamRetryDelayMs?: (attempt: number) => number;
  streamSilenceMs?: number;
}

export class LiveFeed<T> {
  private state: FeedState<T> = { data: null, connection: "connecting", offline: false, loadFailed: false };
  private readonly listeners = new Set<() => void>();
  private readonly now: () => number;

  private controller: AbortController | null = null;
  private lastContact = 0;
  private loadId = 0;
  private refetchTimer: ReturnType<typeof setTimeout> | undefined;
  private pollTimer: ReturnType<typeof setInterval> | undefined;
  private offlineTimer: ReturnType<typeof setInterval> | undefined;

  private readonly options: FeedOptions<T>;

  constructor(options: FeedOptions<T>) {
    this.options = options;
    this.now = options.now ?? (() => Date.now());
  }

  // For useSyncExternalStore.
  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };

  getSnapshot = (): FeedState<T> => this.state;

  start(): void {
    if (this.controller) return;
    const controller = new AbortController();
    this.controller = controller;
    this.lastContact = this.now();
    this.state = { data: this.state.data, connection: "connecting", offline: false, loadFailed: false };

    // Every second: has the contact been lost for too long?
    this.offlineTimer = setInterval(() => this.checkOffline(), 1000);
    void this.refetch();

    void connectStationStream({
      stationId: this.options.stationId,
      signal: controller.signal,
      ...(this.options.streamFetch ? { fetch: this.options.streamFetch } : {}),
      ...(this.options.streamRetryDelayMs ? { retryDelayMs: this.options.streamRetryDelayMs } : {}),
      ...(this.options.streamSilenceMs !== undefined ? { silenceMs: this.options.streamSilenceMs } : {}),
      onOpen: () => {
        if (controller.signal.aborted) return;
        this.setConnection("live");
        // The stream is back: the poll is not needed, and whatever happened meanwhile is read in full (E-33).
        this.stopPolling();
        void this.refetch();
      },
      onActivity: () => {
        if (!controller.signal.aborted) this.touch();
      },
      onFrame: () => {
        // ANY event: read the data again, once for a burst of events.
        if (!controller.signal.aborted) this.scheduleRefetch();
      },
      onClosed: () => {
        if (controller.signal.aborted) return;
        this.setConnection("reconnecting");
        this.startPolling();
      },
    });
  }

  stop(): void {
    this.controller?.abort();
    this.controller = null;
    clearTimeout(this.refetchTimer);
    this.stopPolling();
    clearInterval(this.offlineTimer);
    this.offlineTimer = undefined;
  }

  // ---- loading ----

  private scheduleRefetch(): void {
    clearTimeout(this.refetchTimer);
    this.refetchTimer = setTimeout(() => void this.refetch(), REFETCH_DELAY_MS);
  }

  private async refetch(): Promise<void> {
    const id = ++this.loadId;
    const controller = this.controller;
    try {
      const data = await this.options.load();
      if (id !== this.loadId || !controller || controller.signal.aborted) return; // an older answer, or the page left
      this.lastContact = this.now();
      this.update({ data, loadFailed: false, offline: false });
    } catch {
      if (id !== this.loadId || !controller || controller.signal.aborted) return;
      this.update({ loadFailed: true });
    }
  }

  private startPolling(): void {
    // Started once: a stream that keeps failing must not push the next poll back.
    if (this.pollTimer !== undefined) return;
    this.pollTimer = setInterval(() => void this.refetch(), POLL_MS);
  }

  private stopPolling(): void {
    clearInterval(this.pollTimer);
    this.pollTimer = undefined;
  }

  // ---- contact and offline ----

  private touch(): void {
    this.lastContact = this.now();
    if (this.state.offline) this.update({ offline: false });
  }

  private checkOffline(): void {
    const offline = this.now() - this.lastContact >= OFFLINE_AFTER_MS;
    if (offline !== this.state.offline) this.update({ offline });
  }

  // ---- state ----

  private setConnection(connection: Connection): void {
    if (connection !== this.state.connection) this.update({ connection });
  }

  private update(change: Partial<FeedState<T>>): void {
    this.state = { ...this.state, ...change };
    for (const listener of this.listeners) listener();
  }
}
