using EnergyMarket.Domain.Common;
using EnergyMarket.Domain.Entities;
using EnergyMarket.Domain.Repositories;
using EnergyMarket.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace EnergyMarket.Infrastructure.Persistence;

public sealed class DayAheadPriceRepository : IDayAheadPriceRepository
{
    private readonly EnergyMarketDbContext _context;

    public DayAheadPriceRepository(EnergyMarketDbContext context) => _context = context;

    public async Task UpsertRangeAsync(IEnumerable<DayAheadPrice> prices, CancellationToken ct = default)
    {
        var incoming = prices.ToList();
        if (incoming.Count == 0) return;

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var zoneIds = incoming.Select(p => p.PriceZoneId).Distinct().ToList();
            var starts = incoming.Select(p => p.Period.StartUtc).ToList();

            var conflicting = await _context.DayAheadPrices
                .Where(e => zoneIds.Contains(e.PriceZoneId)
                         && starts.Contains(EF.Property<DateTimeOffset>(e, "_periodStartUtc")))
                .ToListAsync(ct);

            if (conflicting.Count > 0)
            {
                _context.DayAheadPrices.RemoveRange(conflicting);
                await _context.SaveChangesAsync(ct);
            }

            await _context.DayAheadPrices.AddRangeAsync(incoming, ct);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<DayAheadPrice>> GetByRangeAsync(int priceZoneId, DateRange range, CancellationToken ct = default)
    {
        var start = MadridCalendar.GetUtcMidnight(range.Start);
        var end = MadridCalendar.GetUtcMidnightNextDay(range.End);

        return await _context.DayAheadPrices
            .Where(e => e.PriceZoneId == priceZoneId
                     && EF.Property<DateTimeOffset>(e, "_periodStartUtc") >= start
                     && EF.Property<DateTimeOffset>(e, "_periodStartUtc") < end)
            .OrderBy(e => EF.Property<DateTimeOffset>(e, "_periodStartUtc"))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsForDateAsync(int priceZoneId, DateOnly date, CancellationToken ct = default)
    {
        var start = MadridCalendar.GetUtcMidnight(date);
        var end = MadridCalendar.GetUtcMidnightNextDay(date);

        return await _context.DayAheadPrices.AnyAsync(e =>
            e.PriceZoneId == priceZoneId
            && EF.Property<DateTimeOffset>(e, "_periodStartUtc") >= start
            && EF.Property<DateTimeOffset>(e, "_periodStartUtc") < end, ct);
    }
}
