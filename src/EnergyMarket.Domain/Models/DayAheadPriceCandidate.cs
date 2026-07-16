namespace EnergyMarket.Domain.Models;

public sealed record DayAheadPriceCandidate(
    int PriceZoneId,
    DateTimeOffset PeriodStartUtc,
    int PeriodDurationMinutes,
    decimal? Price,
    DateTimeOffset SourceRetrievedAtUtc);
