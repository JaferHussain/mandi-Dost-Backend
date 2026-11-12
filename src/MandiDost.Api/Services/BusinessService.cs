using MandiDost.Api.Common;
using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MandiDost.Api.Services;

public interface IBusinessService
{
    Task<BusinessDto?> GetMineAsync(int userId, CancellationToken ct = default);
    Task<BusinessDto> SaveMineAsync(int userId, BusinessInput input, CancellationToken ct = default);
    Task<BusinessDto> RequestVerificationAsync(int userId, CancellationToken ct = default);
    Task<BusinessDto> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessDto>> GetPendingVerificationAsync(CancellationToken ct = default);
    Task<BusinessDto> DecideVerificationAsync(int adminId, int businessId, VerificationDecision decision, CancellationToken ct = default);
}

public class BusinessService(AppDbContext db, TimeProvider clock) : IBusinessService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<BusinessDto?> GetMineAsync(int userId, CancellationToken ct = default)
    {
        var b = await Query().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        return b is null ? null : await ToDtoAsync(b, ct);
    }

    /// <summary>Create or update the user's business. Changing the name or type after verification asks for a new check.</summary>
    public async Task<BusinessDto> SaveMineAsync(int userId, BusinessInput input, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == input.LocationId && l.IsActive, ct))
            throw new NotFoundException(ErrorCodes.LocationNotFound, "Location not found.");

        var now = Now;
        var b = await db.BusinessProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (b is null)
        {
            b = new BusinessProfile { UserId = userId, CreatedAtUtc = now };
            db.BusinessProfiles.Add(b);
        }
        else if (b.Verification == BusinessVerification.Verified && (b.BusinessName != input.BusinessName.Trim() || b.Type != input.Type))
        {
            // A verified badge belongs to the details that were checked.
            b.Verification = BusinessVerification.Unverified;
            b.VerifiedAtUtc = null;
            b.VerifiedById = null;
        }

        b.BusinessName = input.BusinessName.Trim();
        b.Type = input.Type;
        b.LocationId = input.LocationId;
        b.Address = Clean(input.Address);
        b.Description = Clean(input.Description);
        b.ContactPhone = input.ContactPhone is null ? null : PhoneNumber.Normalize(input.ContactPhone);
        b.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return (await GetMineAsync(userId, ct))!;
    }

    public async Task<BusinessDto> RequestVerificationAsync(int userId, CancellationToken ct = default)
    {
        var b = await db.BusinessProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct)
            ?? throw new NotFoundException(ErrorCodes.BusinessNotFound, "Add your business details first.");
        if (b.Verification is BusinessVerification.Unverified or BusinessVerification.Rejected)
        {
            b.Verification = BusinessVerification.Pending;
            b.VerificationRequestedAtUtc = Now;
            b.VerificationNote = null;
            await db.SaveChangesAsync(ct);
        }
        return (await GetMineAsync(userId, ct))!;
    }

    public async Task<BusinessDto> GetAsync(int id, CancellationToken ct = default)
    {
        var b = await Query().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(ErrorCodes.BusinessNotFound, "Business not found.");
        return await ToDtoAsync(b, ct);
    }

    public async Task<IReadOnlyList<BusinessDto>> GetPendingVerificationAsync(CancellationToken ct = default)
    {
        var list = await Query().Where(x => x.Verification == BusinessVerification.Pending)
            .OrderBy(x => x.VerificationRequestedAtUtc).Take(100).ToListAsync(ct);
        var result = new List<BusinessDto>();
        foreach (var b in list) result.Add(await ToDtoAsync(b, ct));
        return result;
    }

    public async Task<BusinessDto> DecideVerificationAsync(int adminId, int businessId, VerificationDecision decision, CancellationToken ct = default)
    {
        var b = await db.BusinessProfiles.FirstOrDefaultAsync(x => x.Id == businessId, ct)
            ?? throw new NotFoundException(ErrorCodes.BusinessNotFound, "Business not found.");
        b.Verification = decision.Approve ? BusinessVerification.Verified : BusinessVerification.Rejected;
        b.VerifiedAtUtc = decision.Approve ? Now : null;
        b.VerifiedById = adminId;
        b.VerificationNote = Clean(decision.Note);
        b.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
        return await GetAsync(businessId, ct);
    }

    private IQueryable<BusinessProfile> Query() =>
        db.BusinessProfiles.AsNoTracking().Include(x => x.Location).Include(x => x.User).ThenInclude(u => u.Profile);

    private async Task<BusinessDto> ToDtoAsync(BusinessProfile b, CancellationToken ct)
    {
        var now = Now;
        var open = await db.PurchaseRequests.CountAsync(r => r.BuyerId == b.UserId && r.Status == RequestStatus.Open && r.ExpiresAtUtc > now, ct);
        var phone = b.ContactPhone ?? (b.User.Profile is { ShowPhoneNumber: true } ? b.User.PhoneNumber : null);
        return new BusinessDto(b.Id, b.UserId, b.BusinessName, b.Type, b.Location.ToDto(), b.Address, b.Description,
            phone is null ? null : PhoneNumber.ToLocal(phone), b.Verification, b.VerifiedAtUtc, b.CreatedAtUtc, open);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
