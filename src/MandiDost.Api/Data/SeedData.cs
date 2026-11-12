using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MandiDost.Api.Data;

/// <summary>
/// Reference data shipped with migrations. Livestock ids 1–6 and their slugs are
/// unchanged from the first release, so existing app code keeps working.
/// </summary>
public static class SeedData
{
    private static Category Cat(int id, ProductSection section, string slug, string ur, string en, string icon, PriceUnit unit,
        bool milk = false, bool shares = false) =>
        new()
        {
            Id = id, Section = section, Slug = slug, NameUr = ur, NameEn = en, IconKey = icon, DefaultPriceUnit = unit,
            HasMilkProduction = milk, AllowsQurbaniShares = shares, SortOrder = id,
        };

    public static readonly Category[] Categories =
    [
        // Livestock
        Cat(1, ProductSection.Livestock, "cow", "گائے / بیل", "Cow / bull", "cow", PriceUnit.PerItem, milk: true, shares: true),
        Cat(2, ProductSection.Livestock, "buffalo", "بھینس / کٹا", "Buffalo", "cow", PriceUnit.PerItem, milk: true, shares: true),
        Cat(3, ProductSection.Livestock, "goat", "بکری / بکرا", "Goat", "sheep", PriceUnit.PerItem, milk: true),
        Cat(4, ProductSection.Livestock, "sheep", "بھیڑ / دنبہ", "Sheep", "sheep", PriceUnit.PerItem),
        Cat(5, ProductSection.Livestock, "camel", "اونٹ", "Camel", "horse-variant", PriceUnit.PerItem, milk: true, shares: true),
        Cat(6, ProductSection.Livestock, "other", "دیگر جانور", "Other livestock", "paw", PriceUnit.PerItem),

        // Crops and produce
        Cat(7, ProductSection.Crops, "wheat", "گندم", "Wheat", "barley", PriceUnit.PerMaund),
        Cat(8, ProductSection.Crops, "cotton", "کپاس", "Cotton", "flower-outline", PriceUnit.PerMaund),
        Cat(9, ProductSection.Crops, "rice", "چاول / دھان", "Rice / paddy", "rice", PriceUnit.PerMaund),
        Cat(10, ProductSection.Crops, "maize", "مکئی", "Maize", "corn", PriceUnit.PerMaund),
        Cat(11, ProductSection.Crops, "sugarcane", "گنا", "Sugarcane", "grass", PriceUnit.PerMaund),
        Cat(12, ProductSection.Crops, "vegetables", "سبزیاں", "Vegetables", "carrot", PriceUnit.PerKg),
        Cat(13, ProductSection.Crops, "fruits", "پھل", "Fruits", "fruit-cherries", PriceUnit.PerKg),
        Cat(14, ProductSection.Crops, "other-produce", "دیگر پیداوار", "Other produce", "basket-outline", PriceUnit.PerMaund),

        // Agriculture supplies
        Cat(15, ProductSection.Supplies, "seeds", "بیج", "Seeds", "seed-outline", PriceUnit.PerBag),
        Cat(16, ProductSection.Supplies, "fertilizer", "کھاد", "Fertilizer", "sack", PriceUnit.PerBag),
        Cat(17, ProductSection.Supplies, "pesticides", "زرعی ادویات", "Pesticides", "spray", PriceUnit.PerItem),
        Cat(18, ProductSection.Supplies, "feed", "چارہ اور ونڈا", "Animal feed and fodder", "grain", PriceUnit.PerBag),
        Cat(19, ProductSection.Supplies, "tools", "اوزار", "Farming tools", "shovel", PriceUnit.PerItem),
        Cat(20, ProductSection.Supplies, "machinery", "مشینری", "Machinery", "tractor", PriceUnit.PerItem),
        Cat(21, ProductSection.Supplies, "dairy-equipment", "ڈیری کا سامان", "Dairy and livestock equipment", "bucket-outline", PriceUnit.PerItem),
    ];

