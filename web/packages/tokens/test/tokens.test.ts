import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { contrastRatio } from "../src/contrast.ts";
import type { ThemeName, TokensFile } from "../src/tokens-file.ts";
import { tokensToCss } from "../src/tokens-to-css.ts";

// The real shared file, so these tests also guard the file itself.
const tokens = JSON.parse(readFileSync(resolve(import.meta.dirname, "../../../../shared/tokens.json"), "utf8")) as TokensFile;
const css = tokensToCss(tokens, "ops");

// The text of one block of the CSS, e.g. the dark-theme media query.
const darkBlock = css.slice(css.indexOf("@media (prefers-color-scheme: dark)"), css.indexOf("@media (prefers-reduced-motion"));
const lightBlock = css.slice(0, css.indexOf("@media (prefers-color-scheme: dark)"));

describe("tokensToCss", () => {
  it("puts the light colours in :root and the dark colours in the dark media query", () => {
    expect(lightBlock).toContain("--color-background: #FFFFFF;");
    expect(darkBlock).toContain("--color-background: #0B1A2B;");
    expect(lightBlock).not.toContain("#0B1A2B");
  });

  it("writes kebab-case names", () => {
    expect(lightBlock).toContain("--color-text-muted:");
    expect(lightBlock).toContain("--status-platform-changed-fg:");
    expect(lightBlock).toContain("--status-on-time-bg:");
  });

  it("gives every status a foreground and a background in both themes", () => {
    for (const status of Object.keys(tokens.status)) {
      const name = status.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
      for (const block of [lightBlock, darkBlock]) {
        expect(block).toContain(`--status-${name}-fg:`);
        expect(block).toContain(`--status-${name}-bg:`);
      }
    }
  });

  it("uses the ops type sizes, and the display sizes for the display surface", () => {
    expect(lightBlock).toContain("--font-size-body: 14px;");
    expect(lightBlock).toContain("--touch-target: 32px;");
    const display = tokensToCss(tokens, "display");
    expect(display).toContain("--font-size-body: 48px;");
    expect(display).not.toContain("--touch-target");   // displays are not touched
  });

  it("names the font without the explanation in brackets and adds fallbacks (no font package)", () => {
    expect(lightBlock).toContain('--font-family: "Noto Sans", system-ui, sans-serif;');
    expect(lightBlock).toContain('--font-family-mono: "Noto Sans Mono", ui-monospace, monospace;');
  });

  it("turns animation off for people who ask for reduced motion", () => {
    expect(css).toContain("@media (prefers-reduced-motion: reduce)");
    expect(css).toContain("--motion-duration: 0ms;");
    expect(lightBlock).toContain("--motion-duration: 150ms;");
  });

  it("refuses a surface that does not exist", () => {
    expect(() => tokensToCss(tokens, "billboard")).toThrow(/Unknown surface "billboard"/);
  });
});

// tokens.json promises "WCAG 2.2 AA (4.5:1 for text)" in both themes. If someone edits a colour, this fails.
describe("shared/tokens.json contrast", () => {
  const themes: ThemeName[] = ["light", "dark"];

  it.each(themes)("text, muted text and accent are readable on the background (%s)", (theme) => {
    const colours = tokens.color[theme];
    for (const name of ["text", "textMuted", "accent"]) {
      expect(contrastRatio(colours[name]!, colours.background!), `${theme} ${name}`).toBeGreaterThanOrEqual(4.5);
    }
  });

  it.each(themes)("every status text is readable on its own background (%s)", (theme) => {
    for (const [status, colours] of Object.entries(tokens.status)) {
      const { fg, bg } = colours[theme];
      expect(contrastRatio(fg, bg), `${theme} ${status}`).toBeGreaterThanOrEqual(4.5);
    }
  });

  it("the focus colour is visible against the background in both themes (3:1 for non-text)", () => {
    for (const theme of themes) {
      expect(contrastRatio(tokens.color[theme].focus!, tokens.color[theme].background!), theme).toBeGreaterThanOrEqual(3);
    }
  });
});
