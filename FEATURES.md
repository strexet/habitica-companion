# FEATURES.md

Last updated: 2026-05-26
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

### 3.6 Habitica image asset parity

Status: implemented
Owner module: `Habitica.WebApp.Assets`, `Habitica.WebApp.Components.HabiticaImage`
Application entry point: Dashboard, Inventory, Party, and Spells pages
Primary Habitica data: stable content keys from user, inventory, party quest, gear catalog, and spell view models
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: labels and fixed-size fallbacks remain available when remote images cannot load
Rate-limit sensitivity: low; images are static assets and do not use authenticated API requests

Habitica game entities that official Habitica represents visually now flow through `HabiticaImageAssetResolver` instead of ad hoc page markup. The resolver maps stable keys to official static image URLs, alt text, fallback initials, entity kind, and preferred size. The shared `HabiticaImage` component renders a reserved-size image frame, hides broken images with an inline fallback, and never sends Habitica user id or API token headers.

Current placements:

- Dashboard companion panel shows current pet and mount image slots, and the inventory panel shows compact official category icons for eggs, food, hatching potions, and quest scroll counts.
- Inventory shows official gear thumbnails in battle loadout, best-in-category cards, expanded gear cards, accessory cards, and saved battle preset items. Inventory summary also shows companion and item-count icon chips.
- Party shows a quest image slot in the active quest card and compact quest scroll slots in queue, pool, and recently completed quest records.
- Spells shows official skill icons in spell card headers and gear thumbnails for equipment recommendations.

Layout rules are enforced in `app.css`: image frames have stable small/medium/large dimensions, `object-fit: contain`, pixel-art rendering, responsive wrapping, `min-width: 0` text columns, and fixed fallback boxes to prevent overlap or layout shift.

## 4. Party buff timing optimizer

Status: implemented
Owner module: `Habitica.Domain.Party`, `Habitica.Application.Auth`, `Habitica.Storage`
Application entry point: `Habitica.WebApp.Pages.PartyPage`
Primary Habitica data: party group, party members, member `lastCron`, member HP/MP, member public quest progress, member preferences day start and timezone offset
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
member HP/MP when available
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

## 4.1 Party quest start action

Status: implemented
Owner module: `Habitica.Api`, `Habitica.WebApp.State`, `Habitica.WebApp.Pages.PartyPage`
Application entry point: `Habitica.WebApp.Pages.PartyPage`
Primary Habitica data: party group quest state, shared party quest queue entry
Mutates Habitica state: yes
Requires confirmation: no
Offline behavior: unavailable; requires authenticated Habitica API access and a cached startable party quest
Rate-limit sensitivity: medium; performs one mutation and one party refresh

### Goal

The Party page Active Quest card exposes `Start quest` only when the cached Habitica party quest is inactive, matches the invited shared queue entry, and the current user owns that queue entry or is the current Habitica party leader.

### API interaction

`HabiticaApiClient.StartPartyQuestAsync` sends `POST /groups/party/quests/force-start` with no request body, then `AppSessionController.StartSelectedPartyQuestAsync` refreshes `/groups/party` through `GetPartySnapshotAsync` and reconciles the shared queue entry to active when party sync is available.

### Error handling

Validation failures and Habitica API failures return `PartyQuestActionResult.Failure`; the Party page renders start failures inline on the Active Quest card.

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

## 7. Spells page

Status: implemented
Owner module: `Habitica.Rules.Spells`, `Habitica.Rules.Stats`, `Habitica.Api`, `Habitica.WebApp.State`
Application entry point: `Habitica.WebApp.Pages.SpellsPage`; stats allocation entry point: `Habitica.WebApp.Pages.DashboardPage`
Primary Habitica data: full user snapshot, user stats, mana, buffs, class, level, task snapshot, owned gear, gear content catalog
Mutates Habitica state: yes
Requires confirmation: yes when a Cron-sensitive stat buff is cast before the current Habitica day is started; otherwise no for direct casts
Offline behavior: recommendations, estimates, and the stats table are available from cached snapshots; casting, stat allocation, and equip actions require API access
Rate-limit sensitivity: high for repeated casts

### Goal

Provide a class-specific spell workspace that lets the user inspect available mana, unlocked and locked class spells, default targets, approximate outcomes, and dynamic gear recommendations before casting. The Dashboard owns the current STR/INT/CON/PER table and manual stat allocation when the user has unspent stat points and stat allocation is unlocked.

### Inputs

```text
current user snapshot
current task snapshot
gear content catalog
owned battle gear keys
current battle gear
user class and level
current mana and stat points
current buffs
current active boss quest progress and party pending damage
```

### Outputs

```text
class spell cards
spell availability and mana costs
default target task for task-targeted spells
approximate spell result
card-local boss quest context for boss-damage spells
card-local unspent stat context for stat-sensitive spells
dynamic equipment recommendations
cast progress state
Dashboard stat table with base, equipment, buffs, and effective values
stat allocation request from Dashboard
diagnostics log entries
```

### API interaction

All calls go through `Habitica.Api`.

```text
POST /user/class/cast/:skill
POST /user/class/cast/:skill?targetId=:taskId
POST /cron
POST /user/allocate-bulk
POST /user/equip/equipped/:key
GET /user
GET /tasks/user
```

After successful spell casts, refresh `/user` and `/tasks/user` because mana, HP, XP, GP, buffs, quest contribution, and task values can change. If the user chooses `Start New Day and Cast` from the buff timing warning, run `/cron` first, refresh account/tasks/party state, and only cast after Cron succeeds. After stat allocation from Dashboard, refresh `/user`. Dynamic gear recommendation Equip buttons reuse the existing inventory equip flow and refresh `/user`, which causes spell estimates and `Equipped` button states to be recalculated. User-initiated multi-request spell flows execute sequentially with the configured `Features:HabiticaRequestDelayMilliseconds` pause between Habitica API calls; the default is a conservative 1000 ms UI pacing value, not a documented Habitica limit. `HabiticaApiClient` parses `Retry-After`, `X-RateLimit-Limit`, `X-RateLimit-Remaining`, and `X-RateLimit-Reset`. A `429` response is surfaced as a wait message without raw `429 Too Many Requests` copy, and failed non-idempotent mutations are not replayed automatically. If successful responses report zero remaining requests with a reset time, the client waits before the next request.

### Algorithm / rules

Only spells for the cached user class are shown. Supported spell ids are the current Habitica class skill ids:

```text
wizard: fireball, mpheal, earth, frost
warrior: smash, defensiveStance, valorousPresence, intimidate
rogue: pickPocket, backStab, toolsOfTrade, stealth
healer: heal, brightness, protectAura, healAll
```

Unlock levels are 11, 12, 13, and 14 within each class. Locked spells render with their required level and disabled Cast button.

Task-targeting spells default to the eligible non-reward, non-challenge task with the highest Habitica task `value`/cached task value, displayed as the selected target in the spell description. The user can choose another eligible habit, daily, or to-do from a selector ordered by descending task value with the value printed next to each task. Checked-off Dailies remain eligible because they can still receive task-targeted skills for the day. Completed To-Dos are excluded to avoid surfacing finished one-time work. Reward tasks are not valid skill targets, and challenge tasks are excluded because Habitica does not allow casting skills on challenge tasks. Spells that target self, party, or all tasks do not show a target selector.

Effect estimates are approximation-based. Initial formulas are based on Habitica source spell definitions and cross-checked against the stable Habitica User Data Display Tool's Skills and Buffs behavior. Task spell estimates use the selected task's cached `value`, not task priority. Current spell estimates use base stats plus current battle gear plus the Habitica level bonus plus buffs; unbuffed buff spells use base stats plus current battle gear plus the Habitica level bonus. When Auto equip is enabled, the preview uses the selected dynamic recommendation's battle gear slots as though they were already equipped. Estimates must remain labeled approximate until verified directly against live Habitica behavior for the current source version.

The page does not render top summary cards for class, MP, stat points, or quest state. Mana is shown inside each spell's cast preview. Active boss quest progress and party pending damage render only inside spell cards whose estimate includes boss damage, such as Burst of Flames and Brutal Smash. Unspent stat points render only inside spell cards with stat-sensitive estimates and only when stat allocation is unlocked.

Each spell card owns its dynamic equipment recommendations. Recommendations are transient and not saved as user presets. For spells with one relevant stat, show a primary-stat recommendation. For spells with multiple relevant stats, show primary/secondary stat recommendations and a balanced recommendation. Recommendations include stat-bearing battle accessories such as Head Accessory, Eyewear, Body, and Back. If every non-empty recommended gear slot is already equipped in battle gear, its button is disabled and labeled `Equipped`. Weapon and shield are selected as a pair that honors the catalog `twoHanded` flag: a two-handed weapon is recommended only when its score exceeds the combined score of the best one-handed weapon and the best shield for the same stat priority, and the shield slot is left empty when the two-handed weapon wins. Ties keep the one-handed weapon plus shield.

Dashboard stats are displayed as a single table with base/API stats, equipment-derived stats, active buffs, and effective values. Allocation uses per-stat `+` buttons with an Apply action instead of full-width numeric inputs. Stat allocation is treated as locked before level 10; below that level the Dashboard shows unlock copy instead of an unspent-points prompt, the allocation controls are disabled, and Spells hides stat-point context.

Multi-cast uses a direct Cast button but executes sequentially. The active spell card shows a progress bar and text such as `Casting 2 of 5`. Stop on API failure, cancellation, missing target, or stale local state.

Cron-sensitive stat buffs warn when `UserSnapshot.NeedsCron == true`:

```text
wizard: earth
warrior: defensiveStance, valorousPresence, intimidate
rogue: toolsOfTrade
healer: protectAura
```

The warning is rendered inside the affected spell card to keep the decision close to the Cast button. It explains that the current user's buffs can expire on that user's CRON and that party buffs expire separately for each member on that member's next CRON. Actions are `Cancel`, `Cast anyway`, and `Start New Day and Cast`. The optional `Do not warn again for this Habitica day` checkbox stores a local per-user/per-Habitica-day preference under `preferences/spells/cronWarningSuppression`.

### Validation

Block casting when:

- user is not authenticated;
- account snapshot is missing or not fresh;
- spell id is missing;
- selected spell is locked by level;
- task-targeting spell has no eligible target;
- local mana is below the requested count's total cost.

Block stat allocation when:

- user is not authenticated;
- account snapshot is missing or not fresh;
- cached user level is below 10;
- no points are selected;
- selected allocation exceeds `stats.points`.

### Error handling

Show API and validation errors via non-alert page feedback/snackbar. Persist diagnostics entries for spell casts and stat allocation. Partial multi-cast results must include completed/requested counts.

### Security / privacy

Do not log credentials. Diagnostics metadata may include spell id, target task id, requested count, completed count, and stat allocation counts. Do not log API tokens or full request headers.

### Tests

Test:

- API endpoint shape for cast and allocate-bulk;
- user snapshot mapping for stats, points, and buff flags;
- class spell filtering and unlock levels;
- default task-target selection;
- approximate estimate text;
- dynamic equipment recommendation generation and `Equipped` state;
- session sequential cast orchestration and diagnostics logging;
- stat allocation orchestration and diagnostics logging;
- Spells page rendering, count totals, progress bar, target selection/value ordering, Cast button, Cron-sensitive buff warning, and dynamic equipment recommendations;
- Dashboard stats table, plus-button stat allocation controls, stat unlock guard, and unspent stat warning;
- authenticated navigation link.

### Open questions

Verify exact live API response shape for cast results, `Retry-After` propagation, and current Habitica source formulas before raising estimate confidence.

## 8. Macros Collection and skill macro system

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

Macros are not implemented yet. Inventory presets added by the Inventory page are designed as future macro references. Spells added by the Spells page are also designed as future macro references.

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

Spell references should use stable Habitica spell ids. Text macro shorthand may use:

```text
spell:fireball
spell:pickPocket
spell:healAll
```

Structured macro steps should use:

```json
{ "action": "castSpell", "spellId": "fireball", "targetTaskId": "task-id", "count": 1 }
```