    private static readonly ListingPurpose[] AllAnimalPurposes =
        [ListingPurpose.Qurbani, ListingPurpose.MeatSupply, ListingPurpose.Dairy, ListingPurpose.Breeding, ListingPurpose.General];

    /// <summary>Which purposes each livestock category may be listed for. Crops and supplies have none.</summary>
    public static readonly CategoryPurpose[] CategoryPurposes =
    [
        .. AllAnimalPurposes.Select(p => new CategoryPurpose { CategoryId = 1, Purpose = p }),
        .. AllAnimalPurposes.Select(p => new CategoryPurpose { CategoryId = 2, Purpose = p }),
        .. AllAnimalPurposes.Select(p => new CategoryPurpose { CategoryId = 3, Purpose = p }),
        // Sheep are not kept for milk here
        .. new[] { ListingPurpose.Qurbani, ListingPurpose.MeatSupply, ListingPurpose.Breeding, ListingPurpose.General }
            .Select(p => new CategoryPurpose { CategoryId = 4, Purpose = p }),
        .. AllAnimalPurposes.Select(p => new CategoryPurpose { CategoryId = 5, Purpose = p }),
        // Donkeys, horses, etc.: not for Qurbani or meat
        .. new[] { ListingPurpose.Breeding, ListingPurpose.General }
            .Select(p => new CategoryPurpose { CategoryId = 6, Purpose = p }),
    ];

    public static readonly Location[] Locations =
    [
        new() { Id = 1, Slug = "lodhran", NameUr = "لودھراں", NameEn = "Lodhran", DistrictUr = "لودھراں", Latitude = 29.5339, Longitude = 71.6324, IsLaunchArea = true },
        new() { Id = 2, Slug = "dunyapur", NameUr = "دنیا پور", NameEn = "Dunyapur", DistrictUr = "لودھراں", Latitude = 29.802, Longitude = 71.743, IsLaunchArea = true },
        new() { Id = 3, Slug = "kahror", NameUr = "کہروڑ پکا", NameEn = "Kahror Pakka", DistrictUr = "لودھراں", Latitude = 29.6237, Longitude = 71.9165, IsLaunchArea = true },
        new() { Id = 4, Slug = "jalalpur", NameUr = "جلال پور پیر والا", NameEn = "Jalalpur Pirwala", DistrictUr = "ملتان", Latitude = 29.505, Longitude = 71.221 },
        new() { Id = 5, Slug = "multan", NameUr = "ملتان", NameEn = "Multan", DistrictUr = "ملتان", Latitude = 30.1575, Longitude = 71.5249 },
        new() { Id = 6, Slug = "shujabad", NameUr = "شجاع آباد", NameEn = "Shujabad", DistrictUr = "ملتان", Latitude = 29.879, Longitude = 71.295 },
        new() { Id = 7, Slug = "bahawalpur", NameUr = "بہاولپور", NameEn = "Bahawalpur", DistrictUr = "بہاولپور", Latitude = 29.3956, Longitude = 71.6836 },
        new() { Id = 8, Slug = "khanewal", NameUr = "خانیوال", NameEn = "Khanewal", DistrictUr = "خانیوال", Latitude = 30.3017, Longitude = 71.9321 },
        new() { Id = 9, Slug = "vehari", NameUr = "وہاڑی", NameEn = "Vehari", DistrictUr = "وہاڑی", Latitude = 30.0452, Longitude = 72.3489 },
        new() { Id = 10, Slug = "mailsi", NameUr = "میلسی", NameEn = "Mailsi", DistrictUr = "وہاڑی", Latitude = 29.8, Longitude = 72.175 },
    ];

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasData(Categories);
        modelBuilder.Entity<CategoryPurpose>().HasData(CategoryPurposes);
        modelBuilder.Entity<Location>().HasData(Locations);
    }
}
