using System.Collections.Concurrent;
using Npgsql;

namespace SmartRail.IntegrationTests.LiveOperations;

// Tests that make journeys of their own must take them away again: the journey-generation test counts every journey in the
// database, so a leftover would make it fail depending on which test class happened to run first.
internal static class JourneyCleanup
{
    private static readonly ConcurrentBag<Guid> Created = [];

    public static void Remember(Guid journeyId) => Created.Add(journeyId);

    // Called when a test class is finished. The stops go with their journey (cascade).
    public static async Task RemoveAllAsync(string connectionString)
    {
        var ids = new List<Guid>();
        while (Created.TryTake(out var id))
        {
            ids.Add(id);
        }

        if (ids.Count == 0)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""DELETE FROM ops."Journeys" WHERE "Id" = ANY(@ids)""", connection);
        command.Parameters.AddWithValue("ids", ids.ToArray());
        await command.ExecuteNonQueryAsync();
    }
}