For task-targeting spells, `targetTaskId` may be explicit or supplied by a future `selectBestTask` step. Dynamic spell equipment recommendations are not saved presets and must not be referenced by generated preset ids. Future macros should reference them as strategies, such as `maximize:int`, `maximize:per`, or `balanced:int,per`, then compile them against the current gear snapshot at execution time.

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
Mutates Habitica state: yes for Start New Day; otherwise no
Requires confirmation: yes for Start New Day
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
Mutates Habitica state: yes for Start New Day and manual health-potion purchase
Requires confirmation: yes for Start New Day and health-potion purchase
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
pending damage estimate with included and excluded sources
knockout risk warning when estimated damage is high relative to current HP
manual health-potion confirmation and action result
warnings
sync status
Start New Day confirmation and action result
```

### Local storage

Reads from existing snapshot and derived read-model stores.

### API interaction

Only through explicit refresh actions and the confirmed Start New Day action.

```text
POST /cron
POST /user/buy/potion
GET /user
GET /tasks/user
GET /groups/party
```

### Algorithm / rules

Dashboard must not contain business logic. It displays already computed state and invokes use-case services.

Render `Start New Day` only when the current account snapshot says `NeedsCron == true`. The confirmation copy must explain that Habitica will process missed Dailies, active quest progress, current-user temporary buff expiry, and party buffs per member's next CRON. After confirmed CRON, refresh account/tasks/party state through the session controller and show an inline success or error result in addition to the snackbar.

The pending damage panel uses `PendingDamageEstimateFactory` to combine incomplete Daily estimates and saved active boss quest pending damage. It must show included sources and unavailable sources separately, and must label the result as an estimate based on synced data.

Risk thresholds:

```text
Danger: estimatedDamage >= current HP
Warning: estimatedDamage >= current HP * 0.75
Info: estimatedDamage > 0
```

Health-potion purchase is manual only. It is disabled when the account snapshot is stale, the user is signed out, health is full, or saved gold is below 25 GP. After purchase, refresh `/user` so HP, gold, and the dashboard warning recalculate from server state.

### Validation

Show explicit `fresh` / `stale` / `expired` / `missing` state indicators when snapshots are outdated or unavailable.

When derived stat targets such as max health, max mana, or XP-to-next-level are absent from the cached account snapshot, the dashboard must not render misleading `current / 0` output. Show the current value only and downgrade the explanatory label accordingly.

Stat allocation prompts are shown only when cached user level is 10 or higher and unallocated points are available. Below level 10, the stats panel explains that allocation unlocks at level 10 and keeps allocation controls disabled.

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
- Start New Day confirmation, result feedback, and session-controller refresh contract.
- pending damage estimate sources and risk state.
- health-potion confirmation and session-controller call.

### Open questions

Current implementation:

- responsive app shell;
- sign-in entry route;
- dashboard route with cached account cards;
- dashboard inventory readiness summary;
- dashboard stat cards fall back to current-only rendering when the API snapshot lacks non-zero stat targets;
- dashboard Start New Day confirmation and inline result when current-user Cron is due;
- dashboard pending damage estimate with included/excluded source copy and knockout warning;
- manual health-potion purchase action with confirmation and account refresh;
- task workspace with cached browsing and planned guarded mutations;
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
credential discovery guidance
device-local credential safety explanation
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
3. Show where to find the Habitica User ID and API Token on web, Android, and iOS before submission.
4. State that the API token is password-equivalent and is sent only to Habitica for authentication.
5. Send minimal authenticated request with x-client.
6. Handle 401/403 as invalid credentials.
7. Handle 429 using Retry-After.
8. Store credentials only after successful validation, using the selected storage mode.
9. Do not offer save-unverified mode in MVP.
```

### Validation

Reject empty credentials and invalid UUID-like User ID format where applicable.

Reject persistent-storage requests unless the user explicitly acknowledged the persistence warning.

### Error handling

Do not leak token in error messages.

### Security / privacy

Token is password-equivalent. Never log it.

The sign-in page must say that session-only sign-in is the default, persistent credential storage is optional and device-local, and credentials are not sent to Cloudflare sync, exports, diagnostics, or logs.

### Tests

Test:

- redaction;
- invalid credential flow;
- rate-limit flow;
- clear-data flow;
- session-only mode;
- persistent opt-in flow.
- credential discovery and safety guidance rendering.

### Open questions

Current implementation:

- login form with User ID and API Token fields;
- `/`, `/sign-in`, and `/signin` sign-in entry routes for unauthenticated sessions;
- authenticated visits to `/sign-in` or `/signin` redirect to Dashboard before rendering the sign-in form;
- visible guidance for finding credentials in Habitica Settings/API paths on web, Android, and iOS;
- visible token safety copy explaining local/session storage and non-sharing boundaries;
- compact feature overview for dashboard, party, inventory, spells, task helpers, and local snapshots;
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

## 12.1 Local data portability and encrypted cloud sync

Status: partial
Owner module: `Habitica.Application.Sync`, `Habitica.WebApp.Sync`, `Habitica.WebApp.Pages.SettingsPage`
Application entry point: `Habitica.WebApp.Pages.SettingsPage`
Primary Habitica data: local user, task, party, inventory, gear catalog, diagnostics, and party CRON history snapshots
Mutates Habitica state: no
Requires confirmation: yes when importing over existing local app data
Offline behavior: export and file import work offline; Cloudflare sync requires network access
Rate-limit sensitivity: none for Habitica API because sync does not call Habitica

### Goal

Allow users to move local app data between browsers and devices without exposing Habitica API tokens to Cloudflare or export files.

### Inputs

```text
current local app records
optional Habitica User ID and API Token from the active browser session
uploaded Habitica Tool JSON export file
encrypted cloud sync blob from Cloudflare KV
```

### Outputs

```text
plain JSON export file for user-controlled backups
local import preview with conflicts
merged local records
encrypted Cloudflare sync payload
status messages for export, import, upload, and download
per-section cloud sync status records
```

### Local storage

Portable records:

```text
tasks/latestSnapshot
user/latestSnapshot
inventory/gearCatalog
inventory/equipmentPresets
party/latestSnapshot
party/cronHistory
diagnostics/logEntries
```

Excluded records:

```text
auth/persistentCredentials
```

### API interaction

No Habitica API calls are made by export, import, or Cloudflare sync. Cloud sync calls the local Pages Function endpoints:

Legacy single-blob (backward compat, read-only fallback):

```text
GET /api/sync/{syncId}
```

Per-section split-key (current active path):

```text
GET  /api/sync/{syncId}/sections
GET  /api/sync/{syncId}/section/{sectionKey}
PUT  /api/sync/{syncId}/section/{sectionKey}
```

### Algorithm / rules

Export serializes portable storage keys as raw JSON records. Import validates the bundle schema and unsupported keys before writing data.

When existing local data is present, Settings must show explicit options:

```text
Merge
Keep local
Use remote
Apply section choices
```

Merge keeps local records and merges known append-only collections:

- equipment presets by `id`;
- diagnostics log entries by `id`;
- party CRON history events by `partyId`, `memberId`, and `lastCronUtc`;
- latest snapshots by newer `retrievedAtUtc`.

Use remote imports incoming records with override behavior for the chosen import scope. Keep local skips the incoming record. Apply section choices lets conflicting sections choose merge, keep local, or use remote independently.

