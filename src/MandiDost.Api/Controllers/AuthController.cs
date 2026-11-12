using MandiDost.Api.Auth;
using MandiDost.Api.DTOs;
using MandiDost.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MandiDost.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(AuthConstants.AuthRateLimitPolicy)]
public class AuthController(IAuthService auth) : ControllerBase
{
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Create an account with a Pakistani mobile number.</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ClientIp, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        await auth.LoginAsync(request, ClientIp, ct);

    /// <summary>Exchange a refresh token for a new access + refresh token pair (rotation).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        await auth.RefreshAsync(request.RefreshToken, ClientIp, ct);

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(User.GetUserId(), request.RefreshToken, ct);
        return NoContent();
    }
}
