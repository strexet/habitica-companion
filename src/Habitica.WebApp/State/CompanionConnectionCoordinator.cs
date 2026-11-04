using Habitica.Application.Integration;
using Habitica.Application.Notes;
using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public enum CompanionConnectionState { SignedOut, Loading, Browser, Cloud, CloudUnavailable, SettingsConflict, ConsolidationRequired, LegacyRecoveryRequired, StorageUnavailable }

public sealed class CompanionConnectionCoordinator(CompanionPreferencesStore preferences, IBrowserWorkspaceSource browser,
    IChatGptIntegrationClient integration, IWorkspaceCloudClient workspace, INotesCloudClient notes)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _generation;
    private bool _startupPending,_startupComplete;
    private string? _attempt;
    public string? Account { get; private set; }
    public CompanionAccountPreference Preference { get; private set; } = CompanionPreferencesJson.Default();
    public CompanionConnectionState State { get; private set; } = CompanionConnectionState.SignedOut;
    public Guid? BrowserWorkspaceId { get; private set; }
    public bool CloudSelected => Preference.CloudSelected;
    public Guid? ActiveWorkspaceId => CloudSelected ? Preference.WorkspaceId : BrowserWorkspaceId;
    public bool TaskAccessVerified => CloudSelected && workspace.WorkspaceId == Preference.WorkspaceId && workspace.ConnectionStatusVerified;
    public bool NotesAccessVerified => CloudSelected && notes.IsConnected && notes.NotebookId == Preference.NotebookId;
    public string? IntegrationCode { get; private set; }
    public string? WorkspaceCode { get; private set; }
    public string? NotesCode { get; private set; }
    public IntegrationStatusResult? IntegrationStatus { get; private set; }
    public event Action? Changed;

    public void BindAccount(string? account)
    {
        var normalized = Guid.TryParse(account, out var id) ? id.ToString("D") : null;
        if (Account == normalized) return;
        _generation++; _startupPending=_startupComplete=false; Account = normalized; _attempt = null; BrowserWorkspaceId = null;
        Preference = CompanionPreferencesJson.Default(); IntegrationStatus = null;
        IntegrationCode = WorkspaceCode = NotesCode = null;
        State = Account is null ? CompanionConnectionState.SignedOut : CompanionConnectionState.Loading;
        Changed?.Invoke();
    }

    public void BeginStartup(string account)
    { if(Account==Guid.Parse(account).ToString("D")&&!_startupComplete){_startupPending=true;State=CompanionConnectionState.Loading;Changed?.Invoke();} }
    public async Task CompleteStartupAsync(string account,CancellationToken token)
    {
        account=Guid.Parse(account).ToString("D");var generation=_generation;await _gate.WaitAsync(token);
        try{if(!Current(account,generation))return;_startupPending=false;_startupComplete=true;await ReadPreferenceAsync(account,generation,token);await SelectSourceAsync(account,generation,token);}
        catch{if(Current(account,generation)){State=CompanionConnectionState.StorageUnavailable;Changed?.Invoke();}}
        finally{_gate.Release();}
    }

    private bool Current(string account, long generation) => Account == account && _generation == generation;

    public async Task RefreshSelectionAsync(string account, CancellationToken token)
    {
        account = Guid.Parse(account).ToString("D"); var generation = _generation;
        await _gate.WaitAsync(token);
        try
        {
            if (!Current(account, generation)) return;
            await ReadPreferenceAsync(account, generation, token);
            await SelectSourceAsync(account, generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { if (Current(account, generation)) { State = CompanionConnectionState.StorageUnavailable; Changed?.Invoke(); } }
        finally { _gate.Release(); }
    }

    private async Task ReadPreferenceAsync(string account, long generation, CancellationToken token)
    {
        var value = await preferences.ReadAsync(account, token);
        if (Current(account, generation)) Preference = value;
    }

    private async Task SelectSourceAsync(string account, long generation, CancellationToken token)
    {
        if (!Current(account, generation)) return;
        if (Preference.Conflict) { State = CompanionConnectionState.SettingsConflict; BrowserWorkspaceId = null; }
        else if (CloudSelected) { State = TaskAccessVerified ? CompanionConnectionState.Cloud : CompanionConnectionState.CloudUnavailable; BrowserWorkspaceId = null; }
        else if(_startupPending){State=CompanionConnectionState.Loading;BrowserWorkspaceId=null;}
        else
        {
            var selected = await browser.EnsureDefaultAsync(account, token);
            if (!Current(account, generation)) return;
            BrowserWorkspaceId = selected.WorkspaceId;
            State = selected.RequiresRecovery ? CompanionConnectionState.LegacyRecoveryRequired
                : selected.RequiresConsolidation ? CompanionConnectionState.ConsolidationRequired
                : selected.WorkspaceId is null ? CompanionConnectionState.StorageUnavailable : CompanionConnectionState.Browser;
        }
        Changed?.Invoke();
    }

    public async Task BootstrapAsync(HabiticaCredentials credentials, bool legacyAcknowledged, bool retry, CancellationToken token)
    {
        var account = Guid.Parse(credentials.UserId).ToString("D"); var generation = _generation;
        await _gate.WaitAsync(token);
        try
        {
            if (!Current(account, generation)) return;
            await ReadPreferenceAsync(account, generation, token);
            if (!Current(account, generation)) return;
            var reference = Preference.Recovery;
            var revoked = reference is not null && Preference.RevokedConnections.Contains(reference.ConnectionId);
            var key = $"{reference?.ProfileId}:{reference?.ConnectionId}:{Preference.WorkspaceId}:{Preference.NotebookId}:{Preference.RecoverWorkspace}:{Preference.RecoverNotes}";
            if (!legacyAcknowledged && (reference is null || revoked || Preference.Conflict || !retry && _attempt == key))
            { await SelectSourceAsync(account, generation, token); return; }
            _attempt = key; State = CompanionConnectionState.Loading; Changed?.Invoke();
            if (legacyAcknowledged || retry || !integration.HasConnection || integration.RecoveryDescriptor != reference)
            {
                var recovered = await integration.RecoverAsync(credentials, legacyAcknowledged ? null : reference, legacyAcknowledged, token);
                if (!Current(account, generation)) return;
                IntegrationCode = recovered.Code;
                if (!recovered.Succeeded)
                {
                    IntegrationStatus = null;
                    if (reference is not null && recovered.Code == "existing_connection_revoked")
                        await UpdateAsync(account, generation, value => value with { RevokedConnections = value.RevokedConnections.Append(reference.ConnectionId).Distinct().ToArray() }, token);
                    await SelectSourceAsync(account, generation, token); return;
                }
            }
            var status = await integration.GetStatusAsync(token);
            if (!Current(account, generation)) return;
            IntegrationStatus = status;
            if (legacyAcknowledged || Preference.RecoverWorkspace)
            {
                var result = await workspace.RecoverConnectionAsync(legacyAcknowledged ? null : Preference.WorkspaceId, token);
                if (!Current(account, generation)) return;
                WorkspaceCode = result.Message;
                if (!result.Succeeded && result.Message is "workspace_missing" or "workspace_disconnected" or "workspace_connection_changed" or "workspace_consent_unsupported")
                    await UpdateAsync(account, generation, value => value with { RecoverWorkspace = false }, token);
            }
            if (legacyAcknowledged || Preference.RecoverNotes)
            {
                var result = await notes.RecoverConnectionAsync(legacyAcknowledged ? null : Preference.NotebookId, token);
                if (!Current(account, generation)) return;
                NotesCode = result.Code;
                if (!result.Succeeded && result.Code is "notes_missing" or "notes_disconnected" or "notes_connection_changed" or "notes_consent_unsupported")
                    await UpdateAsync(account, generation, value => value with { RecoverNotes = false }, token);
            }
            if (legacyAcknowledged)
                await UpdateAsync(account, generation, value => value with
                {
                    Recovery = integration.RecoveryDescriptor, Conflict = false,
                    CloudSelected = value.CloudSelected || workspace.WorkspaceId is not null && workspace.ConnectionStatusVerified,
                    WorkspaceId = workspace.ConnectionStatusVerified ? workspace.WorkspaceId : value.WorkspaceId,
                    NotebookId = notes.IsConnected ? notes.NotebookId : value.NotebookId,
                    RecoverWorkspace = workspace.ConnectionStatusVerified, RecoverNotes = notes.IsConnected
                }, token);
            await SelectSourceAsync(account, generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch
        {
            if (Current(account, generation))
            { IntegrationCode = "recovery_unavailable"; State = CloudSelected ? CompanionConnectionState.CloudUnavailable : CompanionConnectionState.StorageUnavailable; Changed?.Invoke(); }
        }
        finally { _gate.Release(); }
    }

    public async Task RememberConnectionAsync(string account, bool workspaceConnected, bool notesConnected, CancellationToken token, bool activateWorkspace = false)
    {
        account = Guid.Parse(account).ToString("D"); var generation = _generation;
        await _gate.WaitAsync(token);
        try
        {
            if (!Current(account, generation)) return;
            await UpdateAsync(account, generation, value => value with
            {
                Recovery = integration.RecoveryDescriptor ?? value.Recovery, Conflict = workspaceConnected ? false : value.Conflict,
                RevokedConnections = integration.RecoveryDescriptor is { } active ? value.RevokedConnections.Where(id => id != active.ConnectionId).ToArray() : value.RevokedConnections,
                CloudSelected = value.CloudSelected || workspaceConnected && activateWorkspace,
                WorkspaceId = workspaceConnected ? workspace.WorkspaceId : value.WorkspaceId,
                NotebookId = notesConnected ? notes.NotebookId : value.NotebookId,
                RecoverWorkspace = workspaceConnected || value.RecoverWorkspace,
                RecoverNotes = notesConnected || value.RecoverNotes
            }, token);
            await SelectSourceAsync(account, generation, token);
        }
        finally { _gate.Release(); }
    }

    public async Task SuppressAsync(string account, bool profile, bool tasks, bool personalNotes, CancellationToken token)
    {
        account = Guid.Parse(account).ToString("D"); var generation = _generation;
        await _gate.WaitAsync(token);
        try
        {
            if (!Current(account, generation)) return;
            await UpdateAsync(account, generation, value => value with
            {
                Recovery = profile ? null : value.Recovery,
                RecoverWorkspace = !profile && !tasks && value.RecoverWorkspace,
                RecoverNotes = !profile && !personalNotes && value.RecoverNotes
            }, token);
            IntegrationStatus = null; await SelectSourceAsync(account, generation, token);
        }
        finally { _gate.Release(); }
    }

    private async Task UpdateAsync(string account, long generation, Func<CompanionAccountPreference, CompanionAccountPreference> update, CancellationToken token)
    {
        var result = await preferences.UpdateAsync(account, update, token, () => Current(account, generation));
        if (Current(account, generation)) Preference = result;
    }
}
