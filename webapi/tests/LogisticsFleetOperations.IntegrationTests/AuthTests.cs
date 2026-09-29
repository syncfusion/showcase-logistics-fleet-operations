using System.Net;
using System.Net.Http.Json;

namespace LogisticsFleetOperations.IntegrationTests;

[Collection("api")]
public sealed class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_WithValidDemoCredentials_ReturnsJwt()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "demo@logistics-fleet-operations.showcase",
            password = "FleetDemo!2026",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "demo@logistics-fleet-operations.showcase",
            password = "not-the-password",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record LoginResponseDto(string Token, DateTimeOffset ExpiresAt);
}
