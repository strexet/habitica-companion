# TECHNICAL.md

Last updated: 2026-04-24
Primary audience: AI agents and senior developers
Project type: third-party Habitica companion client
Primary Habitica integration reference: `HABITICA_API.md`

## 1. Purpose

This document defines the baseline technical stack, architecture, boundaries, and implementation rules for the project.

AI agents must treat this file as the source of truth for project-level technical decisions. If the technical stack, architecture, persistence strategy, API integration strategy, or other foundational technical decisions change, update this document in the same change set.

Do not change this document for local implementation details, small refactors, feature-specific code, UI copy, or isolated bug fixes.

## 2. Product summary

The project is a third-party Habitica client focused on local data analysis, optimization, automation planning, and batch execution through Habitica API v3.

The application must:

- run on as many devices as possible;
- work as an installable browser-based client where possible;
- use local-first data storage;
- avoid storing Habitica credentials outside the user's device unless explicitly required by a future architecture decision;
- expose rich data views for Habitica user, task, inventory, party, quest, and skill data;
- support deterministic calculations and explainable recommendations;
- execute mutating Habitica actions conservatively and sequentially.

## 3. Source-of-truth hierarchy

When implementing or modifying the project, use the following priority order:

1. `RULES.md` — repository workflow and AI-agent behavior rules.
2. `HABITICA_API.md` — Habitica API usage, endpoints, headers, rate limits, security, and integration constraints.
3. `TECHNICAL.md` — technical stack, architecture, storage, deployment, and project-level constraints.
4. `FEATURES.md` — feature specifications and domain behavior.
5. Current codebase.
6. Official external documentation.
7. Community notes, forums, Reddit, and third-party tools.

If these sources conflict, prefer the higher-priority source and mention the conflict in the implementation notes or pull request summary.

## 4. Baseline stack

### 4.1 Primary client

Use Blazor WebAssembly PWA as the primary client application.

Rationale:

- C# is the team's strongest language.
- The application is data-heavy and does not require Unity's rendering/game-loop model.
- The app should run through a browser on desktop, tablet, and mobile devices.
- PWA installation and offline startup are important product requirements.
- The same C# domain and rules libraries can later be reused by native shells.

Project:

```text
Habitica.WebApp
```

Technology:

```text
Blazor WebAssembly PWA
.NET 8 LTS for MVP (`net8.0`)
Razor components
C#
```

Versioning rules:

- commit `global.json` with an exact .NET SDK version when the solution is scaffolded;
- do not use floating NuGet package versions in the MVP baseline;
- if JavaScript dependencies are introduced, pin them exactly and commit the lockfile.

### 4.2 UI component stack

Use MudBlazor as the initial UI component library unless a future documented decision replaces it.

Expected UI needs:

- data tables;
- task filtering;
- inventory grids;
- equipment comparison;
- dialogs;
- forms;
- drawers;
- responsive layouts;
- dashboard cards;
- validation messages;
- dry-run previews;
- execution logs.

UI must remain thin. Do not place Habitica formulas, optimizer logic, macro planning, API semantics, or persistence decisions inside UI components.

### 4.3 Domain and rules

Use pure C# class libraries for domain and calculation logic.

Projects:

```text
Habitica.Domain
Habitica.Rules
```

Requirements:

- no UI dependency;
- no direct HTTP dependency;
- no direct IndexedDB dependency;
- deterministic calculations where possible;
- explicit input models;
- explicit output models;
- explanation data for user-facing recommendations;
- unit tests for all non-trivial formulas and optimization logic.

### 4.4 Application / use-case orchestration

Use a dedicated application layer for workflow orchestration.

Project:

```text
Habitica.Application
```

Responsibilities:

- sync orchestration;
- credential validation and clear-data workflows;
- snapshot freshness policy enforcement;
- read-model query orchestration for UI;
- dry-run compilation and validation flows;
- mutation planning and sequential execution coordination;
- execution-log coordination across API and storage;
- mapping application-level failures into user-visible result models.

Rules:

- `Habitica.WebApp` should call application services or query facades, not raw API/storage services;
- `Habitica.Application` may depend on `Habitica.Api`, `Habitica.Storage`, `Habitica.Rules`, and `Habitica.Domain`;
- `Habitica.Application` owns cross-cutting workflow rules such as stale-data gates, confirmation requirements, and mutation sequencing;
- `Habitica.Application` must not contain Habitica formulas that belong in `Habitica.Rules`.

### 4.5 Habitica API client

Use a dedicated C# API layer.

Project:

```text
Habitica.Api
```

Responsibilities:

