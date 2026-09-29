namespace LogisticsFleetOperations.Infrastructure.Auth;

/// <summary>
/// Bound from configuration (Jwt:SigningKey / Jwt:Issuer / Jwt:Audience), sourced from
/// dotnet user-secrets on the Api project — never hardcoded in source.
/// </summary>
public sealed class JwtOptions
{
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "logistics-fleet-operations";
    public string Audience { get; set; } = "logistics-fleet-operations-clients";
    public int ExpiryMinutes { get; set; } = 60;
}
