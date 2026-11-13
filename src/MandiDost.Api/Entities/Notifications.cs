namespace MandiDost.Api.Entities;

/// <summary>What happened. The app turns the type into a short Urdu (or Punjabi/Saraiki) sentence.</summary>
public enum NotificationType
{
    NearbyListing,
    NearbyRequest,
    OfferReceived,
    OfferAccepted,
    OfferDeclined,
    BookingRequested,
    BookingConfirmed,
    BookingDeclined,
}

/// <summary>One item in a user's notification list. Only facts are stored; the app writes the sentence.</summary>
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public NotificationType Type { get; set; }
    public int? ListingId { get; set; }
    public int? RequestId { get; set; }

    /// <summary>The listing or requirement title the notification is about (written by a user, not translated).</summary>
    public string Subject { get; set; } = "";
    public string? Place { get; set; }
    public double? DistanceKm { get; set; }
    /// <summary>Number of shares for booking notifications.</summary>
    public int? Count { get; set; }
    /// <summary>Offered price for offer notifications.</summary>
    public long? Amount { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    /// <summary>Stops the same event notifying the same user twice (e.g. "L12" = listing 12).</summary>
    public string DedupeKey { get; set; } = "";

    public User User { get; set; } = null!;
}

/// <summary>Who wants which nearby alerts. A user without a row gets the defaults below.</summary>
public class NotificationSettings
{
    public const int DefaultRadiusKm = 30;
    public const int MaxRadiusKm = 300;

    public int UserId { get; set; }
    public bool Enabled { get; set; } = true;
    public bool WantListings { get; set; } = true;
    public bool WantRequests { get; set; } = true;
    public int RadiusKm { get; set; } = DefaultRadiusKm;

    /// <summary>Comma-separated category ids the user cares about. Empty means every category.</summary>
    public string? InterestCategoryIds { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
