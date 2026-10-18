import 'freshness.dart';
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
    required this.version,
    required this.stops,
    required this.trainSet,
  });

  final String journeyId;
  final String serviceNumber;
  final String name;
  final String trainType;
  final String serviceDate;
  final JourneyStatus status;
  final JourneyFrom from;
  final JourneyTo to;

  /// The tracking state with the time the data is from. For a stale train,
  /// `lastSeenAt` is the backend's `updatedAt` and `lastStation` is the name of
  /// the station the last position is after.
  final Freshness freshness;

  /// Null before the first position. Only meaningful while tracking is live or stale.
  final Position? lastPosition;
  final int version;
  final List<JourneyStop> stops;

  /// Null when no train set is assigned yet (US-P2 AC4).
  final TrainSet? trainSet;

  /// The calling points, in order (design handoff rule 13).
  List<JourneyStop> get callingPoints => [for (final s in stops) if (s.isCallingPoint) s];

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
      version: version,
      stops: stops,
      trainSet: trainSet,
    );
  }

  factory JourneyDetail.fromJson(Map<String, dynamic> json) {
    final status = json['status'] as Map<String, dynamic>;
    final freshnessJson = json['freshness'] as Map<String, dynamic>;
    requireKeys(status, ['delayMinutes']);
    requireKeys(freshnessJson, ['tracking', 'updatedAt', 'lastPosition']);
    requireKeys(json, ['trainSet']);

    final stops = [for (final s in json['stops'] as List) JourneyStop.fromJson(s as Map<String, dynamic>)];
    final positionJson = freshnessJson['lastPosition'] as Map<String, dynamic>?;
    final position = positionJson == null ? null : Position.fromJson(positionJson);
    final updatedAt = optionalTime(freshnessJson, 'updatedAt');
    String? lastStation;
    if (position != null) {
      for (final s in stops) {
        if (s.stationId == position.lastStationId) lastStation = s.name;
      }
    }
    final trainSetJson = json['trainSet'] as Map<String, dynamic>?;

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
        lastStation: lastStation,
      ),
      lastPosition: position,
      version: json['version'] as int,
      stops: stops,
      trainSet: trainSetJson == null ? null : TrainSet.fromJson(trainSetJson),
    );
  }
}
