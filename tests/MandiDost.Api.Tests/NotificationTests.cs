using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;
using MandiDost.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MandiDost.Api.Tests;

public class NotificationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    // Locations from the seed data: 1 Lodhran, 7 Bahawalpur (~16 km), 5 Multan (~70 km), 9 Vehari (~90 km)
    private const int Lodhran = 1, Bahawalpur = 7, Multan = 5, Vehari = 9;

    private async Task<(HttpClient Client, int Id)> UserAsync(int locationId = Lodhran, string name = "محمد اکرم")
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync(name: name);
        client.WithToken(auth.AccessToken);
        if (locationId != Lodhran) (await client.PatchAsJsonAsync("/api/users/me", new { locationId })).EnsureSuccessStatusCode();
        return (client, auth.User.Id);
    }

    private static async Task<T> Read<T>(HttpResponseMessage res) => (await res.Content.ReadFromJsonAsync<T>(Json))!;

    private static async Task<NotificationListDto> InboxAsync(HttpClient c, string query = "") =>
        await Read<NotificationListDto>(await c.GetAsync("/api/notifications" + query));

    private static async Task<int> PublishAsync(HttpClient seller, int locationId = Lodhran, int categoryId = 1, string title = "ساہیوال گائے")
    {
        var res = await seller.PostAsJsonAsync("/api/listings", new
        {
            categoryId, locationId, title, price = 90_000, priceUnit = "perItem", quantity = 1, quantityUnit = "head",
            purposes = new[] { "general" }, livestock = new { gender = "female" },
        });
        res.EnsureSuccessStatusCode();
        var id = (await Read<ListingDetailDto>(res)).Id;
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Jpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "cow.jpg");
        (await seller.PostAsync($"/api/listings/{id}/media", form)).EnsureSuccessStatusCode();
        (await seller.PostAsync($"/api/listings/{id}/publish", null)).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<int> PostRequestAsync(HttpClient buyer, int locationId = Lodhran, int categoryId = 3)
    {
        var res = await buyer.PostAsJsonAsync("/api/purchase-requests", new
        {
            categoryId, purpose = "meatSupply", locationId, title = "ہوٹل کے لیے 5 بکرے", quantity = 5, quantityUnit = "head",
            frequency = "weekly", targetPrice = 60_000, targetPriceUnit = "perItem",
        });
        res.EnsureSuccessStatusCode();
        return (await Read<PurchaseRequestDetailDto>(res)).Id;
    }

    // ---- Nearby listing alerts -----------------------------------------------------

    [Fact]
    public async Task New_listing_alerts_people_nearby_but_not_the_seller_and_not_those_too_far()
    {
        var (seller, sellerId) = await UserAsync();
        var (near, _) = await UserAsync(Bahawalpur);   // ~16 km, default radius 30 km
        var (far, _) = await UserAsync(Vehari);        // ~90 km, outside the default radius

        var id = await PublishAsync(seller);

        var got = await InboxAsync(near);
        var n = Assert.Single(got.Items, x => x.ListingId == id);
        Assert.Equal(NotificationType.NearbyListing, n.Type);
        Assert.Equal("ساہیوال گائے", n.Subject);
        Assert.Equal("لودھراں", n.Place);
        Assert.InRange(n.DistanceKm!.Value, 10, 25);
        Assert.False(n.IsRead);
        Assert.DoesNotContain(( await InboxAsync(seller)).Items, x => x.ListingId == id);
        Assert.DoesNotContain((await InboxAsync(far)).Items, x => x.ListingId == id);
        Assert.NotEqual(0, sellerId);
    }

    [Fact]
    public async Task A_wider_radius_reaches_further_and_renewing_a_listing_does_not_alert_again()
    {
        var (seller, _) = await UserAsync();
        var (far, _) = await UserAsync(Vehari);
        (await far.PutAsJsonAsync("/api/users/me/notification-settings", new { enabled = true, wantListings = true, wantRequests = true, radiusKm = 120, categoryIds = Array.Empty<int>() }))
            .EnsureSuccessStatusCode();

        var id = await PublishAsync(seller);
        Assert.Single((await InboxAsync(far)).Items, x => x.ListingId == id);

        (await seller.PostAsync($"/api/listings/{id}/publish", null)).EnsureSuccessStatusCode(); // renew
        Assert.Single((await InboxAsync(far)).Items, x => x.ListingId == id);
    }

    [Fact]
    public async Task Alerts_respect_off_switch_kind_and_chosen_categories()
    {
        var (seller, _) = await UserAsync();
        var (off, _) = await UserAsync(Bahawalpur);
        var (noListings, _) = await UserAsync(Bahawalpur);
        var (onlyBuffalo, _) = await UserAsync(Bahawalpur);
        var (onlyCow, _) = await UserAsync(Bahawalpur);

        await off.PutAsJsonAsync("/api/users/me/notification-settings", new { enabled = false, wantListings = true, wantRequests = true, radiusKm = 30, categoryIds = Array.Empty<int>() });
        await noListings.PutAsJsonAsync("/api/users/me/notification-settings", new { enabled = true, wantListings = false, wantRequests = true, radiusKm = 30, categoryIds = Array.Empty<int>() });
        await onlyBuffalo.PutAsJsonAsync("/api/users/me/notification-settings", new { enabled = true, wantListings = true, wantRequests = true, radiusKm = 30, categoryIds = new[] { 2 } });
        await onlyCow.PutAsJsonAsync("/api/users/me/notification-settings", new { enabled = true, wantListings = true, wantRequests = true, radiusKm = 30, categoryIds = new[] { 1 } });

        var id = await PublishAsync(seller, categoryId: 1);

        Assert.DoesNotContain((await InboxAsync(off)).Items, x => x.ListingId == id);
        Assert.DoesNotContain((await InboxAsync(noListings)).Items, x => x.ListingId == id);
        Assert.DoesNotContain((await InboxAsync(onlyBuffalo)).Items, x => x.ListingId == id);
        Assert.Contains((await InboxAsync(onlyCow)).Items, x => x.ListingId == id);
    }

    [Fact]
    public async Task Nobody_gets_more_than_the_daily_limit_of_nearby_alerts()
    {
        var (seller, _) = await UserAsync();
        var (busy, busyId) = await UserAsync(Bahawalpur);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < NotificationService.DailyNearbyLimit; i++)
                db.Notifications.Add(new Notification
                {
                    UserId = busyId, Type = NotificationType.NearbyListing, Subject = "پرانا", DedupeKey = $"fill{i}", CreatedAtUtc = DateTime.UtcNow,
                });
            await db.SaveChangesAsync();
        }

        var id = await PublishAsync(seller);
        Assert.DoesNotContain((await InboxAsync(busy, "?pageSize=50")).Items, x => x.ListingId == id);
    }

    // ---- Nearby requirement alerts ----------------------------------------------------

    [Fact]
    public async Task New_requirement_alerts_nearby_sellers()
    {
        var (buyer, _) = await UserAsync();
        var (seller, _) = await UserAsync(Bahawalpur);
        var (farAway, _) = await UserAsync(Vehari);

        var id = await PostRequestAsync(buyer);

        var n = Assert.Single((await InboxAsync(seller)).Items, x => x.RequestId == id);
        Assert.Equal(NotificationType.NearbyRequest, n.Type);
        Assert.Equal("ہوٹل کے لیے 5 بکرے", n.Subject);
        Assert.DoesNotContain((await InboxAsync(buyer)).Items, x => x.RequestId == id);
        Assert.DoesNotContain((await InboxAsync(farAway)).Items, x => x.RequestId == id);
    }

    // ---- Personal alerts ---------------------------------------------------------------

    [Fact]
    public async Task Offers_tell_the_buyer_and_the_answer_tells_the_seller()
    {
        var (buyer, _) = await UserAsync();
        var (seller, _) = await UserAsync(Multan);
        var requestId = await PostRequestAsync(buyer);

        var offer = await Read<OfferDto>(await seller.PostAsJsonAsync($"/api/purchase-requests/{requestId}/offers",
            new { price = 58_000, priceUnit = "perItem", message = "لوہی بکرے" }));

        var got = Assert.Single((await InboxAsync(buyer)).Items, x => x.Type == NotificationType.OfferReceived);
        Assert.Equal(requestId, got.RequestId);
        Assert.Equal(58_000, got.Amount);

        // Changing the offer does not alert the buyer a second time
        await seller.PostAsJsonAsync($"/api/purchase-requests/{requestId}/offers", new { price = 57_000, priceUnit = "perItem" });
        Assert.Single((await InboxAsync(buyer)).Items, x => x.Type == NotificationType.OfferReceived);

        (await buyer.PostAsync($"/api/offers/{offer.Id}/accept", null)).EnsureSuccessStatusCode();
        Assert.Single((await InboxAsync(seller)).Items, x => x.Type == NotificationType.OfferAccepted && x.RequestId == requestId);
    }

    [Fact]
    public async Task Share_bookings_tell_the_seller_and_the_decision_tells_the_buyer()
    {
        var (seller, _) = await UserAsync();
        var (buyer, _) = await UserAsync();
        var res = await seller.PostAsJsonAsync("/api/listings", new
        {
            categoryId = 1, locationId = Lodhran, title = "سات حصے", price = 40_000, priceUnit = "perShare", quantity = 1, quantityUnit = "head",
            purposes = new[] { "qurbani" }, livestock = new { gender = "female", teeth = "two" }, qurbani = new { totalShares = 7, pricePerShare = 40_000 },
        });
        var listingId = (await Read<ListingDetailDto>(res)).Id;
        using (var form = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Jpeg);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "file", "cow.jpg");
            (await seller.PostAsync($"/api/listings/{listingId}/media", form)).EnsureSuccessStatusCode();
        }
        (await seller.PostAsync($"/api/listings/{listingId}/publish", null)).EnsureSuccessStatusCode();

        var booking = await Read<BookingDto>(await buyer.PostAsJsonAsync($"/api/listings/{listingId}/bookings", new { shares = 2 }));
        var asked = Assert.Single((await InboxAsync(seller)).Items, x => x.Type == NotificationType.BookingRequested);
        Assert.Equal(2, asked.Count);
        Assert.Equal(listingId, asked.ListingId);

        (await seller.PostAsync($"/api/bookings/{booking.Id}/confirm", null)).EnsureSuccessStatusCode();
        Assert.Single((await InboxAsync(buyer)).Items, x => x.Type == NotificationType.BookingConfirmed);
    }

    // ---- Reading ----------------------------------------------------------------------------

    [Fact]
    public async Task Unread_count_polling_and_marking_as_read()
    {
        var (seller, _) = await UserAsync();
        var (reader, _) = await UserAsync(Bahawalpur);

        await PublishAsync(seller, title: "پہلا");
        var first = await InboxAsync(reader);
        Assert.Equal(1, first.UnreadCount);
        var lastSeen = first.Items[0].Id;

        await PublishAsync(seller, title: "دوسرا");
        var fresh = await InboxAsync(reader, $"?afterId={lastSeen}");
        Assert.Equal("دوسرا", Assert.Single(fresh.Items).Subject); // polling returns only what is new
        Assert.Equal(2, fresh.UnreadCount);
        Assert.Equal(2, await (await reader.GetAsync("/api/notifications/unread-count")).Content.ReadFromJsonAsync<int>());

        Assert.Equal(HttpStatusCode.NoContent, (await reader.PostAsync($"/api/notifications/{lastSeen}/read", null)).StatusCode);
        Assert.Equal(1, (await InboxAsync(reader)).UnreadCount);
        Assert.Equal(HttpStatusCode.NoContent, (await reader.PostAsync("/api/notifications/read-all", null)).StatusCode);
        var all = await InboxAsync(reader);
        Assert.Equal(0, all.UnreadCount);
        Assert.All(all.Items, x => Assert.True(x.IsRead));
    }

    [Fact]
    public async Task Nobody_can_read_or_mark_someone_elses_notifications()
    {
        var (seller, _) = await UserAsync();
        var (reader, _) = await UserAsync(Bahawalpur);
        var (stranger, _) = await UserAsync(Bahawalpur);
        await PublishAsync(seller);
        var id = (await InboxAsync(reader)).Items[0].Id;

        Assert.Equal("notification_not_found", await (await stranger.PostAsync($"/api/notifications/{id}/read", null)).ErrorCodeAsync());
        Assert.Equal(1, (await InboxAsync(reader)).UnreadCount);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/notifications")).StatusCode);
    }

    // ---- Settings ------------------------------------------------------------------------------

    [Fact]
    public async Task Settings_default_save_and_validate()
    {
        var (user, _) = await UserAsync();
        var defaults = await Read<NotificationSettingsDto>(await user.GetAsync("/api/users/me/notification-settings"));
        Assert.True(defaults.Enabled);
        Assert.True(defaults.WantListings);
        Assert.True(defaults.WantRequests);
        Assert.Equal(30, defaults.RadiusKm);
        Assert.Empty(defaults.CategoryIds);

        var saved = await Read<NotificationSettingsDto>(await user.PutAsJsonAsync("/api/users/me/notification-settings",
            new { enabled = true, wantListings = false, wantRequests = true, radiusKm = 60, categoryIds = new[] { 3, 2, 999 } }));
        Assert.False(saved.WantListings);
        Assert.Equal(60, saved.RadiusKm);
        Assert.Equal([2, 3], saved.CategoryIds); // unknown category 999 is dropped
        Assert.Equal(60, (await Read<NotificationSettingsDto>(await user.GetAsync("/api/users/me/notification-settings"))).RadiusKm);

        Assert.Equal(HttpStatusCode.BadRequest, (await user.PutAsJsonAsync("/api/users/me/notification-settings",
            new { enabled = true, wantListings = true, wantRequests = true, radiusKm = 2, categoryIds = Array.Empty<int>() })).StatusCode);
    }
}
