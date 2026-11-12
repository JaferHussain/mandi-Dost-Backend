using MandiDost.Api.Common;
using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MandiDost.Api.Services;

public interface IListingService
{
    Task<PagedResult<ListingSummaryDto>> SearchAsync(ListingQuery query, CancellationToken ct = default);
    Task<ListingDetailDto> GetAsync(int id, int? viewerId, CancellationToken ct = default);
    Task<ListingDetailDto> CreateAsync(int sellerId, ListingInput input, CancellationToken ct = default);
    Task<ListingDetailDto> UpdateAsync(int sellerId, int id, ListingInput input, CancellationToken ct = default);
    Task<ListingDetailDto> PublishAsync(int sellerId, int id, CancellationToken ct = default);
    Task<ListingDetailDto> MarkSoldAsync(int sellerId, int id, CancellationToken ct = default);
    Task RemoveAsync(int sellerId, int id, CancellationToken ct = default);
    Task<PagedResult<ListingSummaryDto>> GetMineAsync(int sellerId, PageQuery page, CancellationToken ct = default);
    Task<MediaDto> AddMediaAsync(int sellerId, int id, IFormFile file, CancellationToken ct = default);
    Task DeleteMediaAsync(int sellerId, int id, int mediaId, CancellationToken ct = default);
    Task AddFavoriteAsync(int userId, int listingId, CancellationToken ct = default);
    Task RemoveFavoriteAsync(int userId, int listingId, CancellationToken ct = default);
    Task<PagedResult<ListingSummaryDto>> GetFavoritesAsync(int userId, PageQuery page, CancellationToken ct = default);
}

