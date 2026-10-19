import 'freshness.dart';
import 'journey_reason.dart';
import 'journey_status.dart';
import 'journey_stop.dart';
import 'journey_summary.dart';
import 'train_set.dart';

/// Last known position of the train, as the backend resolved it (schema
/// PositionResponse). The app never computes a position: it draws this.
class Position {
  const Position({required this.lastStationId, required this.nextStationId, required this.fractionToNext});

  final String lastStationId;
  final String nextStationId;

  /// 0.0 to 1.0 of the way from the last to the next station.
  final double fractionToNext;

  factory Position.fromJson(Map<String, dynamic> json) => Position(
        lastStationId: json['lastStationId'] as String,
        nextStationId: json['nextStationId'] as String,
        fractionToNext: (json['fractionToNext'] as num).toDouble(),
      );
}

/// A station by id and name (schema JourneyStationResponse).
class StationRef {
  const StationRef({required this.stationId, required this.name});

  final String stationId;
  final String name;

  factory StationRef.fromJson(Map<String, dynamic> json) =>
      StationRef(stationId: json['stationId'] as String, name: json['name'] as String);
}

/// A journey with its stops (`GET /journeys/{id}`, schema JourneyDetailResponse).
/// Only the fields the passenger screens use are read (hold and reason follow in
/// the disruption step).
class JourneyDetail {
  const JourneyDetail({
    required this.journeyId,
    required this.serviceNumber,
    required this.name,
    required this.trainType,
    required this.serviceDate,
    required this.status,
    required this.from,
    required this.to,
    required this.freshness,
    required this.lastPosition,
    required this.hold,
    required this.reason,
    required this.version,
    required this.stops,
    required this.trainSet,
    required this.terminatesAt,
  });

  final String journeyId;
  final String serviceNumber;
  final String name;
  final String trainType;
  final String serviceDate;
  final JourneyStatus status;
  final JourneyFrom from;
  final JourneyTo to;

  /// The tracking state. `updatedAt` is the time of the last accepted GPS
  /// position only, and `lastStationName` the station that position is after;
  /// together they give "Last seen 07:38 near Kelaniya" (S-15).
  final Freshness freshness;

  /// Null before the first position. Only meaningful while tracking is live or stale.
  final Position? lastPosition;

  /// Null unless the controller has set a hold.
  final JourneyHold? hold;

  /// The controller's reason for a delay, cancellation or early end; null when none was given.
  final JourneyReason? reason;
  final int version;
  final List<JourneyStop> stops;

  /// Null when no train set is assigned yet (US-P2 AC4).
  final TrainSet? trainSet;

  /// The calling points, in order (design handoff rule 13).
  List<JourneyStop> get callingPoints => [for (final s in stops) if (s.isCallingPoint) s];

  /// For an early-terminated journey: where it ends, as the backend says (20 Oct).
  /// Null for every other journey. (The schema does not list it as required, so a
  /// missing key counts as null; the openapi test checks that the field still exists.)
  final StationRef? terminatesAt;

  JourneyStop? stopAt(String stationId) {
    for (final s in stops) {
      if (s.stationId == stationId) return s;
    }
    return null;
  }

  /// The same journey with a newer position from the stream. Only the position
  /// and the time the data is from change; everything else stays as loaded.
  JourneyDetail withPosition(Position position, DateTime at) {
    String? lastStation;
    for (final s in stops) {
      if (s.stationId == position.lastStationId) lastStation = s.name;
    }
    return JourneyDetail(
      journeyId: journeyId,
      serviceNumber: serviceNumber,
      name: name,
      trainType: trainType,
      serviceDate: serviceDate,
      status: status,
      from: from,
      to: to,
      freshness: Freshness(freshness.tracking, updatedAt: at, lastSeenAt: at, lastStation: lastStation),
      lastPosition: position,
      hold: hold,
      reason: reason,
      version: version,
      stops: stops,
      trainSet: trainSet,
      terminatesAt: terminatesAt,
    );
  }

  factory JourneyDetail.fromJson(Map<String, dynamic> json) {
    final status = json['status'] as Map<String, dynamic>;
    final freshnessJson = json['freshness'] as Map<String, dynamic>;
    requireKeys(status, ['delayMinutes']);
    requireKeys(freshnessJson, ['tracking', 'updatedAt', 'lastStationName', 'lastPosition']);
    requireKeys(json, ['trainSet', 'hold', 'reason']);

    final stops = [for (final s in json['stops'] as List) JourneyStop.fromJson(s as Map<String, dynamic>)];
    final positionJson = freshnessJson['lastPosition'] as Map<String, dynamic>?;
    final position = positionJson == null ? null : Position.fromJson(positionJson);
    final updatedAt = optionalTime(freshnessJson, 'updatedAt');
    final trainSetJson = json['trainSet'] as Map<String, dynamic>?;
    final terminatesJson = json['terminatesAt'] as Map<String, dynamic>?;

    return JourneyDetail(
      journeyId: json['journeyId'] as String,
      serviceNumber: json['serviceNumber'] as String,
      name: json['name'] as String,
      trainType: json['trainType'] as String,
      serviceDate: json['serviceDate'] as String,
      status: JourneyStatus.fromJson(status),
      from: JourneyFrom.fromJson(json['from'] as Map<String, dynamic>),
      to: JourneyTo.fromJson(json['to'] as Map<String, dynamic>),
      freshness: Freshness(
        Tracking.parse(freshnessJson['tracking'] as String),
        updatedAt: updatedAt,
        lastSeenAt: updatedAt,
        lastStation: freshnessJson['lastStationName'] as String?,
      ),
      lastPosition: position,
      hold: holdFrom(json['hold']),
      reason: reasonFrom(json['reason']),
      version: json['version'] as int,
      stops: stops,
      trainSet: trainSetJson == null ? null : TrainSet.fromJson(trainSetJson),
      terminatesAt: terminatesJson == null ? null : StationRef.fromJson(terminatesJson),
    );
  }
}
