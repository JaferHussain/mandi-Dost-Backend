using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MandiDost.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<CategoryPurpose> CategoryPurposes => Set<CategoryPurpose>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingPurposeLink> ListingPurposes => Set<ListingPurposeLink>();
    public DbSet<ListingMedia> ListingMedia => Set<ListingMedia>();
    public DbSet<LivestockDetails> LivestockDetails => Set<LivestockDetails>();
    public DbSet<QurbaniDetails> QurbaniDetails => Set<QurbaniDetails>();
    public DbSet<MeatSupplyDetails> MeatSupplyDetails => Set<MeatSupplyDetails>();
    public DbSet<CropDetails> CropDetails => Set<CropDetails>();
    public DbSet<SupplyDetails> SupplyDetails => Set<SupplyDetails>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<RequestOffer> RequestOffers => Set<RequestOffer>();
    public DbSet<QurbaniBooking> QurbaniBookings => Set<QurbaniBooking>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        SeedData.Apply(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums as readable strings
        builder.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<VerificationLevel>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<ProductSection>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<ListingPurpose>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<AnimalGender>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<TeethStage>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<PriceUnit>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<QuantityUnit>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<SupplyFrequency>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<ItemCondition>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<MediaKind>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<BusinessType>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<BusinessVerification>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<RequestStatus>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<OfferStatus>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<BookingStatus>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<ListingStatus>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<MessageType>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<ReportReason>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<ReportStatus>().HaveConversion<string>().HaveMaxLength(16);

        // All timestamps are UTC; datetime2 doesn't store DateTimeKind, so restore it on read.
        builder.Properties<DateTime>().HaveConversion<UtcConverter>();
        builder.Properties<DateTime?>().HaveConversion<NullableUtcConverter>();
    }

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
