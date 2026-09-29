using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Dtos;
using LogisticsFleetOperations.Domain;

namespace LogisticsFleetOperations.Application.Services;

/// <summary>Computes Overview KPI aggregates server-side from the full dataset, never from a paged subset.</summary>
public sealed class FleetOverviewService(IReadRepository repository, TimeProvider clock)
{
    public async Task<OverviewDto> GetOverviewAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        var shipments = await repository.GetAllShipmentsAsync(ct);
        var exceptions = await repository.GetAllExceptionsAsync(ct);
        var vehicles = await repository.GetVehiclesAsync(ct);
        var routes = await repository.GetRoutesAsync(null, ct);

        var delivered = shipments.Where(s => s.ActualDeliveredAt is not null).ToList();
        var exceptionsByShipment = exceptions
            .GroupBy(e => e.ShipmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var onTimeRate = SlaRules.OnTimeRate(delivered);
        var otifRate = SlaRules.OtifRate(delivered, exceptionsByShipment);
        var activeExceptionCount = exceptions.Count(e => e.Status is ExceptionStatus.Open or ExceptionStatus.Investigating or ExceptionStatus.Escalated);
        var utilization = SlaRules.FleetUtilization(vehicles, routes);

        // On-time-rate trend across the trailing 7 days of the scenario window, bucketed by delivery date.
        var trend = new List<OnTimeTrendPointDto>();
        for (var offset = 6; offset >= 0; offset--)
        {
            var day = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-offset));
            var dayDelivered = delivered
                .Where(s => s.ActualDeliveredAt is { } d && DateOnly.FromDateTime(d.UtcDateTime) == day)
                .ToList();
            trend.Add(new OnTimeTrendPointDto(day, SlaRules.OnTimeRate(dayDelivered)));
        }

        var bySeverity = exceptions
            .Where(e => e.Status is ExceptionStatus.Open or ExceptionStatus.Investigating or ExceptionStatus.Escalated)
            .GroupBy(e => e.Severity)
            .Select(g => new ExceptionSeverityBucketDto(g.Key.ToString(), g.Count()))
            .OrderBy(b => b.Severity)
            .ToList();

        return new OverviewDto(onTimeRate, otifRate, activeExceptionCount, utilization, trend, bySeverity);
    }
}
