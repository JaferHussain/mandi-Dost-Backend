namespace MandiDost.Api.Entities;

// Enums are stored as strings in the database (readable, safe to reorder)
// and sent as camelCase strings in JSON ("qurbani", "perMaund").

public enum UserRole { User, Moderator, Admin }

/// <summary>How much of the seller's identity has been checked.</summary>
public enum VerificationLevel { None, Phone, Cnic }

/// <summary>The three product families. Each has its own detail fields.</summary>
public enum ProductSection { Livestock, Crops, Supplies }

/// <summary>
/// Why a buyer would want a livestock listing. One listing can have several,
/// so the same bull can appear in the Qurbani, meat-supply and general feeds.
/// </summary>
public enum ListingPurpose { Qurbani, MeatSupply, Dairy, Breeding, General }

public enum AnimalGender { Unknown, Male, Female }

/// <summary>Teeth stage as used in the mandi: کھیرا (milk teeth), دو دانت, چار دانت, چھ دانت, پورا منہ.</summary>
public enum TeethStage { MilkTeeth, Two, Four, Six, Full }

/// <summary>What the price refers to.</summary>
public enum PriceUnit { PerItem, PerKg, PerMaund, PerTon, PerBag, PerLitre, PerShare }

/// <summary>Unit of the quantity on offer. Animals are counted in heads.</summary>
public enum QuantityUnit { Head, Kg, Maund, Ton, Bag, Litre, Piece }

public enum SupplyFrequency { OneTime, Daily, Weekly, Monthly }

public enum ItemCondition { New, Used }

public enum MediaKind { Image, Video }

/// <summary>Kind of business behind a profile. A user can be a buyer and a seller whatever this is.</summary>
public enum BusinessType { Restaurant, Hotel, Butcher, MeatSupplier, Trader, Farm, AgriSupplier, Other }

/// <summary>Business check by the Mandi Dost team (documents / visit).</summary>
public enum BusinessVerification { Unverified, Pending, Verified, Rejected }

/// <summary>A buyer's posted requirement ("ضرورت").</summary>
public enum RequestStatus { Open, Fulfilled, Closed, Expired }

public enum OfferStatus { Pending, Accepted, Declined, Withdrawn }

public enum BookingStatus { Requested, Confirmed, Declined, Cancelled }

public enum ListingStatus { Draft, Active, Sold, Expired, Removed, Hidden }


public enum MessageType { Text, Voice, Image }

public enum ReportReason
{
    NotAvailable,
    WrongMedia,
    WrongDetails,
    AdvancePaymentScam,
    HiddenIllness,
    Inappropriate,
    Other,
}

public enum ReportStatus { Open, Reviewed, ActionTaken, Dismissed }
