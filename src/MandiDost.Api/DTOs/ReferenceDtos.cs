using MandiDost.Api.Entities;

namespace MandiDost.Api.DTOs;

/// <summary>
/// A category plus the rules the apps need to build the sell form:
/// which purposes are allowed and which price units make sense.
/// </summary>
public record CategoryDto(
    int Id,
    int? ParentId,
    ProductSection Section,
    string Slug,
    string NameUr,
    string NameEn,
    string? IconKey,
    bool HasMilkProduction,
    bool AllowsQurbaniShares,
    PriceUnit DefaultPriceUnit,
    IReadOnlyList<ListingPurpose> Purposes,
    IReadOnlyList<PriceUnit> PriceUnits,
    IReadOnlyList<QuantityUnit> QuantityUnits);

public record LocationDto(int Id, string Slug, string NameUr, string NameEn, string DistrictUr, double Latitude, double Longitude, bool IsLaunchArea);

public record AdminStatsDto(int Users, int ActiveListings, int SoldListings, int OpenReports);
