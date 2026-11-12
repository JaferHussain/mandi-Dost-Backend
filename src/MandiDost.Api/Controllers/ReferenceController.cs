using MandiDost.Api.Auth;
using MandiDost.Api.DTOs;
using MandiDost.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MandiDost.Api.Controllers;

[ApiController]
[Route("api")]
public class ReferenceController(IReferenceDataService data) : ControllerBase
{
    /// <summary>Animal categories (cow, buffalo, goat…). Public, cacheable.</summary>
    [HttpGet("categories")]
    [ResponseCache(Duration = 3600)]
    public async Task<IReadOnlyList<CategoryDto>> Categories(CancellationToken ct) => await data.GetCategoriesAsync(ct);

    /// <summary>Supported cities, launch area (Lodhran district) first.</summary>
    [HttpGet("locations")]
    [ResponseCache(Duration = 3600)]
    public async Task<IReadOnlyList<LocationDto>> Locations(CancellationToken ct) => await data.GetLocationsAsync(ct);
}

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthConstants.AdminPolicy)]
public class AdminController(IReferenceDataService data) : ControllerBase
{
    /// <summary>Basic platform numbers. Admins and moderators only.</summary>
    [HttpGet("stats")]
    public async Task<AdminStatsDto> Stats(CancellationToken ct) => await data.GetAdminStatsAsync(ct);
}
