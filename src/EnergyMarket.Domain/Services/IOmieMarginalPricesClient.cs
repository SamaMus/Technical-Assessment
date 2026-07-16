using EnergyMarket.Domain.Models;

namespace EnergyMarket.Domain.Services;

public interface IOmieMarginalPricesClient
{
    Task<IReadOnlyList<DayAheadPriceCandidate>> GetPricesForDateAsync(DateOnly date, CancellationToken ct = default);
}
