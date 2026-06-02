using System.Globalization;
using System.Text.Json;
using Habitica.Application.Auth;
using Habitica.Application.Diagnostics;
using Habitica.Application.Inventory;
using Habitica.Application.Sync;
using Habitica.Api;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Rules.Spells;
using Habitica.Storage;
using Habitica.WebApp.Sync;

namespace Habitica.WebApp.State;

public sealed class AppSessionController : IAppSessionController
{
    private const decimal HealthPotionGoldCost = 25m;
    private readonly ICredentialStore _credentialStore;
    private readonly IDiagnosticsLogStore _diagnosticsLogStore;
    private readonly DiagnosticsLogWriter _diagnosticsLogWriter;
    private readonly DiagnosticsPresetWorkflow _diagnosticsPresetWorkflow;
    private readonly IEquipmentPresetStore _equipmentPresetStore;
    private readonly IGearCatalogStore _gearCatalogStore;
    private readonly IHabiticaSyncClient _habiticaSyncClient;
    private readonly LoginWorkflow _loginWorkflow;
    private readonly LiveTestWorkflow _liveTestWorkflow;
    private readonly IPartyCronHistoryStore _partyCronHistoryStore;
    private readonly IPartySnapshotStore _partySnapshotStore;
    private readonly LocalUserDataPortabilityService _localUserDataPortabilityService;
    private readonly IRemotePartyDataSyncProvider _remotePartyDataSyncProvider;
    private readonly IRemoteUserDataSyncProvider _remoteUserDataSyncProvider;
    private readonly SnapshotFreshnessPolicy _snapshotFreshnessPolicy;
    private readonly ITaskSnapshotStore _taskSnapshotStore;
    private readonly IUserSnapshotStore _userSnapshotStore;
    private readonly RefreshCoordinator _refreshCoordinator;
    private readonly AppFeatureOptions _featureOptions;
    private readonly TimeProvider _timeProvider;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private HabiticaCredentials? _currentCredentials;
    private readonly HashSet<CloudSyncSection> _cloudSyncExcludedSections;
    private readonly SemaphoreSlim _partySyncSemaphore = new(1, 1);
    private bool _includeStalePartyMembersInQuestForecasts;
    private bool _initialized;
    private bool _persistLocally;

    public AppSessionController(
        LoginWorkflow loginWorkflow,
        IHabiticaSyncClient habiticaSyncClient,
        LiveTestWorkflow liveTestWorkflow,
        DiagnosticsPresetWorkflow diagnosticsPresetWorkflow,
        ICredentialStore credentialStore,
        IEquipmentPresetStore equipmentPresetStore,
        IGearCatalogStore gearCatalogStore,
        IPartyCronHistoryStore partyCronHistoryStore,
        IPartySnapshotStore partySnapshotStore,
        LocalUserDataPortabilityService localUserDataPortabilityService,
        RefreshCoordinator refreshCoordinator,
        IRemotePartyDataSyncProvider remotePartyDataSyncProvider,
        IRemoteUserDataSyncProvider remoteUserDataSyncProvider,
        ITaskSnapshotStore taskSnapshotStore,
        IUserSnapshotStore userSnapshotStore,
        IDiagnosticsLogStore diagnosticsLogStore,
        DiagnosticsLogWriter diagnosticsLogWriter,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy,
        AppFeatureOptions featureOptions,
        TimeProvider timeProvider)
    {
        _loginWorkflow = loginWorkflow;
        _habiticaSyncClient = habiticaSyncClient;
        _liveTestWorkflow = liveTestWorkflow;
        _diagnosticsPresetWorkflow = diagnosticsPresetWorkflow;
        _credentialStore = credentialStore;
        _equipmentPresetStore = equipmentPresetStore;
        _gearCatalogStore = gearCatalogStore;
        _partyCronHistoryStore = partyCronHistoryStore;
        _partySnapshotStore = partySnapshotStore;
        _localUserDataPortabilityService = localUserDataPortabilityService;
        _refreshCoordinator = refreshCoordinator;
        _remotePartyDataSyncProvider = remotePartyDataSyncProvider;
        _remoteUserDataSyncProvider = remoteUserDataSyncProvider;
        _taskSnapshotStore = taskSnapshotStore;
        _userSnapshotStore = userSnapshotStore;
        _diagnosticsLogStore = diagnosticsLogStore;
        _diagnosticsLogWriter = diagnosticsLogWriter;
        _snapshotFreshnessPolicy = snapshotFreshnessPolicy;
        _featureOptions = featureOptions;
        _timeProvider = timeProvider;
        _cloudSyncExcludedSections = ParseCloudSyncExcludedSections(featureOptions.CloudSyncExcludedSections);
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

    public async Task RefreshForPageAsync(string pageRoute, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return;
        }

        if (!State.IsAuthenticated)
        {
            return;
        }

        var pageDomains = ResolvePageDomains(pageRoute);
        var allDomains = new[] { RefreshDomain.UserProfile, RefreshDomain.Tasks, RefreshDomain.Party, RefreshDomain.GearCatalog };
        var requests = new List<(RefreshDomain Domain, RefreshPriority Priority)>();

        foreach (var domain in pageDomains)
        {
            requests.Add((domain, RefreshPriority.Visible));
        }

        foreach (var domain in allDomains.Where(d => !pageDomains.Contains(d)))
        {
            requests.Add((domain, RefreshPriority.Background));
        }

        var domainStates = MarkDomainsFetching(requests, RefreshReason.ManualRefresh);
        SetState(State with { ErrorMessage = null, DomainStates = domainStates });

        await _refreshCoordinator.RefreshDomainsAsync(
            credentials,
            requests,
            state =>
            {
                domainStates[state.Domain] = state;
                LoadCachedStateAndNotify(domainStates, cancellationToken);
            },
            cancellationToken,
            RefreshReason.ManualRefresh);

        await LoadCachedStateAsync(cancellationToken);
        SetState(State with { DomainStates = domainStates });
        _ = TryMergeAndUploadCloudSyncAsync(credentials, cancellationToken, RefreshReason.ManualRefresh);
        _ = TryMergeAndUploadPartySyncAsync(credentials, cancellationToken);
    }

