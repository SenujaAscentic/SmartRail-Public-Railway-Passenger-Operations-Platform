import { useId, useRef, useState, type FormEvent } from "react";
import { Navigate } from "react-router";
import { Icon } from "../components/Icon.tsx";
import { Message } from "../components/Message.tsx";
import { useServices, useSessionState } from "../services-context.tsx";
import { endedSessionText, signInProblemText } from "./messages.ts";

type FieldErrors = Record<string, string[]>;

// A message tied to its field. Words and icon, never colour alone. It is not a live region on purpose: the summary
// message above the button is the one that is announced, so a screen reader does not read everything twice.
function FieldError({ id, messages }: { id: string; messages: string[] | undefined }) {
  if (!messages?.length) return null;
  return (
    <p className="field-error" id={id}>
      <Icon name="x-circle" />
      <span>
        <span className="visually-hidden">Error: </span>
        {messages.join(" ")}
      </span>
    </p>
  );
}

export function SignInPage() {
  const { session } = useServices();
  const state = useSessionState();

  const staffId = useId();
  const passwordId = useId();
  const staffRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);

  const [staffNumber, setStaffNumber] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  if (state.status === "signedIn") return <Navigate to="/" replace />;
  if (state.status === "checking") {
    return (
      <>
        <h1>Sign in</h1>
        <p role="status">Checking your session…</p>
      </>
    );
  }

  const focusFirstProblem = (errors: FieldErrors) => (errors.staffNumber ? staffRef : passwordRef).current?.focus();

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (submitting) return;

    // The same checks the server makes, so the person sees them at once. The server still checks (it never trusts this).
    const errors: FieldErrors = {};
    if (!staffNumber.trim()) errors.staffNumber = ["Enter your staff number."];
    if (!password) errors.password = ["Enter your password."];
    setProblem(null);
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      focusFirstProblem(errors);   // keyboard and screen-reader users land on the first problem
      return;
    }

    setSubmitting(true);
    const result = await session.signIn(staffNumber.trim(), password);
    setSubmitting(false);

    if (result.kind === "ok") return;   // the session changed: this page redirects itself

    if (result.kind === "validation") {
      setFieldErrors(result.errors);
      focusFirstProblem(result.errors);
      return;
    }

    setProblem(signInProblemText(result));
    setPassword("");   // a wrong password is not kept in the page
    passwordRef.current?.focus();
  }

  return (
    <>
      <h1>Sign in</h1>
      {state.endedReason && <Message kind="notice">{endedSessionText(state.endedReason)}</Message>}

      <form className="form" onSubmit={onSubmit} noValidate>
        {problem && <Message kind="error">{problem}</Message>}

        <div className="field">
          <label htmlFor={staffId}>Staff number</label>
          <input
            id={staffId}
            ref={staffRef}
            value={staffNumber}
            onChange={(event) => setStaffNumber(event.target.value)}
            autoComplete="username"
            autoCapitalize="none"
            spellCheck={false}
            aria-required="true"
            aria-invalid={fieldErrors.staffNumber ? "true" : undefined}
            aria-describedby={fieldErrors.staffNumber ? `${staffId}-error` : undefined}
          />
          <FieldError id={`${staffId}-error`} messages={fieldErrors.staffNumber} />
        </div>

        <div className="field">
          <label htmlFor={passwordId}>Password</label>
          <input
            id={passwordId}
            ref={passwordRef}
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="current-password"
            aria-required="true"
            aria-invalid={fieldErrors.password ? "true" : undefined}
            aria-describedby={fieldErrors.password ? `${passwordId}-error` : undefined}
          />
          <FieldError id={`${passwordId}-error`} messages={fieldErrors.password} />
        </div>

        <button type="submit" disabled={submitting} aria-busy={submitting}>
          {submitting ? "Signing in…" : "Sign in"}
        </button>
      </form>
    </>
  );
}
