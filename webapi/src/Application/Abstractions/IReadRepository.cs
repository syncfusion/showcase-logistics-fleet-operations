using LogisticsFleetOperations.Domain;

namespace LogisticsFleetOperations.Application.Abstractions;

/// <summary>
/// Read-only data access port. The Infrastructure implementation backing this port must use the
/// read-only (lfo_readonly) database identity — see PostgresReadRepository.
/// </summary>
public interface IReadRepository
{
    Task<List<Vehicle>> GetVehiclesAsync(CancellationToken ct);
    Task<List<Driver>> GetDriversAsync(CancellationToken ct);
    Task<List<Depot>> GetDepotsAsync(CancellationToken ct);
    Task<List<Route>> GetRoutesAsync(RouteStatus? status, CancellationToken ct);
    Task<(List<Shipment> Items, int Total)> GetShipmentsPagedAsync(ShipmentStatus? status, Guid? routeId, int page, int pageSize, CancellationToken ct);
    Task<List<Shipment>> GetAllShipmentsAsync(CancellationToken ct);
    Task<List<ShipmentException>> GetExceptionsAsync(ExceptionStatus? status, ExceptionSeverity? severity, CancellationToken ct);
    Task<List<ShipmentException>> GetAllExceptionsAsync(CancellationToken ct);
    Task<List<ScheduleEntry>> GetScheduleAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);
    Task<DatasetMeta?> GetMetaAsync(CancellationToken ct);
}

public sealed class DatasetMeta
{
    public string DatasetVersion { get; set; } = "";
    public DateTimeOffset ScenarioClockReference { get; set; }
    public DateTimeOffset SeededAt { get; set; }
}
