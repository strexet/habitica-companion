namespace Habitica.Application.Auth;

public sealed record LoginCommand(string UserId, string ApiToken, bool PersistLocally);
