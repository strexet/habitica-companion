using Habitica.Application.Diagnostics;
using Habitica.Application.Workspace;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed record WorkspaceOrganizationInput(WorkspaceOrganizationKind Kind, WorkspaceOrganizationOperation Operation,
    Guid? EntityId = null, WorkspaceOrganizationPatch? Patch = null);

/// <summary>Account-bound native organization intents and receipts, independent of task/game commands.</summary>
public sealed class WorkspaceOrganizationController : IDisposable
{
    private readonly IAppSessionController _session;
    private readonly IWorkspaceCloudClient _cloud;
    private readonly IWorkspaceOrganizationApi _api;
    private readonly IndexedDbWorkspaceDatabase _database;
    private readonly TimeProvider _clock;
    private readonly DiagnosticsLogWriter? _diagnostics;
    private readonly Dictionary<string, Context> _pending = [];
    private Context? _context;
    private string? _observedAccount;
    private long _generation, _accountGeneration, _loadVersion;
    private bool _busy;
    public WorkspaceOrganizationController(IAppSessionController session, IWorkspaceCloudClient cloud,
        IWorkspaceOrganizationApi api, IndexedDbWorkspaceDatabase database, TimeProvider? clock = null, DiagnosticsLogWriter? diagnostics = null)
    {
        _session = session; _cloud = cloud; _api = api; _database = database;
        _clock = clock ?? TimeProvider.System; _diagnostics = diagnostics; _observedAccount = Account;
        _session.Changed += SessionChanged;
    }
    private string? Account => _session.State.IsAuthenticated && Guid.TryParse(_session.State.UserId, out var id) ? id.ToString("D") : null;
    private Context? Current => _context?.Account == Account && Account is not null ? _context : null;
    public event Action? Changed;
    public bool IsBusy => _busy;
    public bool IsCloud => Current?.Cloud == true;
    public Guid? WorkspaceId => Current?.WorkspaceId;
    public WorkspaceJournalSnapshot? Snapshot => Current is { RequiresReload: false } context && (!context.Cloud || _cloud.WorkspaceId == context.WorkspaceId) ? context.Snapshot : null;
    public bool HasPendingIntent => Current?.Intent is not null;
    public bool AcceptanceUnknown => Current?.AcceptanceUnknown == true;
    public bool RequiresReload => Current?.RequiresReload != false;
    public bool IsReadOnly => Current is not { Snapshot: { } snapshot } context || context.RequiresReload
        || context.Cloud && _cloud.WorkspaceId != context.WorkspaceId
        || !context.Cloud && snapshot.Authority?.Mode is WorkspaceAuthorityMode.Frozen or WorkspaceAuthorityMode.Cloud;
    public WorkspaceOrganizationPreview? Preview => Current?.Preview;
    public WorkspaceOrganizationReceipt? LastReceipt => Current?.Receipt;
    public string? Message => Current?.Message;
    private bool Matches(Context context, long generation) => ReferenceEquals(context, Current) && _generation == generation && (!context.Cloud || _cloud.WorkspaceId == context.WorkspaceId);

    private void SessionChanged()
    {
        var account = Account;
        if (_observedAccount == account) return;
        _generation++; _accountGeneration++; _loadVersion++; _observedAccount = account;
        if (_context is { Intent: not null } prior) _pending[prior.Account] = prior;
        _context = account is not null && _pending.TryGetValue(account, out var retained) ? retained : null;
        if (_context is { } context) { context.RequiresReload = true; context.Message = "An original organization request remains. Reload its exact workspace or recover its original receipt before another action."; }
        Changed?.Invoke();
    }

