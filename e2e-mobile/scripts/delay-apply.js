// The controller delays 1001 by 5 minutes (US-O4), reason Signal failure. Records the wall-clock time of the call.
// Needs controller-login.js to have run, and the flow env API.
function call(method, path, body) {
  var options = { headers: { "Content-Type": "application/json", Authorization: "Bearer " + output.token } };
  if (body) options.body = JSON.stringify(body);
  var response = method === "GET" ? http.get(API + path, options) : http.post(API + path, options);
  if (!response.ok) throw new Error(method + " " + path + " answered " + response.status + ": " + response.body);
  return json(response.body);
}

var detail = call("GET", "/api/v1/journeys/" + output.journeyId);
output.delayCalledAt = Date.now();
call("POST", "/api/v1/ops/journeys/" + output.journeyId + "/delay", { minutes: 5, reasonCode: "SignalFailure", note: "Signal fault near Ragama", expectedVersion: detail.version });
console.log("delay of 5 min sent at " + output.delayCalledAt);
