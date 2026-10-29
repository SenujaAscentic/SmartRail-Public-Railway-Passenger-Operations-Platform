// The controller moves 1001 at Gampaha from platform 1 to platform 2 and sets a hold there (US-O5, R-22).
// conflictsAcknowledged is true so that a conflict with another train cannot refuse the change. Needs controller-login.js first.
var options = { headers: { "Content-Type": "application/json", Authorization: "Bearer " + output.token } };
var detail = json(http.get(API + "/api/v1/journeys/" + output.journeyId, {}).body);
options.body = JSON.stringify({ platform: "2", setHold: true, holdMinutes: 3, conflictsAcknowledged: true, expectedVersion: detail.version });
output.platformCalledAt = Date.now();
var response = http.post(API + "/api/v1/ops/journeys/" + output.journeyId + "/stops/" + output.gampahaId + "/platform", options);
if (!response.ok) throw new Error("platform change answered " + response.status + ": " + response.body);