- Habitica API v3 base URL management;
- authentication headers;
- required `x-client` header;
- typed endpoint clients;
- request throttling;
- `429 Too Many Requests` handling;
- `Retry-After` handling;
- rate-limit header tracking;
- retry policy for safe transient failures;
- request/response DTOs;
- API error normalization;
- redaction of credentials from logs.

Do not call Habitica API directly from UI components.

### 4.6 Local storage

Use local-first storage.

Primary browser storage:

```text
IndexedDB
```

Recommended access strategy:

```text
Blazor C# -> JS interop -> Dexie.js -> IndexedDB
```

Rationale:

- IndexedDB is browser-native storage for significant structured client-side data.
- It supports indexes and offline data access.
- It is better suited than Web Storage for task snapshots, inventory state, sync logs, macro definitions, and derived read models.

Project:

```text
Habitica.Storage
```

Responsibilities:

- local schema definitions;
- local migrations;
- snapshot persistence;
- read models;
- sync metadata;
- local user settings;
- macro and gear-set persistence;
- credential storage abstraction;
- data clearing/export support.

Do not store API credentials in calculation snapshots, execution logs, debug exports, analytics, or exception reports.

Browser storage contract:

- keep the raw Habitica API token in a dedicated credential store, separate from normal snapshot/read-model stores;
- keep redacted credential metadata such as validation state and last validated user in normal app stores only when needed;
- route all IndexedDB access through `Habitica.Storage`; no ad-hoc JS interop from UI components;
- own Dexie schema and migrations in one JS module boundary, for example `src/Habitica.WebApp/wwwroot/js/storage/indexedDbStorage.js` or an equivalent single-module path;
- pin Dexie and any supporting browser dependencies in `src/Habitica.WebApp/package.json` and commit `package-lock.json`;
- do not load Dexie or other production JS dependencies from a CDN;
- test the boundary at two levels: storage/domain tests for schema and serialization rules, and WebApp adapter tests for JS interop/error mapping.

### 4.7 Optional native shell

Native apps are not part of the initial baseline.

If native apps become required, prefer:

```text
.NET MAUI Blazor Hybrid
```

Expected reuse:

- Blazor components where practical;
- `Habitica.Api`;
- `Habitica.Domain`;
- `Habitica.Rules`;
- storage abstractions.

Expected native substitutions:

- SQLite instead of IndexedDB;
- platform secure storage instead of browser storage;
- platform-specific packaging and permissions.

Native shell work must not fork domain logic or Habitica API behavior.

### 4.8 Backend

Do not add a backend for the initial architecture.

The baseline architecture is:

```text
Browser PWA -> Habitica API v3 -> Local IndexedDB
```

A backend may be proposed only for documented product needs such as:

- team-shared analytics;
- cloud sync of app-specific presets;
- scheduled background jobs;
- push notifications;
- server-side data aggregation;
- shared party dashboards;
- central configuration;
- secure proxying with explicit credential-handling design.

If a backend is introduced, update this document before or during the implementation.

## 5. Repository structure

Use this structure unless the existing repository already has a compatible equivalent:

```text
/src
  /Habitica.Application
  /Habitica.WebApp
  /Habitica.Api
  /Habitica.Domain
  /Habitica.Rules
  /Habitica.Storage
  /Habitica.Shared
Habitica.sln
global.json
Directory.Build.props
Directory.Packages.props
.gitignore
/tests
  /Habitica.Application.Tests
  /Habitica.Api.Tests
  /Habitica.Domain.Tests
  /Habitica.Rules.Tests
  /Habitica.Storage.Tests
  /Habitica.WebApp.Tests
/docs
  HABITICA_API.md
  TECHNICAL.md
  FEATURES.md
  RULES.md
```

If the repository root contains these markdown files instead of `/docs`, keep all project-level documents together in the same location.

Within `src/Habitica.WebApp`, the MVP baseline should also commit:

```text
package.json
package-lock.json
```

If npm-based JS tooling is introduced, document the required Node LTS major in `package.json` and keep the app build reproducible without network-loaded runtime assets.

## 6. Core architecture

### 6.1 Layering

Use this dependency direction:

```text
Habitica.WebApp
  -> Habitica.Application

Habitica.Application
  -> Habitica.Api
  -> Habitica.Storage
  -> Habitica.Rules
  -> Habitica.Domain

Habitica.Rules
  -> Habitica.Domain

Habitica.Api
  -> Habitica.Domain

Habitica.Storage
  -> Habitica.Domain
```

Avoid reverse dependencies.

Rules:

