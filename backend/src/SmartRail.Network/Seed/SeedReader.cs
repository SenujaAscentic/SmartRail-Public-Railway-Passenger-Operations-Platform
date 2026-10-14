using System.Globalization;
using System.Text.Json;

namespace SmartRail.Network.Seed;

// Reads the files in shared/seed/ into NetworkSeedData. It only reads; SeedValidator decides if the data is acceptable.
public static class SeedReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static NetworkSeedData Read(string folder)
    {
        var stations = ReadStations(Path.Combine(folder, "stations.csv"));
        var network = ReadJson<NetworkFile>(Path.Combine(folder, "network.json"));
        var services = ReadJson<ServicesFile>(Path.Combine(folder, "services.json"));
        var calendars = ReadJson<CalendarsFile>(Path.Combine(folder, "calendars.json"));
        var trainSets = ReadJson<TrainSetsFile>(Path.Combine(folder, "train-sets.json"));

        return new NetworkSeedData(
            stations, network.Lines, network.ZoneRadiusM, network.Routes,
            services.Services, calendars.Calendars, trainSets.TrainSets);
    }

    // stations.csv: the columns are found by header name, so their order in the file does not matter.
    private static List<StationSeed> ReadStations(string path)
    {
        var lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var header = CsvLine.Split(lines[0]).ToList();

        int Column(string name) =>
            header.IndexOf(name) is var i and >= 0 ? i : throw new InvalidDataException($"{path}: column '{name}' is missing.");

        var sequence = Column("sequence");
        var code = Column("code");
        var name = Column("name");
        var distance = Column("distance_km");
        var latitude = Column("latitude");
        var longitude = Column("longitude");
        var platforms = Column("platforms");
        var distanceSource = Column("distance_source");

        var stations = new List<StationSeed>();
        foreach (var line in lines.Skip(1))
        {
            var c = CsvLine.Split(line);
            stations.Add(new StationSeed(
                int.Parse(c[sequence], CultureInfo.InvariantCulture),
                c[code],
                c[name],
                decimal.Parse(c[distance], CultureInfo.InvariantCulture),
                double.Parse(c[latitude], CultureInfo.InvariantCulture),
                double.Parse(c[longitude], CultureInfo.InvariantCulture),
                int.Parse(c[platforms], CultureInfo.InvariantCulture),
                c[distanceSource]));
        }

        return stations;
    }

    private static T ReadJson<T>(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json)
                   ?? throw new InvalidDataException($"{path}: the file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }
    }

    // The outer objects of the JSON files ({ "services": [...] } and so on).
    private sealed record NetworkFile(IReadOnlyList<LineSeed> Lines, int ZoneRadiusM, IReadOnlyList<RouteSeed> Routes);
    private sealed record ServicesFile(IReadOnlyList<ServiceSeed> Services);
    private sealed record CalendarsFile(IReadOnlyList<CalendarSeed> Calendars);
    private sealed record TrainSetsFile(IReadOnlyList<TrainSetSeed> TrainSets);
}
