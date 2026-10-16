using Habitica.Domain.Auth;

namespace Habitica.Application.Integration;

public sealed record IntegrationLinkResult(bool Succeeded, string Message, string? Code = null, DateTimeOffset? ExpiresAtUtc = null);

public sealed record IntegrationStatusResult(bool Succeeded, string Message, string? DisplayName = null, string[]? Permissions = null, string? CredentialStatus = null, DateTimeOffset? LastVerifiedAtUtc = null, DateTimeOffset? LastUsedAtUtc = null, bool PermissionsTruncated = false);

public interface IChatGptIntegrationClient
{
    bool IsAvailable { get; }
    bool HasConnection { get; }
    Task<IntegrationLinkResult> EnrollAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);
    Task<IntegrationLinkResult> CreateLinkAsync(CancellationToken cancellationToken);
    Task<IntegrationLinkResult> DisconnectAsync(bool deleteProfile, CancellationToken cancellationToken);
    Task<IntegrationStatusResult> GetStatusAsync(CancellationToken cancellationToken);
    Task<IntegrationLinkResult> RevokeGrantsAsync(CancellationToken cancellationToken);
    Task<IntegrationLinkResult> RotateManagementAsync(CancellationToken cancellationToken);
    Task<IntegrationLinkResult> RevalidateAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);
    void ForgetSession();
}
