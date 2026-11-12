using System.Net.Http.Headers;
using System.Net.Http.Json;
using MandiDost.Api.Data;
using MandiDost.Api.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MandiDost.Api.Tests;

/// <summary>
/// Runs the real API in memory, with SQLite in place of SQL Server so tests need no database server.
/// Each factory instance gets its own fresh database.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminPhone = "03110000000";
    public const string AdminPassword = "Admin#Test2026";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), "mandidost-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "server=unused");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-long-enough-1234567890");
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "1000");
        builder.UseSetting("Seed:AdminPhone", AdminPhone);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Media:RootPath", _mediaRoot);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            _connection.Open();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
        try { Directory.Delete(_mediaRoot, recursive: true); } catch (IOException) { } // test photos only
    }
}

public static class TestHelpers
{
    private static int _counter;

    /// <summary>A unique valid Pakistani mobile number per call.</summary>
    public static string NewPhone() => $"0300{Interlocked.Increment(ref _counter) + 1000000:D7}";

    public static async Task<AuthResponse> RegisterAsync(this HttpClient client, string? phone = null, string password = "Bakra#2026", string name = "محمد اکرم")
    {
        var res = await client.PostAsJsonAsync("/api/auth/register", new { fullName = name, phoneNumber = phone ?? NewPhone(), password, locationId = 1, preferredLanguage = "skr" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return doc.TryGetProperty("code", out var c) ? c.GetString() : null;
    }
}
