using EnergyMarket.Domain.ValueObjects;

namespace EnergyMarket.Domain.Entities;

public sealed class DayAheadPrice
{
    public long Id { get; private set; }
    public int PriceZoneId { get; private set; }
    public decimal? Price { get; private set; }
    public DateTimeOffset SourceRetrievedAtUtc { get; private set; }

    private DateTimeOffset _periodStartUtc;
    private int _periodDurationMinutes;
    public MarketPeriod Period => new(_periodStartUtc, _periodDurationMinutes);
    public bool IsPublished => Price.HasValue;

    private DayAheadPrice() { } // EF Core materialization

    public DayAheadPrice(int priceZoneId, MarketPeriod period, decimal? price, DateTimeOffset sourceRetrievedAtUtc)
    {
        if (price is < -500m or > 4000m)
            throw new ArgumentOutOfRangeException(nameof(price), "Price is outside the harmonised market price limits.");

        PriceZoneId = priceZoneId;
        _periodStartUtc = period.StartUtc;
        _periodDurationMinutes = period.DurationMinutes;
        Price = price;
        SourceRetrievedAtUtc = sourceRetrievedAtUtc;
    }
}
