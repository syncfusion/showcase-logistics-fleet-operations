using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Domain;
using LogisticsFleetOperations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogisticsFleetOperations.Infrastructure.Repositories;

/// <summary>Backed by FleetWriteDbContext, which connects using the writable lfo_app database identity.</summary>
public sealed class PostgresWriteRepository(FleetWriteDbContext db) : IWriteRepository
{
    public async Task<ScheduleEntry?> GetScheduleEntryAsync(Guid id, CancellationToken ct) =>
        await db.ScheduleEntries.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<List<ScheduleEntry>> GetDriverScheduleEntriesAsync(Guid driverId, CancellationToken ct) =>
        await db.ScheduleEntries.AsNoTracking().Where(e => e.DriverId == driverId).ToListAsync(ct);

    public async Task SaveScheduleEntryAsync(ScheduleEntry entry, CancellationToken ct)
    {
        if (db.Entry(entry).State == EntityState.Detached)
        {
            db.ScheduleEntries.Update(entry);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<ShipmentException?> GetExceptionAsync(Guid id, CancellationToken ct) =>
        await db.Exceptions.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task SaveExceptionAsync(ShipmentException exception, CancellationToken ct)
    {
        if (db.Entry(exception).State == EntityState.Detached)
        {
            db.Exceptions.Update(exception);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<Route?> GetRouteAsync(Guid id, CancellationToken ct) =>
        await db.Routes.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Driver?> GetDriverAsync(Guid id, CancellationToken ct) =>
        await db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Vehicle?> GetVehicleAsync(Guid id, CancellationToken ct) =>
        await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task SaveRouteAsync(Route route, CancellationToken ct)
    {
        if (db.Entry(route).State == EntityState.Detached)
        {
            db.Routes.Update(route);
        }
        await db.SaveChangesAsync(ct);
    }
}
