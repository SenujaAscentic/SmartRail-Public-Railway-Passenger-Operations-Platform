import 'package:flutter/material.dart';

import '../models/journey_status.dart';
import 'tokens.dart';

/// Token icon name -> built-in Material icon (design handoff rule 12).
const _icons = <String, IconData>{
  'check-circle': Icons.check_circle,
  'arrow-up-left': Icons.north_west,
  'clock-alert': Icons.schedule,
  'hand-stop': Icons.pan_tool,
  'x-circle': Icons.cancel,
  'arrows-exchange': Icons.swap_horiz,
  'clock': Icons.access_time,
  'calendar': Icons.calendar_today,
  'signal-weak': Icons.signal_cellular_alt_1_bar,
  'signal-off': Icons.signal_cellular_off,
};

/// Icon and colours of one status for the current theme.
class StatusStyle {
  const StatusStyle({required this.icon, required this.fg, required this.bg});
  final IconData icon;
  final Color fg;
  final Color bg;
}

/// The status colours and icons, on top of the Material colour scheme (rule 11).
@immutable
class StatusTheme extends ThemeExtension<StatusTheme> {
  const StatusTheme({required this.dark});

  final bool dark;

  /// [name] is a key of [statusTokens], e.g. 'Late' or 'Stale'.
  StatusStyle named(String name) {
    final token = statusTokens[name] ?? (throw ArgumentError.value(name, 'name', 'no such status token'));
    final colors = dark ? token.dark : token.light;
    return StatusStyle(icon: _icons[token.icon]!, fg: colors.fg, bg: colors.bg);
  }

  /// Token name for a status kind. 'Completed' has no token in tokens.json, so it
  /// uses the neutral 'Scheduled' style (approved 18 Oct, no new colour).
  static String tokenNameFor(StatusKind kind) => switch (kind) {
        StatusKind.onTime => 'OnTime',
        StatusKind.late => 'Late',
        StatusKind.early => 'Early',
        StatusKind.cancelled => 'Cancelled',
        StatusKind.terminatedEarly => 'TerminatedEarly',
        StatusKind.held => 'Held',
        StatusKind.notStarted => 'NotStarted',
        StatusKind.completed => 'Scheduled',
      };

  StatusStyle forKind(StatusKind kind) => named(tokenNameFor(kind));

  @override
  StatusTheme copyWith({bool? dark}) => StatusTheme(dark: dark ?? this.dark);

  @override
  StatusTheme lerp(StatusTheme? other, double t) => t < 0.5 ? this : (other ?? this);
}
