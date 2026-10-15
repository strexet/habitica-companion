# Technical Architecture

Last updated: 2026-06-09
Primary audience: AI agents and senior developers
Project type: third-party Habitica companion client
Primary Habitica integration reference: `skill-tree/documents/habitica-api.md`

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

When implementing or modifying the project, use this hierarchy:

1. `skill-tree/documents/rules.md` - repository workflow, AI-agent behavior rules, and required research.
2. `skill-tree/documents/habitica-api.md` - Habitica API usage, endpoints, headers, rate limits, security, and integration constraints.
3. `skill-tree/documents/technical.md` - technical stack, architecture, storage, deployment, and project-level constraints.
4. `skill-tree/documents/features.md` - implemented feature behavior, partial limitations, and feature-specific ownership.
5. Current code and tests - final verification source for what is actually implemented.
6. Official external documentation and source repositories for third-party behavior.
7. Community notes, forums, Reddit, and third-party tools.

If these sources conflict during implementation, prefer the higher-priority source and mention the conflict in the implementation notes or pull request summary.

For documentation audits and drift fixes, verify claims against the current codebase first, then against official Habitica server/mobile repositories and public API docs. Update or qualify stale documentation instead of treating older project docs as evidence that current code is wrong.

Document ownership:

```text
skill-tree/documents/rules.md                         agent workflow and research requirements
skill-tree/documents/technical.md                     foundational architecture and stack decisions
skill-tree/documents/features.md                      current feature implementation details
skill-tree/documents/future.md                        active future work queue
skill-tree/documents/ux-ui-manifest.md           UI/UX guidance and current page behavior
skill-tree/documents/habitica-api.md                  Habitica API contracts and safety rules
skill-tree/documents/habitica-tool-references.md      third-party Habitica tool observations
skill-tree/documents/cron.md                          Cron model and Cron-specific project rules
skill-tree/documents/deploy-cloudflare-pages.md  deployment and Cloudflare operations
```

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
.NET 8 LTS (`net8.0`)
Razor components
C#
```

Versioning rules:

- commit `global.json` with an exact .NET SDK version when the solution is scaffolded;
- do not use floating NuGet package versions in the current baseline;
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
- safe transient retry policy only when a feature explicitly implements and verifies it;
- request/response DTOs;
- API error normalization;
- redaction of credentials from logs.

Default behavior:

- require an explicit project-owned `Habitica:XClientHeader`; reject missing or blank configuration during API-client initialization without substituting an authenticated user identity;
- enforce a 350 ms minimum spacing between Habitica API requests unless an explicit `Habitica:MinRequestSpacingMilliseconds` configuration override is supplied;
- keep feature-specific preparation delays separate from API throttling so UI cancellation windows do not count as rate-limit spacing.

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

The initial local-only architecture has been extended by documented Cloudflare backend surfaces. Do not add another backend surface unless a documented feature requires it.

The baseline read/write Habitica architecture is:

```text
Browser PWA -> Habitica API v3 -> Local IndexedDB
```

Optional backend surfaces may be introduced only for documented product needs such as:

- team-shared analytics;
- cloud sync of app-specific presets;
- scheduled background jobs;
- push notifications;
- server-side data aggregation;
- shared party dashboards;
- central configuration;
- secure proxying with explicit credential-handling design.

If a backend is introduced, update this document before or during the implementation.

Current optional backend surfaces:

```text
Browser PWA -> Cloudflare Pages Function -> Cloudflare KV
Browser PWA -> Cloudflare Pages Function -> Cloudflare D1
```

The KV backend is only for encrypted app-data sync. Habitica API credentials must never be sent to the encrypted app-data sync endpoint. The browser derives a sync id and AES-GCM key from the active Habitica User ID and API Token, encrypts each section payload independently, and uploads only encrypted JSON.

Cloud sync uses per-section KV records (`sync:{syncId}:section:{sectionKey}`) instead of a single blob. Each section corresponds to one portable data key and has its own 2MB KV limit. A `sync-metadata` section tracks schema version 2, upload timestamp, and succeeded/failed sections. Legacy single-blob records (`sync:{syncId}`) are still readable for backward-compatible migration but are no longer written. The WebApp keeps per-section status records in session state with section key, direction, status, update time, payload size, and message; diagnostics is excluded from upload by default through `Features:CloudSyncExcludedSections`.

The D1 backend stores shared party data for CRON history and party quest planning under `functions/api/party-sync/[partyId].js`. It stores party snapshots, CRON events, quest pool availability, quest queue entries, quest votes, recently completed quest history, automatic completion detection keys, companion-app Owner and Officer roles, party-sync settings, active kick records, and optional tokenized invite-proof metadata. Selected queue entries are companion-app planning state; after a successful Habitica invite, the entry moves to `InviteSent` and is no longer shown in the queue/Next Quest planning views while the Habitica party quest invitation drives accept/reject/start behavior.

Party-sync uses browser-only `local-claim-v1` by default and as the owner/app-admin recovery path. Parties can optionally enable `tokenized-invite-v1`: the browser sends a locally stored proof id and token alongside the same local user identity fields, D1 stores only the SHA-256 token hash plus label/issuer/lifecycle metadata, and the Worker validates the proof before applying the existing owner/admin/Officer/kick checks. When mode is disabled or no active proof remains, members continue through `local-claim-v1`. The browser bridge clears a rejected local proof and retries once through local claims so recovery remains possible. The Cloudflare party-sync Function must never receive the Habitica API token.

Both provider boundaries must stay abstract so another remote sync provider can replace Cloudflare later.

## 5. Repository ownership

[repository-map.md](repository-map.md) owns physical paths and module responsibilities. Application source remains under `src/`, tests under `tests/`, Pages Functions under `functions/`, and canonical documentation under `skill-tree/documents/`. The manifest indexes documentation and workflow state; dedicated Auth/MCP Worker scaffolds reside under `workers/`. Protected operations remain denied until authorization is implemented.

`src/Habitica.WebApp/package.json` and `package-lock.json` pin browser dependencies. Dependency versions reside in [dependencies.md](dependencies.md); build/release steps reside in [build-and-release.md](build-and-release.md).

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

Follow `skill-tree/documents/habitica-api.md` first.

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

Dashboard CRON damage estimates keep numeric totals separate from explanatory notes. `PendingDamageEstimate` carries typed state for Daily damage, boss damage, included Daily count, unknown due-state count, missing task values, computed Constitution use, missing stat/catalog inputs, unavailable checklist reductions, boss unavailability, Inn pause, and compact diagnostics. UI and health-potion recovery decisions must read those typed fields instead of parsing user-facing strings.

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

Current default freshness categories:

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
- default to session-only mode;
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

## Optional ChatGPT integration proposal

Separate Auth/MCP Workers and a disabled-by-default Auth credential vault are implemented; OAuth linking and protected resource validation are implemented but disabled in shipped configurations. Local integration implementation follows the owner-approved [proposed ChatGPT enrollment, privacy and retention contract](companion-first-mcp-research.md#proposed-chatgpt-enrollment-privacy-and-retention-contract). Current credential storage remains browser-local; the proposal defines separate integration resources and a narrow internal operation boundary. The owner approved local implementation and tests. Enrollment and deployment remain disabled; current browser behavior sends no credentials to the integration backend.

## 10. Sync strategy

Use local-first snapshots with staged sign-in sync and page-prioritized user-initiated refresh.

Current implementation:

- sign-in validates credentials with a minimal `/user` fetch, stores the account snapshot, and makes the authenticated UI usable before loading non-critical domains;
- `RefreshCoordinator` runs independent refresh domains with visible/background priority and deduplicates concurrent same-domain requests;
- `RefreshForPageAsync` prioritizes domains needed by the current route, then refreshes other domains in the background priority group;
- shared party-sync merge/upload runs are serialized inside the session controller so background refresh and manual sync cannot interleave quest lifecycle reconciliation;
- successful refreshes, mutations, and persisted app-data preference changes attempt encrypted cloud sync and shared party sync without making the original Habitica action depend on remote-sync success;
- refresh diagnostics include domain, reason, priority, duration, deduplication, and error metadata;
- cloud sync diagnostics include direction, section status map, payload sizes, skipped/excluded/failed/conflicting sections, metadata upload status, and merge state.

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
8. Update sync metadata and per-section sync status.
9. Surface warnings and partial failures at the closest affected scope.
```

