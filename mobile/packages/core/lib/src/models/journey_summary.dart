import 'freshness.dart';
import 'journey_reason.dart';
import 'journey_status.dart';

/// Where the passenger boards: the `from` part of a journey summary.
class JourneyFrom {
  const JourneyFrom({
    required this.stationId,
    required this.name,
    required this.scheduledDeparture,
    required this.expectedDeparture,
    required this.platform,
    required this.previousPlatform,
  });

  final String stationId;
  final String name;
  final DateTime scheduledDeparture;
  final DateTime? expectedDeparture;

  /// Null when the backend sends an empty platform (not known yet).
  final String? platform;
  final String? previousPlatform;

  factory JourneyFrom.fromJson(Map<String, dynamic> json) {
    // The backend always sends these keys, null when unknown; an absent key
    // means the contract changed and must not pass as "unknown".
    requireKeys(json, ['expectedDeparture', 'previousPlatform']);
    return JourneyFrom(
      stationId: json['stationId'] as String,
      name: json['name'] as String,
      scheduledDeparture: requiredTime(json, 'scheduledDeparture'),
      expectedDeparture: optionalTime(json, 'expectedDeparture'),
      platform: emptyToNull(json['platform'] as String),
      previousPlatform: json['previousPlatform'] as String?,
    );
  }
}

/// Where the passenger gets off: the `to` part of a journey summary.
class JourneyTo {
  const JourneyTo({
    required this.stationId,
    required this.name,
    required this.scheduledArrival,
    required this.expectedArrival,
  });

  final String stationId;
  final String name;
  final DateTime scheduledArrival;
  final DateTime? expectedArrival;

  factory JourneyTo.fromJson(Map<String, dynamic> json) {
    requireKeys(json, ['expectedArrival']);
    return JourneyTo(
      stationId: json['stationId'] as String,
      name: json['name'] as String,
      scheduledArrival: requiredTime(json, 'scheduledArrival'),
      expectedArrival: optionalTime(json, 'expectedArrival'),
    );
  }
}

/// One search result (`GET /journeys/search`, schema JourneySummaryResponse).
/// Only the fields the passenger screens use are read; unknown fields (hold,
/// reason, ...) are ignored until a screen needs them.
class JourneySummary {
  const JourneySummary({
    required this.journeyId,
    required this.serviceNumber,
    required this.name,
    required this.trainType,
    required this.serviceDate,
    required this.status,
    required this.from,
    required this.to,
    required this.freshness,
    required this.hold,
    required this.reason,
    required this.version,
  });

  final String journeyId;
  final String serviceNumber;

  /// Already contains the type, e.g. "Kandy Express".
  final String name;
  final String trainType;

  /// `YYYY-MM-DD`.
  final String serviceDate;
  final JourneyStatus status;
  final JourneyFrom from;
  final JourneyTo to;
  final Freshness freshness;

  /// Null unless the controller has set a hold.
  final JourneyHold? hold;

  /// The controller's reason for a delay, cancellation or early end; null when none was given.
  final JourneyReason? reason;

  /// Control version (R-18). Read-only for passengers.
  final int version;

  factory JourneySummary.fromJson(Map<String, dynamic> json) {
    final status = json['status'] as Map<String, dynamic>;
    final freshness = json['freshness'] as Map<String, dynamic>;
    requireKeys(status, ['delayMinutes']);
    requireKeys(freshness, ['updatedAt']);
    requireKeys(json, ['hold', 'reason']);
    return JourneySummary(
      journeyId: json['journeyId'] as String,
      serviceNumber: json['serviceNumber'] as String,
      name: json['name'] as String,
      trainType: json['trainType'] as String,
      serviceDate: json['serviceDate'] as String,
      status: JourneyStatus.fromJson(status),
      from: JourneyFrom.fromJson(json['from'] as Map<String, dynamic>),
      to: JourneyTo.fromJson(json['to'] as Map<String, dynamic>),
      freshness: Freshness.fromJson(freshness),
      hold: holdFrom(json['hold']),
      reason: reasonFrom(json['reason']),
      version: json['version'] as int,
    );
  }
}

JourneyHold? holdFrom(Object? json) => json == null ? null : JourneyHold.fromJson(json as Map<String, dynamic>);

JourneyReason? reasonFrom(Object? json) => json == null ? null : JourneyReason.fromJson(json as Map<String, dynamic>);

/// Throws when a key is absent. A value of null is fine; a missing key is not.
void requireKeys(Map<String, dynamic> json, List<String> keys) {
  for (final key in keys) {
    if (!json.containsKey(key)) throw FormatException('"$key" is missing');
  }
}

/// A time that must be present: a missing or null value is an error, because a
/// journey card without its scheduled time would show nothing useful.
DateTime requiredTime(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value == null) throw FormatException('"$key" is missing or null');
  return DateTime.parse(value as String);
}

DateTime? optionalTime(Map<String, dynamic> json, String key) {
  final value = json[key];
  return value == null ? null : DateTime.parse(value as String);
}

String? emptyToNull(String value) => value.isEmpty ? null : value;
