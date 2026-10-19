import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:yaml/yaml.dart';

// Contract tests for GET /journeys/{id} (ADR-12). test/fixtures/journey_detail.json
// is a real answer of the backend (train 1001 on 20 Oct, a moving train); the
// field `freshness.lastStationName`, added by the backend on 20 Oct, was put in
// by hand (value "Kelaniya", the station the last position is after).
// flutter test runs with the package folder as working directory.

Map<String, dynamic> stopNamed(Map<String, dynamic> json, String name) =>
    (json['stops'] as List).cast<Map<String, dynamic>>().firstWhere((s) => s['name'] == name);

Map<String, dynamic> load() =>
    jsonDecode(File('test/fixtures/journey_detail.json').readAsStringSync()) as Map<String, dynamic>;

/// The fields the models read, per openapi.yaml schema (same idea as in
/// journey_summary_test.dart): removing one must break parsing, and each must
/// exist in openapi.yaml.
const readFields = <String, List<String>>{
  'JourneyDetailResponse': [
    'journeyId', 'serviceNumber', 'name', 'trainType', 'serviceDate', 'status', 'from', 'to', 'freshness', 'hold', 'reason', 'version', 'stops', 'trainSet',
    'terminatesAt',
  ],
  'JourneyDetailFreshnessResponse': ['tracking', 'updatedAt', 'lastStationName', 'lastPosition'],
  'JourneyStationResponse': ['stationId', 'name'],
  'JourneyHoldResponse': ['stationId', 'minutes', 'reason'],
  'JourneyReasonResponse': ['code', 'note'],
  'PositionResponse': ['lastStationId', 'nextStationId', 'fractionToNext'],
  'JourneyStopResponse': [
    'sequence', 'stationId', 'name', 'callType', 'status', 'isAdded', 'scheduledArrival', 'expectedArrival', 'actualArrival',
    'scheduledDeparture', 'expectedDeparture', 'actualDeparture', 'platform', 'previousPlatform',
  ],
  'TrainSetResponse': ['code', 'traction', 'coaches'],
  'CoachResponse': ['position', 'class', 'seating'],
};