All sync and refresh results should report which freshness state each updated data category ended in. Cached data should remain visible and interactive during non-blocking background refresh or cloud sync; global busy state should be reserved for blocking mutations and first-load surfaces without usable cached data.

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

If a formula is uncertain or based on reverse-engineered behavior, mark the result with a warning and document the assumption in `skill-tree/documents/features.md`.

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
Cloudflare Pages for Blazor WebAssembly PWA
```

Current deployment contract:

- serve the app from the site root with `base href="/"`;
- require HTTPS;
- require SPA fallback to `index.html` for app routes;
- keep the service worker scope at the app root;
- deploy `functions/api/sync/[syncId].js`, `functions/api/sync/[syncId]/section/[sectionKey].js`, and `functions/api/sync/[syncId]/sections.js` as Cloudflare Pages Functions when encrypted cloud sync is enabled;
- configure a Cloudflare KV binding named `HABITICA_SYNC_KV` for encrypted sync payloads;
- treat subpath hosting as a non-baseline deployment that requires an explicit documented configuration change in the same change set.

Acceptable hosts:

- Cloudflare Pages;
- GitHub Pages;
- Azure Static Web Apps;
- any static hosting capable of serving the published Blazor assets correctly.

PWA offline behavior must be tested from a published build. Do not assume development builds represent offline behavior.

## 16. Technical decisions requiring skill-tree/documents/technical.md update

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
Target framework: .NET 8 LTS (`net8.0`)
Primary UI library: MudBlazor
Primary local browser storage: IndexedDB through a pinned Dexie.js interop boundary
Primary API target: Habitica API v3
Backend: Cloudflare Pages Functions with KV for encrypted app-data sync and D1 for shared party sync
Native shell: optional future .NET MAUI Blazor Hybrid
Domain logic: pure C# class libraries
Workflow orchestration: application/use-case layer, not UI components
Mutation model: validated, sequential, dry-run-first for multi-step actions
Sync model: user-initiated Habitica refresh plus automatic encrypted app-data and party-sync attempts after supported refreshes, mutations, and local preference saves
Freshness model: fresh/stale/expired/missing with mutation gating
Credential model: local-only, password-equivalent, session-only by default with explicit persistent opt-in
```

