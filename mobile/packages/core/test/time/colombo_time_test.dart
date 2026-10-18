import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

void main() {
  test('formatTime shows Colombo time (UTC+05:30), not UTC', () {
    expect(formatTime(DateTime.utc(2026, 10, 14, 2, 8)), '07:38');
  });

  test('formatTime pads hours and minutes', () {
    expect(formatTime(DateTime.utc(2026, 10, 14, 0, 0)), '05:30');
  });

  test('formatTime crosses midnight into the next Colombo day', () {
    expect(formatTime(DateTime.utc(2026, 10, 14, 19, 0)), '00:30');
    expect(formatDate(DateTime.utc(2026, 10, 14, 19, 0)), '2026-10-15');
  });

  test('formatDate keeps the Colombo date for the early morning', () {
    expect(formatDate(DateTime.utc(2026, 10, 14, 0, 30)), '2026-10-14');
  });

  test('a non-UTC DateTime is refused (never device local time)', () {
    expect(() => formatTime(DateTime(2026, 10, 14, 7, 38)), throwsArgumentError);
  });

  test('a parsed server timestamp works', () {
    expect(formatTime(DateTime.parse('2026-10-14T04:40:00Z')), '10:10');
  });
}
