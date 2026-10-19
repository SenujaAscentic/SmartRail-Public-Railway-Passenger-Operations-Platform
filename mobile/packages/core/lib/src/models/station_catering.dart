/// One item of the provider's menu preview (schema MenuItemResponse).
class MenuItem {
  const MenuItem({required this.name, required this.price, required this.available});

  final String name;

  /// Whole Sri Lankan rupees.
  final num price;

  /// False means "Sold out".
  final bool available;

  factory MenuItem.fromJson(Map<String, dynamic> json) => MenuItem(
        name: json['name'] as String,
        price: json['price'] as num,
        available: json['available'] as bool,
      );
}

/// When the provider's hotline is answered, as a time of day in Colombo ("06:00" to "20:00"). Only displayed: whether it is open
/// NOW is decided by the server ([StationCatering.openNow]).
class OpeningHours {
  const OpeningHours({required this.opens, required this.closes});

  /// "HH:mm", as the backend sent it.
  final String opens;
  final String closes;

  factory OpeningHours.fromJson(Map<String, dynamic> json) =>
      OpeningHours(opens: json['opens'] as String, closes: json['closes'] as String);
}

/// The catering of one station (`GET /stations/{stationId}/catering`, schema StationCateringResponse): the provider's cached menu,
/// hotline and opening hours. All five fields are always sent.
///
/// [available] is false when the provider is down, its cache is older than 15 minutes, or it is not taking orders: then [menu] is
/// empty, [hotline] is "" and [openingHours] is null, and the app says "Menu unavailable". [openNow] is decided by the server (the
/// app does not look at a clock). [hotline] is "" when there is none: the app then shows no call button.
class StationCatering {
  const StationCatering({
    required this.available,
    required this.openNow,
    required this.hotline,
    required this.openingHours,
    required this.menu,
  });

  final bool available;
  final bool openNow;
  final String hotline;
  final OpeningHours? openingHours;
  final List<MenuItem> menu;

  bool get hasHotline => hotline.isNotEmpty;

  factory StationCatering.fromJson(Map<String, dynamic> json) => StationCatering(
        available: json['available'] as bool,
        openNow: json['openNow'] as bool,
        hotline: json['hotline'] as String? ?? '',
        openingHours: json['openingHours'] == null ? null : OpeningHours.fromJson(json['openingHours'] as Map<String, dynamic>),
        menu: [for (final item in (json['menu'] as List<dynamic>? ?? const [])) MenuItem.fromJson(item as Map<String, dynamic>)],
      );
}
