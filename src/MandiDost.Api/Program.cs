using MandiDost.Api.Data;
using MandiDost.Api.Extensions;
using MandiDost.Api.Middleware;
using MandiDost.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDatabase(builder.Configuration)
    .AddJwtAuth(builder.Configuration)
    .AddAppServices()
    .AddApiBehaviour(builder.Configuration);

var app = builder.Build();

// Behind nginx / IIS / Plesk: take the client's IP and https scheme from the proxy headers.
// Only proxies on this machine (loopback) are trusted by default, so the headers can't be spoofed from outside.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
});

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("Mandi Dost API")); // browse at /scalar
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();

// Uploaded listing photos. File names are random, so they can be cached for a long time.
var mediaRoot = app.Services.GetRequiredService<IMediaStorage>().RootPath;
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRoot),
    RequestPath = LocalMediaStorage.PublicPrefix,
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public, max-age=2592000, immutable",
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { name = "Mandi Dost API", status = "ok" })).ExcludeFromDescription();

// Development convenience: apply migrations automatically. In production run them explicitly.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}
await AdminSeeder.SeedAsync(app.Services, app.Configuration);

app.Run();

public partial class Program; // for integration tests
