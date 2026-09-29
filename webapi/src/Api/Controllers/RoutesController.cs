using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Dtos;
using LogisticsFleetOperations.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsFleetOperations.Api.Controllers;

/// <summary>
/// GET is public (read-only DB identity, no write action exists on this controller for the
/// unauthenticated surface); the driver-reassignment PATCH is a separate authenticated endpoint below.
/// </summary>
[ApiController]
[Route("api/routes")]
public sealed class RoutesController(RouteService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? status, CancellationToken ct)
    {
        try
        {
            return Ok(await service.GetRoutesAsync(status, ct));
        }
        catch (Exception ex) when (ex is BusinessRuleException)
        {
            return ex.ToActionResult();
        }
    }

    /// <summary>Authenticated write: reassigns a route's driver/vehicle. Rejects a driver schedule conflict with 409.</summary>
    [HttpPatch("{id:guid}/driver")]
    [Authorize]
    public async Task<IActionResult> PatchDriver(Guid id, [FromBody] PatchRouteDriverRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await service.ReassignDriverAsync(id, request.DriverId, request.VehicleId, ct));
        }
        catch (Exception ex) when (ex is NotFoundException or ConflictException or BusinessRuleException)
        {
            return ex.ToActionResult();
        }
    }
}
