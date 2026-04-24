# Initial Web App MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first usable Habitica companion web app with credential login, local-first storage, manual sync, app navigation, and read-only task views.

**Architecture:** Keep the documented layered structure from `TECHNICAL.md`: `Habitica.WebApp` calls `Habitica.Application`, which orchestrates `Habitica.Api`, `Habitica.Storage`, and `Habitica.Domain`. The MVP stores credentials locally behind a storage abstraction, syncs only user-initiated snapshots, and renders tasks from local read models with visible freshness state.

**Tech Stack:** Blazor WebAssembly PWA, .NET 8, MudBlazor, IndexedDB via Dexie.js JS module, xUnit, FluentAssertions, bUnit.

---

### Task 1: Scaffold Repository Baseline

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `Habitica.sln`
- Create: `src/Habitica.Application/`
- Create: `src/Habitica.Api/`
- Create: `src/Habitica.Domain/`
- Create: `src/Habitica.Rules/`
- Create: `src/Habitica.Storage/`
- Create: `src/Habitica.Shared/`
- Create: `src/Habitica.WebApp/`
- Create: `tests/Habitica.Application.Tests/`
- Create: `tests/Habitica.Api.Tests/`
- Create: `tests/Habitica.Storage.Tests/`
- Create: `tests/Habitica.WebApp.Tests/`

- [ ] Pin the installed SDK version in `global.json`.
- [ ] Scaffold the solution and projects with `dotnet new` commands targeting `net8.0`.
- [ ] Add package version management and shared build settings.
- [ ] Add project references to match the documented dependency direction.
- [ ] Run `dotnet restore Habitica.sln` and fix any baseline restore/build issues before moving on.

### Task 2: Add Domain Models and Application Tests First

**Files:**
- Create: `tests/Habitica.Application.Tests/Auth/LoginWorkflowTests.cs`
- Create: `tests/Habitica.Application.Tests/Tasks/TaskListViewModelFactoryTests.cs`
- Create: `tests/Habitica.Application.Tests/Sync/SnapshotFreshnessPolicyTests.cs`
- Create: `src/Habitica.Domain/...`
- Create: `src/Habitica.Application/...`

- [ ] Write failing tests for credential validation outcomes, task list grouping/filtering, and freshness classification.
- [ ] Run the targeted application tests and verify they fail for the missing behavior.
- [ ] Add the minimal domain records and application services needed to make those tests pass.
- [ ] Re-run the targeted tests, then the full `Habitica.Application.Tests` project.

### Task 3: Add Habitica API Client With Safe Auth Handling

**Files:**
- Create: `tests/Habitica.Api.Tests/HabiticaApiClientTests.cs`
- Create: `src/Habitica.Api/...`
- Modify: `src/Habitica.Application/...`

- [ ] Write failing API tests for authenticated request headers, response parsing for user/tasks sync, and error normalization.
- [ ] Run the API tests and verify the failures are caused by the missing client behavior.
- [ ] Implement the typed API client, DTO mapping, and auth-header injection with secret redaction.
- [ ] Re-run the API tests and then a solution build.

### Task 4: Add Storage Abstractions and Dexie Boundary

**Files:**
- Create: `tests/Habitica.Storage.Tests/CredentialStoreTests.cs`
- Create: `tests/Habitica.Storage.Tests/SnapshotMetadataTests.cs`
- Create: `src/Habitica.Storage/...`
- Create: `src/Habitica.WebApp/wwwroot/js/storage/indexedDbStorage.js`
- Create: `src/Habitica.WebApp/package.json`

- [ ] Write failing storage tests for session-vs-persistent credential decisions and task snapshot metadata persistence behavior.
- [ ] Run the targeted storage tests and verify they fail first.
- [ ] Implement the storage contracts, in-memory test doubles, and Dexie-backed JS module boundary for browser persistence.
- [ ] Pin the JS dependency versions and commit the lockfile after `npm install`.
- [ ] Re-run the storage tests.

### Task 5: Build the Web App Shell and Read-Only Tasks Experience

**Files:**
- Create: `tests/Habitica.WebApp.Tests/Pages/LoginPageTests.cs`
- Create: `tests/Habitica.WebApp.Tests/Pages/TasksPageTests.cs`
- Create: `tests/Habitica.WebApp.Tests/Layout/AppShellTests.cs`
- Create: `src/Habitica.WebApp/Components/...`
- Create: `src/Habitica.WebApp/Layout/...`
- Create: `src/Habitica.WebApp/Pages/...`
- Modify: `src/Habitica.WebApp/Program.cs`

- [ ] Write failing component tests for login form behavior, authenticated navigation visibility, and task list rendering from local snapshots.
- [ ] Run the targeted bUnit tests and verify they fail first.
- [ ] Implement MudBlazor theming, the app shell, login/logout flow, sync actions, freshness/status surfaces, and read-only task list/detail UI.
- [ ] Re-run the targeted web tests, then run the full solution test suite.

### Task 6: Update Product Documentation to Match Reality

**Files:**
- Modify: `README.md`
- Modify: `FEATURES.md`
- Modify: `TECHNICAL.md` if the implementation forces a real baseline change
- Modify: `.dual-graph-context/PROJECT_CONTEXT.md`
- Modify: `.dual-graph-context/SESSION_CONTEXT.md`

- [ ] Update `FEATURES.md` with baseline MVP features that were missing from the spec.
- [ ] Mark features as `implemented`, `partial`, `planned`, or `skipped` and describe what exists now, what is next, and what remains intentionally out of scope.
- [ ] Update `README.md` with exact setup, run, and testing commands for the scaffolded app.
- [ ] Update technical/context docs only where implementation changed source-of-truth behavior.

### Task 7: Verify and Review

**Files:**
- Modify: changed files only

- [ ] Run `npm --prefix src/Habitica.WebApp test` only if a JS test script exists; otherwise skip explicitly.
- [ ] Run `dotnet test Habitica.sln`.
- [ ] Run `dotnet build Habitica.sln`.
- [ ] Review only the modified files and the immediately affected code paths for real issues.
- [ ] If no issues are found in the changed files, report exactly `CHANGED FILES ARE OK.`
