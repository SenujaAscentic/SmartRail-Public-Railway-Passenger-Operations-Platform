import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

void main() {
  group('reasonLine', () {
    test('code and note: the label, a dot and the note', () {
      expect(Strings.reasonLine('SignalFailure', 'signal fault near Ragama'), 'Reason: Signal failure · signal fault near Ragama');
    });

    test('code only: the label', () {
      expect(Strings.reasonLine('TrackWork', null), 'Reason: Track work');
      expect(Strings.reasonLine('Weather', '  '), 'Reason: Weather');
    });

    test('"Other" with a note shows the note alone', () {
      expect(Strings.reasonLine('Other', 'Bridge inspection'), 'Reason: Bridge inspection');
    });

    test('"Other" without a note', () {
      expect(Strings.reasonLine('Other', null), 'Reason: Other');
    });

    test('a code the app does not know is shown as sent', () {
      expect(Strings.reasonLine('LandSlide', null), 'Reason: LandSlide');
    });

    test('every code of the contract has a label', () {
      const labels = {
        'SignalFailure': 'Signal failure',
        'TrackWork': 'Track work',
        'TrainFault': 'Train fault',
        'Weather': 'Weather',
        'CrewShortage': 'Crew shortage',
        'Congestion': 'Congestion',
        'Other': 'Other',
      };
      labels.forEach((code, label) => expect(Strings.reasonLabel(code), label));
    });

    test('the note is kept as plain text, not interpreted', () {
      expect(Strings.reasonLine('Other', '<b>bold</b> & more'), 'Reason: <b>bold</b> & more');
    });
  });
}
