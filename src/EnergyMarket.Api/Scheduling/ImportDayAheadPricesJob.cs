using EnergyMarket.Domain.Common;
using EnergyMarket.Domain.Services;
using EnergyMarket.Domain.ValueObjects;
using Quartz;

namespace EnergyMarket.Api.Scheduling;

public sealed class ImportDayAheadPricesJob : IJob
{
    private readonly DayAheadPriceImportService _importService;
    private readonly ILogger<ImportDayAheadPricesJob> _logger;

    // Quartz.Extensions.Hosting resolves each job execution within its own DI
    // scope, so constructor-injecting the Scoped DayAheadPriceImportService
    // directly should work. If this throws a "cannot resolve scoped service
    // from root provider" error at runtime, replace this constructor injection
    // with IServiceScopeFactory and create the scope manually inside Execute
    // (see commented fallback below).
    public ImportDayAheadPricesJob(DayAheadPriceImportService importService, ILogger<ImportDayAheadPricesJob> logger)
    {
        _importService = importService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var madridToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, MadridCalendar.Zone).Date);
        var range = new DateRange(madridToday.AddDays(-1), madridToday);

        try
        {
            var results = await _importService.ImportRangeAsync(range, forceReimport: false, ct);
            foreach (var r in results)
            {
                _logger.LogInformation("Import {Date}: {Status} ({Imported}/{Received} rows).",
                    r.Date, r.Status, r.ImportedRows, r.ReceivedRows);
            }
        }
        catch (OperationCanceledException)
        {
            // Job cancelled (e.g. host shutting down) — not an error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled OMIE import job failed.");
        }
    }
}

/*
// FALLBACK if constructor injection of the Scoped service fails at runtime:
public sealed class ImportDayAheadPricesJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImportDayAheadPricesJob> _logger;

    public ImportDayAheadPricesJob(IServiceScopeFactory scopeFactory, ILogger<ImportDayAheadPricesJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var importService = scope.ServiceProvider.GetRequiredService<DayAheadPriceImportService>();
        // ... same body as above, using importService instead of _importService
    }
}
*/
