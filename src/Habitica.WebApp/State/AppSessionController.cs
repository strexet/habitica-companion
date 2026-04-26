using Habitica.Application.Auth;
using Habitica.Application.Sync;
using Habitica.Domain.Auth;
using Habitica.Domain.Sync;
using Habitica.Domain.User;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed class AppSessionController : IAppSessionController
{
    private readonly ICredentialStore _credentialStore;
    private readonly LoginWorkflow _loginWorkflow;
    private readonly SnapshotFreshnessPolicy _snapshotFreshnessPolicy;
    private readonly ITaskSnapshotStore _taskSnapshotStore;
    private readonly IUserSnapshotStore _userSnapshotStore;
    private readonly TimeProvider _timeProvider;
    private HabiticaCredentials? _currentCredentials;
    private bool _initialized;
    private bool _persistLocally;

    public AppSessionController(
        LoginWorkflow loginWorkflow,
        ICredentialStore credentialStore,
        ITaskSnapshotStore taskSnapshotStore,
        IUserSnapshotStore userSnapshotStore,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy,
        TimeProvider timeProvider)
    {
        _loginWorkflow = loginWorkflow;
        _credentialStore = credentialStore;
        _taskSnapshotStore = taskSnapshotStore;
        _userSnapshotStore = userSnapshotStore;
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
        if (_currentCredentials is not null)
        {
            await SignInCoreAsync(
                new SignInRequest
                {
                    ApiToken = _currentCredentials.ApiToken,
                    PersistLocally = _persistLocally,
                    UserId = _currentCredentials.UserId
                },
                cancellationToken);

            return;
        }

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

            return;
        }

        SetState(State with
        {
            ErrorMessage = "Sign in is required before refreshing."
        });
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
        await _taskSnapshotStore.ClearAsync(cancellationToken);
        await _userSnapshotStore.ClearAsync(cancellationToken);

        SetState(SessionViewModel.Empty);
    }

    private async Task LoadCachedStateAsync(CancellationToken cancellationToken)
    {
        var taskSnapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);
        var userSnapshot = await _userSnapshotStore.GetLatestAsync(cancellationToken);

        SetState(State with
        {
            ClassName = userSnapshot?.ClassName ?? State.ClassName,
            DisplayName = userSnapshot?.DisplayName ?? State.DisplayName,
            LastSyncedAtUtc = GetLatestSyncTimestamp(taskSnapshot, userSnapshot),
            Level = userSnapshot?.Level ?? State.Level,
            TaskFreshness = ClassifyFreshness(taskSnapshot),
            TaskSnapshot = taskSnapshot,
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
                TaskFreshness: ClassifyFreshness(taskSnapshot),
                TaskSnapshot: taskSnapshot,
                ClassName: loginResult.ClassName,
                Level: loginResult.Level,
                UserSnapshot: userSnapshot,
                UserFreshness: ClassifyFreshness(userSnapshot)));
        }
        catch (Exception exception)
        {
            var taskSnapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);
            var userSnapshot = await _userSnapshotStore.GetLatestAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                IsBusy = false,
                LastSyncedAtUtc = GetLatestSyncTimestamp(taskSnapshot ?? State.TaskSnapshot, userSnapshot ?? State.UserSnapshot) ?? State.LastSyncedAtUtc,
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

    private static DateTimeOffset? GetLatestSyncTimestamp(
        Habitica.Domain.Tasks.TaskCollectionSnapshot? taskSnapshot,
        UserSnapshot? userSnapshot)
    {
        return new[]
        {
            taskSnapshot?.RetrievedAtUtc,
            userSnapshot?.RetrievedAtUtc
        }.Max();
    }

    private void SetState(SessionViewModel nextState)
    {
        State = nextState;
        Changed?.Invoke();
    }
}
