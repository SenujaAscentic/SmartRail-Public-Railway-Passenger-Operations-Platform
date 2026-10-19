import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../support/pump.dart';

DateTime t(int h, int m) => DateTime.utc(2026, 10, 14, h, m); // UTC; Colombo = +05:30

const late7 = JourneyStatus(StatusKind.late, delayMinutes: 7);

BoxDecoration rowDecoration(WidgetTester tester) {
  final box = tester.widget<Container>(find.descendant(of: find.byType(StopRow), matching: find.byType(Container)).first);
  return box.decoration! as BoxDecoration;
}

void main() {
  setUpAll(loadAppFonts);

  testWidgets('scheduled: name, struck-through scheduled time and expected time in the late colour', (tester) async {
    await pumpThemed(
      tester,
      StopRow(name: 'Polgahawela', state: StopState.scheduled, scheduled: t(12, 18), expected: t(12, 25), journeyStatus: late7),
    );
    expect(find.text('Polgahawela'), findsOneWidget);
    expect(tester.widget<Text>(find.text('17:48')).style!.decoration, TextDecoration.lineThrough);
    expect(tester.widget<Text>(find.text('17:55')).style!.color, tokenColors('Late', Brightness.light).fg);
  });

  testWidgets('on time: one time only', (tester) async {
    await pumpThemed(tester, StopRow(name: 'Maradana', state: StopState.scheduled, scheduled: t(11, 6), expected: t(11, 6)));
    expect(find.text('16:36'), findsOneWidget);
  });

  testWidgets('arrived: outlined with a train icon', (tester) async {
    await pumpThemed(
      tester,
      StopRow(name: 'Gampaha', state: StopState.arrived, scheduled: t(11, 32), expected: t(11, 39), journeyStatus: late7),
    );
    expect(find.byIcon(Icons.train), findsOneWidget);
    final border = rowDecoration(tester).border! as Border;
    expect(border.top.color, lightColors.accent);
  });

  testWidgets('departed: greyed, expected time not coloured as late', (tester) async {
    await pumpThemed(
      tester,
      StopRow(name: 'Maradana', state: StopState.departed, scheduled: t(11, 6), expected: t(11, 13), journeyStatus: late7),
    );
    expect(rowDecoration(tester).color, lightColors.surface);
    expect(tester.widget<Text>(find.text('16:43')).style!.color, lightColors.textMuted);
  });

  testWidgets('passed: greyed like departed', (tester) async {
    await pumpThemed(tester, StopRow(name: 'Colombo Fort', state: StopState.passed, scheduled: t(11, 0), expected: t(11, 7)));
    expect(rowDecoration(tester).color, lightColors.surface);
  });

  testWidgets('skipped: name struck through, "Does not stop", dash instead of an expected time', (tester) async {
    await pumpThemed(tester, StopRow(name: 'Peradeniya', state: StopState.skipped, scheduled: t(13, 25)));
    expect(tester.widget<Text>(find.text('Peradeniya')).style!.decoration, TextDecoration.lineThrough);
    expect(find.text('Does not stop'), findsOneWidget);
    expect(find.text('—'), findsOneWidget);
  });

  testWidgets('added stop: "Extra stop"', (tester) async {
    await pumpThemed(tester, StopRow(name: 'Veyangoda', state: StopState.scheduled, scheduled: t(11, 50), isAdded: true));
    expect(find.text('Extra stop'), findsOneWidget);
  });

  testWidgets('platform changed: "Platform 5 (was 2)" badge', (tester) async {
    await pumpThemed(
      tester,
      StopRow(name: 'Gampaha', state: StopState.scheduled, scheduled: t(11, 32), expected: t(11, 39), platform: '5', previousPlatform: '2'),
    );
    expect(find.text('Platform 5 (was 2)'), findsOneWidget);
  });

  testWidgets('your station with a platform', (tester) async {
    await pumpThemed(
      tester,
      StopRow(name: 'Gampaha', state: StopState.scheduled, scheduled: t(11, 32), platform: '2', isYourStation: true),
    );
    expect(find.text('Your station · Platform 2'), findsOneWidget);
  });

  testWidgets('destination', (tester) async {
    await pumpThemed(tester, StopRow(name: 'Kandy', state: StopState.scheduled, scheduled: t(13, 35), isDestination: true));
    expect(find.text('destination'), findsOneWidget);
  });

  testWidgets('food available', (tester) async {
    await pumpThemed(tester, StopRow(name: 'Polgahawela', state: StopState.scheduled, scheduled: t(12, 18), foodAvailable: true));
    expect(find.text('Food available at Polgahawela'), findsOneWidget);
  });

  testWidgets('every row is at least 48 px high', (tester) async {
    await pumpThemed(tester, StopRow(name: 'Maradana', state: StopState.scheduled, scheduled: t(11, 6)));
    expect(tester.getSize(find.byType(StopRow)).height, greaterThanOrEqualTo(48));
  });

  testWidgets('times use tabular figures in Noto Sans Mono', (tester) async {
    await pumpThemed(tester, StopRow(name: 'Maradana', state: StopState.scheduled, scheduled: t(11, 6)));
    final style = tester.widget<Text>(find.text('16:36')).style!;
    expect(style.fontFeatures, contains(const FontFeature.tabularFigures()));
    expect(style.fontFamily, contains('NotoSansMono'));
  });
}
