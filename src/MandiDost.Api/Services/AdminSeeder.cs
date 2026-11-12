using MandiDost.Api.Auth;
using MandiDost.Api.Common;
using MandiDost.Api.Data;
using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MandiDost.Api.Services;

/// <summary>
/// Creates the first admin account from configuration (Seed:AdminPhone / Seed:AdminPassword),
/// so no credentials are ever hard-coded. Does nothing when those settings are absent.
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, CancellationToken ct = default)
    {
        var phone = PhoneNumber.Normalize(config["Seed:AdminPhone"]);
        var password = config["Seed:AdminPassword"];
        if (phone is null || string.IsNullOrWhiteSpace(password)) return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Users.AnyAsync(u => u.PhoneNumber == phone, ct)) return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var now = DateTime.UtcNow;
        var admin = new User
        {
            PhoneNumber = phone,
            FullName = config["Seed:AdminName"] ?? "Mandi Dost Admin",
            Role = UserRole.Admin,
            Verification = VerificationLevel.Phone,
            PhoneVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Profile = new UserProfile(),
        };
        admin.PasswordHash = hasher.Hash(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder")
            .LogInformation("Seeded admin user {UserId}", admin.Id);
    }
}
