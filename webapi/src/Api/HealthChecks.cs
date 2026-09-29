using LogisticsFleetOperations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LogisticsFleetOperations.Api;

/// <summary>
/// Checks a specific DbContext's connection can actually open -- not merely that the process is
/// running. Two separate checks (write/read-only) deliberately surface this app's two-database-
/// identity architecture: if the read-only role's credentials or grants ever broke, this reports
/// it distinctly from the write role, rather than one opaque "database: down".
/// </summary>
public sealed class DbConnectivityHealthCheck<TContext>(TContext context, string label) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context1, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy($"{label} connection reachable")
                : HealthCheckResult.Unhealthy($"{label} connection could not be opened");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{label} connection threw", ex);
        }
    }
}
