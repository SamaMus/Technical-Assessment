using EnergyMarket.Domain.Common;

namespace EnergyMarket.Api.Time;

public interface ICetTimeConverter { DateTimeOffset ToDisplayTime(DateTimeOffset utc); }

public sealed class CetTimeConverter : ICetTimeConverter
{
    public DateTimeOffset ToDisplayTime(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, MadridCalendar.Zone);
}
