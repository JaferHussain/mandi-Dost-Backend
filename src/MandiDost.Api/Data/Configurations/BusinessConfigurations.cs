using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MandiDost.Api.Data.Configurations;

public class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> b)
    {
        b.Property(x => x.BusinessName).HasMaxLength(80).IsRequired();
        b.Property(x => x.Address).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.ContactPhone).HasMaxLength(16);
        b.Property(x => x.VerificationNote).HasMaxLength(300);
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => new { x.Verification, x.VerificationRequestedAtUtc });
        b.HasOne(x => x.User).WithOne(u => u.Business).HasForeignKey<BusinessProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.VerifiedById).OnDelete(DeleteBehavior.ClientSetNull); // one cascading path from Users only (SQL Server rule)
    }
}

public class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> b)
    {
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.Quantity).HasPrecision(12, 2);
        b.HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        // Seller-facing list of open requirements, newest first
        b.HasIndex(x => new { x.Status, x.ExpiresAtUtc, x.CreatedAtUtc });
        b.HasIndex(x => new { x.Status, x.CategoryId });
        b.HasIndex(x => new { x.BuyerId, x.CreatedAtUtc });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_PurchaseRequests_Quantity", "Quantity > 0");
            t.HasCheckConstraint("CK_PurchaseRequests_Weight", "MinWeightKg IS NULL OR MaxWeightKg IS NULL OR MinWeightKg <= MaxWeightKg");
        });
    }
}

public class RequestOfferConfiguration : IEntityTypeConfiguration<RequestOffer>
{
    public void Configure(EntityTypeBuilder<RequestOffer> b)
    {
        b.Property(x => x.Message).HasMaxLength(500);
        b.Property(x => x.Quantity).HasPrecision(12, 2);
        b.HasOne(x => x.PurchaseRequest).WithMany(r => r.Offers).HasForeignKey(x => x.PurchaseRequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.SetNull);
        // One offer per seller per request
        b.HasIndex(x => new { x.PurchaseRequestId, x.SellerId }).IsUnique();
        b.HasIndex(x => new { x.SellerId, x.UpdatedAtUtc });
    }
}
