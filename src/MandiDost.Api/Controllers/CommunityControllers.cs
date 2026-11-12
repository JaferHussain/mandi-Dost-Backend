using MandiDost.Api.Auth;
using MandiDost.Api.DTOs;
using MandiDost.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MandiDost.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chats")]
public class ChatsController(IChatService chats) : ControllerBase
{
    /// <summary>Your conversations, most recent first.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<ChatSummaryDto>> List(CancellationToken ct) => await chats.ListAsync(User.GetUserId(), ct);

    /// <summary>Number of unread messages (for the Messages tab badge).</summary>
    [HttpGet("unread-count")]
    public async Task<int> Unread(CancellationToken ct) => await chats.UnreadCountAsync(User.GetUserId(), ct);

    /// <summary>Open a conversation with a listing's seller or with a user. Returns the existing one if there is one.</summary>
    [HttpPost]
    public async Task<ChatSummaryDto> Start(StartChatInput input, CancellationToken ct) => await chats.StartAsync(User.GetUserId(), input, ct);

    /// <summary>
    /// Messages, newest 50 by default. Poll with ?afterId=lastId for new ones; ?beforeId= for older ones.
    /// Opening marks the other person's messages as read.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ChatDetailDto> Get(int id, [FromQuery] long? afterId, [FromQuery] long? beforeId, CancellationToken ct) =>
        await chats.GetAsync(User.GetUserId(), id, afterId, beforeId, ct);

    [HttpPost("{id:int}/messages")]
    public async Task<MessageDto> Send(int id, SendMessageInput input, CancellationToken ct) =>
        await chats.SendTextAsync(User.GetUserId(), id, input.Text, ct);

    /// <summary>Voice message (m4a/aac/3gp/webm/ogg/mp3, max 2 MB, max 3 minutes). Form fields: file, durationSeconds.</summary>
    [HttpPost("{id:int}/voice")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<MessageDto> Voice(int id, IFormFile file, [FromForm] int durationSeconds, CancellationToken ct) =>
        await chats.SendVoiceAsync(User.GetUserId(), id, file, durationSeconds, ct);
}

[ApiController]
[Route("api")]
public class QurbaniSharesController(ICommunityService community) : ControllerBase
{
    /// <summary>How many of the 7 shares are confirmed, requested and free. The seller also sees every booking.</summary>
    [HttpGet("listings/{id:int}/shares")]
    public async Task<SharesDto> Shares(int id, CancellationToken ct) => await community.GetSharesAsync(id, User.TryGetUserId(), ct);

    /// <summary>Ask the seller for 1–7 shares. They are held until the seller answers.</summary>
    [Authorize]
    [HttpPost("listings/{id:int}/bookings")]
    public async Task<BookingDto> Book(int id, BookSharesInput input, CancellationToken ct) =>
        await community.BookSharesAsync(User.GetUserId(), id, input, ct);

    [Authorize]
    [HttpPost("bookings/{id:int}/confirm")]
    public async Task<BookingDto> Confirm(int id, CancellationToken ct) => await community.DecideBookingAsync(User.GetUserId(), id, true, ct);

    [Authorize]
    [HttpPost("bookings/{id:int}/decline")]
    public async Task<BookingDto> Decline(int id, CancellationToken ct) => await community.DecideBookingAsync(User.GetUserId(), id, false, ct);

    [Authorize]
    [HttpPost("bookings/{id:int}/cancel")]
    public async Task<BookingDto> Cancel(int id, CancellationToken ct) => await community.CancelBookingAsync(User.GetUserId(), id, ct);

    [Authorize]
    [HttpGet("users/me/bookings")]
    public async Task<IReadOnlyList<BookingDto>> MyBookings(CancellationToken ct) => await community.MyBookingsAsync(User.GetUserId(), ct);
}

[ApiController]
[Route("api")]
public class PeopleController(ICommunityService community) : ControllerBase
{
    /// <summary>A seller's public page: name, business, rating, number (if shown).</summary>
    [HttpGet("users/{id:int}")]
    public async Task<PublicProfileDto> Profile(int id, CancellationToken ct) => await community.GetProfileAsync(id, User.TryGetUserId(), ct);

    [HttpGet("users/{id:int}/reviews")]
    public async Task<IReadOnlyList<ReviewDto>> Reviews(int id, CancellationToken ct) => await community.GetReviewsAsync(id, ct);

    /// <summary>Rate a seller you dealt with (chat, accepted offer or share booking). Writing again replaces your review.</summary>
    [Authorize]
    [HttpPost("users/{id:int}/reviews")]
    public async Task<ReviewDto> Review(int id, ReviewInput input, CancellationToken ct) =>
        await community.ReviewAsync(User.GetUserId(), id, input, ct);

    /// <summary>Report a fake or wrong listing. Three separate reports hide it until a moderator checks.</summary>
    [Authorize]
    [HttpPost("listings/{id:int}/reports")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Report(int id, ReportInput input, CancellationToken ct)
    {
        await community.ReportAsync(User.GetUserId(), id, input, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = AuthConstants.AdminPolicy)]
public class AdminReportsController(ICommunityService community) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ReportDto>> Open(CancellationToken ct) => await community.OpenReportsAsync(ct);

    [HttpPost("{id:int}/resolve")]
    public async Task<ReportDto> Resolve(int id, ResolveReportInput input, CancellationToken ct) =>
        await community.ResolveReportAsync(User.GetUserId(), id, input, ct);
}
