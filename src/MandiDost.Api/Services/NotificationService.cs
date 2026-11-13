using MandiDost.Api.Common;
using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MandiDost.Api.Services;

public interface INotificationService
{
    // Called by other services after something happens. They never throw: a failed alert must not break the action itself.
    Task NewListingPublishedAsync(int listingId, CancellationToken ct = default);
    Task NewRequestPostedAsync(int requestId, CancellationToken ct = default);
    Task OfferReceivedAsync(int offerId, CancellationToken ct = default);
    Task OfferDecidedAsync(int offerId, CancellationToken ct = default);
    Task BookingRequestedAsync(int bookingId, CancellationToken ct = default);
    Task BookingDecidedAsync(int bookingId, CancellationToken ct = default);

    // Reading and settings
    Task<NotificationListDto> ListAsync(int userId, NotificationQuery query, CancellationToken ct = default);
    Task<int> UnreadCountAsync(int userId, CancellationToken ct = default);
    Task MarkReadAsync(int userId, int id, CancellationToken ct = default);
    Task MarkAllReadAsync(int userId, CancellationToken ct = default);
    Task<NotificationSettingsDto> GetSettingsAsync(int userId, CancellationToken ct = default);
    Task<NotificationSettingsDto> UpdateSettingsAsync(int userId, NotificationSettingsInput input, CancellationToken ct = default);
}

/// <summary>
/// In-app notifications. "Nearby" alerts go to users whose saved city is within their chosen radius, who want that kind
/// of alert and the animal or crop in question. Nobody gets more than <see cref="DailyNearbyLimit"/> nearby alerts a day,
/// and the same event never alerts the same person twice.
/// </summary>
public class NotificationService(AppDbContext db, TimeProvider clock, ILogger<NotificationService> log) : INotificationService
{
    public const int DailyNearbyLimit = 20;
    private const double KmPerDegree = 111.32;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---- Events ---------------------------------------------------------------

    public Task NewListingPublishedAsync(int listingId, CancellationToken ct = default) => Safe(async () =>
    {
        var l = await db.Listings.AsNoTracking().Where(x => x.Id == listingId && x.Status == ListingStatus.Active)
            .Select(x => new { x.Id, x.SellerId, x.Title, x.CategoryId, Place = x.Location.NameUr, x.Location.Latitude, x.Location.Longitude })
            .FirstOrDefaultAsync(ct);
        if (l is null) return;
        await NotifyNearbyAsync(NotificationType.NearbyListing, $"L{l.Id}", l.SellerId, l.CategoryId, l.Latitude, l.Longitude,
            n => { n.ListingId = l.Id; n.Subject = Cut(l.Title); n.Place = l.Place; }, wantsKind: s => s.WantListings, ct);
    });

    public Task NewRequestPostedAsync(int requestId, CancellationToken ct = default) => Safe(async () =>
    {
        var r = await db.PurchaseRequests.AsNoTracking().Where(x => x.Id == requestId && x.Status == RequestStatus.Open)
            .Select(x => new { x.Id, x.BuyerId, x.Title, x.CategoryId, Place = x.Location.NameUr, x.Location.Latitude, x.Location.Longitude })
            .FirstOrDefaultAsync(ct);
        if (r is null) return;
        await NotifyNearbyAsync(NotificationType.NearbyRequest, $"R{r.Id}", r.BuyerId, r.CategoryId, r.Latitude, r.Longitude,
            n => { n.RequestId = r.Id; n.Subject = Cut(r.Title); n.Place = r.Place; }, wantsKind: s => s.WantRequests, ct);
    });

    public Task OfferReceivedAsync(int offerId, CancellationToken ct = default) => Safe(async () =>
    {
        var o = await db.RequestOffers.AsNoTracking().Where(x => x.Id == offerId)
            .Select(x => new { x.Id, x.Price, Request = new { x.PurchaseRequest.Id, x.PurchaseRequest.BuyerId, x.PurchaseRequest.Title } })
            .FirstOrDefaultAsync(ct);
        if (o is null) return;
        await AddAsync(o.Request.BuyerId, NotificationType.OfferReceived, $"O{o.Id}",
            n => { n.RequestId = o.Request.Id; n.Subject = Cut(o.Request.Title); n.Amount = o.Price; }, ct);
    });