- `Habitica.Domain` must not depend on API, storage, UI, or framework-specific services.
- `Habitica.Application` owns orchestration across API, storage, rules, and UI-facing workflows.
- `Habitica.Rules` must not perform HTTP requests.
- `Habitica.Api` must not write directly to UI state.
- `Habitica.WebApp` must not contain business-critical formulas or workflow orchestration.
- `Habitica.Storage` must expose abstractions that can later be backed by IndexedDB, SQLite, or another local store.

### 6.2 Data flow

Preferred read flow:

```text
Habitica API response
  -> API DTO
  -> domain model / normalized local model
  -> local snapshot
  -> derived read model
  -> application query/result
  -> UI
```

Preferred action flow:

```text
User intent
  -> application use case
  -> validation against local snapshot
  -> dry-run preview
  -> explicit user confirmation for mutating/batch actions
  -> sequential API execution
  -> response validation
  -> local state refresh/update
  -> execution log
```

Do not design mutating features as fire-and-forget API call batches.

## 7. Habitica API integration constraints

Follow `HABITICA_API.md` first.

Mandatory rules:

- use Habitica API v3;
- do not build against API v4;
- always send `x-api-user` for authenticated requests;
- always send `x-api-key` for authenticated requests;
- always send `x-client` for third-party API requests;
- respect rate-limit headers;
- respect `Retry-After` exactly;
- do not aggressively poll;
- avoid blind retries for non-idempotent operations;
- redact credentials from all logs and exports.

API client must expose enough metadata for UI and diagnostics:

```text
request id / local trace id
endpoint category
HTTP status
rate-limit remaining
rate-limit reset
retry-after seconds
redacted error body
local timestamp
```

Never expose raw credentials through diagnostic APIs.

## 8. Local data model principles

Use snapshot-based storage.

Store raw-enough synchronized data to allow recalculation when formulas or heuristics change. Store derived read models separately where useful for UI performance.

Recommended object categories:

```text
account_profile
user_snapshots
task_snapshots
party_snapshots
party_member_snapshots
inventory_snapshots
equipment_snapshots
quest_snapshots
sync_sessions
sync_errors
gear_sets
skill_macros
macro_execution_logs
sell_recommendation_runs
calculation_runs
app_settings
```

Every snapshot should include:

```text
localSnapshotId
habiticaUserId
source
schemaVersion
createdAtUtc
habiticaUpdatedAt when available
rawVersion or etag-like marker when available
```

Snapshot freshness model:

Every snapshot-backed feature must classify required data into one of these states:

```text
fresh
stale
expired
missing
```

Definitions:

- `fresh`: within the freshness window for the data category and not invalidated by a more recent local mutation;
- `stale`: older than the fresh window but still usable for read-only views or downgraded recommendations with visible warnings;
- `expired`: too old for mutation planning, destructive actions, or high-confidence recommendations;
- `missing`: no local snapshot is available.

MVP default freshness categories:

```text
volatile gameplay state (user stats, mana, tasks, inventory, equipment, quest state)
  fresh: <= 5 minutes
  stale: > 5 minutes and <= 60 minutes
  expired: > 60 minutes

party activity timing inputs
  fresh: <= 6 hours
  stale: > 6 hours and <= 72 hours
  expired: > 72 hours

reference metadata (gear/item/skill definitions)
  fresh until explicit invalidation, schema change, or app update
```

Freshness rules:

- mutating operations must require `fresh` snapshots for directly affected volatile entities;
- read-only features may use `stale` snapshots only with visible warnings and downgraded confidence;
- `expired` or `missing` data must block destructive actions and high-confidence dry-run plans;
- any successful mutating operation must immediately invalidate affected volatile snapshots until the app applies a local patch or refreshes the data.

## 9. Credential handling

Habitica API token must be treated as password-equivalent.

PWA credential policy:

- store only on the user's device;
- do not sync credentials;
- default to session-only mode for MVP;
- allow persistent storage only as an explicit user opt-in with a warning that browser runtime code cannot fully protect the token;
- use a dedicated credential store for the raw token and keep only redacted credential metadata in ordinary app stores;
- do not store tokens in cookies, URLs, calculation snapshots, execution logs, or telemetry payloads;
- provide distinct `logout` and `clear local data` actions:
  - `logout` clears the in-memory/session credential and resets authenticated UI state;
  - `clear local data` removes snapshots, derived stores, execution logs, and any persisted credential store entry;
- never include credentials in exported debug bundles;
- never send credentials to third-party analytics.

Native credential policy, if native shell is added:

- iOS/macOS: Keychain;
- Android: Keystore-backed secure storage;
- Windows: platform credential store or DPAPI-backed storage;
- Linux: platform-supported secret service where available, otherwise clearly document limitations.

## 10. Sync strategy

Start with manual sync plus user-initiated refresh.

