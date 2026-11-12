using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MandiDost.Api.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Slug).HasMaxLength(32).IsRequired();
        b.Property(x => x.NameUr).HasMaxLength(64).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(64).IsRequired();
        b.Property(x => x.IconKey).HasMaxLength(48);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.Section, x.SortOrder });
        b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CategoryPurposeConfiguration : IEntityTypeConfiguration<CategoryPurpose>
{
    public void Configure(EntityTypeBuilder<CategoryPurpose> b)
    {
        b.ToTable("CategoryPurposes");
        b.HasKey(x => new { x.CategoryId, x.Purpose });
        b.HasOne(x => x.Category).WithMany(c => c.Purposes).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> b)
    {
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1500);
        b.Property(x => x.AreaText).HasMaxLength(120);
        b.Property(x => x.VoiceNoteUrl).HasMaxLength(512);
        b.Property(x => x.Quantity).HasPrecision(12, 2);
        b.Property(x => x.MinOrderQuantity).HasPrecision(12, 2);

        // Seller deletion must not silently wipe listings – users are deactivated instead.
        b.HasOne(x => x.Seller).WithMany(u => u.Listings).HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);

        // Indexes for the common marketplace queries (browse, filter, sort, seller page)
        b.HasIndex(x => new { x.Status, x.CategoryId, x.PublishedAtUtc });
        b.HasIndex(x => new { x.Status, x.LocationId, x.PublishedAtUtc });
        b.HasIndex(x => new { x.Status, x.Price });
        b.HasIndex(x => new { x.Status, x.ExpiresAtUtc });
        b.HasIndex(x => new { x.SellerId, x.Status });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Listings_Price", "Price IS NULL OR Price > 0");
            t.HasCheckConstraint("CK_Listings_Quantity", "Quantity > 0");
        });
    }
}

public class ListingPurposeConfiguration : IEntityTypeConfiguration<ListingPurposeLink>
{
    public void Configure(EntityTypeBuilder<ListingPurposeLink> b)
    {
        b.ToTable("ListingPurposes");
        b.HasKey(x => new { x.ListingId, x.Purpose });
        b.HasOne(x => x.Listing).WithMany(l => l.Purposes).HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        // Feed queries filter by purpose first
        b.HasIndex(x => new { x.Purpose, x.ListingId });
    }
}

public class ListingMediaConfiguration : IEntityTypeConfiguration<ListingMedia>
{
    public void Configure(EntityTypeBuilder<ListingMedia> b)
    {
        b.ToTable("ListingMedia");
        b.Property(x => x.Path).HasMaxLength(256).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(32).IsRequired();
        b.HasOne(x => x.Listing).WithMany(l => l.Media).HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ListingId, x.SortOrder });
    }
}

public class LivestockDetailsConfiguration : IEntityTypeConfiguration<LivestockDetails>
{
    public void Configure(EntityTypeBuilder<LivestockDetails> b)
    {
        b.HasKey(x => x.ListingId);
        b.Property(x => x.Breed).HasMaxLength(48);
        b.Property(x => x.MilkLitresPerDay).HasPrecision(4, 1);
        b.Property(x => x.HealthNotes).HasMaxLength(300);
        b.HasOne(x => x.Listing).WithOne(l => l.Livestock).HasForeignKey<LivestockDetails>(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Breed);
        b.HasIndex(x => new { x.Gender, x.Teeth });
        b.HasIndex(x => x.LiveWeightKg);
    }
}

public class QurbaniDetailsConfiguration : IEntityTypeConfiguration<QurbaniDetails>
{
    public void Configure(EntityTypeBuilder<QurbaniDetails> b)
    {
        b.HasKey(x => x.ListingId);
        b.HasOne(x => x.Listing).WithOne(l => l.Qurbani).HasForeignKey<QurbaniDetails>(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        b.ToTable(t => t.HasCheckConstraint("CK_Qurbani_Shares", "TotalShares IN (1, 7)"));
    }
}

public class MeatSupplyDetailsConfiguration : IEntityTypeConfiguration<MeatSupplyDetails>
{
    public void Configure(EntityTypeBuilder<MeatSupplyDetails> b)
    {
        b.HasKey(x => x.ListingId);
        b.HasOne(x => x.Listing).WithOne(l => l.MeatSupply).HasForeignKey<MeatSupplyDetails>(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CropDetailsConfiguration : IEntityTypeConfiguration<CropDetails>
{
    public void Configure(EntityTypeBuilder<CropDetails> b)
    {
        b.HasKey(x => x.ListingId);
        b.Property(x => x.Variety).HasMaxLength(48);
        b.Property(x => x.Grade).HasMaxLength(24);
        b.HasOne(x => x.Listing).WithOne(l => l.Crop).HasForeignKey<CropDetails>(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SupplyDetailsConfiguration : IEntityTypeConfiguration<SupplyDetails>
{
    public void Configure(EntityTypeBuilder<SupplyDetails> b)
    {
        b.HasKey(x => x.ListingId);
        b.Property(x => x.Brand).HasMaxLength(48);
        b.HasOne(x => x.Listing).WithOne(l => l.Supply).HasForeignKey<SupplyDetails>(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> b)
    {
        b.HasKey(x => new { x.UserId, x.ListingId });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}

public class QurbaniBookingConfiguration : IEntityTypeConfiguration<QurbaniBooking>
{
    public void Configure(EntityTypeBuilder<QurbaniBooking> b)
    {
        b.Property(x => x.Note).HasMaxLength(300);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ListingId, x.Status });
        b.HasIndex(x => new { x.BuyerId, x.CreatedAtUtc });
        b.ToTable(t => t.HasCheckConstraint("CK_QurbaniBookings_Shares", "Shares BETWEEN 1 AND 7"));
    }
}
