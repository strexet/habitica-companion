# Diagnostics Navigation And Shared Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the current `Live Tests` surface into a first-class `Diagnostics` workspace with curated preset API inspection and a persistent shared diagnostics log that future features can reuse.

**Architecture:** Keep the current drawer-based Blazor shell. Add a storage-backed diagnostics journal in `Habitica.Storage`, extend application workflows to emit redacted diagnostics events, expose diagnostics state through `AppSessionController`, and evolve the current live-tests page into a broader diagnostics operator page that keeps the console visible beside the tools on wide screens.

**Tech Stack:** Blazor WebAssembly, MudBlazor, C#/.NET 8, IndexedDB via `Habitica.Storage`, xUnit, bUnit.

---

## File Structure

### Navigation and shell

- Modify: `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor`
  - Rename the drawer entry from `Live Tests` to `Diagnostics` and point it at `/diagnostics`.
- Modify: `src/Habitica.WebApp/Layout/MainLayout.razor`
  - Update the drawer hero copy so the app no longer describes itself as read-only-only.
- Modify: `tests/Habitica.WebApp.Tests/AppNavMenuTests.cs`
  - Lock the new diagnostics entry and the removal of the old `Live Tests` label.

### Shared diagnostics log

- Create: `src/Habitica.Domain/Diagnostics/DiagnosticsLogEntry.cs`
  - Shared log model and enums used by storage, workflows, controller state, and UI.
- Create: `src/Habitica.Storage/IDiagnosticsLogStore.cs`
  - Storage abstraction for recent diagnostics history.
- Create: `src/Habitica.Storage/DiagnosticsLogStore.cs`
  - IndexedDB-backed capped log history over the existing key-value adapter.
- Modify: `src/Habitica.Storage/StorageKeys.cs`
  - Add a dedicated diagnostics-log storage key.
- Modify: `tests/Habitica.Storage.Tests/StorageStoreTests.cs`
  - Add retention and clear-history tests.

### Diagnostics application workflows

- Create: `src/Habitica.Application/Diagnostics/DiagnosticsLogWriter.cs`
  - Small application service that writes redacted log entries with a consistent timestamp/id shape.
- Create: `src/Habitica.Application/Diagnostics/DiagnosticsPreset.cs`
  - Curated preset enum and result model for the diagnostics request runner.
- Create: `src/Habitica.Application/Diagnostics/DiagnosticsPresetWorkflow.cs`
  - Executes curated GET-style diagnostics presets sequentially and writes result logs.
- Modify: `src/Habitica.Application/Auth/LoginWorkflow.cs`
  - Emit success diagnostics events after sign-in sync completes.
- Modify: `src/Habitica.Application/Diagnostics/LiveTestWorkflow.cs`
  - Emit start/result/failure diagnostics events for safe tests and reversible gear tests.
- Create: `tests/Habitica.Application.Tests/Diagnostics/DiagnosticsPresetWorkflowTests.cs`
  - Lock preset behavior and preview shaping.
- Modify: `tests/Habitica.Application.Tests/Auth/LoginWorkflowTests.cs`
  - Lock sign-in success logging.
- Modify: `tests/Habitica.Application.Tests/Diagnostics/LiveTestWorkflowTests.cs`
  - Lock diagnostics log writes for safe and reversible flows.

### Web session/controller integration

- Modify: `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor`
  - Broaden diagnostics visibility once the session state can detect retained diagnostics history.
- Modify: `src/Habitica.WebApp/State/IAppSessionController.cs`
  - Add preset-run and clear-log actions.
- Modify: `src/Habitica.WebApp/State/SessionViewModel.cs`
  - Add diagnostics log state and derived counters.
- Modify: `src/Habitica.WebApp/State/AppSessionController.cs`
  - Load log history, route preset/test actions through shared state refresh, and log auth/refresh failures.
- Modify: `src/Habitica.WebApp/Program.cs`
  - Register diagnostics store/workflows/writer.
- Create: `tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs`
  - Lock log loading and post-action state refresh.
- Modify: `tests/Habitica.WebApp.Tests/FakeAppSessionController.cs`
  - Support the new controller methods and diagnostics state used by page tests.

### Diagnostics page and styling

- Modify: `src/Habitica.WebApp/Pages/LiveTestsPage.razor`
  - Keep the existing file for minimal churn, but add `@page "/diagnostics"` and evolve the page into the full diagnostics workspace.
- Modify: `src/Habitica.WebApp/wwwroot/css/app.css`
  - Add diagnostics layout, console, filter, and preview styles.
- Modify: `tests/Habitica.WebApp.Tests/Pages/LiveTestsPageTests.cs`
  - Lock the diagnostics layout, preset runner, and console rendering.

### Documentation

- Modify: `FEATURES.md`
  - Update app shell/navigation, diagnostics, live-test behavior, and shared logging details/status.
- Modify: `README.md`
  - Document the Diagnostics route and the shared diagnostics console.
- Modify if needed: `HABITICA_API.md`
  - Only if the curated preset runner needs extra API behavior notes beyond the existing docs.

## Task 1: Rename The Drawer Entry To Diagnostics

**Files:**
- Modify: `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor`
- Modify: `src/Habitica.WebApp/Layout/MainLayout.razor`
- Test: `tests/Habitica.WebApp.Tests/AppNavMenuTests.cs`

- [ ] **Step 1: Write the failing navigation test**

```csharp
[Fact]
public void Renders_diagnostics_link_instead_of_live_tests_for_authenticated_sessions()
{
    JSInterop.Mode = JSRuntimeMode.Loose;
    Services.AddMudServices();
    Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
        new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            DisplayName: "Mage Tester",
            ErrorMessage: null,
            LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
            TaskFreshness: SnapshotFreshnessState.Fresh,
            TaskSnapshot: null)));

    var cut = Render<AppNavMenu>();

    Assert.Contains("Diagnostics", cut.Markup);
    Assert.DoesNotContain("Live Tests", cut.Markup);
    Assert.Contains("/diagnostics", cut.Markup);
}
```

