import type { ThemeName, TokensFile } from "./tokens-file.ts";

// "textMuted" -> "text-muted", "OnTime" -> "on-time". CSS variable names are kebab-case.
const kebab = (name: string): string => name.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();

// "Noto Sans (supports Sinhala and Tamil later)" -> "Noto Sans": the file adds a note after the name.
const familyName = (text: string): string => text.split("(")[0]!.trim();

// The variables that change with the theme: colours, and the colours of every status.
function themeVariables(tokens: TokensFile, theme: ThemeName): string[] {
  const lines: string[] = [];
  for (const [name, value] of Object.entries(tokens.color[theme])) {
    lines.push(`--color-${kebab(name)}: ${value};`);
  }
  for (const [status, colours] of Object.entries(tokens.status)) {
    lines.push(`--status-${kebab(status)}-fg: ${colours[theme].fg};`);
    lines.push(`--status-${kebab(status)}-bg: ${colours[theme].bg};`);
  }
  return lines;
}

// The colours of the station displays (tokens.json -> display): --display-surface, --display-on-surface, ...
// Written for every surface, so the generator needs no special case; only the displays use them.
function displayVariables(tokens: TokensFile): string[] {
  const lines: string[] = [];
  for (const [name, value] of Object.entries(tokens.display ?? {})) {
    if (name !== "note") lines.push(`--display-${kebab(name)}: ${value};`);
  }
  return lines;
}

// The variables that do not change with the theme. `surface` picks one row of the typography table
// ("ops" for the ops web; a display app would pass "display").
function fixedVariables(tokens: TokensFile, surface: string): string[] {
  const sizes = tokens.typography.surfaces[surface];
  if (!sizes) {
    throw new Error(`Unknown surface "${surface}". Known: ${Object.keys(tokens.typography.surfaces).join(", ")}`);
  }

  const lines = [
    `--font-family: "${familyName(tokens.typography.fontFamily)}", system-ui, sans-serif;`,
    `--font-family-mono: "${familyName(tokens.typography.fontFamilyMono)}", ui-monospace, monospace;`,
  ];
  for (const [name, value] of Object.entries(sizes)) {
    if (typeof value === "number") {
      lines.push(`--font-size-${kebab(name)}: ${value}px;`);
    }
  }
  for (const [name, value] of Object.entries(tokens.space)) lines.push(`--space-${name}: ${value}px;`);
  for (const [name, value] of Object.entries(tokens.radius)) lines.push(`--radius-${name}: ${value}px;`);
  const touch = tokens.touchTarget[surface];
  if (touch !== undefined) lines.push(`--touch-target: ${touch}px;`);
  lines.push(`--motion-duration: ${tokens.motion.durationMs}ms;`);
  return lines;
}

const block = (selector: string, lines: string[], indent = ""): string =>
  `${indent}${selector} {\n${lines.map((line) => `${indent}  ${line}`).join("\n")}\n${indent}}`;

// Turns shared/tokens.json into one CSS file. The light theme is the default; the dark theme applies when the system
// asks for it (the ops web follows the system setting, tokens.json rules). Reduced motion zeroes the duration.
//
// [fixedTheme] is for a surface that has ONE theme whatever the system says (the station displays are always dark,
// tokens.json rule): then :root holds just that theme's colours, with no media query for the colours.
export function tokensToCss(tokens: TokensFile, surface = "ops", fixedTheme?: ThemeName): string {
  if (fixedTheme) {
    return [
      "/* Generated from shared/tokens.json by packages/tokens. Do not edit; run `npm run build` in web/packages/tokens. */",
      block(":root", [
        `color-scheme: ${fixedTheme};`,
        ...themeVariables(tokens, fixedTheme),
        ...displayVariables(tokens),
        ...fixedVariables(tokens, surface),
      ]),
      `@media (prefers-reduced-motion: reduce) {\n${block(":root", ["--motion-duration: 0ms;"], "  ")}\n}`,
      "",
    ].join("\n\n");
  }
  return [
    "/* Generated from shared/tokens.json by packages/tokens. Do not edit; run `npm run build` in web/packages/tokens. */",
    block(":root", ["color-scheme: light dark;", ...themeVariables(tokens, "light"), ...displayVariables(tokens), ...fixedVariables(tokens, surface)]),
    `@media (prefers-color-scheme: dark) {\n${block(":root", themeVariables(tokens, "dark"), "  ")}\n}`,
    `@media (prefers-reduced-motion: reduce) {\n${block(":root", ["--motion-duration: 0ms;"], "  ")}\n}`,
    "",
  ].join("\n\n");
}
