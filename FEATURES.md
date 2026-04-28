# FEATURES.md

Last updated: 2026-04-27
Primary audience: AI agents and senior developers
Primary Habitica integration reference: `HABITICA_API.md`
Related technical reference: `TECHNICAL.md`

## 1. Purpose

This document describes project features at a technical level.

AI agents must update this file when adding, materially changing, or removing a feature. Feature descriptions must be implementation-oriented and useful for future agents that need to continue the work without rediscovering domain intent.

Do not use this document for marketing copy. Keep it dry, technical, and explicit.

## 2. Feature documentation format

Each feature should use this structure:

```text
## Feature name

Status:
Owner module:
Application entry point:
Primary Habitica data:
Mutates Habitica state:
Requires confirmation:
Offline behavior:
Rate-limit sensitivity:

### Goal
### Inputs
### Outputs
### Local storage
### API interaction
### Algorithm / rules
### Validation
### Error handling
### Security / privacy
### Tests
### Open questions
```

Use `Open questions` when Habitica API behavior, game formulas, or available fields are not fully verified.

## 3. Shared feature principles

### 3.1 Local-first behavior

Features should read from local snapshots by default. A feature may request fresh Habitica data when stale data materially affects correctness.

### 3.2 Explainable output

Any recommendation, optimizer result, or estimated value must include explanation data:

```text
input snapshots
selected factors
ignored factors
assumptions
warnings
confidence level
calculation version
```

### 3.3 Mutating actions

Any feature that changes Habitica state must:

- validate against the latest available snapshot;
- show a dry-run preview for multi-step or destructive operations;
- execute sequentially unless a safe batch endpoint exists;
- stop on unexpected state changes;
- respect `Retry-After` and rate-limit metadata;
- persist execution logs;
- support partial-success reporting.

### 3.4 Formula uncertainty

If a formula is inferred from Habitica behavior, community documentation, or code inspection rather than stable API documentation, mark the calculation as assumption-based.

### 3.5 Snapshot freshness states

Features must use the shared snapshot freshness model from `TECHNICAL.md`:

```text
fresh
stale
expired
missing
```

Rules:

- mutating features must require `fresh` snapshots for directly affected volatile entities;
- read-only recommendations may use `stale` snapshots only with visible warnings and downgraded confidence;
- `expired` or `missing` data must block destructive actions and high-confidence dry-run plans;
- feature-specific thresholds may be stricter than `TECHNICAL.md`, but not looser for mutating actions.

## 4. Party buff timing optimizer

Status: partial
Owner module: `Habitica.Domain.Party`, `Habitica.Application.Auth`, `Habitica.Storage`
Application entry point: `Habitica.WebApp.Pages.PartyPage`
Primary Habitica data: party group, party members, member `lastCron`, member public quest progress, member preferences day start and timezone offset
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: available from latest party snapshot and local CRON history
Rate-limit sensitivity: medium; party data should not be polled aggressively

### Goal

Estimate the best time to cast team buffs from read-only party-member CRON data. The Party page shows current `CRONed X/Y`, data-gap counts when unknown/stale members exist, member-level CRON state, average member CRON time, and early/low-confidence recommendations as soon as the first refresh stores CRON data.

### Inputs

```text
party id
party member list
member display names
member user ids when available
member lastCron timestamps when available
member pending quest damage when available
member custom day start when available
member timezone offset when available
local user time zone
snapshot timestamp
```

### Outputs

```text
current CRONed X/Y count
data-gap counts when unknown or possibly stale members exist
average best buff time
self-first buff time
party pending boss damage/items when available
per-member CRON state and average CRON time
viewer-local graph buckets
member coverage count
members excluded due to missing data
confidence level
warnings
```

### Local storage

Store current party state separately from historical CRON events.

```text
party/latestSnapshot
party/cronHistory
```

### API interaction

Use Habitica API v3 according to `HABITICA_API.md`. Fetch party/group data only through the API client layer.

Current read-only requests:

```text
GET /groups/party
GET /groups/party/members?includeAllPublicFields=true
```

Do not scrape Habitica pages. Do not call `POST /api/v3/cron` for this feature.

### Algorithm / rules

Normalize all stored timestamps to UTC before calculations. Convert UTC event times to the viewer's local time only for display, graph buckets, and recommendations.

Initial algorithm:

```text
1. Fetch party group and public party members on normal refresh.
2. Classify each member as Croned today, Not croned yet, Unknown, or Possibly stale.
3. Upsert member CRON events by party id, member id, and lastCronUtc.
4. Keep 90 days of stored history and use the latest 60 days for statistics.
5. Deduplicate unchanged same-day refreshes while preserving newly observed lastCron values.
6. Compute per-member average CRON time with a circular time-of-day average.
7. Count stored history days by observation/fetch day, not by each member's old `lastCron` day, so a first refresh remains a 1-day sample even when members last CRONed on different dates.
8. Build viewer-local hourly graph points from UTC CRON events.
9. Recommend average best buff time from the practical CRON threshold; mark estimates low-confidence until 7 stored observation days exist.
10. Recommend self-first buff time from the current user's own CRON anchor. Nearby member CRON times can move the recommendation later, but each member's influence halves every 90 minutes and the wait cost rises by 1 score point per 120 minutes, so far-away party members do not drag this recommendation across the day.
11. For active quests, map member pending quest progress into the member list. Boss quests use member `party.quest.progress.up`; collection quests use member `party.quest.progress.collectedItems` or item totals from `party.quest.progress.collect`.
```

Time-of-day is circular. Avoid naive arithmetic that treats 23:30 and 00:30 as far apart.

### Validation

Warn when:

- less than 50% of members have usable activity data;
- fewer than 7 usable history days exist;
- member lastCron is missing;
- member day start or timezone offset is unavailable;
- the member snapshot was fetched before that member's current Habitica day start.

### Error handling

If party data cannot be fetched, use the latest local snapshot and mark the result stale. Unknown members do not block recommendations, but their count must be visible.

### Security / privacy

Do not expose party member private data beyond what the authenticated user can access through Habitica API.

### Tests

Test:

