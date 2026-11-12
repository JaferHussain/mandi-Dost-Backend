namespace MandiDost.Api.Entities;

/// <summary>
/// Optional business details on top of a user account (restaurant, hotel, butcher, trader…).
/// One per user for now. Having one doesn't stop the user buying or selling as a person.
/// </summary>
public class BusinessProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string BusinessName { get; set; } = "";
    public BusinessType Type { get; set; }
    public int LocationId { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    /// <summary>Contact number for the business if different from the account's own number.</summary>
    public string? ContactPhone { get; set; }

    public BusinessVerification Verification { get; set; } = BusinessVerification.Unverified;
    public DateTime? VerificationRequestedAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public int? VerifiedById { get; set; }
    public string? VerificationNote { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Location Location { get; set; } = null!;
}

/// <summary>
/// A buyer's requirement: "10 goats, 25–30 kg, every week, Lodhran". Sellers answer with offers.
/// Same category/purpose/unit rules as listings.
/// </summary>
public class PurchaseRequest
{
    public int Id { get; set; }
    public int BuyerId { get; set; }
    public int CategoryId { get; set; }
    /// <summary>Livestock only, e.g. MeatSupply or Qurbani.</summary>
    public ListingPurpose? Purpose { get; set; }
    public int LocationId { get; set; }

    public string Title { get; set; } = "";
    public string? Notes { get; set; }

    public decimal Quantity { get; set; }
    public QuantityUnit QuantityUnit { get; set; }
    public SupplyFrequency Frequency { get; set; } = SupplyFrequency.OneTime;

    /// <summary>Livestock: wanted live weight range per animal.</summary>
    public int? MinWeightKg { get; set; }
    public int? MaxWeightKg { get; set; }

    /// <summary>What the buyer hopes to pay. Optional – never shown as a market rate.</summary>
    public long? TargetPrice { get; set; }
    public PriceUnit? TargetPriceUnit { get; set; }
    public DateOnly? NeededBy { get; set; }
    public bool DeliveryNeeded { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Open;
    public int OfferCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public User Buyer { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public ICollection<RequestOffer> Offers { get; set; } = [];
}

/// <summary>A seller's answer to a purchase request. One per seller per request (updating replaces it).</summary>
public class RequestOffer
{
    public int Id { get; set; }
    public int PurchaseRequestId { get; set; }
    public int SellerId { get; set; }
    /// <summary>Optional: one of the seller's own listings that matches.</summary>
    public int? ListingId { get; set; }

    public long? Price { get; set; }
    public PriceUnit? PriceUnit { get; set; }
    public decimal? Quantity { get; set; }
    public string? Message { get; set; }

    public OfferStatus Status { get; set; } = OfferStatus.Pending;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public PurchaseRequest PurchaseRequest { get; set; } = null!;
    public User Seller { get; set; } = null!;
    public Listing? Listing { get; set; }
}
