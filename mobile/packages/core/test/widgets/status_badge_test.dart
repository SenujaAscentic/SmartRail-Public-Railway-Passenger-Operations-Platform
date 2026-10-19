import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../support/pump.dart';

void main() {
  setUpAll(loadAppFonts);

  // Each case: label, badge, expected text, token name, expected icon.
  final cases = <(String, StatusBadge, String, String, IconData)>[
    ('OnTime', StatusBadge.journey(const JourneyStatus(StatusKind.onTime, delayMinutes: 0)), 'On time', 'OnTime', Icons.check_circle),
    ('Late', StatusBadge.journey(const JourneyStatus(StatusKind.late, delayMinutes: 3)), '3 min late', 'Late', Icons.schedule),
    ('Early', StatusBadge.journey(const JourneyStatus(StatusKind.early, delayMinutes: -2)), '2 min early', 'Early', Icons.north_west),
    ('Cancelled', StatusBadge.journey(const JourneyStatus(StatusKind.cancelled)), 'Cancelled', 'Cancelled', Icons.cancel),
    (
      'TerminatedEarly',
      StatusBadge.journey(const JourneyStatus(StatusKind.terminatedEarly), terminatesAt: 'Rambukkana'),
      'Terminates at Rambukkana',
      'TerminatedEarly',
      Icons.cancel
    ),
    ('PlatformChanged', StatusBadge.platformChange('5', '2'), 'Platform 5 (was 2)', 'PlatformChanged', Icons.swap_horiz),
    ('Held', StatusBadge.journey(const JourneyStatus(StatusKind.held, delayMinutes: 3)), 'Held · 3 min late', 'Held', Icons.pan_tool),
    ('NotStarted', StatusBadge.journey(const JourneyStatus(StatusKind.notStarted)), 'Not started', 'NotStarted', Icons.access_time),
    ('Skipped stop', StatusBadge.stop('Skipped'), 'Does not stop', 'Cancelled', Icons.block),
    ('Added stop', StatusBadge.stop('Added'), 'Extra stop', 'Scheduled', Icons.calendar_today),
  ];

  for (final brightness in Brightness.values) {
    for (final (label, badge, text, token, icon) in cases) {
      testWidgets('StatusBadge $label (${brightness.name}): icon + text + token colours', (tester) async {
        await pumpThemed(tester, badge, brightness: brightness);

        // Text and icon are both present: never colour alone.
        expect(find.text(text), findsOneWidget);
        final iconWidget = tester.widget<Icon>(find.byType(Icon));
        expect(iconWidget.icon, icon);

        final colors = tokenColors(token, brightness);
        expect(iconWidget.color, colors.fg);
        expect(tester.widget<Text>(find.text(text)).style!.color, colors.fg);
        final box = tester.widget<Container>(find.descendant(of: find.byType(StatusBadge), matching: find.byType(Container)));
        expect((box.decoration as BoxDecoration).color, colors.bg);
      });
    }
  }

  testWidgets('StatusBadge.neutral shows its text in the neutral style', (tester) async {
    await pumpThemed(tester, const StatusBadge.neutral('Does not run today', icon: Icons.event_busy));
    expect(find.text('Does not run today'), findsOneWidget);
    expect(tester.widget<Icon>(find.byType(Icon)).color, tokenColors('Scheduled', Brightness.light).fg);
  });

  test('StatusBadge.stop refuses a stop status that has no text', () {
    expect(() => StatusBadge.stop('Departed'), throwsArgumentError);
  });
}
