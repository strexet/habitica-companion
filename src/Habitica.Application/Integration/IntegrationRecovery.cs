namespace Habitica.Application.Integration;

// A routing reference, never an authorization capability or a stored consent acknowledgement.
public sealed record IntegrationRecoveryDescriptor(string Version, string ProfileId, Guid ConnectionId)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => Version == "integration-recovery-v1" && ConnectionId != Guid.Empty
        && System.Text.RegularExpressions.Regex.IsMatch(ProfileId ?? "", "^prf_[A-Za-z0-9_-]{43}$");
}

public sealed record IntegrationRecoveryResult(bool Succeeded, string Code,
    IntegrationRecoveryDescriptor? Descriptor = null, DateTimeOffset? ExpiresAtUtc = null);
