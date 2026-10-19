import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

// Remove what a test rendered and any fake timers or stubs it set, so the next test starts clean.
afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllEnvs();
});