    public Task LoadAsync(bool cloudSource, Guid? localId = null, CancellationToken token = default) => RunAsync("workspace-load", async ct =>
    {
        var account = Account ?? throw new OrganizationStateException("Sign in before managing organization.");
        var prior = Current;
        var id = cloudSource ? _cloud.WorkspaceId ?? (prior is { Cloud: true, Intent: not null } ? prior.WorkspaceId : Guid.Empty)
            : localId ?? (prior is { Cloud: false } ? prior.WorkspaceId : Guid.Empty);
        if (prior?.Intent is not null && (prior.Cloud != cloudSource || id != prior.WorkspaceId)) throw new OrganizationStateException("Resolve the original organization request before changing workspace sources.");
        if (id == Guid.Empty && !cloudSource)
        {
            var generation = _generation; var ids = await _database.ListAsync(account, ct);
            if (generation != _generation || account != Account) return;
            if (ids.Count != 1) throw new OrganizationStateException("Choose the exact available browser workspace.");
            id = ids[0];
        }
        if (id == Guid.Empty) throw new OrganizationStateException("Connect and choose an available cloud workspace.");
        var context = prior is not null && prior.Cloud == cloudSource && prior.WorkspaceId == id ? prior : new Context(account, cloudSource, id);
        _context = context; context.RequiresReload = true;
        await ReloadAsync(context, ct);
    }, token, cloudSource);

    private async Task ReloadAsync(Context context, CancellationToken token)
    {
        var generation = _generation; var version = ++_loadVersion;
        var snapshot = context.Cloud ? await _cloud.LoadWorkspaceAsync(token) : await _database.ForScope(context.Account).LoadAsync(context.WorkspaceId, token);
        if (!Matches(context, generation) || version != _loadVersion) return;
        token.ThrowIfCancellationRequested();
        if (snapshot is null || snapshot.Document.Id != context.WorkspaceId || !WorkspaceJournalValidation.IsValid(snapshot))
        { context.RequiresReload = true; context.Message = "Organization data is unavailable. Keep any original request and reload the exact workspace."; return; }
        context.Snapshot = snapshot; context.RequiresReload = false; context.Message = "Native organization data loaded. No game effect was requested.";
    }

    public Task PrepareAsync(WorkspaceOrganizationInput input, CancellationToken token = default) => RunAsync("workspace-organization-prepare", ct => PrepareCoreAsync(input, ct), token);
    public Task RetryOriginalPreviewAsync(CancellationToken token = default) => RunAsync("workspace-organization-prepare", ct => PrepareCoreAsync(null, ct), token);
    private async Task PrepareCoreAsync(WorkspaceOrganizationInput? input, CancellationToken token)
    {
        var context = Current ?? throw new OrganizationStateException("Load the current account workspace.");
        if (IsReadOnly || context.Snapshot is null) throw new OrganizationStateException("Reload a writable organization workspace.");
        if (context.Intent is not null && input is not null) throw new OrganizationStateException("Resolve the original organization request first. Retry the original preview instead of submitting another edit.");
        if (context.Intent is null)
        {
            if (input is null) throw new OrganizationStateException("No original organization preparation is available.");
            var command = new WorkspaceOrganizationCommand(Guid.NewGuid(), context.WorkspaceId, context.Snapshot.Document.Revision,
                input.Kind, input.Operation, input.Operation == WorkspaceOrganizationOperation.Create ? Guid.NewGuid() : input.EntityId ?? Guid.Empty, input.Patch);
            // Validate references, bounds, authority and the exact resulting native snapshot before transport.
            var simulated = WorkspaceOrganizationEngine.Accept(context.Cloud ? context.Snapshot with { Authority = null } : context.Snapshot, command, _clock.GetUtcNow());
            if (simulated.Disposition != SubmissionDisposition.Accepted) { context.Message = "The organization edit is invalid, unavailable or stale. Review current fields and references."; return; }
            if (command.Operation == WorkspaceOrganizationOperation.Update && WorkspaceOrganizationSnapshot.From(context.Snapshot.Document, command.Kind, command.EntityId) is { } current
                && simulated.Entry!.Snapshot with { Revision = current.Revision } is { } changed && changed.Equivalent(current))
            { context.Message = "No organization fields changed."; return; }
            context.Intent = command; context.Baseline = context.Snapshot; context.RequestId = Guid.NewGuid(); context.Receipt = null;
        }
        var generation = _generation;
        if (context.Cloud)
        {
            var response = await _api.PrepareOrganizationAsync(context.RequestId, context.Intent, token);
            if (!Matches(context, generation)) return;
            if (!response.Succeeded || response.Value is not { } preview || !ValidPreview(context, preview))
            { context.Message = "No matching organization preview is available. Retry the original request before another edit."; return; }
            context.Preview = preview;
            if (preview.SavedInCompanion && preview.Receipt is { } receipt) await SavedAsync(context, receipt, token);
            else context.Message = "Review the exact native organization changes before saving. No game effect will occur.";
        }
        else
        {
            var simulated = WorkspaceOrganizationEngine.Accept(context.Snapshot, context.Intent, _clock.GetUtcNow());
            if (simulated.Disposition != SubmissionDisposition.Accepted) { context.Message = "The original organization preview is stale. Check its receipt before a new review."; return; }
            context.Preview = new(context.Intent.Id, context.RequestId, _clock.GetUtcNow().AddMinutes(10), false, context.Intent, simulated.Entry!.Snapshot, false, false);
            context.Message = "Review the exact native organization changes before saving. History and task relationships remain retained.";
        }
    }

