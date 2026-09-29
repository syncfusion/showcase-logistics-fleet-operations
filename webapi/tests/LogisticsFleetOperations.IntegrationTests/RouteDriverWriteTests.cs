using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using LogisticsFleetOperations.Domain;
using LogisticsFleetOperations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogisticsFleetOperations.IntegrationTests;

/// <summary>Covers PATCH /api/routes/{id}/driver happy path against the real database.</summary>
[Collection("api")]
public sealed class RouteDriverWriteTests(ApiFactory factory)
{
    [Fact]
    public async Task PatchDriver_ReassignsRouteAndPersists()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetWriteDbContext>();

        // Use a dedicated fixture route far outside the seeded one-week schedule window, so this
        // test's driver-conflict check is deterministic across reruns (a seeded route reused directly
        // could legitimately collide with that driver's seeded shifts, which is correct rejection
        // behavior, just not what this happy-path test is exercising).
        var anyDriver = await db.Drivers.FirstAsync();
        var anyVehicle = await db.Vehicles.FirstAsync();
        var anyDepot = await db.Depots.FirstAsync();
        var newDriver = await db.Drivers.FirstAsync(d => d.Id != anyDriver.Id);

        var route = new Route
        {
            Id = Guid.NewGuid(),
            Label = "Integration Test Fixture Route",
            DriverId = anyDriver.Id,
            VehicleId = anyVehicle.Id,
            DepotId = anyDepot.Id,
            PlannedStart = DateTimeOffset.UtcNow.AddDays(90),
            PlannedEnd = DateTimeOffset.UtcNow.AddDays(90).AddHours(3),
            Status = RouteStatus.Planned,
        };
        db.Routes.Add(route);
        await db.SaveChangesAsync();

        try
        {
            using var client = await TestAuth.CreateAuthenticatedClientAsync(factory);
            var response = await client.PatchAsJsonAsync($"/api/routes/{route.Id}/driver", new { driverId = newDriver.Id });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            db.ChangeTracker.Clear();
            var reloaded = await db.Routes.FirstAsync(r => r.Id == route.Id);
            Assert.Equal(newDriver.Id, reloaded.DriverId);
        }
        finally
        {
            db.ChangeTracker.Clear();
            var toRemove = await db.Routes.FirstOrDefaultAsync(r => r.Id == route.Id);
            if (toRemove is not null)
            {
                db.Routes.Remove(toRemove);
                await db.SaveChangesAsync();
            }
        }
    }
}
