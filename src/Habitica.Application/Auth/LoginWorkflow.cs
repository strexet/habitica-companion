using Habitica.Api;
using Habitica.Domain.Auth;
using Habitica.Storage;

namespace Habitica.Application.Auth;

public sealed class LoginWorkflow
{
    private readonly IHabiticaSyncClient _habiticaSyncClient;
    private readonly ICredentialStore _credentialStore;
    private readonly IPartySnapshotStore _partySnapshotStore;
    private readonly ITaskSnapshotStore _taskSnapshotStore;
    private readonly IUserSnapshotStore _userSnapshotStore;

    public LoginWorkflow(
        IHabiticaSyncClient habiticaSyncClient,
        ICredentialStore credentialStore,
        ITaskSnapshotStore taskSnapshotStore,
        IUserSnapshotStore userSnapshotStore,
        IPartySnapshotStore partySnapshotStore)
    {
        _habiticaSyncClient = habiticaSyncClient;
        _credentialStore = credentialStore;
        _taskSnapshotStore = taskSnapshotStore;
        _userSnapshotStore = userSnapshotStore;
        _partySnapshotStore = partySnapshotStore;
    }

    public async Task<LoginResult> AuthenticateAndSyncAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var credentials = new HabiticaCredentials(command.UserId, command.ApiToken);
        var user = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
        var tasks = await _habiticaSyncClient.GetTasksAsync(credentials, cancellationToken);
        Habitica.Domain.Party.PartySnapshot? party = null;

        if (!string.IsNullOrWhiteSpace(user.PartyId))
        {
            party = await _habiticaSyncClient.GetPartySnapshotAsync(credentials, cancellationToken);
        }

        if (command.PersistLocally)
        {
            await _credentialStore.SavePersistentCredentialsAsync(credentials, cancellationToken);
        }
        else
        {
            await _credentialStore.ClearPersistentCredentialsAsync(cancellationToken);
        }

        await _taskSnapshotStore.SaveAsync(tasks, cancellationToken);
        await _userSnapshotStore.SaveAsync(user, cancellationToken);

        if (party is null)
        {
            await _partySnapshotStore.ClearAsync(cancellationToken);
        }
        else
        {
            await _partySnapshotStore.SaveAsync(party, cancellationToken);
        }

        return new LoginResult(
            user.DisplayName,
            user.ClassName,
            user.Level,
            tasks.Items.Count,
            tasks.RetrievedAtUtc);
    }
}
