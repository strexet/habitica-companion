using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Application.Integration;
using Habitica.Application.Sync;
using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Domain.Notes;
using Habitica.Storage;
using Habitica.WebApp.Integration;
using Habitica.WebApp.State;

namespace Habitica.WebApp.Tests.Integration;

public sealed class CompanionPreferencesTests
{
    private const string Account = "00000000-0000-4000-8000-000000000011";
    private const string Other = "00000000-0000-4000-8000-000000000012";
    private const string Profile = "prf_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly Guid Workspace = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid Notebook = Guid.Parse("20000000-0000-4000-8000-000000000001");
    private static readonly IntegrationRecoveryDescriptor Descriptor = new("integration-recovery-v1", Profile, Guid.NewGuid());
    private static CompanionAccountPreference Entry(long revision = 1, bool cloud = true) => new(revision, Guid.NewGuid(), cloud, cloud ? Workspace : null, cloud ? Notebook : null, Descriptor, cloud, cloud, false, []);
    private static string Document(CompanionAccountPreference value, string account = Account) => CompanionPreferencesJson.Write(new(1, new Dictionary<string, CompanionAccountPreference> { [CompanionPreferencesJson.AccountScope(account)] = value }));
    private static CompanionAccountPreference Read(string json) => CompanionPreferencesJson.Read(json).Accounts[CompanionPreferencesJson.AccountScope(Account)];

    [Fact] public void Missing_sections_are_compatible_and_account_metadata_is_isolated()
    {
        Assert.Empty(CompanionPreferencesJson.Read(null).Accounts);
        var first = Entry(); var other = Entry();
        var combined = CompanionPreferencesJson.Merge(Document(first), Document(other, Other));
        Assert.Equal(2, CompanionPreferencesJson.Read(combined).Accounts.Count);
        var scoped = CompanionPreferencesJson.OnlyAccount(combined, Account);
        Assert.Single(CompanionPreferencesJson.Read(scoped).Accounts);
        Assert.Equal(Document(first), scoped);
        Assert.DoesNotContain(Other, scoped);
    }

    [Theory]
    [InlineData("apiToken")][InlineData("managementToken")][InlineData("sessionToken")][InlineData("draft")][InlineData("consentAcknowledged")]
    public void Unknown_or_authorizing_fields_are_refused(string key)
    {
        var text = Document(Entry()).Replace("\"revision\":1", "\"" + key + "\":\"private\",\"revision\":1");
        Assert.Throws<JsonException>(() => CompanionPreferencesJson.Read(text));
    }

    [Fact] public void Equal_revision_forks_fail_closed_commutatively_and_cloud_never_silently_downgrades()
    {
        var cloud = Entry(); var local = Entry(cloud: false);
        var merged = CompanionPreferencesJson.Merge(Document(cloud), Document(local));
        Assert.Equal(merged, CompanionPreferencesJson.Merge(Document(local), Document(cloud)));
        Assert.True(Read(merged).Conflict); Assert.True(Read(merged).CloudSelected); Assert.Null(Read(merged).Recovery);
        var newerLocal = Entry(99, false);
        var blocked = Read(CompanionPreferencesJson.Merge(Document(cloud), Document(newerLocal)));
        Assert.True(blocked.CloudSelected); Assert.True(blocked.Conflict); Assert.Equal(Workspace, blocked.WorkspaceId);
    }

    [Fact] public void Revocation_survives_newer_stale_metadata_and_mutation_identity_reuse_is_refused()
    {
        var revoked = Entry() with { RevokedConnections = new[] { Descriptor.ConnectionId } };
        var merged = Read(CompanionPreferencesJson.Merge(Document(revoked), Document(Entry(9))));
        Assert.Contains(Descriptor.ConnectionId, merged.RevokedConnections);
        Assert.Throws<JsonException>(() => CompanionPreferencesJson.Merge(Document(revoked), Document(revoked with { RecoverNotes = false })));
    }

