using System.Net;

namespace Habitica.Api;

public sealed class HabiticaApiException : Exception
{
    public HabiticaApiException(HttpStatusCode statusCode, string message)
        : this(statusCode, message, null)
    {
    }

    public HabiticaApiException(HttpStatusCode statusCode, string message, HabiticaRateLimitInfo? rateLimit)
        : base(message)
    {
        StatusCode = statusCode;
        RateLimit = rateLimit;
    }

    public HttpStatusCode StatusCode { get; }

    public HabiticaRateLimitInfo? RateLimit { get; }

    public bool IsDefinitiveRefusal => (int)StatusCode is 400 or 401 or 403 or 404 or 409 or 422 or 429;

    public bool IsRateLimited => StatusCode == HttpStatusCode.TooManyRequests;
}

public sealed record HabiticaRateLimitInfo(
    TimeSpan? RetryAfter,
    int? Limit,
    int? Remaining,
    DateTimeOffset? ResetAtUtc);
