// Runs right after the app showed "5 min late": the time since the API call (includes Maestro's own polling, about a second).
var ms = Date.now() - output.delayCalledAt;
console.log("LATENCY delay: API call to '5 min late' visible = " + ms + " ms");
output.delayLatencyMs = ms;