## Integration credential vault

The Auth Worker owns a dedicated `integration_profiles` table. Profile IDs derive from HMAC-SHA256 over the validated canonical Habitica User ID with `PROFILE_ID_KEY`; the HMAC key remains stable across credential rotation. AES-256-GCM envelopes bind profile ID, credential schema and key version in authenticated data. Random 96-bit IVs prevent nonce reuse; distinct `CREDENTIAL_KEY_V1` and `CREDENTIAL_KEY_V2` secrets provide versioned decryption independently of profile derivation. The current write version is configured by `CREDENTIAL_CURRENT_KEY_VERSION`.

`POST /enroll` requires explicit `integration-v1` consent, validates Habitica credentials before persistence, and rotates a 256-bit management credential whose hash alone is stored. Re-enrollment rotates the management credential and encrypted envelope while preserving the profile ID. Identical active credentials preserve the connection epoch only after fresh Habitica validation and constant-time credential comparison. Changed credentials, revoked profiles and deleted/recreated profiles receive a new epoch. Enrollment generation checks reject stale concurrent updates. Successful revalidation clears credential-repair state while retaining quota cooldown. The random connection epoch prevents obsolete authorization contexts from becoming valid after deletion and re-enrollment. Management authorization is scoped to one profile and uses constant-time hash comparison. Disconnect removes ciphertext and management credentials and marks the profile revoked. Profile deletion removes the row. OAuth validation enforces the current connection epoch and active profile on every request and refresh. Cleanup revokes grants for the disconnected epoch and deletes associated linking records. Access remains blocked when cleanup returns a retryable pending result.

