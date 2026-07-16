namespace EnergyMarket.Domain.Common;

/// <summary>
/// Single source of truth for converting between a Madrid/MIBEL local calendar
/// date and UTC instants. Used by both the OMIE file client (to map hour
/// indices to UTC) and the repository (to compute day boundaries for queries),
/// so DST handling logic exists in exactly one place.
/// </summary>
public static class MadridCalendar
{
    public static readonly TimeZoneInfo Zone = Resolve();

    public static DateTimeOffset GetUtcMidnight(DateOnly date)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
    }

    public static DateTimeOffset GetUtcMidnightNextDay(DateOnly date) => GetUtcMidnight(date.AddDays(1));

    private static TimeZoneInfo Resolve()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"); } // Windows
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid"); // Linux/macOS
        }
    }
}
