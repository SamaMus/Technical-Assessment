namespace EnergyMarket.Domain.Entities;

public sealed class PriceZone
{
    public int Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    private PriceZone() { }

    public PriceZone(int id, string code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
    }

    // MARGINALPDBC has no zone/geo_id field of its own — Spain and Portugal
    // are just the two fixed price columns in every row. These ids are our
    // own convention, not values OMIE assigns.
    public static readonly PriceZone Spain = new(1, "ES", "España");
    public static readonly PriceZone Portugal = new(2, "PT", "Portugal");

    public static IReadOnlyList<PriceZone> All => new[] { Spain, Portugal };
}
