using System.ComponentModel.DataAnnotations;
using MandiDost.Api.Entities;

namespace MandiDost.Api.DTOs;

// ---- Chat -------------------------------------------------------------------------

/// <summary>Start (or reopen) a conversation with another user, optionally about a listing.</summary>
public class StartChatInput
{
    /// <summary>Talk to the seller of this listing.</summary>
    public int? ListingId { get; set; }

    /// <summary>Or talk to this user directly (e.g. from a requirement's offer).</summary>
    public int? UserId { get; set; }
}

public class SendMessageInput
{
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Text { get; set; } = "";
}

public record ChatPartyDto(int Id, string FullName, bool PhoneVerified, string? BusinessName);

public record ChatSummaryDto(
    int Id,
    ChatPartyDto Other,
    int? ListingId,
    string? ListingTitle,
    string? ListingThumbnailUrl,
    string? LastMessagePreview,
    MessageType? LastMessageType,
    bool LastMessageIsMine,
    DateTime LastMessageAtUtc,
    int UnreadCount);

public record MessageDto(long Id, int SenderId, bool IsMine, MessageType Type, string? Text, string? MediaUrl, int? DurationSeconds, DateTime CreatedAtUtc, DateTime? ReadAtUtc);

public record ChatDetailDto(ChatSummaryDto Chat, IReadOnlyList<MessageDto> Messages, bool HasOlder);

// ---- Qurbani shares -----------------------------------------------------------------

public class BookSharesInput
{
    [Range(1, 7)]
    public int Shares { get; set; } = 1;

    [StringLength(300)]
    public string? Note { get; set; }
}

public record BookingDto(
    int Id,
    int ListingId,
    string ListingTitle,
    int Shares,
    BookingStatus Status,
    string? Note,
    int BuyerId,
    string BuyerName,
    /// <summary>Shown to the seller so they can call; the buyer sees the seller's number on the listing.</summary>
    string? BuyerPhone,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record SharesDto(
    int TotalShares,
    int ConfirmedShares,
    int RequestedShares,
    int AvailableShares,
    long? PricePerShare,
    IReadOnlyList<BookingDto> MyBookings,
    /// <summary>All bookings – only for the seller.</summary>
    IReadOnlyList<BookingDto>? AllBookings);

// ---- Reviews, reports, public profile ------------------------------------------------------

public class ReviewInput
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Text { get; set; }

    public int? ListingId { get; set; }
}

public record ReviewDto(int Id, int Rating, string? Text, int ReviewerId, string ReviewerName, int? ListingId, DateTime CreatedAtUtc);

public class ReportInput
{
    public ReportReason Reason { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public record ReportDto(
    int Id, int ListingId, string ListingTitle, ListingStatus ListingStatus, ReportReason Reason, string? Note,
    ReportStatus Status, string ReporterName, int OpenReportsOnListing, DateTime CreatedAtUtc, string? ResolutionNote);

public class ResolveReportInput
{
    /// <summary>true = hide the listing; false = dismiss the report.</summary>
    public bool HideListing { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

/// <summary>What anyone can see about a seller.</summary>
public record PublicProfileDto(
    int Id,
    string FullName,
    string Verification,
    bool PhoneVerified,
    string? Bio,
    LocationDto? Location,
    /// <summary>Only when the user chose to show it.</summary>
    string? PhoneNumber,
    BusinessDto? Business,
    int ActiveListings,
    int SoldCount,
    double RatingAverage,
    int RatingCount,
    DateTime MemberSinceUtc,
    /// <summary>Whether the viewer may leave a review (has dealt with this seller).</summary>
    bool CanReview);
