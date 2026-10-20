using Habitica.Application.Integration;
using Habitica.Application.Workspace;
using Habitica.WebApp.Integration;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Habitica.Api;
using Habitica.Application.Auth;
using Habitica.Application.Dashboard;
using Habitica.Application.Diagnostics;
using Habitica.Application.Inventory;
using Habitica.Application.Sync;
using Habitica.Application.Tasks;
using Habitica.Rules.Spells;
using Habitica.Rules.Stats;
using Habitica.Storage;
using Habitica.WebApp;
using Habitica.WebApp.Sync;
using Habitica.WebApp.State;
using Habitica.WebApp.Theme;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddScoped<TimeProvider>(_ => TimeProvider.System);
builder.Services.AddScoped<IKeyValueStorage, IndexedDbStorageAdapter>();
builder.Services.AddScoped<ICredentialStore, CredentialStore>();
builder.Services.AddScoped<IDiagnosticsLogStore, DiagnosticsLogStore>();
builder.Services.AddScoped<IEquipmentPresetStore, EquipmentPresetStore>();
builder.Services.AddScoped<IGearCatalogStore, GearCatalogStore>();
builder.Services.AddScoped<IPartyCronHistoryStore, PartyCronHistoryStore>();
builder.Services.AddScoped<IPartySnapshotStore, PartySnapshotStore>();
builder.Services.AddScoped<ITaskSnapshotStore, TaskSnapshotStore>();
builder.Services.AddScoped<IUserSnapshotStore, UserSnapshotStore>();
builder.Services.AddScoped<SnapshotFreshnessPolicy>();
builder.Services.AddScoped<LocalUserDataPortabilityService>();
builder.Services.AddScoped<DiagnosticsLogWriter>();
builder.Services.AddScoped<DiagnosticsPresetWorkflow>();
builder.Services.AddScoped<PendingDamageEstimateFactory>();
builder.Services.AddScoped<HealthPotionRecoveryEstimateFactory>();
builder.Services.AddScoped<InventoryViewModelFactory>();
builder.Services.AddScoped<SpellViewModelFactory>();
builder.Services.AddScoped<CharacterStatsViewModelFactory>();
builder.Services.AddScoped<LiveTestWorkflow>();
builder.Services.AddScoped<TaskListViewModelFactory>();
builder.Services.AddScoped<TaskOrderPlanner>();
builder.Services.AddScoped<LoginWorkflow>();
builder.Services.AddScoped<RefreshCoordinator>();
builder.Services.AddScoped<ColorSchemeService>();
builder.Services.AddScoped<IRemoteUserDataSyncProvider, CloudflareUserDataSyncProvider>();
builder.Services.AddScoped<IRemotePartyDataSyncProvider, CloudflarePartyDataSyncProvider>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Features").Get<AppFeatureOptions>() ?? new AppFeatureOptions());
builder.Services.AddScoped<IndexedDbWorkspaceDatabase>();
builder.Services.AddScoped<WorkspaceDataController>();
builder.Services.AddScoped<WorkspaceTaskController>();
builder.Services.AddScoped<TodoWorkspacePresentation>();
builder.Services.AddScoped<ChatGptIntegrationClient>(_ => new ChatGptIntegrationClient(
    new HttpClient { Timeout = TimeSpan.FromSeconds(15) }, builder.Configuration["ChatGptIntegration:AuthBaseUrl"], builder.Configuration.GetValue("ChatGptIntegration:Enabled", false)));
builder.Services.AddScoped<IChatGptIntegrationClient>(sp => sp.GetRequiredService<ChatGptIntegrationClient>());
builder.Services.AddScoped<IWorkspaceCloudClient>(sp => new WorkspaceCloudClient(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }, sp.GetRequiredService<ChatGptIntegrationClient>(), builder.Configuration["CompanionWorkspace:BaseUrl"], builder.Configuration.GetValue<bool>("CompanionWorkspace:PartyPlanningEnabled"), sp.GetRequiredService<DiagnosticsLogWriter>()));
builder.Services.AddScoped<IWorkspaceOrganizationApi>(sp => (IWorkspaceOrganizationApi)sp.GetRequiredService<IWorkspaceCloudClient>());
builder.Services.AddScoped<IWorkspaceSavedTaskViewApi>(sp => (IWorkspaceSavedTaskViewApi)sp.GetRequiredService<IWorkspaceCloudClient>());
builder.Services.AddScoped<IAppSessionController, AppSessionController>();
builder.Services.AddScoped<IHabiticaSyncClient>(_ => new HabiticaApiClient(
    new HttpClient
    {
        BaseAddress = new Uri("https://habitica.com/api/v3/")
    },
    new HabiticaApiClientOptions(
        builder.Configuration["Habitica:XClientHeader"],
        MinRequestSpacingMilliseconds: builder.Configuration.GetValue("Habitica:MinRequestSpacingMilliseconds", 350),
        OwnerIsolatedTestingEnabled: builder.Configuration.GetValue("Features:OwnerIsolatedTestingEnabled", false))));

await builder.Build().RunAsync();
