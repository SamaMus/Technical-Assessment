using EnergyMarket.Domain.Models;

namespace EnergyMarket.Domain.Validation;

public sealed record ValidationOutcome(
    IReadOnlyList<DayAheadPriceCandidate> Valid,
    IReadOnlyList<DayAheadPriceCandidate> Rejected,
    IReadOnlyList<string> Issues);

public interface IDayAheadPriceValidator
{
    ValidationOutcome Validate(IReadOnlyList<DayAheadPriceCandidate> candidates);
}

public sealed class DayAheadPriceValidator : IDayAheadPriceValidator
{
    private const decimal MinPrice = -500m;
    private const decimal MaxPrice = 4000m;

    public ValidationOutcome Validate(IReadOnlyList<DayAheadPriceCandidate> candidates)
    {
        var valid = new List<DayAheadPriceCandidate>();
        var rejected = new List<DayAheadPriceCandidate>();
        var issues = new List<string>();

        foreach (var c in candidates)
        {
            if (c.PeriodStartUtc == default)
            {
                rejected.Add(c);
                issues.Add("Missing or invalid timestamp.");
                continue;
            }

            if (c.PeriodDurationMinutes != 60)
            {
                rejected.Add(c);
                issues.Add($"Unsupported period duration: {c.PeriodDurationMinutes} minutes.");
                continue;
            }

            if (c.Price is < MinPrice or > MaxPrice)
            {
                rejected.Add(c);
                issues.Add($"Price {c.Price} is outside the accepted range [{MinPrice}, {MaxPrice}].");
                continue;
            }

            // Negative prices are accepted (real market outcome).
            // Null prices are accepted (not yet published) — no rejection, no issue logged.
            valid.Add(c);
        }

        return new ValidationOutcome(valid, rejected, issues);
    }
}
