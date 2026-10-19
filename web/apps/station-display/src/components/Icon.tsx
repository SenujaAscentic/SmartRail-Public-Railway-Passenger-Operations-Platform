// The icons of the display pages: the names are the ones in shared/tokens.json (each status there has an icon name), plus the
// few the design adds (live, offline, arrows, warning). Drawn as inline SVG, so no icon package is needed. They are
// decoration: the TEXT next to them always says the same thing (status is never colour alone).
// A path with a trailing "~" is drawn faint (the weak parts of a signal icon).
const ICONS = {
  "check-circle": ["M22 11.1V12a10 10 0 1 1-5.9-9.1", "M22 4 12 14l-3-3"],
  "x-circle": ["M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z", "M15 9l-6 6", "M9 9l6 6"],
  "clock-alert": ["M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z", "M12 6v6l4 2"],
  clock: ["M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z", "M12 6v6l4 2"],
  "arrow-up-left": ["M7 17V7h10", "M17 17 7 7"],
  "hand-stop": ["M18 11V6a2 2 0 0 0-4 0", "M14 10V4a2 2 0 0 0-4 0v2", "M10 10.5V6a2 2 0 0 0-4 0v8", "M18 8a2 2 0 1 1 4 0v6a8 8 0 0 1-8 8h-2c-2.8 0-4.5-.9-5.9-2.4L3.4 16a2 2 0 0 1 3.2-2.4L8 15"],
  "arrows-exchange": ["M3 8h18", "M17 4l4 4-4 4", "M21 16H3", "M7 12l-4 4 4 4"],
  calendar: ["M8 2v4", "M16 2v4", "M3 10h18", "M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z"],
  "calendar-x": ["M8 2v4", "M16 2v4", "M3 10h18", "M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z", "M10 14l4 4", "M14 14l-4 4"],
  "signal-weak": ["M5 20v-3", "M10 20v-7~", "M15 20V9~", "M20 20V4~"],
  "signal-off": ["M5 20v-3~", "M10 20v-7~", "M15 20V9~", "M20 20V4~", "M3 3l18 18"],
  live: ["M12 13a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z", "M8.5 15.5a5 5 0 0 1 0-7", "M15.5 8.5a5 5 0 0 1 0 7", "M5.6 18.4a9 9 0 0 1 0-12.8", "M18.4 5.6a9 9 0 0 1 0 12.8"],
  offline: ["M17.5 19a4.5 4.5 0 0 0 .9-8.9A6 6 0 0 0 7 8.7 4.5 4.5 0 0 0 6.5 19Z", "M3 3l18 18"],
  warning: ["M10.3 3.9 2.4 18a2 2 0 0 0 1.7 3h15.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z", "M12 9v4", "M12 17h.01"],
  "arrow-right": ["M5 12h14", "M12 5l7 7-7 7"],
  "arrow-left": ["M19 12H5", "M12 19l-7-7 7-7"],
  walk: ["M13 4.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3Z", "M7 21l3-6", "M10 15l-1-5 5-1 2 4 3 1", "M14 9l-2 6 3 6"],
} as const;

export type IconName = keyof typeof ICONS;

export const ICON_NAMES = Object.keys(ICONS) as IconName[];

export function Icon({ name }: { name: IconName }) {
  return (
    <svg className="icon" aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      {ICONS[name].map((d) => (
        <path key={d} d={d.replace("~", "")} opacity={d.endsWith("~") ? 0.35 : 1} />
      ))}
    </svg>
  );
}
