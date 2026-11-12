using System.ComponentModel.DataAnnotations;
using MandiDost.Api.Common;
using MandiDost.Api.Entities;

namespace MandiDost.Api.DTOs;

// ---- Create / update ------------------------------------------------------

/// <summary>
/// Body for creating or updating a listing. Only the detail block that matches the
/// category's section may be sent (livestock / crop / supply); the server rejects
/// anything that doesn't belong, e.g. teeth on a wheat listing.
/// </summary>
public class ListingInput
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    [Range(1, int.MaxValue)]
    public int LocationId { get; set; }

    [Required, StringLength(120, MinimumLength = 3)]
    public string Title { get; set; } = "";

    [StringLength(1500)]
    public string? Description { get; set; }

    [StringLength(120)]
    public string? AreaText { get; set; }

    /// <summary>Whole PKR. Leave empty for "قیمت بات چیت سے".</summary>
    [Range(1, 1_000_000_000)]
    public long? Price { get; set; }

    public PriceUnit PriceUnit { get; set; } = PriceUnit.PerItem;
    public bool IsNegotiable { get; set; } = true;

    [Range(0.01, 10_000_000)]
    public decimal Quantity { get; set; } = 1;
    public QuantityUnit QuantityUnit { get; set; } = QuantityUnit.Head;

    [Range(0.01, 10_000_000)]
    public decimal? MinOrderQuantity { get; set; }
    public bool IsBulkAvailable { get; set; }
    public bool DeliveryAvailable { get; set; }

    /// <summary>Livestock only. At least one; must be allowed for the category.</summary>
    public List<ListingPurpose>? Purposes { get; set; }

    public LivestockInput? Livestock { get; set; }
    public QurbaniInput? Qurbani { get; set; }
    public MeatSupplyInput? MeatSupply { get; set; }
    public CropInput? Crop { get; set; }
    public SupplyInput? Supply { get; set; }
}

public class LivestockInput
{
    [StringLength(48)]
    public string? Breed { get; set; }
    public AnimalGender Gender { get; set; } = AnimalGender.Unknown;

    [Range(0, 360)]
    public int? AgeMonths { get; set; }
    public TeethStage? Teeth { get; set; }

    [Range(1, 2000)]
    public int? LiveWeightKg { get; set; }
    public bool? IsVaccinated { get; set; }
    public bool? IsPregnant { get; set; }

    [Range(0.1, 80)]
    public decimal? MilkLitresPerDay { get; set; }

    [StringLength(300)]
    public string? HealthNotes { get; set; }
}

public class QurbaniInput
{
    /// <summary>1 = whole animal, 7 = seven shares (cow, buffalo, camel).</summary>
    [Range(1, 7)]
    public int TotalShares { get; set; } = 1;

    [Range(1, 100_000_000)]
    public long? PricePerShare { get; set; }
}

public class MeatSupplyInput
{
    public SupplyFrequency Frequency { get; set; } = SupplyFrequency.OneTime;

    [Range(1, 100_000)]
    public int? PricePerKgLive { get; set; }
}

public class CropInput
{
    [StringLength(48)]
    public string? Variety { get; set; }

    [StringLength(24)]
    public string? Grade { get; set; }
    public DateOnly? HarvestDate { get; set; }
    public bool PickupAvailable { get; set; } = true;
}

public class SupplyInput
{
    [StringLength(48)]
    public string? Brand { get; set; }
    public ItemCondition Condition { get; set; } = ItemCondition.New;
}

// ---- Search ---------------------------------------------------------------

public class ListingQuery : PageQuery
{
    public ProductSection? Section { get; set; }

    /// <summary>One or more purposes, comma separated: "dairy,breeding,general". A listing matches if it has any.</summary>
    public string? Purpose { get; set; }

    /// <summary>Category slug, e.g. "goat" or "wheat".</summary>
    [StringLength(32)]
    public string? Category { get; set; }

    public int? LocationId { get; set; }

    /// <summary>Buyer's position for "near me" filtering and sorting.</summary>
    [Range(-90, 90)]
    public double? Lat { get; set; }

