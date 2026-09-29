using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Dtos;
using LogisticsFleetOperations.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsFleetOperations.Api.Controllers;

/// <summary>GET is public (read-only DB identity); the reschedule PATCH is authenticated.</summary>
[ApiController]
[Route("api/schedule")]
public sealed class ScheduleController(ScheduleService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        Ok(await service.GetScheduleAsync(from, to, ct));

    /// <summary>Authenticated write. Rejects an overlap with another confirmed entry for the same driver (409).</summary>
    [HttpPatch("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Patch(Guid id, [FromBody] PatchScheduleRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await service.RescheduleAsync(id, request, ct));
        }
        catch (Exception ex) when (ex is NotFoundException or ConflictException or BusinessRuleException)
        {
            return ex.ToActionResult();
        }
    }
}
