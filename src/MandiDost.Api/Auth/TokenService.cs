using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MandiDost.Api.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MandiDost.Api.Auth;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user);
    (string RawToken, string Hash, DateTime ExpiresAtUtc) CreateRefreshToken();
    string HashRefreshToken(string rawToken);
}

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_opt.AccessTokenMinutes);
        // Keep the token small and free of personal data (no phone number).
        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            [AuthConstants.RoleClaim] = user.Role.ToString(),
            [AuthConstants.NameClaim] = user.FullName,
        };
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _opt.Issuer,
            Audience = _opt.Audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(SigningKey(_opt), SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }

    public (string RawToken, string Hash, DateTime ExpiresAtUtc) CreateRefreshToken()
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        return (raw, HashRefreshToken(raw), clock.GetUtcNow().UtcDateTime.AddDays(_opt.RefreshTokenDays));
    }

    public string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    public static SymmetricSecurityKey SigningKey(JwtOptions opt) => new(Encoding.UTF8.GetBytes(opt.SigningKey));
}

public static class AuthConstants
{
    public const string RoleClaim = "role";
    public const string NameClaim = "name";
    public const string AuthRateLimitPolicy = "auth";
    public const string AdminPolicy = "AdminOrModerator";
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>The authenticated user's id (from the "sub" claim).</summary>
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing user id claim.");

    /// <summary>The signed-in user's id, or null for anonymous visitors (public endpoints).</summary>
    public static int? TryGetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