- [ ] **Step 2: Run the focused web tests and verify the new assertion fails**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~AppNavMenuTests'
```

Expected: FAIL because the menu still renders `Live Tests` and links to `/live-tests`.

- [ ] **Step 3: Implement the minimal navigation and shell copy change**

Update the authenticated nav link in `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor`:

```razor
@if (SessionController.State.IsAuthenticated)
{
    <MudNavLink Href="/diagnostics" Icon="@Icons.Material.Outlined.Science">
        Diagnostics
    </MudNavLink>
}
```

Update the shell copy in `src/Habitica.WebApp/Layout/MainLayout.razor`:

```razor
<p>Browse cached data, run diagnostics, and keep live mutations behind explicit workflow guardrails.</p>
```

Keep the rest of the drawer structure unchanged.

- [ ] **Step 4: Re-run the focused web tests and verify they pass**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~AppNavMenuTests'
```

Expected: PASS.

- [ ] **Step 5: Commit the navigation rename**

```bash
git add src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor src/Habitica.WebApp/Layout/MainLayout.razor tests/Habitica.WebApp.Tests/AppNavMenuTests.cs
git commit -m "Rename live tests navigation to diagnostics"
```

## Task 2: Add A Persistent Diagnostics Log Store

**Files:**
- Create: `src/Habitica.Domain/Diagnostics/DiagnosticsLogEntry.cs`
- Create: `src/Habitica.Storage/IDiagnosticsLogStore.cs`
- Create: `src/Habitica.Storage/DiagnosticsLogStore.cs`
- Modify: `src/Habitica.Storage/StorageKeys.cs`
- Test: `tests/Habitica.Storage.Tests/StorageStoreTests.cs`

- [ ] **Step 1: Write the failing storage tests**

Add these tests to `tests/Habitica.Storage.Tests/StorageStoreTests.cs`:

```csharp
[Fact]
public async Task DiagnosticsLogStore_prepends_new_entries_and_caps_history()
{
    var adapter = new InMemoryKeyValueStorage();
    var store = new DiagnosticsLogStore(adapter, maxEntries: 2);

    await store.AppendAsync(CreateLogEntry("one", DiagnosticsSeverity.Info), CancellationToken.None);
    await store.AppendAsync(CreateLogEntry("two", DiagnosticsSeverity.Warning), CancellationToken.None);
    await store.AppendAsync(CreateLogEntry("three", DiagnosticsSeverity.Error), CancellationToken.None);

    var entries = await store.GetRecentAsync(CancellationToken.None);

    Assert.Equal(new[] { "three", "two" }, entries.Select(entry => entry.Id));
}

[Fact]
public async Task DiagnosticsLogStore_clears_history()
{
    var adapter = new InMemoryKeyValueStorage();
    var store = new DiagnosticsLogStore(adapter);

    await store.AppendAsync(CreateLogEntry("one", DiagnosticsSeverity.Info), CancellationToken.None);
    await store.ClearAsync(CancellationToken.None);

    var entries = await store.GetRecentAsync(CancellationToken.None);

    Assert.Empty(entries);
}

private static DiagnosticsLogEntry CreateLogEntry(string id, DiagnosticsSeverity severity)
{
    return new DiagnosticsLogEntry(
        Id: id,
        OccurredAtUtc: DateTimeOffset.Parse("2026-04-27T10:00:00Z"),
        FeatureArea: DiagnosticsFeatureArea.Diagnostics,
        Operation: "test",
        Severity: severity,
        Mode: DiagnosticsMode.Local,
        Message: $"entry-{id}",
        Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["requestCount"] = "1"
        });
}
```

- [ ] **Step 2: Run the storage tests and verify they fail**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Storage.Tests/Habitica.Storage.Tests.csproj -m:1 -nodeReuse:false'
```

Expected: FAIL because the diagnostics log types and store do not exist yet.

- [ ] **Step 3: Implement the diagnostics log model and store**

Create `src/Habitica.Domain/Diagnostics/DiagnosticsLogEntry.cs`:

```csharp
namespace Habitica.Domain.Diagnostics;

public sealed record DiagnosticsLogEntry(
    string Id,
    DateTimeOffset OccurredAtUtc,
    DiagnosticsFeatureArea FeatureArea,
    string Operation,
    DiagnosticsSeverity Severity,
    DiagnosticsMode Mode,
    string Message,
    IReadOnlyDictionary<string, string> Metadata);

public enum DiagnosticsFeatureArea
{
    Auth,
    Sync,
    Tasks,
    Inventory,
    Party,
    Diagnostics,
    Equipment,
    Skills
}

public enum DiagnosticsSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public enum DiagnosticsMode
{
    Local,
    LiveRead,
    LiveMutation,
    ReversibleTest
}
```

Create `src/Habitica.Storage/IDiagnosticsLogStore.cs`:

```csharp
using Habitica.Domain.Diagnostics;

namespace Habitica.Storage;

public interface IDiagnosticsLogStore
{
    Task<IReadOnlyList<DiagnosticsLogEntry>> GetRecentAsync(CancellationToken cancellationToken);

