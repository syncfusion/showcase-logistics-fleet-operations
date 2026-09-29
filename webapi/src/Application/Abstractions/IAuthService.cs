namespace LogisticsFleetOperations.Application.Abstractions;

/// <summary>
/// Showcase-scoped demo authentication port (not a production identity system).
/// Implemented in Infrastructure as DemoAuthProvider (JWT issuance).
/// </summary>
public interface IAuthService
{
    /// <summary>Returns a signed JWT (and its expiry) for the single demo account, or null if the credentials don't match.</summary>
    (string Token, DateTimeOffset ExpiresAt)? Login(string email, string password);
}