    public void CancelPreview()
    {
        if (_busy) throw new OrganizationStateException("Wait for the current organization operation.");
        var context = Current;
        if (context?.AcceptanceUnknown == true) throw new OrganizationStateException("Recover the original organization receipt before leaving an uncertain acceptance.");
        if (context is not null) { context.Intent = null; context.Baseline = null; context.Preview = null; context.Message = null; _pending.Remove(context.Account); }
        Changed?.Invoke();
    }

    public Task ConfirmAsync(CancellationToken token = default) => RunAsync("workspace-organization-accept", async ct =>
    {
        var context = Current ?? throw new OrganizationStateException("Load the original workspace.");
        RequireOriginalAccess(context);
        var preview = context.Preview ?? throw new OrganizationStateException("Recover and review the original organization preview first.");
        if (!ValidPreview(context, preview)) throw new OrganizationStateException("The organization preview does not match its original request.");
        if (!context.AcceptanceUnknown && (preview.Expired || preview.ExpiresAtUtc <= _clock.GetUtcNow())) { context.Message = "The original preview expired. Check its receipt, then cancel and review a new action if no save occurred."; return; }
        var generation = _generation; var wasUnknown = context.AcceptanceUnknown; context.AcceptanceUnknown = true;
        if (context.Cloud)
        {
            var response = await _api.AcceptOrganizationAsync(preview, ct);
            if (!Matches(context, generation)) return;
            context.AcceptanceUnknown = wasUnknown || response.OutcomeUnknown;
            if (response.Succeeded && response.Value is { } receipt && ValidReceipt(context, receipt)) await SavedAsync(context, receipt, ct);
            else { context.AcceptanceUnknown |= response.Succeeded; context.Message = "No matching original organization receipt is available. Preserve the command and check its outcome."; }
        }
        else
        {
            var result = await new WorkspaceOrganizationService(_database.ForScope(context.Account), _clock).SubmitAsync(preview.Command, ct);
            if (!Matches(context, generation)) return;
            context.AcceptanceUnknown = wasUnknown || result?.Disposition == SubmissionDisposition.Unavailable;
            if (result?.SavedInCompanion == true && result.Entry is { } entry) await SavedAsync(context, Receipt(entry, context.RequestId), ct);
            else context.Message = context.AcceptanceUnknown ? "Local organization acceptance is unknown. Recover the original receipt before another edit." : "The organization command was not saved. Reload current state before a new review.";
        }
    }, token);

    public Task CheckOriginalAsync(CancellationToken token = default) => RunAsync("workspace-organization-recover", async ct =>
    {
        var context = Current ?? throw new OrganizationStateException("No original organization request is available.");
        RequireOriginalAccess(context);
        var command = context.Preview?.Command ?? context.Intent ?? throw new OrganizationStateException("No original organization request is available.");
        if (context.Cloud && context.Preview is null) throw new OrganizationStateException("Recover the original preview using the original request first.");
        var generation = _generation;
        if (context.Cloud)
        {
            var result = await _api.ReadOrganizationAsync(command.Id, ct);
            if (!Matches(context, generation)) return;
            if (result.Succeeded && result.Value is { } receipt && ValidReceipt(context, receipt)) await SavedAsync(context, receipt, ct);
            else context.Message = "No matching original organization receipt is visible. Keep the original reference; absence does not prove rejection.";
        }
        else
        {
            var snapshot = await _database.ForScope(context.Account).LoadAsync(context.WorkspaceId, ct);
            if (!Matches(context, generation)) return;
            if (snapshot is not null && WorkspaceJournalValidation.IsValid(snapshot) && snapshot.Document.OrganizationHistory?.FirstOrDefault(row => row.Command.Id == command.Id) is { } entry)
            {
                var receipt = Receipt(entry, context.RequestId);
                if (ValidReceipt(context, receipt)) { await SavedAsync(context, receipt, ct); return; }
            }
            context.Message = "No matching original organization receipt is visible. Keep the original reference; absence does not prove rejection.";
        }
    }, token);

