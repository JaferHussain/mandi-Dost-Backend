using System.ComponentModel.DataAnnotations;
using MandiDost.Api.Common;
using MandiDost.Api.Entities;

namespace MandiDost.Api.DTOs;

// ---- Business profile -------------------------------------------------------

public class BusinessInput
{
    [Required, StringLength(80, MinimumLength = 2)]
    public string BusinessName { get; set; } = "";

    public BusinessType Type { get; set; }

    [Range(1, int.MaxValue)]
    public int LocationId { get; set; }

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [PakistaniMobile]
    public string? ContactPhone { get; set; }
}

public record BusinessDto(
    int Id,
    int UserId,
    string BusinessName,
    BusinessType Type,
    LocationDto Location,
    string? Address,
    string? Description,
    /// <summary>Business number, or the owner's number if they show it. Null when hidden.</summary>
    string? ContactPhone,
    BusinessVerification Verification,
    DateTime? VerifiedAtUtc,
    DateTime CreatedAtUtc,
    int OpenRequestCount);

public class VerificationDecision
{
    public bool Approve { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }
}

// ---- Purchase requests ------------------------------------------------------

public class PurchaseRequestInput
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    /// <summary>Livestock only (e.g. meatSupply, qurbani). Must be allowed for the category.</summary>
    public ListingPurpose? Purpose { get; set; }

    [Range(1, int.MaxValue)]
    public int LocationId { get; set; }

    /// <summary>Optional – the server writes one from the details when empty.</summary>
    [StringLength(120)]
    public string? Title { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Range(0.01, 10_000_000)]
    public decimal Quantity { get; set; }
    public QuantityUnit QuantityUnit { get; set; }
    public SupplyFrequency Frequency { get; set; } = SupplyFrequency.OneTime;

    [Range(1, 2000)]
    public int? MinWeightKg { get; set; }

    [Range(1, 2000)]
    public int? MaxWeightKg { get; set; }

    [Range(1, 1_000_000_000)]
    public long? TargetPrice { get; set; }
    public PriceUnit? TargetPriceUnit { get; set; }

    public DateOnly? NeededBy { get; set; }
    public bool DeliveryNeeded { get; set; }
}

public class PurchaseRequestQuery : PageQuery
{
    public ProductSection? Section { get; set; }
    public ListingPurpose? Purpose { get; set; }

    [StringLength(32)]
    public string? Category { get; set; }

    public int? LocationId { get; set; }

    [Range(-90, 90)]
    public double? Lat { get; set; }

    [Range(-180, 180)]
    public double? Lng { get; set; }

    [Range(1, 1000)]
    public int? RadiusKm { get; set; }

    public SupplyFrequency? Frequency { get; set; }

    /// <summary>newest (default) or nearest.</summary>
    [RegularExpression("^(newest|nearest)$")]
    public string? Sort { get; set; }
}

public record BuyerSummaryDto(int Id, string FullName, bool PhoneVerified, string? BusinessName, BusinessType? BusinessType, bool BusinessVerified);

public record PurchaseRequestSummaryDto(
    int Id,
    string Title,
    ProductSection Section,
    string CategorySlug,
    string CategoryNameUr,
    ListingPurpose? Purpose,
    decimal Quantity,
    QuantityUnit QuantityUnit,
    SupplyFrequency Frequency,
    int? MinWeightKg,
    int? MaxWeightKg,
    long? TargetPrice,
    PriceUnit? TargetPriceUnit,
    DateOnly? NeededBy,
    string LocationNameUr,
    double? DistanceKm,
    int OfferCount,
    RequestStatus Status,
    DateTime CreatedAtUtc,
    BuyerSummaryDto Buyer);

public record PurchaseRequestDetailDto(
    int Id,
    string Title,
    string? Notes,
    ProductSection Section,
    CategoryDto Category,
    ListingPurpose? Purpose,
    LocationDto Location,
    decimal Quantity,
    QuantityUnit QuantityUnit,
    SupplyFrequency Frequency,
    int? MinWeightKg,
    int? MaxWeightKg,
    long? TargetPrice,
    PriceUnit? TargetPriceUnit,
    DateOnly? NeededBy,
    bool DeliveryNeeded,
    RequestStatus Status,
    int OfferCount,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    BuyerSummaryDto Buyer,
    /// <summary>The buyer's number – only shown to sellers who have made an offer, and to the buyer.</summary>
    string? BuyerPhone,
    bool IsMine,
    /// <summary>All offers – only for the buyer.</summary>
    IReadOnlyList<OfferDto>? Offers,
    /// <summary>The viewer's own offer, if they are a seller who answered.</summary>
    OfferDto? MyOffer);

// ---- Offers -------------------------------------------------------------------

public class OfferInput
{
    [Range(1, 1_000_000_000)]
    public long? Price { get; set; }
    public PriceUnit? PriceUnit { get; set; }

    [Range(0.01, 10_000_000)]
    public decimal? Quantity { get; set; }

    /// <summary>Optional: one of your own active listings that matches the request.</summary>
    public int? ListingId { get; set; }

    [StringLength(500)]
    public string? Message { get; set; }
}

public record OfferDto(
    int Id,
    int PurchaseRequestId,
    string RequestTitle,
    long? Price,
    PriceUnit? PriceUnit,
    decimal? Quantity,
    string? Message,
    OfferStatus Status,
    int? ListingId,
    string? ListingTitle,
    string? ListingThumbnailUrl,
    SellerSummaryDto Seller,
    /// <summary>Seller's number – shown to the buyer of the request only.</summary>
    string? SellerPhone,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
