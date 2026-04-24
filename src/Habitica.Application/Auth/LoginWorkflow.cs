using Habitica.Api;
using Habitica.Domain.Auth;
using Habitica.Storage;

namespace Habitica.Application.Auth;

public sealed class LoginWorkflow
{
    private readonly IHabiticaSyncClient _habiticaSyncClient;
    private readonly ICredentialStore _credentialStore;
    private readonly ITaskSnapshotStore _taskSnapshotStore;

    public LoginWorkflow(
        IHabiticaSyncClient habiticaSyncClient,
        ICredentialStore credentialStore,
        ITaskSnapshotStore taskSnapshotStore)
    {
        _habiticaSyncClient = habiticaSyncClient;
        _credentialStore = credentialStore;
        _taskSnapshotStore = taskSnapshotStore;
    }

    public async Task<LoginResult> AuthenticateAndSyncAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var credentials = new HabiticaCredentials(command.UserId, command.ApiToken);
        var user = await _habiticaSyncClient.GetUserAsync(credentials, cancellationToken);
        var tasks = await _habiticaSyncClient.GetTasksAsync(credentials, cancellationToken);

        if (command.PersistLocally)
        {
            await _credentialStore.SavePersistentCredentialsAsync(credentials, cancellationToken);
        }
        else
        {
            await _credentialStore.ClearPersistentCredentialsAsync(cancellationToken);
        }

        await _taskSnapshotStore.SaveAsync(tasks, cancellationToken);

        return new LoginResult(
            user.DisplayName,
            user.ClassName,
            user.Level,
            tasks.Items.Count,
            tasks.RetrievedAtUtc);
    }
}