Enrollment is disabled in every shipped Worker configuration. Local fixtures enable the route with synthetic credentials and mocked Habitica validation. Real enrollment requires separate authorization and user consent. The optional Settings enrollment surface requires a configured Auth URL and explicit consent. Shipped PWA configuration leaves the Auth URL empty. Integration management state remains tab-local and separate from portable app data.

## OAuth linking boundary

The Auth Worker owns single-use hashed 60-bit linking tickets, browser-bound two-stage consent, exact resource registration, PKCE S256, CIMD and DCR compatibility registration. Account labels and requested scopes appear before grant approval. OAuth identity equals the stable profile ID; opaque props contain only `profileId` and `connectionId`. Rate limits cover network, authorization session and profile ticket issuance. Conditional D1 updates prevent replay and concurrent ticket consumption.

The MCP Worker validates bearer authorization through the named Auth service before protected handling. Resource audience, profile status, connection epoch and minimum scopes must match. The Auth service returns safe authorization context without Habitica credentials. Both Worker `OAUTH_ENABLED` flags default to false. Four read-only bootstrap tools are implemented behind OAuth validation; mutation tools remain unavailable. Ticket expiry denies access immediately; stored expired metadata cleanup currently runs during subsequent requests or profile cleanup, so expiry alone does not establish physical retention bounds.

## MCP read execution

The MCP Worker creates a fresh SDK server and Web Standards Streamable HTTP transport per request, with no session store or background poll. The endpoint accepts bounded POST requests and exact allowed Origins; OAuth validation precedes transport handling. Each tool declares strict input/output schemas, read-only annotations and OAuth scope metadata. Scope challenges request only missing tool access. The locked SDK negotiates protocol versions through 2025-11-25; newer client compatibility requires rollout verification.

The Auth `AuthService` entrypoint owns a narrow `/read` operation. The request contains the bearer token, semantic tool name and validated arguments; Auth independently derives the profile and connection epoch from authorization. Only Auth decrypts Habitica credentials. Whitelisted GET routes retrieve full account stats or bounded task collections; transformed results exclude unrelated account fields. Shared read contracts prevent tool and Auth input drift. Integration read-state D1 rows persist per-connection cooldown and credential-repair markers; disconnect cleanup removes current connection state. Existing browser sync storage remains independent.

## Canonical Companion workspace domain

`Habitica.Domain.Workspace` owns schema-versioned Companion records independently of Habitica snapshots. Random GUID identities represent flat categories, projects, sections, labels, goals, tasks/subtasks, Daily occurrences, completion events, Habit score events and projection mappings. Tasks have one optional primary project and section; dependency and parent graphs must remain acyclic. Todo completion, Daily occurrence completion and repeated Habit scoring remain distinct from Habitica reward confirmation. Projection mappings carry separate external IDs and typed linkage/conflict/unknown state.

`WorkspaceRules` validates identities, revisions, relationships, lifecycle, bounded text, explicit recurrence data and UTC event timestamps. Iterative graph validation supports long imported dependency chains without recursive stack exhaustion. Goal progress counts completed eligible Todos; archived, cancelled and non-Todo records are excluded, and an empty eligible set yields unknown progress. `WorkspaceJson` preserves camel-case schema version 1 with named enums, rejects unsupported fields/versions, duplicate properties, numeric enums and invalid documents, and bounds imports. Existing Habitica `TaskSnapshot` and browser data remain unchanged; command and persistence adapters own subsequent adoption. The [workspace contract](companion-workspace-contract.md) owns the approved privacy and authority boundaries.

## Companion command journal core

`WorkspaceCommandEngine` defines typed create-Todo, partial-update, completion and bounded Habit-score commands. Commands carry independent IDs, workspace revision preconditions and whitelisted changes. Canonical changes, completion/score events, projection intent and acceptance evidence form one candidate journal snapshot. `WorkspaceCommandService` commits through the account-bound `ICompanionWorkspaceStore` compare/exchange port. Duplicate payloads return the original receipt; changed payloads cannot reuse a command ID. A lost commit acknowledgement reports unknown acceptance and permits safe lookup/resubmission with the original command ID.

