import 'package:flutter/material.dart';

import '../models/journey_status.dart';
import '../status/status_texts.dart';
import '../theme/app_theme.dart';
import '../theme/tokens.dart';
import '../time/colombo_time.dart';
import 'status_badge.dart';

/// Text of the "not running today" badge (US-P6). Not in
/// shared/status-fixtures.json yet: pending Senuja's approval.
const doesNotRunTodayText = 'Does not run today';

/// One search result: times, status, train and platform. Shows what the backend
/// sent; it calculates nothing. The scheduled times appear (struck through,
/// in a second line) only when they differ from the expected ones.
class JourneyCard extends StatelessWidget {
  const JourneyCard({
    super.key,
    required this.name,
    required this.trainType,
    required this.status,
    required this.scheduledDeparture,
    required this.scheduledArrival,
    this.expectedDeparture,
    this.expectedArrival,
    this.platform,
    this.previousPlatform,
    this.notRunningToday = false,
    this.onTap,
  });

  final String name;
  final String trainType;
  final JourneyStatus status;
  final DateTime scheduledDeparture;
  final DateTime scheduledArrival;
  final DateTime? expectedDeparture;
  final DateTime? expectedArrival;
  final String? platform;
  final String? previousPlatform;

  /// The saved train does not run today (calendar): no live status is shown.
  final bool notRunningToday;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final cancelled = status.kind == StatusKind.cancelled;
    final muted = cancelled || notRunningToday;

    final shownDeparture = cancelled || notRunningToday ? scheduledDeparture : (expectedDeparture ?? scheduledDeparture);
    final shownArrival = cancelled || notRunningToday ? scheduledArrival : (expectedArrival ?? scheduledArrival);
    final differs = !muted &&
        (formatTime(shownDeparture) != formatTime(scheduledDeparture) ||
            formatTime(shownArrival) != formatTime(scheduledArrival));

    final timeStyle = monoStyle(context, size: textTime, weight: FontWeight.w700).copyWith(
      color: muted ? scheme.onSurfaceVariant : scheme.onSurface,
      decoration: cancelled ? TextDecoration.lineThrough : null,
    );
    final small = Theme.of(context).textTheme.bodySmall!;

    final content = Padding(
      padding: const EdgeInsets.all(spaceMd),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text('${formatTime(shownDeparture)} → ${formatTime(shownArrival)}', style: timeStyle),
              ),
              const SizedBox(width: spaceSm),
              notRunningToday
                  ? const StatusBadge.neutral(doesNotRunTodayText, icon: Icons.event_busy)
                  : StatusBadge.journey(status),
            ],
          ),
          if (differs)
            Text(
              '${formatTime(scheduledDeparture)} → ${formatTime(scheduledArrival)} scheduled',
              style: monoStyle(context, size: textSmall).copyWith(
                color: scheme.onSurfaceVariant,
                decoration: TextDecoration.lineThrough,
              ),
            ),
          const SizedBox(height: spaceSm),
          Row(
            children: [
              Expanded(
                child: Text.rich(
                  TextSpan(children: [
                    TextSpan(text: name, style: Theme.of(context).textTheme.labelLarge!.copyWith(fontSize: textSmall)),
                    TextSpan(text: ' · $trainType', style: small),
                  ]),
                ),
              ),
              if (platform != null) Text(platformText(platform!, previousPlatform), style: small),
            ],
          ),
        ],
      ),
    );

    return Card(
      margin: EdgeInsets.zero,
      elevation: 0,
      color: scheme.surfaceContainer,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(radiusLg),
        side: BorderSide(color: scheme.outline),
      ),
      clipBehavior: Clip.antiAlias,
      child: onTap == null ? content : InkWell(onTap: onTap, child: content),
    );
  }
}
