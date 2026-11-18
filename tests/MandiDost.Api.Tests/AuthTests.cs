using System.Net;
using System.Net.Http.Json;
using MandiDost.Api.DTOs;

namespace MandiDost.Api.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_creates_account_and_returns_tokens()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "حاجی اسلم", phoneNumber = "+92 300 7654321", password = "Bakra#2026", locationId = 1, preferredLanguage = "pa" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
        Assert.False(string.IsNullOrEmpty(body.RefreshToken));
        Assert.Equal("0300-7654321", body.User.PhoneNumber);
        Assert.Equal("حاجی اسلم", body.User.FullName);
        Assert.Equal("pa", body.User.Profile.PreferredLanguage);
        Assert.Equal("lodhran", body.User.Profile.Location?.Slug);
        Assert.Equal("User", body.User.Role);
    }

    [Fact]
    public async Task Register_same_phone_in_different_format_is_rejected()
    {
        var client = factory.CreateClient();
        await client.RegisterAsync("03001112233");

        var res = await client.PostAsJsonAsync("/api/auth/register", new { fullName = "دوسرا", phoneNumber = "923001112233", password = "Bakra#2026" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("phone_taken", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Register_same_name_is_allowed_but_same_cnic_is_rejected()
    {
        var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "محمد علی", phoneNumber = TestHelpers.NewPhone(), password = "Bakra#2026", cnic = "35202-7654321-9" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Same name, different number and CNIC: fine
        var sameName = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "محمد علی", phoneNumber = TestHelpers.NewPhone(), password = "Bakra#2026", cnic = "3520276543218" });
        Assert.Equal(HttpStatusCode.Created, sameName.StatusCode);

        // Same CNIC written without dashes: rejected
        var sameCnic = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "کوئی اور", phoneNumber = TestHelpers.NewPhone(), password = "Bakra#2026", cnic = "3520276543219" });
        Assert.Equal(HttpStatusCode.Conflict, sameCnic.StatusCode);
        Assert.Equal("cnic_taken", await sameCnic.ErrorCodeAsync());

        // No CNIC at all: allowed for many users
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "بغیر شناختی کارڈ", phoneNumber = TestHelpers.NewPhone(), password = "Bakra#2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "بغیر شناختی کارڈ", phoneNumber = TestHelpers.NewPhone(), password = "Bakra#2026", cnic = "" })).StatusCode);
    }

    [Theory]
    [InlineData("1234")]              // too short
    [InlineData("35202-123456-1")]    // 12 digits
    [InlineData("05202-1234567-1")]   // no province starts with 0
    [InlineData("3520a-1234567-1")]   // letters
    public async Task Register_rejects_invalid_cnic(string cnic)
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { fullName = "ٹیسٹ", phoneNumber = TestHelpers.NewPhone(), password = "Bakra#2026", cnic });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation_failed", await res.ErrorCodeAsync());
    }

    [Theory]
    [InlineData("12345", "Bakra#2026")]        // not a PK mobile
    [InlineData("04212345678", "Bakra#2026")]  // landline
    [InlineData("03001234567", "abc")]          // password too short
    public async Task Register_rejects_invalid_input(string phone, string password)
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { fullName = "ٹیسٹ", phoneNumber = phone, password });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation_failed", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Register_rejects_easily_guessed_password()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { fullName = "ٹیسٹ", phoneNumber = TestHelpers.NewPhone(), password = "123456" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("weak_password", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Register_rejects_unknown_location()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { fullName = "ٹیسٹ", phoneNumber = TestHelpers.NewPhone(), password = "Bakra#2026", locationId = 999 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("location_not_found", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Login_works_with_correct_password_and_fails_with_wrong_one()
    {
        var client = factory.CreateClient();
        var phone = TestHelpers.NewPhone();
        await client.RegisterAsync(phone);

        var ok = await client.PostAsJsonAsync("/api/auth/login", new { phoneNumber = phone, password = "Bakra#2026" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var bad = await client.PostAsJsonAsync("/api/auth/login", new { phoneNumber = phone, password = "wrong-pass" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal("invalid_credentials", await bad.ErrorCodeAsync());

        // Unknown number gives the same answer (doesn't reveal who is registered)
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { phoneNumber = "03459999999", password = "Bakra#2026" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal("invalid_credentials", await unknown.ErrorCodeAsync());
    }

    [Fact]
    public async Task Account_locks_after_five_wrong_passwords()
    {
        var client = factory.CreateClient();
        var phone = TestHelpers.NewPhone();
        await client.RegisterAsync(phone);

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/auth/login", new { phoneNumber = phone, password = "wrong-pass" });

        // Even the right password is refused while locked
        var res = await client.PostAsJsonAsync("/api/auth/login", new { phoneNumber = phone, password = "Bakra#2026" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("account_locked", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Refresh_rotates_token_and_reuse_ends_all_sessions()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterAsync();

        var res = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var second = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        // Re-using the old (rotated) token looks like theft → rejected…
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // …and the newer token is revoked too.
        var after = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAsync();
        client.WithToken(auth.AccessToken);

        var res = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
