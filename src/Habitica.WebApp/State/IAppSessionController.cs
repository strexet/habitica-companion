using Habitica.Application.Diagnostics;

namespace Habitica.WebApp.State;

public interface IAppSessionController
{
    event Action? Changed;

    SessionViewModel State { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SignInAsync(SignInRequest request, CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);

    Task<LiveTestSuiteResult> RunSafeLiveTestsAsync(CancellationToken cancellationToken = default);

    Task<LiveTestSuiteResult> RunReversibleGearTestAsync(CancellationToken cancellationToken = default);

    Task<DiagnosticsPresetRunResult> RunDiagnosticsPresetAsync(DiagnosticsPreset preset, CancellationToken cancellationToken = default);

    Task ClearDiagnosticsLogsAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task ClearLocalDataAsync(CancellationToken cancellationToken = default);
}
