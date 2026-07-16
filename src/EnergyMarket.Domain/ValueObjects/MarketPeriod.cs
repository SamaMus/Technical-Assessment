namespace EnergyMarket.Domain.ValueObjects;

public sealed record MarketPeriod
{
    public DateTimeOffset StartUtc { get; }
    public int DurationMinutes { get; }

    // MARGINALPDBC is an hourly-only file (unlike ESIOS's newer 15-minute MTU
    // series) — this is our current, stated assumption for this data source.
    public MarketPeriod(DateTimeOffset startUtc, int durationMinutes)
    {
        if (startUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Period start must be expressed in UTC.", nameof(startUtc));
        if (durationMinutes != 60)
            throw new ArgumentException("Only 60-minute periods are supported for this data source.", nameof(durationMinutes));

        StartUtc = startUtc;
        DurationMinutes = durationMinutes;
    }

    public DateTimeOffset EndUtc => StartUtc.AddMinutes(DurationMinutes);
}