    Task AppendAsync(DiagnosticsLogEntry entry, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
```

Create `src/Habitica.Storage/DiagnosticsLogStore.cs`:

```csharp
using Habitica.Domain.Diagnostics;

namespace Habitica.Storage;

public sealed class DiagnosticsLogStore : IDiagnosticsLogStore
{
    private readonly IKeyValueStorage _keyValueStorage;
    private readonly int _maxEntries;

    public DiagnosticsLogStore(IKeyValueStorage keyValueStorage, int maxEntries = 250)
    {
        _keyValueStorage = keyValueStorage;
        _maxEntries = maxEntries;
    }

    public async Task<IReadOnlyList<DiagnosticsLogEntry>> GetRecentAsync(CancellationToken cancellationToken)
    {
        return await _keyValueStorage.GetAsync<DiagnosticsLogEntry[]>(StorageKeys.DiagnosticsLogEntries, cancellationToken)
            ?? Array.Empty<DiagnosticsLogEntry>();
    }

    public async Task AppendAsync(DiagnosticsLogEntry entry, CancellationToken cancellationToken)
    {
        var entries = (await GetRecentAsync(cancellationToken)).ToList();
        entries.Insert(0, entry);

        if (entries.Count > _maxEntries)
        {
            entries.RemoveRange(_maxEntries, entries.Count - _maxEntries);
        }

        await _keyValueStorage.SetAsync(StorageKeys.DiagnosticsLogEntries, entries.ToArray(), cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.RemoveAsync(StorageKeys.DiagnosticsLogEntries, cancellationToken);
    }
}
```

Update `src/Habitica.Storage/StorageKeys.cs`:

```csharp
public const string DiagnosticsLogEntries = "diagnostics/logEntries";
```

- [ ] **Step 4: Re-run the storage tests and verify they pass**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Storage.Tests/Habitica.Storage.Tests.csproj -m:1 -nodeReuse:false'
```

Expected: PASS.

- [ ] **Step 5: Commit the diagnostics log store**

```bash
git add src/Habitica.Domain/Diagnostics/DiagnosticsLogEntry.cs src/Habitica.Storage/IDiagnosticsLogStore.cs src/Habitica.Storage/DiagnosticsLogStore.cs src/Habitica.Storage/StorageKeys.cs tests/Habitica.Storage.Tests/StorageStoreTests.cs
git commit -m "Add persistent diagnostics log store"
```

## Task 3: Add Diagnostics Logging And Curated Preset Workflows

**Files:**
- Create: `src/Habitica.Application/Diagnostics/DiagnosticsLogWriter.cs`
- Create: `src/Habitica.Application/Diagnostics/DiagnosticsPreset.cs`
- Create: `src/Habitica.Application/Diagnostics/DiagnosticsPresetWorkflow.cs`
- Modify: `src/Habitica.Application/Auth/LoginWorkflow.cs`
- Modify: `src/Habitica.Application/Diagnostics/LiveTestWorkflow.cs`
- Test: `tests/Habitica.Application.Tests/Auth/LoginWorkflowTests.cs`
- Test: `tests/Habitica.Application.Tests/Diagnostics/LiveTestWorkflowTests.cs`
- Test: `tests/Habitica.Application.Tests/Diagnostics/DiagnosticsPresetWorkflowTests.cs`

- [ ] **Step 1: Write the failing application tests**

Create `tests/Habitica.Application.Tests/Diagnostics/DiagnosticsPresetWorkflowTests.cs`:

```csharp
using Habitica.Api;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Storage;

namespace Habitica.Application.Tests.Diagnostics;

public sealed class DiagnosticsPresetWorkflowTests
{
    [Fact]
    public async Task RunAsync_returns_preview_and_writes_success_log()
    {
        var client = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot());
        var logStore = new FakeDiagnosticsLogStore();
        var workflow = new DiagnosticsPresetWorkflow(
            client,
            new DiagnosticsLogWriter(logStore, TimeProvider.System));

        var result = await workflow.RunAsync(
            new HabiticaCredentials("user-id", "api-token"),
            DiagnosticsPreset.UserAccount,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.RequestCount);
        Assert.Contains("\"displayName\"", result.ResponsePreview, StringComparison.Ordinal);
        Assert.Contains(logStore.Entries, entry =>
            entry.Operation == "preset-user-account"
            && entry.FeatureArea == DiagnosticsFeatureArea.Diagnostics
            && entry.Severity == DiagnosticsSeverity.Success);
    }
}
```

In the same file, add a minimal in-memory diagnostics log store so the assertions have a concrete backing implementation:

```csharp
private sealed class FakeDiagnosticsLogStore : IDiagnosticsLogStore
{
    public List<DiagnosticsLogEntry> Entries { get; } = new();

    public Task<IReadOnlyList<DiagnosticsLogEntry>> GetRecentAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<DiagnosticsLogEntry>>(Entries);
    }

    public Task AppendAsync(DiagnosticsLogEntry entry, CancellationToken cancellationToken)
    {
        Entries.Insert(0, entry);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        Entries.Clear();
        return Task.CompletedTask;
    }
}
```

Copy the existing `CreateUserSnapshot`, `CreateTaskSnapshot`, `CreatePartySnapshot`, and `FakeHabiticaSyncClient` helpers from `tests/Habitica.Application.Tests/Diagnostics/LiveTestWorkflowTests.cs` into the new preset test file so the test remains self-contained.

Extend `tests/Habitica.Application.Tests/Auth/LoginWorkflowTests.cs` with:

```csharp
Assert.Contains(logStore.Entries, entry =>
    entry.Operation == "sign-in"
    && entry.FeatureArea == DiagnosticsFeatureArea.Auth
    && entry.Severity == DiagnosticsSeverity.Success);
```

Extend `tests/Habitica.Application.Tests/Diagnostics/LiveTestWorkflowTests.cs` with:

```csharp
Assert.Contains(logStore.Entries, entry =>
    entry.Operation == "safe-live-tests"
    && entry.Severity == DiagnosticsSeverity.Success);
```

- [ ] **Step 2: Run the application tests and verify they fail**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Application.Tests/Habitica.Application.Tests.csproj -m:1 -nodeReuse:false'
```

Expected: FAIL because the writer, preset workflow, and log assertions are not implemented.

- [ ] **Step 3: Implement the diagnostics writer, preset workflow, and workflow logging**

Create `src/Habitica.Application/Diagnostics/DiagnosticsLogWriter.cs`:

```csharp
using Habitica.Domain.Diagnostics;
using Habitica.Storage;

namespace Habitica.Application.Diagnostics;

public sealed class DiagnosticsLogWriter
{
    private readonly IDiagnosticsLogStore _logStore;
    private readonly TimeProvider _timeProvider;

    public DiagnosticsLogWriter(IDiagnosticsLogStore logStore, TimeProvider timeProvider)
    {
        _logStore = logStore;
        _timeProvider = timeProvider;
    }

    public Task WriteAsync(
        DiagnosticsFeatureArea featureArea,
        string operation,
        DiagnosticsSeverity severity,
        DiagnosticsMode mode,
        string message,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        return _logStore.AppendAsync(
            new DiagnosticsLogEntry(
                Id: Guid.NewGuid().ToString("N"),
                OccurredAtUtc: _timeProvider.GetUtcNow(),
                FeatureArea: featureArea,
                Operation: operation,
                Severity: severity,
                Mode: mode,
                Message: message,
                Metadata: metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)),
            cancellationToken);
    }
}
```

Create `src/Habitica.Application/Diagnostics/DiagnosticsPreset.cs`:

```csharp
namespace Habitica.Application.Diagnostics;

public enum DiagnosticsPreset
{
    UserAccount,
    UserInventory,
    TasksUser,
    Party
}

public sealed record DiagnosticsPresetRunResult(
    DiagnosticsPreset Preset,
    bool Succeeded,
    int RequestCount,
    string Summary,
    string ResponsePreview);
```

Create `src/Habitica.Application/Diagnostics/DiagnosticsPresetWorkflow.cs`:

```csharp
using System.Text.Json;
using Habitica.Api;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;

namespace Habitica.Application.Diagnostics;

public sealed class DiagnosticsPresetWorkflow
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IHabiticaSyncClient _habiticaSyncClient;
    private readonly DiagnosticsLogWriter _logWriter;

    public DiagnosticsPresetWorkflow(
        IHabiticaSyncClient habiticaSyncClient,
        DiagnosticsLogWriter logWriter)
    {
        _habiticaSyncClient = habiticaSyncClient;
        _logWriter = logWriter;
    }

    public Task<DiagnosticsPresetRunResult> RunAsync(
        HabiticaCredentials credentials,
        DiagnosticsPreset preset,
        CancellationToken cancellationToken)
    {
        return preset switch
        {
            DiagnosticsPreset.UserAccount => RunUserAccountAsync(credentials, cancellationToken),
            DiagnosticsPreset.UserInventory => RunUserInventoryAsync(credentials, cancellationToken),
            DiagnosticsPreset.TasksUser => RunTasksAsync(credentials, cancellationToken),
            DiagnosticsPreset.Party => RunPartyAsync(credentials, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }

    private async Task<DiagnosticsPresetRunResult> RunUserAccountAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var snapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
        var preview = JsonSerializer.Serialize(new
        {
            snapshot.DisplayName,
            snapshot.ClassName,
            snapshot.Level,
            snapshot.Health,
            snapshot.MaxHealth,
            snapshot.Mana,
            snapshot.MaxMana,
            snapshot.Experience,
            snapshot.ToNextLevel,
            snapshot.Gold
        }, JsonOptions);

        await WriteSuccessLogAsync("preset-user-account", DiagnosticsPreset.UserAccount, cancellationToken);

        return new DiagnosticsPresetRunResult(
            DiagnosticsPreset.UserAccount,
            true,
            1,
            $"{snapshot.DisplayName} level {snapshot.Level} account snapshot loaded.",
            preview);
    }

    private async Task<DiagnosticsPresetRunResult> RunUserInventoryAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var snapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
        var preview = JsonSerializer.Serialize(new
        {
            snapshot.CurrentPetKey,
            snapshot.CurrentMountKey,
            ownedGearCount = snapshot.Inventory.OwnedGearKeys.Length,
            snapshot.Inventory.EggCount,
            snapshot.Inventory.FoodCount,
            snapshot.Inventory.HatchingPotionCount,
            snapshot.Inventory.QuestCount
        }, JsonOptions);

        await WriteSuccessLogAsync("preset-user-inventory", DiagnosticsPreset.UserInventory, cancellationToken);

        return new DiagnosticsPresetRunResult(
            DiagnosticsPreset.UserInventory,
            true,
            1,
            $"Inventory preset loaded with {snapshot.Inventory.OwnedGearKeys.Length} owned gear keys.",
            preview);
    }

    private async Task<DiagnosticsPresetRunResult> RunTasksAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var snapshot = await _habiticaSyncClient.GetTasksAsync(credentials, cancellationToken);
        var preview = JsonSerializer.Serialize(new
        {
            total = snapshot.Items.Count,
            open = snapshot.Items.Count(task => !task.IsCompleted),
            completed = snapshot.Items.Count(task => task.IsCompleted),
            sample = snapshot.Items.Take(5).Select(task => new
            {
                task.Id,
                task.Title,
                Type = task.Type.ToString(),
                task.IsCompleted
            })
        }, JsonOptions);

        await WriteSuccessLogAsync("preset-tasks-user", DiagnosticsPreset.TasksUser, cancellationToken);

        return new DiagnosticsPresetRunResult(
            DiagnosticsPreset.TasksUser,
            true,
            1,
            $"Tasks preset loaded {snapshot.Items.Count} tasks.",
            preview);
    }