    public Task OfferDecidedAsync(int offerId, CancellationToken ct = default) => Safe(async () =>
    {
        var o = await db.RequestOffers.AsNoTracking().Where(x => x.Id == offerId)
            .Select(x => new { x.Id, x.SellerId, x.Status, x.Price, Request = new { x.PurchaseRequest.Id, x.PurchaseRequest.Title } })
            .FirstOrDefaultAsync(ct);
        if (o is null || o.Status is not (OfferStatus.Accepted or OfferStatus.Declined)) return;
        var type = o.Status == OfferStatus.Accepted ? NotificationType.OfferAccepted : NotificationType.OfferDeclined;
        await AddAsync(o.SellerId, type, $"OD{o.Id}:{o.Status}",
            n => { n.RequestId = o.Request.Id; n.Subject = Cut(o.Request.Title); n.Amount = o.Price; }, ct);
    });

    public Task BookingRequestedAsync(int bookingId, CancellationToken ct = default) => Safe(async () =>
    {
        var b = await db.QurbaniBookings.AsNoTracking().Where(x => x.Id == bookingId)
            .Select(x => new { x.Id, x.Shares, Listing = new { x.Listing.Id, x.Listing.SellerId, x.Listing.Title } })
            .FirstOrDefaultAsync(ct);
        if (b is null) return;
        await AddAsync(b.Listing.SellerId, NotificationType.BookingRequested, $"BK{b.Id}",
            n => { n.ListingId = b.Listing.Id; n.Subject = Cut(b.Listing.Title); n.Count = b.Shares; }, ct);
    });

    public Task BookingDecidedAsync(int bookingId, CancellationToken ct = default) => Safe(async () =>
    {
        var b = await db.QurbaniBookings.AsNoTracking().Where(x => x.Id == bookingId)
            .Select(x => new { x.Id, x.BuyerId, x.Shares, x.Status, Listing = new { x.Listing.Id, x.Listing.Title } })
            .FirstOrDefaultAsync(ct);
        if (b is null || b.Status is not (BookingStatus.Confirmed or BookingStatus.Declined)) return;
        var type = b.Status == BookingStatus.Confirmed ? NotificationType.BookingConfirmed : NotificationType.BookingDeclined;
        await AddAsync(b.BuyerId, type, $"BKD{b.Id}:{b.Status}",
            n => { n.ListingId = b.Listing.Id; n.Subject = Cut(b.Listing.Title); n.Count = b.Shares; }, ct);
    });

    // ---- Matching ----------------------------------------------------------------

    private async Task NotifyNearbyAsync(NotificationType type, string key, int actorId, int categoryId, double lat, double lng,
        Action<Notification> fill, Func<NotificationSettings, bool> wantsKind, CancellationToken ct)
    {
        // Rough box first (done in SQL), exact distance after.
        var maxKm = NotificationSettings.MaxRadiusKm;
        var dLat = maxKm / KmPerDegree;
        var dLng = maxKm / (KmPerDegree * Math.Max(0.1, Math.Cos(lat * Math.PI / 180)));

        var candidates = await db.UserProfiles.AsNoTracking()
            .Where(p => p.LocationId != null && p.UserId != actorId && p.User.IsActive
                && p.Location!.Latitude >= lat - dLat && p.Location.Latitude <= lat + dLat
                && p.Location.Longitude >= lng - dLng && p.Location.Longitude <= lng + dLng)
            .Select(p => new
            {
                p.UserId, p.Location!.Latitude, p.Location.Longitude,
                S = db.NotificationSettings.Where(s => s.UserId == p.UserId).FirstOrDefault(),
            })
            .Take(2000)
            .ToListAsync(ct);

        var chosen = new List<(int UserId, double Km)>();
        foreach (var c in candidates)
        {
            var s = c.S ?? new NotificationSettings { UserId = c.UserId };
            if (!s.Enabled || !wantsKind(s)) continue;
            var km = Geo.DistanceKm(lat, lng, c.Latitude, c.Longitude);
            if (km > s.RadiusKm) continue;
            var interests = ParseIds(s.InterestCategoryIds);
            if (interests.Count > 0 && !interests.Contains(categoryId)) continue;
            chosen.Add((c.UserId, km));
        }
        if (chosen.Count == 0) return;

        var ids = chosen.Select(c => c.UserId).ToList();
        var since = Now.AddHours(-24);
        var recent = await db.Notifications.AsNoTracking()
            .Where(n => ids.Contains(n.UserId) && n.CreatedAtUtc >= since
                && (n.Type == NotificationType.NearbyListing || n.Type == NotificationType.NearbyRequest))
            .GroupBy(n => n.UserId).Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.UserId, g => g.Count, ct);
        var already = (await db.Notifications.AsNoTracking().Where(n => ids.Contains(n.UserId) && n.DedupeKey == key)
            .Select(n => n.UserId).ToListAsync(ct)).ToHashSet();