Cloud sync derives both the sync identifier and AES-GCM key locally from the active Habitica User ID and API Token. The Cloudflare Pages Function stores only encrypted payloads.

Cloud sync uses per-section encrypted records in Cloudflare KV. Each section maps to one `StorageKeys` portable data key and is uploaded/downloaded independently with its own 2MB limit. Section mapping is defined in `CloudSyncSectionMapping`:

```text
Section             StorageKey                   KV suffix
UserProfile         user/latestSnapshot          user-profile
TasksCurrent        tasks/latestSnapshot         tasks-current
TaskOrderPreferences preferences/taskOrder       task-order-preferences
InventoryCatalog    inventory/gearCatalog        inventory-catalog
SavedPresets        inventory/equipmentPresets   saved-presets
PartyCurrent        party/latestSnapshot         party-current
PartyCronHistory    party/cronHistory            party-cron-history
Diagnostics         diagnostics/logEntries       diagnostics
SyncMetadata        (metadata only)              sync-metadata
```

The `SyncMetadata` section stores `schemaVersion: 2`, upload timestamp, succeeded/failed section keys.

After successful refreshes and app data mutations, the WebApp attempts automatic encrypted sync when Habitica credentials are available. Automatic sync lists remote sections, downloads and merges each into local data, then uploads each local section back to Cloudflare. If no remote sections exist, the system falls back to downloading the legacy single-blob format, imports it via merge, and re-uploads as individual sections (one-time migration). Sections that exceed the 2MB per-section limit are skipped with a diagnostic warning; critical sections (UserProfile, SavedPresets, SyncMetadata) produce error-level diagnostics on failure. Cloud sync failures are logged as warnings and must not fail the original Habitica refresh or equipment/stat/spell action.

Cloud sync section status is kept in session state with section key, direction, status, update time, payload size, and message. Settings shows each syncable section, including skipped, failed, excluded, and conflict states. `Features:CloudSyncExcludedSections` configures sections that should not upload; diagnostics is excluded by default so app logs do not inflate encrypted sync payloads unless explicitly enabled.

Cloud sync work updates the `CloudSync` refresh domain and records the reason (`AppBoot`, `ManualRefresh`, or `MutationCompleted`). These sync states do not use the global busy flag and should surface in Settings or the app bar rather than blanking cached pages.

### Validation

Reject:

- unsupported bundle schema versions;
- unsupported storage keys;
- empty record payloads;
- invalid JSON;
- cloud sync requests when the user is not signed in.

### Error handling

Surface import parse errors, unsupported schema errors, missing cloud data, and Cloudflare upload/download failures in Settings. If import succeeds but cloud upload fails, the local data may already be changed; the UI must report the upload failure instead of pretending cloud sync succeeded.

### Security / privacy

Habitica API tokens are not exported and are not sent to Cloudflare sync endpoints. The API token is used only inside the browser to derive the sync identifier and encryption key. Cloudflare KV stores encrypted JSON and cannot decrypt it without the user's Habitica credentials.

Plain JSON export files are not encrypted. The UI must tell users to keep export files private because snapshots and party history may contain personal or party data.

### Tests

Test:

- credential exclusion from export;
- import conflict preview;
- merge behavior for equipment presets and party CRON history;
- automatic cloud sync after refresh and equipment preset changes;
- Settings controls for export, import, and cloud sync;
- per-section cloud sync statuses and exclusions;
- WebApp build after adding Pages Function assets.

### Open questions

- Token rotation breaks access to data encrypted with the previous API token unless a future migration/export path is added.
- A future provider can replace Cloudflare by implementing the remote sync provider boundary.
- Legacy single-blob data (`sync:{syncId}`) is not automatically cleaned up after section-based migration. It remains readable but is no longer updated. A future cleanup step could delete it once section-based sync is stable.

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
active refresh count
cloud sync activity state
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
2. Show `Dashboard`, `Tasks`, `Inventory`, `Party`, `Spells`, `Settings`, and `Diagnostics` in the drawer once an authenticated session exists.
3. Keep refresh disabled unless authenticated credentials are available for the current session.
4. Surface active refresh or cloud sync state in the top bar without hiding cached page content.
5. Surface the latest workflow error above route content.
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
- sync timestamp rendering when available;
- active refresh and cloud-sync status rendering.

### Open questions

Current implementation:

- `Sign In`, `Dashboard`, `Tasks`, `Inventory`, `Party`, `Spells`, `Settings`, and `Diagnostics` routes;
- `/` resolves after session initialization, sending authenticated sessions to Dashboard and unauthenticated sessions to Sign In;
- saved local credentials are checked before the route body renders, avoiding a sign-in flash for returning authenticated users;
- authenticated drawer order is `Dashboard`, `Tasks`, `Inventory`, `Party`, `Spells`, `Settings`, `Diagnostics`;
- top app bar with refresh action, active refresh count, cloud sync state, and latest sync timestamp fallback;
- responsive drawer navigation shown only after authentication;
- dashboard navigation cards for Tasks, Inventory, Party, and Spells;
- stable Habitica web links for known web routes with no mobile deep links or custom schemes;
- shared error banner;
- cached identity summary in the app shell;
- diagnostics route included in the authenticated drawer.

Next:

- route-aware breadcrumbs and active-workspace context;
- connection-state badge.

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
Rate-limit sensitivity: low-to-medium; sign-in stages reads, manual refresh is page-prioritized, and mutating workflows use configured request pacing

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
per-domain refresh state
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

Current flow uses a staged refresh model:

Sign-in:

```text
1. Build authenticated request headers.
2. Validate credentials by fetching `GET /user` only (AuthenticateMinimalAsync).
3. Persist credentials only if the user selected persistent mode.
4. Persist the latest user snapshot.
5. Show authenticated UI immediately with user profile data.
6. Dispatch remaining domains (Tasks, Party, GearCatalog) via RefreshCoordinator with per-domain callbacks.
7. Each completed domain triggers LoadCachedStateAsync and UI notification.
8. After all domains complete, run cloud sync and party sync in background.
```

Manual refresh (Refresh button):

```text
1. Resolve page route to required domains via RefreshForPageAsync.
2. Assign Visible priority to page-relevant domains, Background to others.
3. Dispatch via RefreshCoordinator with deduplication.
4. Fire-and-forget cloud sync and party sync after API domains complete.
```

Domain mapping for page-first refresh:

```text
/dashboard        -> UserProfile, Tasks, GearCatalog
/tasks            -> Tasks, UserProfile
/party            -> Party, UserProfile
/inventory        -> UserProfile, GearCatalog
/spells           -> UserProfile, Tasks, GearCatalog
(default)         -> UserProfile, Tasks
```