void main() {
  group('JourneyDetail.fromJson', () {
    test('parses a real answer: header, freshness, position, train set, stops', () {
      final j = JourneyDetail.fromJson(load());
      expect(j.serviceNumber, '1001');
      expect(j.name, 'Kandy Express');
      expect(j.status.kind, StatusKind.onTime);
      expect(j.freshness.tracking, Tracking.live);
      expect(j.freshness.updatedAt, DateTime.utc(2026, 10, 20, 1, 16, 31, 183, 576));
      expect(j.lastPosition!.lastStationId, '01a14eb2-015b-7ebe-998e-19f3d315ef4d');
      expect(j.lastPosition!.fractionToNext, closeTo(0.2933, 0.0001));
      expect(j.trainSet!.traction, 'DieselPowerSet');
      expect(j.trainSet!.coaches, hasLength(5));
      expect(j.stops, hasLength(11));
    });

    test('lastStationName gives the name for "Last seen ... near" (updatedAt is the last GPS position)', () {
      final j = JourneyDetail.fromJson(load());
      expect(j.freshness.lastStation, 'Kelaniya');
      expect(j.freshness.lastSeenAt, j.freshness.updatedAt);
    });

    test('callingPoints lists the stops where the train stops, not the ones it passes', () {
      final j = JourneyDetail.fromJson(load());
      expect(j.callingPoints.map((s) => s.name), ['Colombo Fort', 'Maradana', 'Gampaha', 'Polgahawela', 'Peradeniya', 'Kandy']);
    });

    test('a skipped stop stays on the list of calling points', () {
      final json = load();
      final stops = json['stops'] as List;
      (stops[4] as Map<String, dynamic>)['status'] = 'Skipped'; // Gampaha
      final j = JourneyDetail.fromJson(json);
      expect(j.callingPoints.map((s) => s.name), contains('Gampaha'));
      expect(j.callingPoints.firstWhere((s) => s.name == 'Gampaha').state, StopState.skipped);
    });

    test('stop states and times are read as sent', () {
      final j = JourneyDetail.fromJson(load());
      final fort = j.stops.first;
      expect(fort.state, StopState.departed);
      expect(fort.scheduledArrival, isNull);
      expect(fort.actualDeparture, isNotNull);
      expect(fort.platform, '5');
      expect(j.stops[2].callType, CallType.pass);
      expect(j.stops[2].state, StopState.passed);
      expect(j.stops[3].actualArrival, isNull);
    });

    test('withPosition changes the position and the time of the data, nothing else', () {
      final j = JourneyDetail.fromJson(load());
      final at = DateTime.utc(2026, 10, 20, 2);
      final moved = j.withPosition(
        const Position(lastStationId: '01a14eb2-015b-732d-aef4-d3a44774841f', nextStationId: '01a14eb2-015b-7b5a-a2da-49d81f392430', fractionToNext: 0.5),
        at,
      );

      expect(moved.lastPosition!.fractionToNext, 0.5);
      expect(moved.freshness.updatedAt, at);
      expect(moved.freshness.lastSeenAt, at);
      expect(moved.freshness.lastStation, 'Ragama');
      expect(moved.freshness.tracking, j.freshness.tracking);
      expect(moved.status, same(j.status));
      expect(moved.stops, same(j.stops));
    });

    test('no hold and no reason are null', () {
      final j = JourneyDetail.fromJson(load());
      expect(j.hold, isNull);
      expect(j.reason, isNull);
    });

    test('a hold with its reason, and the journey reason, are read as sent', () {
      final json = load()
        ..['hold'] = {
          'stationId': '01a14eb2-015b-7b5a-a2da-49d81f392430',
          'minutes': 3,
          'reason': {'code': 'Congestion', 'note': 'Waiting for the platform'},
        }
        ..['reason'] = {'code': 'SignalFailure', 'note': null};
      final j = JourneyDetail.fromJson(json);

      expect(j.hold!.minutes, 3);
      expect(j.hold!.stationId, '01a14eb2-015b-7b5a-a2da-49d81f392430');
      expect(j.hold!.reason!.code, 'Congestion');
      expect(j.hold!.reason!.note, 'Waiting for the platform');
      expect(j.reason!.code, 'SignalFailure');
      expect(j.reason!.note, isNull);
    });

    test('a hold without a reason or minutes (both optional) is read', () {
      final json = load()..['hold'] = {'stationId': 'x', 'minutes': null, 'reason': null};
      final hold = JourneyDetail.fromJson(json).hold!;
      expect(hold.minutes, isNull);
      expect(hold.reason, isNull);
    });

    test('terminatesAt is null for a journey that runs on', () {
      expect(JourneyDetail.fromJson(load()).terminatesAt, isNull);
    });

    test('terminatesAt is read as sent: station id and name', () {
      final json = load()
        ..['status'] = {'kind': 'TerminatedEarly', 'delayMinutes': null}
        ..['terminatesAt'] = {'stationId': '01a14eb2-015b-7b5a-a2da-49d81f392430', 'name': 'Gampaha'};
      final end = JourneyDetail.fromJson(json).terminatesAt!;

      expect(end.stationId, '01a14eb2-015b-7b5a-a2da-49d81f392430');
      expect(end.name, 'Gampaha');
    });

    test('a missing terminatesAt key counts as null (the schema does not require it)', () {
      final json = load()..remove('terminatesAt');
      expect(JourneyDetail.fromJson(json).terminatesAt, isNull);
    });

    test('a terminatesAt without its name is refused', () {
      final json = load()..['terminatesAt'] = {'stationId': 'x'};
      expect(() => JourneyDetail.fromJson(json), throwsA(anything));
    });

    test('withPosition keeps terminatesAt', () {
      final json = load()..['terminatesAt'] = {'stationId': 'x', 'name': 'Gampaha'};
      final j = JourneyDetail.fromJson(json);
      final moved = j.withPosition(const Position(lastStationId: 'a', nextStationId: 'b', fractionToNext: 0.1), DateTime.utc(2026, 10, 20));
      expect(moved.terminatesAt!.name, 'Gampaha');
    });

    test('no train set yet is null, not an error', () {
      final json = load()..['trainSet'] = null;
      expect(JourneyDetail.fromJson(json).trainSet, isNull);
    });

    test('no position yet is null, and then there is no station name', () {
      final json = load();
      (json['freshness'] as Map<String, dynamic>)
        ..['lastPosition'] = null
        ..['lastStationName'] = null;
      final j = JourneyDetail.fromJson(json);
      expect(j.lastPosition, isNull);
      expect(j.freshness.lastStation, isNull);
    });

    test('an unknown stop status or call type is refused', () {
      final a = load();
      ((a['stops'] as List).first as Map<String, dynamic>)['status'] = 'Teleported';
      expect(() => JourneyDetail.fromJson(a), throwsFormatException);
      final b = load();
      ((b['stops'] as List).first as Map<String, dynamic>)['callType'] = 'Maybe';
      expect(() => JourneyDetail.fromJson(b), throwsFormatException);
    });

    test('unknown fields are ignored', () {
      final json = load()..['somethingNew'] = 1;
      expect(() => JourneyDetail.fromJson(json), returnsNormally);
    });
  });

  group('every field the detail models read is required', () {
    // The fixture has no hold and no reason; the tests of these two schemas use one.
    Map<String, dynamic> withHoldAndReason() => load()
      ..['hold'] = {
        'stationId': '01a14eb2-015b-7b5a-a2da-49d81f392430',
        'minutes': 3,
        'reason': {'code': 'Congestion', 'note': 'Waiting for the platform'},
      }
      ..['reason'] = {'code': 'SignalFailure', 'note': 'signal fault near Ragama'};

    Map<String, dynamic> objectFor(Map<String, dynamic> json, String schema) => switch (schema) {
          'JourneyDetailResponse' => json,
          'JourneyDetailFreshnessResponse' => json['freshness'] as Map<String, dynamic>,
          'PositionResponse' => (json['freshness'] as Map<String, dynamic>)['lastPosition'] as Map<String, dynamic>,
          'JourneyStopResponse' => (json['stops'] as List).first as Map<String, dynamic>,
          'JourneyHoldResponse' => json['hold'] as Map<String, dynamic>,
          'JourneyReasonResponse' => json['reason'] as Map<String, dynamic>,
          'TrainSetResponse' => json['trainSet'] as Map<String, dynamic>,
          'CoachResponse' => ((json['trainSet'] as Map<String, dynamic>)['coaches'] as List).first as Map<String, dynamic>,
          _ => throw ArgumentError(schema),
        };

    // terminatesAt is not required by the schema: a missing key is null (tested above).
    const optional = {'terminatesAt'};
    for (final entry in readFields.entries.where((e) => e.key != 'JourneyStationResponse')) {
      for (final field in entry.value.where((f) => !optional.contains(f))) {
        test('${entry.key}.$field missing makes parsing fail', () {
          final json = withHoldAndReason();
          objectFor(json, entry.key).remove(field);
          expect(() => JourneyDetail.fromJson(json), throwsA(anything));
        });
      }
    }
  });

  group('backend/openapi.yaml still has every field the detail models read', () {
    final spec = loadYaml(File('../../../backend/openapi.yaml').readAsStringSync()) as YamlMap;
    final schemas = (spec['components'] as YamlMap)['schemas'] as YamlMap;

    for (final entry in readFields.entries) {
      test('${entry.key}: properties', () {
        final schema = schemas[entry.key] as YamlMap?;
        expect(schema, isNotNull, reason: 'schema ${entry.key} is gone from openapi.yaml');
        final properties = (schema!['properties'] as YamlMap).keys.toSet();
        for (final field in entry.value) {
          expect(properties, contains(field), reason: '${entry.key}.$field');
        }
      });
    }

    test('GET /api/v1/journeys/{journeyId} exists and answers JourneyDetailResponse', () {
      final path = (spec['paths'] as YamlMap)['/api/v1/journeys/{journeyId}'] as YamlMap?;
      expect(path, isNotNull);
      final schema = ((((path!['get'] as YamlMap)['responses'] as YamlMap)['200'] as YamlMap)['content'] as YamlMap)['application/json']
          as YamlMap;
      expect((schema['schema'] as YamlMap)[r'$ref'], '#/components/schemas/JourneyDetailResponse');
    });
  });
}