- CRON classification for midnight and non-midnight day starts;
- timezone-offset classification;
- missing member CRON fields;
- stale fetch detection;
- same-day refresh dedupe;
- UTC storage and viewer-local graph bucketing;
- stored observation day counting;
- circular average around midnight;
- self-first recommendation with exponentially diminishing member influence;
- early estimate warning from first refresh;
- Party page CRON summary and member average rendering;
- missing members;
- all timestamps missing.

### Open questions

Verify how consistently `lastCron`, `preferences.dayStart`, and timezone offsets are returned for all party members under different privacy settings.

## 5. Gear set management

Status: planned
Owner module: `Habitica.Rules.Equipment`
Application entry point: `Habitica.Application.Equipment`
Primary Habitica data: user equipment, owned gear, current gear, class, stats
Mutates Habitica state: yes when equipping a set
Requires confirmation: yes for one-click equip if multiple slots change
Offline behavior: set creation and preview available offline; execution requires API access
Rate-limit sensitivity: medium for multi-slot changes

### Goal

Allow users to create named combat/utility gear sets from owned equipment and equip them with one action.

### Inputs

```text
owned gear
current equipped gear
gear metadata
user class
user stats
gear set definition
```

### Outputs

```text
gear set preview
stat delta
missing item warnings
conflicting slot warnings
equip execution plan
```

### Local storage

Suggested records:

```text
gear_set
gear_set_slot
gear_set_preview_cache
gear_set_execution_log
```

Gear sets are app-specific local data. They should not be assumed to exist in Habitica.

### API interaction

Use Habitica API v3 equipment endpoints as documented in `HABITICA_API.md`.

Each slot change must be treated as a mutating action unless Habitica provides a safe batch operation.

### Algorithm / rules

A gear set is a declarative mapping:

```text
slot -> gear key
```

The equip planner should:

```text
1. Compare desired gear set with current equipment.
2. Exclude unchanged slots.
3. Validate that every desired item is owned and equippable.
4. Produce ordered API steps.
5. Estimate resulting stat changes.
```

### Validation

Reject or warn when:

- item is not owned;
- item metadata is unknown;
- class restrictions prevent use;
- slot is invalid;
- current snapshot is stale;
- API state differs from local state during execution.

### Error handling

Stop execution on first failed slot change unless the user explicitly chose best-effort mode.

Persist partial success.

### Security / privacy

No special sensitive data beyond normal Habitica credentials.

### Tests

Test:

- unchanged slots excluded;
- missing item detection;
- invalid slot detection;
- stat delta calculation;
- partial execution failure;
- stale snapshot warning.

### Open questions

Confirm exact Habitica API semantics for equipping multiple slots and whether any endpoint can change multiple equipment slots atomically.

## 6. Skill-specific gear optimizer

Status: planned
Owner module: `Habitica.Rules.Equipment`
Application entry point: `Habitica.Application.Equipment`
Primary Habitica data: owned gear, gear stats, user class, user stats, skill formulas
Mutates Habitica state: no by itself
Requires confirmation: no
Offline behavior: available from latest inventory/equipment snapshot
Rate-limit sensitivity: low

### Goal

Recommend the best owned gear for a selected skill or action.

Examples:

```text
Pickpocket -> maximize perception or expected gold/drop result
Backstab -> maximize strength or expected damage
team buffs -> maximize relevant casting stat where applicable
```

### Inputs

```text
selected skill
action target
owned gear
gear metadata
current stats
class
buffs
current equipment
formula version
```

### Outputs

```text
recommended gear set
expected stat delta
expected skill result
alternative gear sets
explanation factors
warnings
```

### Local storage

Suggested records:

```text
gear_optimization_run
gear_optimization_candidate
```

### API interaction

No API call is required for calculation if current snapshots are available.

Optional user action may create a local gear set or execute equip through gear set management.

### Algorithm / rules

Initial implementation can use exhaustive search across owned gear per slot if the search space is small enough.

If exhaustive search becomes expensive, use per-slot greedy scoring only after documenting the approximation.

Scoring must be skill-specific:

```text
score = expected_skill_output(candidate_gear, user_stats, target_context)
```

Do not use a generic stat sum unless the selected skill actually depends on that score.

### Validation

Warn when:

- formula is unknown or assumption-based;
- gear metadata is incomplete;
- buffs are stale;
- class-specific effects are not modeled;
- equipment ownership data is stale.

### Error handling

Return partial recommendations only if enough data exists. Otherwise return a blocking validation error.

### Security / privacy

No credentials required beyond local snapshot access.

### Tests

Test:

- Pickpocket scoring;
- Backstab scoring;
- missing gear metadata;
- class-specific restrictions;
- tie-breaking;
- current gear included as baseline;
- explanation output.

### Open questions

Verify formulas for each supported skill against Habitica source/docs before marking results as high confidence.

## 7. Macros Collection and skill macro system

Status: planned
Owner module: `Habitica.Rules.Skills`, `Habitica.Application.Macros`, and `Habitica.WebApp.Macros`
Application entry point: `Habitica.Application.Macros`
Primary Habitica data: user stats, mana, skills, tasks, equipment, inventory, party/quest state
Mutates Habitica state: yes
Requires confirmation: yes
Offline behavior: create/edit/dry-run available offline when snapshots exist; execution requires API access
Rate-limit sensitivity: high

### Goal

Allow users to define and execute a local Macros Collection. A macro is a named declarative sequence of validated actions such as equipping a preset or item, casting a skill, selecting a target, refreshing snapshots, and restoring the original gear captured at macro start.

Macros are not implemented yet. Inventory presets added by the Inventory page are designed as future macro references.

### Inputs

```text
macro definition
macro collection
current user snapshot
current task snapshot
current equipment snapshot
current party/quest snapshot
local equipment presets
owned gear catalog
available skill metadata
```

### Outputs

```text
compiled macro plan
dry-run preview
mana cost estimate
expected result estimate
API execution steps
validation warnings
execution log
```

### Local storage

Suggested records:

```text
macro_collection
skill_macro
skill_macro_step
skill_macro_dry_run
skill_macro_execution_log
skill_macro_execution_step_log
```

Equipment presets live in local per-user inventory preset storage. Macro steps should reference preset ids when possible, not duplicate preset slot mappings into the macro definition.

