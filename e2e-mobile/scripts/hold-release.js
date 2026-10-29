// The controller releases the hold at Gampaha, so the train (which waits for it) leaves. Needs controller-login.js first.
var detail = json(http.get(API + "/api/v1/journeys/" + output.journeyId, {}).body);
if (!detail.hold) throw new Error("There is no hold to release");
var options = { headers: { "Content-Type": "application/json", Authorization: "Bearer " + output.token }, body: JSON.stringify({ expectedVersion: detail.version }) };
var response = http.post(API + "/api/v1/ops/journeys/" + output.journeyId + "/hold/release", options);
if (!response.ok) throw new Error("hold release answered " + response.status + ": " + response.body);
