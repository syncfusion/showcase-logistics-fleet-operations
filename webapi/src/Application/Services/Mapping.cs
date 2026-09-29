using LogisticsFleetOperations.Application.Dtos;
using LogisticsFleetOperations.Domain;

namespace LogisticsFleetOperations.Application.Services;

internal static class Mapping
{
    public static VehicleDto ToDto(this Vehicle v) => new(v.Id, v.Label, v.Type, v.Capacity, v.Status.ToString(), v.HomeDepotId);

    public static DriverDto ToDto(this Driver d) => new(d.Id, d.Name, d.Status.ToString(), d.VehicleId, d.Territory);

    public static DepotDto ToDto(this Depot d) => new(d.Id, d.Name, d.Lat, d.Lng);

    public static RouteStopDto ToDto(this RouteStop s) => new(s.Id, s.RouteId, s.Sequence, s.Lat, s.Lng, s.ShipmentId);

    public static RouteDto ToDto(this Route r) => new(
        r.Id, r.Label, r.DriverId, r.VehicleId, r.DepotId, r.PlannedStart, r.PlannedEnd, r.Status.ToString(),
        r.Stops.OrderBy(s => s.Sequence).Select(ToDto).ToList());

    public static ShipmentDto ToDto(this Shipment s, DateTimeOffset now) => new(
        s.Id, s.RouteId, s.CustomerLabel, s.OriginDepotId, s.DestinationLat, s.DestinationLng,
        s.SlaWindowStart, s.SlaWindowEnd, s.Status.ToString(), s.PlannedEta, s.ActualDeliveredAt,
        SlaRules.Evaluate(s, now).ToString());

    public static ExceptionDto ToDto(this ShipmentException e, string customerLabel) => new(
        e.Id, e.ShipmentId, customerLabel, e.Kind.ToString(), e.Severity.ToString(), e.OpenedAt, e.Status.ToString(), e.Note);

    public static ScheduleEntryDto ToDto(this ScheduleEntry e) => new(
        e.Id, e.DriverId, e.ShiftStart, e.ShiftEnd, e.RouteId, e.Status.ToString());
}
