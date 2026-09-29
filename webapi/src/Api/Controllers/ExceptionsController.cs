using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Dtos;
using LogisticsFleetOperations.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsFleetOperations.Api.Controllers;

/// <summary>GET is public (read-only DB identity); the status-transition PATCH is authenticated.</summary>
[ApiController]
[Route("api/exceptions")]
public sealed class ExceptionsController(ExceptionService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] string? severity, CancellationToken ct)
    {
        try
        {
            return Ok(await service.GetExceptionsAsync(status, severity, ct));
        }
        catch (BusinessRuleException ex)
        {
            return ex.ToActionResult();
        }
    }

    /// <summary>Authenticated write. Rejects an Open-to-Resolved skip (400) and a transition off an already-Resolved exception (400).</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize]
    public async Task<IActionResult> PatchStatus(Guid id, [FromBody] PatchExceptionStatusRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await service.UpdateStatusAsync(id, request.Status, ct));
        }
        catch (Exception ex) when (ex is NotFoundException or BusinessRuleException)
        {
            return ex.ToActionResult();
        }
    }
}
