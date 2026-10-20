using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartRail.Simulator;

// What a run did.
public sealed record SimulationResult(Guid JourneyId, int Posts, int Accepted, int Discarded);

// The simulator could not do its job; the message says what the server answered.
public sealed class SimulatorException(string message) : Exception(message);

// Plays a cab tablet: signs the driver in, finds the journey, starts it, sends GPS fixes along the timetable and ends it.
// It talks to the system only through its public API, exactly like the real crew app will, so a run proves the whole chain
// (positions -> rules -> events -> stream). It knows no backend code.
public sealed class TrainSimulator(HttpClient http, SimulatorOptions options, Func<DateTimeOffset, ISimulatorTime> createTime, Action<string> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ISimulatorTime _time = null!;
    private string _accessToken = "";
    private string _refreshToken = "";
    private DateTimeOffset _accessExpiresAt;

    public async Task<SimulationResult> RunAsync(CancellationToken cancellationToken)
    {
        // The tablet learns the server's time from the Date header of a public answer. The machine's own clock may be hours or days
        // away from a server running the demo clock, and the server refuses a sign-in from a tablet more than 24 hours off (US-C1).
        using var linesRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lines");
        var linesResponse = await http.SendAsync(linesRequest, cancellationToken);
        var serverNow = linesResponse.Headers.Date ?? DateTimeOffset.UtcNow;
        var lines = await ReadAsync<List<LineDto>>(linesResponse, "GET /api/v1/lines", cancellationToken);

        await SignInAsync(serverNow, cancellationToken);

        var (journeyId, stops) = await FindJourneyAsync(lines, cancellationToken);
        var shift = TimeSpan.FromMinutes(options.DelayMinutes);
        var timeline = Timeline.Build(stops, shift, options.Scenario, TimeSpan.FromMinutes(options.StopMinutes), TimeSpan.FromMinutes(options.GapMinutes));
        log($"Journey {journeyId}: {stops.Count} stops, leaves {stops[0].Departure:HH:mm} UTC (+{options.DelayMinutes} min), scenario {options.Scenario}.");

        // Start shortly before the scheduled departure. The server refuses a start more than 60 minutes early (US-C2).
        var startAt = stops[0].Departure!.Value - TimeSpan.FromMinutes(options.StartMinutesBeforeDeparture);
        if (_time.Now < startAt)
        {
            await _time.WaitAsync(startAt - _time.Now, cancellationToken);
        }

        await PostAsync($"/api/v1/crew/journeys/{journeyId}/start", new { clientActionId = Guid.NewGuid() }, cancellationToken);
        log("Journey started.");

        int posts = 0, accepted = 0, discarded = 0;
        var tick = TimeSpan.FromSeconds(options.TickSeconds);
        var gate = new HoldGate();
        var observed = new Observed();
        while (true)
        {
            var now = _time.Now;

            // The controller's decisions are read from the public journey, like any passenger app would: a hold keeps the train at its
            // station, and a platform change is announced (the driver would see it on the tablet).
            var journey = await ObserveAsync(journeyId, stops, observed, cancellationToken);
            var held = gate.Update(now, timeline, journey?.HoldIndex, tick);
            var trainTime = gate.TrainTime(now);

            if (!timeline.IsSilent(trainTime))
            {
                var result = await SendFixAsync(journeyId, timeline.At(trainTime), now, cancellationToken);
                posts++;
                accepted += result.Accepted;
                discarded += result.Discarded.Count;
                foreach (var d in result.Discarded)
                {
                    log($"Fix discarded: {d.Reason}");
                }
            }

            // The train keeps reporting for a minute at the terminus: a station counts as reached only after the train has been
            // slow inside it for 20 seconds (R-04), and the last station is the one that ends the journey.
            if (!held && trainTime >= timeline.End + TimeSpan.FromMinutes(1))
            {
                break;
            }

            await _time.WaitAsync(tick, cancellationToken);
        }

        await PostAsync($"/api/v1/crew/journeys/{journeyId}/end", new { clientActionId = Guid.NewGuid() }, cancellationToken);
        log($"Journey ended. {posts} batches sent, {accepted} fixes accepted, {discarded} discarded.");
        return new SimulationResult(journeyId, posts, accepted, discarded);
    }

    private async Task SignInAsync(DateTimeOffset serverNow, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/crew/cab-sign-in")
        {
            Content = JsonContent.Create(new { staffNumber = options.StaffNumber, password = options.Password, deviceTime = serverNow }, options: Json),
        };
        request.Headers.Add("X-Device-Id", options.DeviceId);
        request.Headers.Add("X-Device-Secret", options.DeviceSecret);

        var body = await ReadAsync<CabSignInResponse>(await http.SendAsync(request, ct), "cab sign-in", ct);
        _time = createTime(body.ServerTime);   // from now on the tablet's clock is the server's clock
        _accessToken = body.AccessToken;
        _refreshToken = body.RefreshToken;
        _accessExpiresAt = body.ExpiresAt;
        log($"Signed in as {options.StaffNumber} on {options.DeviceId}.");
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { refreshToken = _refreshToken }, options: Json),
        };
        var body = await ReadAsync<AccessTokenResponse>(await http.SendAsync(request, ct), "token refresh", ct);
        (_accessToken, _refreshToken, _accessExpiresAt) = (body.AccessToken, body.RefreshToken, body.ExpiresAt);
        log("Access token refreshed.");
    }

    private async Task<(Guid JourneyId, List<SimStop> Stops)> FindJourneyAsync(List<LineDto> lines, CancellationToken ct)
    {
        var stations = lines.SelectMany(l => l.Routes).SelectMany(r => r.Stations)
            .GroupBy(s => s.StationId).ToDictionary(g => g.Key, g => g.First());

        var journeyId = options.JourneyId;
        if (journeyId is null)
        {
            if (options.ServiceNumber is null || options.FromStationCode is null || options.ToStationCode is null)
            {
                throw new SimulatorException("Give a journey id, or a service number with the codes of the first and last station.");
            }

            Guid Station(string code) => stations.Values.FirstOrDefault(s => s.Code == code)?.StationId
                ?? throw new SimulatorException($"No station has the code {code}.");

            var date = options.Date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_time.Now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo")).DateTime);
            var found = await GetAsync<List<SummaryDto>>(
                $"/api/v1/journeys/search?from={Station(options.FromStationCode)}&to={Station(options.ToStationCode)}&date={date:yyyy-MM-dd}&limit=50", ct);
            journeyId = found.FirstOrDefault(j => j.ServiceNumber == options.ServiceNumber)?.JourneyId
                ?? throw new SimulatorException($"Service {options.ServiceNumber} was not found on {date:yyyy-MM-dd} (past departures are hidden today).");
        }

        var detail = await GetAsync<DetailDto>($"/api/v1/journeys/{journeyId}", ct);
        var stops = detail.Stops.OrderBy(s => s.Sequence).Select(s =>
        {
            var station = stations.GetValueOrDefault(s.StationId)
                ?? throw new SimulatorException($"Station {s.Name} has no coordinates in /lines.");
            return new SimStop(s.Name, s.CallType == "Stop", s.ScheduledArrival, s.ScheduledDeparture, station.Latitude, station.Longitude, s.StationId);
        }).ToList();
        return (journeyId.Value, stops);
    }

    // What the simulator has already told the log, so each decision is announced once.
    private sealed class Observed
    {
        public Guid? HeldStation;
        public Dictionary<Guid, string?> Platforms = [];
    }

    private sealed record JourneyView(int? HoldIndex);

    // Reads the public journey. A failed read is skipped (the train just keeps going on what it knew): the tablet must not stop
    // because one request failed.
    private async Task<JourneyView?> ObserveAsync(Guid journeyId, List<SimStop> stops, Observed seen, CancellationToken ct)
    {
        DetailDto detail;
        try
        {
            detail = await GetAsync<DetailDto>($"/api/v1/journeys/{journeyId}", ct);
        }
        catch (Exception e) when (e is SimulatorException or HttpRequestException)
        {
            return null;
        }

        int? holdIndex = null;
        var holdStation = detail.Hold?.StationId;
        if (holdStation is { } held)
        {
            var index = stops.FindIndex(s => s.StationId == held);
            holdIndex = index < 0 ? null : index;
            if (seen.HeldStation != held)
            {
                var minutes = detail.Hold!.Minutes is { } m ? $" for {m} min" : "";
                log($"Hold received: waiting at {(index < 0 ? "a station" : stops[index].Name)}{minutes}.");
            }
        }
        else if (seen.HeldStation is { } before)
        {
            var name = stops.Find(s => s.StationId == before)?.Name ?? "the station";
            log($"Hold released: leaving {name}.");
        }

        seen.HeldStation = holdStation;

        foreach (var stop in detail.Stops)
        {
            if (seen.Platforms.TryGetValue(stop.StationId, out var known) && known != stop.Platform)
            {
                log($"Platform changed at {stop.Name}: platform {stop.Platform}{(stop.PreviousPlatform is null ? "" : $" (was {stop.PreviousPlatform})")}.");
            }

            seen.Platforms[stop.StationId] = stop.Platform;
        }

        return new JourneyView(holdIndex);
    }

    private async Task<PositionBatchResultDto> SendFixAsync(Guid journeyId, SimFix fix, DateTimeOffset now, CancellationToken ct)
    {
        var batch = new
        {
            clientActionId = Guid.NewGuid(),
            positions = new[]
            {
                new { lat = fix.Latitude, lng = fix.Longitude, speedKmh = fix.SpeedKmh, accuracyM = 5.0, gpsTime = now, deviceTime = now },
            },
        };
        var response = await PostAsync($"/api/v1/crew/journeys/{journeyId}/positions", batch, ct);
        return await ReadAsync<PositionBatchResultDto>(response, "position batch", ct);
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object body, CancellationToken ct)
    {
        // A token that is about to expire is replaced first.
        if (_accessExpiresAt - _time.Now < TimeSpan.FromMinutes(1))
        {
            await RefreshAsync(ct);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new SimulatorException($"POST {path} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        }

        return response;
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        return await ReadAsync<T>(await http.SendAsync(request, ct), $"GET {path}", ct);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, string what, CancellationToken ct)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new SimulatorException($"{what} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        }

        return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new SimulatorException($"{what} answered with nothing.");
    }

    // The parts of the public answers the simulator reads (its own copies: it must not reference backend code).
    private sealed record CabSignInResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt, Guid CrewSessionId, DateTimeOffset ServerTime);
    private sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt);
    private sealed record LineDto(List<RouteDto> Routes);
    private sealed record RouteDto(List<StationDto> Stations);
    private sealed record StationDto(Guid StationId, string Code, double Latitude, double Longitude);
    private sealed record SummaryDto(Guid JourneyId, string ServiceNumber);
    private sealed record DetailDto(List<StopDto> Stops, HoldDto? Hold = null);
    private sealed record HoldDto(Guid StationId, int? Minutes);
    private sealed record StopDto(int Sequence, Guid StationId, string Name, string CallType, DateTimeOffset? ScheduledArrival, DateTimeOffset? ScheduledDeparture, string? Platform = null, string? PreviousPlatform = null);
    private sealed record PositionBatchResultDto(int Accepted, List<DiscardedDto> Discarded);
    private sealed record DiscardedDto(int Index, string Reason);
}
