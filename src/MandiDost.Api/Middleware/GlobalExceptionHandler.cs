using MandiDost.Api.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MandiDost.Api.Middleware;

/// <summary>
/// Turns every error into a consistent RFC 7807 ProblemDetails response:
/// { status, title, detail, code, traceId }. Unexpected errors never leak internals.
/// </summary>
public class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        int status;
        string code, title, detail;

        switch (exception)
        {
            case AppException app:
                status = app.StatusCode;
                code = app.Code;
                title = ReasonPhrase(status);
                detail = app.Message;
                break;
            case OperationCanceledException when http.RequestAborted.IsCancellationRequested:
                return true; // client went away (common on weak mobile networks) – nothing to send
            default:
                logger.LogError(exception, "Unhandled error on {Method} {Path}", http.Request.Method, http.Request.Path);
                status = StatusCodes.Status500InternalServerError;
                code = ErrorCodes.ServerError;
                title = "Server error";
                detail = "Something went wrong. Please try again.";
                break;
        }

        http.Response.StatusCode = status;
        var pd = new ProblemDetails { Status = status, Title = title, Detail = detail };
        pd.Extensions["code"] = code;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = pd, Exception = exception });
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not found",
        409 => "Conflict",
        _ => "Error",
    };
}