    private async Task<DiagnosticsPresetRunResult> RunPartyAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var snapshot = await _habiticaSyncClient.GetPartySnapshotAsync(credentials, cancellationToken);
        var preview = JsonSerializer.Serialize(new
        {
            snapshot.Name,
            snapshot.MemberCount,
            snapshot.Summary,
            quest = snapshot.Quest is null ? null : new
            {
                snapshot.Quest.Key,
                snapshot.Quest.IsActive,
                snapshot.Quest.ProgressUp,
                snapshot.Quest.ProgressDown,
                snapshot.Quest.ParticipantCount
            }
        }, JsonOptions);

        await WriteSuccessLogAsync("preset-party", DiagnosticsPreset.Party, cancellationToken);

        return new DiagnosticsPresetRunResult(
            DiagnosticsPreset.Party,
            true,
            1,
            $"Party preset loaded {snapshot.Name}.",
            preview);
    }

    private Task WriteSuccessLogAsync(string operation, DiagnosticsPreset preset, CancellationToken cancellationToken)
    {
        return _logWriter.WriteAsync(
            DiagnosticsFeatureArea.Diagnostics,
            operation,
            DiagnosticsSeverity.Success,
            DiagnosticsMode.LiveRead,
            $"Fetched curated diagnostics preset {preset}.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestCount"] = "1",
                ["preset"] = preset.ToString()
            },
            cancellationToken);
    }
}
```

Modify `src/Habitica.Application/Auth/LoginWorkflow.cs` so the constructor takes `DiagnosticsLogWriter` and writes a success entry after snapshots are saved:

```csharp
await _diagnosticsLogWriter.WriteAsync(
    DiagnosticsFeatureArea.Auth,
    "sign-in",
    DiagnosticsSeverity.Success,
    DiagnosticsMode.LiveRead,
    $"Signed in and refreshed account snapshots for {user.DisplayName}.",
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["requestCount"] = string.IsNullOrWhiteSpace(user.PartyId) ? "2" : "3",
        ["taskCount"] = tasks.Items.Count.ToString(CultureInfo.InvariantCulture)
    },
    cancellationToken);
```

Modify `src/Habitica.Application/Diagnostics/LiveTestWorkflow.cs` so it takes `DiagnosticsLogWriter` and writes:

```csharp
await _logWriter.WriteAsync(
    DiagnosticsFeatureArea.Diagnostics,
    "safe-live-tests",
    DiagnosticsSeverity.Success,
    DiagnosticsMode.LiveRead,
    "Completed the safe diagnostics suite.",
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture),
        ["resultCount"] = results.Count.ToString(CultureInfo.InvariantCulture)
    },
    cancellationToken);
