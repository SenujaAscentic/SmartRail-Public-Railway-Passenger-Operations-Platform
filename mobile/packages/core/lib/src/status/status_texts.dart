import '../models/freshness.dart';
import '../models/journey_status.dart';
import '../time/colombo_time.dart';

// Every wording here comes from shared/status-fixtures.json, and the tests run
// every fixture against these functions (CLAUDE.md §5.6). Change the fixture
// and the code together. Nothing here calculates a delay or a status.

String statusText(JourneyStatus status, {String? terminatesAt}) {
  final delay = status.delayMinutes;
  switch (status.kind) {
    case StatusKind.onTime:
      return 'On time';
    case StatusKind.late:
      return '$delay min late';
    case StatusKind.early:
      return '${delay!.abs()} min early';
    case StatusKind.cancelled:
      return 'Cancelled';
    case StatusKind.terminatedEarly:
      return terminatesAt == null ? 'Terminates early' : 'Terminates at $terminatesAt';
    case StatusKind.held:
      // The delay is only added when it is a positive number of minutes.
      return (delay != null && delay > 0) ? 'Held · $delay min late' : 'Held';
    case StatusKind.notStarted:
      return 'Not started';
    case StatusKind.completed:
      return 'Completed';
  }
}

/// "Platform 5 (was 2)" or "Platform 3".
String platformText(String platform, String? previousPlatform) =>
    previousPlatform == null ? 'Platform $platform' : 'Platform $platform (was $previousPlatform)';

/// Text for a stop that is skipped or added; null for every other stop status.
String? stopStatusText(String stopStatus) => switch (stopStatus) {
      'Skipped' => 'Does not stop',
      'Added' => 'Extra stop',
      _ => null,
    };

/// "30 s" below one minute, then whole minutes ("4 min"). Rounded down.
String ageLabel(Duration age) =>
    age.inSeconds < 60 ? '${age.inSeconds} s' : '${age.inMinutes} min';

/// Text for the tracking state. [age] is how old the data is, counted by the
/// app's own stopwatch (Q4); null when unknown, and then no age is shown.
/// Returns null when there is nothing to say (not started, ended, live without an age).
String? freshnessText(Freshness freshness, {Duration? age}) {
  switch (freshness.tracking) {
    case Tracking.live:
      return age == null ? null : 'Updated ${ageLabel(age)} ago';
    case Tracking.stale:
      final seen = freshness.lastSeenAt;
      if (seen == null) return null;
      final station = freshness.lastStation;
      return station == null
          ? 'Last seen ${formatTime(seen)}'
          : 'Last seen ${formatTime(seen)} near $station';
    case Tracking.lost:
      return 'Live location unavailable – scheduled times shown';
    case Tracking.notStarted:
    case Tracking.ended:
      return null;
  }
}

/// Shown when the phone itself has no connection.
String offlineText(Duration age) => "You're offline · updated ${ageLabel(age)} ago";
