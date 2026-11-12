using System.Net;
using System.Net.Http.Json;
using MandiDost.Api.Common;
using MandiDost.Api.DTOs;

namespace MandiDost.Api.Tests;

public class UserAndReferenceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Me_requires_login()
    {
        var res = await factory.CreateClient().GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_rejects_a_tampered_token()
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync();
        client.WithToken(auth.AccessToken[..^4] + "AAAA");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Me_returns_and_updates_profile()
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync();
        client.WithToken(auth.AccessToken);

        var me = await client.GetFromJsonAsync<UserDto>("/api/users/me");
        Assert.Equal(auth.User.Id, me!.Id);
        Assert.Equal("skr", me.Profile.PreferredLanguage);

        var res = await client.PatchAsJsonAsync("/api/users/me",
            new { bio = "بیتل بکرے، لودھراں", locationId = 3, whatsAppNumber = "0301-2223334", preferredLanguage = "ur" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = (await res.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal("بیتل بکرے، لودھراں", updated.Profile.Bio);
        Assert.Equal("kahror", updated.Profile.Location?.Slug);
        Assert.Equal("0301-2223334", updated.Profile.WhatsAppNumber);
        Assert.Equal("ur", updated.Profile.PreferredLanguage);
        Assert.Equal(me.FullName, updated.FullName); // untouched field stays the same
    }

    [Fact]
    public async Task Changing_password_signs_out_other_sessions()
    {
        var client = factory.CreateClient();
        var phone = TestHelpers.NewPhone();
        var auth = await client.RegisterAsync(phone);
        client.WithToken(auth.AccessToken);

        var wrong = await client.PutAsJsonAsync("/api/users/me/password", new { currentPassword = "nope-nope", newPassword = "Gaye#2027" });
        Assert.Equal("wrong_password", await wrong.ErrorCodeAsync());

        var res = await client.PutAsJsonAsync("/api/users/me/password", new { currentPassword = "Bakra#2026", newPassword = "Gaye#2027" });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var oldSession = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { phoneNumber = phone, password = "Gaye#2027" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Categories_and_locations_are_public_and_seeded()
    {
        var client = factory.CreateClient();
        var cats = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/categories");
        var locs = await client.GetFromJsonAsync<List<LocationDto>>("/api/locations");

        // Livestock comes first with the same slugs as the first release; crops and supplies follow.
        Assert.Equal(["cow", "buffalo", "goat", "sheep", "camel", "other"], cats!.Take(6).Select(c => c.GetProperty("slug").GetString()));
        Assert.Equal("livestock", cats[0].GetProperty("section").GetString());
        Assert.Equal("crops", cats[6].GetProperty("section").GetString());
        Assert.Equal(10, locs!.Count);
        Assert.True(locs.Take(3).All(l => l.IsLaunchArea)); // Lodhran district first
    }

    [Fact]
    public async Task Admin_stats_are_for_admins_only()
    {
        var client = factory.CreateClient();
        var user = await client.RegisterAsync();
        client.WithToken(user.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/stats")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { phoneNumber = ApiFactory.AdminPhone, password = ApiFactory.AdminPassword });
        var admin = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Equal("Admin", admin.User.Role);
        client.WithToken(admin.AccessToken);
        var stats = await client.GetFromJsonAsync<AdminStatsDto>("/api/admin/stats");
        Assert.True(stats!.Users >= 2);
    }

    [Fact]
    public async Task Health_endpoint_reports_database_ok()
    {
        var res = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Healthy", await res.Content.ReadAsStringAsync());
    }
}

public class PhoneNumberTests
{
    [Theory]
    [InlineData("03001234567", "+923001234567")]
    [InlineData("0300-1234567", "+923001234567")]
    [InlineData("+92 300 1234567", "+923001234567")]
    [InlineData("923001234567", "+923001234567")]
    [InlineData("00923001234567", "+923001234567")]
    [InlineData("3001234567", "+923001234567")]
    [InlineData("0300123456", null)]   // too short
    [InlineData("04212345678", null)]  // landline
    [InlineData("", null)]
    public void Normalize_handles_common_pakistani_formats(string input, string? expected) =>
        Assert.Equal(expected, PhoneNumber.Normalize(input));
}