```

Also write a `reversible-gear-roundtrip` log entry with `DiagnosticsMode.ReversibleTest` after the restore path completes, and a warning entry when the test is skipped for missing alternate gear.

- [ ] **Step 4: Re-run the application diagnostics tests and verify they pass**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Application.Tests/Habitica.Application.Tests.csproj -m:1 -nodeReuse:false'
```

Expected: PASS.

- [ ] **Step 5: Commit the diagnostics workflow layer**

```bash
git add src/Habitica.Application/Diagnostics/DiagnosticsLogWriter.cs src/Habitica.Application/Diagnostics/DiagnosticsPreset.cs src/Habitica.Application/Diagnostics/DiagnosticsPresetWorkflow.cs src/Habitica.Application/Auth/LoginWorkflow.cs src/Habitica.Application/Diagnostics/LiveTestWorkflow.cs tests/Habitica.Application.Tests/Auth/LoginWorkflowTests.cs tests/Habitica.Application.Tests/Diagnostics/LiveTestWorkflowTests.cs tests/Habitica.Application.Tests/Diagnostics/DiagnosticsPresetWorkflowTests.cs
git commit -m "Add diagnostics logging workflows"
```

## Task 4: Expose Diagnostics State Through The Session Controller

**Files:**
- Modify: `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor`
- Modify: `src/Habitica.WebApp/State/IAppSessionController.cs`
- Modify: `src/Habitica.WebApp/State/SessionViewModel.cs`
- Modify: `src/Habitica.WebApp/State/AppSessionController.cs`
- Modify: `src/Habitica.WebApp/Program.cs`
- Test: `tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs`
- Modify: `tests/Habitica.WebApp.Tests/FakeAppSessionController.cs`

- [ ] **Step 1: Write the failing controller-state tests**

Create `tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs`:

```csharp
using Habitica.Api;
using Habitica.Application.Auth;
using Habitica.Application.Diagnostics;
using Habitica.Application.Sync;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Storage;
using Habitica.WebApp.State;

namespace Habitica.WebApp.Tests.State;

public sealed class AppSessionControllerTests
{
    [Fact]
    public async Task InitializeAsync_loads_cached_diagnostics_entries_into_state()
    {
        var logStore = new FakeDiagnosticsLogStore(new[]
        {
            new DiagnosticsLogEntry(
                "entry-1",
                DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                DiagnosticsFeatureArea.Diagnostics,
                "safe-live-tests",
                DiagnosticsSeverity.Warning,
                DiagnosticsMode.LiveRead,
                "warning",
                new Dictionary<string, string>())
        });

        var controller = CreateController(logStore: logStore);

        await controller.InitializeAsync();

        Assert.Single(controller.State.DiagnosticsLogEntries);
        Assert.Equal(1, controller.State.DiagnosticsWarningCount);
        Assert.True(controller.State.HasDiagnosticsHistory);
    }

    [Fact]
    public async Task ClearDiagnosticsLogsAsync_refreshes_state_after_clearing_store()
    {
        var logStore = new FakeDiagnosticsLogStore(new[]
        {
            new DiagnosticsLogEntry(
                "entry-1",
                DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                DiagnosticsFeatureArea.Diagnostics,
                "safe-live-tests",
                DiagnosticsSeverity.Info,
                DiagnosticsMode.LiveRead,
                "info",
                new Dictionary<string, string>())
        });

        var controller = CreateController(logStore: logStore);
        await controller.InitializeAsync();

        await controller.ClearDiagnosticsLogsAsync();

        Assert.Empty(controller.State.DiagnosticsLogEntries);
        Assert.Equal(0, controller.State.DiagnosticsWarningCount);
    }

    private static AppSessionController CreateController(FakeDiagnosticsLogStore logStore)
    {
        var syncClient = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot());
        var credentialStore = new FakeCredentialStore();
        var taskSnapshotStore = new FakeTaskSnapshotStore();
        var userSnapshotStore = new FakeUserSnapshotStore();
        var partySnapshotStore = new FakePartySnapshotStore();
        var logWriter = new DiagnosticsLogWriter(logStore, TimeProvider.System);

        return new AppSessionController(
            loginWorkflow: new LoginWorkflow(syncClient, credentialStore, taskSnapshotStore, userSnapshotStore, partySnapshotStore, logWriter),
            liveTestWorkflow: new LiveTestWorkflow(syncClient, userSnapshotStore, taskSnapshotStore, partySnapshotStore, logWriter, TimeProvider.System),
            diagnosticsPresetWorkflow: new DiagnosticsPresetWorkflow(syncClient, logWriter),
            credentialStore: credentialStore,
            partySnapshotStore: partySnapshotStore,
            taskSnapshotStore: taskSnapshotStore,
            userSnapshotStore: userSnapshotStore,
            diagnosticsLogStore: logStore,
            diagnosticsLogWriter: logWriter,
            snapshotFreshnessPolicy: new SnapshotFreshnessPolicy(),
            timeProvider: TimeProvider.System);
    }

    private sealed class FakeDiagnosticsLogStore : IDiagnosticsLogStore
    {
        private readonly List<DiagnosticsLogEntry> _entries;

        public FakeDiagnosticsLogStore(IEnumerable<DiagnosticsLogEntry> entries)
        {
            _entries = entries.ToList();
        }

        public Task<IReadOnlyList<DiagnosticsLogEntry>> GetRecentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<DiagnosticsLogEntry>>(_entries);
        }

        public Task AppendAsync(DiagnosticsLogEntry entry, CancellationToken cancellationToken)
        {
            _entries.Insert(0, entry);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            _entries.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialStore : ICredentialStore
    {
        public Task ClearPersistentCredentialsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<HabiticaCredentials?> GetPersistentCredentialsAsync(CancellationToken cancellationToken)
            => Task.FromResult<HabiticaCredentials?>(null);

        public Task SavePersistentCredentialsAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeTaskSnapshotStore : ITaskSnapshotStore
    {
        public TaskCollectionSnapshot? Snapshot { get; set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }

        public Task<TaskCollectionSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
            => Task.FromResult(Snapshot);

        public Task SaveAsync(TaskCollectionSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserSnapshotStore : IUserSnapshotStore
    {
        public UserSnapshot? Snapshot { get; set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }

        public Task<UserSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
            => Task.FromResult(Snapshot);

        public Task SaveAsync(UserSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePartySnapshotStore : IPartySnapshotStore
    {
        public PartySnapshot? Snapshot { get; set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }

        public Task<PartySnapshot?> GetLatestAsync(CancellationToken cancellationToken)
            => Task.FromResult(Snapshot);

        public Task SaveAsync(PartySnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHabiticaSyncClient : IHabiticaSyncClient
    {
        private readonly UserSnapshot _userSnapshot;
        private readonly TaskCollectionSnapshot _taskSnapshot;
        private readonly PartySnapshot _partySnapshot;

        public FakeHabiticaSyncClient(UserSnapshot userSnapshot, TaskCollectionSnapshot taskSnapshot, PartySnapshot partySnapshot)
        {
            _userSnapshot = userSnapshot;
            _taskSnapshot = taskSnapshot;
            _partySnapshot = partySnapshot;
        }

        public Task EquipGearAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<PartySnapshot> GetPartySnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(_partySnapshot);

        public Task<TaskCollectionSnapshot> GetTasksAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(_taskSnapshot);

        public Task<UserSummary> GetUserAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(new UserSummary(_userSnapshot.DisplayName, _userSnapshot.ClassName, _userSnapshot.Level));

        public Task<UserSnapshot> GetUserSnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(_userSnapshot);
    }

    private static UserSnapshot CreateUserSnapshot()
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            "Mage Tester",
            "wizard",
            15,
            42.5m,
            50m,
            33.5m,
            40m,
            125.1m,
            74.9m,
            88.25m,
            "party-123",
            "Wolf-Base",
            "Wolf-Base",
            new EquipmentSnapshot(
                new GearSlotsSnapshot("head_wizard_3", "armor_wizard_4", "weapon_wizard_5", "shield_wizard_2", "back_wizard_1"),
                new GearSlotsSnapshot("head_special_2", "armor_special_2", "weapon_special_2", "shield_special_2", "back_special_2")),
            new InventorySnapshot(1, 5, 1, 1, 1, 1, new[] { "weapon_wizard_5", "weapon_warrior_6" }));
    }

    private static TaskCollectionSnapshot CreateTaskSnapshot()
    {
        return new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            new[]
            {
                new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 1.5m, null, null)
            });
    }

    private static PartySnapshot CreatePartySnapshot()
    {
        return new PartySnapshot(
            DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            "party-123",
            "Night Owls",
            "Quest-focused party",
            4,
            new PartyQuestSnapshot("dragon", true, 12.5m, 3m, 2));
    }
}
```