### API interaction

Macro execution may call multiple Habitica API endpoints.

All calls must go through `Habitica.Api`.

### Algorithm / rules

Macros must be declarative, not arbitrary code.

Allowed initial step types:

```text
equip
cast
selectBestTask
assertManaAtLeast
assertCurrentClass
refreshSnapshot
stopIfWarning
restoreOriginalGear
```

Initial `equip` references should support:

```text
preset id
single gear item key
best gear query such as maximize perception or maximize strength
restore original battle gear or costume captured when the macro starts
```

Selectable equip targets should list matching presets first, then individual owned gear items. Preset labels must include kind, name, and battle preset stat totals when available. Individual gear labels should use the inventory gear catalog display name with raw key fallback.

Example macro:

```text
1. Equip gear that maximizes perception.
2. Cast Tools of the Trade 3 times.
3. Equip gear that maximizes strength.
4. Cast Backstab until there is no mana left.
5. Restore gear that was equipped before the macro started.
```

Execution flow:

```text
1. Load macro definition.
2. Compile into explicit steps.
3. Validate against latest snapshot.
4. Produce dry-run preview.
5. Require user confirmation.
6. Execute one step at a time.
7. After each mutating step, update local state or refresh relevant data.
8. Stop on unexpected state, API error, insufficient mana, or rate-limit delay requiring user-visible wait.
9. Persist execution log.
```

Macro execution must snapshot original battle gear and costume gear before the first mutating step when any restore action is present. Restore actions must use that captured state, not the currently edited preset definitions.

### Validation

Reject macros that:

- contain unknown step types;
- target missing tasks;
- require unavailable gear;
- reference deleted equipment presets;
- exceed available mana at dry-run time;
- contain unsupported class skills;
- require stale data for destructive decisions;
- would execute unbounded loops.

Loops are not supported in the initial macro format.

### Error handling

Persist partial execution state after every step.

If an API call fails:

- stop by default;
- show completed steps;
- show failed step;
- show whether local state may be stale;
- offer manual refresh.

### Security / privacy

Macros must not store credentials.

Exported macros must not include user API tokens, raw API headers, or private snapshots unless explicitly exported as a debug bundle.

### Tests

Test:

- macro parsing;
- macro validation;
- insufficient mana;
- missing gear;
- deleted preset references;
- restore-original-gear planning;
- preset-first gear selection lists;
- task target resolution;
- sequential execution;
- stop-on-failure;
- partial log persistence;
- rate-limit response handling.

### Open questions

Confirm each skill endpoint and target semantics against `HABITICA_API.md` and current Habitica API docs before execution support is enabled.

## 8. Best task selector for skill casting

Status: planned
Owner module: `Habitica.Rules.Tasks`
Application entry point: `Habitica.Application.Tasks`
Primary Habitica data: tasks, task values, task types, task tags, user stats, selected skill
Mutates Habitica state: no by itself
Requires confirmation: no
Offline behavior: available from latest task snapshot
Rate-limit sensitivity: low

### Goal

Find the best daily, habit, todo, or reward target for a selected skill or action.

Examples:

```text
select best task for Pickpocket
select best task for Backstab
select task where skill value is maximized
exclude unsafe or user-protected tasks
```

### Inputs

```text
selected skill
task list
task type filter
tags
task value
completion state
user-defined exclusions
snapshot timestamp
```

### Outputs

```text
recommended task
alternative tasks
expected result
reasoning factors
warnings
```

### Local storage

Suggested records:

```text
task_selection_rule
task_selection_result
user_task_exclusion
```

### API interaction

No API call is required if task snapshots are current.

Macro execution or manual action may later cast a skill against the selected task.

### Algorithm / rules

Initial selector:

```text
1. Filter tasks by allowed types.
2. Remove completed or unavailable tasks where relevant.
3. Remove user-excluded tasks.
4. Score remaining tasks using selected skill formula.
5. Return best candidate and alternatives.
```

### Validation

Warn when:

- task snapshot is stale;
- selected skill formula is assumption-based;
- task value is missing;
- no valid target exists.

### Error handling

Return no candidate rather than selecting unsafe fallback tasks.

### Security / privacy

Task titles may contain sensitive user-entered data. Do not include raw task titles in telemetry.

### Tests

Test:

- type filtering;
- completed task exclusion;
- tag filtering;
- user exclusion;
- stale snapshot warning;
- no valid candidate.

### Open questions

Verify task scoring formulas and skill target constraints.

## 9. Bulk sell planner

Status: planned
Owner module: `Habitica.Rules.Inventory`
Application entry point: `Habitica.Application.Inventory`
Primary Habitica data: inventory, items, pets, mounts, quests, gear where relevant
Mutates Habitica state: yes when selling
Requires confirmation: yes
Offline behavior: recommendation available offline; execution requires API access
Rate-limit sensitivity: high for many sell operations

### Goal

Recommend and optionally sell multiple unneeded items while avoiding items needed for pets, mounts, quests, or user-defined goals.

### Inputs

```text
inventory snapshot
pet collection
mount collection
quest inventory
item metadata
user-defined keep rules
sale value metadata
```

### Outputs

```text
sell candidates
keep candidates
blocked candidates
expected gold
risk warnings
execution plan
```

### Local storage

Suggested records:

```text
sell_rule
sell_recommendation_run
sell_candidate
sell_execution_log
```

### API interaction

Selling is mutating. Use Habitica API v3 through `Habitica.Api`.

Do not execute bulk sale without explicit user confirmation.

### Algorithm / rules

Initial planner:

```text
1. Load inventory snapshot.
2. Apply hard keep rules.
3. Apply pet/mount dependency rules.
4. Apply quest dependency rules.
5. Apply user-defined keep quantity.
6. Calculate sale candidates and expected gold.
7. Produce dry-run preview.
```

### Validation

Block sale when:

- item purpose is unknown;
- item is needed for an uncompleted pet/mount goal;
- user keep threshold would be violated;
- inventory snapshot is stale;
- sale endpoint semantics are not verified.

### Error handling

Sell sequentially by default.

Persist partial success and refresh inventory after execution.

### Security / privacy

