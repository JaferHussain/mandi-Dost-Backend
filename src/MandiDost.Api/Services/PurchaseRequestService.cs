using MandiDost.Api.Common;
using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MandiDost.Api.Services;

public interface IPurchaseRequestService
{
    Task<PagedResult<PurchaseRequestSummaryDto>> SearchAsync(PurchaseRequestQuery query, CancellationToken ct = default);
    Task<PurchaseRequestDetailDto> GetAsync(int id, int? viewerId, CancellationToken ct = default);
    Task<PurchaseRequestDetailDto> CreateAsync(int buyerId, PurchaseRequestInput input, CancellationToken ct = default);
    Task<PurchaseRequestDetailDto> UpdateAsync(int buyerId, int id, PurchaseRequestInput input, CancellationToken ct = default);
    Task<PurchaseRequestDetailDto> CloseAsync(int buyerId, int id, bool fulfilled, CancellationToken ct = default);
    Task<PurchaseRequestDetailDto> ReopenAsync(int buyerId, int id, CancellationToken ct = default);
    Task<PagedResult<PurchaseRequestSummaryDto>> GetMineAsync(int buyerId, PageQuery page, CancellationToken ct = default);

    Task<OfferDto> MakeOfferAsync(int sellerId, int requestId, OfferInput input, CancellationToken ct = default);
    Task<OfferDto> WithdrawOfferAsync(int sellerId, int offerId, CancellationToken ct = default);
    Task<OfferDto> DecideOfferAsync(int buyerId, int offerId, bool accept, CancellationToken ct = default);
    Task<PagedResult<OfferDto>> GetMyOffersAsync(int sellerId, PageQuery page, CancellationToken ct = default);
}

public class PurchaseRequestService(AppDbContext db, TimeProvider clock) : IPurchaseRequestService
{
    public static readonly TimeSpan RequestLifetime = TimeSpan.FromDays(30);
    private const double KmPerDegree = 111.32;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---- Browse -------------------------------------------------------------

    public async Task<PagedResult<PurchaseRequestSummaryDto>> SearchAsync(PurchaseRequestQuery query, CancellationToken ct = default)
    {
        var now = Now;
        var q = db.PurchaseRequests.AsNoTracking().Where(r => r.Status == RequestStatus.Open && r.ExpiresAtUtc > now);

        if (query.Section is { } section) q = q.Where(r => r.Category.Section == section);
        if (query.Purpose is { } purpose) q = q.Where(r => r.Purpose == purpose);
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var slug = query.Category.Trim().ToLowerInvariant();
            q = q.Where(r => r.Category.Slug == slug);
        }
        if (query.Frequency is { } f) q = q.Where(r => r.Frequency == f);

        (double Lat, double Lng)? origin = query.Lat is { } lat && query.Lng is { } lng ? (lat, lng) : null;
        if (origin is null && query.LocationId is { } locId)
        {
            var loc = await db.Locations.AsNoTracking().Where(x => x.Id == locId).Select(x => new { x.Latitude, x.Longitude }).FirstOrDefaultAsync(ct);
            if (loc is not null) origin = (loc.Latitude, loc.Longitude);
        }
        if (query.LocationId is { } onlyCity && query.RadiusKm is null && query.Lat is null) q = q.Where(r => r.LocationId == onlyCity);

        var cosLat = origin is { } o0 ? Math.Cos(o0.Lat * Math.PI / 180) : 1;
        if (origin is { } o && query.RadiusKm is { } radius)
        {
            var maxDeg2 = Math.Pow(radius / KmPerDegree, 2);
            q = q.Where(r =>
                (r.Location.Latitude - o.Lat) * (r.Location.Latitude - o.Lat)
                + (r.Location.Longitude - o.Lng) * cosLat * ((r.Location.Longitude - o.Lng) * cosLat) <= maxDeg2);
        }

