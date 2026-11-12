using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;

namespace MandiDost.Api.Tests;

public class CommunityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    // Minimal MP4/M4A header ("....ftypM4A ")
    private static readonly byte[] M4a = [0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41, 0x20, 0x00, 0x00];

    private async Task<(HttpClient Client, int Id)> UserAsync(string name = "محمد اکرم")
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync(name: name);
        return (client.WithToken(auth.AccessToken), auth.User.Id);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { phoneNumber = ApiFactory.AdminPhone, password = ApiFactory.AdminPassword });
        return client.WithToken((await res.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private static async Task<T> Read<T>(HttpResponseMessage res) => (await res.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>A published cow, optionally sold as 7 Qurbani shares.</summary>
    private static async Task<int> PublishCowAsync(HttpClient seller, bool shares = false)
    {
        var res = await seller.PostAsJsonAsync("/api/listings", new
        {
            categoryId = 1, locationId = 1, title = "قربانی کی گائے", price = shares ? 40_000 : 280_000, priceUnit = shares ? "perShare" : "perItem",
            quantity = 1, quantityUnit = "head", purposes = new[] { "qurbani" },
            livestock = new { gender = "female", teeth = "two" },
            qurbani = new { totalShares = shares ? 7 : 1, pricePerShare = shares ? 40_000 : (int?)null },
        });
        var listing = await Read<ListingDetailDto>(res);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Jpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "cow.jpg");
        (await seller.PostAsync($"/api/listings/{listing.Id}/media", form)).EnsureSuccessStatusCode();
        (await seller.PostAsync($"/api/listings/{listing.Id}/publish", null)).EnsureSuccessStatusCode();
        return listing.Id;
    }

    // ---- Chat ----

    [Fact]
    public async Task Buyer_and_seller_chat_about_a_listing()
    {
        var (seller, sellerId) = await UserAsync("بیچنے والا");
        var (buyer, _) = await UserAsync("خریدار");
        var cowId = await PublishCowAsync(seller);

        var chat = await Read<ChatSummaryDto>(await buyer.PostAsJsonAsync("/api/chats", new { listingId = cowId }));
        Assert.Equal(sellerId, chat.Other.Id);
        Assert.Equal(cowId, chat.ListingId);
        // Starting again returns the same conversation
        Assert.Equal(chat.Id, (await Read<ChatSummaryDto>(await buyer.PostAsJsonAsync("/api/chats", new { listingId = cowId }))).Id);

        var first = await Read<MessageDto>(await buyer.PostAsJsonAsync($"/api/chats/{chat.Id}/messages", new { text = "گائے ابھی دستیاب ہے؟" }));
        Assert.True(first.IsMine);

        Assert.Equal(1, await seller.GetFromJsonAsync<int>("/api/chats/unread-count"));
        var sellerChats = await seller.GetFromJsonAsync<List<ChatSummaryDto>>("/api/chats", Json);
        Assert.Equal("گائے ابھی دستیاب ہے؟", Assert.Single(sellerChats!).LastMessagePreview);

        var opened = await seller.GetFromJsonAsync<ChatDetailDto>($"/api/chats/{chat.Id}", Json);
        Assert.Single(opened!.Messages);
        Assert.Equal(0, await seller.GetFromJsonAsync<int>("/api/chats/unread-count")); // opening marks read

        await seller.PostAsJsonAsync($"/api/chats/{chat.Id}/messages", new { text = "جی ہاں، آ جائیں" });
        // The buyer polls for new messages only
        var poll = await buyer.GetFromJsonAsync<ChatDetailDto>($"/api/chats/{chat.Id}?afterId={first.Id}", Json);
        var reply = Assert.Single(poll!.Messages);
        Assert.False(reply.IsMine);
        Assert.Equal("جی ہاں، آ جائیں", reply.Text);
    }

    [Fact]
    public async Task Outsiders_cannot_read_a_chat_and_nobody_chats_with_themselves()
    {
        var (seller, _) = await UserAsync();
        var (buyer, _) = await UserAsync();
        var (stranger, _) = await UserAsync();
        var cowId = await PublishCowAsync(seller);
        var chat = await Read<ChatSummaryDto>(await buyer.PostAsJsonAsync("/api/chats", new { listingId = cowId }));

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/chats/{chat.Id}")).StatusCode);
        Assert.Equal("chat_not_found", await (await stranger.PostAsJsonAsync($"/api/chats/{chat.Id}/messages", new { text = "ہیلو" })).ErrorCodeAsync());
        Assert.Equal("chat_with_self", await (await seller.PostAsJsonAsync("/api/chats", new { listingId = cowId })).ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/chats")).StatusCode);
    }

    [Fact]
    public async Task Voice_messages_are_checked_and_served()
    {
        var (seller, _) = await UserAsync();
        var (buyer, _) = await UserAsync();
        var chat = await Read<ChatSummaryDto>(await buyer.PostAsJsonAsync("/api/chats", new { listingId = await PublishCowAsync(seller) }));

        async Task<HttpResponseMessage> SendAsync(byte[] bytes, int seconds)
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("audio/mp4");
            form.Add(file, "file", "voice.m4a");
            form.Add(new StringContent(seconds.ToString()), "durationSeconds");
            return await buyer.PostAsync($"/api/chats/{chat.Id}/voice", form);
        }

        Assert.Equal("invalid_audio", await (await SendAsync("not audio at all"u8.ToArray(), 5)).ErrorCodeAsync());
        Assert.Equal("invalid_audio", await (await SendAsync(M4a, 0)).ErrorCodeAsync());

        var ok = await SendAsync(M4a, 7);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var msg = await Read<MessageDto>(ok);
        Assert.Equal(MessageType.Voice, msg.Type);
        Assert.Equal(7, msg.DurationSeconds);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync(msg.MediaUrl)).StatusCode);
    }

    // ---- Qurbani shares ----

    [Fact]
    public async Task Shares_are_held_confirmed_and_freed_without_overbooking()
    {
        var (seller, _) = await UserAsync();
        var (buyerA, _) = await UserAsync("احمد");
        var (buyerB, _) = await UserAsync("بلال");
        var cowId = await PublishCowAsync(seller, shares: true);

        var a = await Read<BookingDto>(await buyerA.PostAsJsonAsync($"/api/listings/{cowId}/bookings", new { shares = 3, note = "تین بھائی" }));
        Assert.Equal(BookingStatus.Requested, a.Status);

        // 3 held, so 5 is too many
        Assert.Equal("not_enough_shares", await (await buyerB.PostAsJsonAsync($"/api/listings/{cowId}/bookings", new { shares = 5 })).ErrorCodeAsync());
        var b = await Read<BookingDto>(await buyerB.PostAsJsonAsync($"/api/listings/{cowId}/bookings", new { shares = 4 }));

        var pub = await factory.CreateClient().GetFromJsonAsync<SharesDto>($"/api/listings/{cowId}/shares", Json);
        Assert.Equal(0, pub!.AvailableShares);
        Assert.Null(pub.AllBookings); // strangers don't see who booked

        // Seller sees both with phone numbers and confirms one
        var sellerView = await seller.GetFromJsonAsync<SharesDto>($"/api/listings/{cowId}/shares", Json);
        Assert.Equal(2, sellerView!.AllBookings!.Count);
        Assert.All(sellerView.AllBookings, x => Assert.NotNull(x.BuyerPhone));
        Assert.Equal(BookingStatus.Confirmed, (await Read<BookingDto>(await seller.PostAsync($"/api/bookings/{a.Id}/confirm", null))).Status);

        // Buyers can't confirm, and can't book their own… nor someone else's booking can be cancelled by a stranger
        Assert.Equal("not_owner", await (await buyerA.PostAsync($"/api/bookings/{b.Id}/confirm", null)).ErrorCodeAsync());
        Assert.Equal("not_owner", await (await buyerA.PostAsync($"/api/bookings/{b.Id}/cancel", null)).ErrorCodeAsync());
        Assert.Equal("own_request", await (await seller.PostAsJsonAsync($"/api/listings/{cowId}/bookings", new { shares = 1 })).ErrorCodeAsync());

        // B cancels: 4 shares free again
        await buyerB.PostAsync($"/api/bookings/{b.Id}/cancel", null);
        var after = await buyerA.GetFromJsonAsync<SharesDto>($"/api/listings/{cowId}/shares", Json);
        Assert.Equal(3, after!.ConfirmedShares);
        Assert.Equal(4, after.AvailableShares);
        Assert.Single(after.MyBookings);

        var mine = await buyerA.GetFromJsonAsync<List<BookingDto>>("/api/users/me/bookings", Json);
        Assert.Equal(BookingStatus.Confirmed, Assert.Single(mine!).Status);
    }

    [Fact]
    public async Task Whole_animal_listing_has_no_shares()
    {
        var (seller, _) = await UserAsync();
        var (buyer, _) = await UserAsync();
        var cowId = await PublishCowAsync(seller, shares: false);
        Assert.Equal("no_shares", await (await buyer.PostAsJsonAsync($"/api/listings/{cowId}/bookings", new { shares = 1 })).ErrorCodeAsync());
    }

    // ---- Reviews and profile ----

    [Fact]
    public async Task Only_people_who_dealt_with_a_seller_can_review()
    {
        var (seller, sellerId) = await UserAsync("حاجی صاحب");
        var (buyer, _) = await UserAsync();
        var cowId = await PublishCowAsync(seller);

        Assert.Equal("review_not_allowed", await (await buyer.PostAsJsonAsync($"/api/users/{sellerId}/reviews", new { rating = 5 })).ErrorCodeAsync());
        Assert.False((await buyer.GetFromJsonAsync<PublicProfileDto>($"/api/users/{sellerId}", Json))!.CanReview);

        // They talk – both write
        var chat = await Read<ChatSummaryDto>(await buyer.PostAsJsonAsync("/api/chats", new { listingId = cowId }));
        await buyer.PostAsJsonAsync($"/api/chats/{chat.Id}/messages", new { text = "قیمت؟" });
        await seller.PostAsJsonAsync($"/api/chats/{chat.Id}/messages", new { text = "دو لاکھ اسی ہزار" });

        Assert.True((await buyer.GetFromJsonAsync<PublicProfileDto>($"/api/users/{sellerId}", Json))!.CanReview);
        var review = await buyer.PostAsJsonAsync($"/api/users/{sellerId}/reviews", new { rating = 4, text = "ایماندار آدمی", listingId = cowId });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        // Writing again replaces, doesn't add
        await buyer.PostAsJsonAsync($"/api/users/{sellerId}/reviews", new { rating = 5, text = "بہت اچھا سودا" });

        var profile = await factory.CreateClient().GetFromJsonAsync<PublicProfileDto>($"/api/users/{sellerId}", Json);
        Assert.Equal(5, profile!.RatingAverage);
        Assert.Equal(1, profile.RatingCount);
        Assert.Equal(1, profile.ActiveListings);
        var reviews = await factory.CreateClient().GetFromJsonAsync<List<ReviewDto>>($"/api/users/{sellerId}/reviews", Json);
        Assert.Equal("بہت اچھا سودا", Assert.Single(reviews!).Text);

        Assert.Equal("review_not_allowed", await (await seller.PostAsJsonAsync($"/api/users/{sellerId}/reviews", new { rating = 5 })).ErrorCodeAsync());
    }

    // ---- Reports ----

    [Fact]
    public async Task Three_reports_hide_a_listing_and_a_moderator_decides()
    {
        var (seller, _) = await UserAsync();
        var cowId = await PublishCowAsync(seller);

        var (r1, _) = await UserAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await r1.PostAsJsonAsync($"/api/listings/{cowId}/reports", new { reason = "advancePaymentScam", note = "پیشگی پیسے مانگ رہا ہے" })).StatusCode);
        Assert.Equal("already_reported", await (await r1.PostAsJsonAsync($"/api/listings/{cowId}/reports", new { reason = "other" })).ErrorCodeAsync());
        for (var i = 0; i < 2; i++)
        {
            var (r, _) = await UserAsync();
            await r.PostAsJsonAsync($"/api/listings/{cowId}/reports", new { reason = "wrongMedia" });
        }

        // Hidden from buyers now
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/listings/{cowId}")).StatusCode);

        var admin = await AdminAsync();
        var open = await admin.GetFromJsonAsync<List<ReportDto>>("/api/admin/reports", Json);
        var report = open!.First(x => x.ListingId == cowId);
        Assert.Equal(3, report.OpenReportsOnListing);
        Assert.Equal(HttpStatusCode.Forbidden, (await r1.GetAsync("/api/admin/reports")).StatusCode);

        // Moderator decides it's fine: dismissing all three brings it back
        foreach (var x in open.Where(x => x.ListingId == cowId))
            await admin.PostAsJsonAsync($"/api/admin/reports/{x.Id}/resolve", new { hideListing = false, note = "جانور دیکھ لیا، ٹھیک ہے" });
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync($"/api/listings/{cowId}")).StatusCode);
    }
}