public class ListingService(AppDbContext db, IMediaStorage media, TimeProvider clock) : IListingService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private const double KmPerDegree = 111.32;

    // ---- Search -----------------------------------------------------------

    public async Task<PagedResult<ListingSummaryDto>> SearchAsync(ListingQuery query, CancellationToken ct = default)
    {
        var now = Now;
        var q = db.Listings.AsNoTracking()
            .Where(l => l.Status == ListingStatus.Active && (l.ExpiresAtUtc == null || l.ExpiresAtUtc > now));

        if (query.Section is { } section) q = q.Where(l => l.Category.Section == section);

        var purposes = ParsePurposes(query.Purpose);
        if (purposes.Count > 0) q = q.Where(l => l.Purposes.Any(p => purposes.Contains(p.Purpose)));

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var slug = query.Category.Trim().ToLowerInvariant();
            q = q.Where(l => l.Category.Slug == slug);
        }
        if (query.SellerId is { } sellerId) q = q.Where(l => l.SellerId == sellerId);
        if (query.MinPrice is { } min) q = q.Where(l => l.Price >= min);
        if (query.MaxPrice is { } max) q = q.Where(l => l.Price <= max);
        if (query.Gender is { } gender) q = q.Where(l => l.Livestock != null && l.Livestock.Gender == gender);
        if (query.Teeth is { } teeth) q = q.Where(l => l.Livestock != null && l.Livestock.Teeth == teeth);
        if (query.MinWeightKg is { } minW) q = q.Where(l => l.Livestock != null && l.Livestock.LiveWeightKg >= minW);
        if (query.MaxWeightKg is { } maxW) q = q.Where(l => l.Livestock != null && l.Livestock.LiveWeightKg <= maxW);
        if (query.Shares == true) q = q.Where(l => l.Qurbani != null && l.Qurbani.TotalShares == 7);
        if (query.Bulk == true) q = q.Where(l => l.IsBulkAvailable);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var text = query.Q.Trim();
            q = q.Where(l => l.Title.Contains(text)
                || (l.Description != null && l.Description.Contains(text))
                || (l.Livestock != null && l.Livestock.Breed != null && l.Livestock.Breed.Contains(text))
                || (l.Crop != null && l.Crop.Variety != null && l.Crop.Variety.Contains(text)));
        }

        // Distance: from the buyer's position, or from the chosen city when only a city is given.
        (double Lat, double Lng)? origin = query.Lat is { } lat && query.Lng is { } lng ? (lat, lng) : null;
        if (origin is null && query.LocationId is { } locId)
        {
            var loc = await db.Locations.AsNoTracking().Where(x => x.Id == locId).Select(x => new { x.Latitude, x.Longitude }).FirstOrDefaultAsync(ct);
            if (loc is not null) origin = (loc.Latitude, loc.Longitude);
        }
        // Without a radius, a city filter means "only that city".
        if (query.LocationId is { } onlyCity && query.RadiusKm is null && query.Lat is null) q = q.Where(l => l.LocationId == onlyCity);

        // Equirectangular approximation – accurate enough for a few hundred km and translatable to SQL.
        var cosLat = origin is { } o0 ? Math.Cos(o0.Lat * Math.PI / 180) : 1;
        if (origin is { } o && query.RadiusKm is { } radius)
        {
            var maxDeg2 = Math.Pow(radius / KmPerDegree, 2);
            q = q.Where(l =>
                (l.Location.Latitude - o.Lat) * (l.Location.Latitude - o.Lat)
                + (l.Location.Longitude - o.Lng) * cosLat * ((l.Location.Longitude - o.Lng) * cosLat) <= maxDeg2);
        }

        q = (query.Sort, origin) switch
        {
            ("priceAsc", _) => q.OrderBy(l => l.Price == null).ThenBy(l => l.Price).ThenByDescending(l => l.Id),
            ("priceDesc", _) => q.OrderBy(l => l.Price == null).ThenByDescending(l => l.Price).ThenByDescending(l => l.Id),
            ("nearest", { } n) => q
                .OrderBy(l => (l.Location.Latitude - n.Lat) * (l.Location.Latitude - n.Lat)
                    + (l.Location.Longitude - n.Lng) * cosLat * ((l.Location.Longitude - n.Lng) * cosLat))
                .ThenByDescending(l => l.PublishedAtUtc).ThenByDescending(l => l.Id),
            _ => q.OrderByDescending(l => l.PublishedAtUtc).ThenByDescending(l => l.Id),
        };

        var total = await q.CountAsync(ct);
        var rows = await ProjectSummaries(q.Skip(query.Skip).Take(query.PageSize)).ToListAsync(ct);
        return new PagedResult<ListingSummaryDto>(rows.Select(r => r.ToDto(origin, now)).ToList(), query.Page, query.PageSize, total);
    }

    private static List<ListingPurpose> ParsePurposes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var result = new List<ListingPurpose>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<ListingPurpose>(part, ignoreCase: true, out var p) || !Enum.IsDefined(p))
                throw new BadRequestException(ErrorCodes.Validation, $"Unknown purpose '{part}'.");
            if (!result.Contains(p)) result.Add(p);
        }
        return result;
    }

    // ---- Read one -----------------------------------------------------------

    public async Task<ListingDetailDto> GetAsync(int id, int? viewerId, CancellationToken ct = default)
    {
        var l = await LoadFullAsync(id, ct);
        var isMine = viewerId == l.SellerId;
        // Buyers only see published, unexpired listings and sold ones (so shared links still open).
        var effective = l.EffectiveStatus(Now);
        var publicVisible = effective is ListingStatus.Active or ListingStatus.Sold;
        if (l.Status == ListingStatus.Removed || (!isMine && !publicVisible))
            throw new NotFoundException(ErrorCodes.ListingNotFound, "Listing not found.");

        if (!isMine && effective == ListingStatus.Active)
        {
            await db.Listings.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ViewCount, x => x.ViewCount + 1), ct);
            l.ViewCount++;
        }

        var isFavorite = viewerId is { } v && await db.Favorites.AnyAsync(f => f.UserId == v && f.ListingId == id, ct);
        return ToDetail(l, isMine, isFavorite);
    }

    // ---- Create / update ----------------------------------------------------

    public async Task<ListingDetailDto> CreateAsync(int sellerId, ListingInput input, CancellationToken ct = default)
    {
        var now = Now;
        var listing = new Listing { SellerId = sellerId, Status = ListingStatus.Draft, CreatedAtUtc = now };
        await ApplyAsync(listing, input, ct);
        listing.UpdatedAtUtc = now;
        db.Listings.Add(listing);
        await db.SaveChangesAsync(ct);
        return await GetOwnAsync(listing.Id, ct);
    }

    public async Task<ListingDetailDto> UpdateAsync(int sellerId, int id, ListingInput input, CancellationToken ct = default)
    {
        var listing = await LoadOwnedForEditAsync(sellerId, id, ct);
        await ApplyAsync(listing, input, ct);
        listing.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
        return await GetOwnAsync(id, ct);
    }

    /// <summary>
    /// Copies the input onto the listing, enforcing the category rules: purposes, detail
    /// blocks, units and fields must all fit the category. Nothing irrelevant is stored.
    /// </summary>
    private async Task ApplyAsync(Listing l, ListingInput input, CancellationToken ct)
    {
        var category = await db.Categories.Include(c => c.Purposes).FirstOrDefaultAsync(c => c.Id == input.CategoryId && c.IsActive, ct)
            ?? throw new NotFoundException(ErrorCodes.CategoryNotFound, "Category not found.");
        if (!await db.Locations.AnyAsync(x => x.Id == input.LocationId && x.IsActive, ct))
            throw new NotFoundException(ErrorCodes.LocationNotFound, "Location not found.");

        if (!ListingRules.PriceUnitsFor(category).Contains(input.PriceUnit))
            throw new BadRequestException(ErrorCodes.UnitNotAllowed, $"Price unit {input.PriceUnit} is not allowed for {category.NameEn}.");
        if (!ListingRules.QuantityUnitsFor(category.Section).Contains(input.QuantityUnit))
            throw new BadRequestException(ErrorCodes.UnitNotAllowed, $"Quantity unit {input.QuantityUnit} is not allowed for {category.NameEn}.");
        if (input.MinOrderQuantity > input.Quantity)
            throw new BadRequestException(ErrorCodes.Validation, "Minimum order cannot be more than the quantity on offer.");

        l.CategoryId = category.Id;
        l.LocationId = input.LocationId;
        l.Title = input.Title.Trim();
        l.Description = Clean(input.Description);
        l.AreaText = Clean(input.AreaText);
        l.Price = input.Price;
        l.PriceUnit = input.PriceUnit;
        l.IsNegotiable = input.IsNegotiable;
        l.Quantity = input.Quantity;
        l.QuantityUnit = input.QuantityUnit;
        l.MinOrderQuantity = input.MinOrderQuantity;
        l.IsBulkAvailable = input.IsBulkAvailable;
        l.DeliveryAvailable = input.DeliveryAvailable;

        switch (category.Section)
        {
            case ProductSection.Livestock:
                ApplyLivestock(l, input, category);
                break;
            case ProductSection.Crops:
                RejectIfSent(input.Purposes is { Count: > 0 } || input.Livestock is not null || input.Qurbani is not null
                    || input.MeatSupply is not null || input.Supply is not null, "Crop listings only take crop details.");
                SetPurposes(l, []);
                l.Livestock = null; l.Qurbani = null; l.MeatSupply = null; l.Supply = null;
                var crop = input.Crop ?? new CropInput();
                l.Crop ??= new CropDetails();
                l.Crop.Variety = Clean(crop.Variety);
                l.Crop.Grade = Clean(crop.Grade);
                l.Crop.HarvestDate = crop.HarvestDate;
                l.Crop.PickupAvailable = crop.PickupAvailable;
                break;
            case ProductSection.Supplies:
                RejectIfSent(input.Purposes is { Count: > 0 } || input.Livestock is not null || input.Qurbani is not null
                    || input.MeatSupply is not null || input.Crop is not null, "Supply listings only take supply details.");
                SetPurposes(l, []);
                l.Livestock = null; l.Qurbani = null; l.MeatSupply = null; l.Crop = null;
                var supply = input.Supply ?? new SupplyInput();
                l.Supply ??= new SupplyDetails();
                l.Supply.Brand = Clean(supply.Brand);
                l.Supply.Condition = supply.Condition;
                break;
        }
    }

    private void ApplyLivestock(Listing l, ListingInput input, Category category)
    {
        RejectIfSent(input.Crop is not null || input.Supply is not null, "Animal listings don't take crop or supply details.");

        var purposes = (input.Purposes ?? []).Distinct().ToList();
        if (purposes.Count == 0)
            throw new BadRequestException(ErrorCodes.PurposeRequired, "Choose at least one purpose (Qurbani, meat supply, dairy, breeding or general).");
        var allowed = category.Purposes.Select(p => p.Purpose).ToHashSet();
        var notAllowed = purposes.Where(p => !allowed.Contains(p)).ToList();
        if (notAllowed.Count > 0)
            throw new BadRequestException(ErrorCodes.PurposeNotAllowed, $"{string.Join(", ", notAllowed)} is not allowed for {category.NameEn}.");

        if (input.Quantity != decimal.Truncate(input.Quantity))
            throw new BadRequestException(ErrorCodes.Validation, "Number of animals must be a whole number.");

        var a = input.Livestock ?? new LivestockInput();
        if (a.MilkLitresPerDay is not null && (!category.HasMilkProduction || a.Gender == AnimalGender.Male))
            throw new BadRequestException(ErrorCodes.DetailsMismatch, "Milk yield only applies to female milk animals.");
        if (a.IsPregnant is not null && a.Gender != AnimalGender.Female)
            throw new BadRequestException(ErrorCodes.DetailsMismatch, "Pregnancy only applies to female animals.");

        // Purpose-specific blocks only when that purpose is ticked.
        var hasQurbani = purposes.Contains(ListingPurpose.Qurbani);
        var hasMeat = purposes.Contains(ListingPurpose.MeatSupply);
        RejectIfSent(input.Qurbani is not null && !hasQurbani, "Qurbani details need the Qurbani purpose.");
        RejectIfSent(input.MeatSupply is not null && !hasMeat, "Meat supply details need the meat supply purpose.");

        var shares = input.Qurbani?.TotalShares ?? 1;
        if (shares is not (1 or 7))
            throw new BadRequestException(ErrorCodes.SharesNotAllowed, "Qurbani shares must be 1 (whole animal) or 7.");
        if (shares == 7 && (!category.AllowsQurbaniShares || input.Quantity != 1))
            throw new BadRequestException(ErrorCodes.SharesNotAllowed, "Seven shares are only possible for one cow, buffalo or camel.");
        if (input.Qurbani?.PricePerShare is not null && shares != 7)
            throw new BadRequestException(ErrorCodes.SharesNotAllowed, "Price per share needs 7 shares.");
        if (input.PriceUnit == PriceUnit.PerShare && shares != 7)
            throw new BadRequestException(ErrorCodes.UnitNotAllowed, "Price per share needs 7 Qurbani shares.");

        SetPurposes(l, purposes);
        l.Crop = null;
        l.Supply = null;

        l.Livestock ??= new LivestockDetails();
        l.Livestock.Breed = Clean(a.Breed);
        l.Livestock.Gender = a.Gender;
        l.Livestock.AgeMonths = a.AgeMonths;
        l.Livestock.Teeth = a.Teeth;
        l.Livestock.LiveWeightKg = a.LiveWeightKg;
        l.Livestock.IsVaccinated = a.IsVaccinated;
        l.Livestock.IsPregnant = a.IsPregnant;
        l.Livestock.MilkLitresPerDay = a.MilkLitresPerDay;
        l.Livestock.HealthNotes = Clean(a.HealthNotes);

        if (hasQurbani)
        {
            l.Qurbani ??= new QurbaniDetails();
            l.Qurbani.TotalShares = shares;
            l.Qurbani.PricePerShare = input.Qurbani?.PricePerShare;
        }
        else l.Qurbani = null;

        if (hasMeat)
        {
            l.MeatSupply ??= new MeatSupplyDetails();
            l.MeatSupply.Frequency = input.MeatSupply?.Frequency ?? SupplyFrequency.OneTime;
            l.MeatSupply.PricePerKgLive = input.MeatSupply?.PricePerKgLive;
        }
        else l.MeatSupply = null;
    }

    private static void SetPurposes(Listing l, List<ListingPurpose> purposes)
    {
        foreach (var old in l.Purposes.Where(p => !purposes.Contains(p.Purpose)).ToList()) l.Purposes.Remove(old);
        foreach (var p in purposes.Where(p => l.Purposes.All(x => x.Purpose != p))) l.Purposes.Add(new ListingPurposeLink { Purpose = p });
    }

    private static void RejectIfSent(bool condition, string message)
    {
        if (condition) throw new BadRequestException(ErrorCodes.DetailsMismatch, message);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ---- Status changes -----------------------------------------------------

    public async Task<ListingDetailDto> PublishAsync(int sellerId, int id, CancellationToken ct = default)
    {
        var l = await LoadOwnedForEditAsync(sellerId, id, ct);
        var section = await db.Categories.Where(c => c.Id == l.CategoryId).Select(c => c.Section).FirstAsync(ct);
        // Buyers don't trust animal listings without a photo.
        if (section == ProductSection.Livestock && !await db.ListingMedia.AnyAsync(m => m.ListingId == id, ct))
            throw new BadRequestException(ErrorCodes.ImageRequired, "Add at least one photo of the animal before publishing.");

        var now = Now;
        l.Status = ListingStatus.Active;
        l.PublishedAtUtc = now; // publishing again (renewing) moves it back to the top
        l.ExpiresAtUtc = now + ListingRules.ListingLifetime;
        l.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return await GetOwnAsync(id, ct);
    }

    public async Task<ListingDetailDto> MarkSoldAsync(int sellerId, int id, CancellationToken ct = default)
    {
        var l = await LoadOwnedAsync(sellerId, id, ct);
        if (l.Status != ListingStatus.Active)
            throw new BadRequestException(ErrorCodes.ListingNotEditable, "Only a published listing can be marked as sold.");
        var now = Now;
        l.Status = ListingStatus.Sold;
        l.SoldAtUtc = now;
        l.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return await GetOwnAsync(id, ct);
    }

    /// <summary>Soft delete: the row stays for chats, reviews and reports that point to it.</summary>
    public async Task RemoveAsync(int sellerId, int id, CancellationToken ct = default)
    {
        var l = await LoadOwnedAsync(sellerId, id, ct);
        l.Status = ListingStatus.Removed;
        l.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<ListingSummaryDto>> GetMineAsync(int sellerId, PageQuery page, CancellationToken ct = default)
    {
        var q = db.Listings.AsNoTracking()
            .Where(l => l.SellerId == sellerId && l.Status != ListingStatus.Removed)
            .OrderByDescending(l => l.UpdatedAtUtc).ThenByDescending(l => l.Id);
        var total = await q.CountAsync(ct);
        var rows = await ProjectSummaries(q.Skip(page.Skip).Take(page.PageSize)).ToListAsync(ct);
        var now = Now;
        return new PagedResult<ListingSummaryDto>(rows.Select(r => r.ToDto(null, now)).ToList(), page.Page, page.PageSize, total);
    }

    // ---- Media --------------------------------------------------------------

    public async Task<MediaDto> AddMediaAsync(int sellerId, int id, IFormFile file, CancellationToken ct = default)
    {
        var l = await LoadOwnedForEditAsync(sellerId, id, ct);
        if (file.Length == 0 || file.Length > ListingRules.MaxImageBytes)
            throw new BadRequestException(ErrorCodes.InvalidImage, "Photo must be smaller than 5 MB.");
        var count = await db.ListingMedia.CountAsync(m => m.ListingId == id, ct);
        if (count >= ListingRules.MaxMediaPerListing)
            throw new BadRequestException(ErrorCodes.TooManyMedia, $"A listing can have at most {ListingRules.MaxMediaPerListing} photos.");

        await using var stream = file.OpenReadStream();
        var head = new byte[12];
        var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        var type = ImageSniffer.Detect(head.AsSpan(0, read))
            ?? throw new BadRequestException(ErrorCodes.InvalidImage, "Only JPEG, PNG or WebP photos are accepted.");
        stream.Position = 0;

        var path = $"listings/{l.Id}/{Guid.NewGuid():N}{type.Extension}";
        await media.SaveAsync(path, stream, ct);

        var maxOrder = await db.ListingMedia.Where(m => m.ListingId == id).MaxAsync(m => (int?)m.SortOrder, ct) ?? -1;
        var item = new ListingMedia
        {
            ListingId = id, Kind = MediaKind.Image, Path = path, ContentType = type.ContentType,
            SizeBytes = file.Length, SortOrder = maxOrder + 1, CreatedAtUtc = Now,
        };
        db.ListingMedia.Add(item);
        l.UpdatedAtUtc = Now;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            media.Delete(path); // don't leave orphan files behind
            throw;
        }
        return item.ToDto();
    }

    public async Task DeleteMediaAsync(int sellerId, int id, int mediaId, CancellationToken ct = default)
    {
        await LoadOwnedForEditAsync(sellerId, id, ct);
        var item = await db.ListingMedia.FirstOrDefaultAsync(m => m.Id == mediaId && m.ListingId == id, ct)
            ?? throw new NotFoundException(ErrorCodes.MediaNotFound, "Photo not found.");
        db.ListingMedia.Remove(item);
        await db.SaveChangesAsync(ct);
        media.Delete(item.Path);
    }

    // ---- Favorites ----------------------------------------------------------

    public async Task AddFavoriteAsync(int userId, int listingId, CancellationToken ct = default)
    {
        if (!await db.Listings.AnyAsync(l => l.Id == listingId && l.Status == ListingStatus.Active, ct))
            throw new NotFoundException(ErrorCodes.ListingNotFound, "Listing not found.");
        if (await db.Favorites.AnyAsync(f => f.UserId == userId && f.ListingId == listingId, ct)) return; // idempotent
        db.Favorites.Add(new Favorite { UserId = userId, ListingId = listingId, CreatedAtUtc = Now });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveFavoriteAsync(int userId, int listingId, CancellationToken ct = default) =>
        await db.Favorites.Where(f => f.UserId == userId && f.ListingId == listingId).ExecuteDeleteAsync(ct);

    public async Task<PagedResult<ListingSummaryDto>> GetFavoritesAsync(int userId, PageQuery page, CancellationToken ct = default)
    {
        var q = db.Favorites.AsNoTracking()
            .Where(f => f.UserId == userId && f.Listing.Status != ListingStatus.Removed)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Select(f => f.Listing);
        var total = await q.CountAsync(ct);
        var rows = await ProjectSummaries(q.Skip(page.Skip).Take(page.PageSize)).ToListAsync(ct);
        var now = Now;
        return new PagedResult<ListingSummaryDto>(rows.Select(r => r.ToDto(null, now)).ToList(), page.Page, page.PageSize, total);
    }

    // ---- Helpers --------------------------------------------------------------

    private async Task<Listing> LoadFullAsync(int id, CancellationToken ct) =>
        await db.Listings
            .Include(l => l.Category).ThenInclude(c => c.Purposes)
            .Include(l => l.Location)
            .Include(l => l.Seller).ThenInclude(s => s.Profile)
            .Include(l => l.Purposes)
            .Include(l => l.Media)
            .Include(l => l.Livestock)
            .Include(l => l.Qurbani)
            .Include(l => l.MeatSupply)
            .Include(l => l.Crop)
            .Include(l => l.Supply)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id, ct)
        ?? throw new NotFoundException(ErrorCodes.ListingNotFound, "Listing not found.");

    private async Task<ListingDetailDto> GetOwnAsync(int id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        return ToDetail(await LoadFullAsync(id, ct), isMine: true, isFavorite: false);
    }

    private async Task<Listing> LoadOwnedAsync(int sellerId, int id, CancellationToken ct)
    {
        var l = await db.Listings
            .Include(x => x.Purposes).Include(x => x.Livestock).Include(x => x.Qurbani)
            .Include(x => x.MeatSupply).Include(x => x.Crop).Include(x => x.Supply)
            .FirstOrDefaultAsync(x => x.Id == id && x.Status != ListingStatus.Removed, ct)
            ?? throw new NotFoundException(ErrorCodes.ListingNotFound, "Listing not found.");
        if (l.SellerId != sellerId) throw new ForbiddenException(ErrorCodes.NotOwner, "This is not your listing.");
        return l;
    }

    private async Task<Listing> LoadOwnedForEditAsync(int sellerId, int id, CancellationToken ct)
    {
        var l = await LoadOwnedAsync(sellerId, id, ct);
        if (l.Status is ListingStatus.Sold or ListingStatus.Hidden)
            throw new BadRequestException(ErrorCodes.ListingNotEditable, "A sold or hidden listing can't be changed.");
        return l;
    }

    private ListingDetailDto ToDetail(Listing l, bool isMine, bool isFavorite)
    {
        var p = l.Seller.Profile;
        return new ListingDetailDto(
            l.Id, l.Title, l.Description, l.AreaText, l.Category.Section, l.Category.ToDto(), l.Location.ToDto(),
            l.Purposes.Select(x => x.Purpose).Order().ToList(),
            l.Price, l.PriceUnit, l.IsNegotiable, l.Quantity, l.QuantityUnit, l.MinOrderQuantity,
            l.IsBulkAvailable, l.DeliveryAvailable,
            l.Media.OrderBy(m => m.SortOrder).Select(m => m.ToDto()).ToList(),
            l.Livestock is { } a ? new LivestockDto(a.Breed, a.Gender, a.AgeMonths, a.Teeth, a.LiveWeightKg, a.IsVaccinated, a.IsPregnant, a.MilkLitresPerDay, a.HealthNotes) : null,
            l.Qurbani is { } q ? new QurbaniDto(q.TotalShares, q.PricePerShare) : null,
            l.MeatSupply is { } m ? new MeatSupplyDto(m.Frequency, m.PricePerKgLive) : null,
            l.Crop is { } c ? new CropDto(c.Variety, c.Grade, c.HarvestDate, c.PickupAvailable) : null,
            l.Supply is { } s ? new SupplyDto(s.Brand, s.Condition) : null,
            l.EffectiveStatus(Now), l.ViewCount, l.CreatedAtUtc, l.PublishedAtUtc, l.ExpiresAtUtc,
            l.Seller.ToSummary(),
            p is { ShowPhoneNumber: true } ? PhoneNumber.ToLocal(l.Seller.PhoneNumber) : null,
            isMine, isFavorite);
    }

    // One SQL query per page: only the columns a feed card needs.
    private static IQueryable<SummaryRow> ProjectSummaries(IQueryable<Listing> q) => q.Select(l => new SummaryRow
    {
        Id = l.Id, Title = l.Title, Section = l.Category.Section, CategorySlug = l.Category.Slug, CategoryNameUr = l.Category.NameUr,
        Purposes = l.Purposes.Select(p => p.Purpose).ToList(),
        Price = l.Price, PriceUnit = l.PriceUnit, IsNegotiable = l.IsNegotiable,
        Quantity = l.Quantity, QuantityUnit = l.QuantityUnit, IsBulkAvailable = l.IsBulkAvailable,
        LocationNameUr = l.Location.NameUr, Lat = l.Location.Latitude, Lng = l.Location.Longitude,
        ThumbnailPath = l.Media.OrderBy(m => m.SortOrder).Select(m => m.Path).FirstOrDefault(),
        Breed = l.Livestock != null ? l.Livestock.Breed : null,
        Gender = l.Livestock != null ? l.Livestock.Gender : null,
        Teeth = l.Livestock != null ? l.Livestock.Teeth : null,
        AgeMonths = l.Livestock != null ? l.Livestock.AgeMonths : null,
        LiveWeightKg = l.Livestock != null ? l.Livestock.LiveWeightKg : null,
        Milk = l.Livestock != null ? l.Livestock.MilkLitresPerDay : null,
        QurbaniShares = l.Qurbani != null ? l.Qurbani.TotalShares : null,
        Variety = l.Crop != null ? l.Crop.Variety : null,
        Status = l.Status, ExpiresAtUtc = l.ExpiresAtUtc, PublishedAtUtc = l.PublishedAtUtc,
        SellerId = l.SellerId, SellerName = l.Seller.FullName, SellerVerification = l.Seller.Verification,
        SellerPhoneVerified = l.Seller.PhoneVerified,
        SellerRating = l.Seller.Profile != null ? l.Seller.Profile.RatingAverage : 0,
        SellerRatingCount = l.Seller.Profile != null ? l.Seller.Profile.RatingCount : 0,
    });

    private sealed class SummaryRow
    {
        public int Id { get; init; }
        public string Title { get; init; } = "";
        public ProductSection Section { get; init; }
        public string CategorySlug { get; init; } = "";
        public string CategoryNameUr { get; init; } = "";
        public List<ListingPurpose> Purposes { get; init; } = [];
        public long? Price { get; init; }
        public PriceUnit PriceUnit { get; init; }
        public bool IsNegotiable { get; init; }
        public decimal Quantity { get; init; }
        public QuantityUnit QuantityUnit { get; init; }
        public bool IsBulkAvailable { get; init; }
        public string LocationNameUr { get; init; } = "";
        public double Lat { get; init; }
        public double Lng { get; init; }
        public string? ThumbnailPath { get; init; }
        public string? Breed { get; init; }
        public AnimalGender? Gender { get; init; }
        public TeethStage? Teeth { get; init; }
        public int? AgeMonths { get; init; }
        public int? LiveWeightKg { get; init; }
        public decimal? Milk { get; init; }
        public int? QurbaniShares { get; init; }
        public string? Variety { get; init; }
        public ListingStatus Status { get; init; }
        public DateTime? ExpiresAtUtc { get; init; }
        public DateTime? PublishedAtUtc { get; init; }
        public int SellerId { get; init; }
        public string SellerName { get; init; } = "";
        public VerificationLevel SellerVerification { get; init; }
        public bool SellerPhoneVerified { get; init; }
        public double SellerRating { get; init; }
        public int SellerRatingCount { get; init; }

        public ListingSummaryDto ToDto((double Lat, double Lng)? origin, DateTime now)
        {
            double? distance = origin is { } o ? Math.Round(Geo.DistanceKm(o.Lat, o.Lng, Lat, Lng), 1) : null;
            var status = Status == ListingStatus.Active && ExpiresAtUtc <= now ? ListingStatus.Expired : Status;
            return new ListingSummaryDto(
                Id, Title, Section, CategorySlug, CategoryNameUr, Purposes.Order().ToList(),
                Price, PriceUnit, IsNegotiable, Quantity, QuantityUnit, IsBulkAvailable,
                LocationNameUr, distance, ThumbnailPath is null ? null : LocalMediaStorage.UrlFor(ThumbnailPath),
                Breed, Gender, Teeth, AgeMonths, LiveWeightKg, Milk, QurbaniShares, Variety,
                status, PublishedAtUtc,
                new SellerSummaryDto(SellerId, SellerName, SellerVerification.ToString(), SellerPhoneVerified, Math.Round(SellerRating, 1), SellerRatingCount));
        }
    }
}

public static class Geo
{
    /// <summary>Great-circle distance in km (haversine).</summary>
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371;
        double ToRad(double d) => d * Math.PI / 180;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}