RefreshCoordinator provides:

```text
- Deduplication: concurrent same-domain requests await the existing in-flight task.
- Priority scheduling: Visible domains execute before Background domains.
- Per-domain dispatch: each domain maps to specific API fetch + store save.
- Per-domain callbacks: UI updates incrementally as each domain finishes.
- Diagnostics: domain, reason, priority, duration, error, and deduplication state are logged.
```

Keep cached local data when refresh fails.
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

- initial sync on sign-in via staged `AuthenticateMinimalAsync` (user snapshot only) followed by `RefreshCoordinator` for remaining domains;
- manual refresh action via `RefreshForPageAsync` with page-aware domain priorities;
- `RefreshCoordinator` provides domain-level deduplication, priority scheduling (Visible/Background), and per-domain completion callbacks;
- `DomainRefreshState` per domain tracks fetching status, last refresh timestamp, errors, reason, priority, duration, and deduplication state;
- `SessionViewModel.DomainStates` exposes per-domain status to UI;
- Dashboard shows a compact refresh strip for account, tasks, and gear, plus card-level background refresh notes where a calculation depends on refreshing data;
- mutation methods fire-and-forget cloud sync instead of blocking on it;
- persisted-credential restore on app startup;
- freshness classification for cached tasks, user, and party snapshots;
- cached account snapshot with class, stat, companion, and inventory-summary fields;
- cached party snapshot with summary and quest progress fields when the user belongs to a party;
- successful sign-in refreshes appended into the shared diagnostics journal with redacted metadata.

Next:

- add per-domain error banners beyond the compact status chips.

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

