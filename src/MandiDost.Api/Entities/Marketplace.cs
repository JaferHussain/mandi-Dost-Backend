namespace MandiDost.Api.Entities;

/// <summary>
/// One thing for sale: an animal (or a group of animals), a crop lot or a supply item.
/// Common fields live here; fields that only make sense for one product family live
/// in a 1:1 detail table (<see cref="LivestockDetails"/>, <see cref="CropDetails"/>,
/// <see cref="SupplyDetails"/>) so this table doesn't fill up with unrelated columns.
/// </summary>
public class Listing
{
    public int Id { get; set; }
    public int SellerId { get; set; }
    public int CategoryId { get; set; }
    public int LocationId { get; set; }

    public string Title { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Village / chak / mohalla typed by the seller.</summary>
    public string? AreaText { get; set; }

    /// <summary>Asking price in whole PKR. Null = "قیمت بات چیت سے" (price on call).</summary>
    public long? Price { get; set; }
    public PriceUnit PriceUnit { get; set; } = PriceUnit.PerItem;
    public bool IsNegotiable { get; set; } = true;

    /// <summary>How much is on offer: 1 head, 12 heads, 400 maund, 50 bags…</summary>
    public decimal Quantity { get; set; } = 1;
    public QuantityUnit QuantityUnit { get; set; } = QuantityUnit.Head;
    /// <summary>Smallest amount the seller will sell (bulk listings).</summary>
    public decimal? MinOrderQuantity { get; set; }
    public bool IsBulkAvailable { get; set; }
    public bool DeliveryAvailable { get; set; }

    public string? VoiceNoteUrl { get; set; }
    public int? VoiceNoteSeconds { get; set; }

    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public int ViewCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    /// <summary>Active listings disappear from feeds after this time unless renewed.</summary>
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? SoldAtUtc { get; set; }

    public User Seller { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public ICollection<ListingPurposeLink> Purposes { get; set; } = [];
    public ICollection<ListingMedia> Media { get; set; } = [];

    public LivestockDetails? Livestock { get; set; }
    public QurbaniDetails? Qurbani { get; set; }
    public MeatSupplyDetails? MeatSupply { get; set; }
    public CropDetails? Crop { get; set; }
    public SupplyDetails? Supply { get; set; }
}

/// <summary>A purpose ticked by the seller. Table: ListingPurposes.</summary>
public class ListingPurposeLink
{
    public int ListingId { get; set; }
    public ListingPurpose Purpose { get; set; }

    public Listing Listing { get; set; } = null!;
}

public class ListingMedia
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public MediaKind Kind { get; set; } = MediaKind.Image;
    /// <summary>Relative path under the media root, e.g. "listings/12/ab12….jpg".</summary>
    public string Path { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Listing Listing { get; set; } = null!;
}

/// <summary>Animal facts. Present for every livestock listing.</summary>
public class LivestockDetails
{
    public int ListingId { get; set; }
    public string? Breed { get; set; }
    public AnimalGender Gender { get; set; } = AnimalGender.Unknown;
    public int? AgeMonths { get; set; }
    public TeethStage? Teeth { get; set; }
    /// <summary>Live weight per animal as told by the seller (or from the weight calculator).</summary>
    public int? LiveWeightKg { get; set; }
    public bool? IsVaccinated { get; set; }
    /// <summary>Only for females.</summary>
    public bool? IsPregnant { get; set; }
    /// <summary>Only for milk-producing categories and females.</summary>
    public decimal? MilkLitresPerDay { get; set; }
    public string? HealthNotes { get; set; }

    public Listing Listing { get; set; } = null!;
}

/// <summary>Only when the listing has the Qurbani purpose.</summary>
public class QurbaniDetails
{
    public int ListingId { get; set; }
    /// <summary>1 = whole animal; 7 = sold as seven shares (cow, buffalo, camel only).</summary>
    public int TotalShares { get; set; } = 1;
    public long? PricePerShare { get; set; }

    public Listing Listing { get; set; } = null!;
}

/// <summary>Only when the listing has the meat-supply (restaurant/butcher) purpose.</summary>
public class MeatSupplyDetails
{
    public int ListingId { get; set; }
    public SupplyFrequency Frequency { get; set; } = SupplyFrequency.OneTime;
    /// <summary>Optional rate per kg of live weight ("زندہ وزن ریٹ").</summary>
    public int? PricePerKgLive { get; set; }

    public Listing Listing { get; set; } = null!;
}

public class CropDetails
{
    public int ListingId { get; set; }
    /// <summary>Variety, e.g. "اکبر 2019" wheat or "سپر باسمتی".</summary>
    public string? Variety { get; set; }
    /// <summary>Seller's own grade, e.g. "اول", "دوم".</summary>
    public string? Grade { get; set; }
    public DateOnly? HarvestDate { get; set; }
    public bool PickupAvailable { get; set; } = true;

    public Listing Listing { get; set; } = null!;
}

public class SupplyDetails
{
    public int ListingId { get; set; }
    public string? Brand { get; set; }
    public ItemCondition Condition { get; set; } = ItemCondition.New;

    public Listing Listing { get; set; } = null!;
}

public class Favorite
{
    public int UserId { get; set; }
    public int ListingId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Listing Listing { get; set; } = null!;
}

/// <summary>
/// A buyer asking for some of the 7 Qurbani shares of a cow, buffalo or camel.
/// Requested shares are held until the seller confirms or declines, so shares can't be double-booked.
/// No money moves through the app.
/// </summary>
public class QurbaniBooking
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public int BuyerId { get; set; }
    public int Shares { get; set; }
    public string? Note { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Requested;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Listing Listing { get; set; } = null!;
    public User Buyer { get; set; } = null!;
}
