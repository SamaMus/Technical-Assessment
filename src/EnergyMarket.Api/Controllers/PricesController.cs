using EnergyMarket.Api.Contracts;
using EnergyMarket.Api.Formatters;
using EnergyMarket.Api.Time;
using EnergyMarket.Domain.Repositories;
using EnergyMarket.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMarket.Api.Controllers;

[ApiController]
[Route("api/prices")]
public sealed class PricesController : ControllerBase
{
    private readonly IDayAheadPriceRepository _repository;
    private readonly ICetTimeConverter _timeConverter;
    private readonly ICsvPriceFormatter _csvFormatter;
    private readonly ILogger<PricesController> _logger;

    public PricesController(IDayAheadPriceRepository repository, ICetTimeConverter timeConverter,
        ICsvPriceFormatter csvFormatter, ILogger<PricesController> logger)
    {
        _repository = repository;
        _timeConverter = timeConverter;
        _csvFormatter = csvFormatter;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PriceSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get(
        [FromQuery] int priceZoneId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        if (priceZoneId <= 0 || to < from)
        {
            return BadRequest(new ApiErrorResponse("Invalid request parameters.", 400,
                new List<string> { "priceZoneId must be positive and 'to' cannot precede 'from'." }));
        }

        try
        {
            var range = new DateRange(from, to);
            var entities = await _repository.GetByRangeAsync(priceZoneId, range, ct);

            if (entities.Count == 0)
                return NotFound(new ApiErrorResponse("No price data found for the requested range.", 404));

            var points = entities
                .Select(e => new PricePointResponse(_timeConverter.ToDisplayTime(e.Period.StartUtc),
                    e.Period.DurationMinutes, e.PriceZoneId, e.Price, e.IsPublished))
                .OrderBy(p => p.TimestampCet)
                .ToList();

            var series = new PriceSeriesResponse(priceZoneId, from, to, points);

            if (Request.Headers.Accept.Any(a => a is not null && a.Contains("text/plain", StringComparison.OrdinalIgnoreCase)))
                return Content(_csvFormatter.Format(series), "text/plain");

            return Ok(series);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving prices.");
            return StatusCode(500, new ApiErrorResponse("An unexpected error occurred.", 500));
        }
    }
}
