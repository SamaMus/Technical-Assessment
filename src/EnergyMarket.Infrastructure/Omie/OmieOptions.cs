using System.ComponentModel.DataAnnotations;

namespace EnergyMarket.Infrastructure.Omie;

public sealed class OmieOptions
{
    public const string SectionName = "Omie";

    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; init; } = "https://www.omie.es/en/file-download";

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 30;
}
