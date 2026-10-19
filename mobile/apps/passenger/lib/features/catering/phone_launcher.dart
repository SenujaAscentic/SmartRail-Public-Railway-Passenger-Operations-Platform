import 'package:url_launcher/url_launcher.dart';

/// Opens a `tel:` address in the phone's dialler. Returns false when nothing
/// could handle it. Passed in (like the api and the clock), so tests use a fake
/// and never launch anything.
typedef PhoneLauncher = Future<bool> Function(Uri uri);

/// The real one: the `url_launcher` package (flutter.dev) hands the address to the phone app.
Future<bool> openDialler(Uri uri) => launchUrl(uri);
