/// Wording of the passenger UI that is NOT a status. Status texts (delay,
/// platform, freshness, ...) come only from shared/status-fixtures.json via
/// status_texts.dart. Keeping this wording in one file makes it easy to review
/// and, later, to translate.
abstract final class Strings {
  // Stop list
  static const yourStation = 'Your station';
  static const destination = 'destination';
  static String foodAvailableAt(String station) => 'Food available at $station';

  // Empty and error states
  static const noDirectTrains = 'No direct trains between these stations on this date';
  static const loadError = "Couldn't load train information";
  static const tryAgain = 'Try again';

  // Search screen
  static const searchTitle = 'Find a train';
  static const from = 'From';
  static const to = 'To';
  static const date = 'Date';
  static const search = 'Search';
  static const stationHint = 'Type a station name';

  // Train details
  static const refresh = 'Refresh';
  static const stationColumn = 'Station';
  static const scheduledColumn = 'Sched.';
  static const expectedColumn = 'Exp.';
  static const usuallyPowerSet = 'Usually: power set';
  static String trainLine(String number, String type, String from, String to) => 'Train $number · $type · $from → $to';
  static String betweenStations(String a, String b) => 'Between $a and $b';
  static String atStation(String name) => 'At $name';

  /// "Diesel power set" or "Diesel locomotive-hauled" (US-P2 AC4); an unknown value is shown as sent.
  static String traction(String value) => switch (value) {
        'DieselPowerSet' => 'Diesel power set',
        'DieselLocoHauled' => 'Diesel locomotive-hauled',
        _ => value,
      };

  /// "1st class" for "First", and so on; an unknown value is shown as sent.
  static String coachClass(String value) => switch (value) {
        'First' => '1st class',
        'Second' => '2nd class',
        'Third' => '3rd class',
        _ => value,
      };

  /// "reserved" / "unreserved"; an unknown value is shown as sent.
  static String seating(String value) => switch (value) {
        'Reserved' => 'reserved',
        'Unreserved' => 'unreserved',
        _ => value,
      };
}
