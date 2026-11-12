using MandiDost.Api.Auth;
using MandiDost.Api.DTOs;
using MandiDost.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MandiDost.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me")]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken ct) =>
        await users.GetMeAsync(User.GetUserId(), ct);

    /// <summary>Partial update – only fields that are sent are changed.</summary>
    [HttpPatch]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileRequest request, CancellationToken ct) =>
        await users.UpdateMeAsync(User.GetUserId(), request, ct);

    /// <summary>Changes the password and signs out all devices.</summary>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await users.ChangePasswordAsync(User.GetUserId(), request, ct);
        return NoContent();
    }
}
