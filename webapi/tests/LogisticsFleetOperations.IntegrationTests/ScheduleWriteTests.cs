using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using LogisticsFleetOperations.Domain;
using LogisticsFleetOperations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogisticsFleetOperations.IntegrationTests;

/// <summary>
/// Covers PATCH /api/schedule/{id}: the happy path, and the real overlap rejection (409) — reached by
/// actually issuing the conflicting request against the real database, not asserted by omission.
/// </summary>
[Collection("api")]
public sealed class ScheduleWriteTests(ApiFactory factory)
{
    [Fact]
    public async Task PatchSchedule_NonOverlappingReschedule_Succeeds()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetWriteDbContext>();

        var driver = await db.Drivers.FirstAsync();
        var entry = new ScheduleEntry
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            ShiftStart = DateTimeOffset.UtcNow.AddDays(30),
            ShiftEnd = DateTimeOffset.UtcNow.AddDays(30).AddHours(4),
            Status = ScheduleEntryStatus.Confirmed,
        };
        db.ScheduleEntries.Add(entry);
        await db.SaveChangesAsync();

        try
        {
            using var client = await TestAuth.CreateAuthenticatedClientAsync(factory);
            var response = await client.PatchAsJsonAsync($"/api/schedule/{entry.Id}", new
            {
                shiftStart = entry.ShiftStart.AddHours(1),
                shiftEnd = entry.ShiftEnd.AddHours(1),
                status = "Confirmed",
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            db.ChangeTracker.Clear();
            var reloaded = await db.ScheduleEntries.FirstAsync(e => e.Id == entry.Id);
            Assert.Equal(entry.ShiftStart.AddHours(1), reloaded.ShiftStart);
        }
        finally
        {
            db.ChangeTracker.Clear();
            var toRemove = await db.ScheduleEntries.FirstOrDefaultAsync(e => e.Id == entry.Id);
            if (toRemove is not null)
            {
                db.ScheduleEntries.Remove(toRemove);
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task PatchSchedule_OverlappingConfirmedShift_Returns409()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetWriteDbContext>();

        var driver = await db.Drivers.FirstAsync();
        var existingStart = DateTimeOffset.UtcNow.AddDays(31);
        var existingEnd = existingStart.AddHours(8);

        var fixedEntry = new ScheduleEntry
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            ShiftStart = existingStart,
            ShiftEnd = existingEnd,
            Status = ScheduleEntryStatus.Confirmed,
        };
        var movableEntry = new ScheduleEntry
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            ShiftStart = existingStart.AddDays(1),
            ShiftEnd = existingEnd.AddDays(1),
            Status = ScheduleEntryStatus.Confirmed,
        };
        db.ScheduleEntries.AddRange(fixedEntry, movableEntry);
        await db.SaveChangesAsync();

        try
        {
            using var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

            // Attempt to move the movable entry directly on top of the fixed entry's window — a real overlap.
            var response = await client.PatchAsJsonAsync($"/api/schedule/{movableEntry.Id}", new
            {
                shiftStart = existingStart.AddHours(1),
                shiftEnd = existingEnd.AddHours(-1),
                status = "Confirmed",
            });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            db.ChangeTracker.Clear();
            var toRemove = await db.ScheduleEntries
                .Where(e => e.Id == fixedEntry.Id || e.Id == movableEntry.Id)
                .ToListAsync();
            if (toRemove.Count > 0)
            {
                db.ScheduleEntries.RemoveRange(toRemove);
                await db.SaveChangesAsync();
            }
        }
    }
}
