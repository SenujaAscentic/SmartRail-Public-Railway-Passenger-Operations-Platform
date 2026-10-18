namespace SmartRail.Simulator;

// Everything one simulated run needs to know. Secrets (the device secret, the password) come from the caller, never from a file in the repo.
public sealed record SimulatorOptions
{
    public required Uri ApiBase { get; init; }

    // The cab tablet: its id and secret (seeded as CAB-01.. with the secrets from user-secrets), and the driver who signs in on it.
    public required string DeviceId { get; init; }
    public required string DeviceSecret { get; init; }
    public required string StaffNumber { get; init; }
    public required string Password { get; init; }

    // Which journey to drive: either its id, or the service number with the stations (codes) and the date to find it.
    public Guid? JourneyId { get; init; }
    public string? ServiceNumber { get; init; }
    public string? FromStationCode { get; init; }
    public string? ToStationCode { get; init; }
    public DateOnly? Date { get; init; }

    public Scenario Scenario { get; init; } = Scenario.Normal;

    // How many minutes later than the timetable the train runs (it leaves late and keeps the same running times).
    public int DelayMinutes { get; init; }

    // Simulated seconds between two GPS fixes.
    public int TickSeconds { get; init; } = 10;

    // Stop scenario: minutes halted between two stations. Gap scenario: minutes without any GPS.
    public int StopMinutes { get; init; } = 5;
    public int GapMinutes { get; init; } = 5;

    // How long before departure the driver starts the journey (the system allows up to 60 minutes, US-C2).
    public int StartMinutesBeforeDeparture { get; init; } = 5;
}
