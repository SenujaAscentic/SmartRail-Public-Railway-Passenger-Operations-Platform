import { useCallback, useState } from "react";
import type { Detail } from "./journey-store.ts";

// What a controller's decision answers: the new journey (200), or a problem (Problem Details with a stable `code`, contract section 6).
interface Answer {
  data?: Detail | undefined;
  error?: unknown;
  response: Response;
}

export interface Problem {
  code: string | null;
  text: string;                          // for the person: the server's detail, or its title
  fields: Record<string, string[]>;      // field errors of a 400, by field name
  conflicts: unknown[];                  // the platform conflicts of a 422 CONFLICT_NOT_ACKNOWLEDGED
}

// 409 VERSION_CONFLICT: somebody else decided first (US-O4 AC6). `detail` says who and how long ago; `journey` is the journey as it is now.
export interface Conflict {
  text: string;
  journey: Detail | null;
}

export interface ActionState {
  busy: boolean;
  problem: Problem | null;
  conflict: Conflict | null;
  done: boolean;
  clear: () => void;
  // Runs one decision. Resolves true when it worked (the new journey was handed to `onDone`).
  run: (call: () => Promise<Answer>) => Promise<boolean>;
}

const isObject = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null && !Array.isArray(value);

export function toProblem(error: unknown, status: number): Problem {
  const body = isObject(error) ? error : {};
  const fields: Record<string, string[]> = {};
  if (isObject(body.errors)) {
    for (const [name, messages] of Object.entries(body.errors)) if (Array.isArray(messages)) fields[name] = messages.map(String);
  }
  const text = typeof body.detail === "string" && body.detail !== "" ? body.detail : typeof body.title === "string" ? body.title : `The server answered ${status}.`;
  return {
    code: typeof body.code === "string" ? body.code : null,
    text,
    fields,
    conflicts: Array.isArray(body.conflicts) ? body.conflicts : [],
  };
}

// The same handling for every decision: busy while it runs, the new journey on success, the conflict message on 409 VERSION_CONFLICT,
// and the field errors or the server's words on any other problem. Nothing is retried: a decision is made once, by a person.
export function useAction(onDone: (detail: Detail) => void): ActionState {
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [conflict, setConflict] = useState<Conflict | null>(null);
  const [done, setDone] = useState(false);

  const clear = useCallback(() => {
    setProblem(null);
    setConflict(null);
    setDone(false);
  }, []);

  const run = useCallback(
    async (call: () => Promise<Answer>) => {
      setBusy(true);
      clear();
      try {
        const { data, error, response } = await call();
        if (response.ok && data) {
          onDone(data);
          setDone(true);
          return true;
        }
        const found = toProblem(error, response.status);
        if (response.status === 409 && found.code === "VERSION_CONFLICT") {
          const journey = isObject(error) && isObject(error.journey) ? (error.journey as unknown as Detail) : null;
          setConflict({ text: found.text, journey });
        } else {
          setProblem(found);
        }
        return false;
      } catch {
        setProblem({ code: null, text: "The server could not be reached. Nothing was changed.", fields: {}, conflicts: [] });
        return false;
      } finally {
        setBusy(false);
      }
    },
    [onDone, clear],
  );

  return { busy, problem, conflict, done, clear, run };
}
