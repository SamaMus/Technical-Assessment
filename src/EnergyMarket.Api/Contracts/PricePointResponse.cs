namespace EnergyMarket.Api.Contracts;

public sealed record PricePointResponse(
    DateTimeOffset TimestampCet, int DurationMinutes, int PriceZoneId, decimal? Price, bool IsPublished);
