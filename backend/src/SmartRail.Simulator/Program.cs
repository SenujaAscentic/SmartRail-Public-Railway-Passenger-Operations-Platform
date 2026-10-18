using System.Globalization;
namespace SmartRail.Simulator;

// A cab tablet for demonstrations. Example (the API running in Development with the demo clock set to just before the train leaves):
//   dotnet run --project backend/src/SmartRail.Simulator -- --device CAB-01 --driver DRV-01 --service 1001 --from KDY --to FOT
// Secrets are read from the environment, never from the command line or a file:
//   SMARTRAIL_CAB_SECRET (the tablet's secret)   SMARTRAIL_DEMO_PASSWORD (the seeded demo password)
// Options: --api <url> --journey <id> --date yyyy-MM-dd --scenario normal|stop|gap --delay <minutes> --speed <n>
//          --tick <seconds> --stop-minutes <n> --gap-minutes <n>
internal static class SimulatorCli
{
    public static async Task<int> Main(string[] args)
    {
        var arguments = ParseArguments(args);
        string Need(string key) => arguments.GetValueOrDefault(key) ?? throw new SimulatorException($"Missing --{key}.");
        string Secret(string name) => Environment.GetEnvironmentVariable(name) ?? throw new SimulatorException($"Set the environment variable {name}.");

        try
        {
            var speed = double.Parse(arguments.GetValueOrDefault("speed") ?? "1", CultureInfo.InvariantCulture);
            var options = new SimulatorOptions
            {
                ApiBase = new Uri(arguments.GetValueOrDefault("api") ?? "http://localhost:5000"),
                DeviceId = Need("device"),
                DeviceSecret = Secret("SMARTRAIL_CAB_SECRET"),
                StaffNumber = Need("driver"),
                Password = Secret("SMARTRAIL_DEMO_PASSWORD"),
                JourneyId = arguments.TryGetValue("journey", out var journey) ? Guid.Parse(journey) : null,
                ServiceNumber = arguments.GetValueOrDefault("service"),
                FromStationCode = arguments.GetValueOrDefault("from"),
                ToStationCode = arguments.GetValueOrDefault("to"),
                Date = arguments.TryGetValue("date", out var date) ? DateOnly.Parse(date, CultureInfo.InvariantCulture) : null,
                Scenario = Enum.Parse<Scenario>(arguments.GetValueOrDefault("scenario") ?? "normal", ignoreCase: true),
                DelayMinutes = int.Parse(arguments.GetValueOrDefault("delay") ?? "0", CultureInfo.InvariantCulture),
                TickSeconds = int.Parse(arguments.GetValueOrDefault("tick") ?? "10", CultureInfo.InvariantCulture),
                StopMinutes = int.Parse(arguments.GetValueOrDefault("stop-minutes") ?? "5", CultureInfo.InvariantCulture),
                GapMinutes = int.Parse(arguments.GetValueOrDefault("gap-minutes") ?? "5", CultureInfo.InvariantCulture),
            };

            using var http = new HttpClient { BaseAddress = options.ApiBase };
            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };

            // Real waiting; with the API's Clock:Speed set to the same number, one simulated minute takes 60 / speed real seconds.
            var simulator = new TrainSimulator(http, options, serverNow => new RealSimulatorTime(serverNow, speed), Console.WriteLine);
            await simulator.RunAsync(stop.Token);
            return 0;
        }
        catch (Exception e) when (e is SimulatorException or FormatException or OperationCanceledException or HttpRequestException)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>();
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            result[args[i].TrimStart('-')] = args[i + 1];
        }

        return result;
    }
}
