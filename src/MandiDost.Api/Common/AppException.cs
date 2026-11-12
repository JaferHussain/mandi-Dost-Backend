namespace MandiDost.Api.Common;

/// <summary>
/// Expected business errors. Thrown by services, turned into ProblemDetails by
/// <see cref="Middleware.GlobalExceptionHandler"/>. <c>Code</c> is a stable
/// machine-readable key the frontend maps to an Urdu message.
/// </summary>
public class AppException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public sealed class NotFoundException(string code, string message) : AppException(StatusCodes.Status404NotFound, code, message);

public sealed class ConflictException(string code, string message) : AppException(StatusCodes.Status409Conflict, code, message);

public sealed class UnauthorizedException(string code, string message) : AppException(StatusCodes.Status401Unauthorized, code, message);

public sealed class ForbiddenException(string code, string message) : AppException(StatusCodes.Status403Forbidden, code, message);

public sealed class BadRequestException(string code, string message) : AppException(StatusCodes.Status400BadRequest, code, message);

public static class ErrorCodes
{
    public const string PhoneTaken = "phone_taken";
    public const string InvalidCredentials = "invalid_credentials";
    public const string AccountLocked = "account_locked";
    public const string AccountDisabled = "account_disabled";
    public const string InvalidRefreshToken = "invalid_refresh_token";
    public const string WrongPassword = "wrong_password";
    public const string UserNotFound = "user_not_found";
    public const string LocationNotFound = "location_not_found";
    public const string Validation = "validation_failed";

    // Listings
    public const string ListingNotFound = "listing_not_found";
    public const string CategoryNotFound = "category_not_found";
    public const string NotOwner = "not_owner";
    public const string ListingNotEditable = "listing_not_editable";
    public const string PurposeRequired = "purpose_required";
    public const string PurposeNotAllowed = "purpose_not_allowed";
    /// <summary>A detail block or field that doesn't belong to this category (e.g. teeth on wheat).</summary>
    public const string DetailsMismatch = "details_mismatch";
    public const string SharesNotAllowed = "shares_not_allowed";
    public const string UnitNotAllowed = "unit_not_allowed";
    public const string ImageRequired = "image_required";
    public const string TooManyMedia = "too_many_media";
    public const string InvalidImage = "invalid_image";
    public const string MediaNotFound = "media_not_found";

    // Business profiles, purchase requests and offers
    public const string BusinessNotFound = "business_not_found";
    public const string RequestNotFound = "request_not_found";
    public const string RequestClosed = "request_closed";
    public const string OfferNotFound = "offer_not_found";
    public const string OwnRequest = "own_request";
    public const string OfferNotAllowed = "offer_not_allowed";

    // Chat, Qurbani shares, reviews, reports
    public const string ChatNotFound = "chat_not_found";
    public const string ChatWithSelf = "chat_with_self";
    public const string InvalidAudio = "invalid_audio";
    public const string NoShares = "no_shares";
    public const string NotEnoughShares = "not_enough_shares";
    public const string BookingNotFound = "booking_not_found";
    public const string ReviewNotAllowed = "review_not_allowed";
    public const string AlreadyReported = "already_reported";
    public const string ReportNotFound = "report_not_found";
    public const string RateLimited = "rate_limited";
    public const string ServerError = "server_error";
}
