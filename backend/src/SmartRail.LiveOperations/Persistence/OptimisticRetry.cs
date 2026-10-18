using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SmartRail.LiveOperations.Persistence;

// R-18: automatic updates (positions, detected stops, the tracking timer) are applied to the LATEST state and retried when the
// database says someone else saved first. The journey carries a row version (Postgres xmin); a collision throws
// DbUpdateConcurrencyException; the work is then run again from a fresh load, up to three times. The same retry covers two identical
// crew requests racing each other (the second breaks the idempotency primary key and, on the retry, is answered from the store).
internal static class OptimisticRetry
{
    public const int Attempts = 3;

    // `work` must load what it needs itself (it runs again from scratch) and save once at the end.
    public static async Task<T> RunAsync<T>(LiveOperationsDbContext db, Func<Task<T>> work)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await work();
            }
            catch (Exception ex) when (attempt < Attempts && IsCollision(ex))
            {
                db.ChangeTracker.Clear();   // forget everything loaded, including events that were never saved
            }
        }
    }

    public static bool IsCollision(Exception ex) =>
        ex is DbUpdateConcurrencyException
        || ex is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
}
