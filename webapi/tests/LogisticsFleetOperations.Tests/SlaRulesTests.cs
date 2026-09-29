using LogisticsFleetOperations.Domain;

namespace LogisticsFleetOperations.Tests;

public class SlaRulesTests
{
    private static Shipment Shipment(DateTimeOffset slaStart, DateTimeOffset slaEnd, DateTimeOffset plannedEta, DateTimeOffset? deliveredAt = null) => new()
    {
        Id = Guid.NewGuid(),
        CustomerLabel = "Test",
        SlaWindowStart = slaStart,
        SlaWindowEnd = slaEnd,
        PlannedEta = plannedEta,
        ActualDeliveredAt = deliveredAt,
        Status = deliveredAt is null ? ShipmentStatus.InTransit : ShipmentStatus.Delivered,
    };

    [Fact]
    public void OnTrack_when_plenty_of_buffer_before_deadline()
    {
        var now = DateTimeOffset.Parse("2026-09-22T09:00:00-04:00");
        var s = Shipment(now.AddHours(-2), now.AddHours(4), now.AddHours(1));
        Assert.Equal(SlaState.OnTrack, SlaRules.Evaluate(s, now));
    }

    [Fact]
    public void AtRisk_when_planned_eta_within_threshold_of_deadline()
    {
        var now = DateTimeOffset.Parse("2026-09-22T09:00:00-04:00");
        var s = Shipment(now.AddHours(-2), now.AddMinutes(20), now.AddMinutes(10));
        Assert.Equal(SlaState.AtRisk, SlaRules.Evaluate(s, now));
    }

    [Fact]
    public void Breached_when_now_past_deadline_and_not_delivered()
    {
        var now = DateTimeOffset.Parse("2026-09-22T09:00:00-04:00");
        var s = Shipment(now.AddHours(-4), now.AddHours(-1), now.AddHours(-2));
        Assert.Equal(SlaState.Breached, SlaRules.Evaluate(s, now));
    }

    [Fact]
    public void DeliveredOnTime_when_delivered_at_or_before_window_end()
    {
        var now = DateTimeOffset.Parse("2026-09-22T09:00:00-04:00");
        var slaEnd = now.AddHours(-1);
        var s = Shipment(now.AddHours(-4), slaEnd, now.AddHours(-2), deliveredAt: slaEnd.AddMinutes(-5));
        Assert.Equal(SlaState.DeliveredOnTime, SlaRules.Evaluate(s, now));
        Assert.True(SlaRules.IsOnTime(s, now));
    }

    [Fact]
    public void DeliveredLate_when_delivered_after_window_end()
    {
        var now = DateTimeOffset.Parse("2026-09-22T09:00:00-04:00");
        var slaEnd = now.AddHours(-1);
        var s = Shipment(now.AddHours(-4), slaEnd, now.AddHours(-2), deliveredAt: slaEnd.AddMinutes(10));
        Assert.Equal(SlaState.DeliveredLate, SlaRules.Evaluate(s, now));
        Assert.False(SlaRules.IsOnTime(s, now));
    }

    [Fact]
    public void Otif_requires_all_linked_exceptions_resolved_even_if_on_time()
    {
        var now = DateTimeOffset.Parse("2026-09-22T09:00:00-04:00");
        var slaEnd = now.AddHours(-1);
        var s = Shipment(now.AddHours(-4), slaEnd, now.AddHours(-2), deliveredAt: slaEnd.AddMinutes(-5));
        var openException = new ShipmentException { Id = Guid.NewGuid(), ShipmentId = s.Id, Status = ExceptionStatus.Investigating, Kind = ExceptionKind.Damaged, Severity = ExceptionSeverity.Low, OpenedAt = now };
        Assert.True(SlaRules.IsOnTime(s, now));
        Assert.False(SlaRules.IsOtif(s, new[] { openException }, now));

        var resolved = new ShipmentException { Id = openException.Id, ShipmentId = s.Id, Status = ExceptionStatus.Resolved, Kind = ExceptionKind.Damaged, Severity = ExceptionSeverity.Low, OpenedAt = now };
        Assert.True(SlaRules.IsOtif(s, new[] { resolved }, now));
    }

