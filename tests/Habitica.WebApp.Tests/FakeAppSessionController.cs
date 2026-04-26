using Habitica.WebApp.State;

using Habitica.Application.Diagnostics;

namespace Habitica.WebApp.Tests;

internal sealed class FakeAppSessionController : IAppSessionController
{
    public FakeAppSessionController(SessionViewModel state)
    {
        State = state;
    }

    public event Action? Changed;

    public SignInRequest? LastSignInRequest { get; private set; }

    public int ReversibleGearTestCalls { get; private set; }

    public LiveTestSuiteResult? ReversibleGearTestResult { get; set; }

    public int SafeLiveTestCalls { get; private set; }

    public LiveTestSuiteResult? SafeLiveTestResult { get; set; }

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

    public Task<LiveTestSuiteResult> RunReversibleGearTestAsync(CancellationToken cancellationToken = default)
    {
        ReversibleGearTestCalls++;
        return Task.FromResult(ReversibleGearTestResult ?? EmptyResult());
    }

    public Task<LiveTestSuiteResult> RunSafeLiveTestsAsync(CancellationToken cancellationToken = default)
    {
        SafeLiveTestCalls++;
        return Task.FromResult(SafeLiveTestResult ?? EmptyResult());
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

    private static LiveTestSuiteResult EmptyResult()
    {
        return new LiveTestSuiteResult(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<LiveTestResult>());
    }
}
