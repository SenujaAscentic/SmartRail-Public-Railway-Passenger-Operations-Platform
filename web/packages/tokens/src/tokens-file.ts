// The shape of shared/tokens.json, as far as the CSS generator needs it. The file is the single source for web and Flutter;
// this describes it, it does not copy it. Only the parts the web uses are listed.
export interface TokensFile {
  color: Record<ThemeName, Record<string, string>>;
  status: Record<string, { icon: string } & Record<ThemeName, { fg: string; bg: string }>>;
  typography: {
    fontFamily: string;
    fontFamilyMono: string;
    surfaces: Record<string, Record<string, number | string>>;
  };
  // Colours of the station displays only (dark theme), e.g. surface, onSurface, onSurfaceMuted. The "note" is text.
  display?: Record<string, string>;
  space: Record<string, number>;
  radius: Record<string, number>;
  touchTarget: Record<string, number>;
  motion: { durationMs: number };
}

export type ThemeName = "light" | "dark";
