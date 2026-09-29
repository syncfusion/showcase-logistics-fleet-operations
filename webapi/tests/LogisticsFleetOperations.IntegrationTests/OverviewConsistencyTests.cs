using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using LogisticsFleetOperations.Domain;
using LogisticsFleetOperations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogisticsFleetOperations.IntegrationTests;

/// <summary>GET /api/overview must return numbers consistent with SlaRules computed directly from the same database state.</summary>
[Collection("api")]
public sealed class OverviewConsistencyTests(ApiFactory factory)
{
    [Fact]
    public async Task GetOverview_MatchesDirectSlaRulesComputation()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetWriteDbContext>();

        var shipments = await db.Shipments.AsNoTracking().ToListAsync();
        var exceptions = await db.Exceptions.AsNoTracking().ToListAsync();
        var vehicles = await db.Vehicles.AsNoTracking().ToListAsync();
        var routes = await db.Routes.AsNoTracking().ToListAsync();

        var delivered = shipments.Where(s => s.ActualDeliveredAt is not null).ToList();
        var exceptionsByShipment = exceptions.GroupBy(e => e.ShipmentId).ToDictionary(g => g.Key, g => g.ToList());

        var expectedOnTimeRate = SlaRules.OnTimeRate(delivered);
        var expectedOtifRate = SlaRules.OtifRate(delivered, exceptionsByShipment);
        var expectedActiveExceptions = exceptions.Count(e => e.Status is ExceptionStatus.Open or ExceptionStatus.Investigating or ExceptionStatus.Escalated);
        var expectedUtilization = SlaRules.FleetUtilization(vehicles, routes);

        using var client = factory.CreateClient();
        var overview = await client.GetFromJsonAsync<OverviewDto>("/api/overview");

        Assert.NotNull(overview);
        Assert.Equal(expectedOnTimeRate, overview!.OnTimeRate);
        Assert.Equal(expectedOtifRate, overview.OtifRate);
        Assert.Equal(expectedActiveExceptions, overview.ActiveExceptionCount);
        Assert.Equal(expectedUtilization, overview.FleetUtilization);
    }

    private sealed record OverviewDto(decimal OnTimeRate, decimal OtifRate, int ActiveExceptionCount, decimal FleetUtilization);
}
