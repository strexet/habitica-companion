namespace Habitica.WebApp.State;

public sealed class SignInRequest
{
    public string ApiToken { get; set; } = string.Empty;

    public bool PersistLocally { get; set; }

    public string UserId { get; set; } = string.Empty;
}