    [Range(-180, 180)]
    public double? Lng { get; set; }

    [Range(1, 1000)]
    public int? RadiusKm { get; set; }

    [Range(0, long.MaxValue)]
    public long? MinPrice { get; set; }

    [Range(0, long.MaxValue)]
    public long? MaxPrice { get; set; }

    public AnimalGender? Gender { get; set; }
    public TeethStage? Teeth { get; set; }

    [Range(0, 5000)]
    public int? MinWeightKg { get; set; }

    [Range(0, 5000)]
    public int? MaxWeightKg { get; set; }

    /// <summary>Only listings that can be sold as 7 Qurbani shares.</summary>
    public bool? Shares { get; set; }

    public bool? Bulk { get; set; }

    /// <summary>Words to look for in the title, breed, variety and description.</summary>
    [StringLength(60)]
    public string? Q { get; set; }

    public int? SellerId { get; set; }

    /// <summary>newest (default), priceAsc, priceDesc, nearest (needs lat/lng or locationId).</summary>
    [RegularExpression("^(newest|priceAsc|priceDesc|nearest)$")]
    public string? Sort { get; set; }
}

// ---- Responses ------------------------------------------------------------

public record SellerSummaryDto(int Id, string FullName, string Verification, bool PhoneVerified, double RatingAverage, int RatingCount);

public record MediaDto(int Id, MediaKind Kind, string Url, int SortOrder);

/// <summary>A row in a feed. Includes the facts every feed card needs, whatever the purpose.</summary>
public record ListingSummaryDto(
    int Id,
    string Title,
    ProductSection Section,
    string CategorySlug,
    string CategoryNameUr,
    IReadOnlyList<ListingPurpose> Purposes,
    long? Price,
    PriceUnit PriceUnit,
    bool IsNegotiable,
    decimal Quantity,
    QuantityUnit QuantityUnit,
    bool IsBulkAvailable,
    string LocationNameUr,
    double? DistanceKm,
    string? ThumbnailUrl,
    // Livestock facts (null for crops/supplies)
    string? Breed,
    AnimalGender? Gender,
    TeethStage? Teeth,
    int? AgeMonths,
    int? LiveWeightKg,
    decimal? MilkLitresPerDay,
    int? QurbaniShares,
    // Crop facts
    string? Variety,
    ListingStatus Status,
    DateTime? PublishedAtUtc,
    SellerSummaryDto Seller);

public record ListingDetailDto(
    int Id,
    string Title,
    string? Description,
    string? AreaText,
    ProductSection Section,
    CategoryDto Category,
    LocationDto Location,
    IReadOnlyList<ListingPurpose> Purposes,
    long? Price,
    PriceUnit PriceUnit,
    bool IsNegotiable,
    decimal Quantity,
    QuantityUnit QuantityUnit,
    decimal? MinOrderQuantity,
    bool IsBulkAvailable,
    bool DeliveryAvailable,
    IReadOnlyList<MediaDto> Media,
    LivestockDto? Livestock,
    QurbaniDto? Qurbani,
    MeatSupplyDto? MeatSupply,
    CropDto? Crop,
    SupplyDto? Supply,
    ListingStatus Status,
    int ViewCount,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    DateTime? ExpiresAtUtc,
    SellerSummaryDto Seller,
    /// <summary>The seller's phone, only when the seller allows showing it.</summary>
    string? SellerPhone,
    bool IsMine,
    bool IsFavorite);

public record LivestockDto(string? Breed, AnimalGender Gender, int? AgeMonths, TeethStage? Teeth, int? LiveWeightKg,
    bool? IsVaccinated, bool? IsPregnant, decimal? MilkLitresPerDay, string? HealthNotes);

public record QurbaniDto(int TotalShares, long? PricePerShare);

public record MeatSupplyDto(SupplyFrequency Frequency, int? PricePerKgLive);

public record CropDto(string? Variety, string? Grade, DateOnly? HarvestDate, bool PickupAvailable);

public record SupplyDto(string? Brand, ItemCondition Condition);