Provide an equipment management page for currently equipped battle gear, local per-user battle presets, and obtained gear. The page resolves gear keys to real names when the cached content catalog is available, shows current-class-adjusted stat totals, and lets users change battle gear through guarded Habitica API mutations. Stat-bearing battle gear, including accessory slots such as Head Accessory, Eyewear, Body, and Back, is the primary actionable surface. Costume gear and no-stat cosmetic items are separated from the individual-item action list, while saved battle presets capture equipped accessory slots so complete loadouts can be restored.

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
folded cosmetic/no-stat item panels grouped by item type
human-readable gear names with raw-key fallback
gear stat totals
before/after battle stat deltas for equip candidates
equipment optimizer goal selector and recommendation preview
optimizer recommendation preset save controls
owned gear counts
bulk sell planner for eggs, food, and hatching potions
bulk sell safe/unsafe explanation rows
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
POST /user/sell/:type/:key
GET /user
```

After every successful equip or sell mutation, refresh `/user` and save the refreshed snapshot before updating visible equipped or inventory state.

### Algorithm / rules

Current view-model rules:

```text
1. Read the latest cached user snapshot.
2. Read cached gear catalog and local presets for the current Habitica user id.
3. Treat `*_base_0` keys as unequipped-slot markers, not real gear items.
4. Resolve display name, slot, class, notes, and base stats from the catalog when present.
5. Fall back to the raw key when catalog metadata is missing.
6. Apply the current-class 50% gear stat bonus when item class matches user class.
7. Put stat-bearing head, head accessory, eyewear, armor, body, one-handed weapon, shield, two-handed weapon, and back items into the main actionable battle gear groups.
8. Put no-stat items and other cosmetic items into bottom accessory groups by item type.
9. Use catalog `twoHanded` metadata to move two-handed weapons into a separate `Two-Handed Weapons` group after one-handed weapons and shields.
10. Sort groups in slot order and sort keys within each group deterministically.
11. Include stat-bearing accessory slots in the equipped battle display and battle preset item views/execution.
12. For each actionable gear group, compute a `Best in Category` subset by removing items dominated by another item in every stat.
13. A stat value of zero is worse than a positive modifier when another item has equal-or-better values for the remaining stats; exact stat ties remain visible.
14. Show `Best in Category` by default and keep the full per-category item list folded until the user expands it.
15. Disable Best in Category equip buttons for already equipped battle items and label them as equipped.
16. Keep the bottom non-battle/accessory equipment section folded by default; users can expand it only when they need cosmetic, back-slot, or no-stat details.
17. Sum battle preset stat totals from the resolved item totals.
18. Render each battle preset with its id, compact saved item views, small battle equip buttons for individual preset items, and total battle stats.
19. Stack battle preset cards vertically at full content width.
20. Highlight the highest positive visible stat values on battle gear, best-in-category items, normal equipment cards, and saved preset item cards; tied highest stats are highlighted together.
21. For individual battle equip candidates, show the stat delta compared with the current battle loadout after applying that item. Two-handed weapons clear the shield slot for the comparison.
22. Build optimizer recommendations from owned stat-bearing battle gear using the selected goal: Balanced, Strength, Intelligence, Constitution, Perception, Boss damage, or Survival.
23. Score one-handed weapon plus shield against two-handed weapon as a pair and clear the shield when the two-handed recommendation wins.
24. Let users equip optimizer recommendations through the same sequential slot equip flow used by presets.
25. Let users save optimizer recommendations as local battle presets with explicit names.
26. Snapshot eggs, food, and hatching potion counts by key in addition to aggregate counts.
27. The bulk sell planner only plans Habitica-supported sell categories: eggs, food, and hatching potions.
28. Bulk sell planning keeps the configured keep count, marks surplus as safe, marks at-or-below-threshold items unsafe, and explains every row.
29. Bulk sell execution requires an explicit confirmation after preview and sells only safe surplus items.
30. Each sell action validates cached ownership, executes the configured count sequentially with request pacing, refreshes `/user`, writes diagnostics, and leaves cached data visible on failure.
```

Equip action rules:

```text
1. Require authenticated credentials.
2. Require a fresh user snapshot.
3. Validate target keys against cached owned gear or currently equipped gear.
4. Execute item equip/unequip immediately through the matching Habitica equip endpoint.
5. Execute preset equip one changed slot at a time in deterministic slot order with the configured `Features:HabiticaRequestDelayMilliseconds` pause between Habitica API calls.
6. Skip unchanged preset slots.
7. Refresh `/user` after changed equip actions.
8. Write diagnostics log entries for success and failure.
9. Show non-blocking snackbar feedback and update equipped badges from the refreshed snapshot.
10. Reject `*_base_0` empty-slot markers before any Habitica API request.
11. Include accessory slots such as Head Accessory, Eyewear, Body, and Back in battle preset save and equip flows.
```

Battle preset removal and rename are local-only. Removal requires a confirmation prompt because future Macros may reference preset ids. Rename preserves the preset id so existing future macro references can remain stable. After save, rename, or removal, encrypted cloud sync uploads the local saved-presets section as the source of truth without first merging the remote saved-presets section, so deleted presets are not resurrected from stale cloud data.

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
- bulk sell count greater than the cached owned count;
- unsupported inventory categories for sell planning.

### Error handling

Show cached inventory/equipment data even when a previous refresh attempt failed.

Equip and bulk sell failures leave cached state visible, write an `Inventory` diagnostics log entry, and show snackbar feedback. Partial bulk sell failures report completed/requested counts.

### Security / privacy

Do not expose raw credentials or request headers. Diagnostics metadata may include preset ids, preset names, previous preset names, item keys, equipment kind, sell item type, changed slot counts, skipped slot counts, completed/requested counts, request counts, and failed slot names, but never API tokens.

### Tests

Test:

- grouping by slot prefix;
- battle equipped markers;
- catalog name resolution and raw-key fallback;
- current-class stat totals;
- battle preset stat totals;
- before/after battle stat deltas;
- optimizer goal recommendation and two-handed handling;
- optimizer equip/save actions;
- bulk sell preview and confirmation;
- bulk sell session refresh/logging;
- local per-user preset storage and duplicate-name validation;
- stable preset ids and preset rename;
- preset removal;
- base-slot marker normalization;
- battle preset accessory-slot persistence;
- best-in-category gear selection by non-dominated stat comparison;
- best-in-category equipped button state;
- two-handed weapon parsing and separate group ordering;
- cosmetic/no-stat grouping;
- folded other-equipment rendering;
- item equip and preset equip controller dispatch;
- full-width vertical battle preset layout;
- highest-stat highlighting, including tied highest stats;
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
- bottom cosmetic/no-stat item explorer grouped by item type and folded by default;
- full-width vertical battle preset layout;
- highest-stat highlighting across battle gear, best-in-category items, normal equipment cards, and saved preset item cards;
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
Primary Habitica data: cached party group summary, quest state, party members, member CRON fields, user quest-scroll inventory, Habitica content quest metadata
Mutates Habitica state: no; mutates shared Cloudflare party quest queue state
Requires confirmation: no
Offline behavior: party overview is available from cached snapshots; shared queue/pool/history requires the Cloudflare party-sync endpoint
Rate-limit sensitivity: low for Habitica reads; shared queue actions use tokenless local party claims through party-sync

### Goal

Provide a party overview that surfaces the latest cached party name, summary, member count, quest progress, party-member CRON state, buff timing recommendations, local CRON statistics, and shared party quest planning data without directly mutating Habitica quest state.

### Inputs

```text
cached user snapshot
cached party snapshot
party freshness state
party quest summary
party member CRON summary
party member HP/MP summary
party CRON history
current user's owned quest scrolls
quest content metadata
shared party quest pool
shared party quest queue and votes
recently completed shared party quests
recent party chat finish signals when no quest is active
party-sync owner/admin/Officer roles
party-sync settings
party-sync kick list
party-sync selected quest expiry metadata
```

### Outputs

```text
party summary cards
member count
quest progress snapshot
party pending boss damage/items, boss HP remaining, total boss HP when available, and pending damage to party
CRONed X/Y summary
buff timing recommendations
compact party member CRON list with HP/MP, sortable low-HP/low-MP modes, and foldable details
viewer-local CRON statistics graph
active quest card with real quest metadata and rewards when cached
quest invitation card with accepted, pending, and rejected response lists before the quest starts
Party and Dashboard warnings with Accept/Reject actions when the current user has not answered a quest invitation
shared quest queue cards with vote counts and voter names
separate Next Quest card for the selected shared queue item
skipped and expired queue-state labels
owner/admin/Officer pin, select, skip, expire, and return-to-queue controls
quest pool cards with owner availability
recently completed quest cards with manual vs automatic source labels and management removal controls
owner/admin/Officer strip after party notes and before active quest state
owner/admin settings controls
member-detail Officer and kick controls for management roles
bottom-page kick list for management roles
party-sync role and kick-list names open the same member details affordance as Active Quest finishing-member names
freshness banner
no-party empty state
```

### Local storage

Reads `party/latestSnapshot`, `party/cronHistory`, `user/latestSnapshot`, `inventory/gearCatalog`, and Cloudflare party-sync quest state.

### API interaction

The page consumes local Habitica snapshots prepared by the sync workflow. Shared quest queue actions go through the application/session controller and Cloudflare party-sync; Razor components do not call Habitica API directly. When the party has no active quest, the sync workflow may fetch recent party chat so it can detect structured Habitica quest-finish messages without parsing localized text.

### Algorithm / rules

Current display rules:

```text
1. If the cached user snapshot has no party id, render a no-party state.
2. If a party id exists but no party snapshot exists, render a refresh-required state.
3. Show the latest cached party name, summary, and member count.
4. Show quest key, active state, party pending boss damage or collection items when member progress is available, boss HP remaining, total boss HP when content data is available, pending damage to party, and participant count when a quest snapshot exists.
5. Show a dedicated CRON summary when member CRON data exists.
6. Show compact per-member cards with display name, class, subtle HP/MP values, CRON state, last CRON, average CRON time, and active-quest pending damage/items when available.
7. Keep member id, level, CRON reason, and stat breakdowns behind a collapsed in-memory details toggle on each member card.
8. Let the member list sort by name, average CRON, latest CRON, pending quest contribution, low HP, low MP, and CRON status. HP/MP sorts are ascending so the lowest current value appears first; unknown values sort last.
9. Show viewer-local CRON graph points and low-confidence warnings from local history.
10. Render party summaries and quest descriptions through `SafeMarkdownRenderer`, including Markdown inline formatting and a small safe HTML subset (`br`, `strong`/`b`, `em`/`i`, `code`) while escaping unsafe tags.
11. Publish the current user's owned quest scrolls to the shared party quest pool after party sync when inventory and content metadata are available.
12. Allow only the current quest owner to add that user's quest scroll to the shared queue.
13. Allow one vote per party member per queued quest; clicking again removes the vote.
14. Sort visible queue cards by lifecycle state, manual pin rank, vote count, queue age, and recently completed penalty. Selected entries render above the queue as the separate Next Quest card instead of occupying a normal queue position.
15. Let the quest owner remove their own queue item unless owner/admin settings restrict queue edits to management roles.
16. Let app admins assign the explicit companion-app party owner from expanded member details; when no explicit owner exists, the Habitica party leader remains the automatic party-sync owner.
17. Let owner/app admins assign and remove Officers from expanded member details.
18. Let owner/app admins update party-sync settings with short labels and direct helper copy for Officer queue management, Officer moderation, limited queue editing, and member auto updates.
19. Let owner/app admins and authorized Officers kick and unkick party-sync users. Kicked users cannot read or write normal party-sync data; owner/app admins bypass kicks to recover from mistakes.
20. Store recently completed shared quests separately from active/queued quests for display and queue-priority penalties.
21. Keep Active Quest "Open in Habitica" links on web URLs only; official mobile app party/quest deep links are documented as unsupported in `docs/HABITICA_DEEPLINKS.md`.
22. Let role-strip and kick-list member names focus the same expanded member details UI used by the Active Quest finishing-member link.
23. When Habitica has a quest invitation that is not active yet, hide progress and finish estimates and show accepted, pending, and rejected member response lists instead; names focus the same expanded member details UI.
24. Show `Invite party` on the Next Quest card. The button is enabled only when fresh party data shows no Habitica quest or invitation and the current user owns the selected item; disabled buttons explain Habitica quest, ownership, queue-state, or refresh requirements. Success sends `POST /groups/party/quests/invite/:questKey`, refreshes party state, and marks the shared queue item `InviteSent`; invite-sent items leave the Next Quest and normal queue views because Habitica now owns the invitation flow.
25. Let users toggle an owned-only queue filter that hides not-owned queue entries and not-owned quest-pool scrolls without mutating shared party-sync data.
26. When a previously active companion-app quest disappears and recent party chat contains a reliable structured completion signal, mark the active shared queue item completed automatically and store an idempotent detection key.
27. When a previously active collection quest disappears and recent party chat contains `info.type = "all_items_found"`, record it as an automatic recently completed quest even if it was never in the shared queue. Boss completions require `info.type = "boss_defeated"` and a matching `info.quest`.
28. Label recently completed entries as `Marked manually` or `Auto-detected`, including the local detecting user when available. Party-sync owner, app admins, and Officers can remove completed entries from this history.
29. Let party-sync owner/admin/authorized Officers pin and unpin queue entries; pinned entries sort ahead of unpinned entries inside the same queue state.
30. Let party-sync owner/admin/authorized Officers select a queued, skipped, or expired entry as Next Quest. If another entry is already selected, selecting a different entry first returns the previous Next Quest to the top of the normal queue, then shows the newly selected entry in the Next Quest card.
31. Keep queue additions, votes, and removals available while Next Quest is selected. The selected item is removed from the normal queue list and shown in its own card above the queue.
32. Show Next Quest entries with their expiry time when available. The Next Quest card can return the item to the top of the queue. Show skipped and expired entries as readable states with `Return to queue`; selected entries can be skipped, and non-active entries can be expired manually.
33. Expire selected entries deterministically after 72 hours. Expire queued or skipped entries when the matching owner/quest scroll has not appeared in the party quest pool for 30 days. Expiry runs during party-sync reads and queue mutations.
```

### Validation

Show explicit states for:

- no active party in the cached user snapshot;
- missing cached party snapshot;
- fresh party snapshot;
- stale party snapshot;
- expired party snapshot;
- empty shared quest queue;
- empty shared quest pool;
- queued quests with and without votes.

### Error handling

Show cached party data even when a previous refresh attempt failed. Shared party queue failures are reported through the session error/snackbar and do not hide the local party overview.

### Security / privacy

Display only the locally cached group summary fields required for the explorer. Do not expose credentials or raw request headers. Shared party-sync sends a tokenless local claim to Cloudflare instead of Habitica API tokens. The local claim is token-private but trust-based; the Worker keeps access proof parsing separate from role/action checks so a future tokenized manager-invite proof can replace local claims if needed.

### Tests

Test:

- `/groups/party` response mapping;
- party snapshot persistence;
- party page rendering;
- compact member card details toggling;
- quest reward metadata rendering from cached Habitica content;
- navigation rendering for the `Party` route.
- shared quest queue/pool rendering;
- party-sync queue and vote mutations.
- party-sync queue selection, pinning, skip, expiry, and requeue actions.
- safe markdown and supported inline HTML rendering for party and quest descriptions.
- local party-sync claims that do not pass API tokens to Cloudflare.
- Officer/settings/kick visibility and management actions.

### Open questions

Current implementation:

- dedicated `Party` route in the app shell;
- cached party summary cards;
- cached quest progress snapshot.
- compact party member cards with foldable extra info and stats;
- subtle HP/MP values and low-HP/low-MP member sorting;
- active quest card with real cached quest metadata and compact rewards;
- inactive quest invitation response lists before quest progress exists;
- party detail section order of summary, party-sync roles, party-sync settings, then active quest state;
- party-sync settings labels and helper descriptions for non-technical party members;
- shared quest pool from published member quest-scroll availability;
- shared quest queue with owner-only add/remove and one-vote-per-member voting;
- shared queue invite action, management pin/select/skip/expire/requeue controls, separate Next Quest card, and owned-only queue/pool filter;
- Party/Dashboard quest-invitation warnings and Accept/Reject actions;
- recently completed shared quest history table, UI, and management removal;
- automatic recent-completion detection from structured Habitica party chat signals.

Next:

- add party-member explorer with throttled pagination and cancellation;
- replace party-sync credential verification with a tokenless party membership proof.

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

- add optional live checks for task mutations with stronger warnings and dry-run summaries;
- add richer diagnostics such as per-step timestamps and redacted raw status codes;
- extend the shared journal to future mutation workflows such as equip actions and skill casts on their dedicated pages.

Waiting:

- any live test or action check that consumes gold, mana, items, or irreversible state must use the shared guarded-mutation confirmation rules before execution.

## 18. Task workspace

Status: implemented
Owner module: `Habitica.WebApp.Pages.TasksPage` and `Habitica.Application.Tasks`
Application entry point: `Habitica.WebApp.Pages.TasksPage`
Primary Habitica data: local task snapshot
Mutates Habitica state: yes for inline task scoring/checkoff controls
Requires confirmation: no for browsing; task mutations follow section 19 confirmation and progress rules
Offline behavior: browsing is fully available from cached task snapshot; mutations require authentication and fresh task/user data
Rate-limit sensitivity: none for browsing; medium for task mutation controls

### Goal

Provide a task workspace that loads from the local snapshot for scanning, filtering, and detail review, with guarded live scoring/checkoff controls for common task actions.

### Inputs

```text
cached task snapshot
task freshness state
search text
selected task types
sort mode
task statistics period
per-category folded preferences
per-category completed visibility preferences
per-task-type display order preferences
```

### Outputs

```text
task groups by type
foldable task group sections
per-category Show completed / Hide completed controls
task cards
task notes
numeric task value
subtle value-based card background for open tasks
muted completed-task styling
priority and due-date metadata
freshness banner
type filters
sort control
inline task scoring/checkoff controls
habit multi-score progress
task detail panel
task statistics summary
task-history histogram
month activity chart
expanded task compact activity charts
drag-handle task reordering
task-card move to top/up/down/bottom controls
empty-state messaging
```

### Local storage

Reads:

```text
tasks/latestSnapshot
preferences/tasksPage/{userId}
preferences/taskOrder
```

Writes:

```text
preferences/tasksPage/{userId}
preferences/taskOrder
```

### API interaction

Browsing consumes local state prepared by the sync workflow. Mutating actions must go through `Habitica.Api` and the session/application layer described in section 19.

### Algorithm / rules

Current view-model rules:

```text
1. Read the latest local task snapshot.
2. Filter by search text over task text and notes.
3. Hide completed tasks by default.
4. Group visible tasks in this order: To-Dos, Dailies, Habits, Rewards.
5. Sort items within each group by completion state, then the selected sort mode: name, highest value, lowest value, or due soon.
6. Keep group fold state and completed visibility separately for each task type.
7. Persist task-page preferences by user id on the current device.
8. Show the numeric task value when available.
9. Tint open task cards with a continuous low-saturation value gradient from warm negative values to cool positive values.
10. Render completed tasks with neutral muted styling when the category is set to show completed.
11. Render task actions inline on each card when authentication and freshness allow mutation.
12. For Habit scoring, clamp multi-score count to 1-20 and show determinate progress while requests execute sequentially.
13. Apply saved per-type task order after filtering/sorting; unknown saved IDs are ignored and new task IDs append after ordered known IDs.
14. Drag handles move items within the currently visible list and persist the resulting per-type ID order for export/import and cloud sync.
15. Drag reordering is scoped to the current task type group and preserves hidden or completed items that are filtered out of the visible subset.
16. Focused drag handles support arrow-key reordering through the same local ordering path for keyboard precision.
17. Each task card also renders move to top, move up, move down, and move to bottom buttons that use the same local ordering path and disable edge moves that would not change the visible order.
18. Parse cached task history points when Habitica returns them and keep them attached to task snapshots.
19. Let users switch task statistics between week, month, and year periods.
20. Render aggregate task-history and month-activity charts from cached history without requiring a live refresh.
21. Render smaller history and month-activity charts inside expanded task details only, so task cards remain scannable by default.
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

