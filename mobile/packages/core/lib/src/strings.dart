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
}
