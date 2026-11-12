using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MandiDost.Api.Services;

public interface IReferenceDataService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default);
    Task<AdminStatsDto> GetAdminStatsAsync(CancellationToken ct = default);
}

/// <summary>Categories and locations rarely change, so they are cached in memory for an hour.</summary>
public class ReferenceDataService(AppDbContext db, IMemoryCache cache) : IReferenceDataService
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync("ref:categories", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheFor;
            var cats = await db.Categories.AsNoTracking().Include(c => c.Purposes)
                .Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync(ct); // SortOrder groups livestock, crops, supplies
            return (IReadOnlyList<CategoryDto>)cats.Select(c => c.ToDto()).ToList();
        }))!;

    public async Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync("ref:locations", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheFor;
            return await db.Locations.AsNoTracking().Where(l => l.IsActive)
                .OrderByDescending(l => l.IsLaunchArea).ThenBy(l => l.Id)
                .Select(l => new LocationDto(l.Id, l.Slug, l.NameUr, l.NameEn, l.DistrictUr, l.Latitude, l.Longitude, l.IsLaunchArea))
                .ToListAsync(ct);
        }))!;

    public async Task<AdminStatsDto> GetAdminStatsAsync(CancellationToken ct = default) => new(
        await db.Users.CountAsync(ct),
        await db.Listings.CountAsync(a => a.Status == ListingStatus.Active, ct),
        await db.Listings.CountAsync(a => a.Status == ListingStatus.Sold, ct),
        await db.Reports.CountAsync(r => r.Status == ReportStatus.Open, ct));
}
