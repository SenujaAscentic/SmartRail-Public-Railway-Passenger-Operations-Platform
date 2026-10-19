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
  /// The name already holds the train type ("Kandy Express"), so the type is not repeated.
  static String trainLine(String number, String from, String to) => 'Train $number · $from → $to';
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

  // Disruptions (wording for approval, see mobile/NOTES.md)
  static const nextTrains = 'Next trains';
  static const otherTrains = 'Other trains';
  static const noOtherTrains = 'No other direct trains on this date';
  static const otherTrainsError = "Couldn't load other trains";

  /// "Signal failure" for the code the controller picked; an unknown code is shown as sent.
  static String reasonLabel(String code) => switch (code) {
        'SignalFailure' => 'Signal failure',
        'TrackWork' => 'Track work',
        'TrainFault' => 'Train fault',
        'Weather' => 'Weather',
        'CrewShortage' => 'Crew shortage',
        'Congestion' => 'Congestion',
        'Other' => 'Other',
        _ => code,
      };

  /// "Reason: Signal failure · signal fault near Ragama". The note is plain text.
  /// For the code "Other" the note alone says it ("Reason: Bridge inspection").
  static String reasonLine(String code, String? note) {
    final text = note?.trim();
    final hasNote = text != null && text.isNotEmpty;
    if (code == 'Other' && hasNote) return 'Reason: $text';
    return hasNote ? 'Reason: ${reasonLabel(code)} · $text' : 'Reason: ${reasonLabel(code)}';
  }

  // Tracking: waiting and on board (US-P3). Wording for approval, see mobile/NOTES.md.
  static const trackThisTrain = 'Track this train';
  static const waiting = 'Waiting';
  static const onBoard = 'On board';
  static const imOnThisTrain = "I'm on this train";
  static const stopTracking = 'Stop tracking';
  static const platformWord = 'Platform';
  static const scheduledLabel = 'Scheduled';
  static const whereTheTrainIs = 'Where the train is';
  static const nextStop = 'Next stop';
  static const areYouOnBoard = 'Are you on board?';
  static const yesOnBoard = "Yes, I'm on board";
  static const noMissedIt = 'No, I missed it';
  static String expectedAt(String station) => 'Expected at $station';
  static String arrivedAt(String station) => 'Arrived at $station';
  static String leftStation(String station) => 'Left $station';
  static String arrivesAt(String station) => 'Arrives at $station';
  static String lastPassed(String station, String time) => 'Last passed $station at $time';
  static String trainLeft(String train, String station, String time) => '$train left $station at $time';
}

