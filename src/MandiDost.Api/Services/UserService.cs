using MandiDost.Api.Auth;
using MandiDost.Api.Common;
using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MandiDost.Api.Services;

public interface IUserService
{
    Task<UserDto> GetMeAsync(int userId, CancellationToken ct = default);
    Task<UserDto> UpdateMeAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default);
}

public class UserService(AppDbContext db, IPasswordService passwords, TimeProvider clock) : IUserService
{
    public async Task<UserDto> GetMeAsync(int userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.Profile).ThenInclude(p => p!.Location)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct)
            ?? throw new NotFoundException(ErrorCodes.UserNotFound, "User not found.");
        return user.ToDto();
    }

    public async Task<UserDto> UpdateMeAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct)
            ?? throw new NotFoundException(ErrorCodes.UserNotFound, "User not found.");
        var profile = user.Profile ??= new Entities.UserProfile { UserId = userId };

        if (request.LocationId is { } locId && !await db.Locations.AnyAsync(l => l.Id == locId && l.IsActive, ct))
            throw new BadRequestException(ErrorCodes.LocationNotFound, "Unknown location.");

        // Only fields that were sent are changed.
        if (request.FullName is not null) user.FullName = request.FullName.Trim();
        if (request.Bio is not null) profile.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        if (request.LocationId is not null) profile.LocationId = request.LocationId;
        if (request.WhatsAppNumber is not null) profile.WhatsAppNumber = PhoneNumber.Normalize(request.WhatsAppNumber);
        if (request.ShowPhoneNumber is not null) profile.ShowPhoneNumber = request.ShowPhoneNumber.Value;
        if (request.PreferredLanguage is not null) profile.PreferredLanguage = request.PreferredLanguage;
        user.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        return await GetMeAsync(userId, ct);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct)
            ?? throw new NotFoundException(ErrorCodes.UserNotFound, "User not found.");

        if (passwords.Verify(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new BadRequestException(ErrorCodes.WrongPassword, "Current password is incorrect.");
        if (passwords.IsTooWeak(request.NewPassword))
            throw new BadRequestException("weak_password", "This password is too easy to guess. Choose a different one.");

        var now = clock.GetUtcNow().UtcDateTime;
        user.PasswordHash = passwords.Hash(user, request.NewPassword);
        user.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);

        // Sign out every device so a stolen session can't continue.
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }
}
