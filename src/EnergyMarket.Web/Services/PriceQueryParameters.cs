namespace EnergyMarket.Web.Services;

public sealed record PriceQueryParameters(int PriceZoneId, DateOnly From, DateOnly To);
