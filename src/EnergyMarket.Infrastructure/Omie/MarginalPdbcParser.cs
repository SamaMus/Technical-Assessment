using System.Globalization;
using Microsoft.Extensions.Logging;

namespace EnergyMarket.Infrastructure.Omie;

internal sealed record MarginalPdbcRow(int Hour, decimal? PriceEs, decimal? PricePt);

/// <summary>
/// Parses the MARGINALPDBC public file format. Confirmed: prices use a period
/// (.) as the decimal separator, not a comma — parsed with InvariantCulture
/// and explicit NumberStyles, never a Spanish/comma-decimal culture.
/// </summary>
internal static class MarginalPdbcParser
{
    private const NumberStyles PriceNumberStyle = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

    public static IReadOnlyList<MarginalPdbcRow> Parse(string rawText, ILogger logger)
    {
        var rows = new List<MarginalPdbcRow>();
        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Line 0 is the OMIE metadata/header line (e.g. "MARGINALPDBC;5;...") — skip it.
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line) || line.TrimEnd(';').Length == 0) continue; // trailing blank/terminator line

            var parts = line.Split(';');
            if (parts.Length < 6)
            {
                logger.LogDebug("Skipping unparseable MARGINALPDBC line (too few fields): {Line}", line);
                continue;
            }

            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
            {
                logger.LogDebug("Skipping unparseable MARGINALPDBC line (bad hour field): {Line}", line);
                continue;
            }

            var priceEs = ParseNullableDecimal(parts[4], logger, line);
            var pricePt = ParseNullableDecimal(parts[5], logger, line);

            rows.Add(new MarginalPdbcRow(hour, priceEs, pricePt));
        }

        return rows;
    }

    private static decimal? ParseNullableDecimal(string raw, ILogger logger, string sourceLine)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null; // legitimate "not yet published" state — not an error

        if (decimal.TryParse(trimmed, PriceNumberStyle, CultureInfo.InvariantCulture, out var value))
            return value;

        logger.LogDebug("Skipping unparseable price field '{Raw}' in line: {Line}", trimmed, sourceLine);
        return null;
    }
}
