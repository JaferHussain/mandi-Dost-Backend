using MandiDost.Api.Entities;

namespace MandiDost.Api.Common;

/// <summary>
/// Which price and quantity units make sense for each product family.
/// The apps read these from GET /api/categories so the sell form only offers valid choices.
/// </summary>
public static class ListingRules
{
    public const int MaxMediaPerListing = 8;
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public static readonly TimeSpan ListingLifetime = TimeSpan.FromDays(60);

    public static IReadOnlyList<PriceUnit> PriceUnitsFor(Category c) => c.Section switch
    {
        ProductSection.Livestock => c.AllowsQurbaniShares
            ? [PriceUnit.PerItem, PriceUnit.PerKg, PriceUnit.PerMaund, PriceUnit.PerShare]
            : [PriceUnit.PerItem, PriceUnit.PerKg, PriceUnit.PerMaund],
        ProductSection.Crops => [PriceUnit.PerMaund, PriceUnit.PerKg, PriceUnit.PerTon, PriceUnit.PerBag],
        _ => [PriceUnit.PerItem, PriceUnit.PerBag, PriceUnit.PerKg, PriceUnit.PerLitre],
    };

    public static IReadOnlyList<QuantityUnit> QuantityUnitsFor(ProductSection section) => section switch
    {
        ProductSection.Livestock => [QuantityUnit.Head],
        ProductSection.Crops => [QuantityUnit.Maund, QuantityUnit.Kg, QuantityUnit.Ton, QuantityUnit.Bag],
        _ => [QuantityUnit.Piece, QuantityUnit.Bag, QuantityUnit.Kg, QuantityUnit.Litre],
    };
}
