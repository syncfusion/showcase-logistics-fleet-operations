using LogisticsFleetOperations.Domain;

namespace LogisticsFleetOperations.Application.Abstractions;

/// <summary>
/// Read/write data access port for authenticated command endpoints. The Infrastructure implementation
/// backing this port must use the writable (lfo_app) database identity — see PostgresWriteRepository.
/// </summary>
public interface IWriteRepository
{
    Task<ScheduleEntry?> GetScheduleEntryAsync(Guid id, CancellationToken ct);
    Task<List<ScheduleEntry>> GetDriverScheduleEntriesAsync(Guid driverId, CancellationToken ct);
    Task SaveScheduleEntryAsync(ScheduleEntry entry, CancellationToken ct);

    Task<ShipmentException?> GetExceptionAsync(Guid id, CancellationToken ct);
    Task SaveExceptionAsync(ShipmentException exception, CancellationToken ct);

    Task<Route?> GetRouteAsync(Guid id, CancellationToken ct);
    Task<Driver?> GetDriverAsync(Guid id, CancellationToken ct);
    Task<Vehicle?> GetVehicleAsync(Guid id, CancellationToken ct);
    Task SaveRouteAsync(Route route, CancellationToken ct);
}