    private void LoadCachedStateAndNotify(
        Dictionary<RefreshDomain, DomainRefreshState> domainStates,
        CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            await LoadCachedStateAsync(cancellationToken);
            PreserveSyncDomainStates(domainStates);
            SetState(State with { DomainStates = new Dictionary<RefreshDomain, DomainRefreshState>(domainStates) });
        }, cancellationToken);
    }

    private Dictionary<RefreshDomain, DomainRefreshState> MarkDomainsFetching(
        IReadOnlyList<(RefreshDomain Domain, RefreshPriority Priority)> requests,
        RefreshReason reason)
    {
        var domainStates = State.DomainStates is null
            ? new Dictionary<RefreshDomain, DomainRefreshState>()
            : new Dictionary<RefreshDomain, DomainRefreshState>(State.DomainStates);
        foreach (var request in requests)
        {
            domainStates[request.Domain] = new DomainRefreshState(
                request.Domain,
                true,
                domainStates.TryGetValue(request.Domain, out var existing) ? existing.LastRefreshedAtUtc : null,
                null,
                reason,
                request.Priority);
        }

        return domainStates;
    }

    private static IReadOnlyList<RefreshDomain> ResolvePageDomains(string pageRoute)
    {
        var path = new Uri(pageRoute, UriKind.RelativeOrAbsolute).IsAbsoluteUri
            ? new Uri(pageRoute).AbsolutePath
            : pageRoute;

        path = path.TrimEnd('/').ToLowerInvariant();

        return path switch
        {
            "" or "/" or "/dashboard" => new[] { RefreshDomain.UserProfile, RefreshDomain.Tasks, RefreshDomain.GearCatalog },
            "/tasks" => new[] { RefreshDomain.Tasks, RefreshDomain.UserProfile },
            "/party" => new[] { RefreshDomain.Party, RefreshDomain.UserProfile, RefreshDomain.GearCatalog },
            "/inventory" => new[] { RefreshDomain.UserProfile, RefreshDomain.GearCatalog },
            "/spells" => new[] { RefreshDomain.UserProfile, RefreshDomain.Tasks, RefreshDomain.GearCatalog },
            _ => new[] { RefreshDomain.UserProfile, RefreshDomain.Tasks }
        };
    }

    public async Task SetIncludeStalePartyMembersAsync(bool include, CancellationToken cancellationToken = default)
    {
        _includeStalePartyMembersInQuestForecasts = include;
        await RebuildDerivedLocalStateAsync(State.UserId ?? _currentCredentials?.UserId, cancellationToken);
        await LoadCachedStateAsync(cancellationToken);
    }

    public async Task<PartyQuestActionResult> RefreshPartyQuestStateAsync(CancellationToken cancellationToken = default)
    {
        if (!_featureOptions.PartySyncEnabled)
        {
            return PartyQuestActionResult.Failure("Shared party sync is disabled.");
        }

        var claim = await ResolvePartySyncClaimAsync(cancellationToken);
        if (claim is null)
        {
            return PartyQuestActionResult.Failure("Sign in with an active party before loading shared quest state.");
        }

        try
        {
            var publishedPool = await PublishCurrentQuestPoolAsync(claim, cancellationToken);
            if (publishedPool is not null)
            {
                ApplyPartyQuestState(claim.PartyId, publishedPool);
                return PartyQuestActionResult.Success("Shared quest state refreshed.");
            }

            var remoteSnapshot = await _remotePartyDataSyncProvider.DownloadAsync(claim, cancellationToken);
            ApplyPartyQuestState(claim.PartyId, remoteSnapshot);
            return PartyQuestActionResult.Success("Shared quest state refreshed.");
        }
        catch (Exception exception)
        {
            SetState(State with { ErrorMessage = exception.Message });
            return PartyQuestActionResult.Failure(exception.Message);
        }
    }

    public async Task<PartyQuestActionResult> AddPartyQuestToQueueAsync(string questKey, CancellationToken cancellationToken = default)
    {
        if (!_featureOptions.PartySyncEnabled)
        {
            return PartyQuestActionResult.Failure("Shared party sync is disabled.");
        }

        var validation = await ValidatePartyQuestMutationAsync(questKey, cancellationToken);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        try
        {
            var state = await _remotePartyDataSyncProvider.AddQuestQueueItemAsync(
                validation.Claim!,
                validation.PoolEntry!,
                cancellationToken);
            ApplyPartyQuestState(validation.PartyId!, state);
            return PartyQuestActionResult.Success($"{validation.PoolEntry!.QuestName} was added to the party queue.");
        }
        catch (Exception exception)
        {
            SetState(State with { ErrorMessage = exception.Message });
            return PartyQuestActionResult.Failure(exception.Message);
        }
    }

    public async Task<PartyQuestActionResult> TogglePartyQuestVoteAsync(string queueItemId, CancellationToken cancellationToken = default)
    {
        if (!_featureOptions.PartySyncEnabled)
        {
            return PartyQuestActionResult.Failure("Shared party sync is disabled.");
        }

        var claim = await ResolvePartySyncClaimAsync(cancellationToken);
        if (claim is null)
        {
            return PartyQuestActionResult.Failure("Sign in with an active party before voting.");
        }

        try
        {
            var state = await _remotePartyDataSyncProvider.ToggleQuestVoteAsync(
                claim,
                queueItemId,
                State.DisplayName ?? claim.UserId,
                cancellationToken);
            ApplyPartyQuestState(claim.PartyId, state);
            return PartyQuestActionResult.Success("Quest vote updated.");
        }
        catch (Exception exception)
        {
            SetState(State with { ErrorMessage = exception.Message });
            return PartyQuestActionResult.Failure(exception.Message);
        }
    }

    public async Task<PartyQuestActionResult> RemovePartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        if (!_featureOptions.PartySyncEnabled)
        {
            return PartyQuestActionResult.Failure("Shared party sync is disabled.");
        }

        var claim = await ResolvePartySyncClaimAsync(cancellationToken);
        if (claim is null)
        {
            return PartyQuestActionResult.Failure("Sign in with an active party before removing queue items.");
        }

        try
        {
            var state = await _remotePartyDataSyncProvider.RemoveQuestQueueItemAsync(
                claim,
                queueItemId,
                version,
                cancellationToken);
            ApplyPartyQuestState(claim.PartyId, state);
            return PartyQuestActionResult.Success("Quest queue item removed.");
        }
        catch (Exception exception)
        {
            SetState(State with { ErrorMessage = exception.Message });
            return PartyQuestActionResult.Failure(exception.Message);
        }
    }

    public async Task<PartyQuestActionResult> PinPartyQuestQueueItemAsync(string queueItemId, int version, bool pinned, CancellationToken cancellationToken = default)
    {
        return await RunPartyQueueActionAsync(
            claim => _remotePartyDataSyncProvider.PinQuestQueueItemAsync(claim, queueItemId, version, pinned, cancellationToken),
            pinned ? "Quest pinned." : "Quest unpinned.",
            "Sign in with an active party before pinning queue items.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> SelectPartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        return await RunPartyQueueActionAsync(
            claim => _remotePartyDataSyncProvider.SelectQuestQueueItemAsync(claim, queueItemId, version, cancellationToken),
            "Next quest selected.",
            "Sign in with an active party before selecting queue items.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> SkipPartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        return await RunPartyQueueActionAsync(
            claim => _remotePartyDataSyncProvider.SkipQuestQueueItemAsync(claim, queueItemId, version, cancellationToken),
            "Quest skipped.",
            "Sign in with an active party before skipping queue items.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> ExpirePartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        return await RunPartyQueueActionAsync(
            claim => _remotePartyDataSyncProvider.ExpireQuestQueueItemAsync(claim, queueItemId, version, cancellationToken),
            "Quest expired.",
            "Sign in with an active party before expiring queue items.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> RequeuePartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        return await RunPartyQueueActionAsync(
            claim => _remotePartyDataSyncProvider.RequeueQuestQueueItemAsync(claim, queueItemId, version, cancellationToken),
            "Quest returned to queue.",
            "Sign in with an active party before requeueing quests.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> MarkPartyQuestCompletedAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        if (!_featureOptions.PartySyncEnabled)
        {
            return PartyQuestActionResult.Failure("Shared party sync is disabled.");
        }

        var claim = await ResolvePartySyncClaimAsync(cancellationToken);
        if (claim is null)
        {
            return PartyQuestActionResult.Failure("Sign in with an active party before marking quests completed.");
        }

        try
        {
            var entry = State.PartyQuestQueue?.Queue.FirstOrDefault(candidate =>
                string.Equals(candidate.QueueItemId, queueItemId, StringComparison.Ordinal));
            var participantsCount = entry is not null
                && string.Equals(State.PartySnapshot?.Quest?.Key, entry.QuestKey, StringComparison.Ordinal)
                ? State.PartySnapshot?.Quest?.ParticipantCount
                : null;
            var state = await _remotePartyDataSyncProvider.MarkQuestCompletedAsync(
                claim,
                queueItemId,
                version,
                participantsCount,
                cancellationToken);
            ApplyPartyQuestState(claim.PartyId, state);
            return PartyQuestActionResult.Success("Quest marked completed.");
        }
        catch (Exception exception)
        {
            SetState(State with { ErrorMessage = exception.Message });
            return PartyQuestActionResult.Failure(exception.Message);
        }
    }

    public async Task<PartyQuestActionResult> RemovePartyRecentlyCompletedQuestAsync(
        string questKey,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        return await RunPartyQueueActionAsync(
            claim => _remotePartyDataSyncProvider.RemoveRecentlyCompletedQuestAsync(claim, questKey, completedAtUtc, cancellationToken),
            "Completed quest removed.",
            "Sign in with an active party before removing completed quest history.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> InvitePartyToQuestAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return PartyQuestActionResult.Failure("Sign in before inviting the party to quests.");
        }

        var entry = State.PartyQuestQueue?.Queue.FirstOrDefault(candidate =>
            string.Equals(candidate.QueueItemId, queueItemId, StringComparison.Ordinal));
        if (entry is null)
        {
            return PartyQuestActionResult.Failure("Quest queue item was not found.");
        }

        if (!string.Equals(entry.OwnerUserId, credentials.UserId, StringComparison.Ordinal))
        {
            return PartyQuestActionResult.Failure("Only the quest owner can invite the party.");
        }

        if (entry.Status is not PartyQuestQueueStatus.Selected)
        {
            return PartyQuestActionResult.Failure("Select the quest as Next Quest before inviting the party.");
        }

        if (State.PartyFreshness != SnapshotFreshnessState.Fresh)
        {
            return PartyQuestActionResult.Failure("Refresh party data before inviting.");
        }

        if (State.PartySnapshot?.Quest is not null)
        {
            return PartyQuestActionResult.Failure("The party already has a quest invitation or active quest.");
        }

        SetState(State with { ErrorMessage = null, IsBusy = true });

        try
        {
            await _habiticaSyncClient.InvitePartyToQuestAsync(credentials, entry.QuestKey, cancellationToken);
            var partySnapshot = await _habiticaSyncClient.GetPartySnapshotAsync(credentials, cancellationToken);
            await _partySnapshotStore.SaveAsync(partySnapshot, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);

            var claim = await ResolvePartySyncClaimAsync(cancellationToken);
            if (claim is not null)
            {
                try
                {
                    var remoteState = await _remotePartyDataSyncProvider.InvitePartyAsync(
                        claim,
                        queueItemId,
                        version,
                        cancellationToken);
                    ApplyPartyQuestState(claim.PartyId, remoteState);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await _diagnosticsLogWriter.WriteAsync(
                        DiagnosticsFeatureArea.Party,
                        "party-quest-invite-sync",
                        DiagnosticsSeverity.Warning,
                        DiagnosticsMode.Local,
                        $"Quest invited, but shared queue invite status failed: {exception.Message}",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["queueItemId"] = queueItemId,
                            ["questKey"] = entry.QuestKey
                        },
                        cancellationToken);
                }
            }

            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Party,
                "party-quest-invite",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                $"Invited party to quest '{entry.QuestName}'.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["queueItemId"] = queueItemId,
                    ["questKey"] = entry.QuestKey,
                    ["requestCount"] = "2"
                },
                cancellationToken);

            _ = TryMergeAndUploadPartySyncAsync(credentials, cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });
            return PartyQuestActionResult.Success("Party invited to quest.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            return PartyQuestActionResult.Failure(exception.Message);
        }
    }

    public async Task<PartyQuestActionResult> AcceptPartyQuestInvitationAsync(CancellationToken cancellationToken = default)
    {
        return await RespondToPartyQuestInvitationAsync(
            accept: true,
            operation: "party-quest-accept",
            successMessage: "Quest invitation accepted.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> RejectPartyQuestInvitationAsync(CancellationToken cancellationToken = default)
    {
        return await RespondToPartyQuestInvitationAsync(
            accept: false,
            operation: "party-quest-reject",
            successMessage: "Quest invitation rejected.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> StartSelectedPartyQuestAsync(string queueItemId, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return PartyQuestActionResult.Failure("Sign in before starting selected quests.");
        }

        var entry = State.PartyQuestQueue?.Queue.FirstOrDefault(candidate =>
            string.Equals(candidate.QueueItemId, queueItemId, StringComparison.Ordinal));
        if (entry is null)
        {
            return PartyQuestActionResult.Failure("Selected quest was not found.");
        }

        var canForceStart = string.Equals(entry.OwnerUserId, credentials.UserId, StringComparison.Ordinal)
            || string.Equals(State.PartySnapshot?.LeaderId, credentials.UserId, StringComparison.Ordinal);
        if (!canForceStart)
        {
            return PartyQuestActionResult.Failure("Only the selected quest owner or party leader can start it.");
        }

        if (entry.Status is not (PartyQuestQueueStatus.Selected or PartyQuestQueueStatus.InviteSent))
        {
            return PartyQuestActionResult.Failure("Only selected quests can be started.");
        }

        var quest = State.PartySnapshot?.Quest;
        if (quest is null
            || quest.IsActive
            || string.IsNullOrWhiteSpace(quest.Key)
            || !string.Equals(quest.Key, entry.QuestKey, StringComparison.Ordinal))
        {
            return PartyQuestActionResult.Failure("Refresh party quest state before starting the selected quest.");
        }

        SetState(State with { ErrorMessage = null, IsBusy = true });

        try
        {
            await _habiticaSyncClient.StartPartyQuestAsync(credentials, cancellationToken);
            var partySnapshot = await _habiticaSyncClient.GetPartySnapshotAsync(credentials, cancellationToken);
            await _partySnapshotStore.SaveAsync(partySnapshot, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);

            var claim = await ResolvePartySyncClaimAsync(cancellationToken);
            if (claim is not null)
            {
                try
                {
                    var remoteState = await _remotePartyDataSyncProvider.ReconcileQuestLifecycleAsync(
                        claim,
                        entry.QueueItemId,
                        entry.QuestKey,
                        "activate",
                        null,
                        null,
                        null,
                        cancellationToken);
                    ApplyPartyQuestState(claim.PartyId, remoteState);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await _diagnosticsLogWriter.WriteAsync(
                        DiagnosticsFeatureArea.Party,
                        "party-quest-start-reconcile",
                        DiagnosticsSeverity.Warning,
                        DiagnosticsMode.Local,
                        $"Quest started, but shared queue activation failed: {exception.Message}",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["queueItemId"] = entry.QueueItemId,
                            ["questKey"] = entry.QuestKey
                        },
                        cancellationToken);
                }
            }

            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Party,
                "party-quest-start",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                $"Started quest '{entry.QuestName}'.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["queueItemId"] = entry.QueueItemId,
                    ["questKey"] = entry.QuestKey,
                    ["requestCount"] = "2"
                },
                cancellationToken);

            _ = TryMergeAndUploadPartySyncAsync(credentials, cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });
            return PartyQuestActionResult.Success("Quest started.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            return PartyQuestActionResult.Failure(exception.Message);
        }
    }

    public async Task<PartyQuestActionResult> AssignPartySyncOfficerAsync(string userId, string displayName, CancellationToken cancellationToken = default)
    {
        return await RunPartySyncManagementActionAsync(
            claim => _remotePartyDataSyncProvider.AssignOfficerAsync(claim, userId, displayName, cancellationToken),
            "Officer assigned.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> AssignPartySyncOwnerAsync(string userId, string displayName, CancellationToken cancellationToken = default)
    {
        return await RunPartySyncManagementActionAsync(
            claim => _remotePartyDataSyncProvider.AssignPartyOwnerAsync(claim, userId, displayName, cancellationToken),
            "Party owner assigned.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> RemovePartySyncOfficerAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await RunPartySyncManagementActionAsync(
            claim => _remotePartyDataSyncProvider.RemoveOfficerAsync(claim, userId, cancellationToken),
            "Officer removed.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> KickPartySyncMemberAsync(string userId, string displayName, string? reason, CancellationToken cancellationToken = default)
    {
        return await RunPartySyncManagementActionAsync(
            claim => _remotePartyDataSyncProvider.KickMemberAsync(claim, userId, displayName, reason, cancellationToken),
            "Member removed from party sync.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> UnkickPartySyncMemberAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await RunPartySyncManagementActionAsync(
            claim => _remotePartyDataSyncProvider.UnkickMemberAsync(claim, userId, cancellationToken),
            "Member restored to party sync.",
            cancellationToken);
    }

    public async Task<PartyQuestActionResult> UpdatePartySyncSettingsAsync(PartySyncSettings settings, CancellationToken cancellationToken = default)
    {
        return await RunPartySyncManagementActionAsync(
            claim => _remotePartyDataSyncProvider.UpdateSettingsAsync(claim, settings, cancellationToken),
            "Party sync settings updated.",
            cancellationToken);
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

    public async Task<InventoryActionResult> SaveEquipmentPresetAsync(
        EquipmentSetKind kind,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (State.UserSnapshot is null)
        {
            return await FailInventoryActionAsync("inventory-save-preset", "Sign in and refresh account data before saving equipment presets.", cancellationToken);
        }

        var slots = kind == EquipmentSetKind.Battle ? State.UserSnapshot.Equipment.Battle : State.UserSnapshot.Equipment.Costume;
        return await SaveEquipmentPresetCoreAsync(kind, name, slots, "inventory-save-preset", cancellationToken);
    }

    public async Task<InventoryActionResult> SaveEquipmentPresetAsync(
        EquipmentSetKind kind,
        string name,
        GearSlotsSnapshot slots,
        CancellationToken cancellationToken = default)
    {
        return await SaveEquipmentPresetCoreAsync(kind, name, slots, "inventory-save-recommendation", cancellationToken);
    }

    private async Task<InventoryActionResult> SaveEquipmentPresetCoreAsync(
        EquipmentSetKind kind,
        string name,
        GearSlotsSnapshot slots,
        string diagnosticsOperation,
        CancellationToken cancellationToken)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null || State.UserSnapshot is null)
        {
            return await FailInventoryActionAsync(diagnosticsOperation, "Sign in and refresh account data before saving equipment presets.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return await FailInventoryActionAsync(diagnosticsOperation, "Preset name is required.", cancellationToken);
        }

        var preset = new EquipmentPreset(
            Guid.NewGuid().ToString("N"),
            credentials.UserId,
            kind,
            name.Trim(),
            _timeProvider.GetUtcNow(),
            NormalizePresetSlots(kind, slots));

        try
        {
            await _equipmentPresetStore.SaveAsync(preset, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                diagnosticsOperation,
                DiagnosticsSeverity.Success,
                DiagnosticsMode.Local,
                $"Saved {kind.ToString().ToLowerInvariant()} preset '{preset.Name}'.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = preset.Id,
                    ["presetName"] = preset.Name,
                    ["presetKind"] = kind.ToString()
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            await TryUploadCloudSyncSectionAsync(credentials, CloudSyncSection.SavedPresets, cancellationToken);
            return InventoryActionResult.Success($"Saved preset {preset.Name}.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            return await FailInventoryActionAsync(
                diagnosticsOperation,
                exception.Message,
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetName"] = name.Trim(),
                    ["presetKind"] = kind.ToString()
                });
        }
    }

    public async Task<InventoryActionResult> RemoveEquipmentPresetAsync(string presetId, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return await FailInventoryActionAsync("inventory-remove-preset", "Sign in before removing equipment presets.", cancellationToken);
        }

        var preset = State.Presets.FirstOrDefault(item => string.Equals(item.Id, presetId, StringComparison.Ordinal));
        await _equipmentPresetStore.RemoveAsync(credentials.UserId, presetId, cancellationToken);
        await _diagnosticsLogWriter.WriteAsync(
            DiagnosticsFeatureArea.Inventory,
            "inventory-remove-preset",
            DiagnosticsSeverity.Success,
            DiagnosticsMode.Local,
            preset is null ? "Removed equipment preset." : $"Removed preset '{preset.Name}'.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["presetId"] = presetId,
                ["presetName"] = preset?.Name ?? "",
                ["presetKind"] = preset?.Kind.ToString() ?? ""
            },
            cancellationToken);
        await LoadCachedStateAsync(cancellationToken);
        await TryUploadCloudSyncSectionAsync(credentials, CloudSyncSection.SavedPresets, cancellationToken);
        await LoadCachedStateAsync(cancellationToken);
        return InventoryActionResult.Success(preset is null ? "Removed preset." : $"Removed preset {preset.Name}.");
    }

    public async Task<InventoryActionResult> RenameEquipmentPresetAsync(
        string presetId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return await FailInventoryActionAsync("inventory-rename-preset", "Sign in before renaming equipment presets.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return await FailInventoryActionAsync(
                "inventory-rename-preset",
                "Preset name is required.",
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = presetId
                });
        }

        var preset = State.Presets.FirstOrDefault(item => string.Equals(item.Id, presetId, StringComparison.Ordinal));
        if (preset is null)
        {
            return await FailInventoryActionAsync(
                "inventory-rename-preset",
                "Equipment preset was not found.",
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = presetId
                });
        }

        var renamedPreset = preset with { Name = name.Trim() };
        try
        {
            await _equipmentPresetStore.SaveAsync(renamedPreset, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-rename-preset",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.Local,
                $"Renamed preset '{preset.Name}' to '{renamedPreset.Name}'.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = preset.Id,
                    ["presetName"] = renamedPreset.Name,
                    ["previousPresetName"] = preset.Name,
                    ["presetKind"] = preset.Kind.ToString()
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            await TryUploadCloudSyncSectionAsync(credentials, CloudSyncSection.SavedPresets, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            return InventoryActionResult.Success($"Renamed preset {renamedPreset.Name}.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            return await FailInventoryActionAsync(
                "inventory-rename-preset",
                exception.Message,
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = preset.Id,
                    ["presetName"] = renamedPreset.Name,
                    ["presetKind"] = preset.Kind.ToString()
                });
        }
    }

    public async Task<InventoryActionResult> EquipInventoryItemAsync(
        EquipmentSetKind kind,
        string key,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateInventoryMutationAsync("inventory-equip-item", cancellationToken);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        var snapshot = validation.Snapshot!;
        if (IsUnequippedBaseKey(key))
        {
            return await FailInventoryActionAsync(
                "inventory-equip-item",
                $"{key} is an unequipped slot marker and cannot be sent to Habitica as gear.",
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemKey"] = key,
                    ["equipmentKind"] = kind.ToString()
                });
        }

        if (!CanUseGearKey(snapshot, kind, key))
        {
            return await FailInventoryActionAsync(
                "inventory-equip-item",
                $"Cannot equip {key} because it is not in the cached owned or equipped gear list.",
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemKey"] = key,
                    ["equipmentKind"] = kind.ToString()
                });
        }

        SetState(State with { ErrorMessage = null, IsBusy = true });

        try
        {
            await _habiticaSyncClient.EquipGearAsync(validation.Credentials!, kind, key, cancellationToken);
            await DelayBetweenHabiticaRequestsAsync(cancellationToken);
            var refreshedSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(validation.Credentials!, cancellationToken);
            await _userSnapshotStore.SaveAsync(refreshedSnapshot, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-equip-item",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                $"Changed {kind.ToString().ToLowerInvariant()} equipment to {key}.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemKey"] = key,
                    ["equipmentKind"] = kind.ToString(),
                    ["requestCount"] = "2"
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            _ = TryMergeAndUploadCloudSyncAsync(validation.Credentials!, cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });
            return InventoryActionResult.Success($"Equipment changed to {ResolveGearName(key)}.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            return await FailInventoryActionAsync(
                "inventory-equip-item",
                exception.Message,
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemKey"] = key,
                    ["equipmentKind"] = kind.ToString()
                });
        }
    }

    public async Task<InventoryActionResult> EquipEquipmentPresetAsync(string presetId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateInventoryMutationAsync("inventory-equip-preset", cancellationToken);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        var preset = State.Presets.FirstOrDefault(item => string.Equals(item.Id, presetId, StringComparison.Ordinal));
        if (preset is null)
        {
            return await FailInventoryActionAsync(
                "inventory-equip-preset",
                "Equipment preset was not found.",
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = presetId
                });
        }

        return await EquipGearSlotsCoreAsync(
            validation.Credentials!,
            validation.Snapshot!,
            preset.Kind,
            NormalizePresetSlots(preset.Kind, preset.Slots),
            $"preset:{preset.Id}",
            $"Equipping {preset.Name}",
            "inventory-equip-preset",
            PresetMetadata(preset),
            preset.Name,
            cancellationToken);
    }

    public async Task<InventoryActionResult> EquipGearSlotsAsync(
        EquipmentSetKind kind,
        GearSlotsSnapshot slots,
        string operationId,
        string label,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateInventoryMutationAsync("inventory-equip-slots", cancellationToken);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        return await EquipGearSlotsCoreAsync(
            validation.Credentials!,
            validation.Snapshot!,
            kind,
            NormalizePresetSlots(kind, slots),
            operationId,
            label,
            "inventory-equip-slots",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["operationId"] = operationId,
                ["label"] = label,
                ["equipmentKind"] = kind.ToString()
            },
            label,
            cancellationToken);
    }

    private async Task<InventoryActionResult> EquipGearSlotsCoreAsync(
        HabiticaCredentials credentials,
        UserSnapshot snapshot,
        EquipmentSetKind kind,
        GearSlotsSnapshot slots,
        string operationId,
        string label,
        string diagnosticsOperation,
        IReadOnlyDictionary<string, string> baseMetadata,
        string resultName,
        CancellationToken cancellationToken)
    {
        var desiredSlots = EnumerateSlots(NormalizePresetSlots(kind, slots)).ToArray();
        foreach (var slot in desiredSlots.Where(slot => !string.IsNullOrWhiteSpace(slot.Key)))
        {
            if (!CanUseGearKey(snapshot, kind, slot.Key!))
            {
                return await FailInventoryActionAsync(
                    diagnosticsOperation,
                    $"Cannot equip {resultName} because {slot.Key} is not owned.",
                    cancellationToken,
                    MergeMetadata(baseMetadata, failedSlot: slot.SlotTitle, itemKey: slot.Key));
            }
        }

        var currentSlots = kind == EquipmentSetKind.Battle
            ? snapshot.Equipment.Battle
            : snapshot.Equipment.Costume;
        var changedSlots = desiredSlots
            .Where(slot => !string.Equals(NormalizeGearKey(GetSlotValue(currentSlots, slot.SlotTitle)), slot.Key, StringComparison.Ordinal))
            .ToArray();

        SetState(State with
        {
            ActiveEquipmentProgress = changedSlots.Length == 0 ? null : new EquipmentProgress(operationId, label, 0, changedSlots.Length),
            ErrorMessage = null,
            IsBusy = true
        });

        var requestCount = 0;
        try
        {
            foreach (var slot in changedSlots)
            {
                var keyToToggle = slot.Key ?? NormalizeGearKey(GetSlotValue(currentSlots, slot.SlotTitle));
                if (string.IsNullOrWhiteSpace(keyToToggle))
                {
                    continue;
                }

                await _habiticaSyncClient.EquipGearAsync(credentials, kind, keyToToggle, cancellationToken);
                requestCount++;
                await DelayBetweenHabiticaRequestsAsync(cancellationToken);
                SetState(State with
                {
                    ActiveEquipmentProgress = new EquipmentProgress(operationId, label, requestCount, changedSlots.Length)
                });
            }

            if (changedSlots.Length > 0)
            {
                var refreshedSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
                requestCount++;
                await _userSnapshotStore.SaveAsync(refreshedSnapshot, cancellationToken);
            }

            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                diagnosticsOperation,
                DiagnosticsSeverity.Success,
                changedSlots.Length == 0 ? DiagnosticsMode.Local : DiagnosticsMode.LiveMutation,
                changedSlots.Length == 0
                    ? $"{resultName} was already equipped."
                    : $"Equipped {resultName}.",
                MergeMetadata(baseMetadata, changedSlots.Length, desiredSlots.Length - changedSlots.Length, requestCount),
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            _ = TryMergeAndUploadCloudSyncAsync(credentials, cancellationToken);
            SetState(State with { ActiveEquipmentProgress = null, ErrorMessage = null, IsBusy = false });
            return InventoryActionResult.Success(changedSlots.Length == 0 ? $"{resultName} was already equipped." : $"Equipped {resultName}.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ActiveEquipmentProgress = null, ErrorMessage = exception.Message, IsBusy = false });
            return await FailInventoryActionAsync(
                diagnosticsOperation,
                exception.Message,
                cancellationToken,
                MergeMetadata(baseMetadata, changedSlots.Length, desiredSlots.Length - changedSlots.Length, requestCount));
        }
    }

    public async Task<SpellActionResult> CastSpellAsync(SpellCastRequest request, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return SpellActionResult.Failure("Sign in is required before casting spells.");
        }

        if (State.UserSnapshot is null || State.UserFreshness != SnapshotFreshnessState.Fresh)
        {
            return SpellActionResult.Failure("A fresh account snapshot is required before casting spells.");
        }

        if (string.IsNullOrWhiteSpace(request.SpellId))
        {
            return SpellActionResult.Failure("Spell id is required.");
        }

        var count = Math.Clamp(request.Count, 1, 99);
        var spell = new SpellViewModelFactory()
            .Create(State.UserSnapshot, State.TaskSnapshot, State.GearCatalogSnapshot)
            .Spells
            .FirstOrDefault(item => string.Equals(item.Id, request.SpellId, StringComparison.Ordinal));
        if (spell is null)
        {
            return SpellActionResult.Failure("Spell is not available for the cached user class.");
        }

        if (!spell.IsUnlocked)
        {
            return SpellActionResult.Failure(spell.AvailabilityLabel);
        }

        if (spell.TargetKind == SpellTargetKind.Task && string.IsNullOrWhiteSpace(request.TargetTaskId))
        {
            return SpellActionResult.Failure("Choose a target task before casting this spell.");
        }

        if (State.UserSnapshot.Mana < spell.ManaCost * count)
        {
            return SpellActionResult.Failure("Not enough mana for the requested cast count.");
        }

        var originalBattleGear = NormalizePresetSlots(EquipmentSetKind.Battle, State.UserSnapshot.Equipment.Battle);
        var autoEquipSlots = request.AutoEquipRecommendedGear && request.AutoEquipGearSlots is not null
            ? NormalizePresetSlots(EquipmentSetKind.Battle, request.AutoEquipGearSlots)
            : null;
        if (autoEquipSlots is not null)
        {
            foreach (var slot in EnumerateSlots(autoEquipSlots).Where(slot => !string.IsNullOrWhiteSpace(slot.Key)))
            {
                if (!CanUseGearKey(State.UserSnapshot, EquipmentSetKind.Battle, slot.Key!))
                {
                    return SpellActionResult.Failure($"Cannot auto-equip {slot.Key} because it is not owned.");
                }
            }

            await EquipSlotsWithoutRefreshAsync(
                credentials,
                EquipmentSetKind.Battle,
                State.UserSnapshot.Equipment.Battle,
                autoEquipSlots,
                $"spell:{request.SpellId}:auto-equip",
                "Auto-equipping spell gear",
                cancellationToken);
        }

        SetState(State with
        {
            ActiveSpellCastProgress = new SpellCastProgress(request.SpellId, 0, count),
            ErrorMessage = null,
            IsBusy = true
        });

        var completed = 0;
        var requestCount = 0;
        string? partyRefreshError = null;
        try
        {
            for (var index = 0; index < count; index++)
            {
                await _habiticaSyncClient.CastSpellAsync(credentials, request.SpellId, request.TargetTaskId, cancellationToken);
                completed++;
                requestCount++;
                await DelayBetweenHabiticaRequestsAsync(cancellationToken);
                SetState(State with
                {
                    ActiveSpellCastProgress = new SpellCastProgress(request.SpellId, completed, count)
                });
            }

            if (autoEquipSlots is not null)
            {
                await EquipSlotsWithoutRefreshAsync(
                    credentials,
                    EquipmentSetKind.Battle,
                    autoEquipSlots,
                    originalBattleGear,
                    $"spell:{request.SpellId}:restore-gear",
                    "Restoring battle gear",
                    cancellationToken);
            }

            var userSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
            requestCount++;
            await DelayBetweenHabiticaRequestsAsync(cancellationToken);
            var taskSnapshot = await _habiticaSyncClient.GetTasksAsync(credentials, cancellationToken);
            requestCount++;
            await _userSnapshotStore.SaveAsync(userSnapshot, cancellationToken);
            await _taskSnapshotStore.SaveAsync(taskSnapshot, cancellationToken);

            if (spell.TargetKind == SpellTargetKind.Party)
            {
                try
                {
                    await DelayBetweenHabiticaRequestsAsync(cancellationToken);
                    requestCount++;
                    var partySnapshot = await _habiticaSyncClient.GetPartySnapshotAsync(credentials, cancellationToken);
                    await _partySnapshotStore.SaveAsync(partySnapshot, cancellationToken);
                }
                catch (Exception exception)
                {
                    partyRefreshError = exception.Message;
                    await _diagnosticsLogWriter.WriteAsync(
                        DiagnosticsFeatureArea.Sync,
                        "spell-cast-party-refresh",
                        DiagnosticsSeverity.Warning,
                        DiagnosticsMode.LiveRead,
                        exception.Message,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["spellId"] = request.SpellId,
                            ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture)
                        },
                        cancellationToken);
                }
            }

            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Skills,
                "spell-cast",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                $"Cast {request.SpellId} {completed} time(s).",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["spellId"] = request.SpellId,
                    ["targetTaskId"] = request.TargetTaskId ?? string.Empty,
                    ["completed"] = completed.ToString(CultureInfo.InvariantCulture),
                    ["requested"] = count.ToString(CultureInfo.InvariantCulture),
                    ["autoEquip"] = (autoEquipSlots is not null).ToString(CultureInfo.InvariantCulture),
                    ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            _ = TryMergeAndUploadCloudSyncAsync(credentials, cancellationToken);
            SetState(State with
            {
                ActiveSpellCastProgress = null,
                ActiveEquipmentProgress = null,
                ErrorMessage = null,
                IsBusy = false
            });

            return SpellActionResult.Success(partyRefreshError is null
                ? $"Cast {request.SpellId} {completed} time(s)."
                : $"Cast {request.SpellId} {completed} time(s). Party refresh needs retry: {partyRefreshError}");
        }
        catch (Exception exception)
        {
            if (autoEquipSlots is not null)
            {
                try
                {
                    await EquipSlotsWithoutRefreshAsync(
                        credentials,
                        EquipmentSetKind.Battle,
                        autoEquipSlots,
                        originalBattleGear,
                        $"spell:{request.SpellId}:restore-gear",
                        "Restoring battle gear",
                        cancellationToken);
                }
                catch
                {
                }
            }

            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Skills,
                "spell-cast",
                DiagnosticsSeverity.Error,
                DiagnosticsMode.LiveMutation,
                exception.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["spellId"] = request.SpellId,
                    ["targetTaskId"] = request.TargetTaskId ?? string.Empty,
                    ["completed"] = completed.ToString(CultureInfo.InvariantCulture),
                    ["requested"] = count.ToString(CultureInfo.InvariantCulture),
                    ["autoEquip"] = (autoEquipSlots is not null).ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with
            {
                ActiveSpellCastProgress = null,
                ActiveEquipmentProgress = null,
                ErrorMessage = exception.Message,
                IsBusy = false
            });

            return SpellActionResult.Failure(exception.Message);
        }
    }

    public async Task<SpellActionResult> StartNewDayAsync(CancellationToken cancellationToken = default)
    {
        return await StartNewDayAsync(new StartNewDayRequest(), cancellationToken);
    }

    public async Task<SpellActionResult> StartNewDayAsync(StartNewDayRequest request, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return SpellActionResult.Failure("Sign in is required before starting a new Habitica day.");
        }

        if (State.UserSnapshot is null || State.UserFreshness != SnapshotFreshnessState.Fresh)
        {
            return SpellActionResult.Failure("A fresh account snapshot is required before starting a new Habitica day.");
        }

        if (State.UserSnapshot.NeedsCron == false)
        {
            return SpellActionResult.Success("Habitica day is already started.");
        }

        var originalBattleGear = NormalizePresetSlots(EquipmentSetKind.Battle, State.UserSnapshot.Equipment.Battle);
        var currentBattleGear = originalBattleGear;
        var autoEquipSlots = request.AutoEquipRecommendedGear && request.AutoEquipGearSlots is not null
            ? NormalizePresetSlots(EquipmentSetKind.Battle, request.AutoEquipGearSlots)
            : null;
        if (autoEquipSlots is not null)
        {
            foreach (var slot in EnumerateSlots(autoEquipSlots).Where(slot => !string.IsNullOrWhiteSpace(slot.Key)))
            {
                if (!CanUseGearKey(State.UserSnapshot, EquipmentSetKind.Battle, slot.Key!))
                {
                    return SpellActionResult.Failure($"Start New Day skipped before CRON: Cannot equip recommended gear because {slot.Key} is not owned.");
                }
            }
        }

        var requestCount = 0;
        var gearRequestCount = 0;
        var restoreGearRequestCount = 0;
        var cronStarted = false;
        var cronCompleted = false;
        var restoreStarted = false;
        var restoreCompleted = autoEquipSlots is null;
        string? partyRefreshError = null;
        try
        {
            if (autoEquipSlots is not null)
            {
                await EquipSlotsWithoutRefreshAsync(
                    credentials,
                    EquipmentSetKind.Battle,
                    State.UserSnapshot.Equipment.Battle,
                    autoEquipSlots,
                    "cron:auto-equip",
                    $"Equipping {request.GearOptimizationGoalLabel ?? "CRON"} gear",
                    cancellationToken,
                    (slotTitle, key) =>
                    {
                        currentBattleGear = SetSlotValue(currentBattleGear, slotTitle, key);
                        gearRequestCount++;
                        requestCount++;
                    });
            }

            SetState(State with { ActiveEquipmentProgress = null, ErrorMessage = null, IsBusy = true });
            cronStarted = true;
            await _habiticaSyncClient.RunCronAsync(credentials, cancellationToken);
            cronCompleted = true;
            requestCount++;
            await DelayBetweenHabiticaRequestsAsync(cancellationToken);

            if (autoEquipSlots is not null)
            {
                restoreStarted = true;
                await EquipSlotsWithoutRefreshAsync(
                    credentials,
                    EquipmentSetKind.Battle,
                    currentBattleGear,
                    originalBattleGear,
                    "cron:restore-gear",
                    "Restoring battle gear",
                    cancellationToken,
                    (slotTitle, key) =>
                    {
                        currentBattleGear = SetSlotValue(currentBattleGear, slotTitle, key);
                        restoreGearRequestCount++;
                        requestCount++;
                    });
                restoreCompleted = true;
            }

            var userSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
            requestCount++;
            await _userSnapshotStore.SaveAsync(userSnapshot, cancellationToken);
            await DelayBetweenHabiticaRequestsAsync(cancellationToken);

            var taskSnapshot = await _habiticaSyncClient.GetTasksAsync(credentials, cancellationToken);
            requestCount++;
            await _taskSnapshotStore.SaveAsync(taskSnapshot, cancellationToken);

            if (!string.IsNullOrWhiteSpace(userSnapshot.PartyId ?? State.PartySnapshot?.PartyId))
            {
                try
                {
                    await DelayBetweenHabiticaRequestsAsync(cancellationToken);
                    var partySnapshot = await _habiticaSyncClient.GetPartySnapshotAsync(credentials, cancellationToken);
                    requestCount++;
                    await _partySnapshotStore.SaveAsync(partySnapshot, cancellationToken);
                }
                catch (Exception exception)
                {
                    partyRefreshError = exception.Message;
                    await _diagnosticsLogWriter.WriteAsync(
                        DiagnosticsFeatureArea.Sync,
                        "cron-party-refresh",
                        DiagnosticsSeverity.Warning,
                        DiagnosticsMode.LiveRead,
                        exception.Message,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture)
                        },
                        cancellationToken);
                }
            }

            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Sync,
                "cron-start-new-day",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                "Started a new Habitica day.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["previousHabiticaDayKey"] = State.UserSnapshot.CurrentHabiticaDayKey ?? string.Empty,
                    ["currentHabiticaDayKey"] = userSnapshot.CurrentHabiticaDayKey ?? string.Empty,
                    ["autoEquip"] = (autoEquipSlots is not null).ToString(CultureInfo.InvariantCulture),
                    ["gearGoal"] = request.GearOptimizationGoalLabel ?? string.Empty,
                    ["gearRequestCount"] = gearRequestCount.ToString(CultureInfo.InvariantCulture),
                    ["restoreGearRequestCount"] = restoreGearRequestCount.ToString(CultureInfo.InvariantCulture),
                    ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);

            await LoadCachedStateAsync(cancellationToken);
            _ = TryMergeAndUploadCloudSyncAsync(credentials, cancellationToken);
            _ = TryMergeAndUploadPartySyncAsync(credentials, cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });

            return SpellActionResult.Success(partyRefreshError is null
                ? autoEquipSlots is null
                    ? "Started a new Habitica day."
                    : gearRequestCount == 0
                        ? "Started a new Habitica day. Recommended gear was already equipped."
                        : "Equipped recommended gear, started a new Habitica day, and restored previous battle gear."
                : $"Started a new Habitica day. Party refresh needs retry: {partyRefreshError}");
        }
        catch (Exception exception)
        {
            string? restoreError = null;
            if (autoEquipSlots is not null && !restoreStarted)
            {
                restoreStarted = true;
                try
                {
                    await EquipSlotsWithoutRefreshAsync(
                        credentials,
                        EquipmentSetKind.Battle,
                        currentBattleGear,
                        originalBattleGear,
                        "cron:restore-gear",
                        "Restoring battle gear",
                        cancellationToken,
                        (slotTitle, key) =>
                        {
                            currentBattleGear = SetSlotValue(currentBattleGear, slotTitle, key);
                            restoreGearRequestCount++;
                            requestCount++;
                        });
                    restoreCompleted = true;
                }
                catch (Exception restoreException)
                {
                    restoreError = restoreException.Message;
                }
            }

            await LoadCachedStateAsync(cancellationToken);
            var message = GetStartNewDayFailureMessage(
                exception.Message,
                cronStarted,
                cronCompleted,
                restoreStarted,
                restoreCompleted,
                restoreError);
            SetState(State with { ActiveEquipmentProgress = null, ErrorMessage = message, IsBusy = false });
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Sync,
                "cron-start-new-day",
                DiagnosticsSeverity.Error,
                DiagnosticsMode.LiveMutation,
                message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["autoEquip"] = (autoEquipSlots is not null).ToString(CultureInfo.InvariantCulture),
                    ["gearGoal"] = request.GearOptimizationGoalLabel ?? string.Empty,
                    ["gearRequestCount"] = gearRequestCount.ToString(CultureInfo.InvariantCulture),
                    ["restoreGearRequestCount"] = restoreGearRequestCount.ToString(CultureInfo.InvariantCulture),
                    ["cronStarted"] = cronStarted.ToString(CultureInfo.InvariantCulture),
                    ["cronCompleted"] = cronCompleted.ToString(CultureInfo.InvariantCulture),
                    ["restoreStarted"] = restoreStarted.ToString(CultureInfo.InvariantCulture),
                    ["restoreCompleted"] = restoreCompleted.ToString(CultureInfo.InvariantCulture),
                    ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);
            return SpellActionResult.Failure(message);
        }
    }

    private static string GetStartNewDayFailureMessage(
        string message,
        bool cronStarted,
        bool cronCompleted,
        bool restoreStarted,
        bool restoreCompleted,
        string? restoreError)
    {
        if (cronCompleted && restoreStarted && !restoreCompleted)
        {
            return $"Start New Day completed, but restoring previous battle gear failed: {message}";
        }

        var restoreStatus = restoreCompleted && restoreStarted
            ? " Previous battle gear was restored."
            : string.IsNullOrWhiteSpace(restoreError)
                ? string.Empty
                : $" Restoring previous battle gear also failed: {restoreError}";

        if (!cronStarted)
        {
            return $"Start New Day skipped before CRON: {message}{restoreStatus}";
        }

        return cronCompleted
            ? $"Start New Day completed, but refresh failed: {message}{restoreStatus}"
            : $"Start New Day failed while CRON was running: {message}{restoreStatus}";
    }

    public async Task<TaskActionResult> ScoreTaskAsync(TaskScoreRequest request, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return TaskActionResult.Failure("Sign in is required before scoring tasks.");
        }

        if (State.TaskSnapshot is null || State.TaskFreshness != SnapshotFreshnessState.Fresh)
        {
            return TaskActionResult.Failure("Fresh task data is required before scoring tasks.");
        }

        if (State.UserSnapshot is null || State.UserFreshness != SnapshotFreshnessState.Fresh)
        {
            return TaskActionResult.Failure("Fresh account data is required before scoring tasks.");
        }

        var task = State.TaskSnapshot.Items.FirstOrDefault(item => string.Equals(item.Id, request.TaskId, StringComparison.Ordinal));
        if (task is null)
        {
            return TaskActionResult.Failure("Task is not available in the cached task snapshot.");
        }

        if (!CanScoreTask(task, request.Direction))
        {
            return TaskActionResult.Failure("This task does not support that action.");
        }

        var count = task.Type == TaskType.Habit ? Math.Clamp(request.Count, 1, 20) : 1;
        SetState(State with
        {
            ActiveTaskMutationProgress = new TaskMutationProgress(request.TaskId, request.Direction, 0, count),
            ErrorMessage = null,
            IsBusy = true
        });

        var completed = 0;
        try
        {
            for (var index = 0; index < count; index++)
            {
                await _habiticaSyncClient.ScoreTaskAsync(credentials, request.TaskId, request.Direction, cancellationToken);
                completed++;
                await DelayBetweenHabiticaRequestsAsync(cancellationToken);
                SetState(State with
                {
                    ActiveTaskMutationProgress = new TaskMutationProgress(request.TaskId, request.Direction, completed, count)
                });
            }

            var userSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
            await DelayBetweenHabiticaRequestsAsync(cancellationToken);
            var taskSnapshot = await _habiticaSyncClient.GetTasksAsync(credentials, cancellationToken);
            await _userSnapshotStore.SaveAsync(userSnapshot, cancellationToken);
            await _taskSnapshotStore.SaveAsync(taskSnapshot, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Tasks,
                "task-score",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                $"Scored task {completed} time(s).",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["taskId"] = request.TaskId,
                    ["taskType"] = task.Type.ToString(),
                    ["direction"] = request.Direction.ToString(),
                    ["completed"] = completed.ToString(CultureInfo.InvariantCulture),
                    ["requested"] = count.ToString(CultureInfo.InvariantCulture),
                    ["requestCount"] = (completed + 2).ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            _ = TryMergeAndUploadCloudSyncAsync(credentials, cancellationToken);
            SetState(State with { ActiveTaskMutationProgress = null, ErrorMessage = null, IsBusy = false });

            return TaskActionResult.Success(BuildTaskScoreSuccessMessage(task, request.Direction, completed));
        }
        catch (Exception exception)
        {
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Tasks,
                "task-score",
                DiagnosticsSeverity.Error,
                DiagnosticsMode.LiveMutation,
                exception.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["taskId"] = request.TaskId,
                    ["taskType"] = task.Type.ToString(),
                    ["direction"] = request.Direction.ToString(),
                    ["completed"] = completed.ToString(CultureInfo.InvariantCulture),
                    ["requested"] = count.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ActiveTaskMutationProgress = null, ErrorMessage = exception.Message, IsBusy = false });
            return TaskActionResult.Failure(completed > 0
                ? $"{exception.Message} Completed {completed} of {count} request(s)."
                : exception.Message);
        }
    }

    public async Task<SpellActionResult> AllocateStatsAsync(StatAllocation allocation, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return SpellActionResult.Failure("Sign in is required before allocating stats.");
        }

        if (State.UserSnapshot is null || State.UserFreshness != SnapshotFreshnessState.Fresh)
        {
            return SpellActionResult.Failure("A fresh account snapshot is required before allocating stats.");
        }

        if (!StatAllocationEligibility.IsUnlocked(State.UserSnapshot.Level))
        {
            return SpellActionResult.Failure(StatAllocationEligibility.GetLockedReason(State.UserSnapshot.Level));
        }

        var requestedPoints = allocation.Strength + allocation.Intelligence + allocation.Constitution + allocation.Perception;
        if (requestedPoints <= 0)
        {
            return SpellActionResult.Failure("Choose at least one stat point to allocate.");
        }

        if (requestedPoints > State.UserSnapshot.UnallocatedStatPoints)
        {
            return SpellActionResult.Failure("Cannot allocate more stat points than are available.");
        }

        SetState(State with { ErrorMessage = null, IsBusy = true });

        try
        {
            await _habiticaSyncClient.AllocateStatsAsync(credentials, allocation, cancellationToken);
            await DelayBetweenHabiticaRequestsAsync(cancellationToken);
            var userSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
            await _userSnapshotStore.SaveAsync(userSnapshot, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Skills,
                "stats-allocate",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                "Allocated stat points.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["str"] = allocation.Strength.ToString(CultureInfo.InvariantCulture),
                    ["int"] = allocation.Intelligence.ToString(CultureInfo.InvariantCulture),
                    ["con"] = allocation.Constitution.ToString(CultureInfo.InvariantCulture),
                    ["per"] = allocation.Perception.ToString(CultureInfo.InvariantCulture),
                    ["requestCount"] = "2"
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            _ = TryMergeAndUploadCloudSyncAsync(credentials, cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });

            return SpellActionResult.Success("Stat points allocated.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            return SpellActionResult.Failure(exception.Message);
        }
    }

    private static bool CanScoreTask(TaskSnapshot task, TaskScoreDirection direction)
    {
        return task.Type switch
        {
            TaskType.Habit => direction == TaskScoreDirection.Up
                ? task.SupportsPositiveScore != false
                : task.SupportsNegativeScore != false,
            TaskType.Daily or TaskType.Todo => direction == TaskScoreDirection.Up
                ? !task.IsCompleted
                : task.IsCompleted,
            _ => false
        };
    }

    private static string BuildTaskScoreSuccessMessage(TaskSnapshot task, TaskScoreDirection direction, int completed)
    {
        if (task.Type == TaskType.Habit)
        {
            var label = direction == TaskScoreDirection.Up ? "+" : "-";
            return $"Scored habit {label}{completed}.";
        }

        return direction == TaskScoreDirection.Up
            ? "Task completed."
            : "Task uncompleted.";
    }

    private static int GetOwnedSellItemCount(InventorySnapshot inventory, InventorySellItemType type, string key)
    {
        var items = type switch
        {
            InventorySellItemType.Egg => inventory.Eggs,
            InventorySellItemType.Food => inventory.Food,
            InventorySellItemType.HatchingPotion => inventory.HatchingPotions,
            _ => new Dictionary<string, int>(StringComparer.Ordinal)
        };

        return items.TryGetValue(key, out var count) ? count : 0;
    }

    private static string FormatSellItemType(InventorySellItemType type)
    {
        return type switch
        {
            InventorySellItemType.Egg => "egg",
            InventorySellItemType.Food => "food",
            InventorySellItemType.HatchingPotion => "hatching potion",
            _ => "inventory"
        };
    }

    public async Task<InventoryActionResult> BuyArmoireAsync(int count, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return InventoryActionResult.Failure("Sign in before opening the armoire.");
        }

        if (State.UserSnapshot is null || State.UserFreshness != SnapshotFreshnessState.Fresh)
        {
            return InventoryActionResult.Failure("Refresh your account before opening the armoire.");
        }

        var safeCount = Math.Clamp(count, 1, 50);
        var availableCount = (int)Math.Floor(State.UserSnapshot.Gold / 100m);
        if (availableCount <= 0)
        {
            return InventoryActionResult.Failure("You need at least 100 GP to open the armoire.");
        }

        if (safeCount > availableCount)
        {
            return InventoryActionResult.Failure($"You have enough gold for {availableCount} armoire opening{(availableCount == 1 ? string.Empty : "s")}.");
        }

        SetState(State with { ErrorMessage = null, IsBusy = true });

        var drops = new List<ArmoirePurchaseSnapshot>();
        try
        {
            for (var i = 0; i < safeCount; i++)
            {
                drops.Add(await _habiticaSyncClient.BuyArmoireAsync(credentials, cancellationToken));
                await DelayBetweenHabiticaRequestsAsync(cancellationToken);
            }

            var userSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
            await _userSnapshotStore.SaveAsync(userSnapshot, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "armoire-bulk-buy",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                "Opened the armoire.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["count"] = safeCount.ToString(CultureInfo.InvariantCulture),
                    ["requestCount"] = (safeCount + 1).ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            _ = TryMergeAndUploadCloudSyncAsync(credentials, cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });

            return InventoryActionResult.Success(BuildArmoireResultMessage(drops));
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            return InventoryActionResult.Failure(exception.Message);
        }
    }

    public async Task<InventoryActionResult> BuyHealthPotionAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return InventoryActionResult.Failure("Sign in before buying a health potion.");
        }

        if (State.UserSnapshot is null || State.UserFreshness != SnapshotFreshnessState.Fresh)
        {
            return InventoryActionResult.Failure("Refresh your account before buying a health potion.");
        }

        if (State.UserSnapshot.MaxHealth > 0m && State.UserSnapshot.Health >= State.UserSnapshot.MaxHealth)
        {
            return InventoryActionResult.Failure("Health is already full.");
        }

        if (State.UserSnapshot.Gold < HealthPotionGoldCost)
        {
            return InventoryActionResult.Failure("You need at least 25 GP to buy a health potion.");
        }

        var previousHealth = State.UserSnapshot.Health;
        var previousGold = State.UserSnapshot.Gold;

        SetState(State with { ErrorMessage = null, IsBusy = true });

        try
        {
            await _habiticaSyncClient.BuyHealthPotionAsync(credentials, cancellationToken);
            await DelayBetweenHabiticaRequestsAsync(cancellationToken);
            var userSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
            await _userSnapshotStore.SaveAsync(userSnapshot, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "health-potion-buy",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                "Bought a health potion.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["healthBefore"] = previousHealth.ToString(CultureInfo.InvariantCulture),
                    ["healthAfter"] = userSnapshot.Health.ToString(CultureInfo.InvariantCulture),
                    ["goldBefore"] = previousGold.ToString(CultureInfo.InvariantCulture),
                    ["goldAfter"] = userSnapshot.Gold.ToString(CultureInfo.InvariantCulture),
                    ["requestCount"] = "2"
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            _ = TryMergeAndUploadCloudSyncAsync(credentials, cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });

            return InventoryActionResult.Success("Health potion bought.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "health-potion-buy",
                DiagnosticsSeverity.Error,
                DiagnosticsMode.LiveMutation,
                exception.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["healthBefore"] = previousHealth.ToString(CultureInfo.InvariantCulture),
                    ["goldBefore"] = previousGold.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);
            return InventoryActionResult.Failure(exception.Message);
        }
    }

    public async Task<InventoryActionResult> SellInventoryItemAsync(
        InventorySellItemType type,
        string key,
        int count,
        CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return InventoryActionResult.Failure("Sign in before selling inventory items.");
        }

        if (State.UserSnapshot is null || State.UserFreshness != SnapshotFreshnessState.Fresh)
        {
            return InventoryActionResult.Failure("Refresh your account before selling inventory items.");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return InventoryActionResult.Failure("Inventory item key is required.");
        }

        var safeCount = Math.Clamp(count, 1, 99);
        var ownedCount = GetOwnedSellItemCount(State.UserSnapshot.Inventory, type, key);
        if (ownedCount <= 0)
        {
            return InventoryActionResult.Failure("This item is not available in the cached inventory snapshot.");
        }

        if (safeCount > ownedCount)
        {
            return InventoryActionResult.Failure($"Cached inventory only has {ownedCount} item(s) available.");
        }

        SetState(State with { ErrorMessage = null, IsBusy = true });

        var completed = 0;
        try
        {
            for (var index = 0; index < safeCount; index++)
            {
                await _habiticaSyncClient.SellInventoryItemAsync(credentials, type, key, cancellationToken);
                completed++;
                await DelayBetweenHabiticaRequestsAsync(cancellationToken);
            }

            var userSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
            await _userSnapshotStore.SaveAsync(userSnapshot, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-bulk-sell",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                $"Sold {completed} inventory item(s).",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemType"] = type.ToString(),
                    ["itemKey"] = key,
                    ["completed"] = completed.ToString(CultureInfo.InvariantCulture),
                    ["requested"] = safeCount.ToString(CultureInfo.InvariantCulture),
                    ["requestCount"] = (completed + 1).ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            _ = TryMergeAndUploadCloudSyncAsync(credentials, cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });
            return InventoryActionResult.Success($"Sold {completed} {FormatSellItemType(type)} item(s).");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-bulk-sell",
                DiagnosticsSeverity.Error,
                DiagnosticsMode.LiveMutation,
                exception.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemType"] = type.ToString(),
                    ["itemKey"] = key,
                    ["completed"] = completed.ToString(CultureInfo.InvariantCulture),
                    ["requested"] = safeCount.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);
            return InventoryActionResult.Failure(completed > 0
                ? $"{exception.Message} Sold {completed} of {safeCount} item(s)."
                : exception.Message);
        }
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
        await _diagnosticsLogStore.ClearAsync(cancellationToken);
        await _equipmentPresetStore.ClearAsync(cancellationToken);
        await _gearCatalogStore.ClearAsync(cancellationToken);
        await _partyCronHistoryStore.ClearAsync(cancellationToken);
        await _partySnapshotStore.ClearAsync(cancellationToken);
        await _taskSnapshotStore.ClearAsync(cancellationToken);
        await _userSnapshotStore.ClearAsync(cancellationToken);
        await _localUserDataPortabilityService.ClearSectionAsync(StorageKeys.TaskOrderPreferences, cancellationToken);

        SetState(SessionViewModel.Empty);
    }

    public async Task<LocalDataActionResult> ExportLocalDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bundle = await _localUserDataPortabilityService.ExportAsync(State.UserId ?? _currentCredentials?.UserId, cancellationToken);
            var json = _localUserDataPortabilityService.Serialize(bundle);
            return LocalDataActionResult.Success(
                bundle.Records.Count == 0
                    ? "No local app data is currently saved for export."
                    : $"Prepared export with {bundle.Records.Count} local data records.",
                json);
        }
        catch (Exception exception)
        {
            SetState(State with { ErrorMessage = exception.Message });
            return LocalDataActionResult.Failure(exception.Message);
        }
    }

    public async Task<LocalDataActionResult> PreviewImportLocalDataAsync(
        string jsonText,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bundle = _localUserDataPortabilityService.Deserialize(jsonText);
            var preview = await _localUserDataPortabilityService.PreviewImportAsync(bundle, cancellationToken);
            return LocalDataActionResult.Success(
                preview.HasLocalData
                    ? $"Import contains {preview.IncomingRecordCount} records and conflicts with {preview.ConflictingKeys.Count} local records."
                    : $"Import contains {preview.IncomingRecordCount} records.",
                jsonText,
                preview);
        }
        catch (Exception exception)
        {
            SetState(State with { ErrorMessage = exception.Message });
            return LocalDataActionResult.Failure(exception.Message);
        }
    }

    public async Task<LocalDataActionResult> ImportLocalDataAsync(
        string jsonText,
        LocalDataImportMode mode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SetState(State with { ErrorMessage = null, IsBusy = true });
            var bundle = _localUserDataPortabilityService.Deserialize(jsonText);
            var result = await _localUserDataPortabilityService.ImportAsync(bundle, mode, cancellationToken);
            await RebuildDerivedLocalStateAsync(
                State.UserId ?? _currentCredentials?.UserId ?? bundle.UserId,
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);

            var syncMessage = await TryUploadLocalDataAfterImportAsync(cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });
            return LocalDataActionResult.Success(
                string.IsNullOrWhiteSpace(syncMessage)
                    ? result.Message
                    : $"{result.Message} {syncMessage}");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            return LocalDataActionResult.Failure(exception.Message);
        }
    }

    public async Task<LocalDataActionResult> ImportCloudSyncSectionsAsync(
        string jsonText,
        IReadOnlyDictionary<string, CloudSyncSectionImportDecision> sectionDecisions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SetState(State with { ErrorMessage = null, IsBusy = true });
            var bundle = _localUserDataPortabilityService.Deserialize(jsonText);
            var imported = 0;
            foreach (var record in bundle.Records)
            {
                var section = CloudSyncSectionMapping.SectionForStorageKey(record.Key);
                var sectionKey = section is null ? record.Key : CloudSyncSectionMapping.KvSuffix(section.Value);
                var decision = sectionDecisions.GetValueOrDefault(sectionKey, CloudSyncSectionImportDecision.Merge);
                if (decision == CloudSyncSectionImportDecision.KeepLocal)
                {
                    continue;
                }

                var mode = decision == CloudSyncSectionImportDecision.UseRemote
                    ? LocalDataImportMode.Override
                    : LocalDataImportMode.Merge;
                await _localUserDataPortabilityService.ImportSectionAsync(record, mode, cancellationToken);
                imported++;
            }

            await RebuildDerivedLocalStateAsync(
                State.UserId ?? _currentCredentials?.UserId ?? bundle.UserId,
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            var syncMessage = await TryUploadLocalDataAfterImportAsync(cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });
            return LocalDataActionResult.Success(
                string.IsNullOrWhiteSpace(syncMessage)
                    ? $"Imported {imported} selected cloud sync sections."
                    : $"Imported {imported} selected cloud sync sections. {syncMessage}");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            return LocalDataActionResult.Failure(exception.Message);
        }
    }

    public Task SetCloudSyncSectionExcludedAsync(
        CloudSyncSection section,
        bool isExcluded,
        CancellationToken cancellationToken = default)
    {
        if (section is CloudSyncSection.SyncMetadata || CloudSyncSectionMapping.IsCritical(section))
        {
            return Task.CompletedTask;
        }

        if (isExcluded)
        {
            _cloudSyncExcludedSections.Add(section);
        }
        else
        {
            _cloudSyncExcludedSections.Remove(section);
        }

        SetState(State with { CloudSyncExcludedSections = _cloudSyncExcludedSections.ToArray() });
        return Task.CompletedTask;
    }

    public async Task<LocalDataActionResult> PushCloudSyncAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return LocalDataActionResult.Failure("Sign in with Habitica credentials before uploading encrypted cloud sync data.");
        }

        try
        {
            SetCloudSyncFetching(true, RefreshReason.ManualRefresh);
            var result = await MergeAndUploadCloudSyncSectionsAsync(credentials, cancellationToken);
            var partyResult = await TryMergeAndUploadPartySyncAsync(credentials, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);

            SetCloudSyncFetching(false, RefreshReason.ManualRefresh);
            var message = result.IsPartial
                ? $"Cloud sync partially completed. {result.SucceededCount} sections uploaded, {result.FailedCount} skipped."
                : $"Uploaded {result.SucceededCount} encrypted sections to Cloudflare sync.";
            if (partyResult is not null)
            {
                message += partyResult.MergedRemoteHistory
                    ? " Shared party CRON data was merged and uploaded."
                    : " Shared party CRON data was uploaded.";
            }

            return LocalDataActionResult.Success(
                message);
        }
        catch (Exception exception)
        {
            SetCloudSyncFetching(false, RefreshReason.ManualRefresh, exception.Message);
            SetState(State with { ErrorMessage = exception.Message });
            return LocalDataActionResult.Failure(exception.Message);
        }
    }

    public async Task<LocalDataActionResult> DownloadCloudSyncAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return LocalDataActionResult.Failure("Sign in with Habitica credentials before downloading encrypted cloud sync data.");
        }

        try
        {
            SetCloudSyncFetching(true, RefreshReason.ManualRefresh);

            var remoteSections = await _remoteUserDataSyncProvider.ListSectionsAsync(credentials, cancellationToken);
            if (remoteSections.Count > 0)
            {
                return await DownloadCloudSyncSectionsAsync(credentials, remoteSections, cancellationToken);
            }

            return await DownloadCloudSyncLegacyAsync(credentials, cancellationToken);
        }
        catch (Exception exception)
        {
            SetCloudSyncFetching(false, RefreshReason.ManualRefresh, exception.Message);
            SetState(State with { ErrorMessage = exception.Message });
            return LocalDataActionResult.Failure(exception.Message);
        }
    }

    private async Task<LocalDataActionResult> DownloadCloudSyncSectionsAsync(
        HabiticaCredentials credentials,
        IReadOnlyList<string> remoteSectionKeys,
        CancellationToken cancellationToken)
    {
        var dataSectionKeys = remoteSectionKeys
            .Where(static key => !string.Equals(key, CloudSyncSectionMapping.KvSuffix(CloudSyncSection.SyncMetadata), StringComparison.Ordinal))
            .ToArray();

        if (dataSectionKeys.Length == 0)
        {
            SetCloudSyncFetching(false, RefreshReason.ManualRefresh, "No cloud sync data exists.");
            return LocalDataActionResult.Failure("No cloud sync data exists for these Habitica credentials.");
        }

        var remoteSnapshots = await _remoteUserDataSyncProvider.DownloadAllSectionsAsync(
            credentials,
            dataSectionKeys,
            cancellationToken);

        var records = new List<LocalUserDataRecord>();
        var sectionResults = new List<CloudSyncSectionResult>();
        for (var i = 0; i < dataSectionKeys.Length; i++)
        {
            var snapshot = i < remoteSnapshots.Count ? remoteSnapshots[i] : null;
            if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.PlainTextJson))
            {
                var missingSection = ResolveCloudSyncSection(dataSectionKeys[i]);
                if (missingSection is not null)
                {
                    sectionResults.Add(new CloudSyncSectionResult(
                        missingSection.Value,
                        dataSectionKeys[i],
                        false,
                        "No remote data",
                        Status: CloudSyncSectionStatusKind.Skipped));
                    UpdateCloudSyncSectionStatus(
                        missingSection.Value,
                        CloudSyncDirection.Download,
                        CloudSyncSectionStatusKind.Skipped,
                        null,
                        "No remote data");
                }
                continue;
            }

            var section = CloudSyncSectionMapping.AllSections
                .FirstOrDefault(s => string.Equals(CloudSyncSectionMapping.KvSuffix(s), dataSectionKeys[i], StringComparison.Ordinal));
            var storageKey = CloudSyncSectionMapping.StorageKeyFor(section);
            if (storageKey is null)
            {
                continue;
            }

            var payloadBytes = System.Text.Encoding.UTF8.GetByteCount(snapshot.PlainTextJson);
            sectionResults.Add(new CloudSyncSectionResult(
                section,
                dataSectionKeys[i],
                true,
                PayloadBytes: payloadBytes,
                Status: CloudSyncSectionStatusKind.Succeeded));
            UpdateCloudSyncSectionStatus(
                section,
                CloudSyncDirection.Download,
                CloudSyncSectionStatusKind.Succeeded,
                payloadBytes,
                "Downloaded");
            records.Add(new LocalUserDataRecord(storageKey, snapshot.PlainTextJson));
        }

        if (records.Count == 0)
        {
            SetCloudSyncFetching(false, RefreshReason.ManualRefresh, "No cloud sync data exists.");
            return LocalDataActionResult.Failure("No cloud sync data exists for these Habitica credentials.");
        }

        var bundle = new LocalUserDataBundle(
            SchemaVersion: 1,
            ExportedAtUtc: _timeProvider.GetUtcNow(),
            UserId: credentials.UserId,
            Records: records);

        var preview = await _localUserDataPortabilityService.PreviewImportAsync(bundle, cancellationToken);
        MarkCloudSyncConflicts(preview.ConflictingKeys);
        await WriteCloudSyncSectionDiagnosticsAsync(
            CloudSyncDirection.Download,
            sectionResults,
            preview.ConflictingKeys,
            null,
            null,
            mergedRemoteData: false,
            cancellationToken);
        SetCloudSyncFetching(false, RefreshReason.ManualRefresh);

        var serialized = _localUserDataPortabilityService.Serialize(bundle);
        return LocalDataActionResult.Success(
            $"Downloaded {records.Count} cloud sync sections with {preview.IncomingRecordCount} records.",
            serialized,
            preview);
    }

    private async Task<LocalDataActionResult> DownloadCloudSyncLegacyAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var snapshot = await _remoteUserDataSyncProvider.DownloadAsync(credentials, cancellationToken);
        if (snapshot is null)
        {
            SetCloudSyncFetching(false, RefreshReason.ManualRefresh, "No cloud sync data exists.");
            return LocalDataActionResult.Failure("No cloud sync data exists for these Habitica credentials.");
        }

        var bundle = _localUserDataPortabilityService.Deserialize(snapshot.PlainTextJson);
        var preview = await _localUserDataPortabilityService.PreviewImportAsync(bundle, cancellationToken);
        MarkCloudSyncConflicts(preview.ConflictingKeys);
        await WriteCloudSyncSectionDiagnosticsAsync(
            CloudSyncDirection.Download,
            BuildSectionResultsForBundle(bundle),
            preview.ConflictingKeys,
            null,
            null,
            mergedRemoteData: false,
            cancellationToken);
        SetCloudSyncFetching(false, RefreshReason.ManualRefresh);
        return LocalDataActionResult.Success(
            snapshot.UpdatedAtUtc is null
                ? $"Downloaded cloud sync data with {preview.IncomingRecordCount} records."
                : $"Downloaded cloud sync data from {snapshot.UpdatedAtUtc.Value.LocalDateTime:g} with {preview.IncomingRecordCount} records.",
            snapshot.PlainTextJson,
            preview);
    }

    private async Task<string> TryUploadLocalDataAfterImportAsync(CancellationToken cancellationToken)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return "Sign in to upload the imported data to encrypted cloud sync.";
        }

        SetCloudSyncFetching(true, RefreshReason.ManualRefresh);
        CloudSyncUploadReport result;
        try
        {
            result = await MergeAndUploadCloudSyncSectionsAsync(credentials, cancellationToken);
        }
        finally
        {
            SetCloudSyncFetching(false, RefreshReason.ManualRefresh);
        }

        var messages = new List<string>
        {
            result.MergedRemoteData
                ? $"Imported data, merged existing cloud data, and uploaded {result.SucceededCount} sections."
                : $"Imported data was uploaded as {result.SucceededCount} encrypted sections."
        };

        var partyResult = await TryMergeAndUploadPartySyncAsync(credentials, cancellationToken);
        if (partyResult is not null)
        {
            messages.Add(partyResult.MergedRemoteHistory
                ? "Shared party CRON data was merged and uploaded."
                : "Shared party CRON data was uploaded.");
        }

        return string.Join(" ", messages);
    }

    private async Task<CloudSyncUploadReport?> TryMergeAndUploadCloudSyncAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken,
        RefreshReason reason = RefreshReason.MutationCompleted)
    {
        try
        {
            SetCloudSyncFetching(true, reason);
            var report = await MergeAndUploadCloudSyncSectionsAsync(credentials, cancellationToken);
            SetCloudSyncFetching(false, reason);
            return report;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Auth,
                "cloud-sync",
                DiagnosticsSeverity.Warning,
                DiagnosticsMode.Local,
                $"Encrypted cloud sync was skipped: {exception.Message}",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["provider"] = "cloudflare",
                    ["automatic"] = "true"
                },
                cancellationToken);
            SetCloudSyncFetching(false, reason, exception.Message);
            return null;
        }
    }

    private async Task<CloudSyncUploadReport?> TryUploadCloudSyncSectionAsync(
        HabiticaCredentials credentials,
        CloudSyncSection section,
        CancellationToken cancellationToken,
        RefreshReason reason = RefreshReason.MutationCompleted)
    {
        try
        {
            SetCloudSyncFetching(true, reason);
            var report = await UploadCloudSyncSectionsAsync(
                credentials,
                new[] { section },
                mergedRemoteData: false,
                cancellationToken);
            SetCloudSyncFetching(false, reason);
            return report;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Auth,
                "cloud-sync",
                DiagnosticsSeverity.Warning,
                DiagnosticsMode.Local,
                $"Encrypted cloud sync section was skipped: {exception.Message}",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["provider"] = "cloudflare",
                    ["automatic"] = "true",
                    ["section"] = CloudSyncSectionMapping.KvSuffix(section)
                },
                cancellationToken);
            SetCloudSyncFetching(false, reason, exception.Message);
            return null;
        }
    }

    private async Task<CloudSyncUploadReport> MergeAndUploadCloudSyncSectionsAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var mergedRemoteData = false;
        var remoteSections = await _remoteUserDataSyncProvider.ListSectionsAsync(credentials, cancellationToken);

        if (remoteSections.Count == 0)
        {
            mergedRemoteData = await TryMigrateLegacySingleBlobAsync(credentials, cancellationToken);
        }
        else
        {
            mergedRemoteData = await MergeRemoteSectionsAsync(credentials, remoteSections, cancellationToken);
        }

        return await UploadCloudSyncSectionsAsync(
            credentials,
            CloudSyncSectionMapping.AllSections,
            mergedRemoteData,
            cancellationToken);
    }

    private async Task<CloudSyncUploadReport> UploadCloudSyncSectionsAsync(
        HabiticaCredentials credentials,
        IReadOnlyList<CloudSyncSection> sections,
        bool mergedRemoteData,
        CancellationToken cancellationToken)
    {
        var sectionResults = new List<CloudSyncSectionResult>();
        foreach (var section in sections)
        {
            if (section == CloudSyncSection.SyncMetadata)
            {
                continue;
            }

            var storageKey = CloudSyncSectionMapping.StorageKeyFor(section);
            if (storageKey is null)
            {
                continue;
            }

            var kvSuffix = CloudSyncSectionMapping.KvSuffix(section);
            if (_cloudSyncExcludedSections.Contains(section))
            {
                sectionResults.Add(new CloudSyncSectionResult(
                    section,
                    kvSuffix,
                    false,
                    "Excluded by sync settings",
                    Status: CloudSyncSectionStatusKind.Excluded));
                UpdateCloudSyncSectionStatus(
                    section,
                    CloudSyncDirection.Upload,
                    CloudSyncSectionStatusKind.Excluded,
                    null,
                    "Excluded by sync settings");
                continue;
            }

            var record = await _localUserDataPortabilityService.ExportSectionAsync(storageKey, cancellationToken);
            if (record is null)
            {
                sectionResults.Add(new CloudSyncSectionResult(
                    section,
                    kvSuffix,
                    false,
                    "No local data",
                    Status: CloudSyncSectionStatusKind.Skipped));
                UpdateCloudSyncSectionStatus(
                    section,
                    CloudSyncDirection.Upload,
                    CloudSyncSectionStatusKind.Skipped,
                    null,
                    "No local data");
                continue;
            }

            var payloadBytes = System.Text.Encoding.UTF8.GetByteCount(record.JsonText);
            if (payloadBytes > CloudSyncSectionMapping.MaxSectionPayloadBytes)
            {
                await _diagnosticsLogWriter.WriteAsync(
                    DiagnosticsFeatureArea.Auth,
                    "cloud-sync",
                    DiagnosticsSeverity.Warning,
                    DiagnosticsMode.Local,
                    $"Cloud sync section {kvSuffix} skipped: payload {payloadBytes} bytes exceeds limit.",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["section"] = kvSuffix,
                        ["payloadBytes"] = payloadBytes.ToString(CultureInfo.InvariantCulture),
                        ["maxBytes"] = CloudSyncSectionMapping.MaxSectionPayloadBytes.ToString(CultureInfo.InvariantCulture)
                    },
                    cancellationToken);
                sectionResults.Add(new CloudSyncSectionResult(
                    section,
                    kvSuffix,
                    false,
                    "Payload too large",
                    payloadBytes,
                    CloudSyncSectionStatusKind.Skipped));
                UpdateCloudSyncSectionStatus(
                    section,
                    CloudSyncDirection.Upload,
                    CloudSyncSectionStatusKind.Skipped,
                    payloadBytes,
                    "Payload too large");
                continue;
            }

            var uploadResult = await _remoteUserDataSyncProvider.UploadSectionAsync(
                credentials,
                kvSuffix,
                record.JsonText,
                cancellationToken);

            sectionResults.Add(new CloudSyncSectionResult(
                section,
                kvSuffix,
                uploadResult.Succeeded,
                uploadResult.ErrorMessage,
                payloadBytes,
                uploadResult.Succeeded ? CloudSyncSectionStatusKind.Succeeded : CloudSyncSectionStatusKind.Failed));
            UpdateCloudSyncSectionStatus(
                section,
                CloudSyncDirection.Upload,
                uploadResult.Succeeded ? CloudSyncSectionStatusKind.Succeeded : CloudSyncSectionStatusKind.Failed,
                payloadBytes,
                uploadResult.Succeeded ? "Uploaded" : uploadResult.ErrorMessage);
        }

        var succeededSections = sectionResults.Where(static r => r.Status == CloudSyncSectionStatusKind.Succeeded).Select(static r => r.SectionKey).ToArray();
        var failedSections = sectionResults.Where(static r => r.Status == CloudSyncSectionStatusKind.Failed).Select(static r => r.SectionKey).ToArray();
        var metadata = new CloudSyncMetadata(2, _timeProvider.GetUtcNow(), succeededSections, failedSections);
        var metadataJson = JsonSerializer.Serialize(metadata, JsonOptions);
        var metadataUpload = await _remoteUserDataSyncProvider.UploadSectionAsync(
            credentials,
            CloudSyncSectionMapping.KvSuffix(CloudSyncSection.SyncMetadata),
            metadataJson,
            cancellationToken);
        UpdateCloudSyncSectionStatus(
            CloudSyncSection.SyncMetadata,
            CloudSyncDirection.Metadata,
            metadataUpload.Succeeded ? CloudSyncSectionStatusKind.Succeeded : CloudSyncSectionStatusKind.Failed,
            System.Text.Encoding.UTF8.GetByteCount(metadataJson),
            metadataUpload.Succeeded ? "Metadata uploaded" : metadataUpload.ErrorMessage);

        var succeeded = sectionResults.Count(static r => r.Status == CloudSyncSectionStatusKind.Succeeded);
        var failed = sectionResults.Count(static r => r.Status == CloudSyncSectionStatusKind.Failed);

        await WriteCloudSyncSectionDiagnosticsAsync(
            CloudSyncDirection.Upload,
            sectionResults,
            Array.Empty<string>(),
            metadataUpload.Succeeded ? CloudSyncSectionStatusKind.Succeeded : CloudSyncSectionStatusKind.Failed,
            System.Text.Encoding.UTF8.GetByteCount(metadataJson),
            mergedRemoteData,
            cancellationToken);

        return new CloudSyncUploadReport(sectionResults, mergedRemoteData, succeeded, failed);
    }

    private Task WriteCloudSyncSectionDiagnosticsAsync(
        CloudSyncDirection direction,
        IReadOnlyList<CloudSyncSectionResult> sectionResults,
        IReadOnlyList<string> conflictingStorageKeys,
        CloudSyncSectionStatusKind? metadataStatus,
        int? metadataPayloadBytes,
        bool mergedRemoteData,
        CancellationToken cancellationToken)
    {
        var failedSections = sectionResults
            .Where(static result => result.Status == CloudSyncSectionStatusKind.Failed)
            .Select(static result => result.SectionKey)
            .ToArray();
        var skippedSections = sectionResults
            .Where(static result => result.Status == CloudSyncSectionStatusKind.Skipped)
            .Select(static result => result.SectionKey)
            .ToArray();
        var excludedSections = sectionResults
            .Where(static result => result.Status == CloudSyncSectionStatusKind.Excluded)
            .Select(static result => result.SectionKey)
            .ToArray();
        var succeededSections = sectionResults
            .Where(static result => result.Status == CloudSyncSectionStatusKind.Succeeded)
            .Select(static result => result.SectionKey)
            .ToArray();
        var conflictSections = new List<string>();
        foreach (var storageKey in conflictingStorageKeys)
        {
            var section = CloudSyncSectionMapping.SectionForStorageKey(storageKey);
            if (section is not null)
            {
                conflictSections.Add(CloudSyncSectionMapping.KvSuffix(section.Value));
            }
        }
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["provider"] = "cloudflare",
            ["direction"] = direction.ToString(),
            ["mergedRemoteData"] = mergedRemoteData.ToString(CultureInfo.InvariantCulture),
            ["sectionStatuses"] = string.Join(", ", sectionResults.Select(static result => $"{result.SectionKey}:{result.Status}")),
            ["sectionPayloadBytes"] = string.Join(", ", sectionResults
                .Where(static result => result.PayloadBytes is not null)
                .Select(static result => $"{result.SectionKey}:{result.PayloadBytes!.Value.ToString(CultureInfo.InvariantCulture)}")),
            ["succeededSections"] = string.Join(", ", succeededSections),
            ["failedSections"] = string.Join(", ", failedSections),
            ["skippedSections"] = string.Join(", ", skippedSections),
            ["excludedSections"] = string.Join(", ", excludedSections),
            ["conflictSections"] = string.Join(", ", conflictSections)
        };

        if (metadataStatus is not null)
        {
            metadata["metadataStatus"] = metadataStatus.Value.ToString();
        }

        if (metadataPayloadBytes is not null)
        {
            metadata["metadataPayloadBytes"] = metadataPayloadBytes.Value.ToString(CultureInfo.InvariantCulture);
        }

        var severity = sectionResults.Any(static result => result.Status == CloudSyncSectionStatusKind.Failed && CloudSyncSectionMapping.IsCritical(result.Section))
            || metadataStatus == CloudSyncSectionStatusKind.Failed
            ? DiagnosticsSeverity.Error
            : failedSections.Length > 0 || skippedSections.Length > 0 || excludedSections.Length > 0 || conflictSections.Count > 0
                ? DiagnosticsSeverity.Warning
                : DiagnosticsSeverity.Success;
        var statusText = direction == CloudSyncDirection.Download
            ? $"{succeededSections.Length} downloaded, {conflictSections.Count} conflict(s)"
            : $"{succeededSections.Length} uploaded, {failedSections.Length} failed, {skippedSections.Length + excludedSections.Length} skipped";

        return _diagnosticsLogWriter.WriteAsync(
            DiagnosticsFeatureArea.Sync,
            $"cloud-sync-{direction.ToString().ToLowerInvariant()}",
            severity,
            DiagnosticsMode.Local,
            $"Cloud sync {direction.ToString().ToLowerInvariant()} status: {statusText}.",
            metadata,
            cancellationToken);
    }

    private static IReadOnlyList<CloudSyncSectionResult> BuildSectionResultsForBundle(LocalUserDataBundle bundle)
    {
        var results = new List<CloudSyncSectionResult>();
        foreach (var record in bundle.Records)
        {
            var section = CloudSyncSectionMapping.SectionForStorageKey(record.Key);
            if (section is null)
            {
                continue;
            }

            results.Add(new CloudSyncSectionResult(
                section.Value,
                CloudSyncSectionMapping.KvSuffix(section.Value),
                true,
                PayloadBytes: System.Text.Encoding.UTF8.GetByteCount(record.JsonText),
                Status: CloudSyncSectionStatusKind.Succeeded));
        }

        return results;
    }

    private void SetCloudSyncFetching(bool isFetching, RefreshReason reason, string? error = null)
    {
        var domainStates = State.DomainStates is null
            ? new Dictionary<RefreshDomain, DomainRefreshState>()
            : new Dictionary<RefreshDomain, DomainRefreshState>(State.DomainStates);
        domainStates[RefreshDomain.CloudSync] = new DomainRefreshState(
            RefreshDomain.CloudSync,
            isFetching,
            isFetching ? domainStates.GetValueOrDefault(RefreshDomain.CloudSync)?.LastRefreshedAtUtc : _timeProvider.GetUtcNow(),
            error,
            reason,
            RefreshPriority.Background);
        SetState(State with { DomainStates = domainStates });
    }

    private void UpdateCloudSyncSectionStatus(
        CloudSyncSection section,
        CloudSyncDirection direction,
        CloudSyncSectionStatusKind status,
        int? payloadBytes,
        string? message)
    {
        var sectionKey = CloudSyncSectionMapping.KvSuffix(section);
        var statuses = (State.CloudSyncSectionStatuses ?? Array.Empty<CloudSyncSectionStatus>())
            .Where(item => !string.Equals(item.SectionKey, sectionKey, StringComparison.Ordinal))
            .ToList();
        statuses.Add(new CloudSyncSectionStatus(
            section,
            sectionKey,
            direction,
            status,
            _timeProvider.GetUtcNow(),
            payloadBytes,
            message));
        SetState(State with
        {
            CloudSyncSectionStatuses = OrderCloudSyncStatuses(statuses),
            CloudSyncExcludedSections = _cloudSyncExcludedSections.ToArray()
        });
    }

    private void MarkCloudSyncConflicts(IReadOnlyList<string> conflictingStorageKeys)
    {
        foreach (var storageKey in conflictingStorageKeys)
        {
            var section = CloudSyncSectionMapping.SectionForStorageKey(storageKey);
            if (section is null)
            {
                continue;
            }

            UpdateCloudSyncSectionStatus(
                section.Value,
                CloudSyncDirection.Download,
                CloudSyncSectionStatusKind.Conflict,
                null,
                "Local and remote data both exist");
        }
    }

    private static IReadOnlyList<CloudSyncSectionStatus> OrderCloudSyncStatuses(IReadOnlyList<CloudSyncSectionStatus> statuses)
    {
        return statuses
            .OrderBy(item => GetCloudSyncSectionOrder(item.Section))
            .ThenBy(item => item.SectionKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static int GetCloudSyncSectionOrder(CloudSyncSection section)
    {
        for (var i = 0; i < CloudSyncSectionMapping.AllSections.Count; i++)
        {
            if (CloudSyncSectionMapping.AllSections[i] == section)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static CloudSyncSection? ResolveCloudSyncSection(string sectionKey)
    {
        foreach (var section in CloudSyncSectionMapping.AllSections)
        {
            if (string.Equals(CloudSyncSectionMapping.KvSuffix(section), sectionKey, StringComparison.Ordinal))
            {
                return section;
            }
        }

        return null;
    }

    private static HashSet<CloudSyncSection> ParseCloudSyncExcludedSections(IReadOnlyList<string>? sectionKeys)
    {
        var excluded = new HashSet<CloudSyncSection>();
        if (sectionKeys is null)
        {
            return excluded;
        }

        foreach (var sectionKey in sectionKeys)
        {
            var section = CloudSyncSectionMapping.AllSections.FirstOrDefault(candidate =>
                string.Equals(candidate.ToString(), sectionKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(CloudSyncSectionMapping.KvSuffix(candidate), sectionKey, StringComparison.OrdinalIgnoreCase));
            if (section == default || section == CloudSyncSection.SyncMetadata || CloudSyncSectionMapping.IsCritical(section))
            {
                continue;
            }

            excluded.Add(section);
        }

        return excluded;
    }

    private async Task<bool> TryMigrateLegacySingleBlobAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var legacySnapshot = await _remoteUserDataSyncProvider.DownloadAsync(credentials, cancellationToken);
        if (legacySnapshot is null || string.IsNullOrWhiteSpace(legacySnapshot.PlainTextJson))
        {
            return false;
        }

        var remoteBundle = _localUserDataPortabilityService.Deserialize(legacySnapshot.PlainTextJson);
        await _localUserDataPortabilityService.ImportAsync(remoteBundle, LocalDataImportMode.Merge, cancellationToken);
        await RebuildDerivedLocalStateAsync(credentials.UserId, cancellationToken);
        return true;
    }

    private async Task<bool> MergeRemoteSectionsAsync(
        HabiticaCredentials credentials,
        IReadOnlyList<string> remoteSectionKeys,
        CancellationToken cancellationToken)
    {
        var dataSectionKeys = remoteSectionKeys
            .Where(static key => !string.Equals(key, CloudSyncSectionMapping.KvSuffix(CloudSyncSection.SyncMetadata), StringComparison.Ordinal))
            .ToArray();

        if (dataSectionKeys.Length == 0)
        {
            return false;
        }

        var remoteSnapshots = await _remoteUserDataSyncProvider.DownloadAllSectionsAsync(
            credentials,
            dataSectionKeys,
            cancellationToken);

        var merged = false;
        for (var i = 0; i < dataSectionKeys.Length; i++)
        {
            var snapshot = i < remoteSnapshots.Count ? remoteSnapshots[i] : null;
            if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.PlainTextJson))
            {
                continue;
            }

            var section = CloudSyncSectionMapping.AllSections
                .FirstOrDefault(s => string.Equals(CloudSyncSectionMapping.KvSuffix(s), dataSectionKeys[i], StringComparison.Ordinal));
            var storageKey = CloudSyncSectionMapping.StorageKeyFor(section);
            if (storageKey is null)
            {
                continue;
            }

            await _localUserDataPortabilityService.ImportSectionAsync(
                new LocalUserDataRecord(storageKey, snapshot.PlainTextJson),
                LocalDataImportMode.Merge,
                cancellationToken);
            merged = true;
        }

        if (merged)
        {
            await RebuildDerivedLocalStateAsync(credentials.UserId, cancellationToken);
        }

        return merged;
    }

    private async Task<PartySyncUploadResult?> TryMergeAndUploadPartySyncAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (!_featureOptions.PartySyncEnabled)
        {
            return null;
        }

        try
        {
            return await MergeAndUploadPartySyncAsync(credentials, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Auth,
                "party-sync",
                DiagnosticsSeverity.Warning,
                DiagnosticsMode.Local,
                $"Shared party sync was skipped: {exception.Message}",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["provider"] = "cloudflare",
                    ["automatic"] = "true"
                },
                cancellationToken);
            return null;
        }
    }

    private async Task<PartyQuestActionResult> RunPartySyncManagementActionAsync(
        Func<PartySyncClaim, Task<RemotePartyQuestState>> action,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (!_featureOptions.PartySyncEnabled)
        {
            return PartyQuestActionResult.Failure("Shared party sync is disabled.");
        }

        var claim = await ResolvePartySyncClaimAsync(cancellationToken);
        if (claim is null)
        {
            return PartyQuestActionResult.Failure("Sign in with an active party before changing party sync management.");
        }

        try
        {
            var state = await action(claim);
            ApplyPartyQuestState(claim.PartyId, state);
            return PartyQuestActionResult.Success(successMessage);
        }
        catch (Exception exception)
        {
            SetState(State with { ErrorMessage = exception.Message });
            return PartyQuestActionResult.Failure(exception.Message);
        }
    }

    private async Task<PartyQuestActionResult> RunPartyQueueActionAsync(
        Func<PartySyncClaim, Task<RemotePartyQuestState>> action,
        string successMessage,
        string missingClaimMessage,
        CancellationToken cancellationToken)
    {
        if (!_featureOptions.PartySyncEnabled)
        {
            return PartyQuestActionResult.Failure("Shared party sync is disabled.");
        }

        var claim = await ResolvePartySyncClaimAsync(cancellationToken);
        if (claim is null)
        {
            return PartyQuestActionResult.Failure(missingClaimMessage);
        }

        try
        {
            var state = await action(claim);
            ApplyPartyQuestState(claim.PartyId, state);
            return PartyQuestActionResult.Success(successMessage);
        }
        catch (Exception exception)
        {
            SetState(State with { ErrorMessage = exception.Message });
            return PartyQuestActionResult.Failure(exception.Message);
        }
    }

    private async Task<PartyQuestActionResult> RespondToPartyQuestInvitationAsync(
        bool accept,
        string operation,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return PartyQuestActionResult.Failure("Sign in before responding to quest invitations.");
        }

        var quest = State.PartySnapshot?.Quest;
        if (quest is null || quest.IsActive)
        {
            return PartyQuestActionResult.Failure("No pending quest invitation is available.");
        }

        var currentStatus = GetCurrentUserQuestParticipationStatus(credentials.UserId);
        if (currentStatus != PartyQuestParticipationStatus.Pending)
        {
            return PartyQuestActionResult.Failure("This account has already responded to the quest invitation.");
        }

        SetState(State with { ErrorMessage = null, IsBusy = true });

        try
        {
            if (accept)
            {
                await _habiticaSyncClient.AcceptPartyQuestAsync(credentials, cancellationToken);
            }
            else
            {
                await _habiticaSyncClient.RejectPartyQuestAsync(credentials, cancellationToken);
            }

            var partySnapshot = await _habiticaSyncClient.GetPartySnapshotAsync(credentials, cancellationToken);
            await _partySnapshotStore.SaveAsync(partySnapshot, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Party,
                operation,
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                successMessage,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["questKey"] = quest.Key ?? string.Empty,
                    ["requestCount"] = "2"
                },
                cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });
            return PartyQuestActionResult.Success(successMessage);
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            return PartyQuestActionResult.Failure(exception.Message);
        }
    }

    private PartyQuestParticipationStatus GetCurrentUserQuestParticipationStatus(string userId)
    {
        return State.PartySnapshot?.Members.FirstOrDefault(member =>
            string.Equals(member.MemberId, userId, StringComparison.Ordinal))?.ParticipationStatus
            ?? PartyQuestParticipationStatus.Unknown;
    }

    private async Task<PartySyncClaim?> ResolvePartySyncClaimAsync(CancellationToken cancellationToken)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        var partyId = State.PartySnapshot?.PartyId ?? State.UserSnapshot?.PartyId;
        if (credentials is null || string.IsNullOrWhiteSpace(partyId))
        {
            return null;
        }

        return BuildPartySyncClaim(credentials, partyId, State.PartySnapshot);
    }

    private PartySyncClaim BuildPartySyncClaim(
        HabiticaCredentials credentials,
        string partyId,
        PartySnapshot? partySnapshot)
    {
        var displayName = State.UserSnapshot?.DisplayName ?? State.DisplayName ?? credentials.UserId;
        return new PartySyncClaim(
            partyId,
            credentials.UserId,
            displayName,
            partySnapshot?.LeaderId);
    }

    private async Task<PartySyncUploadResult?> MergeAndUploadPartySyncAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        await _partySyncSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await MergeAndUploadPartySyncCoreAsync(credentials, cancellationToken);
        }
        finally
        {
            _partySyncSemaphore.Release();
        }
    }

    private async Task<PartySyncUploadResult?> MergeAndUploadPartySyncCoreAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var partySnapshot = await _partySnapshotStore.GetLatestAsync(cancellationToken);
        if (partySnapshot is null || string.IsNullOrWhiteSpace(partySnapshot.PartyId))
        {
            return null;
        }

        var claim = BuildPartySyncClaim(credentials, partySnapshot.PartyId, partySnapshot);
        var mergedRemoteHistory = false;
        var remoteSnapshot = await _remotePartyDataSyncProvider.DownloadAsync(claim, cancellationToken);
        var previousPartySnapshot = TryDeserializePartySnapshot(remoteSnapshot?.PartySnapshotJson);
        if (remoteSnapshot is not null && !string.IsNullOrWhiteSpace(remoteSnapshot.CronHistoryJson))
        {
            ApplyPartyQuestState(partySnapshot.PartyId, remoteSnapshot);
            var remoteHistory = JsonSerializer.Deserialize<PartyCronHistorySnapshot>(remoteSnapshot.CronHistoryJson, JsonOptions);
            if (remoteHistory is not null && remoteHistory.Events.Count > 0)
            {
                await _partyCronHistoryStore.UpsertAsync(remoteHistory.Events, partySnapshot.RetrievedAtUtc, cancellationToken);
                await RebuildDerivedLocalStateAsync(credentials.UserId, cancellationToken);
                partySnapshot = await _partySnapshotStore.GetLatestAsync(cancellationToken) ?? partySnapshot;
                mergedRemoteHistory = true;
            }
        }

        var cronHistory = await _partyCronHistoryStore.GetAsync(cancellationToken);
        await _remotePartyDataSyncProvider.UploadAsync(
            claim,
            JsonSerializer.Serialize(partySnapshot, JsonOptions),
            JsonSerializer.Serialize(cronHistory, JsonOptions),
            cancellationToken);
        var questState = await PublishCurrentQuestPoolAsync(claim, cancellationToken);
        if (questState is not null)
        {
            ApplyPartyQuestState(partySnapshot.PartyId, questState);
        }
        await ReconcileQuestLifecycleAsync(claim, previousPartySnapshot, partySnapshot, cancellationToken);

        return new PartySyncUploadResult(mergedRemoteHistory, cronHistory.Events.Count);
    }

    private async Task ReconcileQuestLifecycleAsync(
        PartySyncClaim claim,
        PartySnapshot? previousPartySnapshot,
        PartySnapshot currentPartySnapshot,
        CancellationToken cancellationToken)
    {
        var queue = State.PartyQuestQueue?.Queue;
        var completionDetection = DetectCompletedQuest(previousPartySnapshot, currentPartySnapshot);
        if ((queue is null || queue.Count == 0) && completionDetection is null)
        {
            return;
        }

        var habiticaQuest = currentPartySnapshot.Quest;
        var habiticaQuestKey = habiticaQuest is { IsActive: true } ? habiticaQuest.Key : null;
        var completedQueuedQuest = false;
        var matchedQueuedQuest = false;

        foreach (var entry in queue ?? Array.Empty<PartyQuestQueueEntry>())
        {
            if (entry.Status is PartyQuestQueueStatus.Active
                && completionDetection is not null
                && string.Equals(entry.QuestKey, completionDetection.Quest.Key, StringComparison.Ordinal)
                && !string.Equals(entry.QuestKey, habiticaQuestKey, StringComparison.Ordinal))
            {
                matchedQueuedQuest = true;
                try
                {
                    var state = await _remotePartyDataSyncProvider.ReconcileQuestLifecycleAsync(
                        claim, entry.QueueItemId, entry.QuestKey, "complete",
                        completionDetection.Quest.ParticipantCount, State.DisplayName, completionDetection.DetectionKey, cancellationToken);
                    ApplyPartyQuestState(claim.PartyId, state);
                    completedQueuedQuest = true;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await _diagnosticsLogWriter.WriteAsync(
                        DiagnosticsFeatureArea.Party,
                        "quest-lifecycle-complete",
                        DiagnosticsSeverity.Warning,
                        DiagnosticsMode.Local,
                        $"Auto-complete for '{entry.QuestName}' failed: {exception.Message}",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["queueItemId"] = entry.QueueItemId,
                            ["questKey"] = entry.QuestKey
                        },
                        cancellationToken);
                }
            }
            else if (entry.Status is PartyQuestQueueStatus.Queued or PartyQuestQueueStatus.Selected or PartyQuestQueueStatus.InviteSent
                     && string.Equals(entry.QuestKey, habiticaQuestKey, StringComparison.Ordinal))
            {
                try
                {
                    var state = await _remotePartyDataSyncProvider.ReconcileQuestLifecycleAsync(
                        claim, entry.QueueItemId, entry.QuestKey, "activate",
                        null, null, null, cancellationToken);
                    ApplyPartyQuestState(claim.PartyId, state);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await _diagnosticsLogWriter.WriteAsync(
                        DiagnosticsFeatureArea.Party,
                        "quest-lifecycle-activate",
                        DiagnosticsSeverity.Warning,
                        DiagnosticsMode.Local,
                        $"Auto-activate for '{entry.QuestName}' failed: {exception.Message}",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["queueItemId"] = entry.QueueItemId,
                            ["questKey"] = entry.QuestKey
                        },
                        cancellationToken);
                }
            }
        }

        if (completionDetection is not null && !completedQueuedQuest && !matchedQueuedQuest)
        {
            try
            {
                var state = await _remotePartyDataSyncProvider.RecordDetectedQuestCompletionAsync(
                    claim,
                    new PartyDetectedQuestCompletion(
                        completionDetection.Quest.Key!,
                        GetQuestDisplayName(completionDetection.Quest),
                        previousPartySnapshot?.RetrievedAtUtc,
                        completionDetection.Quest.ParticipantCount,
                        completionDetection.Quest.Rewards,
                        completionDetection.DetectionKey,
                        completionDetection.CompletedAtUtc),
                    cancellationToken);
                ApplyPartyQuestState(claim.PartyId, state);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await _diagnosticsLogWriter.WriteAsync(
                    DiagnosticsFeatureArea.Party,
                    "quest-lifecycle-detect-complete",
                    DiagnosticsSeverity.Warning,
                    DiagnosticsMode.Local,
                    $"Detected completion for '{GetQuestDisplayName(completionDetection.Quest)}' could not be recorded: {exception.Message}",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["questKey"] = completionDetection.Quest.Key ?? string.Empty,
                        ["detectionKey"] = completionDetection.DetectionKey
                    },
                    cancellationToken);
            }
        }
    }

    private static PartySnapshot? TryDeserializePartySnapshot(string? jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PartySnapshot>(jsonText, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static QuestCompletionDetection? DetectCompletedQuest(
        PartySnapshot? previousPartySnapshot,
        PartySnapshot currentPartySnapshot)
    {
        var previousQuest = previousPartySnapshot?.Quest;
        if (previousQuest is not { IsActive: true } || string.IsNullOrWhiteSpace(previousQuest.Key))
        {
            return null;
        }

        if (currentPartySnapshot.Quest is { IsActive: true } currentQuest
            && string.Equals(currentQuest.Key, previousQuest.Key, StringComparison.Ordinal))
        {
            return null;
        }

        var oldestReliableSignalUtc = previousPartySnapshot!.RetrievedAtUtc.AddMinutes(-5);
        foreach (var chatMessage in currentPartySnapshot.RecentChatMessages.OrderByDescending(message => message.SentAtUtc))
        {
            if (chatMessage.SentAtUtc is not { } sentAtUtc || sentAtUtc < oldestReliableSignalUtc)
            {
                continue;
            }

            var type = chatMessage.Info?.Type;
            if (string.Equals(type, "boss_defeated", StringComparison.Ordinal)
                && string.Equals(chatMessage.Info?.QuestKey, previousQuest.Key, StringComparison.Ordinal))
            {
                return new QuestCompletionDetection(
                    previousQuest,
                    BuildDetectionKey("habitica-chat-boss", previousQuest.Key!, chatMessage),
                    sentAtUtc);
            }

            if (string.Equals(type, "all_items_found", StringComparison.Ordinal)
                && previousQuest.QuestType == PartyQuestType.Collection)
            {
                return new QuestCompletionDetection(
                    previousQuest,
                    BuildDetectionKey("habitica-chat-collection", previousQuest.Key!, chatMessage),
                    sentAtUtc);
            }
        }

        return null;
    }

    private static string BuildDetectionKey(string source, string questKey, PartyChatMessageSnapshot chatMessage)
    {
        var signalId = !string.IsNullOrWhiteSpace(chatMessage.MessageId)
            ? chatMessage.MessageId
            : chatMessage.SentAtUtc?.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        return $"{source}:{questKey}:{signalId}";
    }

    private static string GetQuestDisplayName(PartyQuestSnapshot quest)
    {
        return string.IsNullOrWhiteSpace(quest.Name)
            ? quest.Key ?? "Unknown quest"
            : quest.Name;
    }

    private sealed record QuestCompletionDetection(
        PartyQuestSnapshot Quest,
        string DetectionKey,
        DateTimeOffset CompletedAtUtc);

    private async Task<RemotePartyQuestState?> PublishCurrentQuestPoolAsync(
        PartySyncClaim claim,
        CancellationToken cancellationToken)
    {
        var entries = BuildCurrentUserQuestPoolEntries(claim.PartyId, claim.UserId);
        if (entries.Count == 0)
        {
            var remoteSnapshot = await _remotePartyDataSyncProvider.DownloadAsync(claim, cancellationToken);
            ApplyPartyQuestState(claim.PartyId, remoteSnapshot);
            return null;
        }

        return await _remotePartyDataSyncProvider.PublishQuestPoolAsync(claim, entries, cancellationToken);
    }

    private IReadOnlyList<PartyQuestPoolEntry> BuildCurrentUserQuestPoolEntries(string partyId, string userId)
    {
        var snapshot = State.UserSnapshot;
        if (snapshot is null)
        {
            return Array.Empty<PartyQuestPoolEntry>();
        }

        var now = _timeProvider.GetUtcNow();
        var entries = new List<PartyQuestPoolEntry>();
        foreach (var quest in snapshot.Inventory.QuestScrolls.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            QuestCatalogItem? metadata = null;
            State.GearCatalogSnapshot?.QuestItems.TryGetValue(quest.Key, out metadata);
            entries.Add(new PartyQuestPoolEntry(
                partyId,
                quest.Key,
                metadata?.Text ?? quest.Key,
                userId,
                snapshot.DisplayName,
                quest.Value,
                now,
                metadata?.QuestType ?? "Quest",
                metadata?.RewardSummary ?? Array.Empty<string>()));
        }

        return entries;
    }

    private async Task<PartyQuestMutationValidation> ValidatePartyQuestMutationAsync(
        string questKey,
        CancellationToken cancellationToken)
    {
        var claim = await ResolvePartySyncClaimAsync(cancellationToken);
        if (claim is null)
        {
            return PartyQuestMutationValidation.Fail("Sign in with an active party before queueing quests.");
        }

        var entry = BuildCurrentUserQuestPoolEntries(claim.PartyId, claim.UserId)
            .FirstOrDefault(candidate => string.Equals(candidate.QuestKey, questKey, StringComparison.Ordinal));
        if (entry is null)
        {
            return PartyQuestMutationValidation.Fail("Only your own available quest scrolls can be added to the queue.");
        }

        return new PartyQuestMutationValidation(claim, entry, null);
    }

    private void ApplyPartyQuestState(string partyId, RemotePartyDataSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            SetState(State with
            {
                PartyQuestQueue = new PartyQuestQueueSnapshot(
                    null,
                    Array.Empty<PartyQuestPoolEntry>(),
                    Array.Empty<PartyQuestQueueEntry>(),
                    Array.Empty<PartyRecentlyCompletedQuest>(),
                    State.PartyQuestQueue?.Management)
            });
            return;
        }

        SetState(State with
        {
            PartyQuestQueue = new PartyQuestQueueSnapshot(
                snapshot.UpdatedAtUtc,
                snapshot.QuestPool ?? Array.Empty<PartyQuestPoolEntry>(),
                snapshot.QuestQueue ?? Array.Empty<PartyQuestQueueEntry>(),
                snapshot.RecentlyCompleted ?? Array.Empty<PartyRecentlyCompletedQuest>(),
                snapshot.Management ?? State.PartyQuestQueue?.Management)
        });
    }

    private void ApplyPartyQuestState(string partyId, RemotePartyQuestState state)
    {
        SetState(State with
        {
            PartyQuestQueue = new PartyQuestQueueSnapshot(
                state.UpdatedAtUtc,
                state.QuestPool ?? Array.Empty<PartyQuestPoolEntry>(),
                state.QuestQueue ?? Array.Empty<PartyQuestQueueEntry>(),
                state.RecentlyCompleted ?? Array.Empty<PartyRecentlyCompletedQuest>(),
                state.Management ?? State.PartyQuestQueue?.Management)
        });
    }

    private async Task RebuildDerivedLocalStateAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var partySnapshot = await _partySnapshotStore.GetLatestAsync(cancellationToken);
        if (partySnapshot is null || partySnapshot.Members.Count == 0)
        {
            return;
        }

        var cronHistory = await _partyCronHistoryStore.GetAsync(cancellationToken);
        var cronDashboard = PartyCronCalculator.BuildDashboard(
            partySnapshot,
            cronHistory,
            userId,
            partySnapshot.RetrievedAtUtc,
            TimeZoneInfo.Local);
        var enrichedParty = partySnapshot with
        {
            Members = cronDashboard.Members,
            CronDashboard = cronDashboard
        };
        var enrichedQuest = enrichedParty.Quest is null
            ? null
            : PartyQuestProgressCalculator.Enrich(
                enrichedParty,
                enrichedParty.Quest,
                userId,
                partySnapshot.RetrievedAtUtc,
                TimeZoneInfo.Local,
                _includeStalePartyMembersInQuestForecasts);
        await _partySnapshotStore.SaveAsync(
            enrichedParty with
            {
                Quest = enrichedQuest
            },
            cancellationToken);
    }

    private async Task LoadCachedStateAsync(CancellationToken cancellationToken)
    {
        var diagnosticsLogEntries = await _diagnosticsLogStore.GetRecentAsync(cancellationToken);
        var gearCatalog = await _gearCatalogStore.GetLatestAsync(cancellationToken);
        var taskSnapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);
        var userSnapshot = await _userSnapshotStore.GetLatestAsync(cancellationToken);
        var partySnapshot = await _partySnapshotStore.GetLatestAsync(cancellationToken);
        var userId = State.UserId ?? _currentCredentials?.UserId;
        var equipmentPresets = string.IsNullOrWhiteSpace(userId)
            ? Array.Empty<EquipmentPreset>()
            : await _equipmentPresetStore.GetForUserAsync(userId, cancellationToken);

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
            GearCatalogSnapshot = gearCatalog,
            EquipmentPresets = equipmentPresets,
            IncludeStalePartyMembersInQuestForecasts = _includeStalePartyMembersInQuestForecasts,
            CloudSyncSectionStatuses = State.CloudSyncSectionStatuses,
            CloudSyncExcludedSections = _cloudSyncExcludedSections.ToArray(),
            UserId = userId,
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
            var credentials = new HabiticaCredentials(request.UserId.Trim(), request.ApiToken.Trim());
            _currentCredentials = credentials;
            _persistLocally = request.PersistLocally;

            var loginResult = await _loginWorkflow.AuthenticateMinimalAsync(
                new LoginCommand(credentials.UserId, credentials.ApiToken, request.PersistLocally),
                cancellationToken);

            var userSnapshot = await _userSnapshotStore.GetLatestAsync(cancellationToken);
            var equipmentPresets = await _equipmentPresetStore.GetForUserAsync(credentials.UserId, cancellationToken);
            var domainRequests = new (RefreshDomain Domain, RefreshPriority Priority)[]
            {
                (RefreshDomain.Tasks, RefreshPriority.Background),
                (RefreshDomain.Party, RefreshPriority.Background),
                (RefreshDomain.GearCatalog, RefreshPriority.Background)
            };

            SetState(new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: loginResult.DisplayName,
                ErrorMessage: null,
                LastSyncedAtUtc: loginResult.RetrievedAtUtc,
                TaskFreshness: ClassifyFreshness(State.TaskSnapshot),
                TaskSnapshot: State.TaskSnapshot,
                ClassName: loginResult.ClassName,
                Level: loginResult.Level,
                UserSnapshot: userSnapshot,
                UserFreshness: ClassifyFreshness(userSnapshot),
                UserId: credentials.UserId,
                EquipmentPresets: equipmentPresets,
                PartySnapshot: State.PartySnapshot,
                PartyFreshness: ClassifyFreshness(State.PartySnapshot),
                GearCatalogSnapshot: State.GearCatalogSnapshot,
                DiagnosticsLogEntries: await _diagnosticsLogStore.GetRecentAsync(cancellationToken),
                IncludeStalePartyMembersInQuestForecasts: _includeStalePartyMembersInQuestForecasts,
                DomainStates: MarkDomainsFetching(domainRequests, RefreshReason.AppBoot),
                CloudSyncSectionStatuses: State.CloudSyncSectionStatuses,
                CloudSyncExcludedSections: _cloudSyncExcludedSections.ToArray()));
            _ = CompleteSignInRefreshAsync(credentials);
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

    private async Task CompleteSignInRefreshAsync(HabiticaCredentials credentials)
    {
        var cancellationToken = CancellationToken.None;
        var domainStates = State.DomainStates is null
            ? new Dictionary<RefreshDomain, DomainRefreshState>()
            : new Dictionary<RefreshDomain, DomainRefreshState>(State.DomainStates);
        var domainRequests = new (RefreshDomain Domain, RefreshPriority Priority)[]
        {
            (RefreshDomain.Tasks, RefreshPriority.Background),
            (RefreshDomain.Party, RefreshPriority.Background),
            (RefreshDomain.GearCatalog, RefreshPriority.Background)
        };

        try
        {
            await _refreshCoordinator.RefreshDomainsAsync(
                credentials,
                domainRequests,
                state =>
                {
                    domainStates[state.Domain] = state;
                    LoadCachedStateAndNotify(domainStates, cancellationToken);
                },
                cancellationToken,
                RefreshReason.AppBoot);

            await LoadCachedStateAsync(cancellationToken);
            await TryMergeAndUploadCloudSyncAsync(credentials, cancellationToken, RefreshReason.AppBoot);
            await TryMergeAndUploadPartySyncAsync(credentials, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            PreserveSyncDomainStates(domainStates);

            SetState(State with
            {
                DomainStates = domainStates
            });
        }
        catch (Exception exception)
        {
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Sync,
                "sign-in-background-refresh",
                DiagnosticsSeverity.Warning,
                DiagnosticsMode.LiveRead,
                exception.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reason"] = RefreshReason.AppBoot.ToString()
                },
                cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                DomainStates = domainStates
            });
        }
    }

    private void PreserveSyncDomainStates(Dictionary<RefreshDomain, DomainRefreshState> domainStates)
    {
        if (State.DomainStates?.TryGetValue(RefreshDomain.CloudSync, out var cloudSyncState) == true)
        {
            domainStates[RefreshDomain.CloudSync] = cloudSyncState;
        }

        if (State.DomainStates?.TryGetValue(RefreshDomain.PartySync, out var partySyncState) == true)
        {
            domainStates[RefreshDomain.PartySync] = partySyncState;
        }
    }

    private async Task<GearCatalogSnapshot?> RefreshGearCatalogAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await _habiticaSyncClient.GetContentCatalogAsync(credentials, cancellationToken);
            await _gearCatalogStore.SaveAsync(catalog, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-refresh-catalog",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveRead,
                "Refreshed gear content catalog.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemCount"] = catalog.Items.Count.ToString(CultureInfo.InvariantCulture),
                    ["requestCount"] = "1"
                },
                cancellationToken);
            return catalog;
        }
        catch (Exception exception)
        {
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-refresh-catalog",
                DiagnosticsSeverity.Warning,
                DiagnosticsMode.LiveRead,
                exception.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["requestCount"] = "1"
                },
                cancellationToken);
            return await _gearCatalogStore.GetLatestAsync(cancellationToken);
        }
    }

    private async Task<InventoryMutationValidation> ValidateInventoryMutationAsync(
        string operation,
        CancellationToken cancellationToken)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return new InventoryMutationValidation(null, null, await FailInventoryActionAsync(operation, "Sign in is required before changing equipment.", cancellationToken));
        }

        if (State.UserSnapshot is null)
        {
            return new InventoryMutationValidation(credentials, null, await FailInventoryActionAsync(operation, "Refresh account data before changing equipment.", cancellationToken));
        }

        if (State.UserFreshness != SnapshotFreshnessState.Fresh)
        {
            return new InventoryMutationValidation(credentials, State.UserSnapshot, await FailInventoryActionAsync(operation, "Fresh account data is required before changing equipment.", cancellationToken));
        }

        return new InventoryMutationValidation(credentials, State.UserSnapshot, null);
    }

    private async Task<InventoryActionResult> FailInventoryActionAsync(
        string operation,
        string message,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        await _diagnosticsLogWriter.WriteAsync(
            DiagnosticsFeatureArea.Inventory,
            operation,
            DiagnosticsSeverity.Error,
            operation.Contains("equip", StringComparison.Ordinal) ? DiagnosticsMode.LiveMutation : DiagnosticsMode.Local,
            message,
            metadata,
            cancellationToken);
        var diagnosticsLogEntries = await _diagnosticsLogStore.GetRecentAsync(cancellationToken);

        SetState(State with
        {
            DiagnosticsLogEntries = diagnosticsLogEntries,
            ErrorMessage = message,
            IsBusy = false
        });

        return InventoryActionResult.Failure(message);
    }

    private string ResolveGearName(string key)
    {
        return State.GearCatalogSnapshot?.Items.TryGetValue(key, out var item) == true
            ? item.Text
            : key;
    }

    private static bool CanUseGearKey(UserSnapshot snapshot, EquipmentSetKind kind, string key)
    {
        if (IsUnequippedBaseKey(key))
        {
            return false;
        }

        return snapshot.Inventory.OwnedGearKeys.Contains(key, StringComparer.Ordinal)
            || EnumerateSlots(kind == EquipmentSetKind.Battle ? snapshot.Equipment.Battle : snapshot.Equipment.Costume)
                .Any(slot => string.Equals(slot.Key, key, StringComparison.Ordinal));
    }

    private static GearSlotsSnapshot NormalizeBaseSlots(GearSlotsSnapshot slots)
    {
        return new GearSlotsSnapshot(
            NormalizeGearKey(slots.Head),
            NormalizeGearKey(slots.Armor),
            NormalizeGearKey(slots.Weapon),
            NormalizeGearKey(slots.Shield),
            NormalizeGearKey(slots.Back),
            NormalizeGearKey(slots.HeadAccessory),
            NormalizeGearKey(slots.Eyewear),
            NormalizeGearKey(slots.Body));
    }

    private static GearSlotsSnapshot NormalizePresetSlots(EquipmentSetKind kind, GearSlotsSnapshot slots)
    {
        return NormalizeBaseSlots(slots);
    }

    private static string? NormalizeGearKey(string? key)
    {
        return string.IsNullOrWhiteSpace(key) || IsUnequippedBaseKey(key) ? null : key;
    }

    private static bool IsUnequippedBaseKey(string key)
    {
        return key.EndsWith("_base_0", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> PresetMetadata(
        EquipmentPreset preset,
        int changedSlotCount = 0,
        int skippedSlotCount = 0,
        int requestCount = 0,
        string? failedSlot = null,
        string? itemKey = null)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["presetId"] = preset.Id,
            ["presetName"] = preset.Name,
            ["presetKind"] = preset.Kind.ToString(),
            ["changedSlotCount"] = changedSlotCount.ToString(CultureInfo.InvariantCulture),
            ["skippedSlotCount"] = skippedSlotCount.ToString(CultureInfo.InvariantCulture),
            ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(failedSlot))
        {
            metadata["failedSlot"] = failedSlot;
        }

        if (!string.IsNullOrWhiteSpace(itemKey))
        {
            metadata["itemKey"] = itemKey;
        }

        return metadata;
    }

    private static IReadOnlyDictionary<string, string> MergeMetadata(
        IReadOnlyDictionary<string, string> baseMetadata,
        int changedSlotCount = 0,
        int skippedSlotCount = 0,
        int requestCount = 0,
        string? failedSlot = null,
        string? itemKey = null)
    {
        var metadata = new Dictionary<string, string>(baseMetadata, StringComparer.Ordinal)
        {
            ["changedSlotCount"] = changedSlotCount.ToString(CultureInfo.InvariantCulture),
            ["skippedSlotCount"] = skippedSlotCount.ToString(CultureInfo.InvariantCulture),
            ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(failedSlot))
        {
            metadata["failedSlot"] = failedSlot;
        }

        if (!string.IsNullOrWhiteSpace(itemKey))
        {
            metadata["itemKey"] = itemKey;
        }

        return metadata;
    }

    private async Task<int> EquipSlotsWithoutRefreshAsync(
        HabiticaCredentials credentials,
        EquipmentSetKind kind,
        GearSlotsSnapshot currentSlots,
        GearSlotsSnapshot desiredSlots,
        string operationId,
        string label,
        CancellationToken cancellationToken,
        Action<string, string?>? gearChanged = null)
    {
        var changedSlots = EnumerateSlots(NormalizePresetSlots(kind, desiredSlots))
            .Where(slot => !string.Equals(NormalizeGearKey(GetSlotValue(currentSlots, slot.SlotTitle)), slot.Key, StringComparison.Ordinal))
            .ToArray();
        if (changedSlots.Length == 0)
        {
            SetState(State with { ActiveEquipmentProgress = null });
            return 0;
        }

        SetState(State with
        {
            ActiveEquipmentProgress = new EquipmentProgress(operationId, label, 0, changedSlots.Length),
            ErrorMessage = null,
            IsBusy = true
        });

        var completed = 0;
        foreach (var slot in changedSlots)
        {
            var keyToToggle = slot.Key ?? NormalizeGearKey(GetSlotValue(currentSlots, slot.SlotTitle));
            if (string.IsNullOrWhiteSpace(keyToToggle))
            {
                continue;
            }

            await _habiticaSyncClient.EquipGearAsync(credentials, kind, keyToToggle, cancellationToken);
            gearChanged?.Invoke(slot.SlotTitle, slot.Key);
            completed++;
            await DelayBetweenHabiticaRequestsAsync(cancellationToken);
            SetState(State with
            {
                ActiveEquipmentProgress = new EquipmentProgress(operationId, label, completed, changedSlots.Length)
            });
        }

        return completed;
    }

    private Task DelayBetweenHabiticaRequestsAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(Math.Max(0, _featureOptions.HabiticaRequestDelayMilliseconds));
        return delay <= TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(delay, _timeProvider, cancellationToken);
    }

    private static IEnumerable<(string SlotTitle, string? Key)> EnumerateSlots(GearSlotsSnapshot slots)
    {
        yield return ("Head", slots.Head);
        yield return ("Head Accessory", slots.HeadAccessory);
        yield return ("Eyewear", slots.Eyewear);
        yield return ("Armor", slots.Armor);
        yield return ("Body", slots.Body);
        yield return ("Weapon", slots.Weapon);
        yield return ("Shield", slots.Shield);
        yield return ("Back", slots.Back);
    }

    private static string? GetSlotValue(GearSlotsSnapshot slots, string slotTitle)
    {
        return slotTitle switch
        {
            "Head" => slots.Head,
            "Head Accessory" => slots.HeadAccessory,
            "Eyewear" => slots.Eyewear,
            "Armor" => slots.Armor,
            "Body" => slots.Body,
            "Weapon" => slots.Weapon,
            "Shield" => slots.Shield,
            "Back" => slots.Back,
            _ => null
        };
    }

    private static GearSlotsSnapshot SetSlotValue(GearSlotsSnapshot slots, string slotTitle, string? key)
    {
        return slotTitle switch
        {
            "Head" => slots with { Head = key },
            "Head Accessory" => slots with { HeadAccessory = key },
            "Eyewear" => slots with { Eyewear = key },
            "Armor" => slots with { Armor = key },
            "Body" => slots with { Body = key },
            "Weapon" => slots with { Weapon = key },
            "Shield" => slots with { Shield = key },
            "Back" => slots with { Back = key },
            _ => slots
        };
    }

    private sealed record InventoryMutationValidation(
        HabiticaCredentials? Credentials,
        UserSnapshot? Snapshot,
        InventoryActionResult? Result);

    private sealed record PartySyncUploadResult(
        bool MergedRemoteHistory,
        int UploadedEventCount);

    private sealed record PartyQuestMutationValidation(
        PartySyncClaim? Claim,
        PartyQuestPoolEntry? PoolEntry,
        PartyQuestActionResult? Result)
    {
        public string? PartyId => Claim?.PartyId;

        public static PartyQuestMutationValidation Fail(string message)
        {
            return new PartyQuestMutationValidation(null, null, PartyQuestActionResult.Failure(message));
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

    private static string BuildArmoireResultMessage(IReadOnlyList<ArmoirePurchaseSnapshot> drops)
    {
        if (drops.Count == 0)
        {
            return "Armoire opened.";
        }

        var gearCount = drops.Count(static drop => string.Equals(drop.DropType, "gear", StringComparison.OrdinalIgnoreCase));
        var foodCount = drops.Count(static drop => string.Equals(drop.DropType, "food", StringComparison.OrdinalIgnoreCase));
        var experienceCount = drops.Count(static drop => string.Equals(drop.DropType, "experience", StringComparison.OrdinalIgnoreCase));
        var parts = new List<string>();
        if (gearCount > 0)
        {
            parts.Add($"{gearCount} gear");
        }

        if (foodCount > 0)
        {
            parts.Add($"{foodCount} food");
        }

        if (experienceCount > 0)
        {
            parts.Add($"{experienceCount} XP");
        }

        return parts.Count == 0
            ? $"Opened the armoire {drops.Count} time{(drops.Count == 1 ? string.Empty : "s")}."
            : $"Opened the armoire {drops.Count} time{(drops.Count == 1 ? string.Empty : "s")}: {string.Join(", ", parts)}.";
    }

    private void SetState(SessionViewModel nextState)
    {
        var userId = nextState.UserId ?? _currentCredentials?.UserId;
        State = nextState with
        {
            IsAdmin = _featureOptions.IsAdmin(userId),
            IsPartySyncEnabled = _featureOptions.PartySyncEnabled
        };
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
