namespace Habitica.Api;

public sealed record HabiticaApiClientOptions(
    string? ClientHeaderValue,
    string ApplicationName = "habitica-tool",
    // Smallest gap enforced between any two requests, even in the middle of a burst.
    // Habitica dislikes rapid-fire traffic, so we keep a polite floor regardless of
    // how much rate-limit budget is left.
    int MinRequestSpacingMilliseconds = 300);
