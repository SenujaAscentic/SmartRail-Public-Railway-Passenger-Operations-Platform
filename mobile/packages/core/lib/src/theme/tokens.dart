import 'dart:ui';

// Hand-written copy of shared/tokens.json (no code generation, ADR-12). The
// test in test/theme/tokens_test.dart reads the JSON and fails if any value
// here differs, so the two cannot drift apart unnoticed.

/// Base colours of one theme (tokens.json -> color.light / color.dark).
class BaseColors {
  const BaseColors({
    required this.background,
    required this.surface,
    required this.text,
    required this.textMuted,
    required this.border,
    required this.accent,
    required this.focus,
  });

  final Color background;
  final Color surface;
  final Color text;
  final Color textMuted;
  final Color border;
  final Color accent;
  final Color focus;
}

const lightColors = BaseColors(
  background: Color(0xFFFFFFFF),
  surface: Color(0xFFF4F6F8),
  text: Color(0xFF14202B),
  textMuted: Color(0xFF4A5560),
  border: Color(0xFFC9D1D9),
  accent: Color(0xFF1D5FA8),
  focus: Color(0xFF1D5FA8),
);

const darkColors = BaseColors(
  background: Color(0xFF0B1A2B),
  surface: Color(0xFF13263B),
  text: Color(0xFFFFFFFF),
  textMuted: Color(0xFFB8C4D0),
  border: Color(0xFF2C4258),
  accent: Color(0xFF7DB7FF),
  focus: Color(0xFFFFD24D),
);

/// Foreground and background of one status in one theme.
class StatusColors {
  const StatusColors(this.fg, this.bg);
  final Color fg;
  final Color bg;
}

/// Colours and icon name of one status (tokens.json -> status.<Name>).
class StatusToken {
  const StatusToken(this.icon, this.light, this.dark);
  final String icon;
  final StatusColors light;
  final StatusColors dark;
}

const _amberLight = StatusColors(Color(0xFF8A4B00), Color(0xFFFCEFD9));
const _amberDark = StatusColors(Color(0xFFFFC266), Color(0xFF3D2A0B));
const _redLight = StatusColors(Color(0xFFA8201A), Color(0xFFFBE4E2));
const _redDark = StatusColors(Color(0xFFFF8A80), Color(0xFF4A1512));
const _greyLight = StatusColors(Color(0xFF4A5560), Color(0xFFEEF1F4));
const _greyDark = StatusColors(Color(0xFFB8C4D0), Color(0xFF1C2F44));

/// Keys are the token names, which equal the API status kinds where one exists.
const statusTokens = <String, StatusToken>{
  'OnTime': StatusToken('check-circle', StatusColors(Color(0xFF1B6E35), Color(0xFFE3F4E8)),
      StatusColors(Color(0xFF6EE7A0), Color(0xFF123824))),
  'Early': StatusToken('arrow-up-left', StatusColors(Color(0xFF1D5FA8), Color(0xFFE3EEFA)),
      StatusColors(Color(0xFF8CC4FF), Color(0xFF12304F))),
  'Late': StatusToken('clock-alert', _amberLight, _amberDark),
  'Held': StatusToken('hand-stop', _amberLight, _amberDark),
  'Cancelled': StatusToken('x-circle', _redLight, _redDark),
  'TerminatedEarly': StatusToken('x-circle', _redLight, _redDark),
  'PlatformChanged': StatusToken('arrows-exchange', StatusColors(Color(0xFF14202B), Color(0xFFFFE27A)),
      StatusColors(Color(0xFF0B1A2B), Color(0xFFFFD24D))),
  'NotStarted': StatusToken('clock', _greyLight, _greyDark),
  'Scheduled': StatusToken('calendar', _greyLight, _greyDark),
  'Stale': StatusToken('signal-weak', _amberLight, _amberDark),
  'Lost': StatusToken('signal-off', _greyLight, _greyDark),
};

/// Passenger type sizes in logical pixels (tokens.json, typography.surfaces.passenger).
const double textBody = 16;
const double textSmall = 14;
const double textTitle = 22;
const double textTime = 28;
const double textPlatform = 28;

/// Spacing, radius and touch target (tokens.json: space, radius, touchTarget.passenger).
const double spaceXs = 4;
const double spaceSm = 8;
const double spaceMd = 16;
const double spaceLg = 24;
const double spaceXl = 32;
const double radiusSm = 4;
const double radiusMd = 8;
const double radiusLg = 12;
const double touchTarget = 48;
