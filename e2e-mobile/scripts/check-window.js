// One poll of the M3 window: is 1001's expected arrival at Gampaha 4 minutes away or less, on the API's own (demo) clock?
// The demo clock is read from the Date header of the answer, the same way the apps do. Sets output.windowOpen and
// output.leadSeconds. Fails if the window has been missed (less than 90 s left) or the wait takes more than 15 real minutes.
if (!output.waitStartedAt) output.waitStartedAt = Date.now();
var response = http.get(API + "/api/v1/journeys/" + output.journeyId, {});
if (!response.ok) throw new Error("journey answered " + response.status);
var headers = response.headers || {};
var dateHeader = headers["Date"] || headers["date"];
if (!dateHeader) throw new Error("The answer has no Date header");
var stop = json(response.body).stops.filter(function (s) { return s.stationId === output.gampahaId; })[0];
var leadMs = Date.parse(stop.expectedArrival) - Date.parse(dateHeader);
output.leadSeconds = Math.round(leadMs / 1000);
if (leadMs < 90 * 1000) throw new Error("The window was missed: the train is only " + output.leadSeconds + " s from Gampaha");
if (Date.now() - output.waitStartedAt > 15 * 60 * 1000) throw new Error("Waited more than 15 minutes for the window");
output.windowOpen = leadMs <= 4 * 60 * 1000;
