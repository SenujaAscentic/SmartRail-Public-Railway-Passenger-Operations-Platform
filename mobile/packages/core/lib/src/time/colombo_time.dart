// The ONE place where the apps turn server time (UTC) into the time shown to
// the passenger. Never use DateTime.toLocal(): the Android emulator runs in
// UTC and would show every train 5½ hours off.

/// Sri Lanka Standard Time is UTC+05:30 all year (no daylight saving), so a
/// fixed offset is correct and needs no timezone package.
const Duration colomboOffset = Duration(hours: 5, minutes: 30);

/// Wall-clock time in Colombo for a UTC instant. The result is flagged UTC only
/// so that Dart does not shift it again; read its hour / minute / day fields.
DateTime toColombo(DateTime utc) {
  if (!utc.isUtc) {
    throw ArgumentError.value(utc, 'utc', 'must be a UTC DateTime');
  }
  return utc.add(colomboOffset);
}

/// "07:38" (24-hour, Asia/Colombo).
String formatTime(DateTime utc) {
  final c = toColombo(utc);
  return '${_two(c.hour)}:${_two(c.minute)}';
}

/// "2026-10-14": the Colombo calendar date (used for the search date).
String formatDate(DateTime utc) {
  final c = toColombo(utc);
  return '${c.year.toString().padLeft(4, '0')}-${_two(c.month)}-${_two(c.day)}';
}

String _two(int n) => n.toString().padLeft(2, '0');
