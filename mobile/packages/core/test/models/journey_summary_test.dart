import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:yaml/yaml.dart';

// Contract tests (ADR-12). The JSON files in test/fixtures follow
// docs/develop/02-api-contract.md section 5. flutter test runs with the
// package folder as working directory.

Map<String, dynamic> load(String name) =>
    jsonDecode(File('test/fixtures/$name').readAsStringSync()) as Map<String, dynamic>;

/// The fields the models read, per openapi.yaml schema, as paths into the JSON.
/// Used twice below: removing each one must make parsing fail, and each one must
/// exist in the generated openapi.yaml, so a renamed field fails a test on
/// either side.
const readFields = <String, List<String>>{
  'JourneySummaryResponse': [
    'journeyId', 'serviceNumber', 'name', 'trainType', 'serviceDate', 'status', 'from', 'to', 'freshness', 'hold', 'reason', 'version',
  ],
  'JourneyStatusResponse': ['kind', 'delayMinutes'],
  'JourneyHoldResponse': ['stationId', 'minutes', 'reason'],
  'JourneyReasonResponse': ['code', 'note'],
  'JourneyFromResponse': [
    'stationId', 'name', 'scheduledDeparture', 'expectedDeparture', 'platform', 'previousPlatform',
  ],
  'JourneyToResponse': ['stationId', 'name', 'scheduledArrival', 'expectedArrival'],
  'JourneyFreshnessResponse': ['tracking', 'updatedAt'],
  'StationSummary': ['id', 'code', 'name'],
};

void main() {
  group('JourneySummary.fromJson', () {
    test('parses a late journey with a platform change', () {
      final j = JourneySummary.fromJson(load('journey_summary.json'));
      expect(j.serviceNumber, '1001');
      expect(j.name, 'Kandy Express');
      expect(j.status.kind, StatusKind.late);
      expect(j.status.delayMinutes, 7);
      expect(j.from.scheduledDeparture, DateTime.utc(2026, 10, 14, 11, 32));
      expect(j.from.expectedDeparture, DateTime.utc(2026, 10, 14, 11, 39));
      expect(j.from.platform, '5');
      expect(j.from.previousPlatform, '2');
      expect(j.to.expectedArrival, DateTime.utc(2026, 10, 14, 13, 42));
      expect(j.freshness.tracking, Tracking.live);
      expect(j.freshness.updatedAt, DateTime.utc(2026, 10, 14, 11, 36, 30));
      expect(j.version, 12);
    });

    test('parses a cancelled journey: null expected times, empty platform means none', () {
      final j = JourneySummary.fromJson(load('journey_summary_cancelled.json'));
      expect(j.status.kind, StatusKind.cancelled);
      expect(j.from.expectedDeparture, isNull);
      expect(j.from.platform, isNull);
      expect(j.to.expectedArrival, isNull);
      expect(j.freshness.tracking, Tracking.notStarted);
    });

    test('hold and reason are null when there are none, and read when there are', () {
      final none = JourneySummary.fromJson(load('journey_summary.json'));
      expect(none.hold, isNull);
      expect(none.reason, isNull);

      final json = load('journey_summary.json')
        ..['hold'] = {'stationId': 'x', 'minutes': 3, 'reason': null}
        ..['reason'] = {'code': 'Weather', 'note': 'Heavy rain'};
      final j = JourneySummary.fromJson(json);
      expect(j.hold!.minutes, 3);
      expect(j.reason!.code, 'Weather');
      expect(j.reason!.note, 'Heavy rain');
    });

    test('ignores fields it does not know (adding fields is not breaking)', () {
      final json = load('journey_summary.json')..['somethingNew'] = {'a': 1};
      expect(() => JourneySummary.fromJson(json), returnsNormally);
    });

    test('a null scheduled time is refused', () {
      final json = load('journey_summary.json');
      (json['from'] as Map<String, dynamic>)['scheduledDeparture'] = null;
      expect(() => JourneySummary.fromJson(json), throwsFormatException);
    });
  });

  group('every field the models read is required', () {
    // Removes [field] from the JSON object found at [schema] in the fixture.
    Map<String, dynamic> without(String schema, String field) {
      // The fixture has no hold and no reason; the tests of those two schemas use one.
      final json = load('journey_summary.json')
        ..['hold'] = {
          'stationId': 'x',
          'minutes': 3,
          'reason': {'code': 'Congestion', 'note': 'Waiting for the platform'},
        }
        ..['reason'] = {'code': 'SignalFailure', 'note': null};
      final target = switch (schema) {
        'JourneySummaryResponse' => json,
        'JourneyHoldResponse' => json['hold'] as Map<String, dynamic>,
        'JourneyReasonResponse' => json['reason'] as Map<String, dynamic>,
        'JourneyStatusResponse' => json['status'] as Map<String, dynamic>,
        'JourneyFromResponse' => json['from'] as Map<String, dynamic>,
        'JourneyToResponse' => json['to'] as Map<String, dynamic>,
        'JourneyFreshnessResponse' => json['freshness'] as Map<String, dynamic>,
        _ => throw ArgumentError(schema),
      };
      target.remove(field);
      return json;
    }

    for (final entry in readFields.entries.where((e) => e.key != 'StationSummary')) {
      for (final field in entry.value) {
        test('${entry.key}.$field missing makes parsing fail', () {
          // delayMinutes may be null for most kinds, but the key must be there
          // only for Late/Early; a Late fixture needs it.
          expect(() => JourneySummary.fromJson(without(entry.key, field)), throwsA(anything));
        });
      }
    }

    for (final field in readFields['StationSummary']!) {
      test('StationSummary.$field missing makes parsing fail', () {
        final json = (jsonDecode(File('test/fixtures/stations.json').readAsStringSync()) as List).first as Map<String, dynamic>
          ..remove(field);
        expect(() => Station.fromJson(json), throwsA(anything));
      });
    }
  });

  group('backend/openapi.yaml still has every field the models read', () {
    final spec = loadYaml(File('../../../backend/openapi.yaml').readAsStringSync()) as YamlMap;
    final schemas = (spec['components'] as YamlMap)['schemas'] as YamlMap;

    for (final entry in readFields.entries) {
      test('${entry.key}: properties and required', () {
        final schema = schemas[entry.key] as YamlMap?;
        expect(schema, isNotNull, reason: 'schema ${entry.key} is gone from openapi.yaml');
        final properties = (schema!['properties'] as YamlMap).keys.toSet();
        for (final field in entry.value) {
          expect(properties, contains(field), reason: '${entry.key}.$field');
        }
      });
    }

    test('the search endpoint is still GET /api/v1/journeys/search with from, to and date', () {
      final get = ((spec['paths'] as YamlMap)['/api/v1/journeys/search'] as YamlMap)['get'] as YamlMap;
      final names = [for (final p in get['parameters'] as YamlList) (p as YamlMap)['name']];
      expect(names, containsAll(['from', 'to', 'date']));
    });

    test('the station search endpoint is still GET /api/v1/stations with query', () {
      final get = ((spec['paths'] as YamlMap)['/api/v1/stations'] as YamlMap)['get'] as YamlMap;
      final names = [for (final p in get['parameters'] as YamlList) (p as YamlMap)['name']];
      expect(names, contains('query'));
    });
  });
}
