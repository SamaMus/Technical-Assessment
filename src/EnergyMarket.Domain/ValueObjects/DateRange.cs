namespace EnergyMarket.Domain.ValueObjects;

public sealed record DateRange
{
    public DateOnly Start { get; }
    public DateOnly End { get; }

    public DateRange(DateOnly start, DateOnly end)
    {
        if (end < start) throw new ArgumentException("End date cannot be before start date.");
        Start = start;
        End = end;
    }

    public IEnumerable<DateOnly> Dates()
    {
        for (var d = Start; d <= End; d = d.AddDays(1)) yield return d;
    }
}
