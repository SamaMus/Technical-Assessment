using System.Globalization;
using System.Text;
using EnergyMarket.Api.Contracts;

namespace EnergyMarket.Api.Formatters;

public interface ICsvPriceFormatter { string Format(PriceSeriesResponse series); }

public sealed class CsvPriceFormatter : ICsvPriceFormatter
{
    public string Format(PriceSeriesResponse series)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", "TimestampCet", "DurationMinutes", "PriceZoneId", "Price", "IsPublished"));

        foreach (var p in series.Prices)
        {
            var priceText = p.Price?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            sb.AppendLine(string.Join(";",
                p.TimestampCet.ToString("O", CultureInfo.InvariantCulture),
                p.DurationMinutes.ToString(CultureInfo.InvariantCulture),
                p.PriceZoneId.ToString(CultureInfo.InvariantCulture),
                priceText,
                p.IsPublished.ToString(CultureInfo.InvariantCulture)));
        }
        return sb.ToString();
    }
}
