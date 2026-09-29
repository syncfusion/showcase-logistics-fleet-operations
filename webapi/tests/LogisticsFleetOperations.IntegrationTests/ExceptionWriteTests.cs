using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using LogisticsFleetOperations.Domain;
using LogisticsFleetOperations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogisticsFleetOperations.IntegrationTests;

/// <summary>
/// Covers PATCH /api/exceptions/{id}/status: the happy path (Open -&gt; Investigating) and the real
/// Open-&gt;Resolved skip rejection (400) — reached live, not asserted by omission.
/// </summary>
[Collection("api")]
public sealed class ExceptionWriteTests(ApiFactory factory)
{
    [Fact]
    public async Task PatchStatus_OpenToInvestigating_Succeeds()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetWriteDbContext>();
        var shipment = await db.Shipments.FirstAsync();

        var exception = new ShipmentException
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            Kind = ExceptionKind.SlaRisk,
            Severity = ExceptionSeverity.Medium,
            OpenedAt = DateTimeOffset.UtcNow,
            Status = ExceptionStatus.Open,
            Note = "integration test fixture",
        };
        db.Exceptions.Add(exception);
        await db.SaveChangesAsync();

        try
        {
            using var client = await TestAuth.CreateAuthenticatedClientAsync(factory);
            var response = await client.PatchAsJsonAsync($"/api/exceptions/{exception.Id}/status", new { status = "Investigating" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            db.ChangeTracker.Clear();
            var reloaded = await db.Exceptions.FirstAsync(e => e.Id == exception.Id);
            Assert.Equal(ExceptionStatus.Investigating, reloaded.Status);
        }
        finally
        {
            db.ChangeTracker.Clear();
            var toRemove = await db.Exceptions.FirstOrDefaultAsync(e => e.Id == exception.Id);
            if (toRemove is not null)
            {
                db.Exceptions.Remove(toRemove);
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task PatchStatus_OpenDirectlyToResolved_Returns400()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetWriteDbContext>();
        var shipment = await db.Shipments.FirstAsync();

        var exception = new ShipmentException
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            Kind = ExceptionKind.FailedAttempt,
            Severity = ExceptionSeverity.High,
            OpenedAt = DateTimeOffset.UtcNow,
            Status = ExceptionStatus.Open,
            Note = "integration test fixture",
        };
        db.Exceptions.Add(exception);
        await db.SaveChangesAsync();

        try
        {
            using var client = await TestAuth.CreateAuthenticatedClientAsync(factory);
            var response = await client.PatchAsJsonAsync($"/api/exceptions/{exception.Id}/status", new { status = "Resolved" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            db.ChangeTracker.Clear();
            var toRemove = await db.Exceptions.FirstOrDefaultAsync(e => e.Id == exception.Id);
            if (toRemove is not null)
            {
                db.Exceptions.Remove(toRemove);
                await db.SaveChangesAsync();
            }
        }
    }
}