- [ ] **Step 2: Run the focused web/controller tests and verify they fail**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~AppSessionControllerTests'
```

Expected: FAIL because the controller and session model do not expose diagnostics state yet.

- [ ] **Step 3: Implement controller methods, state, and registrations**

Update `src/Habitica.WebApp/State/IAppSessionController.cs`:

```csharp
Task<DiagnosticsPresetRunResult> RunDiagnosticsPresetAsync(DiagnosticsPreset preset, CancellationToken cancellationToken = default);

Task ClearDiagnosticsLogsAsync(CancellationToken cancellationToken = default);
```

Update `src/Habitica.WebApp/State/SessionViewModel.cs`:

```csharp
using Habitica.Domain.Diagnostics;

public sealed record SessionViewModel(
    bool IsBusy,
    bool IsAuthenticated,
    string? DisplayName,
    string? ErrorMessage,
    DateTimeOffset? LastSyncedAtUtc,
    SnapshotFreshnessState TaskFreshness,
    TaskCollectionSnapshot? TaskSnapshot,
    string? ClassName = null,
    int? Level = null,
    UserSnapshot? UserSnapshot = null,
    SnapshotFreshnessState UserFreshness = SnapshotFreshnessState.Missing,
    PartySnapshot? PartySnapshot = null,
    SnapshotFreshnessState PartyFreshness = SnapshotFreshnessState.Missing,
    IReadOnlyList<DiagnosticsLogEntry>? DiagnosticsLogEntries = null)
{
    public static SessionViewModel Empty { get; } = new(
        IsBusy: false,
        IsAuthenticated: false,
        DisplayName: null,
        ErrorMessage: null,
        LastSyncedAtUtc: null,
        TaskFreshness: SnapshotFreshnessState.Missing,
        TaskSnapshot: null,
        DiagnosticsLogEntries: Array.Empty<DiagnosticsLogEntry>());

    public bool HasDiagnosticsHistory => DiagnosticsLogEntries is { Count: > 0 };

    public int DiagnosticsWarningCount =>
        DiagnosticsLogEntries?.Count(entry => entry.Severity is DiagnosticsSeverity.Warning or DiagnosticsSeverity.Error) ?? 0;
}
```

Update the `AppSessionController` constructor to accept:

```csharp
DiagnosticsPresetWorkflow diagnosticsPresetWorkflow,
IDiagnosticsLogStore diagnosticsLogStore,
DiagnosticsLogWriter diagnosticsLogWriter,
```

Load diagnostics entries inside `LoadCachedStateAsync`:

```csharp
var diagnosticsLogEntries = await _diagnosticsLogStore.GetRecentAsync(cancellationToken);

SetState(State with
{
    DiagnosticsLogEntries = diagnosticsLogEntries,
    ...
});
```

Add the preset and clear-log methods:

```csharp
public async Task<DiagnosticsPresetRunResult> RunDiagnosticsPresetAsync(DiagnosticsPreset preset, CancellationToken cancellationToken = default)
{
    var credentials = await ResolveCredentialsAsync(cancellationToken);
    if (credentials is null)
    {
        var message = "Sign in is required before running diagnostics presets.";
        SetState(State with { ErrorMessage = message });
        return new DiagnosticsPresetRunResult(preset, false, 0, message, "{}");
    }

    SetState(State with { ErrorMessage = null, IsBusy = true });

    try
    {
        var result = await _diagnosticsPresetWorkflow.RunAsync(credentials, preset, cancellationToken);
        await LoadCachedStateAsync(cancellationToken);
        SetState(State with { ErrorMessage = null, IsBusy = false });
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
        SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
        return new DiagnosticsPresetRunResult(preset, false, 0, exception.Message, "{}");
    }
}

