using Habitica.Domain.Auth;

namespace Habitica.Application.Integration;

public sealed record IntegrationLinkResult(bool Succeeded, string Message, string? Code = null, DateTimeOffset? ExpiresAtUtc = null);

public interface IChatGptIntegrationClient
{
    bool IsAvailable { get; }
    bool HasConnection { get; }
    Task<IntegrationLinkResult> EnrollAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);
    Task<IntegrationLinkResult> CreateLinkAsync(CancellationToken cancellationToken);
    Task<IntegrationLinkResult> DisconnectAsync(bool deleteProfile, CancellationToken cancellationToken);
    void ForgetSession();
}
