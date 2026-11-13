using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MandiDost.Api.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(x => x.Subject).HasMaxLength(160).IsRequired();
        b.Property(x => x.Place).HasMaxLength(80);
        b.Property(x => x.DedupeKey).HasMaxLength(48).IsRequired();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // Listings and requirements are only ever soft-deleted, so no database cascade is needed.
        b.HasOne<Listing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.ClientSetNull);
        b.HasOne<PurchaseRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.ClientSetNull);
        b.HasIndex(x => new { x.UserId, x.Id });
        b.HasIndex(x => new { x.UserId, x.DedupeKey }).IsUnique();
    }
}

public class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> b)
    {
        b.HasKey(x => x.UserId);
        b.Property(x => x.InterestCategoryIds).HasMaxLength(200);
        b.HasOne(x => x.User).WithOne().HasForeignKey<NotificationSettings>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
