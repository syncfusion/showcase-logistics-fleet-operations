using System.Net;
using System.Net.Http.Json;
using LogisticsFleetOperations.Domain;
using LogisticsFleetOperations.Infrastructure.Persistence;
using LogisticsFleetOperations.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsFleetOperations.IntegrationTests;

/// <summary>
/// Covers POST /api/admin/reset-data: rejects unauthenticated callers (401, same as every other write
/// endpoint), and — reached live against the real database — actually undoes a persisted write that the
/// UI itself cannot revert (the scenario the operator reported: an exception moved to Resolved).
/// </summary>
[Collection("api")]
public sealed class AdminResetTests(ApiFactory factory)
{
    [Fact]
    public async Task ResetData_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/admin/reset-data", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResetData_Authenticated_UndoesPriorWriteAndRestoresBaselineCounts()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetWriteDbContext>();

        // Simulate the reported scenario: a write made through the app (an exception resolved) that
        // the UI has no way to revert. A resolved-but-not-in-the-fixed-seed exception proves the reset
        // actually replaces the dataset rather than merely leaving it untouched.
        var shipment = await db.Shipments.FirstAsync();
        var stray = new ShipmentException
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            Kind = ExceptionKind.FailedAttempt,
            Severity = ExceptionSeverity.Low,
            OpenedAt = DateTimeOffset.UtcNow,
            Status = ExceptionStatus.Resolved,
            Note = "integration test: simulated in-demo write that cannot be undone via the UI",
        };
        db.Exceptions.Add(stray);
        await db.SaveChangesAsync();

        // If anything below throws (an assertion, a flaky test host) before the reset call lands,
        // the stray row must not linger in the shared local dev database -- it did once, from
        // exactly this scenario, and showed up as extra rows in the real Exceptions page.
        try
        {
            using var client = await TestAuth.CreateAuthenticatedClientAsync(factory);
            var response = await client.PostAsync("/api/admin/reset-data", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var summary = await response.Content.ReadFromJsonAsync<SeedSummary>();
            Assert.NotNull(summary);
            Assert.Equal(2, summary!.Depots);
            Assert.Equal(12, summary.Vehicles);
            Assert.Equal(10, summary.Drivers);
            Assert.Equal(15, summary.Routes);
            Assert.Equal(40, summary.Shipments);
            Assert.Equal(8, summary.Exceptions);

            db.ChangeTracker.Clear();
            Assert.False(await db.Exceptions.AnyAsync(e => e.Id == stray.Id));
            Assert.Equal(8, await db.Exceptions.CountAsync());
            Assert.Equal(40, await db.Shipments.CountAsync());
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (await db.Exceptions.AnyAsync(e => e.Id == stray.Id))
            {
                await db.Exceptions.Where(e => e.Id == stray.Id).ExecuteDeleteAsync();
            }
        }
    }
}
