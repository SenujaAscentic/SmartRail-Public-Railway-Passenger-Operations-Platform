import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

// Remove what a test rendered and any environment setting a test changed, so the next test starts clean.
afterEach(() => {
  cleanup();
  vi.unstubAllEnvs();
});
