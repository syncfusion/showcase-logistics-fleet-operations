using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LogisticsFleetOperations.IntegrationTests;

internal static class TestAuth
{
    public static async Task<string> GetDemoTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "demo@logistics-fleet-operations.showcase",
            password = "FleetDemo!2026",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        return body!.Token;
    }

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(ApiFactory factory)
    {
        var client = factory.CreateClient();
        var token = await GetDemoTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed record LoginResponseDto(string Token, DateTimeOffset ExpiresAt);
}
