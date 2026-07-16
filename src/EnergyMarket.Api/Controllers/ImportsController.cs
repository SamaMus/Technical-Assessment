using EnergyMarket.Domain.Services;
using EnergyMarket.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMarket.Api.Controllers;

[ApiController]
[Route("api/imports")]
public sealed class ImportsController : ControllerBase
{
    private readonly DayAheadPriceImportService _importService;

    public ImportsController(DayAheadPriceImportService importService) => _importService = importService;

    [HttpPost]
    public async Task<IActionResult> Import(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] bool force, CancellationToken ct)
    {
        if (to < from) return BadRequest("'to' cannot precede 'from'.");
        var results = await _importService.ImportRangeAsync(new DateRange(from, to), force, ct);
        return Ok(results);
    }
}
