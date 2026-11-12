namespace MandiDost.Api.Middleware;

/// <summary>Basic hardening headers for a JSON API.</summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        return next(context);
    }
}