Task mutation controls are allowed in this workspace when they use the conservative execution rules in section 19 and do not log credentials or raw request headers.

### Tests

Test:

- grouping by task type;
- search filtering;
- completed-task filtering;
- per-category completed visibility;
- foldable task groups;
- persisted user task-page preferences;
- numeric task value rendering;
- type filtering;
- week/month/year task statistics;
- task-history and month activity chart rendering;
- explicit sort modes;
- drag-handle task reordering;
- persisted per-type task order;
- task scoring/checkoff action rendering;
- habit multi-score request and progress wiring;
- freshness banner rendering;
- cached empty-state rendering.

### Open questions

Current implementation:

- grouped task cards;
- search field;
- per-category completed-task controls;
- persisted per-user folded category and completed visibility preferences;
- numeric task value display;
- continuous value-based open-task card tinting;
- muted completed-task styling;
- type filters and explicit sort modes;
- drag-handle task reordering with per-type order persistence;
- inline Complete/Uncomplete controls for Dailies and To-Dos;
- inline positive/negative Habit scoring with count and determinate progress;
- compact cached task detail panel;
- freshness banner driven by the shared freshness policy.

Next:

- richer expandable task details and history/stat surfaces;
- larger-data optimizations such as virtualization.

## 19. Task mutation controls

