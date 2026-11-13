using MandiDost.Api.Auth;
using MandiDost.Api.DTOs;
using MandiDost.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MandiDost.Api.Controllers;

[ApiController]
[Authorize]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    /// <summary>
    /// Your notifications, newest first. The app polls with <c>?afterId=lastSeenId</c> to pick up new ones;
    /// <c>?beforeId=</c> loads older ones. <c>unreadCount</c> covers the whole inbox.
    /// </summary>
    [HttpGet("api/notifications")]
    public async Task<NotificationListDto> List([FromQuery] NotificationQuery query, CancellationToken ct) =>
        await notifications.ListAsync(User.GetUserId(), query, ct);

    [HttpGet("api/notifications/unread-count")]
    public async Task<int> Unread(CancellationToken ct) => await notifications.UnreadCountAsync(User.GetUserId(), ct);

    [HttpPost("api/notifications/{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPost("api/notifications/read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Which nearby alerts you want: on/off, radius, kinds and categories.</summary>
    [HttpGet("api/users/me/notification-settings")]
    public async Task<NotificationSettingsDto> GetSettings(CancellationToken ct) =>
        await notifications.GetSettingsAsync(User.GetUserId(), ct);

    [HttpPut("api/users/me/notification-settings")]
    public async Task<NotificationSettingsDto> PutSettings(NotificationSettingsInput input, CancellationToken ct) =>
        await notifications.UpdateSettingsAsync(User.GetUserId(), input, ct);
}
