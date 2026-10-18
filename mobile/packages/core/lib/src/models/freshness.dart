/// Tracking state of the live data (domain model 4.4). The backend decides it;
/// the app shows it as sent (no thresholds in the app).
enum Tracking {
  notStarted('NotStarted'),
  live('Live'),
  stale('Stale'),
  lost('Lost'),
  ended('Ended');

  const Tracking(this.json);
  final String json;

  static Tracking parse(String value) => Tracking.values.firstWhere(
        (t) => t.json == value,
        orElse: () => throw FormatException('Unknown tracking "$value"'),
      );
}

/// `freshness` of a journey summary / detail.
class Freshness {
  const Freshness(this.tracking, {this.updatedAt, this.lastSeenAt, this.lastStation});

  final Tracking tracking;
  final DateTime? updatedAt;
  final DateTime? lastSeenAt;

  /// Name of the station near the last position (for "Last seen 07:38 near Kelaniya").
  final String? lastStation;

  factory Freshness.fromJson(Map<String, dynamic> json) => Freshness(
        Tracking.parse(json['tracking'] as String),
        updatedAt: _optionalTime(json['updatedAt']),
        lastSeenAt: _optionalTime(json['lastSeenAt']),
        lastStation: json['lastStation'] as String?,
      );
}

DateTime? _optionalTime(Object? value) => value == null ? null : DateTime.parse(value as String);
