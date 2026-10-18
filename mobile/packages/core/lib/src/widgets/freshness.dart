import 'package:flutter/material.dart';

import '../models/freshness.dart';
import '../status/status_texts.dart';
import '../theme/status_theme.dart';
import '../theme/tokens.dart';

/// "Updated 30 s ago" with a small dot, for live data. Shows nothing when the
/// age is unknown (no Date header) or the data is not live; the banner covers
/// those cases. [age] is counted by the app's own stopwatch (Q4).
class AgeLabel extends StatelessWidget {
  const AgeLabel({super.key, required this.freshness, required this.age});

  final Freshness freshness;
  final Duration? age;

  @override
  Widget build(BuildContext context) {
    final text = freshness.tracking == Tracking.live ? freshnessText(freshness, age: age) : null;
    if (text == null) return const SizedBox.shrink();
    // The dot reuses the "On time" green; the words carry the meaning.
    final dot = Theme.of(context).extension<StatusTheme>()!.named('OnTime').fg;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        ExcludeSemantics(
          child: Container(width: 8, height: 8, decoration: BoxDecoration(color: dot, shape: BoxShape.circle)),
        ),
        const SizedBox(width: spaceSm),
        Text(text, style: Theme.of(context).textTheme.bodySmall),
      ],
    );
  }
}

/// A full-width banner for stale data ("Last seen 07:38 near Kelaniya"), lost
/// data ("Live location unavailable – scheduled times shown") or an offline
/// phone ("You're offline · updated 4 min ago"). Shows nothing for other states.
class FreshnessBanner extends StatelessWidget {
  /// Banner for the tracking state the backend sent.
  const FreshnessBanner({super.key, required this.freshness}) : offlineAge = null;

  /// Banner for a phone without connection; [age] is the age of the data shown.
  const FreshnessBanner.offline({super.key, required Duration age})
      : freshness = null,
        offlineAge = age;

  final Freshness? freshness;
  final Duration? offlineAge;

  @override
  Widget build(BuildContext context) {
    final String text;
    final String tokenName;
    final IconData? icon;
    final f = freshness;
    if (f == null) {
      text = offlineText(offlineAge!);
      tokenName = 'Lost';
      icon = Icons.cloud_off;
    } else if (f.tracking == Tracking.stale || f.tracking == Tracking.lost) {
      final t = freshnessText(f);
      if (t == null) return const SizedBox.shrink();
      text = t;
      tokenName = f.tracking == Tracking.stale ? 'Stale' : 'Lost';
      icon = null;
    } else {
      return const SizedBox.shrink();
    }
    final style = Theme.of(context).extension<StatusTheme>()!.named(tokenName);
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(spaceMd),
      decoration: BoxDecoration(color: style.bg, borderRadius: BorderRadius.circular(radiusLg)),
      child: Row(
        children: [
          ExcludeSemantics(child: Icon(icon ?? style.icon, color: style.fg)),
          const SizedBox(width: spaceSm),
          Expanded(
            child: Text(
              text,
              style: Theme.of(context).textTheme.labelLarge!.copyWith(color: style.fg),
            ),
          ),
        ],
      ),
    );
  }
}