No credentials in logs. Inventory can reveal user progression; do not send raw inventory to external telemetry.

### Tests

Test:

- keep rules;
- pet dependency blocking;
- mount dependency blocking;
- unknown item blocking;
- expected gold calculation;
- partial sale failure;
- stale snapshot warning.

### Open questions

Confirm exact sell endpoints and whether Habitica supports safe item sale batching.

## 10. Skill/action result estimator

Status: planned
Owner module: `Habitica.Rules.Calculations`
Application entry point: `Habitica.Application.Calculations`
Primary Habitica data: user stats, class, buffs, gear, tasks, party/quest state, skill metadata
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: available from latest snapshots
Rate-limit sensitivity: low

### Goal

Display approximate results for skills and actions before execution.

Examples:

```text
estimated boss damage
estimated player damage
estimated gold from Pickpocket
estimated task effect
estimated mana cost
estimated buff impact
```

### Inputs

```text
user stats
class
gear
buffs
selected skill/action
target task or boss
quest state
formula version
```

### Outputs

```text
estimated value
range when uncertain
confidence
formula assumptions
warnings
```

### Local storage

Suggested records:

```text
calculation_run
calculation_factor
formula_version
```

### API interaction

No API call is required if snapshots are current.

### Algorithm / rules

Calculators must be modular per action or skill.

Do not implement a single generic estimator with hidden branching. Prefer explicit calculators:

```text
PickpocketEstimator
BackstabEstimator
BossDamageEstimator
PlayerDamageEstimator
BuffImpactEstimator
```

### Validation

Warn when:

- formula is not verified;
- required fields are missing;
- snapshot is stale;
- target state may have changed;
- active buffs are unknown.

### Error handling

Return degraded estimates with warnings only when the missing data is non-critical. Otherwise return a blocking validation error.

### Security / privacy

Do not include raw task titles or credentials in telemetry.

### Tests

Test each estimator with fixed snapshots and expected deterministic outputs.

### Open questions

Extract or verify formulas from official docs, Habitica source, or validated community references before marking estimators stable.

## 11. Dashboard and data explorer

Status: partial
Owner module: `Habitica.WebApp.Dashboard`
Application entry point: `Habitica.Application.Dashboard`
Primary Habitica data: user, tasks, party, inventory, equipment, quest, sync metadata
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: primary feature works offline from snapshots
Rate-limit sensitivity: low unless refresh is triggered

### Goal

Provide a technical dashboard for inspecting local Habitica snapshots, derived data, warnings, and feature recommendations.

### Inputs

```text
local snapshots
derived read models
sync logs
calculation runs
execution logs
```

### Outputs

```text
user summary
current pet and mount state
inventory readiness summary
task-count summary
warnings
sync status
```

### Local storage

Reads from existing snapshot and derived read-model stores.

### API interaction

Only through explicit refresh actions.

### Algorithm / rules

Dashboard must not contain business logic. It displays already computed state and invokes use-case services.

### Validation

Show explicit `fresh` / `stale` / `expired` / `missing` state indicators when snapshots are outdated or unavailable.

When derived stat targets such as max health, max mana, or XP-to-next-level are absent from the cached account snapshot, the dashboard must not render misleading `current / 0` output. Show the current value only and downgrade the explanatory label accordingly.

### Error handling

Show partial data when some stores are unavailable, but surface storage errors clearly.

### Security / privacy

Hide credentials. Avoid displaying raw API headers.

### Tests

Test:

- stale indicators;
- empty state;
- partial sync failure state;
- redacted diagnostics.

### Open questions

Current implementation:

- responsive app shell;
- sign-in entry route;
- dashboard route with cached account cards;
- dashboard inventory readiness summary;
- dashboard stat cards fall back to current-only rendering when the API snapshot lacks non-zero stat targets;
- read-only tasks workspace;
- sync timestamp surface;
- freshness banners for cached tasks and cached account data;
- global error banner for sign-in and refresh failures.

Next:

- add deeper quest explorer surfaces;
- add sync diagnostics and execution history views.

Waiting:

- advanced feature modules must exist before the dashboard can expose their recommendation output.

## 12. Credential setup and validation

Status: partial
Owner module: `Habitica.WebApp.Auth` and `Habitica.Api`
Application entry point: `Habitica.Application.Auth`
Primary Habitica data: authenticated user profile
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: stored credential status can be shown offline; validation requires API access
Rate-limit sensitivity: low

### Goal

Allow the user to enter Habitica credentials, validate them, store them locally, and clear them.

### Inputs

```text
Habitica User ID
Habitica API Token
configured x-client value
```

### Outputs

```text
credential validation result
redacted credential state
current authenticated user summary
```

### Local storage

Suggested records:

```text
credential_state
local_auth_settings
```

Actual token storage must use the credential storage abstraction defined in `TECHNICAL.md`.

Raw token data must live only in the dedicated credential store. Ordinary app stores may keep redacted credential metadata only.

Current MVP records:

```text
auth/persistentCredentials
```

### API interaction

Perform a minimal authenticated request to validate credentials.

### Algorithm / rules

Validation flow:

```text
1. Check local format.
2. Let the user choose session-only mode (default) or persistent mode with explicit opt-in.
3. Send minimal authenticated request with x-client.
4. Handle 401/403 as invalid credentials.
5. Handle 429 using Retry-After.
6. Store credentials only after successful validation, using the selected storage mode.
7. Do not offer save-unverified mode in MVP.
```

### Validation

Reject empty credentials and invalid UUID-like User ID format where applicable.

Reject persistent-storage requests unless the user explicitly acknowledged the persistence warning.

### Error handling

Do not leak token in error messages.

### Security / privacy

Token is password-equivalent. Never log it.

### Tests

Test:

- redaction;
- invalid credential flow;
- rate-limit flow;
- clear-data flow;
- session-only mode;
- persistent opt-in flow.

### Open questions

Current implementation:

- login form with User ID and API Token fields;
- session-only mode by default;
- persistent local credential opt-in;
- credential validation through authenticated `GET /user` request;
- sign-out for the current tab session;
- clear-local-data action that removes persisted credentials and cached task and account snapshots;
- no token logging or token echo in normalized API errors.

