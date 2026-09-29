using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsFleetOperations.Api.Controllers;

/// <summary>
/// Showcase-scoped demo login only (single fixed account, not a real identity system). Issues a
/// short-lived JWT that authenticated write endpoints validate server-side.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var result = authService.Login(request.Email, request.Password);
        if (result is null)
        {
            return Unauthorized(new ApiError("invalid_credentials", "Email or password is incorrect."));
        }

        return Ok(new LoginResponse(result.Value.Token, result.Value.ExpiresAt));
    }
}