Command payload identity uses SHA-256 over a versioned, fixed-order, UTF-8 length-prefixed field sequence. Explicit null and empty values remain distinct; C# and Worker fixtures share an Unicode-bearing request and expected digest. The Worker request parser rejects unrecognized fields, account selectors, coercion and invalid operation/patch combinations. Revisions remain inside the cross-runtime safe-integer range. Journal validation rejects corrupted hashes, identities, effect counts and execution state.

`ProjectionJournal` models pending, leased, executing, confirmed, blocked, cancelled and unknown outcomes. Undispatched expired leases return to pending; expired dispatched attempts become unknown. Unknown effects pause profile execution while canonical saves continue. Profile credential repair and rate-limit cooldown block other leases; per-task ordering prevents overtaking unresolved work. Known refused effects require a future retry deadline. Verified reconciliation records an evidence reference; ambiguous effects never become an automatic retry. Each acceptance/lease/dispatch/result/reconciliation transition appends a typed journal event. Concrete browser persistence and remote adapters remain separate dependencies; journal-core tests use an atomic synthetic store rather than claiming physical durability.

## Atomic local workspace storage

Dexie schema version 2 preserves the existing `kv` table and adds scoped canonical-workspace, command and journal-event tables. The `IndexedDbWorkspaceDatabase` factory binds each adapter to a local account scope. Canonical data and journal changes share one IndexedDB transaction and store-version compare/exchange. Stale writers return false; quota/transaction failures roll back every affected table. Accepted command identities and append-only event order remain immutable. Equivalent JSON property ordering does not change command identity. Local account scoping prevents accidental mixing; browser storage is not a remote authorization boundary.

`WorkspaceJournalJson`, journal validation and command identity hashing reside in the domain assembly so storage adapters enforce the same model constraints without an application/storage dependency cycle. Serialized journals are bounded to sixteen MiB. `WorkspacePortabilityService` preserves identities, records and command history; existing conflicting local state requires a conflict decision rather than silent replacement. Imported dispatched effects become unknown, and imported undispatched leases return to pending. Existing encrypted KV sync remains independent. Canonical Settings/export/clear controls and authenticated cloud routing remain integration work in the cloud-workspace dependency.

## Cloud workspace API and access epochs

The dedicated `workers/companion-workspace` Worker owns `WORKSPACE_DB`; the Auth Worker retains exclusive credential storage. The private `WorkspaceIdentityService` derives identity from a management bearer, verifies current credential epochs and validates MCP bearers without returning credentials. Public Auth routes never dispatch that service. `workspace-v1` consent is independent of OAuth linking and private encrypted sync.

Workspace sessions contain random, hashed-at-rest bearer tokens with eight-hour expiry and a maximum of eight simultaneous sessions per profile. Revalidating unchanged active credentials preserves healthy sessions. Disconnect, credential-context changes and deletion/recreation establish a new workspace access epoch. Session joins, command receipt reads, transaction preconditions, exports and cleanup bind to that epoch. Delayed requests cannot reach a replacement workspace even when the replacement canonical ID matches. A second session check follows the private Auth call; database write predicates also retain the epoch guard.

The command endpoint validates the shared canonical schema and portable command hash. A single D1 batch conditionally replaces the canonical document and appends command/event records. Fresh commit tags gate journal insertion after the store-version comparison; a failed append rolls back every statement. Duplicate IDs return the original receipt only for identical command payloads. Canonical saves do not call Habitica. Accepted projection effects remain pending until the separately enabled Workspace executor dispatches the accepted effects.

The initial service bounds canonical documents at one MiB and accepted complete archives at eight MiB, below the sixteen-MiB export/import boundary. Atomic archive accounting rejects new writes before history becomes unexportable; the service never truncates accepted commands or user completion history. The initial capacity bounds require a deliberate storage extension for larger workspaces. Disconnect retains cloud data; confirmed workspace deletion removes canonical data, command/event rows and workspace sessions. Credential deletion, browser data removal and Habitica effects have independent lifecycles. Every shipped workspace flag remains disabled and remote endpoints remain inert placeholders.

## Verified projection core