    private static bool ValidPreview(Context context, WorkspaceOrganizationPreview preview)
    {
        if (context.Intent is not { } intent || preview.Command is not { } command || !WorkspaceOrganizationCommands.IsValid(command)
            || preview.PreparedCommandId != command.Id || preview.CorrelationId == Guid.Empty || preview.ExpiresAtUtc == default || preview.ExpiresAtUtc.Offset != TimeSpan.Zero
            || preview.MayAffectHabitica && preview.TagProjection?.RequestsMetadataChanges != true || preview.TagProjection is not null && !preview.TagProjection.IsValid() || preview.SavedInCompanion != (preview.Receipt is not null)) return false;
        var normalized = command with { Id = intent.Id, EntityId = intent.Operation == WorkspaceOrganizationOperation.Create ? intent.EntityId : command.EntityId };
        if (WorkspaceOrganizationCommands.Hash(normalized) != WorkspaceOrganizationCommands.Hash(intent)) return false;
        var current = context.Baseline is { } snapshot ? WorkspaceOrganizationSnapshot.From(snapshot.Document, command.Kind, command.EntityId) : null;
        var expected = command.Operation == WorkspaceOrganizationOperation.Create || current is not null ? WorkspaceOrganizationCommands.Apply(command, current) : null;
        if (expected is null || !expected.Equivalent(preview.Snapshot)) return false;
        return preview.Receipt is null || ValidReceipt(context, preview.Receipt, preview);
    }
    private static bool ValidReceipt(Context context, WorkspaceOrganizationReceipt receipt, WorkspaceOrganizationPreview? owner = null)
    {
        var preview = owner ?? context.Preview; var command = preview?.Command;
        var entry = receipt.Entry;
        return command is not null && entry?.Command is not null && entry.Snapshot is not null
            && receipt.CommandId == command.Id && receipt.CorrelationId == preview!.CorrelationId && receipt.WorkspaceId == context.WorkspaceId
            && receipt.EntityId == command.EntityId && receipt.Kind == command.Kind && receipt.Operation == command.Operation
            && receipt.SavedInCompanion && (!receipt.MayAffectHabitica||preview.TagProjection?.RequestsMetadataChanges==true) && !receipt.OutcomeUnknown && receipt.AcceptedEventCount == 0 && receipt.ConfirmedEffectCount == 0
            && entry.AcceptedAtUtc != default && entry.AcceptedAtUtc.Offset == TimeSpan.Zero
            && entry.PayloadHash == WorkspaceOrganizationCommands.Hash(command) && WorkspaceOrganizationCommands.Hash(entry.Command) == entry.PayloadHash
            && entry.Snapshot.Equivalent(preview.Snapshot);
    }
    public static WorkspaceOrganizationReceipt Receipt(WorkspaceOrganizationEntry entry, Guid correlationId) => new(entry.Command.Id, correlationId,
        entry.Command.WorkspaceId, entry.Command.Kind, entry.Command.Operation, entry.Command.EntityId, entry, true, false, 0, 0, false, false);
    private void RequireOriginalAccess(Context context)
    {
        if (context.Cloud && _cloud.WorkspaceId != context.WorkspaceId) throw new OrganizationStateException("Restore access to the original cloud workspace before accepting or recovering this command.");
    }

