namespace LogisticsFleetOperations.Domain;

public enum VehicleStatus { Active, Maintenance, OutOfService }
public enum DriverStatus { OnShift, OffShift, OnBreak }
public enum RouteStatus { Planned, InProgress, Completed, Delayed }
public enum ShipmentStatus { Pending, Dispatched, InTransit, Delivered, Exception }
public enum ExceptionKind { SlaRisk, SlaBreach, FailedAttempt, Damaged, AddressIssue }
public enum ExceptionSeverity { Low, Medium, High }
public enum ExceptionStatus { Open, Investigating, Resolved, Escalated }
public enum ScheduleEntryStatus { Scheduled, Confirmed, Conflict }

public sealed class Vehicle
{
    public Guid Id { get; set; }
    public string Label { get; set; } = "";
    public string Type { get; set; } = "";
    public int Capacity { get; set; }
    public VehicleStatus Status { get; set; }
    public Guid HomeDepotId { get; set; }
}

public sealed class Driver
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DriverStatus Status { get; set; }
    public Guid? VehicleId { get; set; }
    public string Territory { get; set; } = "";
}

public sealed class Depot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public sealed class Route
{
    public Guid Id { get; set; }
    public string Label { get; set; } = "";
    public Guid DriverId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid DepotId { get; set; }
    public DateTimeOffset PlannedStart { get; set; }
    public DateTimeOffset PlannedEnd { get; set; }
    public RouteStatus Status { get; set; }
    public List<RouteStop> Stops { get; set; } = new();
}

public sealed class RouteStop
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public int Sequence { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public Guid? ShipmentId { get; set; }
}

public sealed class Shipment
{
    public Guid Id { get; set; }
    public Guid? RouteId { get; set; }
    public string CustomerLabel { get; set; } = "";
    public Guid OriginDepotId { get; set; }
    public double DestinationLat { get; set; }
    public double DestinationLng { get; set; }
    public DateTimeOffset SlaWindowStart { get; set; }
    public DateTimeOffset SlaWindowEnd { get; set; }
    public ShipmentStatus Status { get; set; }
    public DateTimeOffset PlannedEta { get; set; }
    public DateTimeOffset? ActualDeliveredAt { get; set; }
}

public sealed class ShipmentException
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public ExceptionKind Kind { get; set; }
    public ExceptionSeverity Severity { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public ExceptionStatus Status { get; set; }
    public string Note { get; set; } = "";
}

public sealed class ScheduleEntry
{
    public Guid Id { get; set; }
    public Guid DriverId { get; set; }
    public DateTimeOffset ShiftStart { get; set; }
    public DateTimeOffset ShiftEnd { get; set; }
    public Guid? RouteId { get; set; }
    public ScheduleEntryStatus Status { get; set; }
}
