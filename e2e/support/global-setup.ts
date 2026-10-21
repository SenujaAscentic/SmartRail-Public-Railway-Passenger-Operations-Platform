import { startStack, stopStack } from "./stack.ts";

// Starts the stack once; the function it returns stops it (and removes the scratch database) when the run ends, passed or failed.
export default async function globalSetup() {
  stopStack(); // leftovers of a run that was killed
  await startStack();
  return async () => stopStack();
}