    private async Task SavedAsync(Context context, WorkspaceOrganizationReceipt receipt, CancellationToken token)
    {
        context.Receipt = receipt with { RefreshSucceeded = false }; context.AcceptanceUnknown = false; context.Intent = null; context.Baseline = null; context.Preview = null;
        _pending.Remove(context.Account); context.RequiresReload = true; var generation = _generation;
        try { await ReloadAsync(context, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { if (Matches(context, generation)) context.RequiresReload = true; }
        if (!Matches(context, generation)) return;
        context.Receipt = receipt with { RefreshSucceeded = !context.RequiresReload };
        context.Message = context.RequiresReload ? "The native organization command was saved, but refresh is unavailable. Reload before editing." : "Saved in Companion organization history. No game effects were requested or confirmed.";
    }

    private async Task RunAsync(string operation, Func<CancellationToken, Task> action, CancellationToken token, bool? cloudSource = null)
    {
        if (_busy) throw new OrganizationStateException("Wait for the current organization operation.");
        _busy = true; Changed?.Invoke();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var generation = _generation; var accountGeneration = _accountGeneration; var account = Account;
        var previousReceipt = LastReceipt; var previousPreview = Preview;
        async Task Execute()
        {
            try { deadline.Token.ThrowIfCancellationRequested(); await action(deadline.Token).WaitAsync(deadline.Token); }
            catch (OrganizationStateException) { throw; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { if (_generation == generation) _generation++; throw; }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                if (_generation == generation && Account == account && Current is { } context)
                {
                    _generation++; context.RequiresReload |= operation == "workspace-load";
                    context.Message = "Organization operation timed out. Keep the original request or command and recover its outcome.";
                }
            }
            catch { if (_generation == generation && Account == account && Current is { } context) { context.RequiresReload |= operation == "workspace-load"; context.Message = "Organization state is unavailable. Keep the original request or command and recover its outcome."; } }
        }
        try
        {
            if (_diagnostics is null) await Execute();
            else await _diagnostics.ObserveAsync(DiagnosticsFeatureArea.Workspace, operation, async () => { await Execute(); return true; },
                _ => Describe(),
                () => accountGeneration == _accountGeneration && account == Account, !(cloudSource ?? IsCloud) ? DiagnosticsMode.Local : operation == "workspace-organization-recover" || operation == "workspace-load" ? DiagnosticsMode.LiveRead : DiagnosticsMode.LiveMutation);
        }
        finally { _busy = false; Changed?.Invoke(); }

        DiagnosticsContext Describe()
        {
            var receipt = !ReferenceEquals(previousReceipt, LastReceipt) ? LastReceipt : null;
            var result = AcceptanceUnknown ? DiagnosticsResult.Unknown : receipt is not null ? DiagnosticsResult.Success
                : operation == "workspace-organization-recover" ? DiagnosticsResult.Unknown
                : RequiresReload ? DiagnosticsResult.Unavailable
                : operation == "workspace-organization-accept" ? DiagnosticsResult.Refused
                : operation == "workspace-load" || Preview is not null && !ReferenceEquals(Preview, previousPreview) ? DiagnosticsResult.Success
                : HasPendingIntent && Preview is null ? DiagnosticsResult.Unknown : DiagnosticsResult.Observed;
            return new(result, CommandId: receipt?.CommandId ?? Preview?.PreparedCommandId,
                CorrelationId: receipt?.CorrelationId ?? Preview?.CorrelationId, AcceptedCount: receipt?.AcceptedEventCount, ConfirmedCount: receipt?.ConfirmedEffectCount);
        }
    }
    public void Dispose() { _session.Changed -= SessionChanged; _pending.Clear(); _context = null; }
    private sealed class OrganizationStateException(string message) : InvalidOperationException(message);
    private sealed class Context(string account, bool cloud, Guid workspaceId)
    {
        public string Account { get; } = account;
        public bool Cloud { get; } = cloud;
        public Guid WorkspaceId { get; } = workspaceId;
        public WorkspaceJournalSnapshot? Snapshot, Baseline;
        public WorkspaceOrganizationCommand? Intent;
        public Guid RequestId;
        public WorkspaceOrganizationPreview? Preview;
        public WorkspaceOrganizationReceipt? Receipt;
        public bool AcceptanceUnknown, RequiresReload = true;
        public string? Message;
    }
}
