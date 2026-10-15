using System.Text.Json;
using Bunit;
using Habitica.Application.Sync;
using Habitica.Domain.Sync;
using Habitica.Storage;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Habitica.WebApp.Theme;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class SettingsPageTests : BunitContext
{
    public SettingsPageTests()
    {
        Services.AddSingleton<Habitica.Application.Workspace.IWorkspaceCloudClient>(new Habitica.WebApp.Integration.WorkspaceCloudClient(new HttpClient(), new Habitica.WebApp.Integration.ChatGptIntegrationClient(new HttpClient(), null), null));
        Services.AddSingleton(new IndexedDbWorkspaceDatabase(JSInterop.JSRuntime));
        Services.AddScoped<WorkspaceDataController>();
    }

    [Fact]
    public void ConnectedWorkspaceRenewalRequiresExplicitDataConsent()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage()); Services.AddScoped<ColorSchemeService>();
        var session = new FakeAppSessionController(SessionViewModel.Empty with { IsAuthenticated = true });
        Services.AddSingleton<IAppSessionController>(session); Services.AddSingleton<Habitica.Application.Workspace.IWorkspaceCloudClient>(new ExecutorCloud());
        var cut = Render<SettingsPage>();
        Assert.True(cut.Find("[data-testid='workspace-renew']").HasAttribute("disabled")); Assert.Equal(0, session.WorkspaceConnectCalls);
        Habitica.WebApp.Tests.Workspace.WorkspaceLayoutEvidence.Save("workspace-renewal", cut.Markup);
        cut.Find("[data-testid='workspace-renew-consent']").Change(true); cut.Find("[data-testid='workspace-renew']").Click();
        Assert.Equal(1, session.WorkspaceConnectCalls);
        Assert.True(cut.Find("[data-testid='workspace-renew']").HasAttribute("disabled"));
    }

    [Fact]
    public void Background_execution_requires_its_own_opt_in_and_can_be_paused()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty with { IsAuthenticated = true }));
        var cloud = new ExecutorCloud(); Services.AddSingleton<Habitica.Application.Workspace.IWorkspaceCloudClient>(cloud);
        var cut = Render<Habitica.WebApp.Components.WorkspaceSettingsPanel>();
        Assert.True(cut.Find("[data-testid='workspace-executor-enable']").HasAttribute("disabled")); Assert.Equal(0, cloud.Calls);
        cut.Find("[data-testid='workspace-executor-consent']").Change(true); cut.Find("[data-testid='workspace-executor-enable']").Click();
        Assert.Equal(1, cloud.Calls); Assert.True(cloud.ExecutorEnabled); Assert.Contains("while this tab is closed", cut.Markup);
        cut.Find("[data-testid='workspace-executor-pause']").Click(); Assert.False(cloud.ExecutorEnabled); Assert.Equal(2, cloud.Calls);
        Assert.True(cut.Find("[data-testid='workspace-executor-enable']").HasAttribute("disabled"));
    }
    private sealed class ExecutorCloud : Habitica.Application.Workspace.IWorkspaceCloudClient
    {
        public bool IsAvailable => true;
        public Guid? WorkspaceId => Guid.NewGuid();
        public bool ExecutorEnabled { get; private set; }
        public bool ExecutorAvailable => true;
        public int Calls { get; private set; }
        public Task<Habitica.Application.Workspace.WorkspaceActionResult> SetExecutorAsync(bool enabled, CancellationToken token) { Calls++; ExecutorEnabled = enabled; return Task.FromResult(new Habitica.Application.Workspace.WorkspaceActionResult(true, "Synthetic execution setting saved.")); }
        public Task<Habitica.Application.Workspace.WorkspaceActionResult> ConnectAsync(Habitica.Domain.Auth.HabiticaCredentials credentials, CancellationToken token) => throw new NotSupportedException();
        public Task<Habitica.Application.Workspace.WorkspaceActionResult> ExportAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<Habitica.Application.Workspace.WorkspaceActionResult> HandoffAsync(string archive, CancellationToken token) => throw new NotSupportedException();
        public Task<Habitica.Application.Workspace.WorkspaceActionResult> DisconnectAsync(bool delete, CancellationToken token) => throw new NotSupportedException();
        public Task<Habitica.Domain.Workspace.WorkspaceJournalSnapshot?> LoadWorkspaceAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<Habitica.Application.Workspace.WorkspaceTaskResponse<Habitica.Application.Workspace.WorkspaceCommandPreview>> PrepareCommandAsync(Guid requestId, Habitica.Domain.Workspace.WorkspaceCommand intent, CancellationToken token) => throw new NotSupportedException();
        public Task<Habitica.Application.Workspace.WorkspaceTaskResponse<Habitica.Application.Workspace.WorkspaceMutationReceipt>> AcceptCommandAsync(Habitica.Application.Workspace.WorkspaceCommandPreview preview, CancellationToken token) => throw new NotSupportedException();
        public Task<Habitica.Application.Workspace.WorkspaceTaskResponse<Habitica.Application.Workspace.WorkspaceMutationReceipt>> ReadCommandAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<Habitica.Application.Workspace.WorkspaceTaskResponse<Habitica.Application.Workspace.WorkspaceProjectionPreview>> PreviewLinkAsync(Guid taskId, Guid externalId, CancellationToken token) => throw new NotSupportedException();
        public Task<Habitica.Application.Workspace.WorkspaceActionResult> AdoptLinkAsync(Habitica.Application.Workspace.WorkspaceProjectionPreview preview, CancellationToken token) => throw new NotSupportedException();
        public void ForgetSession() { }
    }

    [Fact]
    public void Workspace_cloud_consent_is_separate_from_ChatGPT_and_private_sync()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(); Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage()); Services.AddScoped<ColorSchemeService>();
        var controller = new FakeAppSessionController(SessionViewModel.Empty with { IsAuthenticated = true });
        Services.AddSingleton<IAppSessionController>(controller);
        Services.AddSingleton<Habitica.Application.Workspace.IWorkspaceCloudClient>(new Habitica.WebApp.Integration.WorkspaceCloudClient(new HttpClient(), new Habitica.WebApp.Integration.ChatGptIntegrationClient(new HttpClient(), "https://auth.invalid/", false), "https://workspace.invalid/"));
        var cut = Render<SettingsPage>();
        Assert.True(cut.Find("[data-testid='workspace-connect']").HasAttribute("disabled"));
        Assert.NotNull(cut.Find("[data-testid='integration-unavailable']"));
        Assert.Equal(0, controller.WorkspaceConnectCalls);
        cut.Find("[data-testid='workspace-consent']").Change(true);
        cut.Find("[data-testid='workspace-connect']").Click();
        Assert.Equal(1, controller.WorkspaceConnectCalls); Assert.Equal(0, controller.ChatGptEnrollCalls);
        Assert.Contains("server can read", cut.Markup);
    }

    [Fact]
    public void Integration_enrollment_requires_consent_and_cleanup_requires_confirmation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var controller = new FakeAppSessionController(SessionViewModel.Empty with { IsAuthenticated = true })
        {
            IsChatGptIntegrationAvailable = true
        };
        Services.AddSingleton<IAppSessionController>(controller);
        var cut = Render<SettingsPage>();

        Assert.True(cut.Find("[data-testid='integration-enroll']").HasAttribute("disabled"));
        Assert.Equal(0, controller.ChatGptEnrollCalls);
        cut.Find("[data-testid='integration-consent']").Change(true);
        cut.Find("[data-testid='integration-enroll']").Click();
        Assert.Equal(1, controller.ChatGptEnrollCalls);
        Assert.Equal("0123456789AB", cut.Find("[data-testid='integration-link-code']").TextContent);
        cut.Find("[data-testid='integration-disconnect']").Click();
        Assert.Equal(0, controller.ChatGptDisconnectCalls);
        cut.Find("[data-testid='integration-cancel-cleanup']").Click();
        Assert.Equal(0, controller.ChatGptDisconnectCalls);
        cut.Find("[data-testid='integration-disconnect']").Click();
        cut.Find("[data-testid='integration-confirm-cleanup']").Click();
        Assert.Equal(1, controller.ChatGptDisconnectCalls);
        Assert.Empty(cut.FindAll("[data-testid='integration-link-code']"));
    }

    [Fact]
    public void Unavailable_integration_has_no_enrollment_control()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty));
        var cut = Render<SettingsPage>();
        Assert.NotNull(cut.Find("[data-testid='integration-unavailable']"));
        Assert.Empty(cut.FindAll("[data-testid='integration-enroll']"));
    }

    [Fact]
    public void Renders_export_import_and_cloud_sync_controls()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = new FakeKeyValueStorage();
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddScoped<ColorSchemeService>();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-05-13T02:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                CloudSyncSectionStatuses: new[]
                {
                    new CloudSyncSectionStatus(
                        CloudSyncSection.UserProfile,
                        "user-profile",
                        CloudSyncDirection.Upload,
                        CloudSyncSectionStatusKind.Succeeded,
                        DateTimeOffset.Parse("2026-05-13T02:00:00Z"),
                        128,
                        "Uploaded")
                },
                CloudSyncExcludedSections: new[] { CloudSyncSection.Diagnostics })));

        var cut = Render<SettingsPage>();

        Assert.Contains("Download backup", cut.Markup);
        Assert.Contains("Restore backup", cut.Markup);
        Assert.Contains("Private device sync", cut.Markup);
        Assert.Contains("Color scheme", cut.Markup);
        // Appearance controls are folded by default, matching the Dashboard; expand to reveal them.
        Assert.Empty(cut.FindAll("[data-testid='color-scheme-select']"));
        cut.Find("[data-testid='settings-appearance-toggle']").Click();
        Assert.Contains("Done", cut.Find("[data-testid='settings-appearance-toggle']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='color-scheme-select']"));
        Assert.NotNull(cut.Find("[data-testid='export-local-data']"));
        Assert.NotNull(cut.Find("[data-testid='import-local-data']"));
        Assert.NotNull(cut.Find("[data-testid='push-cloud-sync']"));
        Assert.NotNull(cut.Find("[data-testid='download-cloud-sync']"));
        Assert.Contains("Cloud sync sections", cut.Markup);
        Assert.Contains("User profile", cut.Markup);
        Assert.Contains("Diagnostics", cut.Markup);
        Assert.Contains("Excluded", cut.Markup);
    }

    [Fact]
    public async Task Color_scheme_controls_save_custom_scheme_preferences()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = new FakeKeyValueStorage();
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddScoped<ColorSchemeService>();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty));

        var cut = Render<SettingsPage>();

        cut.Find("[data-testid='settings-appearance-toggle']").Click();
        cut.Find("[data-testid='color-scheme-select']").Change("habitica");
        // Selecting a built-in scheme opens the editor directly as an unsaved custom copy.
        cut.Find("[data-testid='custom-scheme-name']").Change("Evening");
        cut.Find("[data-testid='save-custom-scheme']").Click();

        var preferences = await storage.GetAsync<ColorSchemePreferences>(StorageKeys.ColorSchemePreferences, CancellationToken.None);
        Assert.NotNull(preferences);
        var custom = Assert.Single(preferences!.CustomSchemes);
        Assert.Equal("Evening", custom.Name);
        // The custom copy keeps the source scheme's tokens; they are edited via the JSON copy/paste flow.
        Assert.Equal(ColorSchemeCatalog.Resolve("habitica", Array.Empty<ColorSchemeDefinition>()).Tokens.Primary, custom.Tokens.Primary);
        Assert.Equal(custom.Id, preferences.SelectedSchemeId);
        Assert.Contains("Saved Evening.", cut.Markup);
    }

    private sealed class FakeKeyValueStorage : IKeyValueStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.TryGetValue(key, out var json)
                ? JsonSerializer.Deserialize<TValue>(json, JsonOptions)
                : default);
        }

        public Task<string?> GetRawJsonAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.GetValueOrDefault(key));
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
        {
            _values[key] = JsonSerializer.Serialize(value, JsonOptions);
            return Task.CompletedTask;
        }

        public Task SetRawJsonAsync(string key, string jsonText, CancellationToken cancellationToken)
        {
            _values[key] = jsonText;
            return Task.CompletedTask;
        }
    }
}
