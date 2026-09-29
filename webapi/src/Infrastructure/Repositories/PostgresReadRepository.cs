using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Domain;
using LogisticsFleetOperations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogisticsFleetOperations.Infrastructure.Repositories;

/// <summary>Backed by FleetReadDbContext, which connects using the lfo_readonly database identity.</summary>
public sealed class PostgresReadRepository(FleetReadDbContext db) : IReadRepository
{
    public async Task<List<Vehicle>> GetVehiclesAsync(CancellationToken ct) =>
        await db.Vehicles.AsNoTracking().OrderBy(v => v.Label).ToListAsync(ct);

    public async Task<List<Driver>> GetDriversAsync(CancellationToken ct) =>
        await db.Drivers.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);

    public async Task<List<Depot>> GetDepotsAsync(CancellationToken ct) =>
        await db.Depots.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);

    public async Task<List<Route>> GetRoutesAsync(RouteStatus? status, CancellationToken ct)
    {
        var query = db.Routes.AsNoTracking().Include(r => r.Stops).AsQueryable();
        if (status is { } s)
        {
            query = query.Where(r => r.Status == s);
        }
        return await query.OrderBy(r => r.Label).ToListAsync(ct);
    }

    public async Task<(List<Shipment> Items, int Total)> GetShipmentsPagedAsync(ShipmentStatus? status, Guid? routeId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Shipments.AsNoTracking().AsQueryable();
        if (status is { } s)
        {
            query = query.Where(x => x.Status == s);
        }
        if (routeId is { } r)
        {
            query = query.Where(x => x.RouteId == r);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.SlaWindowEnd)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<List<Shipment>> GetAllShipmentsAsync(CancellationToken ct) =>
        await db.Shipments.AsNoTracking().ToListAsync(ct);

    public async Task<List<ShipmentException>> GetExceptionsAsync(ExceptionStatus? status, ExceptionSeverity? severity, CancellationToken ct)
    {
        var query = db.Exceptions.AsNoTracking().AsQueryable();
        if (status is { } s)
        {
            query = query.Where(x => x.Status == s);
        }
        if (severity is { } sev)
        {
            query = query.Where(x => x.Severity == sev);
        }
        return await query.OrderByDescending(x => x.OpenedAt).ToListAsync(ct);
    }

    public async Task<List<ShipmentException>> GetAllExceptionsAsync(CancellationToken ct) =>
        await db.Exceptions.AsNoTracking().ToListAsync(ct);

    public async Task<List<ScheduleEntry>> GetScheduleAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var query = db.ScheduleEntries.AsNoTracking().AsQueryable();
        if (from is { } f)
        {
            query = query.Where(x => x.ShiftEnd >= f);
        }
        if (to is { } t)
        {
            query = query.Where(x => x.ShiftStart <= t);
        }
        return await query.OrderBy(x => x.ShiftStart).ToListAsync(ct);
    }

    public async Task<DatasetMeta?> GetMetaAsync(CancellationToken ct)
    {
        var row = await db.DatasetMeta.AsNoTracking().OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct);
        return row is null
            ? null
            : new DatasetMeta { DatasetVersion = row.DatasetVersion, ScenarioClockReference = row.ScenarioClockReference, SeededAt = row.SeededAt };
    }
}
