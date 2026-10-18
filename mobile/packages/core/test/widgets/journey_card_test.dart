import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../support/pump.dart';

// Times are UTC; 11:50 UTC = 17:20 in Colombo.
DateTime t(int h, int m) => DateTime.utc(2026, 10, 14, h, m);

JourneyCard card({
  required JourneyStatus status,
  DateTime? expectedDeparture,
  DateTime? expectedArrival,
  String? platform = '1',
  String? previousPlatform,
  bool notRunningToday = false,
  VoidCallback? onTap,
}) =>
    JourneyCard(
      serviceNumber: '1001',
      name: 'Kandy Express',
      status: status,
      scheduledDeparture: t(11, 50),
      scheduledArrival: t(14, 28),
      expectedDeparture: expectedDeparture,
      expectedArrival: expectedArrival,
      platform: platform,
      previousPlatform: previousPlatform,
      notRunningToday: notRunningToday,
      onTap: onTap,
    );

void main() {
  setUpAll(loadAppFonts);

  testWidgets('normal: times, "On time", train and platform; no scheduled line', (tester) async {
    await pumpThemed(
      tester,
      card(status: const JourneyStatus(StatusKind.onTime, delayMinutes: 0), expectedDeparture: t(11, 50), expectedArrival: t(14, 28)),
    );
    expect(find.text('17:20 → 19:58'), findsOneWidget);
    expect(find.text('On time'), findsOneWidget);
    expect(find.textContaining('scheduled'), findsNothing);
    expect(find.textContaining('1001 Kandy Express', findRichText: true), findsOneWidget);
    expect(find.textContaining('· Express', findRichText: true), findsNothing);
    expect(find.text('Platform 1'), findsOneWidget);
  });

  testWidgets('late: expected times large, scheduled times struck through', (tester) async {
    await pumpThemed(
      tester,
      card(status: const JourneyStatus(StatusKind.late, delayMinutes: 7), expectedDeparture: t(11, 57), expectedArrival: t(14, 35)),
    );
    expect(find.text('17:27 → 20:05'), findsOneWidget);
    expect(find.text('7 min late'), findsOneWidget);
    final scheduled = tester.widget<Text>(find.text('17:20 → 19:58 scheduled'));
    expect(scheduled.style!.decoration, TextDecoration.lineThrough);
  });

  testWidgets('cancelled: scheduled times struck through, "Cancelled" badge', (tester) async {
    await pumpThemed(tester, card(status: const JourneyStatus(StatusKind.cancelled)));
    final times = tester.widget<Text>(find.text('17:20 → 19:58'));
    expect(times.style!.decoration, TextDecoration.lineThrough);
    expect(find.text('Cancelled'), findsOneWidget);
  });

  testWidgets('platform change: "Platform 5 (was 2)"', (tester) async {
    await pumpThemed(
      tester,
      card(status: const JourneyStatus(StatusKind.onTime, delayMinutes: 0), platform: '5', previousPlatform: '2'),
    );
    expect(find.text('Platform 5 (was 2)'), findsOneWidget);
  });

  testWidgets('not running today: neutral badge, no status text', (tester) async {
    await pumpThemed(tester, card(status: const JourneyStatus(StatusKind.onTime, delayMinutes: 0), notRunningToday: true));
    expect(find.text('Does not run today'), findsOneWidget);
    expect(tester.widget<Icon>(find.byType(Icon)).icon, Icons.calendar_today);
    expect(find.text('On time'), findsNothing);
  });

  testWidgets('expected times missing: the scheduled times are shown, nothing is invented', (tester) async {
    await pumpThemed(tester, card(status: const JourneyStatus(StatusKind.notStarted)));
    expect(find.text('17:20 → 19:58'), findsOneWidget);
    expect(find.text('Not started'), findsOneWidget);
  });

  testWidgets('tapping the card calls onTap, and the card is at least 48 px high', (tester) async {
    var taps = 0;
    await pumpThemed(tester, card(status: const JourneyStatus(StatusKind.onTime, delayMinutes: 0), onTap: () => taps++));
    await tester.tap(find.byType(JourneyCard));
    expect(taps, 1);
    expect(tester.getSize(find.byType(JourneyCard)).height, greaterThanOrEqualTo(48));
  });

  testWidgets('times use tabular figures so columns do not shift', (tester) async {
    await pumpThemed(tester, card(status: const JourneyStatus(StatusKind.onTime, delayMinutes: 0)));
    final style = tester.widget<Text>(find.text('17:20 → 19:58')).style!;
    expect(style.fontFeatures, contains(const FontFeature.tabularFigures()));
    expect(style.fontFamily, contains('NotoSansMono'));
  });
}
