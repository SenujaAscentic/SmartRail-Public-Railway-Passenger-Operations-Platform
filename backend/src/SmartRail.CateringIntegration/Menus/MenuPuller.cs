using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRail.CateringIntegration.Persistence;
using SmartRail.CateringIntegration.Relay;
using SmartRail.Network;

namespace SmartRail.CateringIntegration.Menus;

// Fetches the provider's stations and menus (US-F3) and keeps the last good answer in the cache. Failing never throws to the caller and
// never empties the cache: the endpoint then serves what it has until it is too old (CateringRules.MaxCacheAge).
internal sealed class MenuPuller(IHttpClientFactory httpClients, IOptions<CateringOptions> options, TimeProvider time, ILogger<MenuPuller> logger)
{
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(5);

    public async Task PullAsync(CateringDbContext db, INetworkCatalog network, CancellationToken ct)
    {
        try
        {
            var catering = await network.GetCateringStationsAsync(ct);
            var stations = await GetAsync("/provider-api/v1/stations", ct);
            var now = time.GetUtcNow().UtcDateTime;

            foreach (var station in stations.EnumerateArray())
            {
                var stationRef = station.GetProperty("ref").GetString()!;
                if (!catering.Any(c => c.Code == stationRef))
                {
                    continue;   // a station SmartRail does not know as a catering station is ignored
                }

                var menu = await GetAsync($"/provider-api/v1/stations/{Uri.EscapeDataString(stationRef)}/menu", ct);
                var items = menu.GetProperty("items").EnumerateArray()
                    .Select(i => new MenuEntry(i.GetProperty("name").GetString()!, i.GetProperty("priceLkr").GetInt32(), i.GetProperty("available").GetBoolean()))
                    .ToList();

                var row = await db.CachedStations.SingleOrDefaultAsync(c => c.StationRef == stationRef, ct);
                if (row is null)
                {
                    row = new CachedStation { StationRef = stationRef };
                    db.CachedStations.Add(row);
                }

                var hours = station.GetProperty("openingHours");
                row.Name = station.GetProperty("name").GetString()!;
                row.Hotline = station.GetProperty("hotline").GetString()!;
                row.Opens = hours.GetProperty("opens").GetString()!;
                row.Closes = hours.GetProperty("closes").GetString()!;
                row.ProviderAvailable = station.GetProperty("available").GetBoolean();
                row.MenuJson = JsonSerializer.Serialize(items);
                row.FetchedAt = now;
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // Only the kind of failure is logged: no response text, no key.
            logger.LogWarning("Catering menu pull failed: {ErrorType}", e.GetType().Name);
            db.ChangeTracker.Clear();
        }
    }

    private async Task<JsonElement> GetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(options.Value.ProviderUrl!), path));
        request.Headers.Add("X-Api-Key", options.Value.ProviderApiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await httpClients.CreateClient("railbites").SendAsync(request, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
        return document.RootElement.Clone();
    }
}
