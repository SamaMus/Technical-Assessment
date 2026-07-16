namespace EnergyMarket.Api.Contracts;

public sealed record ApiErrorResponse(string Title, int Status, IReadOnlyList<string>? Details = null);