public async Task ClearDiagnosticsLogsAsync(CancellationToken cancellationToken = default)
{
    await _diagnosticsLogStore.ClearAsync(cancellationToken);
    await LoadCachedStateAsync(cancellationToken);
}
```

Register the new services in `src/Habitica.WebApp/Program.cs`:

```csharp
builder.Services.AddScoped<IDiagnosticsLogStore, DiagnosticsLogStore>();
builder.Services.AddScoped<DiagnosticsLogWriter>();
builder.Services.AddScoped<DiagnosticsPresetWorkflow>();
```

Now broaden the diagnostics drawer visibility in `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor`:

```razor
@if (SessionController.State.IsAuthenticated || SessionController.State.HasDiagnosticsHistory)
{
    <MudNavLink Href="/diagnostics" Icon="@Icons.Material.Outlined.Science">
        Diagnostics
    </MudNavLink>
}
```

Update `tests/Habitica.WebApp.Tests/FakeAppSessionController.cs` so it supports:

```csharp
public int DiagnosticsPresetCalls { get; private set; }
public DiagnosticsPresetRunResult? DiagnosticsPresetResult { get; set; }

public Task<DiagnosticsPresetRunResult> RunDiagnosticsPresetAsync(DiagnosticsPreset preset, CancellationToken cancellationToken = default)
{
    DiagnosticsPresetCalls++;
    return Task.FromResult(DiagnosticsPresetResult ?? new DiagnosticsPresetRunResult(preset, true, 0, string.Empty, "{}"));
}

public Task ClearDiagnosticsLogsAsync(CancellationToken cancellationToken = default)
{
    State = State with { DiagnosticsLogEntries = Array.Empty<DiagnosticsLogEntry>() };
    Changed?.Invoke();
    return Task.CompletedTask;
}
```

- [ ] **Step 4: Re-run the controller tests and verify they pass**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~AppSessionControllerTests'
```

Expected: PASS.

- [ ] **Step 5: Commit the controller integration**

```bash
git add src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor src/Habitica.WebApp/State/IAppSessionController.cs src/Habitica.WebApp/State/SessionViewModel.cs src/Habitica.WebApp/State/AppSessionController.cs src/Habitica.WebApp/Program.cs tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs tests/Habitica.WebApp.Tests/FakeAppSessionController.cs
git commit -m "Expose diagnostics state through session controller"
```

## Task 5: Turn The Live Tests Page Into The Diagnostics Workspace

**Files:**
- Modify: `src/Habitica.WebApp/Pages/LiveTestsPage.razor`
- Modify: `src/Habitica.WebApp/wwwroot/css/app.css`
- Test: `tests/Habitica.WebApp.Tests/Pages/LiveTestsPageTests.cs`

- [ ] **Step 1: Write the failing diagnostics-page tests**

Update `tests/Habitica.WebApp.Tests/Pages/LiveTestsPageTests.cs` with these expectations:

```csharp
[Fact]
public void Renders_diagnostics_sections_preset_runner_and_console()
{
    JSInterop.Mode = JSRuntimeMode.Loose;
    Services.AddMudServices();
    Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
        new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            DisplayName: "Mage Tester",
            ErrorMessage: null,
            LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            TaskFreshness: SnapshotFreshnessState.Fresh,
            TaskSnapshot: null,
            DiagnosticsLogEntries: new[]
            {
                new DiagnosticsLogEntry(
                    "entry-1",
                    DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                    DiagnosticsFeatureArea.Diagnostics,
                    "safe-live-tests",
                    DiagnosticsSeverity.Success,
                    DiagnosticsMode.LiveRead,
                    "Completed safe diagnostics suite.",
                    new Dictionary<string, string>())
            })));

    var cut = Render<LiveTestsPage>();

    Assert.Contains("Diagnostics", cut.Markup);
    Assert.Contains("Safe checks", cut.Markup);
    Assert.Contains("Guarded tests", cut.Markup);
    Assert.Contains("Preset API runner", cut.Markup);
    Assert.Contains("Shared console", cut.Markup);
    Assert.Contains("Run /user account preset", cut.Markup);
}

[Fact]
public void Diagnostics_preset_button_renders_the_returned_preview()
{
    JSInterop.Mode = JSRuntimeMode.Loose;
    Services.AddMudServices();
    var controller = new FakeAppSessionController(
        new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            DisplayName: "Mage Tester",
            ErrorMessage: null,
            LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            TaskFreshness: SnapshotFreshnessState.Fresh,
            TaskSnapshot: null))
    {
        DiagnosticsPresetResult = new DiagnosticsPresetRunResult(
            DiagnosticsPreset.UserAccount,
            true,
            1,
            "Mage Tester level 15 account snapshot loaded.",
            "{\n  \"displayName\": \"Mage Tester\"\n}")
    };
    Services.AddSingleton<IAppSessionController>(controller);

    var cut = Render<LiveTestsPage>();

    cut.Find("[data-testid='preset-user-account']").Click();

    Assert.Equal(1, controller.DiagnosticsPresetCalls);
    Assert.Contains("Mage Tester level 15 account snapshot loaded.", cut.Markup);
    Assert.Contains("\"displayName\": \"Mage Tester\"", cut.Markup);
}
```

- [ ] **Step 2: Run the page tests and verify they fail**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~LiveTestsPageTests'
```

Expected: FAIL because the page still renders the old live-test-only UI.

- [ ] **Step 3: Implement the diagnostics page, console, and styles**

Add a primary diagnostics route and keep the old route as a compatibility alias in `src/Habitica.WebApp/Pages/LiveTestsPage.razor`:

```razor
@page "/diagnostics"
@page "/live-tests"
@implements IDisposable
@inject IAppSessionController SessionController

<PageTitle>Diagnostics</PageTitle>

