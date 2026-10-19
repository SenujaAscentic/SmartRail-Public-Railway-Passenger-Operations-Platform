const _months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/// "2026-10-14" -> "14 Oct 2026". Works on the plain service date (a calendar
/// date has no time zone), so nothing here depends on the phone's clock.
String dateLabel(String isoDate) {
  final parts = isoDate.split('-');
  if (parts.length != 3) throw FormatException('Not a YYYY-MM-DD date: "$isoDate"');
  final year = int.parse(parts[0]);
  final month = int.parse(parts[1]);
  final day = int.parse(parts[2]);
  if (month < 1 || month > 12) throw FormatException('Not a YYYY-MM-DD date: "$isoDate"');
  return '$day ${_months[month - 1]} $year';
}
