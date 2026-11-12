using MandiDost.Api.Auth;
using MandiDost.Api.Common;
using MandiDost.Api.DTOs;
using MandiDost.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MandiDost.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me")]
public class MyBusinessController(IBusinessService businesses, IPurchaseRequestService requests) : ControllerBase
{
    /// <summary>Your business profile, or 204 if you haven't added one.</summary>
    [HttpGet("business")]
    [ProducesResponseType<BusinessDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        await businesses.GetMineAsync(User.GetUserId(), ct) is { } b ? Ok(b) : NoContent();

    /// <summary>Create or update your business profile (restaurant, hotel, butcher, trader…).</summary>
    [HttpPut("business")]
    public async Task<BusinessDto> Save(BusinessInput input, CancellationToken ct) =>
        await businesses.SaveMineAsync(User.GetUserId(), input, ct);

    /// <summary>Ask the Mandi Dost team to verify your business.</summary>
    [HttpPost("business/verification")]
    public async Task<BusinessDto> RequestVerification(CancellationToken ct) =>
        await businesses.RequestVerificationAsync(User.GetUserId(), ct);

    /// <summary>Requirements you posted as a buyer.</summary>
    [HttpGet("purchase-requests")]
    public async Task<PagedResult<PurchaseRequestSummaryDto>> MyRequests([FromQuery] PageQuery page, CancellationToken ct) =>
        await requests.GetMineAsync(User.GetUserId(), page, ct);

    /// <summary>Offers you made as a seller.</summary>
    [HttpGet("offers")]
    public async Task<PagedResult<OfferDto>> MyOffers([FromQuery] PageQuery page, CancellationToken ct) =>
        await requests.GetMyOffersAsync(User.GetUserId(), page, ct);
}

[ApiController]
[Route("api/businesses")]
public class BusinessesController(IBusinessService businesses) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<BusinessDto> Get(int id, CancellationToken ct) => await businesses.GetAsync(id, ct);
}

[ApiController]
[Route("api/admin/businesses")]
[Authorize(Policy = AuthConstants.AdminPolicy)]
public class AdminBusinessController(IBusinessService businesses) : ControllerBase
{
    /// <summary>Businesses waiting for verification, oldest first.</summary>
    [HttpGet("pending")]
    public async Task<IReadOnlyList<BusinessDto>> Pending(CancellationToken ct) => await businesses.GetPendingVerificationAsync(ct);

    [HttpPost("{id:int}/verification")]
    public async Task<BusinessDto> Decide(int id, VerificationDecision decision, CancellationToken ct) =>
        await businesses.DecideVerificationAsync(User.GetUserId(), id, decision, ct);
}

[ApiController]
[Route("api/purchase-requests")]
public class PurchaseRequestsController(IPurchaseRequestService requests) : ControllerBase
{
    /// <summary>
    /// Open buyer requirements for sellers to answer. Examples: restaurants' meat needs: ?purpose=meatSupply ·
    /// wheat buyers near me: ?section=crops&amp;category=wheat&amp;locationId=1&amp;radiusKm=100&amp;sort=nearest
    /// </summary>
    [HttpGet]
    public async Task<PagedResult<PurchaseRequestSummaryDto>> Search([FromQuery] PurchaseRequestQuery query, CancellationToken ct) =>
        await requests.SearchAsync(query, ct);

    /// <summary>One requirement. The buyer also sees every offer; a seller sees their own offer.</summary>
    [HttpGet("{id:int}")]
    public async Task<PurchaseRequestDetailDto> Get(int id, CancellationToken ct) =>
        await requests.GetAsync(id, User.TryGetUserId(), ct);

    [Authorize]
    [HttpPost]
    [ProducesResponseType<PurchaseRequestDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PurchaseRequestDetailDto>> Create(PurchaseRequestInput input, CancellationToken ct)
    {
        var created = await requests.CreateAsync(User.GetUserId(), input, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<PurchaseRequestDetailDto> Update(int id, PurchaseRequestInput input, CancellationToken ct) =>
        await requests.UpdateAsync(User.GetUserId(), id, input, ct);

    /// <summary>Close the requirement. fulfilled=true when you got what you needed.</summary>
    [Authorize]
    [HttpPost("{id:int}/close")]
    public async Task<PurchaseRequestDetailDto> Close(int id, [FromQuery] bool fulfilled, CancellationToken ct) =>
        await requests.CloseAsync(User.GetUserId(), id, fulfilled, ct);

    /// <summary>Open again for 30 days.</summary>
    [Authorize]
    [HttpPost("{id:int}/reopen")]
    public async Task<PurchaseRequestDetailDto> Reopen(int id, CancellationToken ct) =>
        await requests.ReopenAsync(User.GetUserId(), id, ct);

    /// <summary>Make (or change) your offer on this requirement.</summary>
    [Authorize]
    [HttpPost("{id:int}/offers")]
    public async Task<OfferDto> Offer(int id, OfferInput input, CancellationToken ct) =>
        await requests.MakeOfferAsync(User.GetUserId(), id, input, ct);
}

[ApiController]
[Authorize]
[Route("api/offers")]
public class OffersController(IPurchaseRequestService requests) : ControllerBase
{
    [HttpPost("{id:int}/accept")]
    public async Task<OfferDto> Accept(int id, CancellationToken ct) => await requests.DecideOfferAsync(User.GetUserId(), id, true, ct);

    [HttpPost("{id:int}/decline")]
    public async Task<OfferDto> Decline(int id, CancellationToken ct) => await requests.DecideOfferAsync(User.GetUserId(), id, false, ct);

    [HttpPost("{id:int}/withdraw")]
    public async Task<OfferDto> Withdraw(int id, CancellationToken ct) => await requests.WithdrawOfferAsync(User.GetUserId(), id, ct);
}
