// Times arrive in UTC; people read them in Sri Lanka time (Asia/Colombo, UTC+5:30, no daylight saving), 24-hour.
const colombo = new Intl.DateTimeFormat("en-GB", {
  timeZone: "Asia/Colombo",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
});

// "2026-10-20T01:40:00Z" -> "07:10". Built from the parts, so a different browser locale cannot change the shape.
export function formatTime(isoUtc: string | null | undefined): string {
  if (!isoUtc) return "--:--";
  const date = new Date(isoUtc);
  if (Number.isNaN(date.getTime())) return "--:--";
  const parts = colombo.formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)?.value ?? "--";
  return `${part("hour")}:${part("minute")}`;
}

// The clock in the header: the time of the given instant (milliseconds), in Sri Lanka time.
export function formatClock(milliseconds: number): string {
  return formatTime(new Date(milliseconds).toISOString());
}

export function toMillis(isoUtc: string): number {
  return new Date(isoUtc).getTime();
}
