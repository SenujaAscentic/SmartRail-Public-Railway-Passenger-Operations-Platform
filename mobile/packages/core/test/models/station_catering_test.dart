import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

void main() {
  group('StationCatering.fromJson', () {
    test('reads the menu, hotline and opening hours', () {
      final catering = StationCatering.fromJson({
        'available': true,
        'hotline': '+94112345678',
        'openingHours': {'opens': '06:00', 'closes': '20:00'},
        'menu': [
          {'name': 'Chicken rice', 'price': 450, 'available': true},
          {'name': 'Egg roti', 'price': 120.5, 'available': false},
        ],
      });

      expect(catering.available, isTrue);
      expect(catering.hotline, '+94112345678');
      expect(catering.openingHours!.opens, '06:00');
      expect(catering.menu.map((m) => m.name), ['Chicken rice', 'Egg roti']);
      expect(catering.menu[1].price, 120.5);
      expect(catering.menu[1].available, isFalse);
    });

    test('an unavailable answer may leave everything else out', () {
      final catering = StationCatering.fromJson({'available': false});

      expect(catering.available, isFalse);
      expect(catering.hotline, isNull);
      expect(catering.openingHours, isNull);
      expect(catering.menu, isEmpty);
    });
  });

  group('OpeningHours.isOpenAt (hours are Colombo time, UTC+05:30)', () {
    const hours = OpeningHours(opens: '06:00', closes: '20:00');

    test('open inside the hours; opening time is open, closing time is closed', () {
      expect(hours.isOpenAt(DateTime.utc(2026, 10, 20, 0, 30)), isTrue); // 06:00
      expect(hours.isOpenAt(DateTime.utc(2026, 10, 20, 6, 30)), isTrue); // 12:00
      expect(hours.isOpenAt(DateTime.utc(2026, 10, 20, 14, 29)), isTrue); // 19:59
      expect(hours.isOpenAt(DateTime.utc(2026, 10, 20, 14, 30)), isFalse); // 20:00
    });

    test('closed before opening', () {
      expect(hours.isOpenAt(DateTime.utc(2026, 10, 20, 0, 29)), isFalse); // 05:59
    });

    test('uses Colombo time, not the UTC hour (23:00 UTC is 04:30 the next day in Colombo)', () {
      expect(hours.isOpenAt(DateTime.utc(2026, 10, 19, 23)), isFalse);
      expect(hours.isOpenAt(DateTime.utc(2026, 10, 20, 12)), isTrue); // 17:30
    });

    test('hours past midnight', () {
      const night = OpeningHours(opens: '18:00', closes: '02:00');
      expect(night.isOpenAt(DateTime.utc(2026, 10, 20, 13)), isTrue); // 18:30
      expect(night.isOpenAt(DateTime.utc(2026, 10, 20, 19)), isTrue); // 00:30
      expect(night.isOpenAt(DateTime.utc(2026, 10, 20, 6)), isFalse); // 11:30
    });
  });

  group('catering wording', () {
    test('prices: whole rupees without decimals, otherwise two', () {
      expect(Strings.price(450), 'Rs 450');
      expect(Strings.price(450.0), 'Rs 450');
      expect(Strings.price(120.5), 'Rs 120.50');
    });

    test('the closed message and the call summary', () {
      expect(Strings.hotlineClosed('06:00'), 'Hotline closed – opens 06:00');
      expect(Strings.mentionOnCall('1005', 'Polgahawela', '10:52'), 'Mention: Train 1005 · arriving Polgahawela ~10:52 · your coach');
      expect(Strings.mentionOnCall('1005', 'Polgahawela', null), 'Mention: Train 1005 · arriving Polgahawela · your coach');
    });
  });
}
