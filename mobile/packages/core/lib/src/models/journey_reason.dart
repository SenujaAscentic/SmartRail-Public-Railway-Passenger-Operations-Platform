/// The controller's reason for a delay, cancellation, early end or hold
/// (schema JourneyReasonResponse): a code and an optional plain-text note.
class JourneyReason {
  const JourneyReason({required this.code, required this.note});

  /// SignalFailure, TrackWork, TrainFault, Weather, CrewShortage, Congestion or Other
  /// (API contract §3.3). A code the app does not know is shown as sent.
  final String code;

  /// Plain text (never HTML), at most 300 characters; null when the controller gave none.
  final String? note;

  factory JourneyReason.fromJson(Map<String, dynamic> json) {
    // The note is always present, null when there is none.
    if (!json.containsKey('note')) throw const FormatException('"note" is missing');
    return JourneyReason(code: json['code'] as String, note: json['note'] as String?);
  }
}

/// A hold: the train waits at [stationId] (schema JourneyHoldResponse). It is
/// only there when the controller has really set one.
class JourneyHold {
  const JourneyHold({required this.stationId, required this.minutes, required this.reason});

  final String stationId;
  final int? minutes;
  final JourneyReason? reason;

  factory JourneyHold.fromJson(Map<String, dynamic> json) {
    for (final key in ['minutes', 'reason']) {
      if (!json.containsKey(key)) throw FormatException('"$key" is missing');
    }
    final reason = json['reason'] as Map<String, dynamic>?;
    return JourneyHold(
      stationId: json['stationId'] as String,
      minutes: json['minutes'] as int?,
      reason: reason == null ? null : JourneyReason.fromJson(reason),
    );
  }
}
