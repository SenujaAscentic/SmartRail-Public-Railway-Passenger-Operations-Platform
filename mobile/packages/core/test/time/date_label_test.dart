import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

void main() {
  test('dateLabel writes day, month name and year', () {
    expect(dateLabel('2026-10-14'), '14 Oct 2026');
  });

  test('dateLabel drops the leading zero of the day', () {
    expect(dateLabel('2026-01-05'), '5 Jan 2026');
  });

  test('dateLabel refuses text that is not a date', () {
    expect(() => dateLabel('14/10/2026'), throwsFormatException);
    expect(() => dateLabel('2026-13-01'), throwsFormatException);
  });
}