    [Fact] public async Task Old_override_bundle_keeps_new_cloud_choice_and_exports_only_the_selected_account()
    {
        var storage = new InMemoryKeyValueStorage(); var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);
        var original = CompanionPreferencesJson.Merge(Document(Entry()), Document(Entry(), Other));
        await storage.SetRawJsonAsync(StorageKeys.CompanionPreferences, original, default);
        await service.ImportAsync(new LocalUserDataBundle(1, DateTimeOffset.UtcNow, Account, []), LocalDataImportMode.Override, default);
        Assert.Equal(original, await storage.GetRawJsonAsync(StorageKeys.CompanionPreferences, default));
        var exported = await service.ExportAsync(Account, default);
        var json = Assert.Single(exported.Records, x => x.Key == StorageKeys.CompanionPreferences).JsonText;
        Assert.Single(CompanionPreferencesJson.Read(json).Accounts); Assert.True(Read(json).CloudSelected);
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request); }
    private sealed class Browser(BrowserWorkspaceSelection selection) : IBrowserWorkspaceSource
    { public int Calls; public Task<BrowserWorkspaceSelection> EnsureDefaultAsync(string account, CancellationToken token) { Calls++; return Task.FromResult(selection); } }
    private sealed class Fixture : IDisposable
    {
        public InMemoryKeyValueStorage Storage = new(); public CompanionPreferencesStore Store; public Browser Browser;
        public HttpClient Http; public ChatGptIntegrationClient Integration; public WorkspaceCloudClient Tasks; public NotesCloudClient Notes;
        public CompanionConnectionCoordinator Coordinator; public List<string> Calls = []; public string? Error;
        public TaskCompletionSource<HttpResponseMessage>? Delayed;
        public Fixture(BrowserWorkspaceSelection? selection = null)
        {
            Store = new(Storage, new()); Browser = new(selection ?? new(Guid.NewGuid(), false, false));
            Http = new(new Handler(request =>
            {
                var path = request.RequestUri!.Host + request.RequestUri.AbsolutePath; Calls.Add(path);
                if (path == "auth.invalid/recover") return Delayed?.Task ?? Task.FromResult(Error is null ? Json(new { profileId = Profile, managementToken = new string('m',43), recovered = true, descriptor = Descriptor, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(8) }) : Json(new { error = Error }, HttpStatusCode.Forbidden));
                if (path == "auth.invalid/profiles/" + Profile + "/status") return Task.FromResult(Json(new { permissions = new[] { "tasks:read" }, credentialStatus = "last_verified" }));
                if (path == "workspace.invalid/recover") return Task.FromResult(Json(new { workspaceId = Workspace, sessionToken = new string('s',43), recovered = true, consentVersion = "workspace-v1", expiresAtUtc = DateTimeOffset.UtcNow.AddHours(8), executorEnabled = false, executorAvailable = true }));
                if (path == "workspace.invalid/notes/recover") return Task.FromResult(Json(new { notebookId = Notebook, ownerScope = NotesModel.Scope(Profile), accessEpoch = Guid.NewGuid(), sessionToken = new string('s',43), recovered = true, consentVersion = "notes-v1", expiresAtUtc = DateTimeOffset.UtcNow.AddHours(8) }));
                throw new InvalidOperationException("Unexpected operation: " + path);
            }));
            Integration = new(Http, "https://auth.invalid/"); Tasks = new(Http, Integration, "https://workspace.invalid/"); Notes = new(Http, Integration, "https://workspace.invalid/");
            Coordinator = new(Store, Browser, Integration, Tasks, Notes); Coordinator.BindAccount(Account);
        }
        public async Task Seed(CompanionAccountPreference entry) => await Storage.SetRawJsonAsync(StorageKeys.CompanionPreferences, Document(entry), default);
        public Task Boot(bool legacy = false, bool retry = false) => Coordinator.BootstrapAsync(new HabiticaCredentials(Account, "fixture-private-key"), legacy, retry, default);
        public void Dispose() => Http.Dispose();
        private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = JsonContent.Create(value) };
    }

    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(false,true)]
    public async Task Local_defaults_preserve_ambiguous_or_frozen_stores_without_credential_calls(bool multiple, bool frozen)
    {
        using var f = new Fixture(new(multiple || frozen ? null : Workspace, multiple, frozen));
        await f.Boot(); Assert.Empty(f.Calls);
        Assert.Equal(frozen ? CompanionConnectionState.LegacyRecoveryRequired : multiple ? CompanionConnectionState.ConsolidationRequired : CompanionConnectionState.Browser, f.Coordinator.State);
        Assert.Equal(multiple || frozen ? null : Workspace, f.Coordinator.ActiveWorkspaceId);
    }

    [Fact] public async Task Parallel_bootstrap_recovers_once_and_restores_both_exact_sources_without_consent_or_game_calls()
    {
        using var f = new Fixture(); await f.Seed(Entry());
        await Task.WhenAll(f.Boot(), f.Boot());
        Assert.Equal(4, f.Calls.Count); Assert.Equal(CompanionConnectionState.Cloud, f.Coordinator.State);
        Assert.True(f.Coordinator.TaskAccessVerified); Assert.True(f.Coordinator.NotesAccessVerified); Assert.Equal(Workspace, f.Coordinator.ActiveWorkspaceId);
        Assert.False(f.Tasks.ExecutorEnabled); Assert.Equal(0, f.Browser.Calls);
        Assert.All(f.Calls, path => Assert.True(path.EndsWith("recover") || path.EndsWith("/status")));
    }

    [Theory][InlineData("recovery_unavailable")][InlineData("existing_connection_revoked")]
    public async Task Unavailable_or_revoked_cloud_never_creates_a_writable_local_fork(string error)
    {
        using var f = new Fixture(); await f.Seed(Entry()); f.Error = error;
        await f.Boot(); Assert.Equal(CompanionConnectionState.CloudUnavailable, f.Coordinator.State); Assert.Equal(Workspace, f.Coordinator.ActiveWorkspaceId); Assert.Equal(0, f.Browser.Calls);
        if (error == "existing_connection_revoked")
        {
            Assert.Contains(Descriptor.ConnectionId, (await f.Store.ReadAsync(Account, default)).RevokedConnections);
            f.Coordinator.BindAccount(null); f.Coordinator.BindAccount(Account); await f.Boot(); Assert.Single(f.Calls);
        }
    }

    [Fact] public async Task Legacy_recovery_requires_explicit_action_and_retains_new_non_authorizing_metadata()
    {
        using var f = new Fixture(); await f.Boot(); Assert.Empty(f.Calls);
        await f.Boot(legacy: true); Assert.Equal(4, f.Calls.Count); Assert.True(f.Coordinator.CloudSelected);
        var saved = await f.Store.ReadAsync(Account, default); Assert.Equal(Descriptor, saved.Recovery); Assert.Equal(Workspace, saved.WorkspaceId); Assert.Equal(Notebook, saved.NotebookId);
        var json = await f.Storage.GetRawJsonAsync(StorageKeys.CompanionPreferences, default);
        Assert.DoesNotContain(new string('m',43), json!); Assert.DoesNotContain("fixture-private-key", json!);
    }

    [Fact] public async Task Delayed_recovery_is_discarded_after_account_switch()
    {
        using var f = new Fixture(); await f.Seed(Entry()); f.Delayed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = f.Boot(); Assert.Single(f.Calls);
        f.Coordinator.BindAccount(Other); f.Integration.ForgetSession(); f.Tasks.ForgetSession(); f.Notes.ForgetSession();
        f.Delayed.SetResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); await pending;
        Assert.Equal(Other, f.Coordinator.Account); Assert.False(f.Coordinator.CloudSelected); Assert.Null(f.Coordinator.IntegrationStatus); Assert.Null(f.Coordinator.ActiveWorkspaceId);
    }

    [Fact] public async Task Explicit_disconnect_suppresses_recovery_without_resetting_cloud_selection()
    {
        using var f = new Fixture(); await f.Seed(Entry()); await f.Boot();
        await f.Coordinator.SuppressAsync(Account, true, true, true, default);
        f.Coordinator.BindAccount(null); f.Coordinator.BindAccount(Account); f.Integration.ForgetSession(); f.Tasks.ForgetSession(); f.Notes.ForgetSession(); await f.Boot();
        Assert.Equal(4, f.Calls.Count); Assert.True(f.Coordinator.CloudSelected); Assert.Equal(CompanionConnectionState.CloudUnavailable, f.Coordinator.State);
    }
    [Fact] public async Task Initial_metadata_loading_does_not_create_a_writable_browser_fork_before_delayed_cloud_settings()
    {
        using var f=new Fixture();f.Coordinator.BeginStartup(Account);await f.Boot();Assert.Equal(CompanionConnectionState.Loading,f.Coordinator.State);Assert.Equal(0,f.Browser.Calls);
        await f.Seed(Entry());await f.Coordinator.CompleteStartupAsync(Account,default);Assert.True(f.Coordinator.CloudSelected);Assert.Equal(0,f.Browser.Calls);await f.Boot();Assert.Equal(CompanionConnectionState.Cloud,f.Coordinator.State);
    }
}
