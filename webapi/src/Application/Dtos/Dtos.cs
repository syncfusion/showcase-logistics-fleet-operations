namespace LogisticsFleetOperations.Application.Dtos;

public sealed record VehicleDto(Guid Id, string Label, string Type, int Capacity, string Status, Guid HomeDepotId);

public sealed record DriverDto(Guid Id, string Name, string Status, Guid? VehicleId, string Territory);

public sealed record DepotDto(Guid Id, string Name, double Lat, double Lng);

public sealed record RouteStopDto(Guid Id, Guid RouteId, int Sequence, double Lat, double Lng, Guid? ShipmentId);

public sealed record RouteDto(
    Guid Id,
    string Label,
    Guid DriverId,
    Guid VehicleId,
    Guid DepotId,
    DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd,
    string Status,
    List<RouteStopDto> Stops);

public sealed record ShipmentDto(
    Guid Id,
    Guid? RouteId,
    string CustomerLabel,
    Guid OriginDepotId,
    double DestinationLat,
    double DestinationLng,
    DateTimeOffset SlaWindowStart,
    DateTimeOffset SlaWindowEnd,
    string Status,
    DateTimeOffset PlannedEta,
    DateTimeOffset? ActualDeliveredAt,
    string SlaState);

public sealed record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public sealed record ExceptionDto(
    Guid Id,
    Guid ShipmentId,
    string CustomerLabel,
    string Kind,
    string Severity,
    DateTimeOffset OpenedAt,
    string Status,
    string Note);

public sealed record ScheduleEntryDto(
    Guid Id,
    Guid DriverId,
    DateTimeOffset ShiftStart,
    DateTimeOffset ShiftEnd,
    Guid? RouteId,
    string Status);

public sealed record OverviewDto(
    decimal OnTimeRate,
    decimal OtifRate,
    int ActiveExceptionCount,
    decimal FleetUtilization,
    List<OnTimeTrendPointDto> OnTimeTrend,
    List<ExceptionSeverityBucketDto> ExceptionsBySeverity);

public sealed record OnTimeTrendPointDto(DateOnly Date, decimal OnTimeRate);

public sealed record ExceptionSeverityBucketDto(string Severity, int Count);

public sealed record MetaDto(string DatasetVersion, DateTimeOffset ScenarioClockReference, DateTimeOffset SeededAt);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);

public sealed record PatchScheduleRequest(DateTimeOffset? ShiftStart, DateTimeOffset? ShiftEnd, Guid? RouteId, string? Status);

public sealed record PatchExceptionStatusRequest(string Status);

public sealed record PatchRouteDriverRequest(Guid DriverId, Guid? VehicleId);
