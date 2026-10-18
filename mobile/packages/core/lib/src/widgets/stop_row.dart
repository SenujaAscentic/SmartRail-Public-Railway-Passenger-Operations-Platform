import 'package:flutter/material.dart';

import '../models/journey_status.dart';
import '../strings.dart';
import '../status/status_texts.dart';
import '../theme/app_theme.dart';
import '../theme/status_theme.dart';
import '../theme/tokens.dart';
import '../time/colombo_time.dart';
import 'status_badge.dart';

/// State of one calling point on the stop list (API contract §5, JourneyStop.status).
enum StopState { scheduled, arrived, departed, passed, skipped }

/// One calling point: name, scheduled and expected time, optional platform.
/// Passed and departed stops are greyed, the stop the train is at is outlined,
/// a skipped stop is struck through with "Does not stop".
class StopRow extends StatelessWidget {
  const StopRow({
    super.key,
    required this.name,
    required this.state,
    required this.scheduled,
    this.expected,
    this.platform,
    this.previousPlatform,
    this.isAdded = false,
    this.isYourStation = false,
    this.isDestination = false,
    this.foodAvailable = false,
    this.journeyStatus,
  });

  final String name;
  final StopState state;
  final DateTime scheduled;
  final DateTime? expected;

  /// Shown under the name; the screen decides at which stops (the hi-fi shows it at the passenger's station).
  final String? platform;
  final String? previousPlatform;
  final bool isAdded;
  final bool isYourStation;
  final bool isDestination;

  /// Shows "Food available at" plus the station name (US-P8).
  final bool foodAvailable;

  /// The journey's status, only used to colour the expected time like a delay.
  final JourneyStatus? journeyStatus;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final statuses = Theme.of(context).extension<StatusTheme>()!;
    final textTheme = Theme.of(context).textTheme;

    final done = state == StopState.departed || state == StopState.passed;
    final skipped = state == StopState.skipped;
    final here = state == StopState.arrived;

    final shownExpected = expected ?? scheduled;
    final differs = formatTime(shownExpected) != formatTime(scheduled);
    final late = journeyStatus?.kind == StatusKind.late || journeyStatus?.kind == StatusKind.held;
    final expectedColor = done
        ? scheme.onSurfaceVariant
        : (late ? statuses.forKind(StatusKind.late).fg : scheme.onSurface);

    final nameStyle = textTheme.bodyLarge!.copyWith(
      fontWeight: (here || isYourStation || isDestination) ? FontWeight.w700 : null,
      color: done ? scheme.onSurfaceVariant : scheme.onSurface,
      decoration: skipped ? TextDecoration.lineThrough : null,
    );
    final timeStyle = monoStyle(context, size: textBody, weight: FontWeight.w700);

    final subtitle = <Widget>[
      if (isYourStation || isDestination || platform != null)
        Text(
          [
            if (isYourStation) Strings.yourStation,
            if (isDestination) Strings.destination,
            if (platform != null && previousPlatform == null) platformText(platform!, null),
          ].join(' · '),
          style: textTheme.bodySmall,
        ),
      if (platform != null && previousPlatform != null)
        Padding(
          padding: const EdgeInsets.only(top: spaceXs),
          child: StatusBadge.platformChange(platform!, previousPlatform!),
        ),
      if (skipped)
        Padding(padding: const EdgeInsets.only(top: spaceXs), child: StatusBadge.stop('Skipped'))
      else if (isAdded)
        Padding(padding: const EdgeInsets.only(top: spaceXs), child: StatusBadge.stop('Added')),
      if (foodAvailable)
        Padding(
          padding: const EdgeInsets.only(top: spaceXs),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              ExcludeSemantics(child: Icon(Icons.restaurant, size: 16, color: scheme.primary)),
              const SizedBox(width: spaceXs),
              Flexible(
                child: Text(
                  Strings.foodAvailableAt(name),
                  style: textTheme.labelLarge!.copyWith(color: scheme.primary, fontSize: textSmall),
                ),
              ),
            ],
          ),
        ),
    ];

    return Container(
      constraints: const BoxConstraints(minHeight: touchTarget),
      padding: const EdgeInsets.symmetric(horizontal: spaceMd, vertical: spaceSm),
      decoration: BoxDecoration(
        color: done ? scheme.surfaceContainer : null,
        borderRadius: BorderRadius.circular(radiusMd),
        border: Border.all(
          color: here ? scheme.primary : Colors.transparent,
          width: 2,
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          if (here) ...[
            ExcludeSemantics(child: Icon(Icons.train, color: scheme.primary)),
            const SizedBox(width: spaceSm),
          ],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [Text(name, style: nameStyle), ...subtitle],
            ),
          ),
          const SizedBox(width: spaceSm),
          if (differs || skipped)
            Text(
              formatTime(scheduled),
              style: monoStyle(context, size: textSmall).copyWith(
                color: scheme.onSurfaceVariant,
                decoration: skipped ? null : TextDecoration.lineThrough,
              ),
            ),
          const SizedBox(width: spaceSm),
          SizedBox(
            width: 56,
            child: Text(
              skipped ? '—' : formatTime(shownExpected),
              textAlign: TextAlign.end,
              style: timeStyle.copyWith(color: expectedColor),
            ),
          ),
        ],
      ),
    );
  }
}