        var now = Now;
        foreach (var (userId, km) in chosen)
        {
            if (already.Contains(userId) || recent.GetValueOrDefault(userId) >= DailyNearbyLimit) continue;
            var n = new Notification { UserId = userId, Type = type, DedupeKey = key, CreatedAtUtc = now, DistanceKm = Math.Round(km, 1) };
            fill(n);
            db.Notifications.Add(n);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>One personal notification (offer, booking). Skipped if the same event was already recorded.</summary>
    private async Task AddAsync(int userId, NotificationType type, string key, Action<Notification> fill, CancellationToken ct)
    {
        if (await db.Notifications.AnyAsync(n => n.UserId == userId && n.DedupeKey == key, ct)) return;
        var n = new Notification { UserId = userId, Type = type, DedupeKey = key, CreatedAtUtc = Now };
        fill(n);
        db.Notifications.Add(n);
        await db.SaveChangesAsync(ct);
    }

    private async Task Safe(Func<Task> work)
    {
        try { await work(); }
        catch (Exception e)
        {
            // The listing, offer or booking is already saved. A failed alert is only logged.
            log.LogWarning(e, "Could not create notifications");
            db.ChangeTracker.Clear();
        }
    }

    // ---- Reading ---------------------------------------------------------------------

    public async Task<NotificationListDto> ListAsync(int userId, NotificationQuery query, CancellationToken ct = default)
    {
        var q = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (query.AfterId is { } after) q = q.Where(n => n.Id > after);
        if (query.BeforeId is { } before) q = q.Where(n => n.Id < before);
        var items = await q.OrderByDescending(n => n.Id).Take(query.PageSize).ToListAsync(ct);
        return new NotificationListDto(items.Select(ToDto).ToList(), await UnreadCountAsync(userId, ct));
    }

    public Task<int> UnreadCountAsync(int userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, ct);

    public async Task MarkReadAsync(int userId, int id, CancellationToken ct = default)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new NotFoundException(ErrorCodes.NotificationNotFound, "Notification not found.");
        if (n.ReadAtUtc is null)
        {
            n.ReadAtUtc = Now;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        var now = Now;
        var unread = await db.Notifications.Where(n => n.UserId == userId && n.ReadAtUtc == null).ToListAsync(ct);
        foreach (var n in unread) n.ReadAtUtc = now;
        if (unread.Count > 0) await db.SaveChangesAsync(ct);
    }

    // ---- Settings ------------------------------------------------------------------

    public async Task<NotificationSettingsDto> GetSettingsAsync(int userId, CancellationToken ct = default)
    {
        var s = await db.NotificationSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct)
            ?? new NotificationSettings { UserId = userId };
        return ToDto(s);
    }

    public async Task<NotificationSettingsDto> UpdateSettingsAsync(int userId, NotificationSettingsInput input, CancellationToken ct = default)
    {
        var valid = await db.Categories.Where(c => input.CategoryIds.Contains(c.Id)).Select(c => c.Id).ToListAsync(ct);
        var s = await db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (s is null)
        {
            s = new NotificationSettings { UserId = userId };
            db.NotificationSettings.Add(s);
        }
        s.Enabled = input.Enabled;
        s.WantListings = input.WantListings;
        s.WantRequests = input.WantRequests;
        s.RadiusKm = input.RadiusKm;
        s.InterestCategoryIds = valid.Count == 0 ? null : string.Join(',', valid.Distinct().Order());
        s.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    // ---- Helpers -------------------------------------------------------------------

    private static string Cut(string s) => s.Length <= 160 ? s : s[..159] + "…";

    private static List<int> ParseIds(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var i) ? i : 0).Where(i => i > 0).ToList();

    private static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.Type, n.ListingId, n.RequestId, n.Subject, n.Place, n.DistanceKm, n.Count, n.Amount, n.CreatedAtUtc, n.ReadAtUtc is not null);

    private static NotificationSettingsDto ToDto(NotificationSettings s) =>
        new(s.Enabled, s.WantListings, s.WantRequests, s.RadiusKm, ParseIds(s.InterestCategoryIds));
}
