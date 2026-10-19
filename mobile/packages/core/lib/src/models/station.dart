/// A station in type-ahead results (`GET /stations?query=`, schema StationSummary).
class Station {
  const Station({required this.id, required this.code, required this.name});

  final String id;
  final String code;
  final String name;

  factory Station.fromJson(Map<String, dynamic> json) => Station(
        id: json['id'] as String,
        code: json['code'] as String,
        name: json['name'] as String,
      );
}
