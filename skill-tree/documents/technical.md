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

`AdaptiveRequestPacer` accounts for the next foreground request and begins slowing below half of the estimated budget. Remaining/reset headers supply a forward-looking spacing estimate; token replenishment protects a reserve of up to two requests. A per-client dispatch gate covers HTTP completion and header reconciliation, preventing concurrent callers from spending the same estimate. Queued and cooling calls remain cancellable. Server reset and Retry-After deadlines form a hard cooldown independent of recovery.

Transport failures, timeouts, HTTP 429 and server failures double a bounded recovery factor; successful responses lower the factor by one eighth per response. Positive bounded jitter spreads later explicit calls. No failed operation is automatically replayed. `PacingStatus` contains only estimated capacity, reserve, recovery factor, cooldown and next delay. The client clock is injectable for synthetic qualification. Per-client estimates cannot coordinate independent browser devices or server instances.

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

The MCP Worker validates bearer authorization through the named Auth service before protected handling. Resource audience, profile status, connection epoch and minimum scopes must match. The Auth service returns safe authorization context without Habitica credentials. Both Worker `OAUTH_ENABLED` flags default to false. Authorization-code exchange requires the exact resource. Refresh requests may omit `resource` and inherit the grant-bound audience; explicit wrong resources remain denied. Temporary profile-storage failures return `temporarily_unavailable` without revoking the grant; inactive profiles and changed epochs remain terminal failures. Four legacy read tools and the canonical reviewed command tools are implemented behind OAuth validation. Ticket expiry denies access immediately; stored expired metadata cleanup currently runs during subsequent requests or profile cleanup, so expiry alone does not establish physical retention bounds.

## MCP read execution

The MCP Worker creates a fresh SDK server and Web Standards Streamable HTTP transport per request, with no session store or background poll. The endpoint accepts bounded POST requests and exact allowed Origins; OAuth validation precedes transport handling. The four bootstrap read tools declare strict input/output schemas, read-only annotations and OAuth scope metadata. Canonical task tools declare strict input schemas and accurate mutation annotations; all bootstrap and canonical scope declarations include the resource base scope and required tool scope. The locked SDK negotiates protocol versions through 2025-11-25; newer client compatibility requires rollout verification.

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

## Extended task command identity

Canonical tasks and partial patches carry nullable `habiticaDetails`; native priority remains a separate urgency enum. C# `HabiticaTaskDetails` validation and the shared Worker `task-details.ts` codec constrain nested values and checklist identities. Both command engines merge only specified detail fields into the current task and preserve the resulting immutable accepted snapshot. The projection adapter translates difficulty to the gateway `priority` field without translating Companion urgency.

`WorkspaceCommandIdentity` and shared `commandHash` retain the original version-one payload when details are absent. Version two appends nullable difficulty, attribute, checklist count and ordered item ID/text/completion fields to the length-prefixed UTF-8 payload. An empty checklist and an unspecified checklist have distinct identities. Existing receipts remain valid without rewriting history. IndexedDB normalizes newly introduced null detail properties only for legacy immutable-intent comparisons; real detail edits still fail the comparison.

## Native task creation and archival

Typed operations `createDaily`, `createHabit` and `deleteTask` extend both command engines without changing existing operation values or v1/v2 payload identities. Creation allocates independent task identities and stores nullable recurrence; an unscheduled Daily still requires an explicit occurrence date for completion. Native task deletion uses the existing archive flag and preserves referenced records, immutable snapshots and command receipts. Active reads exclude archived tasks. The new operations reject projection requests and linked-task deletion before acceptance. The same transaction, revision checks, expiry and access epochs apply to all task operations; no database migration is required.

## Shared reviewed task surface

Workspace migration `0003-prepared-commands.sql` stores profile/request-to-command bindings, access epoch, original input hash, command payload, correlation ID and review expiry. Preparation mutates storage metadata without accepting canonical effects. The preparation transaction increments store version and archive accounting, preventing concurrent canonical acceptance from overwriting preview accounting. Command acceptance includes a database-time expiry predicate and the prepared access epoch in the same transaction as canonical data, journal entry and event insertion. Repeated command IDs return the retained receipt before revision checks.

The named Workspace `WorkspaceCommandService` exposes a private tool handler to the MCP Worker. The service derives profile context from Auth's exact-audience bearer validation and loads an active consented workspace for the matching connection. `/task-api` supplies the browser-session path into the same `taskApi` functions. Both paths call the existing canonical command engine; neither path sends Habitica writes. The Auth gateway and opted-in executor retain exclusive projection responsibility. MCP has only Auth and Workspace service bindings.

`WorkspaceTaskController` binds browser storage to the current account, invokes the application command service for local saves, and uses the typed cloud client for server previews and acceptance. The PWA retains original IDs through uncertainty, discards responses from a changed account/connection and blocks edits after failed refresh. `WorkspaceTaskApiJson` preserves string enums and rejects unknown response fields. Receipt validation rejects impossible effect counts, mismatched identities and inconsistent unknown-state flags.

Reviewed adoption adds expected workspace ID and access epoch to the existing revision/fingerprint preconditions. Fresh server observation still verifies external ownership and metadata. Linking preserves canonical fields and does not dispatch upstream effects. Expired unaccepted preview cleanup processes at most thirty-two profiles per activation, excludes accepted commands, and adjusts archive accounting atomically. The scheduled handler then retains the executor's existing one-profile, one-effect bound.

