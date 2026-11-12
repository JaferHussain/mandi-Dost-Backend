using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MandiDost.Api.Auth;

public class JwtOptions : IValidatableObject
{
    public const string Section = "Jwt";

    [Required] public string Issuer { get; set; } = "MandiDost";
    [Required] public string Audience { get; set; } = "MandiDost.App";

    /// <summary>HMAC-SHA256 key, at least 32 bytes. Never commit a production key – use user-secrets or env vars.</summary>
    [Required] public string SigningKey { get; set; } = "";

    [Range(5, 1440)] public int AccessTokenMinutes { get; set; } = 30;
    [Range(1, 180)] public int RefreshTokenDays { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Encoding.UTF8.GetByteCount(SigningKey) < 32)
            yield return new ValidationResult("Jwt:SigningKey must be at least 32 bytes.", [nameof(SigningKey)]);
    }
}
