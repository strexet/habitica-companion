using System.Net;

namespace Habitica.Api;

public static class HabiticaMutationGuard
{
    public static void Validate(HttpMethod method, string relativePath, bool ownerIsolatedTesting)
    {
        var path = new Uri(new Uri("https://habitica.com/api/v3/"), relativePath).AbsolutePath;
        for (var pass = 0; pass < 4; pass++)
        {
            var decoded = Uri.UnescapeDataString(path);
            if (decoded == path) break;
            path = decoded;
        }
        if (path.Contains('%'))
            throw new HabiticaApiException(HttpStatusCode.Forbidden, "Unsupported encoded mutation path.");
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var groupIndex = Array.FindIndex(segments, part => part.Equals("groups", StringComparison.OrdinalIgnoreCase));
        if (groupIndex >= 0 && segments.Skip(groupIndex + 1).Any(part => part.Equals("leave", StringComparison.OrdinalIgnoreCase)))
            throw new HabiticaApiException(HttpStatusCode.Forbidden, "Leaving a party is prohibited in Companion.");

        if (method == HttpMethod.Get || method == HttpMethod.Head || !ownerIsolatedTesting) return;
        if (groupIndex >= 0 || path.Equals("/api/v3/cron", StringComparison.OrdinalIgnoreCase))
            throw new HabiticaApiException(HttpStatusCode.Forbidden, "Party changes and Start New Day are disabled during own-account testing.");
    }
}
