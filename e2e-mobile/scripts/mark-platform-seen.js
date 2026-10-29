// Runs right after the app showed the platform change: the time since the API call, with Maestro's own polling.
output.platformLatencyMs = Date.now() - output.platformCalledAt;
