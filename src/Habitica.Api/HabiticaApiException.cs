using System.Net;

namespace Habitica.Api;

public sealed class HabiticaApiException : Exception
{
    public HabiticaApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
