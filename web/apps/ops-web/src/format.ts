// Times arrive and are stored in UTC; people read them in Sri Lanka time (Asia/Colombo, UTC+5:30, no daylight saving).
const colombo = new Intl.DateTimeFormat("en-GB", {
  timeZone: "Asia/Colombo",
  day: "numeric",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  hourCycle: "h23",
});

// "2026-10-14T06:44:00Z" -> "14 Oct, 12:14:00". Built from the parts, so a different browser locale cannot reorder it.
export function formatColombo(isoUtc: string | null): string {
  if (!isoUtc) return "—";
  const date = new Date(isoUtc);
  if (Number.isNaN(date.getTime())) return "—";

  const part = (type: Intl.DateTimeFormatPartTypes) => colombo.formatToParts(date).find((p) => p.type === type)?.value ?? "";
  return `${part("day")} ${part("month")}, ${part("hour")}:${part("minute")}:${part("second")}`;
}

const clock = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", hour: "2-digit", minute: "2-digit", hourCycle: "h23" });

// "2026-10-14T02:08:00Z" -> "07:38" (Sri Lanka time, 24 hours).
export function formatClock(isoUtc: string | null | undefined): string {
  if (!isoUtc) return "—";
  const date = new Date(isoUtc);
  if (Number.isNaN(date.getTime())) return "—";
  const part = (type: Intl.DateTimeFormatPartTypes) => clock.formatToParts(date).find((p) => p.type === type)?.value ?? "";
  return `${part("hour")}:${part("minute")}`;
}
