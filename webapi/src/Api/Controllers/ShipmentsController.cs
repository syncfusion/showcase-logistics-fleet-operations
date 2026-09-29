using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsFleetOperations.Api.Controllers;

/// <summary>Public, GET-only, paginated. Backed by the read-only (lfo_readonly) database identity.</summary>
[ApiController]
[Route("api/shipments")]
public sealed class ShipmentsController(ShipmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? status,
        [FromQuery] Guid? routeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.GetShipmentsAsync(status, routeId, page, pageSize, ct));
        }
        catch (BusinessRuleException ex)
        {
            return ex.ToActionResult();
        }
    }
}
