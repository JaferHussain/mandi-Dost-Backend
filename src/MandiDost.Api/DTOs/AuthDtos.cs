using System.ComponentModel.DataAnnotations;
using MandiDost.Api.Common;

namespace MandiDost.Api.DTOs;

public class RegisterRequest
{
    [Required, StringLength(80, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required, PakistaniMobile]
    public string PhoneNumber { get; set; } = "";

    /// <summary>6–64 characters. A 6-digit PIN is allowed – easier for users who don't type much.</summary>
    [Required, StringLength(64, MinimumLength = 6)]
    public string Password { get; set; } = "";

    public int? LocationId { get; set; }

    /// <summary>ur (Urdu), pa (Punjabi), skr (Saraiki).</summary>
    [RegularExpression("^(ur|pa|skr|en)$")]
    public string? PreferredLanguage { get; set; }
}

public class LoginRequest
{
    [Required, PakistaniMobile]
    public string PhoneNumber { get; set; } = "";

    [Required, StringLength(64)]
    public string Password { get; set; } = "";
}

public class RefreshRequest
{
    [Required, StringLength(128)]
    public string RefreshToken { get; set; } = "";
}

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserDto User);