Next:

- add explicit client-side validation feedback for malformed IDs and empty values;
- surface `Retry-After` and rate-limit guidance in the UI instead of generic error text;
- store redacted credential metadata for richer offline auth status.

Waiting:

- production deployment should replace the fallback `x-client` behavior with a project-owned Habitica author header value.

## 13. App shell and navigation

Status: implemented
Owner module: `Habitica.WebApp.Layout` and `Habitica.WebApp.Components.Navigation`
Application entry point: `Habitica.WebApp.State.AppSessionController`
Primary Habitica data: local session state, task snapshot freshness, sync timestamp, diagnostics history
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: fully available from local session, snapshot, and diagnostics stores
Rate-limit sensitivity: none by itself

### Goal

Provide a stable responsive PWA shell with top-level routes for sign-in, dashboard, inventory, party, diagnostics, tasks, and settings, while keeping the foldable feature drawer hidden until an authenticated session exists.

### Inputs

```text
session state
cached task snapshot presence
cached user snapshot presence
task freshness state
user freshness state
latest sync timestamp
diagnostics history presence
diagnostics warning count
global workflow error state
```

### Outputs

```text
top app bar
responsive authenticated navigation drawer
route links
refresh action
global warning banner
identity summary
diagnostics visibility and warning cues
```

### Local storage

No direct storage writes. Reads are mediated through `Habitica.WebApp.State.AppSessionController`.

### API interaction

None directly. Refresh delegates to the sync workflow.

### Algorithm / rules

Navigation rules:

```text
1. Hide the foldable feature drawer entirely when no authenticated session is active.
2. Show `Dashboard`, `Inventory`, `Party`, `Diagnostics`, `Tasks`, and `Settings` in the drawer once an authenticated session exists.
3. Keep refresh disabled unless authenticated credentials are available for the current session.
4. Surface the latest workflow error above route content.
```

### Validation

Handle these states explicitly:

- no local data;
- cached data but no active authenticated session with the drawer still hidden;
- authenticated session with latest sync timestamp;
- failed refresh with cached data still available;
- cached diagnostics history without an active authenticated session.

### Error handling

Do not hide controller-level errors inside route components. Surface them once in the shell.

### Security / privacy

Never display raw API headers or token material in the shell.

### Tests

Test:

- authenticated navigation links;
- unauthenticated drawer suppression;
- shell error banner rendering;
- sync timestamp rendering when available.

### Open questions

Current implementation:

- `Sign In`, `Dashboard`, `Inventory`, `Party`, `Diagnostics`, `Tasks`, and `Settings` routes;
- top app bar with refresh action;
- responsive drawer navigation shown only after authentication;
- shared error banner;
- cached identity summary in the app shell;
- diagnostics route included in the authenticated drawer.

Next:

- route-aware breadcrumbs and active-workspace context;
- connection-state badge and richer sync-status details.

Waiting:

- future advanced modules to justify deeper navigation hierarchy.

## 14. Account, party, and task snapshot sync

Status: implemented
Owner module: `Habitica.Application.Auth`, `Habitica.Api`, and `Habitica.Storage`
Application entry point: `Habitica.WebApp.State.AppSessionController`
Primary Habitica data: authenticated user profile and user task list
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: cached snapshot remains readable offline; refresh requires API access
Rate-limit sensitivity: low because sync is user-initiated only in MVP

### Goal

Validate credentials, fetch the current Habitica user, active party summary when present, and tasks, then persist the latest read-only account, party, and task snapshots locally.

### Inputs

```text
Habitica User ID
Habitica API Token
persistent-storage opt-in
existing cached task snapshot
existing cached user snapshot
existing cached party snapshot
configured x-client value or fallback header strategy
```

### Outputs

```text
authenticated user summary
latest account snapshot
latest party snapshot
latest task snapshot
snapshot timestamps
account freshness state
party freshness state
task freshness state
sign-in or refresh error state
```

### Local storage

Current MVP records:

```text
auth/persistentCredentials
party/latestSnapshot
user/latestSnapshot
tasks/latestSnapshot
```

### API interaction

Current sync flow uses:

```text
GET /user
GET /groups/party
GET /groups/party/members?includeAllPublicFields=true
GET /content?language=en when an active boss quest needs total boss HP
GET /tasks/user
```

The full user response is used for account/dashboard refreshes because Habitica adds computed stat helpers such as `stats.maxHealth`, `stats.maxMP`, and `stats.toNextLevel` only to the full `/user` response. The app still stores only the parsed local projections it needs.

### Algorithm / rules

Current flow:

```text
1. Build authenticated request headers.
2. Validate credentials and load the current account snapshot by reading `GET /user`.
3. If the user snapshot shows an active party, fetch `/groups/party`, visible public members, and content data only when needed for active boss quest total HP.
4. Fetch `/tasks/user`.
5. Persist credentials only if the user selected persistent mode.
6. Persist the latest account snapshot.
7. Persist the latest party snapshot or clear the cached party snapshot when no active party is present.
8. Persist the latest task snapshot.
9. Keep cached local data when refresh fails.
```

### Validation

Reject empty credentials before sending API requests.

If refresh is requested without available credentials, return a visible sign-in-required error.

### Error handling

Normalize API failures into redacted user-visible messages.

Preserve the cached account, party, and task snapshots when sign-in or refresh fails after a previous successful sync.

### Security / privacy

Keep raw token data only in the dedicated credential store.

Do not copy credentials into task snapshots, execution logs, or UI diagnostics.

### Tests

Test:

- credential persistence toggle behavior;
- authenticated request headers;
- `/user` response mapping;
- `/groups/party` response mapping;
- `/tasks/user` response mapping;
- normalized unauthorized response handling;
- account snapshot persistence round-trip;
- party snapshot persistence round-trip;
- task snapshot persistence round-trip.

### Open questions

Current implementation:

- initial sync on sign-in;
- manual refresh action;
- persisted-credential restore on app startup;
- freshness classification for cached tasks;
- cached account snapshot with class, stat, companion, and inventory-summary fields;
- cached party snapshot with summary and quest progress fields when the user belongs to a party;
- successful sign-in refreshes appended into the shared diagnostics journal with redacted metadata.

