import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:yaml/yaml.dart';

// StationCateringResponse as the backend sends it (openapi.yaml): available, openNow, hotline, openingHours, menu. Always all five.
void main() {
  group('StationCatering.fromJson', () {
    test('an available station: menu, hotline, opening hours, open now', () {
      final catering = StationCatering.fromJson({
        'available': true,
        'openNow': true,
        'hotline': '+94112345678',
        'openingHours': {'opens': '06:00', 'closes': '20:00'},
        'menu': [
          {'name': 'Chicken rice', 'price': 450, 'available': true},
          {'name': 'Egg roti', 'price': 120, 'available': false},
        ],
      });

      expect(catering.available, isTrue);
      expect(catering.openNow, isTrue);
      expect(catering.hotline, '+94112345678');
      expect(catering.hasHotline, isTrue);
      expect(catering.openingHours!.opens, '06:00');
      expect(catering.openingHours!.closes, '20:00');
      expect(catering.menu.map((m) => m.name), ['Chicken rice', 'Egg roti']);
      expect(catering.menu[0].price, 450);
      expect(catering.menu[1].available, isFalse);
    });

    test('openNow false is read as it is (the server decides, the model looks at no clock)', () {
      final catering = StationCatering.fromJson({
        'available': true,
        'openNow': false,
        'hotline': '+94112345678',
        'openingHours': {'opens': '06:00', 'closes': '20:00'},
        'menu': [],
      });

      expect(catering.openNow, isFalse);
    });

    test('not available (provider down, cache older than 15 min, not taking orders): menu [], hotline "", hours null', () {
      final catering = StationCatering.fromJson({
        'available': false,
        'openNow': false,
        'hotline': '',
        'openingHours': null,
        'menu': [],
      });

      expect(catering.available, isFalse);
      expect(catering.menu, isEmpty);
      expect(catering.hotline, '');
      expect(catering.hasHotline, isFalse);
      expect(catering.openingHours, isNull);
    });

    test('an empty hotline on an available station: no call button', () {
      final catering = StationCatering.fromJson({
        'available': true,
        'openNow': true,
        'hotline': '',
        'openingHours': {'opens': '06:00', 'closes': '20:00'},
        'menu': [
          {'name': 'Tea', 'price': 80, 'available': true},
        ],
      });

      expect(catering.hasHotline, isFalse);
      expect(catering.menu, hasLength(1));
    });

    test('opening hours null on an available station', () {
      final catering = StationCatering.fromJson({
        'available': true,
        'openNow': true,
        'hotline': '+94112345678',
        'openingHours': null,
        'menu': [],
      });

      expect(catering.openingHours, isNull);
    });
  });

  group('backend/openapi.yaml still has every field the catering models read', () {
    final spec = loadYaml(File('../../../backend/openapi.yaml').readAsStringSync()) as YamlMap;
    final schemas = (spec['components'] as YamlMap)['schemas'] as YamlMap;
    const fields = <String, List<String>>{
      'StationCateringResponse': ['available', 'openNow', 'hotline', 'openingHours', 'menu'],
      'MenuItemResponse': ['name', 'price', 'available'],
      'OpeningHoursResponse': ['opens', 'closes'],
      'StationResponse': ['stationId', 'name', 'cateringStation'],
    };

    for (final entry in fields.entries) {
      test('${entry.key}: properties', () {
        final schema = schemas[entry.key] as YamlMap?;
        expect(schema, isNotNull, reason: 'schema ${entry.key} is gone from openapi.yaml');
        final properties = (schema!['properties'] as YamlMap).keys.toSet();
        for (final field in entry.value) {
          expect(properties, contains(field), reason: '${entry.key}.$field');
        }
      });
    }

    test('the endpoint is still GET /api/v1/stations/{stationId}/catering', () {
      final paths = spec['paths'] as YamlMap;
      expect((paths['/api/v1/stations/{stationId}/catering'] as YamlMap).containsKey('get'), isTrue);
    });
  });

  group('catering wording', () {
    test('prices: whole rupees', () {
      expect(Strings.price(450), 'Rs 450');
      expect(Strings.price(450.0), 'Rs 450');
    });

    test('the caterer is shown as RailCantima (a display name only)', () {
      expect(Strings.catererName, 'RailCantima');
      expect(Strings.ordersHandledBy, 'Orders are handled by RailCantima');
    });

    test('the closed message and the call summary', () {
      expect(Strings.hotlineClosed('06:00'), 'Hotline closed – opens 06:00');
      expect(Strings.mentionOnCall('1005', 'Polgahawela', '10:52'), 'Mention: Train 1005 · arriving Polgahawela ~10:52 · your coach');
      expect(Strings.mentionOnCall('1005', 'Polgahawela', null), 'Mention: Train 1005 · arriving Polgahawela · your coach');
    });
  });
}
