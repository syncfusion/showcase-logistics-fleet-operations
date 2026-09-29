using LogisticsFleetOperations.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsFleetOperations.Api.Controllers;

/// <summary>Public, GET-only reference/lookup data. Backed by the read-only (lfo_readonly) database identity.</summary>
[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController(ReferenceDataService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await service.GetVehiclesAsync(ct));
}

[ApiController]
[Route("api/drivers")]
public sealed class DriversController(ReferenceDataService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await service.GetDriversAsync(ct));
}

[ApiController]
[Route("api/depots")]
public sealed class DepotsController(ReferenceDataService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await service.GetDepotsAsync(ct));
}

[ApiController]
[Route("api/meta")]
public sealed class MetaController(ReferenceDataService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await service.GetMetaAsync(ct));
}
