using LogisticsFleetOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace LogisticsFleetOperations.Infrastructure.Persistence.Seeding;

/// <summary>Counts from a completed <see cref="DemoDataSeeder.ResetAsync"/> run, for the CLI's console
/// output and the API's "reset demo data" response body.</summary>
public sealed record SeedSummary(
    string DatasetVersion,
    DateTimeOffset SeededAt,
    int Depots,
    int Vehicles,
    int Drivers,
    int Routes,
    int RouteStops,
    int Shipments,
    int Exceptions,
    int ScheduleEntries);

/// <summary>
/// The single source of truth for this app's deterministic demo dataset. Deletes every seeded row and
/// rebuilds the same fixed scenario (fixed RNG seed; shipment/exception windows computed relative to
/// "now" at reset time) — used by both the standalone `dotnet run` seed command (initial provisioning)
/// and the authenticated POST /api/admin/reset-data endpoint (undoing in-app writes during a demo,
/// per the operator's request: an open exception moved to Resolved, a schedule conflict edit, etc.
/// cannot be reverted through the UI otherwise).
/// </summary>
public static class DemoDataSeeder
{
    private const string DatasetVersion = "2026.09.22-1";

    public static async Task<SeedSummary> ResetAsync(FleetWriteDbContext db, CancellationToken ct = default)
    {
        // Delete in FK-safe order.
        db.Exceptions.RemoveRange(db.Exceptions);
        db.RouteStops.RemoveRange(db.RouteStops);
        db.ScheduleEntries.RemoveRange(db.ScheduleEntries);
        db.Shipments.RemoveRange(db.Shipments);
        db.Routes.RemoveRange(db.Routes);
        db.Drivers.RemoveRange(db.Drivers);
        db.Vehicles.RemoveRange(db.Vehicles);
        db.Depots.RemoveRange(db.Depots);
        db.DatasetMeta.RemoveRange(db.DatasetMeta);
        await db.SaveChangesAsync(ct);

        var rng = new Random(20260922); // fixed seed: deterministic scenario
        var now = DateTimeOffset.UtcNow;

        // --- Depots: synthetic coordinates in the Denver, CO metro bounding box (real but non-identifying). ---
        var depots = new List<Depot>
        {
            new() { Id = Guid.NewGuid(), Name = "North Metro Depot", Lat = 39.8283, Lng = -104.9876 },
            new() { Id = Guid.NewGuid(), Name = "South Metro Depot", Lat = 39.6133, Lng = -104.8907 },
        };
        db.Depots.AddRange(depots);

        // --- Vehicles: ~12 across van/box-truck/bike, mostly active with a couple maintenance/out-of-service. ---
        string[] vehicleTypes = ["van", "box-truck", "bike"];
        var vehicles = new List<Vehicle>();
        for (var i = 1; i <= 12; i++)
        {
            var status = i switch
            {
                11 => VehicleStatus.Maintenance,
                12 => VehicleStatus.OutOfService,
                _ => VehicleStatus.Active,
            };
            var type = vehicleTypes[(i - 1) % vehicleTypes.Length];
            vehicles.Add(new Vehicle
            {
                Id = Guid.NewGuid(),
                Label = $"{Capitalize(type)} {i:00}",
                Type = type,
                Capacity = type switch { "van" => 40, "box-truck" => 90, _ => 12 },
                Status = status,
                HomeDepotId = depots[i % depots.Count].Id,
            });
        }
        db.Vehicles.AddRange(vehicles);

        // --- Drivers: ~10, synthetic names, mixed shift status, most assigned to a vehicle. ---
        string[] firstNames = ["Jordan", "Casey", "Morgan", "Riley", "Avery", "Quinn", "Rowan", "Emerson", "Hayden", "Dakota"];
        string[] lastNames = ["Reyes", "Nakamura", "Okafor", "Bianchi", "Petrov", "Larsen", "Alvarado", "Kowalski", "Mensah", "Dubois"];
        string[] territories = ["North Metro", "South Metro", "Central", "Foothills"];
        var drivers = new List<Driver>();
        for (var i = 0; i < 10; i++)
        {
            var status = i switch
            {
                < 6 => DriverStatus.OnShift,
                < 8 => DriverStatus.OnBreak,
                _ => DriverStatus.OffShift,
            };
            drivers.Add(new Driver
            {
                Id = Guid.NewGuid(),
                Name = $"{firstNames[i]} {lastNames[i]}",
                Status = status,
                VehicleId = i < vehicles.Count ? vehicles[i].Id : null,
                Territory = territories[i % territories.Length],
            });
        }
        db.Drivers.AddRange(drivers);

        // --- Routes: ~15, spanning planned/in-progress/completed/delayed. ---
        RouteStatus[] routeStatusCycle =
        [
            RouteStatus.Planned, RouteStatus.InProgress, RouteStatus.Completed, RouteStatus.Delayed, RouteStatus.InProgress,
        ];
        var routes = new List<Route>();
        for (var i = 1; i <= 15; i++)
        {
            var depot = depots[i % depots.Count];
            var driver = drivers[i % drivers.Count];
            var vehicle = vehicles[i % (vehicles.Count - 2)]; // skip the maintenance/out-of-service pair for route assignment
            var status = routeStatusCycle[i % routeStatusCycle.Length];
            var plannedStart = now.AddHours(-12 + i);
            var plannedEnd = plannedStart.AddHours(3 + (i % 3));

            routes.Add(new Route
            {
                Id = Guid.NewGuid(),
                Label = $"Route {i:00}",
                DriverId = driver.Id,
                VehicleId = vehicle.Id,
                DepotId = depot.Id,
                PlannedStart = plannedStart,
                PlannedEnd = plannedEnd,
                Status = status,
            });
        }
        db.Routes.AddRange(routes);

        // Route stops: 3-5 per route, walking outward from the route's depot within the metro box.
        var stops = new List<RouteStop>();
        foreach (var route in routes)
        {
            var depot = depots.First(d => d.Id == route.DepotId);
            var stopCount = 3 + (rng.Next(3));
            for (var seq = 1; seq <= stopCount; seq++)
            {
                stops.Add(new RouteStop
                {
                    Id = Guid.NewGuid(),
                    RouteId = route.Id,
                    Sequence = seq,
                    Lat = depot.Lat + (rng.NextDouble() - 0.5) * 0.18,
                    Lng = depot.Lng + (rng.NextDouble() - 0.5) * 0.18,
                    ShipmentId = null, // linked below once shipments exist
                });
            }
        }
        db.RouteStops.AddRange(stops);

        // --- Shipments: ~40 across every status, with deliberate SLA-state variety. ---
        string[] customerAdjectives = ["Summit", "Prairie", "Ridgeline", "Bluebird", "Canyon", "Aspen", "Foothill", "Mesa", "Juniper", "Timber"];
        string[] customerNouns = ["Retail Co", "Logistics Partners", "Market Group", "Supply Works", "Distribution LLC", "Trading Post", "Goods Collective", "Fulfillment Hub"];
        var shipments = new List<Shipment>();

        for (var i = 1; i <= 40; i++)
        {
            var depot = depots[i % depots.Count];
            var customerLabel = $"{customerAdjectives[i % customerAdjectives.Length]} {customerNouns[i % customerNouns.Length]}";
            var id = Guid.NewGuid();

            // Distribute across a deliberate mix of statuses/SLA outcomes (bucketed by index range).
            Shipment shipment;
            if (i <= 12)
            {
                // Delivered on-time.
                var windowStart = now.AddDays(-2).AddHours(-i);
                var windowEnd = windowStart.AddHours(4);
                var delivered = windowEnd.AddMinutes(-15 - rng.Next(60));
                shipment = BuildShipment(id, null, customerLabel, depot.Id, windowStart, windowEnd, ShipmentStatus.Delivered, delivered.AddMinutes(-10), delivered, rng);
            }
            else if (i <= 18)
            {
                // Delivered late (breached after the fact).
                var windowStart = now.AddDays(-1).AddHours(-i);
                var windowEnd = windowStart.AddHours(3);
                var delivered = windowEnd.AddMinutes(20 + rng.Next(90));
                shipment = BuildShipment(id, null, customerLabel, depot.Id, windowStart, windowEnd, ShipmentStatus.Delivered, windowEnd.AddMinutes(-30), delivered, rng);
            }
            else if (i <= 24)
            {
                // In-transit, on track (comfortable window ahead).
                var windowStart = now.AddHours(-1);
                var windowEnd = now.AddHours(5 + rng.Next(4));
                shipment = BuildShipment(id, null, customerLabel, depot.Id, windowStart, windowEnd, ShipmentStatus.InTransit, windowEnd.AddHours(-3), null, rng);
            }
            else if (i <= 30)
            {
                // In-transit, at-risk (planned ETA within the 30-minute threshold of the SLA deadline).
                var windowStart = now.AddHours(-3);
                var windowEnd = now.AddMinutes(20 + rng.Next(10));
                shipment = BuildShipment(id, null, customerLabel, depot.Id, windowStart, windowEnd, ShipmentStatus.InTransit, windowEnd.AddMinutes(-10), null, rng);
            }
            else if (i <= 34)
            {
                // Not yet delivered, already past the SLA window: breached.
                var windowStart = now.AddHours(-6);
                var windowEnd = now.AddHours(-1);
                shipment = BuildShipment(id, null, customerLabel, depot.Id, windowStart, windowEnd, ShipmentStatus.Exception, windowEnd.AddHours(-2), null, rng);
            }
            else if (i <= 37)
            {
                // Dispatched, not yet en route, plenty of runway.
                var windowStart = now.AddHours(1);
                var windowEnd = now.AddHours(8);
                shipment = BuildShipment(id, null, customerLabel, depot.Id, windowStart, windowEnd, ShipmentStatus.Dispatched, windowEnd.AddHours(-2), null, rng);
            }
            else
            {
                // Pending, not yet dispatched to a route.
                var windowStart = now.AddHours(4);
                var windowEnd = now.AddHours(14);
                shipment = BuildShipment(id, null, customerLabel, depot.Id, windowStart, windowEnd, ShipmentStatus.Pending, windowEnd.AddHours(-3), null, rng);
            }

            shipments.Add(shipment);
        }

        // Dispatch roughly two-thirds of shipments onto routes/stops, linking RouteId and stop.ShipmentId.
        var dispatchable = shipments.Where(s => s.Status != ShipmentStatus.Pending).ToList();
        var availableStops = stops.Where(s => s.ShipmentId is null).ToList();
        for (var i = 0; i < dispatchable.Count && i < availableStops.Count; i++)
        {
            var route = routes.First(r => r.Id == availableStops[i].RouteId);
            dispatchable[i].RouteId = route.Id;
            availableStops[i].ShipmentId = dispatchable[i].Id;
        }

        db.Shipments.AddRange(shipments);

        // --- Exceptions: ~8 open/in-progress across kinds/severities, linked to shipments in Exception status
        // or a few delivered-late ones (so OTIF-vs-on-time-rate has real divergence to display). ---
        var exceptionCandidates = shipments
            .Where(s => s.Status == ShipmentStatus.Exception)
            .Concat(shipments.Where(s => s.Status == ShipmentStatus.Delivered && s.ActualDeliveredAt > s.SlaWindowEnd).Take(3))
            .Concat(shipments.Where(s => s.Status == ShipmentStatus.InTransit).Take(1))
            .Distinct()
            .Take(8)
            .ToList();

        ExceptionKind[] kinds = [ExceptionKind.SlaRisk, ExceptionKind.SlaBreach, ExceptionKind.FailedAttempt, ExceptionKind.Damaged, ExceptionKind.AddressIssue];
        ExceptionSeverity[] severities = [ExceptionSeverity.Low, ExceptionSeverity.Medium, ExceptionSeverity.High];
        ExceptionStatus[] statusCycle = [ExceptionStatus.Open, ExceptionStatus.Investigating, ExceptionStatus.Escalated, ExceptionStatus.Resolved];

        var exceptions = new List<ShipmentException>();
        for (var i = 0; i < exceptionCandidates.Count; i++)
        {
            var kind = kinds[i % kinds.Length];
            var shipment = exceptionCandidates[i];
            exceptions.Add(new ShipmentException
            {
                Id = Guid.NewGuid(),
                ShipmentId = shipment.Id,
                Kind = kind,
                Severity = severities[i % severities.Length],
                OpenedAt = now.AddHours(-1 - i),
                Status = statusCycle[i % statusCycle.Length],
                Note = NoteFor(kind, shipment.CustomerLabel),
            });
        }
        db.Exceptions.AddRange(exceptions);

        // --- Schedule: one week of shifts per driver, all non-overlapping/confirmed. One intentionally
        // conflicting pair is deliberately NOT inserted here (see comment below) to prove the conflict
        // rule matters when exercised through the API/UI rather than by construction of the seed data. ---
        var scheduleEntries = new List<ScheduleEntry>();
        var weekStart = now.Date.AddDays(-(int)now.DayOfWeek);
        for (var day = 0; day < 7; day++)
        {
            var shiftDate = weekStart.AddDays(day);
            foreach (var (driver, idx) in drivers.Select((d, idx) => (d, idx)))
            {
                if ((day + idx) % 3 == 2)
                {
                    continue; // rest day for this driver
                }

                var shiftStartHour = 6 + (idx % 3) * 4; // stagger shift starts so drivers don't all overlap
                var shiftStart = new DateTimeOffset(shiftDate, TimeSpan.Zero).AddHours(shiftStartHour);
                var shiftEnd = shiftStart.AddHours(8);
                var linkedRoute = routes.Count > 0 && (day + idx) % 4 == 0 ? routes[(day + idx) % routes.Count] : null;

                scheduleEntries.Add(new ScheduleEntry
                {
                    Id = Guid.NewGuid(),
                    DriverId = driver.Id,
                    ShiftStart = shiftStart,
                    ShiftEnd = shiftEnd,
                    RouteId = linkedRoute?.Id,
                    Status = ScheduleEntryStatus.Confirmed,
                });
            }
        }
        // NOTE (intentionally not inserted): a conflicting pair for drivers[0] — e.g. a second Confirmed
        // shift on weekStart.AddHours(8) overlapping the 06:00-14:00 shift above — is deliberately left out
        // of the seeded data. ScheduleRules.HasConflict / the PATCH /api/schedule/{id} endpoint reject that
        // exact shape of request; integration tests construct it on demand instead of it living in steady-state data.
        db.ScheduleEntries.AddRange(scheduleEntries);

        db.DatasetMeta.Add(new DatasetMetaRow
        {
            DatasetVersion = DatasetVersion,
            ScenarioClockReference = now,
            SeededAt = now,
        });

        await db.SaveChangesAsync(ct);

        return new SeedSummary(
            DatasetVersion,
            now,
            depots.Count,
            vehicles.Count,
            drivers.Count,
            routes.Count,
            stops.Count,
            shipments.Count,
            exceptions.Count,
            scheduleEntries.Count);
    }

