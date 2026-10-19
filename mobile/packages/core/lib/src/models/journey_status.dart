/// The status kinds the API sends (API contract §5). Names match the JSON strings.
enum StatusKind {
  onTime('OnTime'),
  late('Late'),
  early('Early'),
  cancelled('Cancelled'),
  terminatedEarly('TerminatedEarly'),
  held('Held'),
  notStarted('NotStarted'),
  completed('Completed');

  const StatusKind(this.json);
  final String json;

  static StatusKind parse(String value) => StatusKind.values.firstWhere(
        (k) => k.json == value,
        orElse: () => throw FormatException('Unknown status kind "$value"'),
      );
}

/// `status` of a journey: a kind plus, for some kinds, whole minutes of delay.
/// The app only displays it; the backend decides it (CLAUDE.md §3).
class JourneyStatus {
  const JourneyStatus(this.kind, {this.delayMinutes});

  final StatusKind kind;

  /// Negative = early (R-01). Null when the kind carries no delay.
  final int? delayMinutes;

  /// What to SHOW when the controller has set a hold (the API's `hold` field is set): the Held status (fixtures S-21 and S-08), keeping
  /// the delay the API sent. The backend sends a hold as a flag, not as a status (D-4, contract §5), so the badge would otherwise say
  /// "On time". A cancelled, ended-early or completed journey keeps its own status. Nothing is calculated: no minutes are added (R-22).
  JourneyStatus shownWithHold(Object? hold) {
    if (hold == null) return this;
    return switch (kind) {
      StatusKind.cancelled || StatusKind.terminatedEarly || StatusKind.completed => this,
      _ => JourneyStatus(StatusKind.held, delayMinutes: delayMinutes),
    };
  }

  factory JourneyStatus.fromJson(Map<String, dynamic> json) {
    final kind = StatusKind.parse(json['kind'] as String);
    final delay = json['delayMinutes'] as int?;
    // Strict on purpose: a Late / Early status without minutes must never be
    // shown as something else, least of all "On time" (US-P4 ★).
    if ((kind == StatusKind.late || kind == StatusKind.early) && delay == null) {
      throw FormatException('Status ${kind.json} needs delayMinutes');
    }
    return JourneyStatus(kind, delayMinutes: delay);
  }
}