    [Fact]
    public void OnTimeRate_computes_percentage_of_delivered_shipments()
    {
        var now = DateTimeOffset.Parse("2026-09-22T09:00:00-04:00");
        var onTime = Shipment(now.AddHours(-4), now.AddHours(-1), now.AddHours(-2), deliveredAt: now.AddHours(-1).AddMinutes(-5));
        var late = Shipment(now.AddHours(-4), now.AddHours(-1), now.AddHours(-2), deliveredAt: now.AddHours(-1).AddMinutes(10));
        Assert.Equal(50.0m, SlaRules.OnTimeRate(new[] { onTime, late }));
    }

    [Fact]
    public void OnTimeRate_is_zero_with_no_delivered_shipments()
    {
        Assert.Equal(0m, SlaRules.OnTimeRate(Array.Empty<Shipment>()));
    }

    [Theory]
    [InlineData(ExceptionStatus.Open, ExceptionStatus.Investigating, true)]
    [InlineData(ExceptionStatus.Open, ExceptionStatus.Resolved, false)]
    [InlineData(ExceptionStatus.Investigating, ExceptionStatus.Resolved, true)]
    [InlineData(ExceptionStatus.Resolved, ExceptionStatus.Investigating, false)]
    [InlineData(ExceptionStatus.Escalated, ExceptionStatus.Resolved, true)]
    public void ExceptionTransitions_reject_skipping_investigating(ExceptionStatus from, ExceptionStatus to, bool expected)
    {
        Assert.Equal(expected, ExceptionTransitions.IsAllowed(from, to));
    }

    [Fact]
    public void ScheduleRules_detects_overlapping_confirmed_shifts_for_same_driver()
    {
        var driverId = Guid.NewGuid();
        var baseTime = DateTimeOffset.Parse("2026-09-22T08:00:00-04:00");
        var existing = new ScheduleEntry { Id = Guid.NewGuid(), DriverId = driverId, ShiftStart = baseTime, ShiftEnd = baseTime.AddHours(8), Status = ScheduleEntryStatus.Confirmed };
        var overlapping = new ScheduleEntry { Id = Guid.NewGuid(), DriverId = driverId, ShiftStart = baseTime.AddHours(4), ShiftEnd = baseTime.AddHours(12), Status = ScheduleEntryStatus.Confirmed };
        var nonOverlapping = new ScheduleEntry { Id = Guid.NewGuid(), DriverId = driverId, ShiftStart = baseTime.AddHours(8), ShiftEnd = baseTime.AddHours(16), Status = ScheduleEntryStatus.Confirmed };

        Assert.True(ScheduleRules.HasConflict(overlapping, new[] { existing }));
        Assert.False(ScheduleRules.HasConflict(nonOverlapping, new[] { existing }));
    }

    [Fact]
    public void ScheduleRules_ignores_unconfirmed_entries()
    {
        var driverId = Guid.NewGuid();
        var baseTime = DateTimeOffset.Parse("2026-09-22T08:00:00-04:00");
        var tentative = new ScheduleEntry { Id = Guid.NewGuid(), DriverId = driverId, ShiftStart = baseTime, ShiftEnd = baseTime.AddHours(8), Status = ScheduleEntryStatus.Scheduled };
        var candidate = new ScheduleEntry { Id = Guid.NewGuid(), DriverId = driverId, ShiftStart = baseTime.AddHours(2), ShiftEnd = baseTime.AddHours(10), Status = ScheduleEntryStatus.Confirmed };

        Assert.False(ScheduleRules.HasConflict(candidate, new[] { tentative }));
    }

    [Fact]
    public void FleetUtilization_counts_active_vehicles_on_inprogress_routes()
    {
        var v1 = new Vehicle { Id = Guid.NewGuid(), Status = VehicleStatus.Active, Label = "V1" };
        var v2 = new Vehicle { Id = Guid.NewGuid(), Status = VehicleStatus.Active, Label = "V2" };
        var v3 = new Vehicle { Id = Guid.NewGuid(), Status = VehicleStatus.Maintenance, Label = "V3" };
        var routes = new[]
        {
            new Route { Id = Guid.NewGuid(), VehicleId = v1.Id, Status = RouteStatus.InProgress },
            new Route { Id = Guid.NewGuid(), VehicleId = v2.Id, Status = RouteStatus.Planned },
        };
        Assert.Equal(50.0m, SlaRules.FleetUtilization(new[] { v1, v2, v3 }, routes));
    }
}
