/// One station from `GET /stations/{stationId}` (schema StationResponse). Only
/// the fields the passenger app uses: the name and the catering flag (US-P8).
class StationDetail {
  const StationDetail({required this.id, required this.name, required this.cateringStation});

  final String id;
  final String name;

  /// True when the station has catering (the provider serves it).
  final bool cateringStation;

  factory StationDetail.fromJson(Map<String, dynamic> json) => StationDetail(
        id: json['stationId'] as String,
        name: json['name'] as String,
        cateringStation: json['cateringStation'] as bool,
      );
}
