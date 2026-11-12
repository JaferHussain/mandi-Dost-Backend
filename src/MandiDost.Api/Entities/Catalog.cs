namespace MandiDost.Api.Entities;

/// <summary>
/// A product category (cow, wheat, fertilizer…). Categories live in the database,
/// so new ones can be added without an app release. <see cref="ParentId"/> allows
/// sub-categories later (e.g. vegetables → onions).
/// </summary>
public class Category
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public ProductSection Section { get; set; }

    /// <summary>Stable key used by the apps: cow, buffalo, wheat, fertilizer…</summary>
    public string Slug { get; set; } = "";
    public string NameUr { get; set; } = "";
    public string NameEn { get; set; } = "";

    /// <summary>Icon name used by the mobile app (Material Community Icons).</summary>
    public string? IconKey { get; set; }

    /// <summary>Milk yield is a meaningful field (cow, buffalo, goat, camel).</summary>
    public bool HasMilkProduction { get; set; }

    /// <summary>Can be sold as 7 Qurbani shares (cow, buffalo, camel).</summary>
    public bool AllowsQurbaniShares { get; set; }

    public PriceUnit DefaultPriceUnit { get; set; } = PriceUnit.PerItem;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Category? Parent { get; set; }
    public ICollection<CategoryPurpose> Purposes { get; set; } = [];
}

/// <summary>Which buying purposes a category allows. Crops and supplies have none.</summary>
public class CategoryPurpose
{
    public int CategoryId { get; set; }
    public ListingPurpose Purpose { get; set; }

    public Category Category { get; set; } = null!;
}

public class Location
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string NameUr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string DistrictUr { get; set; } = "";
    public string Province { get; set; } = "Punjab";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Part of the initial Lodhran launch area.</summary>
    public bool IsLaunchArea { get; set; }
    public bool IsActive { get; set; } = true;
}
