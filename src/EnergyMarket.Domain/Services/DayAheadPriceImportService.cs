using EnergyMarket.Domain.Entities;
using EnergyMarket.Domain.Repositories;
using EnergyMarket.Domain.ValueObjects;
using EnergyMarket.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace EnergyMarket.Domain.Services;

public sealed class DayAheadPriceImportService
{
    private readonly IOmieMarginalPricesClient _client;
    private readonly IDayAheadPriceValidator _validator;
    private readonly IDayAheadPriceRepository _repository;
    private readonly ILogger<DayAheadPriceImportService> _logger;

    public DayAheadPriceImportService(
        IOmieMarginalPricesClient client,
        IDayAheadPriceValidator validator,
        IDayAheadPriceRepository repository,
        ILogger<DayAheadPriceImportService> logger)
    {
        _client = client;
        _validator = validator;
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ImportResult>> ImportRangeAsync(
        DateRange range, bool forceReimport, CancellationToken ct = default)
    {
        var results = new List<ImportResult>();
        foreach (var date in range.Dates())
        {
            results.Add(await ImportSingleDayAsync(date, forceReimport, ct));
        }
        return results;
    }

    private async Task<ImportResult> ImportSingleDayAsync(DateOnly date, bool forceReimport, CancellationToken ct)
    {
        if (!forceReimport)
        {
            var spainDone = await _repository.ExistsForDateAsync(PriceZone.Spain.Id, date, ct);
            var portugalDone = await _repository.ExistsForDateAsync(PriceZone.Portugal.Id, date, ct);
            if (spainDone && portugalDone)
            {
                _logger.LogInformation("Skipping {Date}: already imported.", date);
                return new ImportResult(date, ImportStatus.Skipped, 0, 0, 0, Array.Empty<string>());
            }
        }

        Models.DayAheadPriceCandidate[] candidates;
        try
        {
            candidates = (await _client.GetPricesForDateAsync(date, ct)).ToArray();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve OMIE data for {Date}.", date);
            return new ImportResult(date, ImportStatus.Failed, 0, 0, 0, new[] { "Failed to retrieve source data." });
        }

        if (candidates.Length == 0)
        {
            _logger.LogWarning("OMIE returned no data for {Date}.", date);
            return new ImportResult(date, ImportStatus.Failed, 0, 0, 0, new[] { "No data returned." });
        }

        var validation = _validator.Validate(candidates);
        if (validation.Valid.Count == 0)
        {
            return new ImportResult(date, ImportStatus.Failed, candidates.Length, 0, validation.Rejected.Count, validation.Issues);
        }

        var entities = validation.Valid
            .Select(c => new DayAheadPrice(c.PriceZoneId,
                new ValueObjects.MarketPeriod(c.PeriodStartUtc, c.PeriodDurationMinutes), c.Price, c.SourceRetrievedAtUtc))
            .ToList();

        try
        {
            await _repository.UpsertRangeAsync(entities, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist OMIE data for {Date}.", date);
            return new ImportResult(date, ImportStatus.Failed, candidates.Length, 0, validation.Rejected.Count, new[] { "Failed to persist data." });
        }

        var status = validation.Rejected.Count == 0 ? ImportStatus.Imported : ImportStatus.PartiallyImported;
        _logger.LogInformation("Imported {Date}: {Count} rows, status {Status}.", date, entities.Count, status);
        return new ImportResult(date, status, candidates.Length, entities.Count, validation.Rejected.Count, validation.Issues);
    }
}