Next:

- add explicit `429` / `Retry-After` UI handling;
- split sync into per-category result reporting instead of a single task snapshot state.

Waiting:

- project-owned `x-client` header configuration for production deployments.

## 15. Inventory and equipment explorer

Status: implemented
Owner module: `Habitica.Application.Inventory`, `Habitica.Storage`, `Habitica.Api`, and `Habitica.WebApp.Pages.InventoryPage`
Application entry point: `Habitica.WebApp.Pages.InventoryPage`
Primary Habitica data: cached user inventory summary, equipped gear keys, owned gear keys, and Habitica content gear catalog
Mutates Habitica state: yes for explicit battle equip/unequip item actions and battle preset equip actions
Requires confirmation: no for equip; yes for local preset removal
Offline behavior: read-only inventory, equipped gear, and preset views remain available from local snapshots; equip execution requires authentication and fresh user data
Rate-limit sensitivity: medium for preset equip because each changed slot is a separate user-initiated mutation

### Goal

Provide an equipment management page for currently equipped battle gear, local per-user battle presets, and obtained gear. The page resolves gear keys to real names when the cached content catalog is available, shows current-class-adjusted stat totals, and lets users change battle gear through guarded Habitica API mutations. Battle gear is the primary actionable surface; costume gear, accessories, back-slot items, and no-stat items are separated from the actionable equipment list so empty-slot markers and cosmetic-only items are not sent as equip requests.

### Inputs

```text
cached user snapshot
user freshness state
authenticated user id
equipped battle gear keys
owned gear keys
cached gear content catalog
local per-user equipment presets
current pet/mount keys
```

### Outputs

```text
equipped battle gear block
battle gear preset list
visible preset ids for future macro references
preset rename controls
slot-grouped obtained gear panels
folded accessory/no-stat item panels grouped by item type
human-readable gear names with raw-key fallback
gear stat totals
owned gear counts
companion summary
freshness banner
empty-state messaging
snackbar feedback for completed or failed equipment changes
```

### Local storage

Reads:

```text
user/latestSnapshot
inventory/gearCatalog
inventory/equipmentPresets
diagnostics/logEntries
```

Equipment presets are app-local records keyed by Habitica user id. Presets are not synced to Habitica and must not be shared across accounts on the same browser.

### API interaction

Current requests:

```text
GET /content?language=en
POST /user/equip/equipped/:key
GET /user
```

After every successful equip mutation, refresh `/user` and save the refreshed snapshot before updating visible equipped state.

### Algorithm / rules

Current view-model rules:

```text
1. Read the latest cached user snapshot.
2. Read cached gear catalog and local presets for the current Habitica user id.
3. Treat `*_base_0` keys as unequipped-slot markers, not real gear items.
4. Resolve display name, slot, class, notes, and base stats from the catalog when present.
5. Fall back to the raw key when catalog metadata is missing.
6. Apply the current-class 50% gear stat bonus when item class matches user class.
7. Put stat-bearing head, armor, one-handed weapon, shield, and two-handed weapon items into the main actionable battle gear groups.
8. Put back-slot items, no-stat items, and other accessory/cosmetic items into bottom accessory groups by item type.
9. Use catalog `twoHanded` metadata to move two-handed weapons into a separate `Two-Handed Weapons` group after one-handed weapons and shields.
10. Sort groups in slot order and sort keys within each group deterministically.
11. Exclude Back from the equipped battle display and from battle preset item views/execution.
12. For each actionable gear group, compute a `Best in Category` subset by removing items dominated by another item in every stat.
13. A stat value of zero is worse than a positive modifier when another item has equal-or-better values for the remaining stats; exact stat ties remain visible.
14. Show `Best in Category` by default and keep the full per-category item list folded until the user expands it.
15. Keep the bottom non-battle/accessory equipment section folded by default; users can expand it only when they need cosmetic, back-slot, or no-stat details.
16. Sum battle preset stat totals from the resolved item totals.
17. Render each battle preset with its id, compact saved item views, small battle equip buttons for individual preset items, and total battle stats.
```

Equip action rules:

```text
1. Require authenticated credentials.
2. Require a fresh user snapshot.
3. Validate target keys against cached owned gear or currently equipped gear.
4. Execute item equip/unequip immediately through the matching Habitica equip endpoint.
5. Execute preset equip one changed slot at a time in deterministic slot order.
6. Skip unchanged preset slots.
7. Refresh `/user` after changed equip actions.
8. Write diagnostics log entries for success and failure.
9. Show non-blocking snackbar feedback and update equipped badges from the refreshed snapshot.
10. Reject `*_base_0` empty-slot markers before any Habitica API request.
11. Ignore Back slots in battle preset save and equip flows.
```

Battle preset removal and rename are local-only. Removal requires a confirmation prompt because future Macros may reference preset ids. Rename preserves the preset id so existing future macro references can remain stable.

### Validation

Show explicit states for:

- no cached account snapshot;
- empty owned-gear cache;
- fresh account snapshot;
- stale account snapshot;
- expired account snapshot;
- duplicate preset names for the same user and preset kind;
- empty preset rename values;
- missing owned gear for equip targets;
- unequipped-slot marker keys such as `back_base_0`;
- missing authenticated credentials for mutating actions.

### Error handling

Show cached inventory/equipment data even when a previous refresh attempt failed.

Equip failures leave cached state visible, write an `Inventory` diagnostics log entry, and show snackbar feedback.

### Security / privacy

Do not expose raw credentials or request headers. Diagnostics metadata may include preset ids, preset names, previous preset names, item keys, equipment kind, changed slot counts, skipped slot counts, request counts, and failed slot names, but never API tokens.

### Tests

Test:

- grouping by slot prefix;
- battle equipped markers;
- catalog name resolution and raw-key fallback;
- current-class stat totals;
- battle preset stat totals;
- local per-user preset storage and duplicate-name validation;
- stable preset ids and preset rename;
- preset removal;
- base-slot marker normalization;
- battle preset Back-slot removal;
- best-in-category gear selection by non-dominated stat comparison;
- two-handed weapon parsing and separate group ordering;
- accessory/no-stat grouping;
- folded non-battle equipment rendering;
- item equip and preset equip controller dispatch;
- empty-state rendering;
- inventory route navigation rendering;
- diagnostics logging for inventory actions.

