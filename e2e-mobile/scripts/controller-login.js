// Signs in as the controller (CTRL-01) and finds train 1001 (Gampaha to Kandy, today) through the public search.
// Keeps token, journeyId and the station ids in `output`, which the later scripts of the same flow can read.
// Needs the flow env: API (e.g. http://localhost:5098) and CONTROLLER_PASSWORD.
function call(method, path, token, body) {
  var options = { headers: { "Content-Type": "application/json" } };
  if (token) options.headers["Authorization"] = "Bearer " + token;
  if (body) options.body = JSON.stringify(body);
  var response = method === "GET" ? http.get(API + path, options) : http.post(API + path, options);
  if (!response.ok) throw new Error(method + " " + path + " answered " + response.status + ": " + response.body);
  return json(response.body);
}

var signIn = call("POST", "/api/v1/auth/staff/sign-in", null, { staffNumber: "CTRL-01", password: CONTROLLER_PASSWORD });
output.token = signIn.accessToken;

var gampaha = call("GET", "/api/v1/stations?query=Gampaha")[0].id;
var kandy = call("GET", "/api/v1/stations?query=Kandy")[0].id;
output.gampahaId = gampaha;
var today = new Date(Date.now() + 5.5 * 3600 * 1000).toISOString().slice(0, 10); // today in Asia/Colombo (the emulator's clock decides the app's date)
var found = call("GET", "/api/v1/journeys/search?from=" + gampaha + "&to=" + kandy + "&date=" + today + "&limit=50");
var train = found.filter(function (j) { return j.serviceNumber === "1001"; })[0];
if (!train) throw new Error("Service 1001 is not in the search for " + today);
output.journeyId = train.journeyId;
console.log("controller signed in; journey 1001 = " + output.journeyId);
