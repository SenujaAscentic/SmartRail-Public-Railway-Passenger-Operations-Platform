namespace SmartRail.Identity.Model;

// A registered cab tablet (ADR-05). Identity owns the credential; LiveOperations later learns the device and train set
// from the cab session token and never reads this table. Only a hash of the device secret is stored.
internal class CabDevice
{
    public Guid Id { get; set; }

    // The natural key from the seed file ("CAB-01"), which the seed loader finds the row by.
    public string Code { get; set; } = "";

    // Text on purpose: the train set belongs to the Network module.
    public string TrainSetCode { get; set; } = "";

    public string CredentialHash { get; set; } = "";

    // A deactivated device is refused at its next sign-in or refresh (threat T-12), so within one access-token life (15 min).
    public bool Active { get; set; } = true;
}
