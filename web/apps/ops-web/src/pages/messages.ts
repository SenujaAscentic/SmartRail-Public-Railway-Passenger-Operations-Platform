import type { EndedReason, SignInResult } from "../session/session.ts";

// The words of the sign-in screen, in one place. Failures are deliberately vague about WHICH credential was wrong
// (contract section 6: sign-in failures give one generic message); a lock is the one thing the person needs to be told
// separately, because retrying will not help.
export const GENERIC_SIGN_IN_ERROR = "The staff number or password is not correct.";

type Problem = Exclude<SignInResult, { kind: "ok" } | { kind: "validation" }>;

export function signInProblemText(result: Problem): string {
  switch (result.kind) {
    case "invalid":
      return GENERIC_SIGN_IN_ERROR;
    case "locked": {
      const minutes = result.retryAfterMinutes;
      return `Too many failed attempts. This account is locked. Try again in about ${minutes} ${minutes === 1 ? "minute" : "minutes"}.`;
    }
    case "network":
      return "Cannot reach the server. Check your connection and try again.";
    case "server":
      return "Something went wrong on our side. Try again in a moment.";
  }
}

export function endedSessionText(reason: EndedReason): string {
  return reason === "idle"
    ? "You were signed out because you were away for more than 30 minutes. Sign in again."
    : "Your session has ended. Sign in again.";
}
