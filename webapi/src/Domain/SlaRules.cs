namespace LogisticsFleetOperations.Domain;

public enum SlaState { OnTrack, AtRisk, Breached, DeliveredOnTime, DeliveredLate }

public static class SlaRules
{
    public static readonly TimeSpan AtRiskThreshold = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Resolves analysis's open question: on-time = delivered within the SLA window;
    /// at-risk = not yet delivered and plannedEta is within AtRiskThreshold of slaWindowEnd;
    /// breached = not yet delivered and now is past slaWindowEnd, or delivered after slaWindowEnd.
    /// </summary>
    public static SlaState Evaluate(Shipment shipment, DateTimeOffset now)
    {
        if (shipment.ActualDeliveredAt is { } deliveredAt)
        {
            return deliveredAt <= shipment.SlaWindowEnd ? SlaState.DeliveredOnTime : SlaState.DeliveredLate;
        }

        if (now > shipment.SlaWindowEnd)
        {
            return SlaState.Breached;
        }

        // At risk once the planned ETA is within (or past) the threshold window before the deadline.
        if (shipment.SlaWindowEnd - shipment.PlannedEta <= AtRiskThreshold)
        {
            return SlaState.AtRisk;
        }

        return SlaState.OnTrack;
    }

    public static bool IsOnTime(Shipment shipment, DateTimeOffset now) =>
        Evaluate(shipment, now) is SlaState.DeliveredOnTime;

    /// <summary>OTIF requires on-time delivery AND every linked exception resolved, not just timeliness.</summary>
    public static bool IsOtif(Shipment shipment, IEnumerable<ShipmentException> shipmentExceptions, DateTimeOffset now) =>
        IsOnTime(shipment, now) && shipmentExceptions.All(e => e.Status == ExceptionStatus.Resolved);

    public static decimal OnTimeRate(IReadOnlyCollection<Shipment> delivered)
    {
        if (delivered.Count == 0) return 0m;
        var onTime = delivered.Count(s => s.ActualDeliveredAt is { } d && d <= s.SlaWindowEnd);
        return Math.Round((decimal)onTime / delivered.Count * 100m, 1);
    }

    public static decimal OtifRate(IReadOnlyCollection<Shipment> delivered, IReadOnlyDictionary<Guid, List<ShipmentException>> exceptionsByShipment)
    {
        if (delivered.Count == 0) return 0m;
        var otif = delivered.Count(s =>
            s.ActualDeliveredAt is { } d && d <= s.SlaWindowEnd &&
            (!exceptionsByShipment.TryGetValue(s.Id, out var exs) || exs.All(e => e.Status == ExceptionStatus.Resolved)));
        return Math.Round((decimal)otif / delivered.Count * 100m, 1);
    }

    public static decimal FleetUtilization(IReadOnlyCollection<Vehicle> vehicles, IReadOnlyCollection<Route> routes)
    {
        var active = vehicles.Where(v => v.Status == VehicleStatus.Active).ToList();
        if (active.Count == 0) return 0m;
        var activeIds = active.Select(v => v.Id).ToHashSet();
        var onRoute = routes
            .Where(r => r.Status == RouteStatus.InProgress && activeIds.Contains(r.VehicleId))
            .Select(r => r.VehicleId)
            .Distinct()
            .Count();
        return Math.Round((decimal)onRoute / active.Count * 100m, 1);
    }
}

public static class ExceptionTransitions
{
    /// <summary>Cannot skip Open -&gt; Resolved directly; Investigating is a mandatory triage step.</summary>
    public static bool IsAllowed(ExceptionStatus from, ExceptionStatus to) => (from, to) switch
    {
        (ExceptionStatus.Open, ExceptionStatus.Investigating) => true,
        (ExceptionStatus.Open, ExceptionStatus.Escalated) => true,
        (ExceptionStatus.Investigating, ExceptionStatus.Resolved) => true,
        (ExceptionStatus.Investigating, ExceptionStatus.Escalated) => true,
        (ExceptionStatus.Escalated, ExceptionStatus.Investigating) => true,
        (ExceptionStatus.Escalated, ExceptionStatus.Resolved) => true,
        _ => false,
    };
}

public static class ScheduleRules
{
    public static bool Overlaps(ScheduleEntry a, ScheduleEntry b) =>
        a.DriverId == b.DriverId && a.Id != b.Id &&
        a.Status == ScheduleEntryStatus.Confirmed && b.Status == ScheduleEntryStatus.Confirmed &&
        a.ShiftStart < b.ShiftEnd && b.ShiftStart < a.ShiftEnd;

    public static bool HasConflict(ScheduleEntry candidate, IEnumerable<ScheduleEntry> existing) =>
        existing.Any(e => Overlaps(candidate, e));
}