Background sync may be added only if:

- it respects Habitica rate limits;
- it has explicit user-facing settings;
- it avoids continuous polling;
- it has termination conditions;
- it does not execute mutating actions without explicit user intent.

Recommended sync session steps:

```text
1. Validate credentials.
2. Fetch current user data.
3. Fetch tasks needed by active features.
4. Fetch party/group data if party features are enabled.
5. Normalize data.
6. Persist immutable snapshots.
7. Rebuild derived read models.
8. Update sync metadata.
9. Surface warnings and partial failures.
```

All sync and refresh results should report which freshness state each updated data category ended in.

## 11. Mutating operations

Mutating operations include, but are not limited to:

- casting skills;
- changing equipment;
- scoring tasks;
- selling inventory;
- buying items;
- joining/leaving groups;
- starting or changing quests;
- modifying tasks;
- running macros.

Requirements:

- validate before execution;
- show dry-run preview for multi-step operations;
- execute sequentially unless a documented endpoint is safe for batching;
- stop on unexpected state changes;
- update local state after each successful step or refresh after the sequence;
- persist an execution log;
- provide partial-success reporting;
- respect rate limits and `Retry-After`.

## 12. Calculation engine principles

All recommendations must be explainable.

Use result models that include:

```text
value
input snapshot references
calculation version
factors
warnings
confidence
assumptions
createdAtUtc
```

Avoid hidden heuristics in UI code.

If a formula is uncertain or based on reverse-engineered behavior, mark the result with a warning and document the assumption in `FEATURES.md`.

## 13. Testing strategy

Required tests:

- unit tests for formulas;
- unit tests for gear optimization;
- unit tests for macro validation;
- unit tests for sell recommendation logic;
- unit tests for freshness-policy decisions;
- unit tests for rate-limit handling;
- unit tests for API error normalization;
- storage migration tests;
- snapshot serialization tests;
- adapter tests for storage JS interop/error mapping;
- component tests for critical UI flows.

Recommended tools:

```text
xUnit
FluentAssertions
bUnit
Verify or equivalent snapshot testing
```

Do not require live Habitica credentials for normal test execution.

Live API tests must be opt-in and isolated from default CI.

## 14. Logging and diagnostics

Log technical events, not secrets.

Recommended diagnostic categories:

```text
ApiRequest
ApiRateLimit
ApiError
SyncSession
StorageMigration
CalculationRun
MacroDryRun
MacroExecution
CredentialStateChanged
```

Logs must redact:

- `x-api-key`;
- `x-api-user` when unnecessary;
- API token values;
- raw request headers;
- raw export payloads that may contain credentials.

## 15. Deployment

Primary deployment target:

```text
static web hosting for Blazor WebAssembly PWA
```

MVP deployment contract:

- serve the app from the site root with `base href="/"`;
- require HTTPS;
- require SPA fallback to `index.html` for app routes;
- keep the service worker scope at the app root;
- treat subpath hosting as a non-baseline deployment that requires an explicit documented configuration change in the same change set.

Acceptable hosts:

- Cloudflare Pages;
- GitHub Pages;
- Azure Static Web Apps;
- any static hosting capable of serving the published Blazor assets correctly.

PWA offline behavior must be tested from a published build. Do not assume development builds represent offline behavior.

## 16. Technical decisions requiring TECHNICAL.md update

Update this document when changing:

- primary client framework;
- .NET version policy;
- UI component library;
- local storage technology;
- API integration pattern;
- backend presence or absence;
- credential storage strategy;
- sync strategy;
- repository structure;
- architecture boundaries;
- deployment target;
- testing baseline;
- logging/security policy.

Do not update this document for:

- feature copy;
- individual endpoint additions that fit the existing API layer;
- small UI layout changes;
- bug fixes;
- non-foundational refactors;
- new calculations already covered by existing rules-engine principles.

## 17. Current technical decision summary

```text
Primary app: Blazor WebAssembly PWA
Application layer: Habitica.Application
Primary language: C#
MVP target framework: .NET 8 LTS (`net8.0`)
Primary UI library: MudBlazor
Primary local browser storage: IndexedDB through a pinned Dexie.js interop boundary
Primary API target: Habitica API v3
Backend: none for MVP
Native shell: optional future .NET MAUI Blazor Hybrid
Domain logic: pure C# class libraries
Workflow orchestration: application/use-case layer, not UI components
Mutation model: validated, sequential, dry-run-first for multi-step actions
Sync model: manual/user-initiated snapshot sync
Freshness model: fresh/stale/expired/missing with mutation gating
Credential model: local-only, password-equivalent, session-only by default with explicit persistent opt-in
```
