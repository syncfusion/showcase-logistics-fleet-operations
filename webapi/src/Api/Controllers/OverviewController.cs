using LogisticsFleetOperations.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsFleetOperations.Api.Controllers;

/// <summary>Public, GET-only. Backed by the read-only (lfo_readonly) database identity via FleetOverviewService.</summary>
[ApiController]
[Route("api/overview")]
public sealed class OverviewController(FleetOverviewService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await service.GetOverviewAsync(ct));
}
