using Habitica.Application.Auth;
using Habitica.Application.Sync;
using Habitica.Domain.Auth;
using Habitica.Domain.Sync;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed class AppSessionController : IAppSessionController
{
    private readonly ICredentialStore _credentialStore;
    private readonly LoginWorkflow _loginWorkflow;
    private readonly SnapshotFreshnessPolicy _snapshotFreshnessPolicy;
    private readonly ITaskSnapshotStore _taskSnapshotStore;
    private readonly TimeProvider _timeProvider;
    private HabiticaCredentials? _currentCredentials;
    private bool _initialized;
    private bool _persistLocally;

    public AppSessionController(
        LoginWorkflow loginWorkflow,
        ICredentialStore credentialStore,
        ITaskSnapshotStore taskSnapshotStore,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy,
        TimeProvider timeProvider)
    {
        _loginWorkflow = loginWorkflow;
        _credentialStore = credentialStore;
        _taskSnapshotStore = taskSnapshotStore;
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
        await LoadCachedSnapshotAsync(cancellationToken);

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

        SetState(State with
        {
            DisplayName = null,
            ClassName = null,
            ErrorMessage = null,
            IsAuthenticated = false,
            Level = null
        });

        return Task.CompletedTask;
    }

    public async Task ClearLocalDataAsync(CancellationToken cancellationToken = default)
    {
        _currentCredentials = null;
        _persistLocally = false;

        await _credentialStore.ClearPersistentCredentialsAsync(cancellationToken);
        await _taskSnapshotStore.ClearAsync(cancellationToken);

        SetState(SessionViewModel.Empty);
    }

    private async Task LoadCachedSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);

        SetState(State with
        {
            LastSyncedAtUtc = snapshot?.RetrievedAtUtc,
            TaskFreshness = ClassifyFreshness(snapshot),
            TaskSnapshot = snapshot
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
            var snapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);

            _currentCredentials = new HabiticaCredentials(request.UserId.Trim(), request.ApiToken.Trim());
            _persistLocally = request.PersistLocally;

            SetState(new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: loginResult.DisplayName,
                ErrorMessage: null,
                LastSyncedAtUtc: loginResult.RetrievedAtUtc,
                TaskFreshness: ClassifyFreshness(snapshot),
                TaskSnapshot: snapshot,
                ClassName: loginResult.ClassName,
                Level: loginResult.Level));
        }
        catch (Exception exception)
        {
            var snapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                IsBusy = false,
                LastSyncedAtUtc = snapshot?.RetrievedAtUtc ?? State.LastSyncedAtUtc,
                TaskFreshness = ClassifyFreshness(snapshot ?? State.TaskSnapshot),
                TaskSnapshot = snapshot ?? State.TaskSnapshot
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

    private void SetState(SessionViewModel nextState)
    {
        State = nextState;
        Changed?.Invoke();
    }
}
