namespace MandiDost.Api.Entities;

public class User
{
    public int Id { get; set; }

    /// <summary>E.164 format, e.g. +923001234567. Unique login identifier.</summary>
    public string PhoneNumber { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";

    public UserRole Role { get; set; } = UserRole.User;
    public VerificationLevel Verification { get; set; } = VerificationLevel.None;
    public bool PhoneVerified { get; set; }
    public bool IsActive { get; set; } = true;

    // Brute-force protection
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public UserProfile? Profile { get; set; }
    public BusinessProfile? Business { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Listing> Listings { get; set; } = [];
}

/// <summary>Public-facing seller details, 1:1 with User.</summary>
public class UserProfile
{
    public int UserId { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public int? LocationId { get; set; }
    public string? WhatsAppNumber { get; set; }

    /// <summary>When false, buyers must use in-app chat instead of seeing the number.</summary>
    public bool ShowPhoneNumber { get; set; } = true;
    public string PreferredLanguage { get; set; } = "ur";

    // Denormalised counters for fast profile pages (updated by services).
    public int FollowerCount { get; set; }
    public int SoldCount { get; set; }
    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }

    public User User { get; set; } = null!;
    public Location? Location { get; set; }
}

/// <summary>Rotating refresh token. Only a SHA-256 hash of the token is stored.</summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }

    public User User { get; set; } = null!;

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
}
