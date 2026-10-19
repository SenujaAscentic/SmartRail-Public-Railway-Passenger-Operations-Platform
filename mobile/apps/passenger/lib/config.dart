/// API base URL, set with `--dart-define=API_BASE_URL=...`.
/// The default is how the Android emulator reaches the PC's localhost.
const apiBaseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://10.0.2.2:5045');