    private static Shipment BuildShipment(
        Guid id, Guid? routeId, string customerLabel, Guid originDepotId,
        DateTimeOffset windowStart, DateTimeOffset windowEnd, ShipmentStatus status,
        DateTimeOffset plannedEta, DateTimeOffset? actualDeliveredAt, Random rng) => new()
    {
        Id = id,
        RouteId = routeId,
        CustomerLabel = customerLabel,
        OriginDepotId = originDepotId,
        DestinationLat = 39.7392 + (rng.NextDouble() - 0.5) * 0.35,
        DestinationLng = -104.9903 + (rng.NextDouble() - 0.5) * 0.35,
        SlaWindowStart = windowStart,
        SlaWindowEnd = windowEnd,
        Status = status,
        PlannedEta = plannedEta,
        ActualDeliveredAt = actualDeliveredAt,
    };

    private static string NoteFor(ExceptionKind kind, string customerLabel) => kind switch
    {
        ExceptionKind.SlaRisk => $"{customerLabel}'s delivery window is closing fast — ETA is within the risk threshold.",
        ExceptionKind.SlaBreach => $"{customerLabel}'s SLA window has passed without delivery confirmation.",
        ExceptionKind.FailedAttempt => $"Driver could not complete delivery for {customerLabel} — recipient unavailable.",
        ExceptionKind.Damaged => $"Package for {customerLabel} reported damaged in transit; inspect before redelivery.",
        ExceptionKind.AddressIssue => $"Delivery address for {customerLabel} could not be located as entered.",
        _ => $"Exception flagged for {customerLabel}.",
    };

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..].Replace("-truck", " Truck");
}
