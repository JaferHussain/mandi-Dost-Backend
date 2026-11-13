using System.ComponentModel.DataAnnotations;
using MandiDost.Api.Entities;

namespace MandiDost.Api.DTOs;

public record NotificationDto(
    int Id, NotificationType Type, int? ListingId, int? RequestId, string Subject, string? Place,
    double? DistanceKm, int? Count, long? Amount, DateTime CreatedAtUtc, bool IsRead);

/// <summary>Newest first. <c>UnreadCount</c> is for the whole inbox, not just this page.</summary>
public record NotificationListDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);

public record NotificationSettingsDto(bool Enabled, bool WantListings, bool WantRequests, int RadiusKm, IReadOnlyList<int> CategoryIds);

public class NotificationSettingsInput
{
    public bool Enabled { get; set; } = true;
    public bool WantListings { get; set; } = true;
    public bool WantRequests { get; set; } = true;

    [Range(5, NotificationSettings.MaxRadiusKm)]
    public int RadiusKm { get; set; } = NotificationSettings.DefaultRadiusKm;

    /// <summary>Categories to get alerts about. Empty means all.</summary>
    [MaxLength(40)]
    public List<int> CategoryIds { get; set; } = [];
}

public class NotificationQuery
{
    /// <summary>Only items newer than this id (used for polling).</summary>
    public int? AfterId { get; set; }
    /// <summary>Only items older than this id (load more).</summary>
    public int? BeforeId { get; set; }
    [Range(1, 50)]
    public int PageSize { get; set; } = 20;
}
