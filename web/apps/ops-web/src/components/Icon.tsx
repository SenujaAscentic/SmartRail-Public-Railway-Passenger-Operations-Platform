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
