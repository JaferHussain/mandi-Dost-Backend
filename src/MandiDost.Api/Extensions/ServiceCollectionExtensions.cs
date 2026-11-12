using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using MandiDost.Api.Auth;
using MandiDost.Api.Common;
using MandiDost.Api.Data;
using MandiDost.Api.Middleware;
using MandiDost.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MandiDost.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not set. Run: dotnet user-secrets set \"ConnectionStrings:Default\" \"Server=(localdb)\\MSSQLLocalDB;Database=MandiDost;Trusted_Connection=True;TrustServerCertificate=True\"");

        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs, sql => sql.EnableRetryOnFailure(3)));
        return services;
    }

    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<JwtOptions>().Bind(config.GetSection(JwtOptions.Section)).ValidateDataAnnotations().ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((o, jwt) =>
            {
                var opt = jwt.Value;
                o.MapInboundClaims = false; // keep "sub" and "role" as-is
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = opt.Issuer,
                    ValidAudience = opt.Audience,
                    IssuerSigningKey = TokenService.SigningKey(opt),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AuthConstants.NameClaim,
                    RoleClaimType = AuthConstants.RoleClaim,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthConstants.AdminPolicy, p => p.RequireRole("Admin", "Moderator"));
        return services;
    }

    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IListingService, ListingService>();
        services.AddScoped<IBusinessService, BusinessService>();
        services.AddScoped<IPurchaseRequestService, PurchaseRequestService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddSingleton<IMediaStorage, LocalMediaStorage>();
        services.AddMemoryCache();
        return services;
    }

    public static IServiceCollection AddApiBehaviour(this IServiceCollection services, IConfiguration config)
    {
        services.AddControllers()
            // Enums travel as readable camelCase strings: "qurbani", "perMaund", "male".
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)))
            .ConfigureApiBehaviorOptions(o =>
            {
                // Validation errors use the same shape as every other error, plus a "code".
                o.InvalidModelStateResponseFactory = ctx =>
                {
                    var pd = new ValidationProblemDetails(ctx.ModelState) { Status = 400, Title = "Validation failed" };
                    pd.Extensions["code"] = ErrorCodes.Validation;
                    pd.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
                    return new BadRequestObjectResult(pd);
                };
            });

        services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
            c.ProblemDetails.Extensions.TryAdd("traceId", c.HttpContext.TraceIdentifier));
        services.AddExceptionHandler<GlobalExceptionHandler>();

        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        // Local testing only: let the web version of the app be opened from any address (localhost, PC's Wi-Fi IP, any port).
        // Safe here because login uses bearer tokens, not cookies. Never set this on a live server.
        var allowAny = config.GetValue<bool>("Cors:AllowAnyOrigin");
        services.AddCors(o => o.AddDefaultPolicy(p =>
        {
            if (allowAny) p.AllowAnyOrigin(); else p.WithOrigins(origins);
            p.AllowAnyHeader().AllowAnyMethod();
        }));

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Login/register: 10 attempts per minute per IP by default (stops password guessing).
            var permit = config.GetValue("RateLimiting:AuthPermitPerMinute", 10);
            o.AddPolicy(AuthConstants.AuthRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.OnRejected = async (ctx, ct) =>
            {
                var pd = new ProblemDetails { Status = 429, Title = "Too many requests", Detail = "Please wait a minute and try again." };
                pd.Extensions["code"] = ErrorCodes.RateLimited;
                await ctx.HttpContext.Response.WriteAsJsonAsync(pd, ct);
            };
        });

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        services.AddOpenApi();
        return services;
    }
}
