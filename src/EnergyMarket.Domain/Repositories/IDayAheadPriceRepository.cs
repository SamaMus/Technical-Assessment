using EnergyMarket.Domain.Entities;
using EnergyMarket.Domain.ValueObjects;

namespace EnergyMarket.Domain.Repositories;

public interface IDayAheadPriceRepository
{
    Task UpsertRangeAsync(IEnumerable<DayAheadPrice> prices, CancellationToken ct = default);
    Task<IReadOnlyList<DayAheadPrice>> GetByRangeAsync(int priceZoneId, DateRange range, CancellationToken ct = default);
    Task<bool> ExistsForDateAsync(int priceZoneId, DateOnly date, CancellationToken ct = default);
}
