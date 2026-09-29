using System.Net;
using System.Net.Http.Json;

namespace LogisticsFleetOperations.IntegrationTests;

/// <summary>Every write endpoint must reject an unauthenticated request with 401 — verified live, not merely absent from a happy-path test.</summary>
[Collection("api")]
public sealed class UnauthenticatedWriteTests(ApiFactory factory)
{
    [Fact]
    public async Task PatchSchedule_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();
        var response = await client.PatchAsJsonAsync($"/api/schedule/{Guid.NewGuid()}", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchExceptionStatus_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();
        var response = await client.PatchAsJsonAsync($"/api/exceptions/{Guid.NewGuid()}/status", new { status = "Investigating" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchRouteDriver_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();
        var response = await client.PatchAsJsonAsync($"/api/routes/{Guid.NewGuid()}/driver", new { driverId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
