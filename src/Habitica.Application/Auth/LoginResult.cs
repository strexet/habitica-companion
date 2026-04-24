namespace Habitica.Application.Auth;

public sealed record LoginResult(
    string DisplayName,
    string? ClassName,
    int Level,
    int TaskCount,
    DateTimeOffset RetrievedAtUtc);
