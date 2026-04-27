using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Habitica.Api;
using Habitica.Application.Auth;
using Habitica.Application.Diagnostics;
using Habitica.Application.Inventory;
using Habitica.Application.Sync;
using Habitica.Application.Tasks;
using Habitica.Storage;
using Habitica.WebApp;
using Habitica.WebApp.State;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddScoped<TimeProvider>(_ => TimeProvider.System);
builder.Services.AddScoped<IKeyValueStorage, IndexedDbStorageAdapter>();
builder.Services.AddScoped<ICredentialStore, CredentialStore>();
builder.Services.AddScoped<IDiagnosticsLogStore, DiagnosticsLogStore>();
builder.Services.AddScoped<IPartySnapshotStore, PartySnapshotStore>();
builder.Services.AddScoped<ITaskSnapshotStore, TaskSnapshotStore>();
builder.Services.AddScoped<IUserSnapshotStore, UserSnapshotStore>();
builder.Services.AddScoped<SnapshotFreshnessPolicy>();
builder.Services.AddScoped<DiagnosticsLogWriter>();
builder.Services.AddScoped<DiagnosticsPresetWorkflow>();
builder.Services.AddScoped<InventoryViewModelFactory>();
builder.Services.AddScoped<LiveTestWorkflow>();
builder.Services.AddScoped<TaskListViewModelFactory>();
builder.Services.AddScoped<LoginWorkflow>();
builder.Services.AddScoped<IAppSessionController, AppSessionController>();
builder.Services.AddScoped<IHabiticaSyncClient>(_ => new HabiticaApiClient(
    new HttpClient
    {
        BaseAddress = new Uri("https://habitica.com/api/v3/")
    },
    new HabiticaApiClientOptions(builder.Configuration["Habitica:XClientHeader"])));

await builder.Build().RunAsync();
