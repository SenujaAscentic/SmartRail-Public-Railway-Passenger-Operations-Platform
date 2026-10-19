import 'dart:convert';
import 'dart:io';
import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

// tokens.dart is a hand-written copy of shared/tokens.json; these tests fail if
// any value differs. flutter test runs with the package folder as working directory.
const _tokensPath = '../../../shared/tokens.json';

Color _hex(String s) => Color(0xFF000000 | int.parse(s.substring(1), radix: 16));

void main() {
  final tokens = jsonDecode(File(_tokensPath).readAsStringSync()) as Map<String, dynamic>;

  void expectBase(BaseColors actual, Map<String, dynamic> json) {
    expect(actual.background, _hex(json['background'] as String));
    expect(actual.surface, _hex(json['surface'] as String));
    expect(actual.text, _hex(json['text'] as String));
    expect(actual.textMuted, _hex(json['textMuted'] as String));
    expect(actual.border, _hex(json['border'] as String));
    expect(actual.accent, _hex(json['accent'] as String));
    expect(actual.focus, _hex(json['focus'] as String));
  }

  test('light base colours match tokens.json', () {
    expectBase(lightColors, tokens['color']['light'] as Map<String, dynamic>);
  });

  test('dark base colours match tokens.json', () {
    expectBase(darkColors, tokens['color']['dark'] as Map<String, dynamic>);
  });

  test('every status token (icon, light and dark colours) matches tokens.json', () {
    final status = tokens['status'] as Map<String, dynamic>;
    expect(statusTokens.keys.toSet(), status.keys.toSet());
    for (final entry in status.entries) {
      final json = entry.value as Map<String, dynamic>;
      final actual = statusTokens[entry.key]!;
      expect(actual.icon, json['icon'], reason: entry.key);
      expect(actual.light.fg, _hex(json['light']['fg'] as String), reason: '${entry.key} light fg');
      expect(actual.light.bg, _hex(json['light']['bg'] as String), reason: '${entry.key} light bg');
      expect(actual.dark.fg, _hex(json['dark']['fg'] as String), reason: '${entry.key} dark fg');
      expect(actual.dark.bg, _hex(json['dark']['bg'] as String), reason: '${entry.key} dark bg');
    }
  });

  test('passenger type sizes, spacing, radius and touch target match tokens.json', () {
    final p = tokens['typography']['surfaces']['passenger'] as Map<String, dynamic>;
    expect([textBody, textSmall, textTitle, textTime, textPlatform],
        [p['body'], p['small'], p['title'], p['time'], p['platform']]);
    final space = tokens['space'] as Map<String, dynamic>;
    expect([spaceXs, spaceSm, spaceMd, spaceLg, spaceXl],
        [space['xs'], space['sm'], space['md'], space['lg'], space['xl']]);
    final radius = tokens['radius'] as Map<String, dynamic>;
    expect([radiusSm, radiusMd, radiusLg], [radius['sm'], radius['md'], radius['lg']]);
    expect(touchTarget, tokens['touchTarget']['passenger']);
  });

  test('every token icon name is mapped to a Material icon', () {
    final status = tokens['status'] as Map<String, dynamic>;
    for (final key in status.keys) {
      expect(() => const StatusTheme(dark: false).named(key), returnsNormally, reason: key);
    }
  });

  group('buildPassengerTheme', () {
    for (final brightness in Brightness.values) {
      test('${brightness.name}: scheme uses the token colours, not a seed palette', () {
        final theme = buildPassengerTheme(brightness);
        final base = brightness == Brightness.dark ? darkColors : lightColors;
        expect(theme.colorScheme.primary, base.accent);
        expect(theme.colorScheme.surface, base.background);
        expect(theme.colorScheme.onSurface, base.text);
        expect(theme.scaffoldBackgroundColor, base.background);
        expect(theme.extension<StatusTheme>()!.dark, brightness == Brightness.dark);
      });
    }

    test('text uses Noto Sans with tabular figures', () {
      final style = buildPassengerTheme(Brightness.light).textTheme.bodyLarge!;
      expect(style.fontFamily, contains('NotoSans'));
      expect(style.fontSize, textBody);
      expect(style.fontFeatures, contains(const FontFeature.tabularFigures()));
    });

    test('focusColor is see-through, so highlighted list options stay readable', () {
      for (final brightness in Brightness.values) {
        expect(buildPassengerTheme(brightness).focusColor.a, lessThan(0.5));
      }
    });

    test('buttons are at least 48 px high', () {
      final theme = buildPassengerTheme(Brightness.light);
      expect(theme.filledButtonTheme.style!.minimumSize!.resolve({})!.height, 48);
    });
  });
}
