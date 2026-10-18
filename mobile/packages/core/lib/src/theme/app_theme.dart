import 'package:flutter/material.dart';

import 'status_theme.dart';
import 'tokens.dart';

const _fontPackage = 'smartrail_core';

final buttonShape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusLg));

/// Material 3 theme for the passenger app, built from the tokens. The
/// ColorScheme is written out by hand: ColorScheme.fromSeed would invent its own
/// palette and lose the checked contrast (design handoff rule 11).
ThemeData buildPassengerTheme(Brightness brightness) {
  final dark = brightness == Brightness.dark;
  final c = dark ? darkColors : lightColors;
  final error = statusTokens['Cancelled']!;
  final errorColors = dark ? error.dark : error.light;

  final scheme = ColorScheme(
    brightness: brightness,
    primary: c.accent,
    onPrimary: c.background,
    secondary: c.accent,
    onSecondary: c.background,
    error: errorColors.fg,
    onError: c.background,
    surface: c.background,
    onSurface: c.text,
    onSurfaceVariant: c.textMuted,
    outline: c.border,
    outlineVariant: c.border,
    surfaceContainerLowest: c.background,
    surfaceContainerLow: c.surface,
    surfaceContainer: c.surface,
    surfaceContainerHigh: c.surface,
    surfaceContainerHighest: c.surface,
  );

  // Tabular figures keep times from shifting when digits change (rule 8).
  const tabular = [FontFeature.tabularFigures()];
  TextStyle sans(double size, {FontWeight? weight}) => TextStyle(
        fontFamily: 'NotoSans',
        package: _fontPackage,
        fontSize: size,
        fontWeight: weight,
        fontFeatures: tabular,
        color: c.text,
      );

  final textTheme = TextTheme(
    titleLarge: sans(textTitle, weight: FontWeight.w700),
    titleMedium: sans(textBody, weight: FontWeight.w700),
    bodyLarge: sans(textBody),
    bodyMedium: sans(textBody),
    bodySmall: sans(textSmall).copyWith(color: c.textMuted),
    labelLarge: sans(textBody, weight: FontWeight.w700),
    labelMedium: sans(textSmall),
    labelSmall: sans(textSmall),
  );

  return ThemeData(
    useMaterial3: true,
    brightness: brightness,
    colorScheme: scheme,
    scaffoldBackgroundColor: c.background,
    textTheme: textTheme,
    // Material also uses focusColor to highlight list options (type-ahead), so it
    // must be see-through or the option text becomes unreadable on solid blue.
    focusColor: c.focus.withValues(alpha: 0.16),
    dividerColor: c.border,
    // Every tap target is at least 48 px (tokens.json -> touchTarget.passenger).
    materialTapTargetSize: MaterialTapTargetSize.padded,
    // Buttons use the token radius (rounded rectangle, as in the hi-fi) and are at least 48 px high.
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(minimumSize: const Size(touchTarget, touchTarget), shape: buttonShape),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(minimumSize: const Size(touchTarget, touchTarget), shape: buttonShape),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(minimumSize: const Size(touchTarget, touchTarget), shape: buttonShape),
    ),
    extensions: [StatusTheme(dark: dark)],
  );
}

/// Style for times and platform numbers (Noto Sans Mono, tabular figures).
TextStyle monoStyle(BuildContext context, {double size = textBody, FontWeight? weight}) => TextStyle(
      fontFamily: 'NotoSansMono',
      package: _fontPackage,
      fontSize: size,
      fontWeight: weight,
      fontFeatures: const [FontFeature.tabularFigures()],
      color: Theme.of(context).colorScheme.onSurface,
    );
