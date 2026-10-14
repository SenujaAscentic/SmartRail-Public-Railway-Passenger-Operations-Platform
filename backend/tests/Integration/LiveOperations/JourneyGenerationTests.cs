using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartRail.Network.Seed;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// US-B2 against a real PostgreSQL. The application clock is fixed at Wednesday 14 Oct 2026, 10:00 in Colombo (see ApiFixture),
// so "today and the next 2 days" are Wed 14, Thu 15 and Fri 16 Oct: all weekdays, so the 4 weekday services run each day.
// The tables are read with plain SQL on purpose: this test must not depend on the module's internals.
[Collection("Api")]
public class JourneyGenerationTests(ApiFixture api)
{
    private const int WeekdayServices = 4;     // 1001, 1002, 2001, 2002
    private const int StopsPerService = 11;    // every seed service has 11 stops (Pass stops included)

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<long> Count(string sql)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    // The generation runs in the background after start-up, so wait (at most 20 s) until the expected number exists.
    private async Task WaitForJourneys(long expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (await Count("""SELECT count(*) FROM ops."Journeys" """) != expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, Ct);
        }
    }

    [Fact]
    public async Task After_startup_the_journeys_for_the_next_3_days_exist()
    {
        await WaitForJourneys(3 * WeekdayServices);

        Assert.Equal(3 * WeekdayServices, await Count("""SELECT count(*) FROM ops."Journeys" """));
        Assert.Equal(3 * WeekdayServices * StopsPerService, await Count("""SELECT count(*) FROM ops."JourneyStops" """));
        Assert.Equal(WeekdayServices, await Count("""SELECT count(*) FROM ops."Journeys" WHERE "ServiceDate" = DATE '2026-10-14'"""));
        Assert.Equal(WeekdayServices, await Count("""SELECT count(*) FROM ops."Journeys" WHERE "ServiceDate" = DATE '2026-10-16'"""));
        Assert.Equal(0, await Count("""SELECT count(*) FROM ops."Journeys" WHERE "ServiceDate" NOT BETWEEN DATE '2026-10-14' AND DATE '2026-10-16'"""));
        Assert.Equal(0, await Count("""SELECT count(*) FROM ops."Journeys" WHERE "Status" <> 'Planned'"""));
    }

    [Fact]
    public async Task Times_are_stored_in_utc()
    {
        await WaitForJourneys(3 * WeekdayServices);

        // Express 1001 leaves Fort at 06:30 in Colombo (UTC+05:30) = 01:00 UTC. It is the only first stop leaving then.
        Assert.Equal(1, await Count("""
            SELECT count(*) FROM ops."JourneyStops"
            WHERE "Sequence" = 1 AND "ScheduledDeparture" = TIMESTAMPTZ '2026-10-14 01:00:00+00'
            """));
    }

    [Fact]
    public async Task A_restart_adds_no_journeys()
    {
        await WaitForJourneys(3 * WeekdayServices);
        var idsBefore = await Count("""SELECT count(*) FROM ops."Journeys" """);

        // A second copy of the application against the same database, as after a restart.
        var log = new LogCapture("JourneyGenerationService", LogLevel.Information);
        using var restarted = api.CreateFactory(builder => builder.ConfigureLogging(logging => logging.AddProvider(log)));
        _ = restarted.Server;

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!log.Messages.Any(m => m.Message.Contains("finished")) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, Ct);
        }

        Assert.Contains(log.Messages, m => m.Message.Contains("0 journeys added"));
        Assert.Equal(idsBefore, await Count("""SELECT count(*) FROM ops."Journeys" """));
        Assert.Equal(3 * WeekdayServices * StopsPerService, await Count("""SELECT count(*) FROM ops."JourneyStops" """));
    }

    [Fact]
    public async Task The_tables_are_in_the_ops_schema_and_the_database_refuses_a_duplicate()
    {
        await WaitForJourneys(3 * WeekdayServices);

        Assert.Equal(2, await Count("""
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema = 'ops' AND table_name IN ('Journeys', 'JourneyStops')
            """));

        // A second journey for a service on a date that already has one is refused by the unique index.
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO ops."Journeys" ("Id", "ServiceId", "ServiceDate", "TrainSetId", "Status")
            SELECT gen_random_uuid(), "ServiceId", "ServiceDate", "TrainSetId", 'Planned' FROM ops."Journeys" LIMIT 1
            """, connection);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(Ct));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
    }

    [Fact]
    public void A_wrong_assignment_file_stops_the_application_from_starting()
    {
        var folder = Path.Combine(Path.GetTempPath(), "smartrail-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            foreach (var file in Directory.GetFiles(SeedFolder.Find(null)))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            }

            var assignments = Path.Combine(folder, "service-assignments.json");
            File.WriteAllText(assignments, File.ReadAllText(assignments).Replace("\"LH-02\"", "\"NO-SUCH-SET\""));

            using var broken = api.CreateFactory(builder =>
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:Path"] = folder })));

            var failure = Assert.ThrowsAny<Exception>(() => broken.Server);

            Assert.Contains("NO-SUCH-SET", string.Join(" | ", Messages(failure)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static IEnumerable<string> Messages(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            yield return exception.Message;
        }
    }
}
