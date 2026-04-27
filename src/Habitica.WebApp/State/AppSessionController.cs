using Habitica.Application.Auth;
using Habitica.Application.Diagnostics;
using Habitica.Application.Sync;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Sync;
using Habitica.Domain.User;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed class AppSessionController : IAppSessionController
{
    private readonly ICredentialStore _credentialStore;
    private readonly IDiagnosticsLogStore _diagnosticsLogStore;
    private readonly DiagnosticsLogWriter _diagnosticsLogWriter;
    private readonly DiagnosticsPresetWorkflow _diagnosticsPresetWorkflow;
    private readonly LoginWorkflow _loginWorkflow;
    private readonly LiveTestWorkflow _liveTestWorkflow;
    private readonly IPartySnapshotStore _partySnapshotStore;
    private readonly SnapshotFreshnessPolicy _snapshotFreshnessPolicy;
    private readonly ITaskSnapshotStore _taskSnapshotStore;
    private readonly IUserSnapshotStore _userSnapshotStore;
    private readonly TimeProvider _timeProvider;
    private HabiticaCredentials? _currentCredentials;
    private bool _initialized;
    private bool _persistLocally;

    public AppSessionController(
        LoginWorkflow loginWorkflow,
        LiveTestWorkflow liveTestWorkflow,
        DiagnosticsPresetWorkflow diagnosticsPresetWorkflow,
        ICredentialStore credentialStore,
        IPartySnapshotStore partySnapshotStore,
        ITaskSnapshotStore taskSnapshotStore,
        IUserSnapshotStore userSnapshotStore,
        IDiagnosticsLogStore diagnosticsLogStore,
        DiagnosticsLogWriter diagnosticsLogWriter,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy,
        TimeProvider timeProvider)
    {
        _loginWorkflow = loginWorkflow;
        _liveTestWorkflow = liveTestWorkflow;
        _diagnosticsPresetWorkflow = diagnosticsPresetWorkflow;
        _credentialStore = credentialStore;
        _partySnapshotStore = partySnapshotStore;
        _taskSnapshotStore = taskSnapshotStore;
        _userSnapshotStore = userSnapshotStore;
        _diagnosticsLogStore = diagnosticsLogStore;
        _diagnosticsLogWriter = diagnosticsLogWriter;
        _snapshotFreshnessPolicy = snapshotFreshnessPolicy;
        _timeProvider = timeProvider;
    }

    public event Action? Changed;

    public SessionViewModel State { get; private set; } = SessionViewModel.Empty;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await LoadCachedStateAsync(cancellationToken);

        var persistedCredentials = await _credentialStore.GetPersistentCredentialsAsync(cancellationToken);

        if (persistedCredentials is not null)
        {
            await SignInCoreAsync(
                new SignInRequest
                {
                    ApiToken = persistedCredentials.ApiToken,
                    PersistLocally = true,
                    UserId = persistedCredentials.UserId
                },
                cancellationToken);
        }
    }

    public async Task SignInAsync(SignInRequest request, CancellationToken cancellationToken = default)
    {
        await SignInCoreAsync(request, cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);

        if (credentials is not null)
        {
            await SignInCoreAsync(
                new SignInRequest
                {
                    ApiToken = credentials.ApiToken,
                    PersistLocally = _persistLocally || _currentCredentials is null,
                    UserId = credentials.UserId
                },
                cancellationToken);

            return;
        }

        SetState(State with
        {
            ErrorMessage = "Sign in is required before refreshing."
        });
    }

    public async Task<LiveTestSuiteResult> RunSafeLiveTestsAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            var message = "Sign in is required before running live tests.";
            SetState(State with
            {
                ErrorMessage = message
            });

            return BuildFailureResult("safe-live-tests", "Safe live tests", message, LiveTestRisk.Safe);
        }

        SetState(State with
        {
            ErrorMessage = null,
            IsBusy = true
        });

        try
        {
            var result = await _liveTestWorkflow.RunSafeLiveTestsAsync(credentials, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = null,
                IsBusy = false
            });

            return result;
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                IsBusy = false
            });

            return BuildFailureResult("safe-live-tests", "Safe live tests", exception.Message, LiveTestRisk.Safe);
        }
    }

    public async Task<LiveTestSuiteResult> RunReversibleGearTestAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            var message = "Sign in is required before running the reversible gear test.";
            SetState(State with
            {
                ErrorMessage = message
            });

            return BuildFailureResult("reversible-gear-roundtrip", "Reversible gear roundtrip", message, LiveTestRisk.ReversibleMutation);
        }

        SetState(State with
        {
            ErrorMessage = null,
            IsBusy = true
        });

        try
        {
            var result = await _liveTestWorkflow.RunReversibleGearTestAsync(credentials, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = null,
                IsBusy = false
            });

            return result;
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                IsBusy = false
            });

            return BuildFailureResult("reversible-gear-roundtrip", "Reversible gear roundtrip", exception.Message, LiveTestRisk.ReversibleMutation);
        }
    }

    public async Task<DiagnosticsPresetRunResult> RunDiagnosticsPresetAsync(DiagnosticsPreset preset, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            var message = "Sign in is required before running diagnostics presets.";
            SetState(State with
            {
                ErrorMessage = message
            });

            return new DiagnosticsPresetRunResult(preset, false, 0, message, "{}");
        }

        SetState(State with
        {
            ErrorMessage = null,
            IsBusy = true
        });

        try
        {
            var result = await _diagnosticsPresetWorkflow.RunAsync(credentials, preset, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = null,
                IsBusy = false
            });

            return result;
        }
        catch (Exception exception)
        {
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Diagnostics,
                $"preset-{preset.ToString().ToLowerInvariant()}",
                DiagnosticsSeverity.Error,
                DiagnosticsMode.LiveRead,
                exception.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["preset"] = preset.ToString()
                },
                cancellationToken);

            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                IsBusy = false
            });

            return new DiagnosticsPresetRunResult(preset, false, 0, exception.Message, "{}");
        }
    }

    public async Task ClearDiagnosticsLogsAsync(CancellationToken cancellationToken = default)
    {
        await _diagnosticsLogStore.ClearAsync(cancellationToken);
        await LoadCachedStateAsync(cancellationToken);
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _currentCredentials = null;
        var cachedUserSnapshot = State.UserSnapshot;

        SetState(State with
        {
            DisplayName = cachedUserSnapshot?.DisplayName,
            ClassName = cachedUserSnapshot?.ClassName,
            ErrorMessage = null,
            IsAuthenticated = false,
            Level = cachedUserSnapshot?.Level
        });

        return Task.CompletedTask;
    }

    public async Task ClearLocalDataAsync(CancellationToken cancellationToken = default)
    {
        _currentCredentials = null;
        _persistLocally = false;

        await _credentialStore.ClearPersistentCredentialsAsync(cancellationToken);
        await _partySnapshotStore.ClearAsync(cancellationToken);
        await _taskSnapshotStore.ClearAsync(cancellationToken);
        await _userSnapshotStore.ClearAsync(cancellationToken);

        SetState(SessionViewModel.Empty);
    }

    private async Task LoadCachedStateAsync(CancellationToken cancellationToken)
    {
        var diagnosticsLogEntries = await _diagnosticsLogStore.GetRecentAsync(cancellationToken);
        var taskSnapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);
        var userSnapshot = await _userSnapshotStore.GetLatestAsync(cancellationToken);
        var partySnapshot = await _partySnapshotStore.GetLatestAsync(cancellationToken);

        SetState(State with
        {
            ClassName = userSnapshot?.ClassName ?? State.ClassName,
            DisplayName = userSnapshot?.DisplayName ?? State.DisplayName,
            LastSyncedAtUtc = GetLatestSyncTimestamp(taskSnapshot, userSnapshot, partySnapshot),
            Level = userSnapshot?.Level ?? State.Level,
            PartyFreshness = ClassifyFreshness(partySnapshot),
            PartySnapshot = partySnapshot,
            TaskFreshness = ClassifyFreshness(taskSnapshot),
            TaskSnapshot = taskSnapshot,
            DiagnosticsLogEntries = diagnosticsLogEntries,
            UserFreshness = ClassifyFreshness(userSnapshot),
            UserSnapshot = userSnapshot
        });
    }

    private async Task SignInCoreAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.ApiToken))
        {
            SetState(State with
            {
                ErrorMessage = "Habitica User ID and API Token are required."
            });
            return;
        }

        SetState(State with
        {
            ErrorMessage = null,
            IsBusy = true
        });

        try
        {
            var loginResult = await _loginWorkflow.AuthenticateAndSyncAsync(
                new LoginCommand(request.UserId.Trim(), request.ApiToken.Trim(), request.PersistLocally),
                cancellationToken);
            var partySnapshot = await _partySnapshotStore.GetLatestAsync(cancellationToken);
            var taskSnapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);
            var userSnapshot = await _userSnapshotStore.GetLatestAsync(cancellationToken);

            _currentCredentials = new HabiticaCredentials(request.UserId.Trim(), request.ApiToken.Trim());
            _persistLocally = request.PersistLocally;

            SetState(new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: loginResult.DisplayName,
                ErrorMessage: null,
                LastSyncedAtUtc: loginResult.RetrievedAtUtc,
                PartyFreshness: ClassifyFreshness(partySnapshot),
                PartySnapshot: partySnapshot,
                TaskFreshness: ClassifyFreshness(taskSnapshot),
                TaskSnapshot: taskSnapshot,
                DiagnosticsLogEntries: await _diagnosticsLogStore.GetRecentAsync(cancellationToken),
                ClassName: loginResult.ClassName,
                Level: loginResult.Level,
                UserSnapshot: userSnapshot,
                UserFreshness: ClassifyFreshness(userSnapshot)));
        }
        catch (Exception exception)
        {
            var partySnapshot = await _partySnapshotStore.GetLatestAsync(cancellationToken);
            var taskSnapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);
            var userSnapshot = await _userSnapshotStore.GetLatestAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                IsBusy = false,
                LastSyncedAtUtc = GetLatestSyncTimestamp(taskSnapshot ?? State.TaskSnapshot, userSnapshot ?? State.UserSnapshot, partySnapshot ?? State.PartySnapshot) ?? State.LastSyncedAtUtc,
                PartyFreshness = ClassifyFreshness(partySnapshot ?? State.PartySnapshot),
                PartySnapshot = partySnapshot ?? State.PartySnapshot,
                TaskFreshness = ClassifyFreshness(taskSnapshot ?? State.TaskSnapshot),
                TaskSnapshot = taskSnapshot ?? State.TaskSnapshot,
                UserFreshness = ClassifyFreshness(userSnapshot ?? State.UserSnapshot),
                UserSnapshot = userSnapshot ?? State.UserSnapshot
            });
        }
    }

    private SnapshotFreshnessState ClassifyFreshness(Habitica.Domain.Tasks.TaskCollectionSnapshot? snapshot)
    {
        return _snapshotFreshnessPolicy.Classify(
            SnapshotCategory.VolatileGameplayState,
            snapshot?.RetrievedAtUtc,
            _timeProvider.GetUtcNow());
    }

    private SnapshotFreshnessState ClassifyFreshness(UserSnapshot? snapshot)
    {
        return _snapshotFreshnessPolicy.Classify(
            SnapshotCategory.VolatileGameplayState,
            snapshot?.RetrievedAtUtc,
            _timeProvider.GetUtcNow());
    }

    private SnapshotFreshnessState ClassifyFreshness(PartySnapshot? snapshot)
    {
        return _snapshotFreshnessPolicy.Classify(
            SnapshotCategory.VolatileGameplayState,
            snapshot?.RetrievedAtUtc,
            _timeProvider.GetUtcNow());
    }

    private static DateTimeOffset? GetLatestSyncTimestamp(
        Habitica.Domain.Tasks.TaskCollectionSnapshot? taskSnapshot,
        UserSnapshot? userSnapshot,
        PartySnapshot? partySnapshot)
    {
        return new[]
        {
            taskSnapshot?.RetrievedAtUtc,
            userSnapshot?.RetrievedAtUtc,
            partySnapshot?.RetrievedAtUtc
        }.Max();
    }

    private void SetState(SessionViewModel nextState)
    {
        State = nextState;
        Changed?.Invoke();
    }

    private async Task<HabiticaCredentials?> ResolveCredentialsAsync(CancellationToken cancellationToken)
    {
        if (_currentCredentials is not null)
        {
            return _currentCredentials;
        }

        var persistedCredentials = await _credentialStore.GetPersistentCredentialsAsync(cancellationToken);
        if (persistedCredentials is not null)
        {
            _persistLocally = true;
        }

        return persistedCredentials;
    }

    private LiveTestSuiteResult BuildFailureResult(string id, string title, string message, LiveTestRisk risk)
    {
        var now = _timeProvider.GetUtcNow();
        return new LiveTestSuiteResult(
            now,
            now,
            new[]
            {
                new LiveTestResult(id, title, LiveTestStatus.Failed, risk, 0, message)
            });
    }
}
