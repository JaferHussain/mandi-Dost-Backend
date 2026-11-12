using MandiDost.Api.Auth;
using MandiDost.Api.Common;
using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MandiDost.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default);
    Task LogoutAsync(int userId, string refreshToken, CancellationToken ct = default);
}

public class AuthService(
    AppDbContext db,
    IPasswordService passwords,
    ITokenService tokens,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct = default)
    {
        var phone = PhoneNumber.Normalize(request.PhoneNumber)
            ?? throw new BadRequestException(ErrorCodes.Validation, "Invalid phone number.");

        if (passwords.IsTooWeak(request.Password))
            throw new BadRequestException("weak_password", "This password is too easy to guess. Choose a different one.");

        if (await db.Users.AnyAsync(u => u.PhoneNumber == phone, ct))
            throw new ConflictException(ErrorCodes.PhoneTaken, "An account with this phone number already exists.");

        if (request.LocationId is { } locId && !await db.Locations.AnyAsync(l => l.Id == locId && l.IsActive, ct))
            throw new BadRequestException(ErrorCodes.LocationNotFound, "Unknown location.");

        var user = new User
        {
            PhoneNumber = phone,
            FullName = request.FullName.Trim(),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
            LastLoginAtUtc = Now,
            Profile = new UserProfile { LocationId = request.LocationId, PreferredLanguage = request.PreferredLanguage ?? "ur" },
        };
        user.PasswordHash = passwords.Hash(user, request.Password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two sign-ups with the same number at the same moment – the unique index caught it.
            db.ChangeTracker.Clear();
            if (await db.Users.AnyAsync(u => u.PhoneNumber == phone, ct))
                throw new ConflictException(ErrorCodes.PhoneTaken, "An account with this phone number already exists.");
            throw;
        }

        logger.LogInformation("User {UserId} registered", user.Id);
        return await IssueTokensAsync(user, ip, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default)
    {
        var phone = PhoneNumber.Normalize(request.PhoneNumber);
        var user = phone is null ? null : await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone, ct);

        if (user is null)
        {
            passwords.SimulateVerify(request.Password); // same timing as a real check
            throw InvalidCredentials();
        }

        if (user.LockoutEndUtc is { } until && until > Now)
            throw new ForbiddenException(ErrorCodes.AccountLocked,
                $"Too many wrong attempts. Try again after {Math.Ceiling((until - Now).TotalMinutes)} minutes.");

        var result = passwords.Verify(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLogins)
            {
                user.LockoutEndUtc = Now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                logger.LogWarning("User {UserId} locked out after repeated failed logins", user.Id);
            }
            await db.SaveChangesAsync(ct);
            throw InvalidCredentials();
        }

        if (!user.IsActive)
            throw new ForbiddenException(ErrorCodes.AccountDisabled, "This account has been disabled.");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwords.Hash(user, request.Password);

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = Now;
        return await IssueTokensAsync(user, ip, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var hash = tokens.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw InvalidRefresh();

        if (stored.RevokedAtUtc is not null)
        {
            // A revoked token was used again: it was probably stolen. End every session of this user.
            logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking all sessions", stored.UserId);
            await RevokeAllAsync(stored.UserId, ct);
            throw InvalidRefresh();
        }

        if (stored.ExpiresAtUtc <= Now) throw InvalidRefresh();
        if (!stored.User.IsActive) throw new ForbiddenException(ErrorCodes.AccountDisabled, "This account has been disabled.");

        // Rotate: the old token can never be used again.
        var (raw, newHash, expires) = tokens.CreateRefreshToken();
        stored.RevokedAtUtc = Now;
        stored.ReplacedByTokenHash = newHash;
        db.RefreshTokens.Add(new RefreshToken { UserId = stored.UserId, TokenHash = newHash, CreatedAtUtc = Now, ExpiresAtUtc = expires, CreatedByIp = ip });
        await db.SaveChangesAsync(ct);

        var user = await LoadUserAsync(stored.UserId, ct);
        var (access, accessExpires) = tokens.CreateAccessToken(user);
        return new AuthResponse(access, accessExpires, raw, expires, user.ToDto());
    }

    public async Task LogoutAsync(int userId, string refreshToken, CancellationToken ct = default)
    {
        var hash = tokens.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);
        if (stored is { RevokedAtUtc: null })
        {
            stored.RevokedAtUtc = Now;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, string? ip, CancellationToken ct)
    {
        var (raw, hash, expires) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = hash, CreatedAtUtc = Now, ExpiresAtUtc = expires, CreatedByIp = ip });

        // Housekeeping: drop this user's tokens that expired more than a week ago.
        var cutoff = Now.AddDays(-7);
        await db.RefreshTokens.Where(t => t.UserId == user.Id && t.ExpiresAtUtc < cutoff).ExecuteDeleteAsync(ct);

        await db.SaveChangesAsync(ct);
        var full = await LoadUserAsync(user.Id, ct);
        var (access, accessExpires) = tokens.CreateAccessToken(full);
        return new AuthResponse(access, accessExpires, raw, expires, full.ToDto());
    }

    private Task<User> LoadUserAsync(int id, CancellationToken ct) =>
        db.Users.AsNoTracking().Include(u => u.Profile).ThenInclude(p => p!.Location).FirstAsync(u => u.Id == id, ct);

    internal async Task RevokeAllAsync(int userId, CancellationToken ct)
    {
        var now = Now;
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }

    private static UnauthorizedException InvalidCredentials() =>
        new(ErrorCodes.InvalidCredentials, "Phone number or password is incorrect.");

    private static UnauthorizedException InvalidRefresh() =>
        new(ErrorCodes.InvalidRefreshToken, "Session expired. Please log in again.");
}
