using MandiDost.Api.Auth;
using MandiDost.Api.Common;
using MandiDost.Api.DTOs;
using MandiDost.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MandiDost.Api.Controllers;

[ApiController]
[Route("api/listings")]
public class ListingsController(IListingService listings) : ControllerBase
{
    /// <summary>
    /// Browse and search published listings. Examples:
    /// Qurbani feed: ?purpose=qurbani · restaurant feed: ?purpose=meatSupply&amp;bulk=true ·
    /// near me: ?lat=29.53&amp;lng=71.63&amp;radiusKm=50&amp;sort=nearest · crops: ?section=crops&amp;category=wheat
    /// </summary>
    [HttpGet]
    public async Task<PagedResult<ListingSummaryDto>> Search([FromQuery] ListingQuery query, CancellationToken ct) =>
        await listings.SearchAsync(query, ct);

    /// <summary>One listing with all its details. Owners also see their drafts.</summary>
    [HttpGet("{id:int}")]
    public async Task<ListingDetailDto> Get(int id, CancellationToken ct) =>
        await listings.GetAsync(id, User.TryGetUserId(), ct);

    /// <summary>Create a listing as a draft. Add photos, then publish.</summary>
    [Authorize]
    [HttpPost]
    [ProducesResponseType<ListingDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ListingDetailDto>> Create(ListingInput input, CancellationToken ct)
    {
        var created = await listings.CreateAsync(User.GetUserId(), input, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Replace the listing's details (same body as create).</summary>
    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<ListingDetailDto> Update(int id, ListingInput input, CancellationToken ct) =>
        await listings.UpdateAsync(User.GetUserId(), id, input, ct);

    /// <summary>Make the listing visible for 60 days. Publishing again renews it.</summary>
    [Authorize]
    [HttpPost("{id:int}/publish")]
    public async Task<ListingDetailDto> Publish(int id, CancellationToken ct) =>
        await listings.PublishAsync(User.GetUserId(), id, ct);

    [Authorize]
    [HttpPost("{id:int}/sold")]
    public async Task<ListingDetailDto> MarkSold(int id, CancellationToken ct) =>
        await listings.MarkSoldAsync(User.GetUserId(), id, ct);

    [Authorize]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        await listings.RemoveAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    /// <summary>Upload one photo (JPEG, PNG or WebP, max 5 MB, max 8 per listing). Form field: "file".</summary>
    [Authorize]
    [HttpPost("{id:int}/media")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType<MediaDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MediaDto>> AddMedia(int id, IFormFile file, CancellationToken ct)
    {
        var item = await listings.AddMediaAsync(User.GetUserId(), id, file, ct);
        return StatusCode(StatusCodes.Status201Created, item);
    }

    [Authorize]
    [HttpDelete("{id:int}/media/{mediaId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMedia(int id, int mediaId, CancellationToken ct)
    {
        await listings.DeleteMediaAsync(User.GetUserId(), id, mediaId, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/users/me")]
public class MyListingsController(IListingService listings) : ControllerBase
{
    /// <summary>The signed-in user's own listings (drafts, active, expired, sold).</summary>
    [HttpGet("listings")]
    public async Task<PagedResult<ListingSummaryDto>> Mine([FromQuery] PageQuery page, CancellationToken ct) =>
        await listings.GetMineAsync(User.GetUserId(), page, ct);

    [HttpGet("favorites")]
    public async Task<PagedResult<ListingSummaryDto>> Favorites([FromQuery] PageQuery page, CancellationToken ct) =>
        await listings.GetFavoritesAsync(User.GetUserId(), page, ct);

    [HttpPut("favorites/{listingId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddFavorite(int listingId, CancellationToken ct)
    {
        await listings.AddFavoriteAsync(User.GetUserId(), listingId, ct);
        return NoContent();
    }

    [HttpDelete("favorites/{listingId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveFavorite(int listingId, CancellationToken ct)
    {
        await listings.RemoveFavoriteAsync(User.GetUserId(), listingId, ct);
        return NoContent();
    }
}
