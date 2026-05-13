using System.Globalization;
using Habitica.Api;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Auth;
using Habitica.Storage;

namespace Habitica.Application.Auth;

public sealed class LoginWorkflow
{
    private readonly IHabiticaSyncClient _habiticaSyncClient;
    private readonly ICredentialStore _credentialStore;
    private readonly IPartyCronHistoryStore _partyCronHistoryStore;
    private readonly IPartySnapshotStore _partySnapshotStore;
    private readonly ITaskSnapshotStore _taskSnapshotStore;
    private readonly IUserSnapshotStore _userSnapshotStore;
    private readonly DiagnosticsLogWriter _diagnosticsLogWriter;

    public LoginWorkflow(
        IHabiticaSyncClient habiticaSyncClient,
        ICredentialStore credentialStore,
        ITaskSnapshotStore taskSnapshotStore,
        IUserSnapshotStore userSnapshotStore,
        IPartySnapshotStore partySnapshotStore,
        IPartyCronHistoryStore partyCronHistoryStore,
        DiagnosticsLogWriter diagnosticsLogWriter)
    {
        _habiticaSyncClient = habiticaSyncClient;
        _credentialStore = credentialStore;
        _taskSnapshotStore = taskSnapshotStore;
        _userSnapshotStore = userSnapshotStore;
        _partySnapshotStore = partySnapshotStore;
        _partyCronHistoryStore = partyCronHistoryStore;
        _diagnosticsLogWriter = diagnosticsLogWriter;
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
            var cronEvents = Habitica.Domain.Party.PartyCronCalculator.CreateHistoryEvents(party);
            var cronHistory = await _partyCronHistoryStore.UpsertAsync(cronEvents, party.RetrievedAtUtc, cancellationToken);
            var cronDashboard = Habitica.Domain.Party.PartyCronCalculator.BuildDashboard(
                party,
                cronHistory,
                command.UserId,
                party.RetrievedAtUtc,
                TimeZoneInfo.Local);
            party = party with
            {
                Members = cronDashboard.Members,
                CronDashboard = cronDashboard
            };
            if (party.Quest is not null)
            {
                party = party with
                {
                    Quest = Habitica.Domain.Party.PartyQuestProgressCalculator.Enrich(
                        party,
                        party.Quest,
                        command.UserId,
                        party.RetrievedAtUtc,
                        TimeZoneInfo.Local,
                        includeStaleMembers: false)
                };
            }

            await _partySnapshotStore.SaveAsync(party, cancellationToken);
        }

        var requestCount = string.IsNullOrWhiteSpace(user.PartyId)
            ? 2
            : party?.Quest?.BossHealthRemaining is not null
                ? 5
                : 4;

        await _diagnosticsLogWriter.WriteAsync(
            DiagnosticsFeatureArea.Auth,
            "sign-in",
            DiagnosticsSeverity.Success,
            DiagnosticsMode.LiveRead,
            $"Signed in and refreshed account snapshots for {user.DisplayName}.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture),
                ["taskCount"] = tasks.Items.Count.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        return new LoginResult(
            user.DisplayName,
            user.ClassName,
            user.Level,
            tasks.Items.Count,
            tasks.RetrievedAtUtc);
    }
}