        q = query.Sort == "nearest" && origin is { } n
            ? q.OrderBy(r => (r.Location.Latitude - n.Lat) * (r.Location.Latitude - n.Lat)
                    + (r.Location.Longitude - n.Lng) * cosLat * ((r.Location.Longitude - n.Lng) * cosLat))
                .ThenByDescending(r => r.CreatedAtUtc)
            : q.OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id);

        var total = await q.CountAsync(ct);
        var rows = await Project(q.Skip(query.Skip).Take(query.PageSize)).ToListAsync(ct);
        return new PagedResult<PurchaseRequestSummaryDto>(rows.Select(r => r.ToDto(origin, now)).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<PagedResult<PurchaseRequestSummaryDto>> GetMineAsync(int buyerId, PageQuery page, CancellationToken ct = default)
    {
        var q = db.PurchaseRequests.AsNoTracking().Where(r => r.BuyerId == buyerId).OrderByDescending(r => r.UpdatedAtUtc).ThenByDescending(r => r.Id);
        var total = await q.CountAsync(ct);
        var rows = await Project(q.Skip(page.Skip).Take(page.PageSize)).ToListAsync(ct);
        var now = Now;
        return new PagedResult<PurchaseRequestSummaryDto>(rows.Select(r => r.ToDto(null, now)).ToList(), page.Page, page.PageSize, total);
    }

    // ---- Read one ---------------------------------------------------------------

    public async Task<PurchaseRequestDetailDto> GetAsync(int id, int? viewerId, CancellationToken ct = default)
    {
        var r = await db.PurchaseRequests.AsNoTracking()
            .Include(x => x.Category).ThenInclude(c => c.Purposes)
            .Include(x => x.Location)
            .Include(x => x.Buyer).ThenInclude(u => u.Profile)
            .Include(x => x.Buyer).ThenInclude(u => u.Business)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(ErrorCodes.RequestNotFound, "Requirement not found.");

        var isMine = viewerId == r.BuyerId;
        var status = EffectiveStatus(r);
        // Closed requirements stay visible to the buyer and to sellers who answered; others get 404.
        var offers = await OfferQuery().Where(o => o.PurchaseRequestId == id).OrderByDescending(o => o.UpdatedAtUtc).ToListAsync(ct);
        var mine = viewerId is { } v ? offers.FirstOrDefault(o => o.SellerId == v) : null;
        if (!isMine && status != RequestStatus.Open && mine is null)
            throw new NotFoundException(ErrorCodes.RequestNotFound, "Requirement not found.");

        // The buyer's number is shared with sellers once they make an offer (so they can talk), never with everyone.
        // A business contact number is meant for sharing; the personal number follows the user's "show my number" setting.
        string? buyerPhone = null;
        if (isMine || (mine is not null && mine.Status != OfferStatus.Withdrawn))
        {
            var p = r.Buyer.Business?.ContactPhone ?? (r.Buyer.Profile is { ShowPhoneNumber: false } ? null : r.Buyer.PhoneNumber);
            buyerPhone = p is null ? null : PhoneNumber.ToLocal(p);
        }

        return new PurchaseRequestDetailDto(
            r.Id, r.Title, r.Notes, r.Category.Section, r.Category.ToDto(), r.Purpose, r.Location.ToDto(),
            r.Quantity, r.QuantityUnit, r.Frequency, r.MinWeightKg, r.MaxWeightKg, r.TargetPrice, r.TargetPriceUnit,
            r.NeededBy, r.DeliveryNeeded, status, r.OfferCount, r.CreatedAtUtc, r.ExpiresAtUtc,
            BuyerOf(r.Buyer), buyerPhone, isMine,
            isMine ? offers.Select(o => ToOfferDto(o, r.Title, showSellerPhone: true)).ToList() : null,
            mine is null ? null : ToOfferDto(mine, r.Title, showSellerPhone: false));
    }

    // ---- Create / update / close -----------------------------------------------

    public async Task<PurchaseRequestDetailDto> CreateAsync(int buyerId, PurchaseRequestInput input, CancellationToken ct = default)
    {
        var now = Now;
        var r = new PurchaseRequest { BuyerId = buyerId, CreatedAtUtc = now, ExpiresAtUtc = now + RequestLifetime, Status = RequestStatus.Open };
        await ApplyAsync(r, input, ct);
        r.UpdatedAtUtc = now;
        db.PurchaseRequests.Add(r);
        await db.SaveChangesAsync(ct);
        return await GetAsync(r.Id, buyerId, ct);
    }

    public async Task<PurchaseRequestDetailDto> UpdateAsync(int buyerId, int id, PurchaseRequestInput input, CancellationToken ct = default)
    {
        var r = await LoadOwnAsync(buyerId, id, ct);
        if (r.Status != RequestStatus.Open) throw new BadRequestException(ErrorCodes.RequestClosed, "This requirement is closed.");
        await ApplyAsync(r, input, ct);
        r.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, buyerId, ct);
    }

    public async Task<PurchaseRequestDetailDto> CloseAsync(int buyerId, int id, bool fulfilled, CancellationToken ct = default)
    {
        var r = await LoadOwnAsync(buyerId, id, ct);
        r.Status = fulfilled ? RequestStatus.Fulfilled : RequestStatus.Closed;
        r.ClosedAtUtc = Now;
        r.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, buyerId, ct);
    }

    /// <summary>Open again for another 30 days (e.g. weekly supply still needed).</summary>
    public async Task<PurchaseRequestDetailDto> ReopenAsync(int buyerId, int id, CancellationToken ct = default)
    {
        var r = await LoadOwnAsync(buyerId, id, ct);
        var now = Now;
        r.Status = RequestStatus.Open;
        r.ClosedAtUtc = null;
        r.ExpiresAtUtc = now + RequestLifetime;
        r.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, buyerId, ct);
    }

    private async Task ApplyAsync(PurchaseRequest r, PurchaseRequestInput input, CancellationToken ct)
    {
        var category = await db.Categories.Include(c => c.Purposes).FirstOrDefaultAsync(c => c.Id == input.CategoryId && c.IsActive, ct)
            ?? throw new NotFoundException(ErrorCodes.CategoryNotFound, "Category not found.");
        if (!await db.Locations.AnyAsync(l => l.Id == input.LocationId && l.IsActive, ct))
            throw new NotFoundException(ErrorCodes.LocationNotFound, "Location not found.");

        if (category.Section == ProductSection.Livestock)
        {
            if (input.Purpose is { } p && category.Purposes.All(x => x.Purpose != p))
                throw new BadRequestException(ErrorCodes.PurposeNotAllowed, $"{p} is not allowed for {category.NameEn}.");
            if (input.Quantity != decimal.Truncate(input.Quantity))
                throw new BadRequestException(ErrorCodes.Validation, "Number of animals must be a whole number.");
        }
        else
        {
            if (input.Purpose is not null) throw new BadRequestException(ErrorCodes.DetailsMismatch, "Only animal requirements have a purpose.");
            if (input.MinWeightKg is not null || input.MaxWeightKg is not null)
                throw new BadRequestException(ErrorCodes.DetailsMismatch, "Weight per animal only applies to animals.");
        }
        if (input.MinWeightKg > input.MaxWeightKg)
            throw new BadRequestException(ErrorCodes.Validation, "Minimum weight is more than maximum weight.");
        if (!ListingRules.QuantityUnitsFor(category.Section).Contains(input.QuantityUnit))
            throw new BadRequestException(ErrorCodes.UnitNotAllowed, $"Quantity unit {input.QuantityUnit} is not allowed for {category.NameEn}.");
        if (input.TargetPrice is not null && input.TargetPriceUnit is null)
            throw new BadRequestException(ErrorCodes.UnitNotAllowed, "Say what the target price is per (animal, kg, maund…).");
        if (input.TargetPriceUnit is { } tpu && (tpu == PriceUnit.PerShare || !ListingRules.PriceUnitsFor(category).Contains(tpu)))
            throw new BadRequestException(ErrorCodes.UnitNotAllowed, $"Price unit {tpu} is not allowed for {category.NameEn}.");
        if (input.NeededBy is { } nb && nb < DateOnly.FromDateTime(Now))
            throw new BadRequestException(ErrorCodes.Validation, "The needed-by date is in the past.");

        r.CategoryId = category.Id;
        r.Purpose = category.Section == ProductSection.Livestock ? input.Purpose : null;
        r.LocationId = input.LocationId;
        r.Quantity = input.Quantity;
        r.QuantityUnit = input.QuantityUnit;
        r.Frequency = input.Frequency;
        r.MinWeightKg = input.MinWeightKg;
        r.MaxWeightKg = input.MaxWeightKg;
        r.TargetPrice = input.TargetPrice;
        r.TargetPriceUnit = input.TargetPrice is null ? null : input.TargetPriceUnit;
        r.NeededBy = input.NeededBy;
        r.DeliveryNeeded = input.DeliveryNeeded;
        r.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        r.Title = string.IsNullOrWhiteSpace(input.Title) ? AutoTitle(category, input) : input.Title.Trim();
    }

    /// <summary>"10 بکری / بکرا، 25–30 کلو، ہفتہ وار" – Urdu, built from the details.</summary>
    private static string AutoTitle(Category c, PurchaseRequestInput i)
    {
        var qty = i.Quantity == decimal.Truncate(i.Quantity) ? ((long)i.Quantity).ToString() : i.Quantity.ToString("0.##");
        var parts = new List<string>
        {
            c.Section == ProductSection.Livestock ? $"{qty} {c.NameUr}" : $"{c.NameUr} {qty} {UnitUr(i.QuantityUnit)}",
        };
        if (i.MinWeightKg is { } min && i.MaxWeightKg is { } max) parts.Add($"{min}–{max} کلو");
        else if (i.MinWeightKg is { } min2) parts.Add($"{min2} کلو سے زیادہ");
        else if (i.MaxWeightKg is { } max2) parts.Add($"{max2} کلو تک");
        parts.Add(i.Frequency switch
        {
            SupplyFrequency.Daily => "روزانہ",
            SupplyFrequency.Weekly => "ہفتہ وار",
            SupplyFrequency.Monthly => "ماہانہ",
            _ => "ایک بار",
        });
        return string.Join("، ", parts);
    }

    private static string UnitUr(QuantityUnit u) => u switch
    {
        QuantityUnit.Kg => "کلو", QuantityUnit.Maund => "من", QuantityUnit.Ton => "ٹن", QuantityUnit.Bag => "بوری",
        QuantityUnit.Litre => "لیٹر", QuantityUnit.Piece => "عدد", _ => "جانور",
    };

    // ---- Offers -------------------------------------------------------------------

    /// <summary>Make an offer, or update your existing one (one per seller per requirement).</summary>
    public async Task<OfferDto> MakeOfferAsync(int sellerId, int requestId, OfferInput input, CancellationToken ct = default)
    {
        var r = await db.PurchaseRequests.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new NotFoundException(ErrorCodes.RequestNotFound, "Requirement not found.");
        if (r.BuyerId == sellerId) throw new BadRequestException(ErrorCodes.OwnRequest, "You can't make an offer on your own requirement.");
        if (EffectiveStatus(r) != RequestStatus.Open) throw new BadRequestException(ErrorCodes.RequestClosed, "This requirement is closed.");
        if (input.Price is null && string.IsNullOrWhiteSpace(input.Message) && input.ListingId is null)
            throw new BadRequestException(ErrorCodes.Validation, "Give a price, a message or one of your listings.");
        if (input.Price is not null && input.PriceUnit is null)
            throw new BadRequestException(ErrorCodes.UnitNotAllowed, "Say what the price is per (animal, kg, maund…).");
        if (input.PriceUnit is { } pu && (pu == PriceUnit.PerShare || !ListingRules.PriceUnitsFor(r.Category).Contains(pu)))
            throw new BadRequestException(ErrorCodes.UnitNotAllowed, $"Price unit {pu} is not allowed here.");

        if (input.ListingId is { } listingId)
        {
            var listing = await db.Listings.Where(l => l.Id == listingId)
                .Select(l => new { l.SellerId, l.Status, l.ExpiresAtUtc, l.Category.Section }).FirstOrDefaultAsync(ct);
            if (listing is null || listing.SellerId != sellerId || listing.Status != ListingStatus.Active || listing.ExpiresAtUtc <= Now)
                throw new BadRequestException(ErrorCodes.OfferNotAllowed, "Attach one of your own published listings.");
            if (listing.Section != r.Category.Section)
                throw new BadRequestException(ErrorCodes.OfferNotAllowed, "That listing is not the kind of thing the buyer needs.");
        }

        var now = Now;
        var offer = await db.RequestOffers.FirstOrDefaultAsync(o => o.PurchaseRequestId == requestId && o.SellerId == sellerId, ct);
        if (offer is null)
        {
            offer = new RequestOffer { PurchaseRequestId = requestId, SellerId = sellerId, CreatedAtUtc = now };
            db.RequestOffers.Add(offer);
            r.OfferCount++;
        }
        else if (offer.Status == OfferStatus.Withdrawn)
        {
            r.OfferCount++; // coming back after withdrawing
        }
        offer.Price = input.Price;
        offer.PriceUnit = input.Price is null ? null : input.PriceUnit;
        offer.Quantity = input.Quantity;
        offer.ListingId = input.ListingId;
        offer.Message = string.IsNullOrWhiteSpace(input.Message) ? null : input.Message.Trim();
        offer.Status = OfferStatus.Pending; // a changed offer needs a fresh answer from the buyer
        offer.UpdatedAtUtc = now;
        r.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return await GetOfferAsync(offer.Id, showSellerPhone: false, ct);
    }

    public async Task<OfferDto> WithdrawOfferAsync(int sellerId, int offerId, CancellationToken ct = default)
    {
        var offer = await db.RequestOffers.Include(o => o.PurchaseRequest).FirstOrDefaultAsync(o => o.Id == offerId, ct)
            ?? throw new NotFoundException(ErrorCodes.OfferNotFound, "Offer not found.");
        if (offer.SellerId != sellerId) throw new ForbiddenException(ErrorCodes.NotOwner, "This is not your offer.");
        if (offer.Status != OfferStatus.Withdrawn)
        {
            offer.Status = OfferStatus.Withdrawn;
            offer.UpdatedAtUtc = Now;
            offer.PurchaseRequest.OfferCount = Math.Max(0, offer.PurchaseRequest.OfferCount - 1);
            await db.SaveChangesAsync(ct);
        }
        return await GetOfferAsync(offerId, showSellerPhone: false, ct);
    }

    /// <summary>The buyer accepts or declines. Accepting doesn't close the requirement – a weekly buyer may want several suppliers.</summary>
    public async Task<OfferDto> DecideOfferAsync(int buyerId, int offerId, bool accept, CancellationToken ct = default)
    {
        var offer = await db.RequestOffers.Include(o => o.PurchaseRequest).FirstOrDefaultAsync(o => o.Id == offerId, ct)
            ?? throw new NotFoundException(ErrorCodes.OfferNotFound, "Offer not found.");
        if (offer.PurchaseRequest.BuyerId != buyerId) throw new ForbiddenException(ErrorCodes.NotOwner, "This is not your requirement.");
        if (offer.Status == OfferStatus.Withdrawn) throw new BadRequestException(ErrorCodes.OfferNotAllowed, "The seller withdrew this offer.");
        offer.Status = accept ? OfferStatus.Accepted : OfferStatus.Declined;
        offer.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
        return await GetOfferAsync(offerId, showSellerPhone: true, ct);
    }

    public async Task<PagedResult<OfferDto>> GetMyOffersAsync(int sellerId, PageQuery page, CancellationToken ct = default)
    {
        var q = OfferQuery().Where(o => o.SellerId == sellerId).OrderByDescending(o => o.UpdatedAtUtc);
        var total = await q.CountAsync(ct);
        var items = await q.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<OfferDto>(items.Select(o => ToOfferDto(o, o.PurchaseRequest.Title, showSellerPhone: false)).ToList(), page.Page, page.PageSize, total);
    }

    // ---- Helpers --------------------------------------------------------------------

    private IQueryable<RequestOffer> OfferQuery() =>
        db.RequestOffers.AsNoTracking()
            .Include(o => o.PurchaseRequest)
            .Include(o => o.Seller).ThenInclude(s => s.Profile)
            .Include(o => o.Listing).ThenInclude(l => l!.Media);

    private async Task<OfferDto> GetOfferAsync(int id, bool showSellerPhone, CancellationToken ct)
    {
        var o = await OfferQuery().FirstAsync(x => x.Id == id, ct);
        return ToOfferDto(o, o.PurchaseRequest.Title, showSellerPhone);
    }

    private static OfferDto ToOfferDto(RequestOffer o, string requestTitle, bool showSellerPhone)
    {
        var thumb = o.Listing?.Media.OrderBy(m => m.SortOrder).Select(m => m.Path).FirstOrDefault();
        return new OfferDto(
            o.Id, o.PurchaseRequestId, requestTitle, o.Price, o.PriceUnit, o.Quantity, o.Message, o.Status,
            o.ListingId, o.Listing?.Title, thumb is null ? null : LocalMediaStorage.UrlFor(thumb),
            o.Seller.ToSummary(),
            // Only the requirement's buyer sees it, and only if the seller shows their number.
            showSellerPhone && o.Status != OfferStatus.Withdrawn && o.Seller.Profile is not { ShowPhoneNumber: false }
                ? PhoneNumber.ToLocal(o.Seller.PhoneNumber) : null,
            o.CreatedAtUtc, o.UpdatedAtUtc);
    }

    private async Task<PurchaseRequest> LoadOwnAsync(int buyerId, int id, CancellationToken ct)
    {
        var r = await db.PurchaseRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(ErrorCodes.RequestNotFound, "Requirement not found.");
        if (r.BuyerId != buyerId) throw new ForbiddenException(ErrorCodes.NotOwner, "This is not your requirement.");
        return r;
    }

    private RequestStatus EffectiveStatus(PurchaseRequest r) =>
        r.Status == RequestStatus.Open && r.ExpiresAtUtc <= Now ? RequestStatus.Expired : r.Status;

    private static BuyerSummaryDto BuyerOf(User u) => new(
        u.Id, u.FullName, u.PhoneVerified, u.Business?.BusinessName, u.Business?.Type,
        u.Business?.Verification == BusinessVerification.Verified);

    private static IQueryable<Row> Project(IQueryable<PurchaseRequest> q) => q.Select(r => new Row
    {
        Id = r.Id, Title = r.Title, Section = r.Category.Section, CategorySlug = r.Category.Slug, CategoryNameUr = r.Category.NameUr,
        Purpose = r.Purpose, Quantity = r.Quantity, QuantityUnit = r.QuantityUnit, Frequency = r.Frequency,
        MinWeightKg = r.MinWeightKg, MaxWeightKg = r.MaxWeightKg, TargetPrice = r.TargetPrice, TargetPriceUnit = r.TargetPriceUnit,
        NeededBy = r.NeededBy, LocationNameUr = r.Location.NameUr, Lat = r.Location.Latitude, Lng = r.Location.Longitude,
        OfferCount = r.OfferCount, Status = r.Status, ExpiresAtUtc = r.ExpiresAtUtc, CreatedAtUtc = r.CreatedAtUtc,
        BuyerId = r.BuyerId, BuyerName = r.Buyer.FullName, BuyerPhoneVerified = r.Buyer.PhoneVerified,
        BusinessName = r.Buyer.Business != null ? r.Buyer.Business.BusinessName : null,
        BusinessType = r.Buyer.Business != null ? r.Buyer.Business.Type : null,
        BusinessVerified = r.Buyer.Business != null && r.Buyer.Business.Verification == BusinessVerification.Verified,
    });

    private sealed class Row
    {
        public int Id { get; init; }
        public string Title { get; init; } = "";
        public ProductSection Section { get; init; }
        public string CategorySlug { get; init; } = "";
        public string CategoryNameUr { get; init; } = "";
        public ListingPurpose? Purpose { get; init; }
        public decimal Quantity { get; init; }
        public QuantityUnit QuantityUnit { get; init; }
        public SupplyFrequency Frequency { get; init; }
        public int? MinWeightKg { get; init; }
        public int? MaxWeightKg { get; init; }
        public long? TargetPrice { get; init; }
        public PriceUnit? TargetPriceUnit { get; init; }
        public DateOnly? NeededBy { get; init; }
        public string LocationNameUr { get; init; } = "";
        public double Lat { get; init; }
        public double Lng { get; init; }
        public int OfferCount { get; init; }
        public RequestStatus Status { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public int BuyerId { get; init; }
        public string BuyerName { get; init; } = "";
        public bool BuyerPhoneVerified { get; init; }
        public string? BusinessName { get; init; }
        public BusinessType? BusinessType { get; init; }
        public bool BusinessVerified { get; init; }

        public PurchaseRequestSummaryDto ToDto((double Lat, double Lng)? origin, DateTime now) => new(
            Id, Title, Section, CategorySlug, CategoryNameUr, Purpose, Quantity, QuantityUnit, Frequency,
            MinWeightKg, MaxWeightKg, TargetPrice, TargetPriceUnit, NeededBy, LocationNameUr,
            origin is { } o ? Math.Round(Geo.DistanceKm(o.Lat, o.Lng, Lat, Lng), 1) : null,
            OfferCount, Status == RequestStatus.Open && ExpiresAtUtc <= now ? RequestStatus.Expired : Status, CreatedAtUtc,
            new BuyerSummaryDto(BuyerId, BuyerName, BuyerPhoneVerified, BusinessName, BusinessType, BusinessVerified));
    }
}