The Auth Worker's private `ProjectionService` accepts only bounded inspect, create-Todo, partial-update, single-score and reconciliation operations. Public routes do not expose the gateway. The Workspace Worker derives profile/credential context from the authenticated workspace session; callers cannot supply arbitrary upstream URLs, methods or headers. `PROJECTION_ENABLED` remains false in every shipped Auth configuration.

Fresh precondition receipts bind an owned task or verified alias absence to one profile and credential epoch. Receipts expire after ninety seconds and are consumed atomically with a durable dispatch reservation. A persisted per-profile lease prevents concurrent gateway sends. Automated calls also share a persisted thirty-second spacing gate. Quota and credential-repair state shares the existing Auth read-state boundary. Daily scoring additionally requires fresh server-evaluated due state, explicit Cron readiness and a matching Habitica day derived from verified account settings; unknown values block execution.

The projection adapter uses independent Companion IDs, reviewed adoption and fingerprints of managed upstream metadata. External deletion and metadata edits require review; the adapter never recreates a mapped task automatically. New command entries retain the task snapshot from acceptance. Later task edits cannot change older projection intent. Portable legacy entries without accepted snapshots remain readable, but projection requires reviewed historical intent. IndexedDB preserves immutable accepted snapshots across journal transitions and reload.

Auth stores attempt hashes and reduced outcomes before sending writes, without task text or credentials in attempt records. Matching attempts return durable receipts without another send. Ambiguous writes remain unknown and pause later gateway writes. Create reconciliation requires a matching owned correlation alias and desired metadata; update reconciliation requires matching desired fields. Absence after a timeout does not prove that an earlier request cannot finish. Habit scores lack a conclusive per-command upstream receipt and remain blocked for review after ambiguity. Reconciliation never accepts a caller's assertion that a score did or did not happen.

Auth migration `0005-projection-gateway.sql` adds temporary checks, pacing state and reduced attempt metadata. The scheduled hourly cleanup targets a thirty-day operational retention window; current credentials and canonical user command history have separate lifecycles. Credential disconnect/delete removes the affected projection context. The Workspace executor owns durable lease transitions and dispatch integration. Every shipped execution flag remains false.

## Workspace execution ownership

Workspace migration `0002-execution-ownership.sql` adds canonical authority, the per-profile execution lease, pacing deadline, original prepared request and handoff receipts. The runtime record contains no Habitica credential. Export includes authority and the retained journal while excluding private precondition receipts and runtime reservations. Every journal transition compares the current store version, access epoch and lease token; one D1 transaction appends the transition event, updates the entry/mapping and maintains archive accounting. A thirty-two-KiB headroom check precedes new dispatch so bounded result records fit without history truncation.

The scheduled handler selects one profile by persisted last-wake order. Each activation holds a maximum four-minute lease, a two-minute dispatch deadline, fifteen-second private-service deadlines and one-effect bound. The awaited pacing waits allow fresh account, due and write preconditions inside the ninety-second receipt validity window. Auth independently enforces thirty-second spacing and ten-second upstream deadlines. A restart never resends the saved prepared request. The Auth `lookup` operation returns a durable original receipt or unknown without calling Habitica; only a known receipt can advance recovery.

Local journal authority uses `local`, `frozen`, `cloud` and `paused`; legacy null authority means local. IndexedDB persists authority in the same transaction as canonical data and journal rows. `WorkspaceHandoffService` commits frozen authority before constructing an upload archive, rejects active local attempts, and retains one transfer ID across retries. Frozen/cloud browser copies cannot accept local canonical edits or acquire/dispatch projection leases. An acknowledged server transfer marks the local copy cloud-owned. Portable restore always pauses copied execution. A failed local freeze never yields an uploadable archive.

The handoff endpoint validates the portable schema and hashes, requires a matching frozen transfer, rejects nonempty or enabled cloud targets, and preserves independently owned workspace identity. Chunked JSON D1 inserts remain in one atomic batch with a transfer receipt. Duplicate transfer IDs require the original archive hash. The imported archive never grants executor consent. Unknown copied effects remain globally fenced even when no original gateway receipt can be recovered.
