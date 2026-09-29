using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LogisticsFleetOperations.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LogisticsFleetOperations.Infrastructure.Auth;

/// <summary>
/// Showcase-scoped demo authentication: a single fixed demo account, not a real identity system.
/// The password is compared as a constant-time hash check; the JWT is signed with a symmetric key
/// sourced from configuration (user-secrets in development), never hardcoded.
/// </summary>
public sealed class DemoAuthProvider(IOptions<JwtOptions> options, TimeProvider clock) : IAuthService
{
    public const string DemoEmail = "demo@logistics-fleet-operations.showcase";

    // Fixed, disclosed demo password for this showcase account (documented in publication/README.md).
    // Not a real credential; compared via SHA-256 digest rather than a plaintext string compare.
    private const string DemoPassword = "FleetDemo!2026";
    private static readonly string DemoPasswordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DemoPassword)));

    public (string Token, DateTimeOffset ExpiresAt)? Login(string email, string password)
    {
        if (!string.Equals(email, DemoEmail, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var candidateHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password ?? "")));
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(candidateHash), Convert.FromHexString(DemoPasswordHash)))
        {
            return null;
        }

        var jwt = options.Value;
        if (string.IsNullOrWhiteSpace(jwt.SigningKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey is not configured. Set it via dotnet user-secrets.");
        }

        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, DemoEmail), new Claim(ClaimTypes.Name, DemoEmail)],
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
