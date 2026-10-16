import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { describe, expect, it } from "vitest";
import { App } from "./App.tsx";

describe("the app frame", () => {
  it("has a skip link, a banner, one main landmark and exactly one h1", () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );

    expect(screen.getByRole("link", { name: "Skip to main content" })).toHaveProperty("hash", "#main");
    expect(screen.getByRole("banner")).toBeTruthy();
    expect(screen.getAllByRole("main")).toHaveLength(1);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });
});
