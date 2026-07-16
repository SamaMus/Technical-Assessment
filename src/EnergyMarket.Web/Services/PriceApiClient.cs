using System.Net;
using System.Net.Http.Json;

namespace EnergyMarket.Web.Services;

public interface IPriceApiClient
{
    Task<ApiCallResult<PriceSeriesResponse>> GetPricesAsync(PriceQueryParameters parameters, CancellationToken ct = default);
}

public sealed class PriceApiClient : IPriceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PriceApiClient> _logger;

    public PriceApiClient(HttpClient httpClient, ILogger<PriceApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiCallResult<PriceSeriesResponse>> GetPricesAsync(PriceQueryParameters parameters, CancellationToken ct = default)
    {
        var uri = $"api/prices?priceZoneId={parameters.PriceZoneId}&from={parameters.From:yyyy-MM-dd}&to={parameters.To:yyyy-MM-dd}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(uri, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Network error calling EnergyMarket API.");
            return ApiCallResult<PriceSeriesResponse>.Failure(ApiCallStatus.NetworkError, "Could not reach the pricing service.");
        }

        return response.StatusCode switch
        {
            HttpStatusCode.OK => ApiCallResult<PriceSeriesResponse>.Success(
                (await response.Content.ReadFromJsonAsync<PriceSeriesResponse>(cancellationToken: ct))!),
            HttpStatusCode.NotFound => ApiCallResult<PriceSeriesResponse>.Failure(
                ApiCallStatus.NotFound, "No price data found for the selected criteria."),
            HttpStatusCode.BadRequest => ApiCallResult<PriceSeriesResponse>.Failure(
                ApiCallStatus.ValidationError, "The request parameters were invalid."),
            _ => ApiCallResult<PriceSeriesResponse>.Failure(
                ApiCallStatus.ServerError, "The pricing service encountered an unexpected error.")
        };
    }
}