### Open questions

Current implementation:

- dedicated `Inventory` route in the app shell;
- equipped battle gear block;
- local battle gear preset list;
- preset save, rename, full-preset equip, individual preset-item equip, and confirmed remove actions;
- slot-grouped obtained gear explorer with folded full item lists;
- bottom accessory/no-stat item explorer grouped by item type and folded by default;
- gear content catalog name/stat resolution;
- battle equipped markers;
- battle equip buttons on owned gear cards;
- snackbar feedback for equipment changes;
- companion summary cards for the cached account snapshot.

Next:

- add slot filters and sort controls;
- surface quest and consumable inventory details beyond aggregate counts.

Waiting:

- Macro execution remains out of scope; inventory presets are stored with stable ids so future Macros can reference them.

## 16. Party explorer

Status: implemented
Owner module: `Habitica.WebApp.Pages.PartyPage`
Application entry point: `Habitica.WebApp.Pages.PartyPage`
Primary Habitica data: cached party group summary, quest state, party members, member CRON fields
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: fully available from the cached party snapshot
Rate-limit sensitivity: none without explicit refresh

### Goal

Provide a read-only party overview that surfaces the latest cached party name, summary, member count, quest progress, party-member CRON state, buff timing recommendations, and local CRON statistics without exposing group mutations.

### Inputs

```text
cached user snapshot
cached party snapshot
party freshness state
party quest summary
party member CRON summary
party CRON history
```

### Outputs

```text
party summary cards
member count
quest progress snapshot
party pending boss damage/items, boss HP remaining, total boss HP when available, and pending damage to party
CRONed X/Y summary
buff timing recommendations
party member CRON list
viewer-local CRON statistics graph
freshness banner
no-party empty state
```

### Local storage

Reads `party/latestSnapshot`, `party/cronHistory`, and `user/latestSnapshot`.

### API interaction

None directly. The page consumes local state prepared by the sync workflow.

### Algorithm / rules

Current display rules:

```text
1. If the cached user snapshot has no party id, render a no-party state.
2. If a party id exists but no party snapshot exists, render a refresh-required state.
3. Show the latest cached party name, summary, and member count.
4. Show quest key, active state, party pending boss damage or collection items when member progress is available, boss HP remaining, total boss HP when content data is available, pending damage to party, and participant count when a quest snapshot exists.
5. Show a dedicated CRON summary when member CRON data exists.
6. Show per-member CRON state, last CRON, average CRON time, and active-quest pending damage/items when available. Keep day-start/timezone diagnostics out of the main row because Habitica usually hides those public member fields.
7. Show viewer-local CRON graph points and low-confidence warnings from local history.
```

### Validation

Show explicit states for:

- no active party in the cached user snapshot;
- missing cached party snapshot;
- fresh party snapshot;
- stale party snapshot;
- expired party snapshot.

### Error handling

Show cached party data even when a previous refresh attempt failed.

### Security / privacy

Display only the locally cached group summary fields required for the read-only explorer. Do not expose credentials or raw request headers.

### Tests

Test:

- `/groups/party` response mapping;
- party snapshot persistence;
- party page rendering;
- navigation rendering for the `Party` route.

### Open questions

Current implementation:

- dedicated `Party` route in the app shell;
- cached party summary cards;
- cached quest progress snapshot.

Next:

- add party-member explorer with throttled pagination and cancellation;
- surface richer quest metadata and warnings about eventual consistency.

Waiting:

- party mutations remain out of scope until confirmation and audit rules are defined.

## 17. Diagnostics workspace and live integration tests

Status: implemented
Owner module: `Habitica.Application.Diagnostics`, `Habitica.Storage`, and `Habitica.WebApp.Pages.LiveTestsPage`
Application entry point: `Habitica.WebApp.Pages.LiveTestsPage`
Primary Habitica data: authenticated user snapshot, task snapshot, party snapshot, equipped battle gear, diagnostics log entries
Mutates Habitica state: yes for the reversible gear roundtrip only
Requires confirmation: yes for the reversible gear roundtrip
Offline behavior: diagnostics history and filters remain readable offline; live checks and presets require authenticated API access
Rate-limit sensitivity: low because tests are user-launched, sequential, and deliberately small

### Goal

Provide a diagnostics workspace from the UI that validates implemented features against the real Habitica API, exposes curated read-only inspection presets, and keeps a persistent redacted diagnostics journal for cross-feature debugging.

### Inputs

```text
authenticated credentials
current cached user snapshot
current cached party snapshot
current cached task snapshot
cached diagnostics log entries
user acknowledgement for reversible gear mutation
owned gear keys
```

### Outputs

```text
per-test pass/fail/skip results
request counts
human-readable result messages
warning copy for reversible mutations
curated preset response previews
filterable developer-oriented diagnostics console entries
JSONL copy/export for filtered diagnostics entries
updated local snapshots after successful checks
```

### Local storage

Refreshes `user/latestSnapshot`, `party/latestSnapshot`, and `tasks/latestSnapshot` as part of the safe suite and gear roundtrip verification.

Persists a capped `diagnostics/logEntries` journal that stores newest-first redacted diagnostics entries across auth, inventory, preset inspection, and live test workflows.

### API interaction

Current live test flow uses:

```text
GET /user
GET /groups/party
GET /tasks/user
POST /user/equip/equipped/:key
```

Curated presets reuse the same `/user`, `/tasks/user`, and `/groups/party` reads that the implemented account, inventory, task, and party pages already depend on.

### Algorithm / rules

Current workflow rules:

```text
1. Run safe checks sequentially and never in parallel.
2. Reuse the same live `/user` response for account and inventory assertions.
3. Fetch `/groups/party` only when the account snapshot shows an active party.
4. Expose only curated diagnostics presets; do not allow arbitrary request paths from this workspace.
5. Append successful and failed auth, preset, and live test workflow events into the same redacted diagnostics journal.
6. Skip the reversible gear test when no alternate owned supported battle item exists.
7. For the reversible gear test, equip an alternate owned battle item, verify with a fresh `/user`, restore the original item, and verify restoration with another fresh `/user`.
8. If restoration or restore verification fails, report the test as failed and preserve the latest known local snapshot.
9. The diagnostics console filters by feature, severity, and mode.
10. Copy and download actions export the currently filtered entries as JSONL. With no filters, they export all stored entries.
11. The selected entry detail renders structured JSON instead of loose key/value text.
```

