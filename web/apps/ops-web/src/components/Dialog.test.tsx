import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import { Dialog } from "./Dialog.tsx";

function Page() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button onClick={() => setOpen(true)}>Open</button>
      <button>Behind the dialog</button>
      {open && (
        <Dialog title="Cancel this journey?" onClose={() => setOpen(false)}>
          <select aria-label="Reason"><option>Weather</option></select>
          <button>Confirm</button>
          <button disabled>Not yet</button>
          <button onClick={() => setOpen(false)}>Back</button>
        </Dialog>
      )}
    </>
  );
}

describe("Dialog focus", () => {
  it("keeps Tab inside the dialog, wrapping at both ends, and skips disabled controls", async () => {
    const user = userEvent.setup();
    render(<Page />);
    await user.click(screen.getByRole("button", { name: "Open" }));

    await user.tab();   // the dialog itself has focus: Tab goes to the first control
    expect(document.activeElement).toBe(screen.getByLabelText("Reason"));
    await user.tab();
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Confirm" }));
    await user.tab();   // "Not yet" is disabled
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Back" }));
    await user.tab();   // wraps to the first, never to the page behind
    expect(document.activeElement).toBe(screen.getByLabelText("Reason"));
    await user.tab({ shift: true });   // and backwards from the first to the last
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Back" }));
  });

  it("closes on Escape and gives the focus back to the button that opened it", async () => {
    const user = userEvent.setup();
    render(<Page />);
    const opener = screen.getByRole("button", { name: "Open" });
    await user.click(opener);
    expect(screen.getByRole("dialog")).toBeTruthy();

    await user.keyboard("{Escape}");

    expect(screen.queryByRole("dialog")).toBeNull();
    expect(document.activeElement).toBe(opener);
  });

  it("gives the focus back to the opener when it is closed by a button inside", async () => {
    const user = userEvent.setup();
    render(<Page />);
    const opener = screen.getByRole("button", { name: "Open" });
    await user.click(opener);

    await user.click(screen.getByRole("button", { name: "Back" }));

    expect(document.activeElement).toBe(opener);
  });
});
