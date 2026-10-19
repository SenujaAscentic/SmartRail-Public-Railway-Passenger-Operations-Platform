// The few icons the ops web needs, named as in shared/tokens.json (each status there has an icon name), drawn as inline SVG
// so no icon package is needed. They are decoration: the TEXT next to them says the same thing (status is never colour alone).
// A path with a trailing "~" is drawn faint (the weak parts of a signal icon).
const ICONS = {
  "check-circle": ["M22 11.1V12a10 10 0 1 1-5.9-9.1", "M22 4 12 14l-3-3"],
  "x-circle": ["M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z", "M15 9l-6 6", "M9 9l6 6"],
  clock: ["M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z", "M12 6v6l4 2"],
  "clock-alert": ["M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z", "M12 6v6", "M12 16h.01"],
  "arrow-up-left": ["M7 17V7h10", "M17 17 7 7"],
  "hand-stop": ["M12 2v10", "M8 6v6", "M16 6v6", "M5 12v3a7 7 0 0 0 14 0v-4"],
  "arrows-exchange": ["M4 8h14", "M14 4l4 4-4 4", "M20 16H6", "M10 12l-4 4 4 4"],
  "signal-weak": ["M5 20v-3", "M10 20v-7~", "M15 20V9~", "M20 20V4~"],
  "signal-off": ["M5 20v-3~", "M10 20v-7~", "M15 20V9~", "M20 20V4~", "M3 3l18 18"],
  train: ["M6 3h12a2 2 0 0 1 2 2v10a3 3 0 0 1-3 3H7a3 3 0 0 1-3-3V5a2 2 0 0 1 2-2Z", "M4 11h16", "M8 21l2-3", "M16 21l-2-3", "M8 15h.01", "M16 15h.01"],
  dashboard: ["M3 3h7v9H3Z", "M14 3h7v5h-7Z", "M14 12h7v9h-7Z", "M3 16h7v5H3Z"],
  history: ["M3 12a9 9 0 1 0 3-6.7L3 8", "M3 3v5h5", "M12 7v5l3 2"],
  pulse: ["M3 12h4l3-8 4 16 3-8h4"],
  satellite: ["M12 10a2 2 0 1 0 0 4 2 2 0 0 0 0-4Z", "M4.9 19.1a10 10 0 0 1 0-14.2", "M7.8 16.2a6 6 0 0 1 0-8.4", "M19.1 4.9a10 10 0 0 1 0 14.2", "M16.2 7.8a6 6 0 0 1 0 8.4"],
  driver: ["M12 4a4 4 0 1 0 0 8 4 4 0 0 0 0-8Z", "M4 21a8 8 0 0 1 16 0"],
  controls: ["M4 6h10", "M18 6h2", "M4 12h4", "M12 12h8", "M4 18h12", "M14 4v4", "M8 10v4", "M16 16v4"],
  bell: ["M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9", "M10 21a2 2 0 0 0 4 0"],
  warning: ["M12 3 2 21h20Z", "M12 10v5", "M12 18h.01"],
  chart: ["M3 17l6-6 4 4 8-8", "M17 7h4v4"],
  list: ["M8 6h13", "M8 12h13", "M8 18h13", "M3 6h.01", "M3 12h.01", "M3 18h.01"],
  pin: ["M12 21s7-6 7-12a7 7 0 1 0-14 0c0 6 7 12 7 12Z", "M12 7a2 2 0 1 0 0 4 2 2 0 0 0 0-4Z"],
  phone: ["M7 2h10a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1Z", "M11 18h2"],
  monitor: ["M3 4h18v12H3Z", "M8 20h8", "M12 16v4"],
  ban: ["M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z", "M5 5l14 14"],
  "arrow-left": ["M19 12H5", "M12 19l-7-7 7-7"],
  undo: ["M3 7v6h6", "M3 13a9 9 0 1 0 3-7.7"],
  "skip-forward": ["M5 4l10 8-10 8Z", "M19 5v14"],
  filter: ["M3 5h18", "M7 12h10", "M10 19h4"],
} as const;

export type IconName = keyof typeof ICONS;

export function Icon({ name }: { name: IconName }) {
  return (
    <svg className="icon" aria-hidden="true" focusable="false" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      {ICONS[name].map((d) => (
        <path key={d} d={d.replace("~", "")} opacity={d.endsWith("~") ? 0.35 : 1} />
      ))}
    </svg>
  );
}
