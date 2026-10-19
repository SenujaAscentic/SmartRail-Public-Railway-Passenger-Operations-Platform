import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:yaml/yaml.dart';

// Contract tests for the stream. EVERY file in shared/events is parsed; a file
// this test does not know fails it, so a new event type cannot be forgotten.
// flutter test runs with the package folder as working directory.
const _eventsDir = '../../../shared/events';

/// What each example file must turn into.
const _changeTypes = {
  'DataFreshnessChanged',
  'DelayChanged',
  'JourneyCancelled',
  'JourneyEnded',
  'JourneyHeld',
  'JourneyReinstated',
  'JourneyStarted',
  'JourneyTerminatedEarly',
  'PlatformChanged',
  'StopSkipped',
  'StopStatusChanged',
};

Map<String, dynamic> example(String name) =>
    jsonDecode(File('$_eventsDir/$name.json').readAsStringSync()) as Map<String, dynamic>;

SseFrame frameOf(String event, Map<String, dynamic> json, {String? id}) =>
    SseFrame(id: id ?? json['sequence']?.toString(), event: event, data: jsonEncode(json));

void main() {
  group('every file in shared/events', () {
    final files = Directory(_eventsDir).listSync().whereType<File>().where((f) => f.path.endsWith('.json')).toList();

    test('the folder has files', () => expect(files, isNotEmpty));

    for (final file in files) {
      final name = file.uri.pathSegments.last.replaceAll('.json', '');

      test('$name parses into the right message', () {
        final json = example(name);
        final message = switch (name) {
          'snapshot' => parseLiveFrame(frameOf('snapshot', json)),
          'resync' => parseLiveFrame(frameOf('resync', json, id: null)),
          _ => parseLiveFrame(frameOf(name, json)),
        };

        switch (name) {
          case 'snapshot':
            final m = message as SnapshotMessage;
            expect(m.sequence, 1051);
            // The snapshot carries the public journey detail (20 Oct), read like GET /journeys/{id}.
            expect(m.journey, isNotNull);
            expect(m.journey!.journeyId, '0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01');
            expect(m.journey!.freshness.lastStation, 'Rambukkana');
          case 'resync':
            expect(message, isA<ResyncMessage>());
          case 'TrainPositionUpdated':
            final m = message as PositionMessage;
            expect(m.sequence, 1062);
            expect(m.journeyId, '0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01');
            expect(m.occurredAt, DateTime.utc(2026, 10, 14, 6, 50, 10));
            expect(m.position.lastStationId, '9a4e2d71-5b38-4c06-8e1f-6a0d3b7c9e24');
            expect(m.position.nextStationId, 'c7d05f38-1e92-4ab6-9d70-4f8a2c1e6b35');
            expect(m.position.fractionToNext, 0.42);
          default:
            expect(_changeTypes, contains(name), reason: 'new event file "$name": add it to this test');
            final m = message as ChangeMessage;
            expect(m.type, name);
            expect(m.sequence, json['sequence']);
            expect(m.journeyId, json['journeyId']);
        }
      });
    }
  });

  group('TrainPositionUpdated is read strictly', () {
    for (final field in ['sequence', 'journeyId', 'occurredAt', 'payload']) {
      test('$field missing makes PositionMessage.fromJson fail', () {
        final json = example('TrainPositionUpdated')..remove(field);
        expect(() => PositionMessage.fromJson(json), throwsA(anything));
      });
    }

    for (final field in ['lastStationId', 'nextStationId', 'fractionToNext']) {
      test('payload.$field missing makes PositionMessage.fromJson fail', () {
        final json = example('TrainPositionUpdated');
        (json['payload'] as Map<String, dynamic>).remove(field);
        expect(() => PositionMessage.fromJson(json), throwsA(anything));
      });
    }

    test('an unreadable position falls back to "load the journey again", never a crash', () {
      final json = example('TrainPositionUpdated');
      (json['payload'] as Map<String, dynamic>).remove('fractionToNext');
      final message = parseLiveFrame(frameOf('TrainPositionUpdated', json));

      expect(message, isA<ChangeMessage>());
      expect((message as ChangeMessage).type, 'TrainPositionUpdated');
    });
  });

  group('the snapshot journey', () {
    test('an unreadable journey in the snapshot becomes null (the app then loads it itself)', () {
      final json = example('snapshot');
      (json['journey'] as Map<String, dynamic>).remove('stops');
      final message = parseLiveFrame(frameOf('snapshot', json)) as SnapshotMessage;
      expect(message.sequence, 1051);
      expect(message.journey, isNull);
    });

    test('a snapshot without a journey keeps its sequence', () {
      final json = example('snapshot')..remove('journey');
      expect((parseLiveFrame(frameOf('snapshot', json)) as SnapshotMessage).journey, isNull);
    });
  });

  group('unreadable and unknown frames', () {
    test('an event type the app has never seen is a change', () {
      final message = parseLiveFrame(const SseFrame(id: '9', event: 'SomethingNew', data: '{"sequence": 9}'));
      expect((message as ChangeMessage).type, 'SomethingNew');
      expect(message.sequence, 9);
    });

    test('data that is not JSON is a change, with the sequence from the id line', () {
      final message = parseLiveFrame(const SseFrame(id: '12', event: 'DelayChanged', data: 'not json'));
      expect((message as ChangeMessage).sequence, 12);
    });

    test('a snapshot without a sequence is a change, not a crash', () {
      expect(parseLiveFrame(const SseFrame(event: 'snapshot', data: '{}')), isA<ChangeMessage>());
    });
  });

  group('backend/openapi.yaml still describes what the app reads', () {
    final spec = loadYaml(File('../../../backend/openapi.yaml').readAsStringSync()) as YamlMap;
    final schemas = (spec['components'] as YamlMap)['schemas'] as YamlMap;

    test('TrainPositionUpdatedEvent: envelope and payload fields', () {
      final schema = schemas['TrainPositionUpdatedEvent'] as YamlMap?;
      expect(schema, isNotNull);
      final properties = (schema!['properties'] as YamlMap);
      for (final field in ['sequence', 'journeyId', 'occurredAt', 'payload']) {
        expect(properties.keys, contains(field), reason: field);
      }
      final payload = (properties['payload'] as YamlMap)['properties'] as YamlMap;
      for (final field in ['lastStationId', 'nextStationId', 'fractionToNext']) {
        expect(payload.keys, contains(field), reason: 'payload.$field');
      }
    });

    test('JourneySnapshot has a sequence', () {
      final properties = (schemas['JourneySnapshot'] as YamlMap)['properties'] as YamlMap;
      expect(properties.keys, contains('sequence'));
    });

    test('every event of the StreamEvent union has an example file, or is on the list of changes', () {
      final mapping = ((schemas['StreamEvent'] as YamlMap)['discriminator'] as YamlMap)['mapping'] as YamlMap;
      final known = {..._changeTypes, 'TrainPositionUpdated'};
      for (final type in mapping.keys) {
        expect(known, contains(type), reason: 'event $type has no example file in shared/events yet');
      }
    });

    test('GET /api/v1/stream takes journeyIds', () {
      final get = ((spec['paths'] as YamlMap)['/api/v1/stream'] as YamlMap)['get'] as YamlMap;
      final names = [for (final p in get['parameters'] as YamlList) (p as YamlMap)['name']];
      expect(names, contains('journeyIds'));
    });
  });
}
