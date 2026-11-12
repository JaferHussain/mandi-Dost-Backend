using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MandiDost.Api.Common;
using MandiDost.Api.DTOs;
using MandiDost.Api.Entities;

namespace MandiDost.Api.Tests;

public class PurchaseRequestTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private async Task<HttpClient> UserAsync(string name = "محمد اکرم")
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync(name: name);
        return client.WithToken(auth.AccessToken);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { phoneNumber = ApiFactory.AdminPhone, password = ApiFactory.AdminPassword });
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        return client.WithToken(auth.AccessToken);
    }

    private static async Task<T> Read<T>(HttpResponseMessage res) => (await res.Content.ReadFromJsonAsync<T>(Json))!;

    // A restaurant needing 10 goats of 25–30 kg every week in Lodhran
    private static object GoatsWeekly(long? targetPrice = 60_000) => new
    {
        categoryId = 3, purpose = "meatSupply", locationId = 1, quantity = 10, quantityUnit = "head", frequency = "weekly",
        minWeightKg = 25, maxWeightKg = 30, targetPrice, targetPriceUnit = targetPrice is null ? null : "perItem",
        deliveryNeeded = true, notes = "صبح 7 بجے سے پہلے",
    };

    private static async Task<PurchaseRequestDetailDto> PostAsync(HttpClient c, object body)
    {
        var res = await c.PostAsJsonAsync("/api/purchase-requests", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await Read<PurchaseRequestDetailDto>(res);
    }

    [Fact]
    public async Task Restaurant_sets_up_business_and_posts_a_weekly_requirement()
    {
        var hotel = await UserAsync("کاشف");
        var biz = await hotel.PutAsJsonAsync("/api/users/me/business", new
        {
            businessName = "لودھراں کڑاہی ہاؤس", type = "restaurant", locationId = 1, address = "ملتان روڈ", contactPhone = "0300-1112223",
        });
        Assert.Equal(HttpStatusCode.OK, biz.StatusCode);
        var business = await Read<BusinessDto>(biz);
        Assert.Equal(BusinessVerification.Unverified, business.Verification);
        Assert.Equal("0300-1112223", business.ContactPhone);

        var req = await PostAsync(hotel, GoatsWeekly());
        Assert.Equal("10 بکری / بکرا، 25–30 کلو، ہفتہ وار", req.Title); // written by the server
        Assert.Equal(RequestStatus.Open, req.Status);
        Assert.True(req.IsMine);
        Assert.Equal("0300-1112223", req.BuyerPhone);
        Assert.Equal("لودھراں کڑاہی ہاؤس", req.Buyer.BusinessName);

        var feed = await factory.CreateClient().GetFromJsonAsync<PagedResult<PurchaseRequestSummaryDto>>("/api/purchase-requests?purpose=meatSupply", Json);
        var row = Assert.Single(feed!.Items, i => i.Id == req.Id);
        Assert.Equal(SupplyFrequency.Weekly, row.Frequency);
        Assert.Equal(BusinessType.Restaurant, row.Buyer.BusinessType);

        // Strangers see the requirement but not the buyer's number
        var anon = await factory.CreateClient().GetFromJsonAsync<PurchaseRequestDetailDto>($"/api/purchase-requests/{req.Id}", Json);
        Assert.Null(anon!.BuyerPhone);
        Assert.Null(anon.Offers);
    }

    [Fact]
    public async Task Seller_offers_buyer_sees_it_with_number_and_accepts()
    {
        var hotel = await UserAsync();
        var req = await PostAsync(hotel, GoatsWeekly());
        var farmer = await UserAsync("اللہ دتہ");

        var offerRes = await farmer.PostAsJsonAsync($"/api/purchase-requests/{req.Id}/offers",
            new { price = 58_000, priceUnit = "perItem", quantity = 10, message = "بیتل بکرے، ہر ہفتے دے سکتا ہوں" });
        Assert.Equal(HttpStatusCode.OK, offerRes.StatusCode);
        var offer = await Read<OfferDto>(offerRes);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Null(offer.SellerPhone); // the seller doesn't need their own number back

        // After offering, the seller can see the buyer's number to talk
        var sellerView = await farmer.GetFromJsonAsync<PurchaseRequestDetailDto>($"/api/purchase-requests/{req.Id}", Json);
        Assert.NotNull(sellerView!.BuyerPhone);
        Assert.Equal(offer.Id, sellerView.MyOffer!.Id);
        Assert.Null(sellerView.Offers); // but not other sellers' offers

        // Changing the offer replaces it instead of adding a second one
        await farmer.PostAsJsonAsync($"/api/purchase-requests/{req.Id}/offers", new { price = 56_000, priceUnit = "perItem" });

        var buyerView = await hotel.GetFromJsonAsync<PurchaseRequestDetailDto>($"/api/purchase-requests/{req.Id}", Json);
        var seen = Assert.Single(buyerView!.Offers!);
        Assert.Equal(56_000, seen.Price);
        Assert.NotNull(seen.SellerPhone);
        Assert.Equal(1, buyerView.OfferCount);

        var accept = await hotel.PostAsync($"/api/offers/{seen.Id}/accept", null);
        Assert.Equal(OfferStatus.Accepted, (await Read<OfferDto>(accept)).Status);

        var mine = await farmer.GetFromJsonAsync<PagedResult<OfferDto>>("/api/users/me/offers", Json);
        Assert.Equal(OfferStatus.Accepted, Assert.Single(mine!.Items).Status);
    }

    [Fact]
    public async Task Offer_rules_are_enforced()
    {
        var hotel = await UserAsync();
        var req = await PostAsync(hotel, GoatsWeekly());

        Assert.Equal("own_request", await (await hotel.PostAsJsonAsync($"/api/purchase-requests/{req.Id}/offers", new { price = 1, priceUnit = "perItem" })).ErrorCodeAsync());

        var farmer = await UserAsync();
        Assert.Equal("validation_failed", await (await farmer.PostAsJsonAsync($"/api/purchase-requests/{req.Id}/offers", new { })).ErrorCodeAsync());
        Assert.Equal("unit_not_allowed", await (await farmer.PostAsJsonAsync($"/api/purchase-requests/{req.Id}/offers", new { price = 50_000 })).ErrorCodeAsync());
        // Someone else's listing can't be attached
        Assert.Equal("offer_not_allowed", await (await farmer.PostAsJsonAsync($"/api/purchase-requests/{req.Id}/offers", new { listingId = 999_999 })).ErrorCodeAsync());

        // Another user can't accept offers on this requirement
        var offer = await Read<OfferDto>(await farmer.PostAsJsonAsync($"/api/purchase-requests/{req.Id}/offers", new { message = "رابطہ کریں" }));
        Assert.Equal("not_owner", await (await farmer.PostAsync($"/api/offers/{offer.Id}/accept", null)).ErrorCodeAsync());

        // Closed requirement takes no more offers
        await hotel.PostAsync($"/api/purchase-requests/{req.Id}/close?fulfilled=true", null);
        var late = await UserAsync();
        Assert.Equal("request_closed", await (await late.PostAsJsonAsync($"/api/purchase-requests/{req.Id}/offers", new { message = "میرے پاس ہیں" })).ErrorCodeAsync());
        // and leaves the public list
        var feed = await factory.CreateClient().GetFromJsonAsync<PagedResult<PurchaseRequestSummaryDto>>("/api/purchase-requests?pageSize=50", Json);
        Assert.DoesNotContain(feed!.Items, i => i.Id == req.Id);
    }

    [Fact]
    public async Task Withdrawn_offer_lowers_the_count_and_hides_numbers()
    {
        var hotel = await UserAsync();
        var req = await PostAsync(hotel, GoatsWeekly());
        var farmer = await UserAsync();
        var offer = await Read<OfferDto>(await farmer.PostAsJsonAsync($"/api/purchase-requests/{req.Id}/offers", new { price = 55_000, priceUnit = "perItem" }));

        var withdrawn = await Read<OfferDto>(await farmer.PostAsync($"/api/offers/{offer.Id}/withdraw", null));
        Assert.Equal(OfferStatus.Withdrawn, withdrawn.Status);

        var buyerView = await hotel.GetFromJsonAsync<PurchaseRequestDetailDto>($"/api/purchase-requests/{req.Id}", Json);
        Assert.Equal(0, buyerView!.OfferCount);
        Assert.Null(Assert.Single(buyerView.Offers!).SellerPhone);
        var sellerView = await farmer.GetFromJsonAsync<PurchaseRequestDetailDto>($"/api/purchase-requests/{req.Id}", Json);
        Assert.Null(sellerView!.BuyerPhone);
        Assert.Equal("offer_not_allowed", await (await hotel.PostAsync($"/api/offers/{offer.Id}/accept", null)).ErrorCodeAsync());
    }

    [Fact]
    public async Task Requirement_details_must_fit_the_category()
    {
        var c = await UserAsync();
        // Weight on wheat
        var wheatWeight = await c.PostAsJsonAsync("/api/purchase-requests", new
        {
            categoryId = 7, locationId = 1, quantity = 500, quantityUnit = "maund", minWeightKg = 20,
        });
        Assert.Equal("details_mismatch", await wheatWeight.ErrorCodeAsync());
        // Donkey for Qurbani
        var donkey = await c.PostAsJsonAsync("/api/purchase-requests", new { categoryId = 6, purpose = "qurbani", locationId = 1, quantity = 1, quantityUnit = "head" });
        Assert.Equal("purpose_not_allowed", await donkey.ErrorCodeAsync());
        // Animals counted in maunds
        var maund = await c.PostAsJsonAsync("/api/purchase-requests", new { categoryId = 3, locationId = 1, quantity = 5, quantityUnit = "maund" });
        Assert.Equal("unit_not_allowed", await maund.ErrorCodeAsync());
        // Target price without unit
        var noUnit = await c.PostAsJsonAsync("/api/purchase-requests", new { categoryId = 7, locationId = 1, quantity = 500, quantityUnit = "maund", targetPrice = 4000 });
        Assert.Equal("unit_not_allowed", await noUnit.ErrorCodeAsync());

        // A valid wheat requirement gets a sensible title
        var wheat = await PostAsync(c, new { categoryId = 7, locationId = 3, quantity = 500, quantityUnit = "maund", frequency = "oneTime" });
        Assert.Equal("گندم 500 من، ایک بار", wheat.Title);
        Assert.Null(wheat.Purpose);
    }

    [Fact]
    public async Task Business_verification_goes_through_an_admin()
    {
        var butcher = await UserAsync();
        await butcher.PutAsJsonAsync("/api/users/me/business", new { businessName = "حاجی قصاب", type = "butcher", locationId = 2 });
        var pending = await Read<BusinessDto>(await butcher.PostAsync("/api/users/me/business/verification", null));
        Assert.Equal(BusinessVerification.Pending, pending.Verification);

        // Normal users can't verify
        Assert.Equal(HttpStatusCode.Forbidden, (await butcher.PostAsJsonAsync($"/api/admin/businesses/{pending.Id}/verification", new { approve = true })).StatusCode);

        var admin = await AdminAsync();
        var queue = await admin.GetFromJsonAsync<List<BusinessDto>>("/api/admin/businesses/pending", Json);
        Assert.Contains(queue!, b => b.Id == pending.Id);
        var verified = await Read<BusinessDto>(await admin.PostAsJsonAsync($"/api/admin/businesses/{pending.Id}/verification", new { approve = true, note = "دکان دیکھی" }));
        Assert.Equal(BusinessVerification.Verified, verified.Verification);

        // Renaming the business removes the badge until it's checked again
        var renamed = await Read<BusinessDto>(await butcher.PutAsJsonAsync("/api/users/me/business", new { businessName = "حاجی قصاب اینڈ سنز", type = "butcher", locationId = 2 }));
        Assert.Equal(BusinessVerification.Unverified, renamed.Verification);
    }

    [Fact]
    public async Task No_business_profile_returns_204()
    {
        var c = await UserAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await c.GetAsync("/api/users/me/business")).StatusCode);
    }

    [Fact]
    public async Task Buyer_can_close_and_reopen_and_list_own_requirements()
    {
        var c = await UserAsync();
        var req = await PostAsync(c, GoatsWeekly(targetPrice: null));
        var closed = await Read<PurchaseRequestDetailDto>(await c.PostAsync($"/api/purchase-requests/{req.Id}/close?fulfilled=false", null));
        Assert.Equal(RequestStatus.Closed, closed.Status);
        var reopened = await Read<PurchaseRequestDetailDto>(await c.PostAsync($"/api/purchase-requests/{req.Id}/reopen", null));
        Assert.Equal(RequestStatus.Open, reopened.Status);

        var mine = await c.GetFromJsonAsync<PagedResult<PurchaseRequestSummaryDto>>("/api/users/me/purchase-requests", Json);
        Assert.Contains(mine!.Items, i => i.Id == req.Id);
        Assert.Equal("not_owner", await (await (await UserAsync()).PostAsync($"/api/purchase-requests/{req.Id}/close?fulfilled=true", null)).ErrorCodeAsync());
    }
}
