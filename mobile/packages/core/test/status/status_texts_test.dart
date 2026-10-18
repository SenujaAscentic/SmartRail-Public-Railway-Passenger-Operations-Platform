import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

// Runs EVERY case of shared/status-fixtures.json. A case with a shape this test
// does not know fails (so a new fixture cannot be silently ignored).
// flutter test runs with the package folder as working directory.
const _fixturePath = '../../../shared/status-fixtures.json';

// Fixtures for the station displays; the passenger app has no such screen.
const _displayOnly = {'S-18', 'S-19', 'S-20'};

void main() {
  final json = jsonDecode(File(_fixturePath).readAsStringSync()) as Map<String, dynamic>;
  final cases = (json['cases'] as List).cast<Map<String, dynamic>>();
  final now = DateTime.parse(json['now'] as String);

  test('the fixture file has cases', () => expect(cases, isNotEmpty));

  for (final c in cases) {
    final id = c['id'] as String;
    if (_displayOnly.contains(id)) continue;

    test('fixture $id', () {
      final input = c['input'] as Map<String, dynamic>;
      final expected = (c['expected'] as Map<String, dynamic>)['text'] as String;
      expect(_render(input, now), expected);
    });
  }
}

String _render(Map<String, dynamic> input, DateTime now) {
  if (input.containsKey('status')) {
    return statusText(
      JourneyStatus.fromJson(input['status'] as Map<String, dynamic>),
      terminatesAt: input['terminatesAt'] as String?,
    );
  }
  if (input.containsKey('platform')) {
    return platformText(input['platform'] as String, input['previousPlatform'] as String?);
  }
  if (input.containsKey('stopStatus')) {
    return stopStatusText(input['stopStatus'] as String)!;
  }
  if (input.containsKey('freshness')) {
    final f = Freshness.fromJson(input['freshness'] as Map<String, dynamic>);
    // The fixture gives updatedAt and a fixed "now"; in the app this age comes
    // from a stopwatch, so here it is derived from the two fixture values.
    final updated = f.updatedAt;
    // null (nothing to say) is written as an empty text in the fixtures.
    return freshnessText(f, age: updated == null ? null : now.difference(updated)) ?? '';
  }
  if (input.containsKey('offline')) {
    return offlineText(Duration(seconds: input['secondsSinceLastUpdate'] as int));
  }
  fail('Unknown fixture input shape: $input');
}
