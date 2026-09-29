using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Dtos;
using LogisticsFleetOperations.Domain;

namespace LogisticsFleetOperations.Application.Services;

/// <summary>Read model for Live Map and Delivery Timeline, plus the authenticated route/driver reassignment command.</summary>
public sealed class RouteService(IReadRepository readRepository, IWriteRepository writeRepository)
{
    public async Task<List<RouteDto>> GetRoutesAsync(string? status, CancellationToken ct)
    {
        RouteStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RouteStatus>(status, ignoreCase: true, out var value))
            {
                throw new BusinessRuleException($"Unknown route status '{status}'.");
            }
            parsed = value;
        }

        var routes = await readRepository.GetRoutesAsync(parsed, ct);
        return routes.Select(Mapping.ToDto).ToList();
    }

    /// <summary>
    /// Reassigns a route's driver (and optionally vehicle). Rejects assigning a driver already
    /// committed to an overlapping confirmed schedule entry for the route's planned window.
    /// </summary>
    public async Task<RouteDto> ReassignDriverAsync(Guid routeId, Guid driverId, Guid? vehicleId, CancellationToken ct)
    {
        var route = await writeRepository.GetRouteAsync(routeId, ct)
            ?? throw new NotFoundException($"Route '{routeId}' not found.");

        var driver = await writeRepository.GetDriverAsync(driverId, ct)
            ?? throw new NotFoundException($"Driver '{driverId}' not found.");

        if (vehicleId is { } vId)
        {
            _ = await writeRepository.GetVehicleAsync(vId, ct)
                ?? throw new NotFoundException($"Vehicle '{vId}' not found.");
        }

        var driverEntries = await writeRepository.GetDriverScheduleEntriesAsync(driverId, ct);
        var candidate = new ScheduleEntry
        {
            Id = Guid.Empty,
            DriverId = driverId,
            ShiftStart = route.PlannedStart,
            ShiftEnd = route.PlannedEnd,
            Status = ScheduleEntryStatus.Confirmed,
        };

        if (ScheduleRules.HasConflict(candidate, driverEntries))
        {
            throw new ConflictException(
                $"Driver '{driverId}' already has a confirmed shift overlapping this route's planned window.");
        }

        route.DriverId = driverId;
        if (vehicleId is { } newVehicleId)
        {
            route.VehicleId = newVehicleId;
        }

        await writeRepository.SaveRouteAsync(route, ct);
        return route.ToDto();
    }
}
