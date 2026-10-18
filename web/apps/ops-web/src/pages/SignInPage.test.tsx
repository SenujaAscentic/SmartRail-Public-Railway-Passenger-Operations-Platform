import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { MINUTE, SIGN_IN, fakeBackend, json, renderApp } from "../test/fake-server.tsx";

// The sign-in screen: an accessible form (labels, errors as text, status never by colour alone) and the messages of US-O1.
const GENERIC = "The staff number or password is not correct.";

async function openSignIn(overrides = {}) {
  const backend = fakeBackend(overrides);
  const view = renderApp(backend.services, "/sign-in");
  await screen.findByRole("button", { name: "Sign in" });   // restore() has finished: nobody is signed in
  return { ...backend, ...view, user: userEvent.setup() };
}

async function fillAndSubmit(user: ReturnType<typeof userEvent.setup>, staffNumber: string, password: string) {
  if (staffNumber) await user.type(screen.getByLabelText("Staff number"), staffNumber);
  if (password) await user.type(screen.getByLabelText("Password"), password);
  await user.click(screen.getByRole("button", { name: "Sign in" }));
}

describe("the form", () => {
  it("has one h1, labelled fields, and the right autocomplete hints for a password manager", async () => {
    await openSignIn();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    const staff = screen.getByLabelText("Staff number");
    const password = screen.getByLabelText("Password");
    expect(staff.getAttribute("autocomplete")).toBe("username");
    expect(password.getAttribute("type")).toBe("password");
    expect(password.getAttribute("autocomplete")).toBe("current-password");
  });

  it("asks for a missing staff number and password in text next to the fields, and sends nothing", async () => {
    const { user, calls } = await openSignIn();

    await fillAndSubmit(user, "", "");

    const staff = screen.getByLabelText("Staff number");
    const password = screen.getByLabelText("Password");
    expect(staff.getAttribute("aria-invalid")).toBe("true");
    expect(password.getAttribute("aria-invalid")).toBe("true");
    // The message is tied to its field, so a screen reader reads it with the field.
    expect(document.getElementById(staff.getAttribute("aria-describedby")!)?.textContent).toContain("Enter your staff number.");
    expect(document.getElementById(password.getAttribute("aria-describedby")!)?.textContent).toContain("Enter your password.");
    expect(document.activeElement).toBe(staff);   // keyboard users land on the first problem
    expect(calls(SIGN_IN)).toHaveLength(0);
  });

  it("shows the field messages of a 400 from the server", async () => {
    const { user } = await openSignIn({
      [SIGN_IN]: () => json(400, { code: "VALIDATION_FAILED", title: "x", status: 400, requestId: "r", errors: { password: ["The password can be at most 200 characters."] } }),
    });

    await fillAndSubmit(user, "CTRL-01", "x");

    expect(await screen.findByText(/at most 200 characters/)).toBeTruthy();
  });
});

describe("when sign-in fails", () => {
  it("says the SAME generic thing for any wrong credential, keeps the staff number and clears the password", async () => {
    const { user } = await openSignIn({ [SIGN_IN]: () => json(401, { code: "UNAUTHENTICATED", title: "Sign-in failed", status: 401, requestId: "r" }) });

    await fillAndSubmit(user, "CTRL-01", "wrong-password");

    expect((await screen.findByRole("alert")).textContent).toContain(GENERIC);
    expect((screen.getByLabelText("Staff number") as HTMLInputElement).value).toBe("CTRL-01");
    expect((screen.getByLabelText("Password") as HTMLInputElement).value).toBe("");
    expect(document.activeElement).toBe(screen.getByLabelText("Password"));
  });

  it("shows a lock as its OWN message with the wait, not the generic one", async () => {
    const { user } = await openSignIn({
      [SIGN_IN]: () => json(429, { code: "SIGN_IN_LOCKED", title: "Sign-in is locked", status: 429, requestId: "r" }, { "retry-after": "840" }),
    });

    await fillAndSubmit(user, "SM-KDT", "x");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("This account is locked");
    expect(alert.textContent).toContain("14 minutes");
    expect(alert.textContent).not.toContain(GENERIC);
  });

  it("says 'minute' for one minute", async () => {
    const { user } = await openSignIn({
      [SIGN_IN]: () => json(429, { code: "SIGN_IN_LOCKED", title: "locked", status: 429, requestId: "r" }, { "retry-after": "30" }),
    });

    await fillAndSubmit(user, "SM-KDT", "x");

    expect((await screen.findByRole("alert")).textContent).toContain("1 minute.");
  });

  it("says when the server cannot be reached, and when it fails", async () => {
    const { user, routes } = await openSignIn({
      [SIGN_IN]: () => {
        throw new TypeError("Failed to fetch");
      },
    });
    await fillAndSubmit(user, "CTRL-01", "pw");
    expect((await screen.findByRole("alert")).textContent).toContain("Cannot reach the server");

    routes[SIGN_IN] = () => json(500, { code: "ERROR", title: "x", status: 500, requestId: "r" });
    await user.type(screen.getByLabelText("Password"), "pw");   // the page cleared it after the first failure
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    await waitFor(() => expect(screen.getByRole("alert").textContent).toContain("went wrong on our side"));
  });

  it("gives every message an icon AND words: status is never colour alone", async () => {
    const { user } = await openSignIn({ [SIGN_IN]: () => json(401, { code: "UNAUTHENTICATED", title: "x", status: 401, requestId: "r" }) });

    await fillAndSubmit(user, "CTRL-01", "wrong");

    const alert = await screen.findByRole("alert");
    expect(alert.querySelector("svg[aria-hidden='true']")).toBeTruthy();   // the icon is decoration for screen readers
    expect(alert.textContent).toContain("Error:");                          // and the words say what it is
  });

  it("disables the button and says 'Signing in…' while the request is running", async () => {
    let finish!: (response: Response) => void;
    const { user } = await openSignIn({ [SIGN_IN]: () => new Promise<Response>((resolve) => (finish = resolve)) });

    await fillAndSubmit(user, "CTRL-01", "pw");

    const button = await screen.findByRole("button", { name: "Signing in…" });
    expect((button as HTMLButtonElement).disabled).toBe(true);
    finish(json(401, { code: "UNAUTHENTICATED", title: "x", status: 401, requestId: "r" }));
    await screen.findByRole("button", { name: "Sign in" });
  });
});

describe("a session that ended by itself", () => {
  it("tells the person why when they were away for more than 30 minutes", async () => {
    const backend = fakeBackend();
    await backend.services.session.signIn("CTRL-01", "pw");
    backend.clock.now += 31 * MINUTE;   // away: no activity
    await backend.services.session.getAccessToken();   // something needed a token: the session ends as idle

    renderApp(backend.services, "/sign-in");

    expect((await screen.findByRole("status")).textContent).toContain("away for more than 30 minutes");
  });

  it("tells the person when the server ended the session", async () => {
    const backend = fakeBackend({ [REFRESH_KEY]: () => json(401, { code: "UNAUTHENTICATED", title: "x", status: 401, requestId: "r" }) });
    await backend.services.session.signIn("CTRL-01", "pw");
    backend.clock.now += 20 * MINUTE;
    backend.services.session.noteActivity();   // active, so the refresh is tried; the server refuses
    await backend.services.session.getAccessToken();

    renderApp(backend.services, "/sign-in");

    expect((await screen.findByRole("status")).textContent).toContain("Your session has ended");
  });
});

const REFRESH_KEY = "POST /api/v1/auth/refresh";
