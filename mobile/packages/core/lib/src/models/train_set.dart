/// One coach of today's train set (schema CoachResponse).
class Coach {
  const Coach({required this.position, required this.coachClass, required this.seating});

  final int position;

  /// "First", "Second" or "Third".
  final String coachClass;

  /// "Reserved" or "Unreserved".
  final String seating;

  factory Coach.fromJson(Map<String, dynamic> json) => Coach(
        position: json['position'] as int,
        coachClass: json['class'] as String,
        seating: json['seating'] as String,
      );
}

/// Today's assigned train set (schema TrainSetResponse).
class TrainSet {
  const TrainSet({required this.code, required this.traction, required this.coaches});

  final String code;

  /// "DieselPowerSet" or "DieselLocoHauled" (domain model, TrainSet).
  final String traction;
  final List<Coach> coaches;

  factory TrainSet.fromJson(Map<String, dynamic> json) => TrainSet(
        code: json['code'] as String,
        traction: json['traction'] as String,
        coaches: [for (final c in json['coaches'] as List) Coach.fromJson(c as Map<String, dynamic>)],
      );
}
