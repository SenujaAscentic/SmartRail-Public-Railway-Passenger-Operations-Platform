namespace SmartRail.Identity;

// Identity's public door for other modules (architecture Q3, like Network's IStationDirectory): turns a staff number from seed
// data into the account id that appears as `sub` in that person's tokens. LiveOperations uses it to say which driver runs which
// journey, so that "the assigned driver" (R-07) can be checked against the token without LiveOperations reading Identity's tables.
public interface IStaffDirectory
{
    // The id of every ACTIVE DRIVER among the staff numbers. A number that does not exist, is not a driver, or is deactivated is
    // left out, so the caller can report it.
    Task<IReadOnlyDictionary<string, Guid>> GetDriverIdsAsync(IReadOnlyCollection<string> staffNumbers, CancellationToken cancellationToken);

    // The display name of an account ("R. Perera"), or null if there is no such account. Only for the one place the contract allows a
    // name in a response: the 409 VERSION_CONFLICT detail (US-O4 AC6). It must never reach a log, an event or a URL.
    Task<string?> GetDisplayNameAsync(Guid accountId, CancellationToken cancellationToken);
}
