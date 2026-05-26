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

    public bool IsRateLimited => StatusCode == HttpStatusCode.TooManyRequests;
}

public sealed record HabiticaRateLimitInfo(
    TimeSpan? RetryAfter,
    int? Limit,
    int? Remaining,
    DateTimeOffset? ResetAtUtc);
