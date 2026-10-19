import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/services.dart' show FontLoader;
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

/// A stand-in for the SmartRail backend (same idea as ops-web's fakes): it
/// answers the public endpoints from lists the test sets up, and remembers the
/// requests it got.
/// One open connection to the stream endpoint.
class FakeStream {
  FakeStream(this.request);

  final http.BaseRequest request;
  final controller = StreamController<List<int>>();

  String? get lastEventId => request.headers['Last-Event-ID'];

  /// Whether the app still listens (false after it cancelled or closed the connection).
  bool get listening => controller.hasListener;

  /// Sends one frame; [data] is encoded as JSON.
  void frame({String? id, required String event, required Object data}) {
    final lines = [if (id != null) 'id: $id', 'event: $event', 'data: ${jsonEncode(data)}'];
    controller.add(utf8.encode('${lines.join('\n')}\n\n'));
  }

  /// The heartbeat comment line.
  void heartbeat() => controller.add(utf8.encode(': keep-alive\n\n'));

  /// The server closes the connection.
  void close() => controller.close();
}

class FakeBackend {
  FakeBackend() {
    client = MockClient.streaming(_handleStreaming);
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

  /// Answer of `GET /journeys/{id}`; null means 404.
  Map<String, dynamic>? detail;

  /// While true, `GET /journeys/{id}` answers 500.
  bool failJourney = false;

  /// The HTTP `Date` header of the journey answer (the server's time); null leaves it out.
  String? dateHeader;

  /// Catering stations for `GET /stations/{id}` (id -> `cateringStation`); any other id answers 404.
  /// Station ids in [failStations] answer 500.
  final cateringStations = <String, bool>{};
  final failStations = <String>{};

  /// Answer of `GET /stations/{id}/catering`; null answers 404, [failCatering] answers 500.
  Map<String, dynamic>? catering;
  bool failCatering = false;

  /// The HTTP `Date` header of the catering answer (the server's time); null leaves it out.
  String? cateringDateHeader;

  /// Plain requests (stations, search, journey). Stream connections are in [streams].
  final requests = <Uri>[];

  /// Every connection to `GET /stream`, oldest first. A test sends frames with
  /// `streams.last.frame(...)` and ends one with `close()`.
  final streams = <FakeStream>[];

  /// While true, `GET /stream` answers 503 instead of opening.
  bool refuseStream = false;

  /// The HTTP `Date` header of the stream answer (the server time at connect); null leaves it out.
  String? streamDateHeader;

  Future<http.StreamedResponse> _handleStreaming(http.BaseRequest request, http.ByteStream body) async {
    if (request.url.path == '/api/v1/stream') {
      if (refuseStream) return http.StreamedResponse(const Stream.empty(), 503, request: request);
      final stream = FakeStream(request);
      streams.add(stream);
      return http.StreamedResponse(
        stream.controller.stream,
        200,
        headers: {'content-type': 'text/event-stream', 'date': ?streamDateHeader},
        request: request,
      );
    }
    final response = await _handle(request);
    return http.StreamedResponse(
      http.ByteStream.fromBytes(response.bodyBytes),
      response.statusCode,
      headers: response.headers,
      request: request,
    );
  }

  Future<http.Response> _handle(http.BaseRequest request) async {
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
    final stationPath = RegExp(r'^/api/v1/stations/([^/]+)(/catering)?$').firstMatch(request.url.path);
    if (stationPath != null) {
      final id = stationPath.group(1)!;
      if (stationPath.group(2) != null) {
        if (failCatering) return http.Response('boom', 500);
        final body = catering;
        if (body == null) return http.Response('{"code":"NOT_FOUND","title":"Not found"}', 404);
        return http.Response(jsonEncode(body), 200, headers: {
          'content-type': 'application/json; charset=utf-8',
          'date': ?cateringDateHeader,
        });
      }
      if (failStations.contains(id)) return http.Response('boom', 500);
      final flag = cateringStations[id];
      if (flag == null) return http.Response('{"code":"NOT_FOUND","title":"Not found"}', 404);
      return _json({'stationId': id, 'code': 'XXX', 'name': 'Station', 'cateringStation': flag});
    }
    if (request.url.path.startsWith('/api/v1/journeys/')) {
      if (failJourney) return http.Response('boom', 500);
      final body = detail;
      if (body == null) return http.Response('{"code":"NOT_FOUND","title":"Not found"}', 404);
      return http.Response(jsonEncode(body), 200, headers: {
        'content-type': 'application/json; charset=utf-8',
        'date': ?dateHeader,
      });
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

/// A real answer of `GET /journeys/{id}` (train 1001, moving between Kelaniya
/// and Ragama), copied from the backend into core's contract fixtures. A fresh
/// copy each call, so a test can change it.
Map<String, dynamic> detailJson() =>
    jsonDecode(File('../../packages/core/test/fixtures/journey_detail.json').readAsStringSync()) as Map<String, dynamic>;

/// The stop with [name] in a detail answer.
Map<String, dynamic> stopNamed(Map<String, dynamic> detail, String name) =>
    (detail['stops'] as List).cast<Map<String, dynamic>>().firstWhere((s) => s['name'] == name);

/// Station ids used in the detail answer.
const gampahaId = '01a14eb2-015b-7b5a-a2da-49d81f392430';
const kandyId = '01a14eb2-015b-7254-ae7a-ded485b163b9';

/// Station ids of the detail answer that have catering in the catering tests (US-P8).
const polgahawelaId = '01a14eb2-015b-7a7e-bd97-4eb8ac4d62df';
const peradeniyaId = '01a14eb2-015b-7829-b54b-ed67a63fae30';

/// `GET /stations/{id}/catering` as the backend sends it (the shape agreed for US-P8, see mobile/NOTES.md).
Map<String, dynamic> cateringJson({
  bool available = true,
  String? hotline = '+94112345678',
  Map<String, String>? openingHours = const {'opens': '06:00', 'closes': '20:00'},
  List<Map<String, dynamic>>? menu,
}) =>
    {
      'stationId': polgahawelaId,
      'available': available,
      'hotline': hotline,
      'openingHours': openingHours,
      'menu': menu ??
          [
            {'name': 'Chicken rice', 'price': 450, 'available': true},
            {'name': 'Egg roti', 'price': 120.5, 'available': false},
          ],
    };
