namespace EnergyMarket.Domain.Services;

public enum ImportStatus { Imported, Skipped, PartiallyImported, Failed }

public sealed record ImportResult(
    DateOnly Date,
    ImportStatus Status,
    int ReceivedRows,
    int ImportedRows,
    int RejectedRows,
    IReadOnlyList<string> Errors);
