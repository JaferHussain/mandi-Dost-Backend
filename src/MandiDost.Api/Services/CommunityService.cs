using System.Data;
using MandiDost.Api.Common;
using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MandiDost.Api.Services;

public interface ICommunityService
{
    // Qurbani shares
    Task<SharesDto> GetSharesAsync(int listingId, int? viewerId, CancellationToken ct = default);
    Task<BookingDto> BookSharesAsync(int buyerId, int listingId, BookSharesInput input, CancellationToken ct = default);
    Task<BookingDto> DecideBookingAsync(int sellerId, int bookingId, bool confirm, CancellationToken ct = default);
    Task<BookingDto> CancelBookingAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<BookingDto>> MyBookingsAsync(int buyerId, CancellationToken ct = default);

    // People
    Task<PublicProfileDto> GetProfileAsync(int userId, int? viewerId, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewDto>> GetReviewsAsync(int sellerId, CancellationToken ct = default);
    Task<ReviewDto> ReviewAsync(int reviewerId, int sellerId, ReviewInput input, CancellationToken ct = default);

    // Reports
    Task ReportAsync(int reporterId, int listingId, ReportInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ReportDto>> OpenReportsAsync(CancellationToken ct = default);
    Task<ReportDto> ResolveReportAsync(int adminId, int reportId, ResolveReportInput input, CancellationToken ct = default);
}

public class CommunityService(AppDbContext db, IBusinessService businesses, TimeProvider clock) : ICommunityService
{
    /// <summary>A listing with this many separate open reports is hidden until a moderator looks at it.</summary>
    public const int AutoHideReportCount = 3;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---- Qurbani shares -----------------------------------------------------------

    public async Task<SharesDto> GetSharesAsync(int listingId, int? viewerId, CancellationToken ct = default)
    {
        var l = await db.Listings.AsNoTracking().Include(x => x.Qurbani)
            .FirstOrDefaultAsync(x => x.Id == listingId && x.Status != ListingStatus.Removed, ct)
            ?? throw new NotFoundException(ErrorCodes.ListingNotFound, "Listing not found.");
        if (l.Qurbani is not { TotalShares: 7 }) throw new BadRequestException(ErrorCodes.NoShares, "This animal is not sold in shares.");

        var bookings = await BookingQuery().Where(b => b.ListingId == listingId).OrderBy(b => b.CreatedAtUtc).ToListAsync(ct);
        var confirmed = bookings.Where(b => b.Status == BookingStatus.Confirmed).Sum(b => b.Shares);
        var requested = bookings.Where(b => b.Status == BookingStatus.Requested).Sum(b => b.Shares);
        var isSeller = viewerId == l.SellerId;
        return new SharesDto(
            7, confirmed, requested, Math.Max(0, 7 - confirmed - requested), l.Qurbani.PricePerShare,
            bookings.Where(b => b.BuyerId == viewerId).Select(b => ToDto(b, showBuyerPhone: false)).ToList(),
            isSeller ? bookings.Select(b => ToDto(b, showBuyerPhone: true)).ToList() : null);
    }

    public async Task<BookingDto> BookSharesAsync(int buyerId, int listingId, BookSharesInput input, CancellationToken ct = default)
    {
        // Serializable so two buyers can't grab the last share at the same moment. The SQL Server
        // connection retries on failure (including deadlocks), so the transaction runs inside the retry strategy.
        var strategy = db.Database.CreateExecutionStrategy();
        var bookingId = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var l = await db.Listings.Include(x => x.Qurbani).FirstOrDefaultAsync(x => x.Id == listingId, ct)
                ?? throw new NotFoundException(ErrorCodes.ListingNotFound, "Listing not found.");
            if (l.Qurbani is not { TotalShares: 7 }) throw new BadRequestException(ErrorCodes.NoShares, "This animal is not sold in shares.");
            if (l.Status != ListingStatus.Active || l.ExpiresAtUtc <= Now) throw new BadRequestException(ErrorCodes.ListingNotEditable, "This listing is not open.");
            if (l.SellerId == buyerId) throw new BadRequestException(ErrorCodes.OwnRequest, "You can't book your own animal.");

            var taken = await db.QurbaniBookings
                .Where(b => b.ListingId == listingId && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed))
                .SumAsync(b => (int?)b.Shares, ct) ?? 0;
            if (input.Shares > 7 - taken)
                throw new BadRequestException(ErrorCodes.NotEnoughShares, $"Only {Math.Max(0, 7 - taken)} shares are left.");

            var now = Now;
            var booking = new QurbaniBooking
            {
                ListingId = listingId, BuyerId = buyerId, Shares = input.Shares,
                Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
                Status = BookingStatus.Requested, CreatedAtUtc = now, UpdatedAtUtc = now,
            };
            db.QurbaniBookings.Add(booking);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return booking.Id;
        });
        return ToDto(await BookingQuery().FirstAsync(b => b.Id == bookingId, ct), showBuyerPhone: false);
    }

    public async Task<BookingDto> DecideBookingAsync(int sellerId, int bookingId, bool confirm, CancellationToken ct = default)
    {
        var b = await db.QurbaniBookings.Include(x => x.Listing).FirstOrDefaultAsync(x => x.Id == bookingId, ct)
            ?? throw new NotFoundException(ErrorCodes.BookingNotFound, "Booking not found.");
        if (b.Listing.SellerId != sellerId) throw new ForbiddenException(ErrorCodes.NotOwner, "This is not your animal.");
        if (b.Status != BookingStatus.Requested) throw new BadRequestException(ErrorCodes.Validation, "This booking was already answered.");
        b.Status = confirm ? BookingStatus.Confirmed : BookingStatus.Declined;
        b.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
        return ToDto(await BookingQuery().FirstAsync(x => x.Id == bookingId, ct), showBuyerPhone: true);
    }

    /// <summary>Buyer or seller can cancel; the shares become free again.</summary>
    public async Task<BookingDto> CancelBookingAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var b = await db.QurbaniBookings.Include(x => x.Listing).FirstOrDefaultAsync(x => x.Id == bookingId, ct)
            ?? throw new NotFoundException(ErrorCodes.BookingNotFound, "Booking not found.");
        if (b.BuyerId != userId && b.Listing.SellerId != userId) throw new ForbiddenException(ErrorCodes.NotOwner, "This is not your booking.");
        if (b.Status is BookingStatus.Requested or BookingStatus.Confirmed)
        {
            b.Status = BookingStatus.Cancelled;
            b.UpdatedAtUtc = Now;
            await db.SaveChangesAsync(ct);
        }
        return ToDto(await BookingQuery().FirstAsync(x => x.Id == bookingId, ct), showBuyerPhone: b.Listing.SellerId == userId);
    }

    public async Task<IReadOnlyList<BookingDto>> MyBookingsAsync(int buyerId, CancellationToken ct = default) =>
        (await BookingQuery().Where(b => b.BuyerId == buyerId).OrderByDescending(b => b.UpdatedAtUtc).Take(100).ToListAsync(ct))
            .Select(b => ToDto(b, showBuyerPhone: false)).ToList();

    private IQueryable<QurbaniBooking> BookingQuery() =>
        db.QurbaniBookings.AsNoTracking().Include(b => b.Listing).Include(b => b.Buyer).ThenInclude(u => u.Profile);

    private static BookingDto ToDto(QurbaniBooking b, bool showBuyerPhone) => new(
        b.Id, b.ListingId, b.Listing.Title, b.Shares, b.Status, b.Note, b.BuyerId, b.Buyer.FullName,
        showBuyerPhone && b.Buyer.Profile is not { ShowPhoneNumber: false } ? PhoneNumber.ToLocal(b.Buyer.PhoneNumber) : null,
        b.CreatedAtUtc, b.UpdatedAtUtc);

    // ---- People -------------------------------------------------------------------------

    public async Task<PublicProfileDto> GetProfileAsync(int userId, int? viewerId, CancellationToken ct = default)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Profile).ThenInclude(p => p!.Location)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, ct)
            ?? throw new NotFoundException(ErrorCodes.UserNotFound, "User not found.");
        var now = Now;
        var active = await db.Listings.CountAsync(l => l.SellerId == userId && l.Status == ListingStatus.Active && l.ExpiresAtUtc > now, ct);
        var sold = await db.Listings.CountAsync(l => l.SellerId == userId && l.Status == ListingStatus.Sold, ct);
        var business = await businesses.GetMineAsync(userId, ct);
        var canReview = viewerId is { } v && v != userId && await HasDealtWithAsync(v, userId, ct);
        var p = u.Profile;
        return new PublicProfileDto(
            u.Id, u.FullName, u.Verification.ToString(), u.PhoneVerified, p?.Bio, p?.Location?.ToDto(),
            p is { ShowPhoneNumber: true } ? PhoneNumber.ToLocal(u.PhoneNumber) : null,
            business, active, sold, Math.Round(p?.RatingAverage ?? 0, 1), p?.RatingCount ?? 0, u.CreatedAtUtc, canReview);
    }

    public async Task<IReadOnlyList<ReviewDto>> GetReviewsAsync(int sellerId, CancellationToken ct = default) =>
        await db.Reviews.AsNoTracking().Where(r => r.SellerId == sellerId && !r.IsHidden)
            .OrderByDescending(r => r.CreatedAtUtc).Take(50)
            .Select(r => new ReviewDto(r.Id, r.Rating, r.Text, r.ReviewerId, r.Reviewer.FullName, r.ListingId, r.CreatedAtUtc))
            .ToListAsync(ct);

    /// <summary>
    /// One review per buyer per seller (writing again replaces it). Only people who actually
    /// dealt with the seller can review: a chat with replies, an accepted offer or a confirmed share booking.
    /// </summary>
    public async Task<ReviewDto> ReviewAsync(int reviewerId, int sellerId, ReviewInput input, CancellationToken ct = default)
    {
        if (reviewerId == sellerId) throw new BadRequestException(ErrorCodes.ReviewNotAllowed, "You can't review yourself.");
        if (!await db.Users.AnyAsync(u => u.Id == sellerId && u.IsActive, ct)) throw new NotFoundException(ErrorCodes.UserNotFound, "User not found.");
        if (!await HasDealtWithAsync(reviewerId, sellerId, ct))
            throw new BadRequestException(ErrorCodes.ReviewNotAllowed, "You can review a seller after you have dealt with them.");

        var now = Now;
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.ReviewerId == reviewerId && r.SellerId == sellerId, ct);
        if (review is null)
        {
            review = new Review { ReviewerId = reviewerId, SellerId = sellerId };
            db.Reviews.Add(review);
        }
        review.Rating = input.Rating;
        review.Text = string.IsNullOrWhiteSpace(input.Text) ? null : input.Text.Trim();
        review.ListingId = input.ListingId;
        review.CreatedAtUtc = now;
        await db.SaveChangesAsync(ct);

        // Keep the profile's average up to date (shown on cards and the seller page)
        var stats = await db.Reviews.Where(r => r.SellerId == sellerId && !r.IsHidden)
            .GroupBy(_ => 1).Select(g => new { Avg = g.Average(r => (double)r.Rating), Count = g.Count() }).FirstAsync(ct);
        await db.UserProfiles.Where(p => p.UserId == sellerId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.RatingAverage, stats.Avg).SetProperty(p => p.RatingCount, stats.Count), ct);

        var name = await db.Users.Where(u => u.Id == reviewerId).Select(u => u.FullName).FirstAsync(ct);
        return new ReviewDto(review.Id, review.Rating, review.Text, reviewerId, name, review.ListingId, review.CreatedAtUtc);
    }

    private async Task<bool> HasDealtWithAsync(int buyerId, int sellerId, CancellationToken ct)
    {
        // Talked: a chat where both people wrote
        var talked = await db.Chats.AnyAsync(c =>
            ((c.BuyerId == buyerId && c.SellerId == sellerId) || (c.BuyerId == sellerId && c.SellerId == buyerId)) &&
            c.Messages.Any(m => m.SenderId == buyerId) && c.Messages.Any(m => m.SenderId == sellerId), ct);
        if (talked) return true;
        // Bought through a requirement
        var offer = await db.RequestOffers.AnyAsync(o => o.SellerId == sellerId && o.Status == OfferStatus.Accepted && o.PurchaseRequest.BuyerId == buyerId, ct);
        if (offer) return true;
        // Booked a Qurbani share
        return await db.QurbaniBookings.AnyAsync(b => b.BuyerId == buyerId && b.Listing.SellerId == sellerId && b.Status == BookingStatus.Confirmed, ct);
    }

    // ---- Reports ------------------------------------------------------------------------

    public async Task ReportAsync(int reporterId, int listingId, ReportInput input, CancellationToken ct = default)
    {
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == listingId && l.Status != ListingStatus.Removed, ct)
            ?? throw new NotFoundException(ErrorCodes.ListingNotFound, "Listing not found.");
        if (listing.SellerId == reporterId) throw new BadRequestException(ErrorCodes.Validation, "You can't report your own listing.");
        if (await db.Reports.AnyAsync(r => r.ListingId == listingId && r.ReporterId == reporterId && r.Status == ReportStatus.Open, ct))
            throw new ConflictException(ErrorCodes.AlreadyReported, "You already reported this listing.");

        db.Reports.Add(new Report
        {
            ListingId = listingId, ReporterId = reporterId, Reason = input.Reason,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(), CreatedAtUtc = Now,
        });
        await db.SaveChangesAsync(ct);

        // Several different people reporting the same listing: hide it until a moderator checks.
        var openReporters = await db.Reports.Where(r => r.ListingId == listingId && r.Status == ReportStatus.Open)
            .Select(r => r.ReporterId).Distinct().CountAsync(ct);
        if (openReporters >= AutoHideReportCount && listing.Status == ListingStatus.Active)
        {
            listing.Status = ListingStatus.Hidden;
            listing.UpdatedAtUtc = Now;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<ReportDto>> OpenReportsAsync(CancellationToken ct = default) =>
        await db.Reports.AsNoTracking().Where(r => r.Status == ReportStatus.Open)
            .OrderBy(r => r.CreatedAtUtc).Take(200)
            .Select(r => new ReportDto(r.Id, r.ListingId, r.Listing.Title, r.Listing.Status, r.Reason, r.Note, r.Status,
                r.Reporter.FullName, db.Reports.Count(x => x.ListingId == r.ListingId && x.Status == ReportStatus.Open),
                r.CreatedAtUtc, r.ResolutionNote))
            .ToListAsync(ct);

    /// <summary>Hide the listing (all its open reports are closed with it) or dismiss this report.</summary>
    public async Task<ReportDto> ResolveReportAsync(int adminId, int reportId, ResolveReportInput input, CancellationToken ct = default)
    {
        var report = await db.Reports.Include(r => r.Listing).FirstOrDefaultAsync(r => r.Id == reportId, ct)
            ?? throw new NotFoundException(ErrorCodes.ReportNotFound, "Report not found.");
        var now = Now;
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        var toClose = input.HideListing
            ? await db.Reports.Where(r => r.ListingId == report.ListingId && r.Status == ReportStatus.Open).ToListAsync(ct)
            : [report];
        foreach (var r in toClose)
        {
            r.Status = input.HideListing ? ReportStatus.ActionTaken : ReportStatus.Dismissed;
            r.ResolvedAtUtc = now;
            r.ResolvedById = adminId;
            r.ResolutionNote = note;
        }
        if (input.HideListing) report.Listing.Status = ListingStatus.Hidden;
        else if (report.Listing.Status == ListingStatus.Hidden &&
                 !await db.Reports.AnyAsync(r => r.ListingId == report.ListingId && r.Status == ReportStatus.Open && r.Id != report.Id, ct))
            report.Listing.Status = ListingStatus.Active; // auto-hidden by mistake: bring it back
        report.Listing.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);

        return new ReportDto(report.Id, report.ListingId, report.Listing.Title, report.Listing.Status, report.Reason, report.Note,
            report.Status, await db.Users.Where(u => u.Id == report.ReporterId).Select(u => u.FullName).FirstAsync(ct),
            await db.Reports.CountAsync(x => x.ListingId == report.ListingId && x.Status == ReportStatus.Open, ct),
            report.CreatedAtUtc, report.ResolutionNote);
    }
}