Status: partial
Owner module: `Habitica.WebApp.Tasks` and `Habitica.Application.Tasks`
Application entry point: `Habitica.Application.Tasks`
Primary Habitica data: live task mutation endpoints and task snapshot state
Mutates Habitica state: yes
Requires confirmation: yes for multi-step, repeated, destructive, or ambiguous actions; single checkoff/score actions may use inline buttons with clear labels and undo-aware result feedback when supported
Offline behavior: not available for execution; dry-run/disabled previews may render from cached snapshots
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

Requires mutation diagnostics and task snapshot invalidation metadata. Existing diagnostics logging may be used for the initial implementation.

### API interaction

Task mutations are available for implementation through `Habitica.Api`. Supported endpoint shapes must be documented in `HABITICA_API.md` before wiring UI controls.

### Algorithm / rules

Implement conservatively:

```text
1. Require authenticated credentials.
2. Require fresh task data and fresh user data when HP/MP/XP/GP can change.
3. Validate the selected task still exists in the local snapshot.
4. Render action-specific controls only when the task type supports the action.
5. For Habit multi-score, execute requests sequentially with visible completed/total progress.
6. Stop on the first API failure and report completed/requested counts.
7. Refresh `/user` and `/tasks/user` after successful mutation because stats, rewards, task values, and quest progress can change.
8. Persist diagnostics metadata for task id, task type, action, requested count, completed count, and request count.
```

### Validation

Before mutation:

- block when unauthenticated;
- block when task snapshot is missing, stale, or expired;
- block when account snapshot is missing, stale, or expired and the action can affect user stats/resources;
- block unsupported habit directions;
- clamp multi-score counts to a small explicit limit;
- explain disabled actions near the affected task card.

### Error handling

Show validation/API errors through the same non-alert feedback pattern used by spells and inventory. Partial multi-score results must include completed/requested counts and leave the page refreshed from the latest available cached state.

### Security / privacy

Mutating task actions must not ship without the conservative execution rules from `RULES.md` and `TECHNICAL.md`.

### Tests

Test:

- API endpoint shape for task score/checkoff mutations;
- session orchestration, refresh, partial-progress handling, and diagnostics logging;
- Tasks page action rendering by task type and supported direction;
- disabled state for stale/missing snapshots;
- habit multi-score count clamping and progress display.

### Open questions

- exact Habitica endpoint behavior for uncompleting Dailies/To-Dos;
- whether a completed To-Do can be safely uncompleted through the public API in all cases;
- whether first implementation should include only scoring/checkoff or also task editing.

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
