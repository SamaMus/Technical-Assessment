using EnergyMarket.Domain.Common;
using EnergyMarket.Domain.Entities;
using EnergyMarket.Domain.Models;
using EnergyMarket.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EnergyMarket.Infrastructure.Omie;

public sealed class OmieMarginalPricesClient : IOmieMarginalPricesClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OmieMarginalPricesClient> _logger;

    public OmieMarginalPricesClient(HttpClient httpClient, ILogger<OmieMarginalPricesClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DayAheadPriceCandidate>> GetPricesForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var filename = $"marginalpdbc_{date:yyyyMMdd}.1";
        var requestUri = $"?parents[0]=marginalpdbc&filename={filename}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUri, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            throw new OmieClientException($"Network error retrieving OMIE file for {date}.", ex);
        }

        if (!response.IsSuccessStatusCode)
            throw new OmieClientException($"OMIE returned status {(int)response.StatusCode} for {date}.");

        var text = await response.Content.ReadAsStringAsync(ct);
        var rows = MarginalPdbcParser.Parse(text, _logger)
            .Where(r => r.PriceEs.HasValue || r.PricePt.HasValue || true) // keep all rows; nulls are valid
            .ToList();

        if (rows.Count == 0)
            return Array.Empty<DayAheadPriceCandidate>();

        var retrievedAtUtc = DateTimeOffset.UtcNow;
        var midnightUtc = MadridCalendar.GetUtcMidnight(date);

        var candidates = new List<DayAheadPriceCandidate>(rows.Count * 2);
        foreach (var row in rows.OrderBy(r => r.Hour))
        {
            // Adding whole UTC hours from Madrid local midnight is DST-safe by
            // construction: OMIE's own row count for the day (23/24/25) already
            // reflects the local day's actual length.
            var periodStartUtc = midnightUtc.AddHours(row.Hour - 1);

            candidates.Add(new DayAheadPriceCandidate(PriceZone.Spain.Id, periodStartUtc, 60, row.PriceEs, retrievedAtUtc));
            candidates.Add(new DayAheadPriceCandidate(PriceZone.Portugal.Id, periodStartUtc, 60, row.PricePt, retrievedAtUtc));
        }

        return candidates;
    }
}
