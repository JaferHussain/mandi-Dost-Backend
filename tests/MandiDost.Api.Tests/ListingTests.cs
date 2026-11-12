using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MandiDost.Api.Common;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;

namespace MandiDost.Api.Tests;

public class ListingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // Smallest valid JPEG header – the server checks the first bytes, not the file name.
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00];

    private async Task<HttpClient> SellerAsync()
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync();
        return client.WithToken(auth.AccessToken);
    }

    private static object Bull(params string[] purposes) => new
    {
        categoryId = 1, locationId = 1, title = "ساہیوال بیل، تین دانت", price = 250_000, priceUnit = "perItem",
        quantity = 1, quantityUnit = "head", purposes,
        livestock = new { breed = "ساہیوال", gender = "male", ageMonths = 30, teeth = "four", liveWeightKg = 320 },
    };

    private static async Task<T> Read<T>(HttpResponseMessage res) => (await res.Content.ReadFromJsonAsync<T>(Json))!;

    private static async Task<ListingDetailDto> CreateAsync(HttpClient c, object body)
    {
        var res = await c.PostAsJsonAsync("/api/listings", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await Read<ListingDetailDto>(res);
    }

    private static async Task UploadPhotoAsync(HttpClient c, int id)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(JpegBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "bull.jpg");
        var res = await c.PostAsync($"/api/listings/{id}/media", form);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    private static async Task<ListingDetailDto> PublishedBullAsync(HttpClient c, params string[] purposes)
    {
        var l = await CreateAsync(c, Bull(purposes));
        await UploadPhotoAsync(c, l.Id);
        var res = await c.PostAsync($"/api/listings/{l.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await Read<ListingDetailDto>(res);
    }

    [Fact]
    public async Task Categories_cover_all_three_sections_with_rules()
    {
        var cats = await factory.CreateClient().GetFromJsonAsync<List<CategoryDto>>("/api/categories", Json);
        Assert.Equal(21, cats!.Count);
        var cow = cats.Single(c => c.Slug == "cow");
        Assert.Contains(ListingPurpose.Qurbani, cow.Purposes);
        Assert.Contains(PriceUnit.PerShare, cow.PriceUnits);
        Assert.True(cow.AllowsQurbaniShares);
        var wheat = cats.Single(c => c.Slug == "wheat");
        Assert.Equal(ProductSection.Crops, wheat.Section);
        Assert.Empty(wheat.Purposes);
        Assert.Equal(PriceUnit.PerMaund, wheat.DefaultPriceUnit);
        Assert.DoesNotContain(ListingPurpose.Qurbani, cats.Single(c => c.Slug == "other").Purposes);
    }

    [Fact]
    public async Task One_bull_appears_in_every_feed_it_was_listed_for()
    {
        var c = await SellerAsync();
        var bull = await PublishedBullAsync(c, "qurbani", "meatSupply", "general");
        Assert.Equal(ListingStatus.Active, bull.Status);
        Assert.NotNull(bull.Qurbani);     // created automatically for the Qurbani purpose
        Assert.NotNull(bull.MeatSupply);  // and for the meat-supply purpose

        var anon = factory.CreateClient();
        async Task<bool> InFeed(string query) =>
            (await anon.GetFromJsonAsync<PagedResult<ListingSummaryDto>>($"/api/listings?{query}&pageSize=50", Json))!
                .Items.Any(i => i.Id == bull.Id);

        Assert.True(await InFeed("purpose=qurbani"));
        Assert.True(await InFeed("purpose=meatSupply"));
        Assert.True(await InFeed("purpose=dairy,breeding,general"));
        Assert.False(await InFeed("purpose=dairy"));
        Assert.False(await InFeed("section=crops"));
        Assert.True(await InFeed("teeth=four&gender=male&minWeightKg=300"));
        Assert.False(await InFeed("minWeightKg=400"));
        Assert.True(await InFeed("q=ساہیوال"));
    }

    [Fact]
    public async Task Irrelevant_details_are_rejected()
    {
        var c = await SellerAsync();

        // Teeth on wheat
        var wheatWithTeeth = await c.PostAsJsonAsync("/api/listings", new
        {
            categoryId = 7, locationId = 1, title = "گندم اکبر", priceUnit = "perMaund", quantity = 200, quantityUnit = "maund",
            livestock = new { teeth = "two" },
        });
        Assert.Equal("details_mismatch", await wheatWithTeeth.ErrorCodeAsync());

        // Animal without a purpose
        var noPurpose = await c.PostAsJsonAsync("/api/listings", Bull());
        Assert.Equal("purpose_required", await noPurpose.ErrorCodeAsync());

        // Donkey for Qurbani
        var donkey = await c.PostAsJsonAsync("/api/listings", new
        {
            categoryId = 6, locationId = 1, title = "گدھا", priceUnit = "perItem", quantity = 1, quantityUnit = "head", purposes = new[] { "qurbani" },
        });
        Assert.Equal("purpose_not_allowed", await donkey.ErrorCodeAsync());

        // Milk from a male
        var milkBull = await c.PostAsJsonAsync("/api/listings", new
        {
            categoryId = 1, locationId = 1, title = "بیل", priceUnit = "perItem", quantity = 1, quantityUnit = "head", purposes = new[] { "dairy" },
            livestock = new { gender = "male", milkLitresPerDay = 10 },
        });
        Assert.Equal("details_mismatch", await milkBull.ErrorCodeAsync());

        // Seven shares on a goat
        var goatShares = await c.PostAsJsonAsync("/api/listings", new
        {
            categoryId = 3, locationId = 1, title = "بکرا", priceUnit = "perItem", quantity = 1, quantityUnit = "head", purposes = new[] { "qurbani" },
            qurbani = new { totalShares = 7 },
        });
        Assert.Equal("shares_not_allowed", await goatShares.ErrorCodeAsync());

        // Price per maund unit for a crop is fine, but "head" quantity is not
        var wheatHeads = await c.PostAsJsonAsync("/api/listings", new
        {
            categoryId = 7, locationId = 1, title = "گندم", priceUnit = "perMaund", quantity = 5, quantityUnit = "head",
        });
        Assert.Equal("unit_not_allowed", await wheatHeads.ErrorCodeAsync());
    }

    [Fact]
    public async Task Cow_can_be_sold_as_seven_qurbani_shares()
    {
        var c = await SellerAsync();
        var cow = await CreateAsync(c, new
        {
            categoryId = 1, locationId = 2, title = "قربانی کی گائے، سات حصے", price = 35_000, priceUnit = "perShare",
            quantity = 1, quantityUnit = "head", purposes = new[] { "qurbani" },
            livestock = new { gender = "female", teeth = "two" },
            qurbani = new { totalShares = 7, pricePerShare = 35_000 },
        });
        Assert.Equal(7, cow.Qurbani!.TotalShares);
        await UploadPhotoAsync(c, cow.Id);
        await c.PostAsync($"/api/listings/{cow.Id}/publish", null);

        var feed = await factory.CreateClient().GetFromJsonAsync<PagedResult<ListingSummaryDto>>("/api/listings?purpose=qurbani&shares=true", Json);
        Assert.Contains(feed!.Items, i => i.Id == cow.Id && i.QurbaniShares == 7);
    }

    [Fact]
    public async Task Crop_listing_is_created_and_found_in_crops_feed()
    {
        var c = await SellerAsync();
        var wheat = await CreateAsync(c, new
        {
            categoryId = 7, locationId = 3, title = "گندم اکبر 2019، صاف دانہ", price = 4_000, priceUnit = "perMaund",
            quantity = 300, quantityUnit = "maund", minOrderQuantity = 50, isBulkAvailable = true,
            crop = new { variety = "اکبر 2019", grade = "اول", harvestDate = "2026-04-20" },
        });
        Assert.Empty(wheat.Purposes);
        Assert.Null(wheat.Livestock);
        Assert.Equal("اکبر 2019", wheat.Crop!.Variety);

        // Crops can be published without a photo
        var pub = await c.PostAsync($"/api/listings/{wheat.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, pub.StatusCode);

        var feed = await factory.CreateClient().GetFromJsonAsync<PagedResult<ListingSummaryDto>>("/api/listings?section=crops&category=wheat&bulk=true", Json);
        var row = Assert.Single(feed!.Items, i => i.Id == wheat.Id);
        Assert.Equal("اکبر 2019", row.Variety);
        Assert.Equal(PriceUnit.PerMaund, row.PriceUnit);
    }

    [Fact]
    public async Task Animal_needs_a_photo_before_publishing_and_drafts_stay_private()
    {
        var c = await SellerAsync();
        var draft = await CreateAsync(c, Bull("general"));

        var publish = await c.PostAsync($"/api/listings/{draft.Id}/publish", null);
        Assert.Equal("image_required", await publish.ErrorCodeAsync());

        // Draft is invisible to others but visible to its owner
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/listings/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/listings/{draft.Id}")).StatusCode);

        var mine = await c.GetFromJsonAsync<PagedResult<ListingSummaryDto>>("/api/users/me/listings", Json);
        Assert.Contains(mine!.Items, i => i.Id == draft.Id && i.Status == ListingStatus.Draft);
    }

    [Fact]
    public async Task Fake_image_files_are_rejected()
    {
        var c = await SellerAsync();
        var l = await CreateAsync(c, Bull("general"));
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("<?php echo 'hi'; ?>"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg"); // lying content type
        form.Add(file, "file", "bull.jpg");
        var res = await c.PostAsync($"/api/listings/{l.Id}/media", form);
        Assert.Equal("invalid_image", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Uploaded_photo_is_served()
    {
        var c = await SellerAsync();
        var bull = await PublishedBullAsync(c, "general");
        var url = Assert.Single(bull.Media).Url;
        Assert.StartsWith("/media/listings/", url);
        var res = await factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("image/jpeg", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Only_the_owner_can_change_a_listing()
    {
        var owner = await SellerAsync();
        var bull = await PublishedBullAsync(owner, "general");
        var other = await SellerAsync();

        Assert.Equal("not_owner", await (await other.PutAsJsonAsync($"/api/listings/{bull.Id}", Bull("general"))).ErrorCodeAsync());
        Assert.Equal("not_owner", await (await other.PostAsync($"/api/listings/{bull.Id}/sold", null)).ErrorCodeAsync());
        Assert.Equal("not_owner", await (await other.DeleteAsync($"/api/listings/{bull.Id}")).ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/listings", Bull("general"))).StatusCode);
    }

    [Fact]
    public async Task Update_removes_details_of_unticked_purposes()
    {
        var c = await SellerAsync();
        var bull = await PublishedBullAsync(c, "qurbani", "meatSupply");
        var res = await c.PutAsJsonAsync($"/api/listings/{bull.Id}", Bull("general"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await Read<ListingDetailDto>(res);
        Assert.Equal([ListingPurpose.General], updated.Purposes);
        Assert.Null(updated.Qurbani);
        Assert.Null(updated.MeatSupply);
        Assert.Equal(ListingStatus.Active, updated.Status);
    }

    [Fact]
    public async Task Sold_and_removed_listings_leave_the_feed()
    {
        var c = await SellerAsync();
        var sold = await PublishedBullAsync(c, "general");
        var removed = await PublishedBullAsync(c, "general");
        await c.PostAsync($"/api/listings/{sold.Id}/sold", null);
        await c.DeleteAsync($"/api/listings/{removed.Id}");

        var feed = await factory.CreateClient().GetFromJsonAsync<PagedResult<ListingSummaryDto>>("/api/listings?pageSize=50", Json);
        Assert.DoesNotContain(feed!.Items, i => i.Id == sold.Id || i.Id == removed.Id);
        // A shared link to a sold animal still opens and says it's sold
        var soldView = await factory.CreateClient().GetFromJsonAsync<ListingDetailDto>($"/api/listings/{sold.Id}", Json);
        Assert.Equal(ListingStatus.Sold, soldView!.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/listings/{removed.Id}")).StatusCode);
    }

    [Fact]
    public async Task Nearby_search_sorts_by_distance()
    {
        var c = await SellerAsync();
        var inLodhran = await PublishedBullAsync(c, "breeding");
        var multan = await CreateAsync(c, new
        {
            categoryId = 1, locationId = 5, title = "ملتان کا بیل", priceUnit = "perItem", quantity = 1, quantityUnit = "head",
            purposes = new[] { "breeding" }, livestock = new { gender = "male" },
        });
        await UploadPhotoAsync(c, multan.Id);
        await c.PostAsync($"/api/listings/{multan.Id}/publish", null);

        // Standing in Lodhran, 100 km radius: Lodhran first, Multan (~70 km) second
        var feed = await factory.CreateClient().GetFromJsonAsync<PagedResult<ListingSummaryDto>>(
            "/api/listings?purpose=breeding&lat=29.5339&lng=71.6324&radiusKm=100&sort=nearest&pageSize=50", Json);
        var ids = feed!.Items.Select(i => i.Id).ToList();
        Assert.True(ids.IndexOf(inLodhran.Id) < ids.IndexOf(multan.Id));
        Assert.InRange(feed.Items.Single(i => i.Id == multan.Id).DistanceKm!.Value, 50, 90);

        // 20 km radius excludes Multan
        var near = await factory.CreateClient().GetFromJsonAsync<PagedResult<ListingSummaryDto>>(
            "/api/listings?purpose=breeding&lat=29.5339&lng=71.6324&radiusKm=20", Json);
        Assert.DoesNotContain(near!.Items, i => i.Id == multan.Id);
    }

    [Fact]
    public async Task Favorites_can_be_added_and_removed()
    {
        var seller = await SellerAsync();
        var bull = await PublishedBullAsync(seller, "general");
        var buyer = await SellerAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await buyer.PutAsync($"/api/users/me/favorites/{bull.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await buyer.PutAsync($"/api/users/me/favorites/{bull.Id}", null)).StatusCode); // twice is fine
        var view = await buyer.GetFromJsonAsync<ListingDetailDto>($"/api/listings/{bull.Id}", Json);
        Assert.True(view!.IsFavorite);
        var favs = await buyer.GetFromJsonAsync<PagedResult<ListingSummaryDto>>("/api/users/me/favorites", Json);
        Assert.Single(favs!.Items);

        await buyer.DeleteAsync($"/api/users/me/favorites/{bull.Id}");
        favs = await buyer.GetFromJsonAsync<PagedResult<ListingSummaryDto>>("/api/users/me/favorites", Json);
        Assert.Empty(favs!.Items);
    }

    [Fact]
    public async Task Buyer_views_are_counted_but_not_the_owners()
    {
        var c = await SellerAsync();
        var bull = await PublishedBullAsync(c, "general");
        await c.GetAsync($"/api/listings/{bull.Id}");
        await factory.CreateClient().GetAsync($"/api/listings/{bull.Id}");
        var view = await c.GetFromJsonAsync<ListingDetailDto>($"/api/listings/{bull.Id}", Json);
        Assert.Equal(1, view!.ViewCount);
    }
}