<div class="diagnostics-shell">
    <section class="card-surface dashboard-hero">
        <div>
            <p class="section-label">Diagnostics</p>
            <h1>Run controlled checks and inspect app behavior</h1>
            <p class="panel-copy">
                Use this workspace for safe checks, guarded live tests, curated API inspection, and shared app logs.
            </p>
        </div>
    </section>

    <section class="diagnostics-grid">
        <div class="diagnostics-main">
            <article class="card-surface diagnostics-panel">
                <p class="section-label">Overview</p>
                <h2>Session and snapshot state</h2>
                <span class="ui-pill">Warnings @SessionController.State.DiagnosticsWarningCount</span>
            </article>

            <article class="card-surface diagnostics-panel">
                <p class="section-label">Safe checks</p>
                <h2>Read-only live checks</h2>
                <MudButton Color="Color.Primary" Disabled="@(!SessionController.State.IsAuthenticated || SessionController.State.IsBusy)" OnClick="RunSafeLiveTestsAsync" Variant="Variant.Filled">
                    Run safe live tests
                </MudButton>
            </article>

            <article class="card-surface diagnostics-panel warning-card">
                <p class="section-label">Guarded tests</p>
                <h2>Reversible and destructive verification</h2>
                <MudButton Color="Color.Secondary" Disabled="@(!SessionController.State.IsAuthenticated || SessionController.State.IsBusy || !_acknowledgeGearMutation)" OnClick="RunReversibleGearTestAsync" Variant="Variant.Filled">
                    Run reversible gear test
                </MudButton>
            </article>

            <article class="card-surface diagnostics-panel">
                <p class="section-label">Preset API runner</p>
                <h2>Curated live inspection presets</h2>
                <div class="action-row">
                    <MudButton data-testid="preset-user-account" OnClick="@(() => RunPresetAsync(DiagnosticsPreset.UserAccount))" Variant="Variant.Filled">Run /user account preset</MudButton>
                    <MudButton OnClick="@(() => RunPresetAsync(DiagnosticsPreset.UserInventory))" Variant="Variant.Outlined">Run /user inventory preset</MudButton>
                    <MudButton OnClick="@(() => RunPresetAsync(DiagnosticsPreset.TasksUser))" Variant="Variant.Outlined">Run /tasks/user preset</MudButton>
                    <MudButton OnClick="@(() => RunPresetAsync(DiagnosticsPreset.Party))" Variant="Variant.Outlined">Run /groups/party preset</MudButton>
                </div>

                @if (_lastPresetResult is not null)
                {
                    <div class="diagnostics-preview">
                        <strong>@_lastPresetResult.Summary</strong>
                        <pre>@_lastPresetResult.ResponsePreview</pre>
                    </div>
                }
            </article>
        </div>

        <aside class="card-surface diagnostics-console">
            <div class="task-group-header">
                <div>
                    <p class="section-label">Shared console</p>
                    <h2>Recent diagnostics history</h2>
                </div>
                <MudButton Color="Color.Default" OnClick="ClearLogsAsync" Variant="Variant.Text">Clear logs</MudButton>
            </div>

            <div class="diagnostics-filter-row">
                <!-- MudSelect filters for feature area, severity, and mode -->
            </div>

            <div class="diagnostics-log-list">
                @foreach (var entry in FilteredEntries)
                {
                    <button class="diagnostics-log-item" @onclick="@(() => _selectedEntry = entry)">
                        <strong>@entry.Operation</strong>
                        <span>@entry.Message</span>
                    </button>
                }
            </div>
        </aside>
    </section>
</div>
```

Add matching diagnostics styles to `src/Habitica.WebApp/wwwroot/css/app.css`:

```css
.diagnostics-shell {
    display: grid;
    gap: 1.25rem;
}

.diagnostics-grid {
    display: grid;
    gap: 1rem;
    grid-template-columns: minmax(0, 1.5fr) minmax(20rem, 0.9fr);
    align-items: start;
}

.diagnostics-main,
.diagnostics-log-list {
    display: grid;
    gap: 1rem;
}

.diagnostics-panel,
.diagnostics-console {
    padding: 1.5rem;
}

.diagnostics-filter-row {
    display: grid;
    gap: 0.75rem;
    margin: 1rem 0;
}

.diagnostics-log-item {
    display: grid;
    gap: 0.35rem;
    text-align: left;
    padding: 0.85rem 1rem;
    border-radius: 1rem;
    border: 1px solid rgba(22, 36, 35, 0.12);
    background: rgba(255, 255, 255, 0.84);
}

@media (max-width: 960px) {
    .diagnostics-grid {
        grid-template-columns: 1fr;
    }
}
```

- [ ] **Step 4: Re-run the page tests and verify they pass**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~LiveTestsPageTests'
```

Expected: PASS.

- [ ] **Step 5: Commit the diagnostics workspace UI**

```bash
git add src/Habitica.WebApp/Pages/LiveTestsPage.razor src/Habitica.WebApp/wwwroot/css/app.css tests/Habitica.WebApp.Tests/Pages/LiveTestsPageTests.cs
git commit -m "Build diagnostics workspace page"
```

## Task 6: Update The Documentation And Run Full Verification

**Files:**
- Modify: `FEATURES.md`
- Modify: `README.md`
- Modify if needed: `HABITICA_API.md`

- [ ] **Step 1: Write the doc deltas into FEATURES and README**

Update `FEATURES.md` to:

```text
- rename the live-tests navigation surface to Diagnostics;
- document the Diagnostics page sections: overview, safe checks, guarded tests, preset API runner, shared console;
- mark the shared diagnostics log as implemented for auth/sync/diagnostics workflows;
- note that future mutation pages must emit into the shared diagnostics log instead of living on Diagnostics.
```

Update `README.md` to:

```text
- list /diagnostics as the operator workspace;
- describe curated diagnostics presets;
- describe the persistent shared log and clear-log action;
- keep live tests opt-in and sequential.
```

Only touch `HABITICA_API.md` if the preset runner needs new clarification beyond the existing `/user`, `/tasks/user`, and `/groups/party` notes.

- [ ] **Step 2: Run focused suites after docs-adjacent code is in place**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Application.Tests/Habitica.Application.Tests.csproj -m:1 -nodeReuse:false'
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Storage.Tests/Habitica.Storage.Tests.csproj -m:1 -nodeReuse:false'
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false'
```

Expected: PASS.

- [ ] **Step 3: Run the full solution tests**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test Habitica.sln -m:1 -nodeReuse:false'
```

Expected: PASS, with the existing note that `Habitica.Domain.Tests` and `Habitica.Rules.Tests` may still contain no tests.

- [ ] **Step 4: Run the full solution build**

Run:

```bash
/bin/zsh -c 'DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet build Habitica.sln -m:1 -nodeReuse:false'
```

Expected: PASS.

- [ ] **Step 5: Commit the docs and final verification state**

```bash
git add FEATURES.md README.md HABITICA_API.md
git commit -m "Document diagnostics workspace behavior"
```
