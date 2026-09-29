using LogisticsFleetOperations.Infrastructure.Persistence;
using LogisticsFleetOperations.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsFleetOperations.Api.Controllers;

/// <summary>
/// Showcase-only demo administration. Writes made while signed in (exception status, schedule edits,
/// route driver/vehicle reassignment) persist to PostgreSQL and cannot be undone through the UI itself —
/// this restores the fixed demo baseline on request, gated behind the same JWT auth as every other write
/// endpoint (never exposed to the public/signed-out session).
/// </summary>
[ApiController]
[Route("api/admin")]
public sealed class AdminController(FleetWriteDbContext db) : ControllerBase
{
    [HttpPost("reset-data")]
    [Authorize]
    public async Task<IActionResult> ResetData(CancellationToken ct)
    {
        var summary = await DemoDataSeeder.ResetAsync(db, ct);
        return Ok(summary);
    }
}
