namespace MandiDost.Api.Entities;

public class Review
{
    public int Id { get; set; }
    public int SellerId { get; set; }
    public int ReviewerId { get; set; }
    public int? ListingId { get; set; }

    /// <summary>1–5 stars.</summary>
    public int Rating { get; set; }
    public string? Text { get; set; }
    public bool IsHidden { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User Seller { get; set; } = null!;
    public User Reviewer { get; set; } = null!;
    public Listing? Listing { get; set; }
}

public class Report
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public int ReporterId { get; set; }
    public ReportReason Reason { get; set; }
    public string? Note { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public int? ResolvedById { get; set; }
    public string? ResolutionNote { get; set; }

    public Listing Listing { get; set; } = null!;
    public User Reporter { get; set; } = null!;
    public User? ResolvedBy { get; set; }
}
