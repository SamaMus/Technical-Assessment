namespace EnergyMarket.Api.Contracts;

public sealed record PriceSeriesResponse(
    int PriceZoneId, DateOnly From, DateOnly To, IReadOnlyList<PricePointResponse> Prices);
