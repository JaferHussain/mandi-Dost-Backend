using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MandiDost.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.PhoneNumber).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.PhoneNumber).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.CreatedAtUtc);

        b.HasOne(x => x.Profile).WithOne(p => p.User).HasForeignKey<UserProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.HasKey(x => x.UserId);
        b.Property(x => x.Bio).HasMaxLength(500);
        b.Property(x => x.AvatarUrl).HasMaxLength(512);
        b.Property(x => x.WhatsAppNumber).HasMaxLength(16);
        b.Property(x => x.PreferredLanguage).HasMaxLength(8);
        b.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> b)
    {
        b.Property(x => x.Slug).HasMaxLength(48).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.NameUr).HasMaxLength(64).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(64).IsRequired();
        b.Property(x => x.DistrictUr).HasMaxLength(64);
        b.Property(x => x.Province).HasMaxLength(32);
    }
}