The reviewed [MCP tools specification](https://modelcontextprotocol.io/specification/2025-11-25/server/tools) and [OpenAI MCP guide](https://developers.openai.com/api/docs/guides/tools-connectors-mcp) describe transport calls, schemas and tool approval. The reviewed contracts provide no Companion durable mutation key. The implementation therefore treats application request/command handles as the replay boundary and makes no assumption about transport retry identity or exactly-once client delivery.

## Workflow package boundary

`plugin/plugin.json` and `plugin/mcp.json` use Agent Plugins 1.0.0; fixed `skills/` paths expose three focused workflows. Official OpenAI packaging and skill guidance was verified on 2026-10-15. No compatibility overlay or universal host-support guarantee is present. The committed `.invalid` endpoint keeps source packaging separate from approved deployment configuration. OAuth is negotiated by the MCP host; credentials never enter package headers or skill content.

`workers/companion-workspace/test/plugin.test.mjs` validates exact upstream schema fixtures and routes declared MCP transport through the actual SDK to isolated Auth/MCP/Workspace services. The fixture verifies two profiles, denied scope and missing mutation input. Manual host/model activation remains distinct from deterministic routing checks. Package setup and source references reside in `plugin/README.md`.

## Verified party proof

Auth `PartyService` is a named internal entrypoint bound only to Workspace through `AUTH_PARTY`. Public Auth routes never dispatch the service. OAuth-origin proof requests validate the original bearer grant and match derived profile/connection identity; session-origin requests can use only the trusted Workspace binding. Fresh bounded user/group reads produce a thirty-second internal proof after a final active-enrollment check. No proof, raw upstream body or credentials are persisted.

MCP `get_party_context` routes through `WorkspaceCommandService`, which independently validates `profile:read`, `party:read` and active cloud workspace identity. Auth repeats the OAuth checks before upstream access. The public projection omits internal actor/leader identifiers and private group fields. Separate `PARTY_ENABLED` flags in Auth and Workspace remain false in local, preview and production configuration. Authenticated planning persistence is described below. Reviewed spell execution is described below.

Party proof delivery also rechecks the exact active Workspace connection and access epoch after the private Auth read. Disconnect, deletion and replacement discard late party facts even when vault enrollment remains active. The isolated lifecycle fixture verifies normal reads before each invalidation.

## Authenticated party planning persistence

Workspace migration `0004-authenticated-party-planning.sql` owns separate authenticated plan, consent and typed command tables. The task-only portable journal remains unchanged. A party plan stores a bounded JSON queue/vote document, namespace UUID, revision and commit tag. A single D1 batch conditionally updates the document and inserts the durable actor-owned receipt. Acceptance predicates include current workspace connection/access epoch, namespace-bound consent, revision, proof expiry, command uniqueness and per-profile receipt capacity. Failed CAS returns the original matching receipt or an explicit conflict.

Own contribution removal uses a transaction guard to roll the complete batch back if any affected namespace revision changed. Export/removal resolve management identity independently of current party membership. State bounds are 100 queued entries, 500 votes, 64 retained party namespaces per contributor and 1000 retained receipts per profile; exceeding limits requires explicit cleanup rather than silent eviction. Namespace deletion cascades consent and receipts. No legacy party database binding exists on Workspace, Auth or MCP.

The private Auth proof loads only the actor's quest inventory for planning-state discovery and queue ownership verification. PWA settings and MCP tools share the Workspace authority; session-origin calls derive identity from workspace access, while OAuth-origin calls additionally enforce tool scopes at MCP, Workspace and Auth. Party-specific transport failures preserve the original planning command ID for receipt recovery. PWA consent binds the reviewed party ID; namespace/revision checks bind leader settings and deletion. Contribution exports serialize typed known fields and exclude unknown upstream properties.

## Private spell dispatch boundary

Auth exports `SpellService` as a named private entrypoint with no public URL routing. The service independently validates the original OAuth bearer and exact profile/connection, derives credentials from the vault and shares `integration_projection_state` and `integration_projection_attempts` with task projection. No new Auth database schema is needed: the existing operation discriminator accepts spell attempts. Production MCP has no direct binding to this entrypoint; Workspace alone owns the configured production dispatch binding; synthetic fixtures additionally bind the entrypoint for isolation tests.

A unique lease nonce includes the attempt ID for same-intent concurrency detection. A competing copy returns uncertainty without releasing the original lease. Reservation requires the owned, unexpired lease, an eleven-second HTTP reserve and no unresolved gameplay receipt. Terminal outcomes cannot be overwritten by a late owner or refusal. Pre-dispatch failure recording inserts only absent receipts; post-dispatch recording updates only dispatched/unknown attempts. Missing receipts are uncertainty, never permission to repeat a cast. Shared `rateState` can distinguish semantic spell refusals from credential failures.

The gateway hashes normalized persisted account fields and minimal verified party facts. Derived UserGet-only fields do not spuriously invalidate the next successful cast response; missing critical fields fail safely. The gateway returns no copied healing formula or full member state. `SPELLS_ENABLED` remains false in every Auth environment. The Workspace reviewed-action ledger preserves unknown-effect fencing beyond Auth operational retention.

## Workspace reviewed spell ledger

Migration `0005-reviewed-spell-actions.sql` adds a typed action table with profile/action and profile/request uniqueness, input/precondition payload, connection/access epoch, authenticated namespace, token hash, expiry, single-use consumption, versioned normalized entry and an optional saved dispatch. Accepted private history cascades only with explicit Workspace deletion. Namespace deletion or contributor opt-out deletes unconsumed previews. No raw OAuth token, Habitica credential or full member snapshot enters this ledger. The separate history projection excludes execution tokens, hashes and private dispatch payloads; task journal serialization remains unchanged.

Preparation validates fresh Auth context and planning consent before recording a two-minute preview. Token rotation uses action version checks. Execution acquires `workspace_runtime` without overwriting task prepared-request fields, checks unresolved task/spell records and atomically consumes the token. Every single-cast dispatch requires the held lease, current Workspace access and matching planning namespace/consent. Known results update the versioned entry. Interrupted dispatches remain fenced; recovery performs Auth lookup only and stops any unexecuted remainder. Concurrent recovery updates use version and lease predicates.

The foreground loop reserves thirty-five seconds per private call within a fifty-five-second deadline and holds a ninety-second runtime lease. The MCP forwarding deadline accommodates bounded execution; a host timeout still requires original-receipt recovery. Final refresh occurs only when the deadline reserve permits, and refresh failure is separate from confirmed effects. The background task executor refuses retained unresolved spell records regardless of Auth's thirty-day operational retention. Auth and Workspace `SPELLS_ENABLED` flags remain false. MCP receives only the existing Workspace binding and has no direct spell write binding.

## Refresh-view ownership

`RefreshCoordinator` deduplicates network work while applying caller-specific priority/reason to the returned status. `AppSessionController` assigns a refresh-view generation on page refresh and sign-in, snapshots completion dictionaries before deferred cache loading and checks ownership before publishing status. A short synchronous gate protects generation selection and final status publication without holding a lock across an await. Cached fetch results remain usable after view ownership changes; stale metadata does not replace current route priorities.

## Computed account readiness boundary

`get_account_status` and projection `inspectAccount` normalize the computed top-level `needsCron` from a bounded full-user read. The filtered user-fields route does not return computed readiness. Private raw user data remains outside public read models and attempt receipts. Server Daily projection requires an explicit boolean plus the existing timezone/day verification; the PWA snapshot parser uses the authoritative boolean before the existing inferred fallback.

## Private Cron authorization and replay protection

Auth exports `CronService` as a private named entrypoint. Public routing does not dispatch that class, and production MCP receives no direct Cron binding. The existing operation-discriminated Auth attempt table stores `cron` receipts; task, spell and Cron writes share the same profile/connection lease and unresolved-attempt fence. New Auth migrations are unnecessary. `CRON_ENABLED` ships false in every environment.

Bounded observation reads full UserGet, server-evaluated task due-state for the previous Habitica day and current group quest facts. Relevant task count is limited to two hundred; review truncation above one hundred unfinished due Dailies blocks preparation. Critical data remains private, enters only a bounded canonical fingerprint and is absent from receipts. Original successful acknowledgements alone confirm complete request processing. Private progress reads report later account day evidence without changing receipt state. Post-dispatch failures retain uncertainty even for non-success status codes; pre-dispatch grant/quota/state refusals remain known failures. A unique sixty-second lease and an eleven-second HTTP reserve bound dispatch ownership. Fresh read verification cannot lock out independent Habitica clients.

## Reviewed Cron storage and recovery

`prepared_cron_actions` is a typed profile-owned ledger with request uniqueness, a token hash, exact private preconditions, public preview/receipt JSON, versioned transitions and a saved attempt identity. Acceptance consumes the latest two-minute token. A ninety-second Workspace lease bounds a sixty-second foreground action; runtime updates preserve saved task attempt fields. Auth performs the fresh upstream checks and durable single dispatch. Confirmed completion and refresh success are distinct fields.

Workspace persists dispatch identity before forwarding and never forwards the saved dispatch again. Recovery waits for active ownership to end, then looks up only the original Auth receipt; optional progress observation cannot alter completion truth. Version and epoch checks reject stale writes. Accepted, dispatching and unknown Cron rows fence task, spell and Cron execution after Auth receipt retention. Own-history exports explicitly project public entries; Workspace deletion cascades private Cron rows. The portable task journal format remains unchanged.

## Durable grant epochs and management status

Auth migration `0006-grant-revocation.sql` adds grant epochs to profiles, linking tickets and pending consent. OAuth grants carry the issued epoch in private properties and metadata. Access validation and refresh exchange compare the issued epoch with active D1 enrollment; grant-only revocation changes the epoch before bounded provider cleanup. Last-use metadata records verified ChatGPT authorization, without request contents. Public management routes require the existing management bearer and permitted origin/transport. Status projects bounded permissions, account labels and credential verification/repair metadata; no raw grant or vault object is returned.

Workspace migration `0007-mcp-grant-epoch.sql` records the observed grant epoch and binds each unaccepted task/spell/Cron preparation to the issuing epoch. A newer epoch invalidates unaccepted previews with versioned storage accounting. Older in-flight calls cannot roll the observed epoch backward; late old previews fail execution even after cleanup. Accepted task receipts and accepted/unknown spell/Cron actions remain. Grant revocation does not change connection identity or implicitly disable separately authorized Workspace execution. Management-token replacement uses an optimistic generation/token comparison. Browser tab-generation guards discard late enrollment, status and rotation responses.

## Credential key job state

Auth migration `0007-credential-key-jobs.sql` stores private job identity, versions, cursor, counts, status and lease. The operator API accepts only configured version identifiers and numeric batches of one to twenty-five profiles. Each step has a thirty-second work budget within a ninety-second lease. Fresh envelope encryption uses the existing authenticated AES-GCM contract and distinct current/old encryption keys. Stable profile-ID keys remain unchanged.

A D1 transaction conditionally replaces the exact old envelope/generation and updates progress using the same lease boundary. Credential changes during the read/encrypt window cause a skip instead of overwrite. Remaining source references trigger another bounded pass rather than false completion. Successful completion clears the cursor. Unreadable data blocks progression; operator repair can resume the same job. Explicit inventory/removal guards require zero references and no unfinished dependent job. Unsupported metadata fails closed. Existing Auth metadata maintenance removes at most one hundred inactive jobs older than thirty days per pass, without rotating keys or calling Habitica. All shipped operator flags remain false.

## Receipt lookup and connection replacement

Workspace connect clears the observed MCP grant epoch only when the verified Auth connection ID changes. The access epoch changes independently and retained canonical data remains. The next fresh grant initializes observed authority for the replacement connection without allowing old same-connection rollback.

Spell/Cron private gateways normalize every unsuccessful lookup response to `unknown` with `receipt_unavailable`, matching the task executor rule. Authorization or service failure is not a stored original refusal. HTTP-success lookup of an actual stored applied/refused receipt retains the existing recovery behavior. Accepted unknown spell/Cron rows remain fences after old credential access disappears.

## Isolated dev configuration

`deploy/prepare-dev.mjs` validates public environment identities and generates separate dev Worker/Pages configs, frontend settings and a staged plugin. Dedicated dev resources exclude committed normal/preview resource IDs. Runtime integration origins accept only a complete canonical HTTPS origin/resource tuple; partial overrides reject. Source defaults remain disabled. `deploy/frontend-settings.mjs` preserves unrelated settings while the build restores source configuration on success/failure. [The operator checklist](../../deploy/README.md) owns remote setup and client qualification steps.

## Integration operational protection

`workers/chatgpt-auth/src/traffic.ts` uses the existing authorization counter table for atomic minute quotas and anonymous UTC-day denial/429 metrics. Profile upstream cooldown and credential repair use the existing current-connection read-state record; enrollment cooldown uses an independent bounded counter record. Expiry cleanup removes at most one hundred counter rows per call. Private traffic checks mediate MCP and Workspace ingress without exposing a public quota-control endpoint. Every upstream dispatch consumes capacity before mutation reservation. Operator aggregates exclude profile/network keys.

Workspace operational status performs profile-scoped aggregate queries and rechecks the active connection/access epoch within the D1 batch. App status reads cap the response at eight KiB, require complete count fields and discard stale session/account results. The status snapshot grants no retry or reconciliation authority. [The adopted operational contract](companion-workspace-contract.md#adopted-operational-protection-boundary) owns numeric caps and privacy boundaries.


## Owner-isolated API dispatch

`OwnerTestingPolicy` holds the narrow known self-spell allowlist. `HabiticaMutationGuard` validates browser API paths before request construction and credential headers. Party departure has no profile override; owner-isolated dispatch also rejects group mutations and Cron. `HabiticaApiClientOptions` defaults to owner isolation, while generic synthetic transport fixtures explicitly select the broader local shape lane.

App feature configuration and API options share `Features:OwnerIsolatedTestingEnabled`. Session state exposes the profile for disabled controls. The controller refuses shared spells, quest changes and Start New Day before automatic equipment work, and disables shared-party sync during the profile. The active production frontend uses owner isolation and no shared-party sync; backend game-effect flags remain independently disabled.

## Durable upstream admission

Auth `upstream-pacing.ts` coordinates foreground and background admission across Worker instances through integration D1. `ADAPTIVE_PACING_ENABLED` ships false in every environment. Enabled admission combines a profile bucket (30 requests per minute fallback) and a shared-egress bucket (300 fallback), with up to two reserved requests and slowdown below half capacity. Authoritative limit/remaining observations lower both estimates conservatively because Habitica can limit User ID or IP traffic. Larger remaining observations cannot credit capacity already reserved by concurrent requests.

A transactional two-row compare-and-swap verifies both versions before claiming capacity; the second update requires the exact global reservation identity. Denied profile admission spends no shared capacity. Contending requests receive a bounded retry deadline rather than an unbounded queue or held response. Per-profile ceilings bound one profile's share; admission provides no strict FIFO or scheduling guarantee across independent Habitica clients. Existing fixed quotas, credential repair, authorization and thirty-second automated-call leases remain separate checks.

Responses and transport failures update durable recovery state. Failure doubles a factor capped at sixteen; success lowers the factor by one eighth. Server cooldowns remain independent and cannot be cleared by success. Upstream dispatch wrappers make one HTTP request and never replay an effect. Anonymous aggregate helpers expose recovering/cooling bucket counts; no task, account, network or credential value enters the aggregates. Profile rows cascade on profile deletion. Hourly and admission cleanup remove at most one hundred rows after twenty-four hours of inactivity once the cooldown has ended.

## Civil recurrence calendar

`RecurrenceCalendar` in the Domain layer and `workers/shared/recurrence-calendar.ts` expand the canonical Daily recurrence model without writing tasks, occurrences or commands. Daily intervals anchor to the start date. Weekly intervals anchor to Monday in the start-date week and select explicit weekdays. Monthly intervals anchor to the start month and select explicit day numbers; missing dates in short months are skipped. Start-date bounds apply to every frequency. Each requested inclusive range contains at most 366 days and returns unique ascending civil dates.

Calendar calculations accept UTC and supported IANA region-style timezone identifiers. Unavailable zones, malformed rules and oversized/reversed ranges return explicit failures without partial dates. UTC-instant conversion uses the selected rule timezone across DST and offset boundaries; no device-zone fallback exists. The pinned WebAssembly SDK enables timezone support by default; actual browser integration and mobile-device qualification remain separate checks.

Civil recurrence dates supply no Habitica due-state or reward evidence. Habitica day-start, Cron readiness, private preconditions and original projection receipts remain independent. The calendar kernel allocates no persistent occurrence identity and never backfills scores. Reviewed authoring and occurrence lifecycle commands use the calendar kernel; retained occurrence review and UI integration remain queued separately.

## Recurrence command identity and portability

WorkspaceTaskPatch adds optional recurrence and clearRecurrence fields. Default fields remain absent from serialized legacy requests and normalized Worker commands. Existing v1/v2 hashes remain unchanged. A v3 prefix covers recurrence edits, explicit detail/rule presence, rule fields and ordered weekday/month-day arrays using the existing length-prefixed UTF-8 encoding. The workspace document already stores recurrence; no D1 migration is required.

Both acceptance engines validate rule structure and local timezone availability before saving. New recurrence intents require a Daily, no game projection and no linked/pending/uncertain mapping. Scheduled completion checks the canonical occurrence date independently of private Habitica due-state and Cron proofs. Schedule clearing retains prior occurrence/completion records and immutable accepted snapshots.

Portable journals validate historical rule structure without requiring the reading device to resolve every timezone. Recurrence intents require a matching Daily snapshot, no projection request and no game effects; contradictory or missing snapshots are rejected. Older clients can reject extended command fields, so coordinated client/Worker release precedes use of v3 intents. Legacy requests and retained v1/v2 receipts preserve the existing protocol.

## Recurring lifecycle commands

SkipOccurrence, PauseTask and ResumeTask append operation values without changing existing enum values or hash encoding. Lifecycle intents have no patch, direction or game projection. Skip requires an explicit matching scheduled date and a pending or absent occurrence; the persisted record uses skipped state without a completion/score event. Pause/resume permits active-to-paused or paused-to-active transitions for unlinked Dailies/Habits. No command backfills occurrences or discards history. Schedule replacement supplies explicit restart semantics independently of resume.

Acceptance checks current mappings and rejects linked, pending or uncertain projections before mutation. Existing paused-state completion/Habit-score guards remain authoritative. Prepared identities, scope checks, access epochs and revision preconditions apply to every lifecycle tool. Portable journals require matching kind/state/date shape and zero game effects. Browser receipt adapters preserve skipped-event counts and reject lifecycle receipts claiming a game projection. No document or D1 schema migration is required.

## Canonical task query contract

WorkspaceTaskFilter/WorkspaceTaskQuery in Application and workers/shared/task-query.ts evaluate the same bounded canonical document without storage writes, clocks or occurrence expansion. Existing list_companion_tasks routes through the shared validator and evaluator. The MCP input schema advertises combined filters, offset and expectedRevision; tasks:read remains required independently at MCP and the private Workspace boundary. Unknown fields cannot supply a profile or account selector. Default ordering and all-state visibility remain compatible; text search now includes notes.

Category filtering uses the direct task category, falling back to the assigned project category only when no task category exists. Archived metadata references remain assignments until explicitly changed. Projection filtering reads nonarchived mappings from the same document snapshot, treating absent mappings as unlinked. Linked describes identity linkage rather than confirmed command execution. Pages include matchedCount, truncated, offset and nextOffset; continuation preserves filters and supplies the returned revision. A stale_revision response requires restarting at offset zero. The query service introduces no persisted schema change.

## Civil agenda query contract

WorkspaceAgenda in Application and workers/shared/task-agenda.ts evaluate the same canonical document. Explicit plan/deadline labels are coalesced by task/date with active-rule virtual Daily occurrences. Persisted completed/skipped dates override generation without requiring membership in the current replacement rule. Persisted pending dates only attach to a currently generated pending occurrence. Paused rules generate no future rows. Each row includes reason flags, canonical task state, optional retained occurrence identity and the current ruleTimeZoneId; historical occurrence timezone and completion instant are not invented.

The narrow list_companion_agenda tool uses tasks:read at both MCP and Workspace boundaries. Explicit from/through civil dates span at most 366 days. Queries refuse more than 250000 active-rule date evaluations or ten thousand materialized rows before returning any page. Pages contain at most one hundred rows and preserve date/document order with expectedRevision continuation guards. No D1/document schema migration, persisted occurrence generation or upstream request is introduced.

## Organization read parity

WorkspaceOrganization exposes typed local category/project/section/label pages and goal summaries. Organization queries share the validated canonical snapshot and existing Domain goal-progress rule. The Worker evaluator serves list_categories, list_projects, list_project_sections, list_labels and list_goals through the existing narrow task API; tasks:read remains mandatory at MCP and Workspace. Inputs whitelist page bounds and only the applicable exact category/project selector. No profile selector, generic collection proxy or write command is exposed.

Lists exclude archived entities and preserve document order, except stable section position order. Section reads hide children of an archived or missing project. Goal results return the canonical goal with completedTodos/eligibleTodos counts; the denominator excludes archived/cancelled/non-Todo tasks. Empty denominators do not imply completion. Pages retain the existing one-hundred result bound and require expectedRevision on nonzero offsets. No storage schema changes are required.

## Task status observation

Workspace task-status.ts serves get_task_sync_status through the existing narrow task API under tasks:read. One aggregate statement joins workspace_commands to the active profile/connection/access epoch and returns current canonical revision, store version and executor opt-in. Runtime executor availability remains a separate configured flag. The query names every durable task projection state and separately sums requested/confirmed effects. The status reports stored receipts rather than current game stats.

A missing active join rejects authorization; SQL failures, unsupported state/effect shapes and inconsistent confirmation counts reject status as unavailable. No prepared gameplay table is read or aggregated. The function performs no writes or service dispatch. Existing MCP grant-epoch maintenance can invalidate stale preparations before ordinary tool routing; status introduces no additional mutation. No D1 migration or new persisted model is required.

## Native placement command boundary

WorkspaceTaskPlanning in Domain and shared task-planning.ts define a partial native placement patch. The appended PlanTask operation accepts only patch.planning; other task operations reject the planning extension. Canonical reference checks require active requested category/project/section/label identities and consistent project sections. Existing metadata is retained unless explicitly changed. planTask bypasses effect generation rather than reusing linked updateTask behavior, retaining all existing mappings and unknown-outcome fences. Application and Worker acceptance preserve task order and history.

Command identity v4 encodes the existing base fields, explicit game-detail presence, the complete recurrence extension and planned-date/clear/reference/label fields. Absent planning retains v1/v2/v3 hashes and legacy serialization. Portable validation checks exact intended planning values against the accepted snapshot and forbids game effects. Cloud previews compare the requested native intent after normalizing only the server-issued command ID; changed targets, fields or proposed effects fail validation. No document or D1 schema migration is introduced; older clients can reject the extended operation/patch and require a coordinated client update.

## Completion history read boundary

WorkspaceCompletionHistory evaluates the canonical document and completion-command snapshots without persistence. Shared completion-history.ts applies equivalent normalized detail search, exact selectors and inclusive UTC tick bounds. Missing accepted snapshots explicitly select currentTask details; a present mismatched command/snapshot fails as unavailable. Retained archive flags remain visible. Sorting uses UTC completion precision, revision and stable document order; Daily occurrence dates remain independent civil labels.

The Workspace history reader uses the existing export-style [D1 batch transaction](https://developers.cloudflare.com/d1/worker-api/d1-database/#batch) pattern for the active document and matching command snapshots. Both statements bind profile, connection and access epoch. Snapshot rows remain separate rather than collecting the journal into one oversized database value. Current revision guards continuation, and malformed source data returns history_unavailable. The new get_completed_history tool requires tasks:read at MCP and Workspace; canonical unavailable-read recovery applies. The history reader introduces no storage schema change or mutation.

## Todo completion-cycle invariants

TaskRestoration and the optional restorations document field extend the current canonical contract without a database schema migration. Absent/null extensions preserve default serialization; explicit empty extensions remain explicit. Restoration IDs participate in global identity, revision and capacity validation. Records require matching Todo/completion identity, unique completion reference and restoration command, a distinct completion/restore command role, UTC time and a revision after completion. A restoration must precede the next completion revision. Archived records retain the same causal meaning.

Tasks with restoration history use the latest closed/open cycle to validate current completed state. Documents without restoration records retain the existing single-completion and legacy active-history acceptance behavior. Current Todo state drives goal progress. Domain and Worker validators mirror the rules, and generic snapshot copies preserve the optional extension. History metadata is optional and omitted from rows without restoration data. Strict clients without extension support reject used extensions; coordinated application/Worker updates precede use. No automatic conversion, reward reversal or execution workflow is supplied by the data model.

## Exact Todo restoration command boundary

The appended RestoreTodo operation accepts only WorkspaceTodoRestore with an exact CompletionId. Native acceptance verifies current task state, exact completion ownership and every retained projection mapping, including archived mappings. Acceptance preserves task order and appends one restoration record without effects. The receipt counts the restoration event separately from game projection counts. Original command lookup precedes current revision/state checks, preserving idempotent recovery after later cycles.

Command identity v5 encodes the existing base, game-detail presence and recurrence extension, an explicit planning-presence marker and exact restoration completion UUID. Unused restoration fields retain v1/v2/v3/v4 hashes and default wire shape. Portable commands require matching causal restoration records and accepted snapshots; fabricated game effects fail validation. Cloud preview comparison normalizes only the server-issued command ID. MCP and private Workspace bindings require habitica.tasks.write for restore_todo. No database migration, new secret, executor activation or upstream call is required.

## Retained Habit history read boundary

WorkspaceHabitHistory and shared habit-history.ts evaluate existing HabitScores with matching scoreHabit snapshots. The query reuses canonical text folding and the precise UTC/page validation conventions from completion history. Every event retains a separate row, including same-command batches. Snapshot fallback is explicit, while present operation/task/direction mismatches fail history_unavailable. Sorting preserves seven-digit UTC precision, revision and stable event order.

The Workspace habit-history reader batches the active document and matching command snapshot rows under the same profile/connection/access epoch. Separate rows avoid one oversized aggregated journal value. The endpoint reads current D1 state rather than an earlier routing snapshot. tasks:read is enforced at MCP and the private Workspace binding; normal unavailable-read recovery supplies no mutation uncertainty or invented counts. No D1 migration, new persisted entity, effect dispatch or external service request is introduced.

## Unblocked Todo candidate read boundary

WorkspaceUnblockedTodos and shared queryUnblockedTodos validate the original canonical document and collect completed dependency identities. Candidate eligibility joins the existing combined task query before matched counts and pagination. The shared query accepts an internal eligibility predicate while ordinary task reads retain unchanged behavior. The dependency graph remains intact throughout evaluation; filtered views never supply dependency state.

Workspace unblockedTodos selects the current active profile/connection/access-epoch document in one D1 statement. Invalid source graphs return candidates_unavailable, missing authorization returns unauthorized and revision changes return stale_revision. The narrow tasks:read tool uses canonical unavailable-read handling. No schema migration, new model, game dispatch or external service call is introduced.

## Explicit agent test profile boundary

OwnerIsolatedTestingEnabled defaults false in source frontend configuration, API options and browser API construction. Deployment generation selects false explicitly and retains PartySyncEnabled true. Test isolation is an opt-in execution profile, never the ordinary user permission model. An explicit test profile still fences party-management routes and manual Cron; spell casting relies on the existing class/unlock, mana, target, freshness and sequential execution logic without a separate test whitelist. Party departure remains denied by the unconditional low-level mutation guard. No backend gameplay flag, storage schema or credential changes accompany the frontend correction.

## Native Todo relationship command boundary

The appended RelateTodo operation accepts only optional WorkspaceTaskRelationships on patch.relationships. Domain/shared parsers enforce explicit parent clearing, bounded complete dependency replacement, exact nonarchived Todo references and self-reference refusal. Existing full graph validation covers parent/dependency cycles. Acceptance preserves task order, state, metadata and history while bypassing game effect generation regardless of mapping state.

Identity v6 retains the existing base, details and recurrence encoding, explicit planning/restore presence markers and parent/clear/dependency fields. Unused relationships preserve v1-v5 hashes and default serialization. Portable validation matches the exact accepted native snapshot and rejects effects. Cloud preview comparison normalizes only the server-issued command ID. MCP/public/private authorization uses the existing task write grant and original prepared command receipt path. No D1 migration, new secret or executor activation is introduced.

## Native organization history representation

Optional OrganizationHistory resides in WorkspaceDocument rather than the game outbox or a second organization database. Typed kind/action/entity identities, accepted snapshots and companion-organization-command-v1 hashes define native receipts. The encoding uses UTF-8 byte-length fields and explicit null markers for exact name/reference/clear/position/task-list intent. Task command encodings remain unchanged.

WorkspaceOrganizationHistory and the shared organization model validate field applicability, retained references, strictly increasing causal revision, current metadata and partial-update/archive transitions. Full journal and native task acceptance validation additionally verify checksums and refuse task/native identity or accepted-revision collisions. Native organization identities cannot acquire generic gameplay journal events. Lightweight root parsing is not a checksum acceptance boundary; browser storage/import and cloud frozen handoff validate the complete journal before persistence.

Document copies and atomic D1 document commits retain the extension automatically. Existing snapshot/handoff/export paths require no new table or migration; current document/archive size limits continue to apply. Native task acceptance refuses organization command-ID reuse and preserves metadata history. Current Todo state determines goal progress independently of retained organization receipts. Missing/null extensions retain legacy default serialization; strict older clients reject used extensions and require coordinated updates. No organization route or gameplay dispatch is supplied by the model.


## Native organization acceptance engine

Organization acceptance validates the complete journal, command shape and UTC timestamp before original receipt lookup. A retained exact hash returns the original entry before current revision/entity/authority checks. Task-command identity collisions remain rejected. Fresh local acceptance rejects frozen/cloud ownership, stale canonical revisions, exhausted canonical/store versions, invalid references and archived targets. Canonical result validation enforces unused global entity IDs and exact metadata history transitions.

Creation appends to the selected metadata array; updates/archive replace only the exact entity at the existing position. The canonical revision and store version each advance once. OrganizationHistory receives one native receipt; task commands, gameplay events, tasks, mappings and unrelated arrays remain unchanged. WorkspaceOrganizationService and Worker submitOrganization use account-bound compare-and-swap stores. A false commit reloads and validates current state before recovery; a failed acknowledgement remains unknown until the same command recovers the original receipt. No production route, D1 schema, binding or executor activation is supplied by the engine.


## Reviewed organization transport boundary

Organization tool schemas use an independent workspace.organization.write grant. Public MCP routing and the private Workspace entrypoint each validate the exact scope; native receipt reads use tasks:read. Auth renders an independently selectable metadata-write disclosure. Existing task grants never acquire organization permission implicitly. Typed PWA IWorkspaceOrganizationApi shares the current account-bound cloud session without exposing credentials or account selectors.

organizationApi loads canonical document, task journal and gameplay events in one D1 batch and validates the complete snapshot and workspace identity. The cloud engine requires matching cloud authority; the local engine continues to reject fresh cloud/frozen writes. Preparation reserves bounded retained request metadata in workspace_preparations; intent hashes include an organization-specific domain prefix. Canonical acceptance updates only document JSON, byte accounting, store version, commit tag and timestamp under exact profile/connection/access/store and unexpired preparation/grant conditions. No task command or gameplay event is inserted.

Accepted organization receipts remain in OrganizationHistory. Retention and grant-epoch cleanup recognize both accepted task outbox rows and native history before deleting unaccepted previews. Accepted original request/correlation bindings survive with retained history. Existing one-MiB document and eight-MiB archive bounds apply before acceptance. No new table, binding, secret or executor switch is required. Coordinated Auth/MCP/Workspace and browser updates precede actual use; no deployment or real grant follows from local qualification.


## Native Todo state command

SetTodoState appends operation value 13 and optional WorkspaceTodoState to the task patch. Shape validation permits only the state field with active/waiting/someday/cancelled; acceptance requires a nonarchived noncompleted Todo and rejects projection. Native acceptance preserves the task position, updates canonical task revision/time/state, retains history/mappings and appends the original ordinary acceptance receipt with zero activity/effect counts. Existing accepted game commands remain untouched.

Identity v7 includes extension-presence markers and exact desired state while unused fields preserve v1-v6 hashes. Portable validation requires a matching native Todo snapshot and refuses altered desired state, fabricated completion or effects. Shared task schemas and browser input/preview guards route set_todo_state through existing account-bound command preparations and task-write grants. No new D1 schema, binding, consent scope, secret or executor activation is introduced.


## Optional Todo stage command and portability

CompanionTask.ExecutionStage is an optional Todo-only enum omitted when null. Domain and shared Worker validation retain dormant metadata under nonactive Todo states and archives while rejecting non-Todo or unknown values. WorkspaceOperation.SetTodoStage appends value 14; patch.todoStage validates an exact defined Stage XOR explicit ClearStage. Authoring requires an existing nonarchived active Todo and refuses projection or mixed patches. Native acceptance retains task order and produces no game effect or task activity.

Identity v8 binds extension presence, exact stage and explicit clear intent. Unused extensions preserve v1-v7 hashes and default serialization. Portable journals require the matching native accepted snapshot; altered preferences or effects fail validation. Browser review and canonical MCP set_todo_stage use existing prepared intents, original receipt recovery and task-write permissions. Atomic document storage, backup and frozen handoff retain metadata without a D1 migration, new secret or executor activation. Current state always dominates dormant stage metadata; Next adds no dependency or scheduling semantics. Production stage presentation remains separate; the active-stage read predicate follows below.


## Canonical stage query predicate

WorkspaceTaskFilter.ExecutionStage is an optional enum omitted from default serialization. Application and shared task-query validation reject undefined stages and explicitly incompatible kind/state predicates. Evaluation requires active Todo state before comparing the stored preference or Inbox default. Current state dominates dormant metadata. Candidate reads reuse the predicate after full-document dependency eligibility.

MCP list_companion_tasks and get_unblocked_todos advertise the same executionStage selector; shared public/private routes retain existing tasks:read ownership. Filters remain read-only with stable canonical order, matching totals and revision-bound continuation. No persistence schema, write scope, secret or executor change accompanies the selector. Saved query definitions remain separate.


## Saved task-view model and evaluation

WorkspaceDocument.SavedTaskViews is an optional collection included in global entity identity and capacity checks. WorkspaceSavedTaskQuery contains an explicit tasks/unblockedTodos source and WorkspaceSavedTaskPredicates with scalar existing query fields. Candidate definitions reject kind/state overrides. Root validation checks source, names, revision, filter enums/ranges and category/project/label references, including retained archived entities. Missing/null extensions remain omitted; strict unsupported clients reject used extensions and require coordinated updates.

Application WorkspaceSavedTaskViews.Evaluate and shared querySavedTaskView reuse canonical task/candidate engines rather than storing result IDs. Caller paging never replaces definition predicates. Current state, stage precedence, dependency eligibility, stable order and guarded continuation remain authoritative. Generic snapshots, scoped IndexedDB, portable backups and frozen authority transfer preserve definitions without D1 table changes. No new write adapter, consent, secret or executor activation is supplied by the model.


## Native saved-view command identity and history

WorkspaceOrganizationKind.SavedTaskView appends value 5. Optional Query fields on native patches/snapshots remain omitted when unused. Native v2 identity encodes saved-view kind, base command fields, query-presence marker, exact source and fixed ordered scalar predicates. Existing kind commands without Query retain v1 bytes. Query equality uses semantic scalar fields independent of JSON property order.

Native create/update/archive engines retain complete saved definitions and typed query history in one optimistic journal commit. New filter references require exact retained category/project/label identities, allowing archives. Full query replacement never merges omitted predicates from the old query. History validation checks source/filter shape, references, transitions, exact query snapshots, hashes and current view state. Original receipt lookup precedes fresh revision/archive/authority checks; uncertain storage acknowledgement retains original recovery. No task/gameplay event or new D1 table is introduced. Scoped adapters remain the next separate unit.


## Saved-view scoped transport

Shared registries classify list_saved_task_views and query_saved_task_view as tasks:read canonical reads. Query input permits exact viewId and limit/offset/expectedRevision only; no predicate override or account selector enters the route. The Workspace adapter rereads the canonical document under current profile, connection and access epoch before strict validation/evaluation. MCP read failure handling reports unavailable without saved/action fields.

Native organization schemas include savedTaskView and explicit complete source/filter definitions under independent workspace.organization.write enforcement on public and private routes. Existing prepared-command/request/correlation binding and retained native history supply original receipt recovery after edits, archival and grant changes. Browser IWorkspaceSavedTaskViewApi validates provenance, workspace/view identity, revision, page totals, definitions and task rows; session changes discard responses and read errors never imply mutation uncertainty. Native browser preview/receipt guards validate complete query intent and typed snapshots. No new database table, secret, executor activation or deployment is required for local qualification.


## Development rollout versus release qualification

Owner selection on 2026-10-19 permits the existing site to expose implemented development capabilities before broader client/device and E01 hands-on acceptance. Missing observations remain unverified. deploy/prepare-dev.mjs accepts optional featureProfile pilot/development and maps known capability flags explicitly; omission remains pilot. Unknown enabled-flag names require a reviewed mapping. The development profile exposes product availability and adaptive pacing while preserving resource identities, protected secrets, account consent, prepared previews, private service boundaries and unknown-outcome fences. Aggregate executor preflight and Auth migration 0008 precede activation.

## Snapshot-bound task list presentation

CompanionTaskList receives the current WorkspaceDocument from the account-bound WorkspaceTaskController. WorkspaceTaskQuery and WorkspaceSavedTaskViews remain the only filter evaluators; UI state contains selectors and an offset rather than copied task data. Page size twenty and the current document revision guard every evaluation. Document identity clears filters; changed revision resets the offset. Missing/archived definitions remain explicit errors until reset or reload. Saved definition descriptions render only after successful canonical validation.

The component emits an exact selected CompanionTask to the existing editor. No new remote transport, persistence field, permission or mutation path is added. Pending task intent disables list controls through the parent. The parent removes account data on sign-out/account change, recreating child selection state for the next loaded document. Cloud snapshot refresh and original command recovery stay owned by the existing controller.

## Synthetic rendered qualification ownership

The WebApp bUnit suite owns PageLayoutEvidence captures and route/state metadata. tests/browser-qualification owns dependency-free staging, pure measurement functions and browser fixtures using existing published assets. Node tests qualify calculation canaries; an actual browser qualifies CSS compositing, geometry and seeded failures. Component interaction tests remain the source of delegate/command behavior evidence. The preparation script serves only synthetic captures on loopback with a bounded lifetime. Browser automation uses the available authorized browser tool; no new browser runtime or package is installed.
