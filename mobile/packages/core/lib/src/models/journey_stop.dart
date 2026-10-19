import 'journey_summary.dart';

/// Whether the train stops at a station or runs through it (JourneyStop.callType).
enum CallType {
  stop('Stop'),
  pass('Pass');

  const CallType(this.json);
  final String json;

  static CallType parse(String value) => CallType.values.firstWhere(
        (c) => c.json == value,
        orElse: () => throw FormatException('Unknown call type "$value"'),
      );
}

/// State of one stop of a journey (JourneyStop.status, domain model 4.3).
enum StopState {
  scheduled('Scheduled'),
  arrived('Arrived'),
  departed('Departed'),
  passed('Passed'),
  skipped('Skipped');

  const StopState(this.json);
  final String json;

  static StopState parse(String value) => StopState.values.firstWhere(
        (s) => s.json == value,
        orElse: () => throw FormatException('Unknown stop status "$value"'),
      );
}

/// One stop of a journey in `GET /journeys/{id}` (schema JourneyStopResponse).
class JourneyStop {
  const JourneyStop({
    required this.sequence,
    required this.stationId,
    required this.name,
    required this.callType,
    required this.state,
    required this.isAdded,
    required this.scheduledArrival,
    required this.expectedArrival,
    required this.actualArrival,
    required this.scheduledDeparture,
    required this.expectedDeparture,
    required this.actualDeparture,
    required this.platform,
    required this.previousPlatform,
  });

  final int sequence;
  final String stationId;
  final String name;
  final CallType callType;
  final StopState state;
  final bool isAdded;
  final DateTime? scheduledArrival;
  final DateTime? expectedArrival;
  final DateTime? actualArrival;
  final DateTime? scheduledDeparture;
  final DateTime? expectedDeparture;
  final DateTime? actualDeparture;

  /// Null when the backend sends an empty platform.
  final String? platform;
  final String? previousPlatform;

  /// The passenger stop list shows calling points only (design handoff rule 13);
  /// a skipped stop stays on it, marked "Does not stop" (US-P4 AC5).
  bool get isCallingPoint => callType == CallType.stop || state == StopState.skipped;

  factory JourneyStop.fromJson(Map<String, dynamic> json) {
    // Every time is always present in the answer, null when unknown; an absent
    // key means the contract changed.
    requireKeys(json, [
      'scheduledArrival',
      'expectedArrival',
      'actualArrival',
      'scheduledDeparture',
      'expectedDeparture',
      'actualDeparture',
      'previousPlatform',
    ]);
    return JourneyStop(
      sequence: json['sequence'] as int,
      stationId: json['stationId'] as String,
      name: json['name'] as String,
      callType: CallType.parse(json['callType'] as String),
      state: StopState.parse(json['status'] as String),
      isAdded: json['isAdded'] as bool,
      scheduledArrival: optionalTime(json, 'scheduledArrival'),
      expectedArrival: optionalTime(json, 'expectedArrival'),
      actualArrival: optionalTime(json, 'actualArrival'),
      scheduledDeparture: optionalTime(json, 'scheduledDeparture'),
      expectedDeparture: optionalTime(json, 'expectedDeparture'),
      actualDeparture: optionalTime(json, 'actualDeparture'),
      platform: emptyToNull(json['platform'] as String),
      previousPlatform: json['previousPlatform'] as String?,
    );
  }
}
