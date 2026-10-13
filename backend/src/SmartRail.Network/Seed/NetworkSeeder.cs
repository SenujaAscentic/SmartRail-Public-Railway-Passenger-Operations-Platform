using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartRail.Network.Model;
using SmartRail.Network.Persistence;

namespace SmartRail.Network.Seed;

// Loads shared/seed/ into the network schema. Safe to run on every start:
//  - the data is validated first; if it is invalid nothing is written and the exception stops the application;
//  - rows that already exist are found by their natural key (station code, route code, service number ...)
//    and updated in place, so their IDs never change and nothing is duplicated;
//  - the child rows (platforms, route stations, stops, calendar exceptions, coaches) have no identity of their own
//    that anything refers to, so they are deleted and written again, all inside one transaction.
// A row that is removed from the seed files stays in the database: other modules may already refer to it.
public sealed class NetworkSeeder(NetworkDbContext db, ILogger<NetworkSeeder> logger)
{
    public async Task SeedAsync(string seedFolder, CancellationToken ct = default)
    {
        var data = SeedReader.Read(seedFolder);
        var result = SeedValidator.Validate(data);

        foreach (var warning in result.Warnings)
        {
            logger.LogWarning("Network seed: {Message}", warning.Message);
        }

        if (!result.IsValid)
        {
            throw new SeedDataException(result.Errors);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.Platforms.ExecuteDeleteAsync(ct);
        await db.RouteStations.ExecuteDeleteAsync(ct);
        await db.ScheduledStops.ExecuteDeleteAsync(ct);
        await db.CalendarExceptions.ExecuteDeleteAsync(ct);
        await db.Coaches.ExecuteDeleteAsync(ct);

        var lines = await db.Lines.ToDictionaryAsync(x => x.Code, ct);
        var stations = await db.Stations.ToDictionaryAsync(x => x.Code, ct);
        var routes = await db.Routes.ToDictionaryAsync(x => x.Code, ct);
        var calendars = await db.Calendars.ToDictionaryAsync(x => x.Code, ct);
        var services = await db.Services.ToDictionaryAsync(x => x.Number, ct);
        var trainSets = await db.TrainSets.ToDictionaryAsync(x => x.Code, ct);

        foreach (var row in data.Lines)
        {
            var line = GetOrAdd(lines, row.Id, () => new Line { Id = Guid.CreateVersion7(), Code = row.Id });
            line.Name = row.Name;
        }

        foreach (var row in data.Stations)
        {
            var station = GetOrAdd(stations, row.Code, () => new Station { Id = Guid.CreateVersion7(), Code = row.Code });
            station.Name = row.Name;
            station.Latitude = row.Latitude;
            station.Longitude = row.Longitude;
            station.ZoneRadiusM = data.ZoneRadiusM;

            // Platforms are numbered 1 to N; the seed only knows how many a station has.
            for (var number = 1; number <= row.Platforms; number++)
            {
                db.Platforms.Add(new Platform { Id = Guid.CreateVersion7(), StationId = station.Id, Number = number.ToString(), InService = true });
            }
        }

        foreach (var row in data.Routes)
        {
            var route = GetOrAdd(routes, row.Id, () => new Route { Id = Guid.CreateVersion7(), Code = row.Id });
            route.LineId = lines[row.LineId].Id;
            route.Direction = Enum.Parse<Direction>(row.Direction);
            route.Name = row.Name;

            foreach (var routeStation in row.Stations)
            {
                db.RouteStations.Add(new RouteStation
                {
                    Id = Guid.CreateVersion7(),
                    RouteId = route.Id,
                    Sequence = routeStation.Sequence,
                    StationId = stations[routeStation.StationCode].Id,
                    DistanceKm = routeStation.DistanceKm,
                });
            }
        }

        foreach (var row in data.Calendars)
        {
            var calendar = GetOrAdd(calendars, row.Id, () => new ServiceCalendar { Id = Guid.CreateVersion7(), Code = row.Id });
            calendar.DaysOfWeek = row.DaysOfWeek.Select(day => SeedDays.ByName[day]).ToArray();

            foreach (var exception in row.Exceptions)
            {
                db.CalendarExceptions.Add(new CalendarException
                {
                    Id = Guid.CreateVersion7(),
                    CalendarId = calendar.Id,
                    Date = exception.Date,
                    Runs = exception.Runs,
                    Name = exception.Name,
                });
            }
        }

        foreach (var row in data.Services)
        {
            var service = GetOrAdd(services, row.Number, () => new Service { Id = Guid.CreateVersion7(), Number = row.Number });
            service.RouteId = routes[row.RouteId].Id;
            service.CalendarId = calendars[row.CalendarId].Id;
            service.TrainType = Enum.Parse<TrainType>(row.TrainType);

            foreach (var stop in row.Stops)
            {
                db.ScheduledStops.Add(new ScheduledStop
                {
                    Id = Guid.CreateVersion7(),
                    ServiceId = service.Id,
                    Sequence = stop.Sequence,
                    StationId = stations[stop.StationCode].Id,
                    CallType = Enum.Parse<CallType>(stop.CallType),
                    ArrivalOffset = stop.ArrivalOffset,
                    DepartureOffset = stop.DepartureOffset,
                    PlannedPlatform = stop.PlannedPlatform,
                });
            }
        }

        foreach (var row in data.TrainSets)
        {
            var trainSet = GetOrAdd(trainSets, row.Code, () => new TrainSet { Id = Guid.CreateVersion7(), Code = row.Code });
            trainSet.Traction = Enum.Parse<Traction>(row.Traction);

            foreach (var coach in row.Coaches)
            {
                db.Coaches.Add(new Coach
                {
                    Id = Guid.CreateVersion7(),
                    TrainSetId = trainSet.Id,
                    Position = coach.Position,
                    Class = Enum.Parse<CoachClass>(coach.Class),
                    Seating = Enum.Parse<SeatingType>(coach.Seating),
                });
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Network seed loaded: {Stations} stations, {Routes} routes, {Services} services.",
            data.Stations.Count, data.Routes.Count, data.Services.Count);
    }

    // Finds the existing row, or creates a new one (the caller gives it a fresh ID) and adds it to the context.
    private T GetOrAdd<T>(Dictionary<string, T> existing, string key, Func<T> create) where T : class
    {
        if (existing.TryGetValue(key, out var found))
        {
            return found;
        }

        var created = create();
        db.Add(created);
        existing[key] = created;
        return created;
    }
}
