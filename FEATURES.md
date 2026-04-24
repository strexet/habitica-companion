# FEATURES.md

Last updated: 2026-04-24
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

Status: planned
Owner module: `Habitica.Rules.BuffTiming`
Application entry point: `Habitica.Application.BuffTiming`
Primary Habitica data: party members, user activity data, login/activity timestamps when available
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: available from latest party/member snapshot
Rate-limit sensitivity: medium; party data should not be polled aggressively

### Goal

Estimate the best time to cast team buffs by finding the median active/login time of party members.

### Inputs

```text
party id
party member list
member display names
member user ids when available
last login or last activity timestamps when available
time zone data when available
local user time zone
snapshot timestamp
```

### Outputs

```text
recommended buff time
median timestamp or median local time bucket
member coverage count
members excluded due to missing data
confidence level
warnings
```

### Local storage

Store party member activity snapshots separately from the current party state.

Suggested records:

```text
party_member_activity_snapshot
party_buff_timing_result
```

### API interaction

Use Habitica API v3 according to `HABITICA_API.md`. Fetch party/group data only through the API client layer.

Do not scrape Habitica pages.

### Algorithm / rules

Normalize all timestamps to UTC before calculations.

Initial algorithm:

```text
1. Collect activity timestamps for all party members with available data.
2. Exclude members with missing or invalid timestamps.
3. Convert timestamps to time-of-day buckets in the selected reference time zone.
4. Compute circular median or closest practical median bucket.
5. Return coverage and confidence.
```

Time-of-day is circular. Avoid naive arithmetic that treats 23:30 and 00:30 as far apart.

### Validation

Warn when:

- less than 50% of members have usable activity data;
- timestamps are older than the configured freshness threshold;
- time zone data is unavailable;
- party size is too small for a stable median.

### Error handling

If party data cannot be fetched, use the latest local snapshot and mark the result stale.

### Security / privacy

Do not expose party member private data beyond what the authenticated user can access through Habitica API.

### Tests

Test:

- circular time median around midnight;
- missing members;
- stale timestamps;
- even and odd member counts;
- single-member party;
- all timestamps missing.

### Open questions

Verify which activity/login fields are available through Habitica API v3 for party members and how privacy settings affect visibility.

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

## 7. Skill macro system

Status: planned
Owner module: `Habitica.Rules.Skills` and `Habitica.WebApp.Macros`
Application entry point: `Habitica.Application.Macros`
Primary Habitica data: user stats, mana, skills, tasks, equipment, inventory, party/quest state
Mutates Habitica state: yes
Requires confirmation: yes
Offline behavior: create/edit/dry-run available offline when snapshots exist; execution requires API access
Rate-limit sensitivity: high

### Goal

Allow users to define and execute declarative macros consisting of gear changes, skill casts, task targeting, and optional state checks.

### Inputs

```text
macro definition
current user snapshot
current task snapshot
current equipment snapshot
current party/quest snapshot
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
skill_macro
skill_macro_step
skill_macro_dry_run
skill_macro_execution_log
skill_macro_execution_step_log
```

### API interaction

Macro execution may call multiple Habitica API endpoints.

All calls must go through `Habitica.Api`.

### Algorithm / rules

Macros must be declarative, not arbitrary code.

Allowed initial step types:

```text
equipGearSet
castSkill
selectBestTask
assertManaAtLeast
assertCurrentClass
refreshSnapshot
stopIfWarning
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

### Validation

Reject macros that:

- contain unknown step types;
- target missing tasks;
- require unavailable gear;
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

Status: planned
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
tasks table
inventory table
gear table
party summary
quest summary
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

None currently.

## 12. Credential setup and validation

Status: planned
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

None currently.

## 13. Feature status labels

Use these labels consistently:

```text
planned
in-progress
implemented
blocked
deprecated
removed
```

When changing a status, update the relevant feature section and include the reason when useful.
