import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { ME, REFRESH, SIGN_IN, SIGN_OUT, fakeBackend, renderApp } from "./test/fake-server.tsx";

describe("the app frame", () => {
  it("has a skip link, a banner, one main landmark and exactly one h1", async () => {
    const { services } = fakeBackend();
    renderApp(services, "/sign-in");
    await screen.findByRole("button", { name: "Sign in" });

    expect(screen.getByRole("link", { name: "Skip to main content" })).toHaveProperty("hash", "#main");
    expect(screen.getByRole("banner")).toBeTruthy();
    expect(screen.getAllByRole("main")).toHaveLength(1);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });
});

describe("signing in and out", () => {
  it("sends a person who is not signed in to the sign-in page", async () => {
    const { services } = fakeBackend();

    renderApp(services, "/");

    expect(await screen.findByRole("heading", { level: 1, name: "Staff sign-in" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Sign out" })).toBeNull();
  });

  it("lands on the dashboard after signing in, shows who is signed in, and sends the Bearer token", async () => {
    const { services, calls } = fakeBackend();
    renderApp(services, "/sign-in");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Staff number"), "CTRL-01");
    await user.type(screen.getByLabelText("Password"), "pw");

    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("Demo Controller")).toBeTruthy();
    expect((await screen.findByRole("heading", { level: 1 })).textContent).toContain("Dashboard");
    expect(calls(SIGN_IN)).toHaveLength(1);
    await waitFor(() => expect(calls(ME)[0]?.headers.get("authorization")).toBe("Bearer token-1"));
  });

  it("restores a session from the refresh cookie on page load, without showing the sign-in page", async () => {
    const backend = fakeBackend();
    backend.routes[REFRESH] = () => backend.tokenResponse("token-from-cookie");

    renderApp(backend.services, "/");

    expect(await screen.findByText("Demo Controller")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Staff sign-in" })).toBeNull();
    await waitFor(() => expect(backend.calls(ME)[0]?.headers.get("authorization")).toBe("Bearer token-from-cookie"));
  });

  it("signs out: revokes the session on the server and returns to the sign-in page", async () => {
    const backend = fakeBackend();
    backend.routes[REFRESH] = () => backend.tokenResponse("t");
    renderApp(backend.services, "/");
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Sign out" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Staff sign-in" })).toBeTruthy();
    expect(backend.calls(SIGN_OUT)).toHaveLength(1);
    expect(screen.queryByText(/session has ended/)).toBeNull();   // signing out yourself needs no explanation
  });
});
