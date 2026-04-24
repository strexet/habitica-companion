namespace Habitica.Api;

public sealed record HabiticaApiClientOptions(string? ClientHeaderValue, string ApplicationName = "habitica-tool");
