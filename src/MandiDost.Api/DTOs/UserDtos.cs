using System.ComponentModel.DataAnnotations;
using MandiDost.Api.Common;

namespace MandiDost.Api.DTOs;

/// <summary>The signed-in user's own account (includes private fields like the phone number).</summary>
public record UserDto(
    int Id,
    string FullName,
    string PhoneNumber,
    string Role,
    string Verification,
    bool PhoneVerified,
    ProfileDto Profile,
    DateTime CreatedAtUtc);

public record ProfileDto(
    string? Bio,
    string? AvatarUrl,
    LocationDto? Location,
    string? WhatsAppNumber,
    bool ShowPhoneNumber,
    string PreferredLanguage,
    int FollowerCount,
    int SoldCount,
    double RatingAverage,
    int RatingCount);

public class UpdateProfileRequest
{
    [StringLength(80, MinimumLength = 2)]
    public string? FullName { get; set; }

    [StringLength(500)]
    public string? Bio { get; set; }

    public int? LocationId { get; set; }

    [PakistaniMobile]
    public string? WhatsAppNumber { get; set; }

    public bool? ShowPhoneNumber { get; set; }

    [RegularExpression("^(ur|pa|skr|en)$", ErrorMessage = "Language must be ur, pa, skr or en.")]
    public string? PreferredLanguage { get; set; }
}

public class ChangePasswordRequest
{
    [Required, StringLength(64)]
    public string CurrentPassword { get; set; } = "";

    [Required, StringLength(64, MinimumLength = 6)]
    public string NewPassword { get; set; } = "";
}
