import 'package:flutter/material.dart';

import '../models/journey_status.dart';
import '../status/status_texts.dart';
import '../theme/status_theme.dart';
import '../theme/tokens.dart';

/// Icon + text + colour in a small rounded box. Status is never shown by colour
/// alone (design handoff rule 1): the icon and the words are always there.
/// The wording comes from status_texts.dart (the shared fixtures).
class StatusBadge extends StatelessWidget {
  const StatusBadge._({super.key, required this.text, required this.tokenName, this.icon});

  /// A journey status: "On time", "3 min late", "Cancelled", ...
  StatusBadge.journey(JourneyStatus status, {String? terminatesAt, Key? key})
      : this._(
          key: key,
          text: statusText(status, terminatesAt: terminatesAt),
          tokenName: StatusTheme.tokenNameFor(status.kind),
        );

  /// "Platform 5 (was 2)" in the platform-change colours.
  StatusBadge.platformChange(String platform, String previousPlatform, {Key? key})
      : this._(
          key: key,
          text: platformText(platform, previousPlatform),
          tokenName: 'PlatformChanged',
        );

  /// "Does not stop" (skipped) or "Extra stop" (added) on the stop list.
  /// Skipped uses the red Cancelled colours; Added the neutral Scheduled ones.
  StatusBadge.stop(String stopStatus, {Key? key})
      : this._(
          key: key,
          text: stopStatusText(stopStatus) ?? (throw ArgumentError.value(stopStatus, 'stopStatus')),
          tokenName: stopStatus == 'Skipped' ? 'Cancelled' : 'Scheduled',
          icon: stopStatus == 'Skipped' ? Icons.block : null,
        );

  /// A badge with text that is not a journey status, in the neutral style
  /// with the calendar icon (for example "Does not run today").
  const StatusBadge.neutral(String text, {Key? key, IconData? icon})
      : this._(key: key, text: text, tokenName: 'Scheduled', icon: icon);

  final String text;
  final String tokenName;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final style = Theme.of(context).extension<StatusTheme>()!.named(tokenName);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: spaceSm, vertical: spaceXs),
      decoration: BoxDecoration(color: style.bg, borderRadius: BorderRadius.circular(radiusMd)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          ExcludeSemantics(child: Icon(icon ?? style.icon, size: 16, color: style.fg)),
          const SizedBox(width: spaceXs),
          Flexible(
            child: Text(
              text,
              style: Theme.of(context).textTheme.labelLarge!.copyWith(color: style.fg, fontSize: textSmall),
            ),
          ),
        ],
      ),
    );
  }
}
