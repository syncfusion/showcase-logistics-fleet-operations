using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LogisticsFleetOperations.IntegrationTests;

/// <summary>
/// Boots the real Api host (real Postgres via the Api project's user-secrets connection strings —
/// no in-memory/fake provider) in the Development environment so user-secrets configuration loads.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
