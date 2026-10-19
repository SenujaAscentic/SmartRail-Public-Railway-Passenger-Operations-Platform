import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../support/pump.dart';

void main() {
  setUpAll(loadAppFonts);

  final seen = DateTime.utc(2026, 10, 14, 2, 8);

  group('AgeLabel', () {
    testWidgets('live with an age: "Updated 30 s ago"', (tester) async {
      await pumpThemed(tester, const AgeLabel(freshness: Freshness(Tracking.live), age: Duration(seconds: 30)));
      expect(find.text('Updated 30 s ago'), findsOneWidget);
    });

    testWidgets('live without an age (no Date header) shows nothing', (tester) async {
      await pumpThemed(tester, const AgeLabel(freshness: Freshness(Tracking.live), age: null));
      expect(find.byType(Text), findsNothing);
    });

    testWidgets('not live: no age label (the banner speaks)', (tester) async {
      await pumpThemed(tester, AgeLabel(freshness: Freshness(Tracking.stale, lastSeenAt: seen), age: const Duration(minutes: 3)));
      expect(find.byType(Text), findsNothing);
    });
  });

  group('FreshnessBanner', () {
    for (final brightness in Brightness.values) {
      testWidgets('stale (${brightness.name}): text, signal icon and stale colours', (tester) async {
        await pumpThemed(
          tester,
          FreshnessBanner(freshness: Freshness(Tracking.stale, lastSeenAt: seen, lastStation: 'Kelaniya')),
          brightness: brightness,
        );
        expect(find.text('Last seen 07:38 near Kelaniya'), findsOneWidget);
        final icon = tester.widget<Icon>(find.byType(Icon));
        expect(icon.icon, Icons.signal_cellular_alt_1_bar);
        expect(icon.color, tokenColors('Stale', brightness).fg);
      });
    }

    testWidgets('lost: text, signal-off icon and lost colours', (tester) async {
      await pumpThemed(tester, const FreshnessBanner(freshness: Freshness(Tracking.lost)));
      expect(find.text('Live location unavailable – scheduled times shown'), findsOneWidget);
      final icon = tester.widget<Icon>(find.byType(Icon));
      expect(icon.icon, Icons.signal_cellular_off);
      expect(icon.color, tokenColors('Lost', Brightness.light).fg);
    });

    testWidgets('offline: text and cloud-off icon', (tester) async {
      await pumpThemed(tester, const FreshnessBanner.offline(age: Duration(minutes: 4)));
      expect(find.text("You're offline · updated 4 min ago"), findsOneWidget);
      expect(tester.widget<Icon>(find.byType(Icon)).icon, Icons.cloud_off);
    });

    for (final tracking in [Tracking.live, Tracking.notStarted, Tracking.ended]) {
      testWidgets('${tracking.name} shows no banner', (tester) async {
        await pumpThemed(tester, FreshnessBanner(freshness: Freshness(tracking)));
        expect(find.byType(Text), findsNothing);
      });
    }
  });
}