### Validation

Require:

- an authenticated session before any live test runs;
- an authenticated session before any curated preset runs;
- explicit user acknowledgement before the reversible gear test is enabled.

Skip:

- party checks when the user has no active party;
- reversible gear checks when no alternate supported battle gear key exists.

### Error handling

Surface per-test and preset failures in the diagnostics result panels.

Attempt to restore the original battle gear in a `finally` path during the reversible mutation test and report cleanup failures explicitly.

Allow diagnostics history to be cleared independently, and clear it together with other local stores when the user invokes the global clear-local-data action.

If browser clipboard or download APIs fail, keep stored diagnostics entries unchanged.

### Security / privacy

Do not display raw credentials or request headers.

Keep all live tests and presets user-initiated. Do not introduce background polling or parallel batch execution through the diagnostics workspace.

Keep diagnostics metadata redacted by default. Do not persist tokens, raw auth headers, or full unrestricted user payloads in the journal.

### Tests

Test:

- safe-suite request reuse and snapshot persistence;
- reversible gear test skip behavior when no alternate item exists;
- reversible gear test restore behavior;
- diagnostics page rendering;
- preset-run rendering and controller dispatch;
- diagnostics journal hydration and clearing;
- JSONL copy/download controls for filtered entries;
- navigation rendering for the `Diagnostics` route.

### Open questions

Current implementation:

- dedicated `Diagnostics` route plus a `Settings` entry point;
- safe suite covering account, inventory, party, and task snapshots;
- reversible gear roundtrip with acknowledgement gate and restore verification;
- curated `/user`, `/tasks/user`, and `/groups/party` diagnostics presets;
- shared diagnostics console with feature, severity, and mode filters;
- copy-all and download controls for JSONL diagnostics export;
- structured selected-entry detail;
- persistent diagnostics logging for sign-in, inventory actions, preset runs, and live tests.

Next:

- add optional live checks for future task mutations with stronger warnings and dry-run summaries;
- add richer diagnostics such as per-step timestamps and redacted raw status codes;
- extend the shared journal to future mutation workflows such as equip actions and skill casts on their dedicated pages.

Waiting:

- any live test or future action check that consumes gold, mana, items, or irreversible state remains blocked until stronger warning and confirmation rules are defined.

## 18. Read-only task workspace

Status: implemented
Owner module: `Habitica.WebApp.Pages.TasksPage` and `Habitica.Application.Tasks`
Application entry point: `Habitica.WebApp.Pages.TasksPage`
Primary Habitica data: local task snapshot
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: fully available from cached task snapshot
Rate-limit sensitivity: none without explicit refresh

### Goal

Provide a read-only task browser that loads from the local snapshot and keeps task scoring out of the MVP.

### Inputs

```text
cached task snapshot
task freshness state
search text
include-completed toggle
```

### Outputs

```text
task groups by type
task cards
task notes
priority and due-date metadata
freshness banner
empty-state messaging
```

### Local storage

Reads `tasks/latestSnapshot`.

### API interaction

None directly. The page consumes local state prepared by the sync workflow.

### Algorithm / rules

Current view-model rules:

```text
1. Read the latest local task snapshot.
2. Filter by search text over task text and notes.
3. Hide completed tasks by default.
4. Group visible tasks in this order: To-Dos, Dailies, Habits, Rewards.
5. Sort items within each group by completion state then text.
```

### Validation

Show explicit states for:

- no cached snapshot;
- empty filter result;
- fresh snapshot;
- stale snapshot;
- expired snapshot.

### Error handling

Show cached data even when a previous refresh attempt failed.

### Security / privacy

The MVP intentionally omits all task mutation controls from this workspace.

### Tests

Test:

- grouping by task type;
- search filtering;
- completed-task filtering;
- freshness banner rendering;
- cached empty-state rendering.

### Open questions

Current implementation:

- grouped task cards;
- search field;
- completed-task toggle;
- freshness banner driven by the shared freshness policy.

Next:

- type filters;
- explicit sort controls;
- larger-data optimizations such as virtualization.

Waiting:

- task scoring, checkoff, and edit flows remain intentionally out of scope until mutation safeguards are designed.

## 19. Task mutation controls

Status: skipped
Owner module: `Habitica.WebApp.Tasks` and `Habitica.Application.Tasks`
Application entry point: `Habitica.Application.Tasks`
Primary Habitica data: live task mutation endpoints and task snapshot state
Mutates Habitica state: yes
Requires confirmation: depends on action
Offline behavior: not available in MVP
Rate-limit sensitivity: medium

### Goal

Allow scoring habits, checking off dailies and to-dos, and other task mutations from the companion client.

### Inputs

```text
selected task
mutation type
fresh task snapshot
live authenticated credentials
```

### Outputs

```text
updated task state
mutation result
partial-success or failure state
execution log
```

### Local storage

Would require mutation logs and task snapshot invalidation metadata.

### API interaction

Would require Habitica mutating task endpoints through `Habitica.Api`.

### Algorithm / rules

Skipped for the initial MVP. The current task workspace is intentionally read-only.

### Validation

Do not implement until:

- mutation confirmation rules are defined;
- stale-snapshot gating is wired end to end;
- partial-success reporting is designed.

### Error handling

Deferred until the mutation workflow exists.

### Security / privacy

Mutating task actions must not ship without the conservative execution rules from `RULES.md` and `TECHNICAL.md`.

### Tests

Deferred until the feature moves out of `skipped`.

### Open questions

Waiting:

- exact MVP mutation scope;
- confirmation UX;
- execution log design.

## 20. Feature status labels

Use these labels consistently:

```text
planned
in-progress
implemented
partial
skipped
blocked
deprecated
removed
```

When changing a status, update the relevant feature section and include the reason when useful.
