using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MandiDost.Api.Data.Configurations;

public class ChatConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> b)
    {
        b.HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.BuyerId, x.LastMessageAtUtc });
        b.HasIndex(x => new { x.SellerId, x.LastMessageAtUtc });
        b.HasIndex(x => new { x.BuyerId, x.SellerId, x.ListingId });
        b.ToTable(t => t.HasCheckConstraint("CK_Chats_DifferentParties", "BuyerId <> SellerId"));
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.Property(x => x.Text).HasMaxLength(2000);
        b.Property(x => x.MediaUrl).HasMaxLength(512);
        b.HasOne(x => x.Chat).WithMany(c => c.Messages).HasForeignKey(x => x.ChatId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ChatId, x.CreatedAtUtc });
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.Property(x => x.Text).HasMaxLength(1000);
        b.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.SellerId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.ReviewerId, x.SellerId, x.ListingId });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Reviews_Rating", "Rating BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Reviews_NotSelf", "ReviewerId <> SellerId");
        });
    }
}

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> b)
    {
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.ResolutionNote).HasMaxLength(500);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        b.HasIndex(x => new { x.ListingId, x.ReporterId });
    }
}
