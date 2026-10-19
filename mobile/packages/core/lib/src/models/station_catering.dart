import '../time/colombo_time.dart';

/// One item of the provider's menu preview.
class MenuItem {
  const MenuItem({required this.name, required this.price, required this.available});

  final String name;

  /// In Sri Lankan rupees.
  final num price;

  /// False means "Sold out".
  final bool available;

  factory MenuItem.fromJson(Map<String, dynamic> json) => MenuItem(
        name: json['name'] as String,
        price: json['price'] as num,
        available: json['available'] as bool,
      );
}

/// When the provider's hotline is answered, in Colombo time ("06:00" to "20:00").
class OpeningHours {
  const OpeningHours({required this.opens, required this.closes});

  /// "HH:mm", as the backend sent it.
  final String opens;
  final String closes;

  factory OpeningHours.fromJson(Map<String, dynamic> json) =>
      OpeningHours(opens: json['opens'] as String, closes: json['closes'] as String);

  /// Whether the hotline is open at [utc] (an instant, turned into Colombo time
  /// here). Hours that run past midnight (opens 18:00, closes 02:00) work too.
  bool isOpenAt(DateTime utc) {
    final c = toColombo(utc);
    final now = c.hour * 60 + c.minute;
    final open = _minutes(opens);
    final close = _minutes(closes);
    return open <= close ? (now >= open && now < close) : (now >= open || now < close);
  }

  static int _minutes(String hhmm) {
    final parts = hhmm.split(':');
    if (parts.length < 2) throw FormatException('Not a time of day: "$hhmm"');
    return int.parse(parts[0]) * 60 + int.parse(parts[1]);
  }
}

/// The catering of one station (`GET /stations/{stationId}/catering`): the
/// provider's cached menu, hotline and opening hours. [available] is false when
/// the provider is down and there is nothing cached; then the app says "Menu
/// unavailable". The shape is the one agreed with the backend for US-P8 (see
/// mobile/NOTES.md); fields other than [available] may be missing then.
class StationCatering {
  const StationCatering({required this.available, required this.hotline, required this.openingHours, required this.menu});

  final bool available;
  final String? hotline;
  final OpeningHours? openingHours;
  final List<MenuItem> menu;

  factory StationCatering.fromJson(Map<String, dynamic> json) => StationCatering(
        available: json['available'] as bool,
        hotline: json['hotline'] as String?,
        openingHours: json['openingHours'] == null ? null : OpeningHours.fromJson(json['openingHours'] as Map<String, dynamic>),
        menu: [for (final item in (json['menu'] as List<dynamic>? ?? const [])) MenuItem.fromJson(item as Map<String, dynamic>)],
      );
}
