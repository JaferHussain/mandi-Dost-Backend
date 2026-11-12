using MandiDost.Api.Common;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;

namespace MandiDost.Api.Services;

public static class Mapping
{
    public static LocationDto ToDto(this Location l) =>
        new(l.Id, l.Slug, l.NameUr, l.NameEn, l.DistrictUr, l.Latitude, l.Longitude, l.IsLaunchArea);

    /// <summary>Requires Purposes to be loaded.</summary>
    public static CategoryDto ToDto(this Category c) => new(
        c.Id, c.ParentId, c.Section, c.Slug, c.NameUr, c.NameEn, c.IconKey,
        c.HasMilkProduction, c.AllowsQurbaniShares, c.DefaultPriceUnit,
        c.Purposes.Select(p => p.Purpose).Order().ToList(),
        ListingRules.PriceUnitsFor(c),
        ListingRules.QuantityUnitsFor(c.Section));

    public static SellerSummaryDto ToSummary(this User u) => new(
        u.Id, u.FullName, u.Verification.ToString(), u.PhoneVerified,
        Math.Round(u.Profile?.RatingAverage ?? 0, 1), u.Profile?.RatingCount ?? 0);

    public static MediaDto ToDto(this ListingMedia m) => new(m.Id, m.Kind, LocalMediaStorage.UrlFor(m.Path), m.SortOrder);

    /// <summary>An Active listing past its expiry date is reported as Expired.</summary>
    public static ListingStatus EffectiveStatus(this Listing l, DateTime nowUtc) =>
        l.Status == ListingStatus.Active && l.ExpiresAtUtc <= nowUtc ? ListingStatus.Expired : l.Status;

    /// <summary>Requires Profile and Profile.Location to be loaded.</summary>
    public static UserDto ToDto(this User u)
    {
        var p = u.Profile ?? new UserProfile();
        return new UserDto(
            u.Id,
            u.FullName,
            PhoneNumber.ToLocal(u.PhoneNumber),
            u.Role.ToString(),
            u.Verification.ToString(),
            u.PhoneVerified,
            new ProfileDto(
                p.Bio, p.AvatarUrl, p.Location?.ToDto(),
                p.WhatsAppNumber is null ? null : PhoneNumber.ToLocal(p.WhatsAppNumber),
                p.ShowPhoneNumber, p.PreferredLanguage, p.FollowerCount, p.SoldCount,
                Math.Round(p.RatingAverage, 1), p.RatingCount),
            u.CreatedAtUtc);
    }
}
