import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/services.dart' show FontLoader;
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

/// A stand-in for the SmartRail backend (same idea as ops-web's fakes): it
/// answers the public endpoints from lists the test sets up, and remembers the
/// requests it got.
class FakeBackend {
  FakeBackend() {
    client = MockClient(_handle);
  }

  late final MockClient client;

  final stations = <Map<String, String>>[
    station('9a4e2d71-5b38-4c06-8e1f-6a0d3b7c9e24', 'GAM', 'Gampaha'),
    station('c7d05f38-1e92-4ab6-9d70-4f8a2c1e6b35', 'KDY', 'Kandy'),
    station('3f1b6a52-8c0d-4e7a-b1f4-2d9c5e7a0b13', 'FOT', 'Colombo Fort'),
  ];

  /// Answer of `GET /journeys/search`.
  List<Map<String, dynamic>> journeys = [];

  /// While true, the journey search answers 500.
  bool failJourneySearch = false;

  final requests = <Uri>[];

  Future<http.Response> _handle(http.Request request) async {
    requests.add(request.url);
    switch (request.url.path) {
      case '/api/v1/stations':
        final query = (request.url.queryParameters['query'] ?? '').toLowerCase();
        final found = stations.where((s) => s['name']!.toLowerCase().contains(query)).toList();
        return _json(found);
      case '/api/v1/journeys/search':
        if (failJourneySearch) return http.Response('boom', 500);
        return _json(journeys);
    }
    return http.Response('not found', 404);
  }

  http.Response _json(Object body) =>
      http.Response(jsonEncode(body), 200, headers: {'content-type': 'application/json; charset=utf-8'});
}

Map<String, String> station(String id, String code, String name) => {'id': id, 'code': code, 'name': name};

/// One JourneySummaryResponse as the backend sends it (API contract §5).
/// Times are UTC strings; Colombo is +05:30.
Map<String, dynamic> journeyJson({
  String id = '0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01',
  String serviceNumber = '1001',
  String name = 'Kandy Express',
  Map<String, dynamic> status = const {'kind': 'OnTime', 'delayMinutes': 0},
  String scheduledDeparture = '2026-10-14T11:32:00Z',
  String? expectedDeparture = '2026-10-14T11:32:00Z',
  String scheduledArrival = '2026-10-14T13:35:00Z',
  String? expectedArrival = '2026-10-14T13:35:00Z',
  String platform = '2',
  String? previousPlatform,
}) =>
    {
      'journeyId': id,
      'serviceNumber': serviceNumber,
      'name': name,
      'trainType': 'Express',
      'serviceDate': '2026-10-14',
      'status': status,
      'from': {
        'stationId': '9a4e2d71-5b38-4c06-8e1f-6a0d3b7c9e24',
        'name': 'Gampaha',
        'scheduledDeparture': scheduledDeparture,
        'expectedDeparture': expectedDeparture,
        'platform': platform,
        'previousPlatform': previousPlatform,
      },
      'to': {
        'stationId': 'c7d05f38-1e92-4ab6-9d70-4f8a2c1e6b35',
        'name': 'Kandy',
        'scheduledArrival': scheduledArrival,
        'expectedArrival': expectedArrival,
      },
      'freshness': {'tracking': 'Live', 'updatedAt': '2026-10-14T11:30:00Z'},
      'hold': null,
      'reason': null,
      'version': 1,
    };

/// Loads the bundled Noto fonts so widths in tests match the real app.
/// flutter test runs with the app folder as working directory.
Future<void> loadAppFonts() async {
  Future<void> load(String family, List<String> files) async {
    final loader = FontLoader('packages/smartrail_core/$family');
    for (final f in files) {
      final bytes = File('../../packages/core/fonts/$f').readAsBytesSync();
      loader.addFont(Future.value(ByteData.sublistView(bytes)));
    }
    await loader.load();
  }

  await load('NotoSans', ['NotoSans-Regular.ttf', 'NotoSans-Bold.ttf']);
  await load('NotoSansMono', ['NotoSansMono-Regular.ttf', 'NotoSansMono-Bold.ttf']);
}
