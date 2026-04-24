using Habitica.WebApp.State;

namespace Habitica.WebApp.Tests;

internal sealed class FakeAppSessionController : IAppSessionController
{
    public FakeAppSessionController(SessionViewModel state)
    {
        State = state;
    }

    public event Action? Changed;

    public SignInRequest? LastSignInRequest { get; private set; }

    public SessionViewModel State { get; private set; }

    public Task ClearLocalDataAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SignInAsync(SignInRequest request, CancellationToken cancellationToken = default)
    {
        LastSignInRequest = request;
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
