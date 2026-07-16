// Local, deliberately duplicated copies of Api's response shapes — Web has no
// Contracts project reference, so it defines its own matching DTOs.
namespace EnergyMarket.Web.Services;

public sealed record PricePointResponse(DateTimeOffset TimestampCet, int DurationMinutes, int PriceZoneId, decimal? Price, bool IsPublished);
public sealed record PriceSeriesResponse(int PriceZoneId, DateOnly From, DateOnly To, IReadOnlyList<PricePointResponse> Prices);
public sealed record ApiErrorResponse(string Title, int Status, IReadOnlyList<string>? Details);

public enum ApiCallStatus { Success, NotFound, ValidationError, ServerError, NetworkError }

public sealed record ApiCallResult<T>
{
    public required ApiCallStatus Status { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }

    public static ApiCallResult<T> Success(T data) => new() { Status = ApiCallStatus.Success, Data = data };
    public static ApiCallResult<T> Failure(ApiCallStatus status, string message) => new() { Status = status, ErrorMessage = message };
}
