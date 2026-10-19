# Features

Last updated: 2026-06-09
Primary audience: AI agents and senior developers
Primary Habitica integration reference: `skill-tree/documents/habitica-api.md`
Related technical reference: `skill-tree/documents/technical.md`

## 1. Purpose

This document describes project features at a technical level.

AI agents must update this file when adding, materially changing, or removing a feature. Feature descriptions must be implementation-oriented and useful for future agents that need to continue the work without rediscovering domain intent.

Do not use this document for marketing copy. Keep it dry, technical, and explicit.

`skill-tree/documents/future.md` owns proposed, planned, and not-yet-implemented work. This file should describe implemented or partial current behavior only. Do not keep proposed-only feature specs here.

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

Features must use the shared snapshot freshness model from `skill-tree/documents/technical.md`:

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
- feature-specific thresholds may be stricter than `skill-tree/documents/technical.md`, but not looser for mutating actions.

### 3.6 Habitica image asset parity

Status: Implemented
Owner module: `Habitica.WebApp.Assets`, `Habitica.WebApp.Components.HabiticaImage`
Application entry point: Dashboard, Equipment, Party, and Spells pages
Primary Habitica data: stable content keys from user, inventory, party quest, gear catalog, and spell view models
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: labels and fixed-size fallbacks remain available when remote images cannot load
Rate-limit sensitivity: low; images are static assets and do not use authenticated API requests

Habitica game entities that official Habitica represents visually now flow through `HabiticaImageAssetResolver` instead of ad hoc page markup. The resolver maps stable keys to official static image URLs, alt text, fallback initials, entity kind, and preferred size. The shared `HabiticaImage` component renders a reserved-size image frame, hides broken images with an inline fallback, and never sends Habitica user id or API token headers.

Current placements:

- Dashboard uses official gear thumbnails in the Start New Day equipment preview; companion and inventory summaries live on their dedicated pages instead of the Dashboard.
- Equipment shows official gear thumbnails in battle loadout, best-in-category cards, expanded gear cards, accessory cards, and saved battle preset items. Equipment summary also shows companion and item-count icon chips.
- Quests shows a quest image slot in the active quest card and compact quest scroll slots in queue, pool, and recently completed quest records.
- Spells shows official skill icons in spell card headers and gear thumbnails for equipment recommendations.

Layout rules are enforced in `app.css`: image frames have stable small/medium/large dimensions, `object-fit: contain`, pixel-art rendering, responsive wrapping, `min-width: 0` text columns, and fixed fallback boxes to prevent overlap or layout shift.

## 4. Party CRON rhythm tracker

Status: Implemented
Owner module: `Habitica.Domain.Party`, `Habitica.Application.Auth`, `Habitica.Storage`
Application entry point: `Habitica.WebApp.Pages.PartyPage`
Primary Habitica data: party group, party members, member `lastCron`, member HP/MP, member public quest progress, member preferences day start and timezone offset
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: available from latest party snapshot and local CRON history
Rate-limit sensitivity: medium; party data should not be polled aggressively

### Goal

Track party-member CRON rhythm from read-only party data. The Party page shows member-level CRON state, average member CRON time, and the viewer-local CRON graph when stored history exists, but it does not show a dedicated overview CRON summary or buff-timing recommendation block.

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
party pending boss damage/items when available
per-member CRON state and average CRON time
viewer-local graph buckets
member coverage count
members excluded due to missing data
```

### Local storage

Store current party state separately from historical CRON events.

```text
party/latestSnapshot
party/cronHistory
```

### API interaction

Use Habitica API v3 according to `skill-tree/documents/habitica-api.md`. Fetch party/group data only through the API client layer.

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
- Party page member average rendering and absence of the removed overview CRON summary;
- missing members;
- all timestamps missing.

### Open questions

Verify how consistently `lastCron`, `preferences.dayStart`, and timezone offsets are returned for all party members under different privacy settings.

## 4.1 Party quest start action

Status: Implemented
Owner module: `Habitica.Api`, `Habitica.WebApp.State`, `Habitica.WebApp.Pages.PartyPage`
Application entry point: `Habitica.WebApp.Pages.PartyPage`
Primary Habitica data: party group quest state, shared party quest queue entry
Mutates Habitica state: yes
Requires confirmation: no
Offline behavior: unavailable; requires authenticated Habitica API access and a cached startable party quest
Rate-limit sensitivity: medium; performs one mutation and one party refresh

### Goal

The Quests page Active Quest card exposes `Start quest` only when the cached Habitica party quest is inactive, matches the invited shared queue entry, and the current user owns that queue entry or is the current Habitica party leader.

### API interaction

`HabiticaApiClient.StartPartyQuestAsync` sends `POST /groups/party/quests/force-start` with no request body, then `AppSessionController.StartSelectedPartyQuestAsync` refreshes `/groups/party` through `GetPartySnapshotAsync` and reconciles the shared queue entry to active when party sync is available.

### Error handling

Validation failures and Habitica API failures return `PartyQuestActionResult.Failure`; the Quests page renders start failures inline on the Active Quest card.

## 5. Gear set management

Status: Partial
Owner module: `Habitica.WebApp.Pages.InventoryPage`, `Habitica.Storage`, `Habitica.WebApp.State`
Application entry point: `Habitica.WebApp.Pages.InventoryPage`
Primary Habitica data: user equipment, owned gear, current gear, class, stats
Mutates Habitica state: yes when equipping a set
Requires confirmation: yes for one-click equip if multiple slots change
Offline behavior: set creation and preview available offline; execution requires API access
Rate-limit sensitivity: medium for multi-slot changes

### Goal

Allow users to create named combat/utility gear sets from owned equipment and equip them with one action.

Current implementation supports local battle presets on Equipment: save current battle gear, save optimizer recommendations as presets, rename/remove presets, show stable preset ids, and equip changed preset slots sequentially. Broader macro-oriented gear-set references remain future work.

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

Use Habitica API v3 equipment endpoints as documented in `skill-tree/documents/habitica-api.md`.

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

Status: Partial
Owner module: `Habitica.Rules.Equipment`, `Habitica.Rules.Spells`, `Habitica.Application.Inventory`
Application entry point: `Habitica.WebApp.Pages.InventoryPage`, `Habitica.WebApp.Pages.SpellsPage`
Primary Habitica data: owned gear, gear stats, user class, user stats, skill formulas
Mutates Habitica state: no by itself
Requires confirmation: no
Offline behavior: available from latest inventory/equipment snapshot
Rate-limit sensitivity: low

### Goal

Recommend the best owned gear for a selected skill or action.

Current implementation covers Equipment optimizer goals and per-spell dynamic equipment recommendations. A generic reusable optimizer surface for every skill/action remains planned.

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

Status: Implemented
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
GET /groups/party
```

After successful spell casts, refresh `/user` and `/tasks/user` because mana, HP, XP, GP, buffs, quest contribution, and task values can change. After a party-targeted spell, also refresh `/groups/party` so cached party-member HP, MP, and buff rows reflect the cast. A failed post-cast party refresh keeps the successful cast result and previous cached party snapshot, writes a warning diagnostic, and tells the user that party refresh needs retry. Self- and task-targeted spells do not add that party request. If the user chooses `Start New Day and Cast` from the buff timing warning, run `/cron` first, refresh account/tasks/party state, and only cast after Cron succeeds. After stat allocation from Dashboard, refresh `/user`. Dynamic gear recommendation Equip buttons reuse the existing inventory equip flow and refresh `/user`, which causes spell estimates and `Equipped` button states to be recalculated. User-initiated multi-request spell flows execute sequentially with a one-second cancellable `Preparing...` stage before the first Habitica request, plus any extra fixed delay configured by `Features:HabiticaRequestDelayMilliseconds`; the fixed app delay default is 0 ms because `HabiticaApiClient` performs adaptive token-bucket throttling and honors server rate-limit headers. `HabiticaApiClient` enforces a default 350 ms minimum spacing between Habitica requests when no explicit `Habitica:MinRequestSpacingMilliseconds` override exists, parses `Retry-After`, `X-RateLimit-Limit`, `X-RateLimit-Remaining`, and `X-RateLimit-Reset`, and keeps failed non-idempotent mutations from replaying automatically. A `429` response is surfaced as a wait message without raw `429 Too Many Requests` copy. If successful responses report zero remaining requests with a reset time, the client waits before the next request.

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

Effect estimates are approximation-based. Initial formulas are based on Habitica source spell definitions and cross-checked against the stable Habitica User Data Display Tool's Skills and Buffs behavior. Task spell estimates use the selected task's cached `value`, not task priority. Current spell estimates use base stats plus current battle gear plus the Habitica level bonus plus buffs; unbuffed buff spells use base stats plus current battle gear plus the Habitica level bonus. When Auto equip is enabled, the preview uses the selected dynamic recommendation's battle gear slots as though they were already equipped. Healing Light caps its preview by the current user's missing HP when fresh user health is available. Blessing shows the raw source-backed HP restored per party member as the primary preview, shows a per-member total when multiple casts are selected, caps each known party member's effective value by that member's missing HP when fresh party health is available, and keeps aggregate party HP restored out of the default copy. If most covered members are already near full HP, Blessing shows a non-fatal limited-value or low-need warning near Cast; if all covered members are full, it says no meaningful healing is needed. Missing or stale party HP produces a short uncertainty note without inventing effective healing. Estimates must remain labeled approximate until verified directly against live Habitica behavior for the current source version.

The page renders a sticky current-mana bar above the spell cards, showing available MP, max MP, and current class while the user scrolls. Per-card mana previews show the selected cast count's total cost and after-cast MP. `Spend All Mana` sets the count to the maximum affordable count, clamped to the page's 99-cast maximum, and never starts casting or sends a Habitica request. It is disabled for locked spells, stale/missing account data, unaffordable spells, and invalid mana costs. Active boss quest progress and party pending damage render only inside spell cards whose estimate includes boss damage, such as Burst of Flames and Brutal Smash. Unspent stat points render only inside spell cards with stat-sensitive estimates and only when stat allocation is unlocked.

Each spell card owns its dynamic equipment recommendations. Recommendations are transient and not saved as user presets. For spells with one relevant stat, show a primary-stat recommendation. For spells with multiple relevant stats, show primary/secondary stat recommendations and a balanced recommendation. Options are ordered by estimated gained spell output from highest to lowest, making the first option the default when Auto equip is enabled. When multiple options exist, show an `Auto equip option` selector so choosing another option updates the preview and cast equip plan; hide the selector for a single option. Recommendations include stat-bearing battle accessories such as Head Accessory, Eyewear, Body, and Back. If every non-empty recommended gear slot is already equipped in battle gear, its button is disabled and labeled `Equipped`. Weapon and shield are selected as a pair that honors the catalog `twoHanded` flag: a two-handed weapon is recommended only when its score exceeds the combined score of the best one-handed weapon and the best shield for the same stat priority, and the shield slot is left empty when the two-handed weapon wins. Ties keep the one-handed weapon plus shield.

Dashboard stats are displayed as a single table with base/API stats, equipment-derived stats, active buffs, and effective values. Allocation uses per-stat `+` buttons with an Apply action instead of full-width numeric inputs. Stat allocation is treated as locked before level 10; below that level the Dashboard shows unlock copy instead of an unspent-points prompt, the allocation controls are disabled, and Spells hides stat-point context.

Multi-cast uses a direct Cast button but executes sequentially. After final confirmation, including any CRON warning decision, the active spell card immediately shows `Preparing...` and a card-local `Cancel` button for about one second before any gear equip, spell cast, refresh, or local snapshot mutation. The active spell card then shows a progress bar and text such as `Casting 2 of 5`. Cancel is available on the active card only; other spell count, target, auto-equip, recommendation, Spend All Mana, and Cast controls are disabled while the run is active. Cancellation during preparation performs no Habitica requests and reports `Casting cancelled before it started.` Cancellation after partial completion stops before later requests where possible, reports completed/requested counts, preserves successful Habitica mutations, and refreshes affected snapshots when any request was already sent. Stop on API failure, cancellation, missing target, or stale local state.

Cron-sensitive stat buffs warn when `UserSnapshot.NeedsCron == true`:

```text
wizard: earth
warrior: defensiveStance, valorousPresence, intimidate
rogue: toolsOfTrade
healer: protectAura
```

The warning is rendered inside the affected spell card to keep the decision close to the Cast button. It explains that the current user's buffs can expire on that user's CRON and that party buffs expire separately for each member on that member's next CRON. Actions are `Cancel`, `Cast anyway`, and `Start New Day and Cast`. When due unfinished Dailies exist, a collapsed due-count disclosure reveals the shared compact checkoff list without leaving the pending cast context. The optional `Do not warn again for this Habitica day` checkbox stores a local per-user/per-Habitica-day preference under `preferences/spells/cronWarningSuppression`.

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
- dynamic equipment recommendation generation, gained-output ordering/default selection, healing overheal caps and theoretical-maximum fallbacks, and `Equipped` state;
- session sequential cast orchestration and diagnostics logging;
- party-targeted cast refresh persistence, non-party cast request counts, and preserved cast success when the post-cast party refresh fails;
- stat allocation orchestration and diagnostics logging;
- Spells page rendering, sticky current-mana bar, count totals, progress bar, target selection/value ordering, Cast button, Cron-sensitive buff warning, and dynamic equipment recommendations;
- Dashboard stats table, plus-button stat allocation controls, stat unlock guard, and unspent stat warning;
- authenticated navigation link.

### Open questions

Verify exact live API response shape for cast results, `Retry-After` propagation, and current Habitica source formulas before raising estimate confidence.

## 8. Best task selector for skill casting

Status: Partial
Owner module: `Habitica.Rules.Spells`; generic future home may be `Habitica.Rules.Tasks`
Application entry point: `Habitica.Rules.Spells.SpellViewModelFactory`
Primary Habitica data: tasks, task values, task types, task tags, user stats, selected skill
Mutates Habitica state: no by itself
Requires confirmation: no
Offline behavior: available from latest task snapshot
Rate-limit sensitivity: low

### Goal

Find the best daily, habit, todo, or reward target for a selected skill or action. Current implementation is limited to the Spells page: `SpellViewModelFactory` filters valid cached task targets, chooses the first/best cached target for task-targeted spells, and exposes per-target estimates. A reusable selector with user-defined exclusions and alternate ranking remains planned.

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

Status: Implemented
Owner module: `Habitica.WebApp.Pages.PetsMountsPage`
Application entry point: `Habitica.WebApp.Pages.PetsMountsPage`
Primary Habitica data: cached eggs, food, and hatching potions
Mutates Habitica state: yes when selling
Requires confirmation: yes
Offline behavior: recommendation available offline; execution requires API access
Rate-limit sensitivity: high for many sell operations

### Goal

Recommend and optionally sell surplus eggs, food, and hatching potions while preserving a user-defined keep count. The current implementation lives on Pets & Mounts after being moved out of Inventory.

### Inputs

```text
inventory snapshot
user-defined keep rules
```

### Outputs

```text
sell candidates
keep candidates
risk warnings
execution plan
```

### Local storage

```text
none; keep count is page-local state
```

### API interaction

Selling is mutating. Use Habitica API v3 through `Habitica.Api`.

Do not execute bulk sale without explicit user confirmation.

### Algorithm / rules

Initial planner:

```text
1. Load cached eggs, food, and hatching potions.
2. Apply the user keep-count threshold to each cached item key.
3. Mark items with count above the threshold as safe surplus.
4. Produce a dry-run preview with owned count, sell count, and safety explanation.
5. Require explicit confirmation before sending sell requests.
```

### Validation

Block sale when:

- user keep threshold would be violated;
- inventory snapshot is stale;
- the item type is outside eggs, food, or hatching potions.

### Error handling

Sell sequentially by default.

Persist partial success and refresh inventory after execution.

### Security / privacy

No credentials in logs. Inventory can reveal user progression; do not send raw inventory to external telemetry.

### Tests

Test:

- keep-count surplus calculation;
- confirmation gate;
- supported sell item types;
- partial sale failure;
- stale snapshot warning.

### Open questions

Confirm exact sell endpoints and whether Habitica supports safe item sale batching.

## 10. Skill/action result estimator

Status: Partial
Owner module: feature-specific rules/application factories; generic future home may be `Habitica.Rules.Calculations`
Application entry point: feature-specific application factories
Primary Habitica data: user stats, class, buffs, gear, tasks, party/quest state, skill metadata
Mutates Habitica state: yes for Start New Day; otherwise no
Requires confirmation: yes for Start New Day
Offline behavior: available from latest snapshots
Rate-limit sensitivity: low

### Goal

Display approximate results for skills and actions before execution. Current implementation is feature-specific: Dashboard estimates CRON damage and health-potion recovery, Spells estimates spell effects and resource costs, Equipment estimates stat deltas, and party/quest views estimate completion timing. A generic reusable estimator layer remains planned.

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

Status: Partial
Owner module: `Habitica.WebApp.Pages.DashboardPage`, `Habitica.Application.Dashboard`, `Habitica.Rules.Equipment`, and `Habitica.WebApp.State.AppSessionController`
Application entry point: `Habitica.WebApp.Pages.DashboardPage`
Primary Habitica data: user, tasks, party, gear catalog, quest, sync metadata
Mutates Habitica state: yes for Start New Day, CRON health-potion purchase, and gem-for-gold purchase
Requires confirmation: yes for Start New Day, health-potion purchase, and gem-for-gold purchase
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
task-count summary
pending damage estimate with included and excluded sources
knockout risk warning when estimated damage is high relative to current HP
compact CRON health-potion recovery estimate, confirmation, and action result
gem-for-gold confirmation and action result
warnings
sync status
Start New Day confirmation and action result
```

### Local storage

Reads from existing snapshot and derived read-model stores.

### API interaction

Only through explicit refresh actions and confirmed mutation actions.

```text
POST /user/equip/equipped/:key
POST /cron
POST /user/buy/potion
POST /user/purchase/gems/gem
GET /user
GET /tasks/user
GET /groups/party
```

### Algorithm / rules

Dashboard must not contain business formulas. It displays state computed by rules/application factories and invokes use-case services.

Render `Start New Day` only when the current account snapshot says `NeedsCron == true`. The confirmation copy must explain that Habitica will process missed Dailies, active quest progress, current-user temporary buff expiry, and party buffs per member's next CRON. After confirmed CRON, refresh account/tasks/party state through the session controller and show an inline success or error result in addition to the snackbar.

Start New Day offers recommended temporary battle gear before CRON and enables that option by default. `Habitica.Rules.Equipment.EquipmentRecommendationFactory` builds the compact summary-first preview from cached owned gear and the gear catalog. Goals are `INT for mana`, `CON for less damage`, and `Survival`; all are marked assumption-based because the final CRON mana and damage calculations are server-side. The preview shows current stats, recommended stats, deltas, whether the recommended gear is already equipped, and expandable recommended-item rows. When enabled, `AppSessionController.StartNewDayAsync(StartNewDayRequest)` validates recommended gear ownership, captures the current battle slots, equips changed recommended slots sequentially without an intermediate user refresh, runs CRON only after successful gear steps, restores the captured battle slots sequentially, then refreshes account/tasks/party state. If temporary equip or CRON fails after a gear change, the controller attempts to restore the changed slots before reporting the failure. Failure messages distinguish skipped-before-CRON, failed-while-CRON-running, post-CRON restore, and completed-but-refresh-failed states.

The Start New Day panel renders a compact CRON damage summary between gear optimization and confirmation, then renders contextual health-potion recovery when it can improve the immediate CRON outcome, followed by `CronUnfinishedDailiesMiniList` when confirmed due unfinished Dailies exist. Each compact Daily row scores that Daily up through `AppSessionController.ScoreTaskAsync`; stale task snapshots replace row actions with a link to the shared Refresh control. The Spells CRON warning reuses the same component behind a collapsed due-count disclosure so a pending cast keeps its context.

`PendingDamageEstimateFactory` builds the Start New Day damage summary from confirmed due unfinished Dailies and saved active boss quest pending damage. Task refresh asks Habitica to recompute Daily due state through `/tasks/user?dueDate=<current UTC timestamp>`, then preserves returned `isDue` in cached task data. `PendingDamageEstimateFactory.GetIncompleteDailies` is the shared Daily selector for the Dashboard CRON mini list, the CRON damage estimate, and Spells CRON warnings: Daily, incomplete, and `isDue == true`. Explicit `isDue: false` Dailies and missing/unknown `isDue` Dailies are excluded from the numeric estimate; unknown due-state Dailies appear only in collapsed estimate details. Daily damage uses the current official source-backed CRON scoring shape from cached task value, task priority, computed Constitution, buffs, level bonus, and battle gear when the gear catalog is available. Missing task value, missing computed stat inputs, unavailable checklist partial-completion data, unavailable negative Habit damage, and incomplete Inn/pause state lower estimate readiness instead of being hidden. Current-user Inn/resting damage exemption is only typed when party member data exposes the current user as resting; otherwise the summary remains an estimate and does not claim exact final CRON damage. The standard Dashboard no longer renders a standalone pending-damage card; detailed source and unavailable-source copy is available only inside the Start New Day section.

Risk thresholds:

```text
Danger: estimatedDamage >= current HP
Warning: estimatedDamage >= current HP * 0.75
Info: estimatedDamage > 0
```

Health-potion purchase is manual only and appears on Dashboard only inside the Start New Day panel. `HealthPotionRecoveryEstimateFactory` uses the Start New Day damage estimate, current HP, maximum HP, and saved gold to show current HP, HP after CRON, 15 HP healing, 25 GP cost, maximum useful potion count, affordable count, and the minimum recommendation that can remove knockout risk when the estimate is complete. It never recommends more potions than are useful or affordable, does not show recovery at full health, and avoids a guaranteed-safe claim for incomplete damage estimates. The button buys one Health Potion through the existing `POST /user/buy/potion` client route, is disabled when the account snapshot is stale, the user is signed out, health is full, or saved gold is below 25 GP, and refreshes `/user` after purchase so HP, gold, and the CRON recovery estimate recalculate from server state. The standard Dashboard no longer renders a standalone Manual Recovery card.

Bulk armoire purchase is manual only. The Dashboard shows this spend-gold card before the gem-for-gold card. Its requested opening count defaults to the maximum available from current gold, capped by the app's guarded bulk limit, then lets the user reduce it before purchase.

Gem-for-gold purchase is manual only. Signed-in users with a cached account snapshot see the Dashboard card even when purchase is unavailable; the card explains stale account data, insufficient gold, reached monthly limit, missing subscription-plan data, or the no-subscription state in plain app language. When the cached plan has no active subscription signal, the card includes a `Subscribe` link to Habitica's subscription settings. The Dashboard defaults requested quantity to the maximum available from current gold and the remaining monthly limit, then lets the user reduce it before confirming. The action requires explicit confirmation, executes sequential one-gem requests with stop-on-failure, refreshes `/user` after success, and reports updated gem balance when the refreshed snapshot includes it.

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
- Start New Day confirmation, default-enabled temporary gear optimization preview/request, gear restoration, result feedback, and session-controller refresh contract.
- Start New Day damage summary, source details, unknown due-state handling, and risk state.
- CRON health-potion recovery visibility, recommendation, confirmation, and session-controller call.
- gem-for-gold eligibility visibility, quantity clamp, confirmation, success refresh, partial-failure stop, and user snapshot mapping.
- dashboard companion navigation uses local `Open` actions for app pages, keeps `Open Habitica` as the only external Habitica web destination, shows it in the Companion links header on desktop, and moves it below the local companion cards on phone widths.

### Open questions

- Final server-side CRON damage, mana, and HP outcomes remain Habitica-owned; local dashboard values stay estimate-based.
- Current-user Inn/resting quest-damage exemption is still incomplete in the cached account projection.

## 12. Credential setup and validation

Status: Partial
Owner module: `Habitica.WebApp.Pages.SignIn`, `Habitica.Application.Auth`, `Habitica.Api`, and `Habitica.Storage`
Application entry point: `Habitica.WebApp.State.AppSessionController.SignInAsync`
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

Actual token storage must use the credential storage abstraction defined in `skill-tree/documents/technical.md`.

Raw token data must live only in the dedicated credential store. Ordinary app stores may keep redacted credential metadata only.

Current records:

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
5. Keep form values synchronized on typing, paste, browser automation fill, and normal field change before submit.
6. Send minimal authenticated request with x-client.
7. Handle 401/403 as invalid credentials.
8. Handle 429 using Retry-After.
9. Store credentials only after successful validation, using the selected storage mode.
10. Do not offer save-unverified mode.
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
- input-event, change-event, and empty-submit sign-in form behavior.

### Open questions

- Client-side validation currently covers missing values; malformed-ID validation remains a possible improvement.
- API-client initialization requires the configured project-owned `x-client`; missing or blank configuration raises a configuration error before network access.

## Optional ChatGPT enrollment status

Status: Partial; optional enrollment and OAuth linking are implemented and disabled by default. `SettingsPage` calls `IAppSessionController`; the application integration contract delegates HTTP to `ChatGptIntegrationClient`. Empty `ChatGptIntegration:AuthBaseUrl` disables enrollment actions. Configured enrollment requires a signed-in account and explicit consent before uploading credentials to Auth for Habitica validation. Browser-only sign-in, backup, private sync and party sync retain existing credential boundaries.

The integration client retains profile management credentials only in tab memory, clears management state on logout/account changes, and excludes management data from app state/export/sync. Code generation produces a 12-character, single-use, ten-minute code. Disconnect and profile deletion require a dedicated confirmation. Failed or partial cleanup retains management access for an explicit retry; ambiguous enrollment requires account revalidation. Integration HTTP calls have a fifteen-second timeout and no automatic mutation retry. Offline enrollment is unavailable. Enrollment validates `/api/v3/user`; linking and cleanup mutate integration state without mutating Habitica data.

The Auth consent page verifies the code, displays the selected account and requested scopes, then requires explicit approval. The page permits form redirects to the callback origin taken from the validated authorization request, while the server retains exact redirect-URI and browser-binding checks. CSP wildcard/directive characters in callback origins are refused. OAuth uses PKCE S256 and exact MCP resource audience with two-profile isolation. Local Worker fixtures and WebApp client/controller/component tests qualify consent, cleanup, account changes, expiry, races and denial. The Codex in-app browser qualifies synthetic code verification and the disabled Settings/Privacy surface. The owner reports initial OAuth completion in ChatGPT and Codex, with ChatGPT tool discovery and two successful reads. ChatGPT repeated-request continuity retesting remains open after the reported failure. Native Codex task/account reads, immediate request reuse and an explicitly approved Companion-only Todo save/read/receipt lookup pass. Complete deployed-client token lifecycle, revocation and broader rollout qualification remain open. The [integration contract](companion-first-mcp-research.md#proposed-chatgpt-enrollment-privacy-and-retention-contract) owns privacy and retention defaults; hosting logs and backups require activation review.

Connected integration Settings explains the client-linking handoff beside the code controls: Codex `Authenticate` opens the browser authorization page; the `One-time Companion code` field accepts the code; `Verify code` and `Allow selected access` complete the reviewed grant. The guide covers ChatGPT, code expiry and single-use recovery without changing consent or authorization behavior. Code status repeats the exact destination and replacement action.

## 12.1 Local data portability and encrypted cloud sync

Status: Partial
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
preferences/taskOrder
preferences/colorSchemes
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
- latest snapshots by newer `retrievedAtUtc`;
- color-scheme preferences: custom schemes union by `id` with newer `updatedAtUtc` winning, and `selectedSchemeId` follows the side with the newer `selectedAtUtc` (a built-in selection is just the id; custom schemes ship their full token bundles).

Use remote imports incoming records with override behavior for the chosen import scope. Keep local skips the incoming record. Apply section choices lets conflicting sections choose merge, keep local, or use remote independently.

Cloud sync derives both the sync identifier and AES-GCM key locally from the active Habitica User ID and API Token. The Cloudflare Pages Function stores only encrypted payloads.

Cloud sync uses per-section encrypted records in Cloudflare KV. Each section maps to one `StorageKeys` portable data key and is uploaded/downloaded independently with its own 2MB limit. Section mapping is defined in `CloudSyncSectionMapping`:

```text
Section             StorageKey                   KV suffix
UserProfile         user/latestSnapshot          user-profile
TasksCurrent        tasks/latestSnapshot         tasks-current
TaskOrderPreferences preferences/taskOrder       task-order-preferences
ColorSchemes       preferences/colorSchemes     color-schemes
InventoryCatalog    inventory/gearCatalog        inventory-catalog
SavedPresets        inventory/equipmentPresets   saved-presets
PartyCurrent        party/latestSnapshot         party-current
PartyCronHistory    party/cronHistory            party-cron-history
Diagnostics         diagnostics/logEntries       diagnostics
SyncMetadata        (metadata only)              sync-metadata
```

The `SyncMetadata` section stores `schemaVersion: 2`, upload timestamp, succeeded/failed section keys.

After successful refreshes and app data mutations, the WebApp attempts automatic encrypted sync when Habitica credentials are available. Automatic sync lists remote sections, downloads and merges each into local data, then uploads each local section back to Cloudflare. Narrow preference saves such as manual task order and color-scheme preference changes can request a single-section upload for their mapped encrypted cloud-sync section after local persistence. If no remote sections exist, the system falls back to downloading the legacy single-blob format, imports it via merge, and re-uploads as individual sections (one-time migration). Sections that exceed the 2MB per-section limit are skipped with a diagnostic warning; critical sections (UserProfile, SavedPresets, SyncMetadata) produce error-level diagnostics on failure. Cloud sync failures are logged as warnings and must not fail the original Habitica refresh, equipment/stat/spell action, or local preference save.

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
- automatic cloud sync after refresh, equipment preset changes, and manual task-order changes;
- Settings controls for export, import, and cloud sync;
- per-section cloud sync statuses and exclusions;
- WebApp build after adding Pages Function assets.

### Open questions

- Token rotation breaks access to data encrypted with the previous API token unless a future migration/export path is added.
- A future provider can replace Cloudflare by implementing the remote sync provider boundary.
- Legacy single-blob data (`sync:{syncId}`) is not automatically cleaned up after section-based migration. It remains readable but is no longer updated. A future cleanup step could delete it once section-based sync is stable.

## 12.2 Color scheme system

Status: Implemented
Owner module: `Habitica.WebApp.Theme`, `Habitica.WebApp.Components.ColorSchemePanel`, `Habitica.WebApp.Pages.SettingsPage`, `Habitica.WebApp.Pages.DashboardPage`, `Habitica.WebApp.Pages.SignIn`, and `Habitica.Storage`
Application entry point: `Habitica.WebApp.Theme.ColorSchemeService`
Primary Habitica data: none
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: fully local; selected scheme applies without network
Rate-limit sensitivity: none

### Goal

Centralize app color choices behind semantic color-scheme tokens and let users choose or edit palettes without changing code.

### Inputs

```text
developer built-in color schemes
saved color-scheme preferences
user-created custom schemes
browser-local active scheme cache
```

### Outputs

```text
semantic CSS variables on document root
Settings color-scheme picker
custom scheme editor
portable color-scheme preferences
cloud-sync color-schemes section
```

### Local storage

Portable user data:

```text
preferences/colorSchemes
```

Fast reload cache:

```text
localStorage habitica-tool/colorScheme/selectedId
localStorage habitica-tool/colorScheme/activeScheme
localStorage habitica-tool/colorScheme/preferences
```

### Algorithm / rules

Built-in developer-editable schemes live in `ColorSchemeCatalog`. Current built-ins are:

```text
Gryphy (Light)
Gryphy (Dark)
Forest Legacy
Frosted Cake
Arcane Wraith
Phantom Fair
Toxic Swamp
Green Menace
Abyssal Blackwater
Obsidian Glow
Blessed Skyhaven
Infernal Covenant
Midnight Tavern
Dragonfire Keep
Frost Healer
Sunlit Stable
Mosswood Quest
Potion Shop
Boss Battle
Quiet Ledger
Celestial Inn
Treasure Vault
Mana Spring
Stonewatch Sanctuary
```

`Gryphy (Light)` and `Gryphy (Dark)` are the light/dark defaults. Every scheme carries explicit light/dark variant metadata, and the picker groups defaults, built-in light schemes, built-in dark schemes, custom schemes, and the session-only generated scheme in that order. `Forest Legacy` preserves the former `alpha` palette. Stored selections migrate removed built-in IDs to their replacement or the matching Gryphy default.

Schemes expose semantic tokens rather than page-specific colors: background, card background, card border, text, muted text, primary, accent, danger, success, focus, shadow, surface, strong surface, chart colors, task-value min/base/max colors, app header, navigation drawer, input, filled-button text, and disabled-state colors. Optional gradient tokens cover page, card, app bar, drawer, primary button, secondary button, and accent chip surfaces. Optional heading, app-bar, and drawer text shadows remain null unless a scheme opts in.

`ColorSchemeService` reads `preferences/colorSchemes`, resolves the active built-in or custom scheme, applies it through `HabiticaColorScheme.applyAndStore`, and persists the full active scheme in browser `localStorage`. The fast cache also stores the normalized preferences as a fallback for mobile browsers where IndexedDB can be delayed or unavailable during navigation. `wwwroot/js/colorSchemes.js` runs before Blazor starts and reapplies the cached active scheme to avoid a visible wrong-theme flash after reload. After persisted appearance changes, `ColorSchemePanel` awaits best-effort encrypted sync of only the `ColorSchemes` section and displays local-save success separately from cloud failure or skipped sync. Appearance sync downloads and timestamp-merges the remote section before upload. Whole-data and narrow uploads share a serialized session lane; account replacement or sign-out cancels obsolete work before remote results can import or upload. Imports and local appearance saves share a storage mutation lock. Timestamp ties retain local selection under the existing merge contract; server KV supplies no globally atomic cross-device convergence guarantee. Transient random themes, rerolls, chaos changes, paste previews and cancelled edits do not upload.

Imported saved appearance updates the active CSS, mounted selectors and fast reload cache without creating a new selection timestamp. Imported preferences update the cache while a transient random/pasted preview retains the current rendered colors. Dirty editor fields remain intact; an inline conflict offers explicit synced-theme selection or continued editing before saving. Theme listeners unsubscribe when panels unmount. Manual bundle/section imports and automatic private sync use the same appearance refresh path.

Preference ownership: saved color selections/custom schemes and manual task ordering are portable encrypted sections. Task filters/page preferences, Pets & Mounts folds, spell-warning suppression, appearance disclosure state and session-only previews remain device-local. Tab management credentials, server enrollment, background-executor consent and cloud-section exclusion state do not become portable appearance preferences. Connection status recovery has a separate task contract.

The app overrides MudBlazor app bar, drawer, button, progress, and disabled-state colors from the same semantic CSS variables. `wwwroot/js/colorSchemes.js` also derives readable drawer text, filled-button text, app-bar text, and native form-control `color-scheme` values from active tokens so drawer links, buttons, shell text, and number-input steppers remain readable across light and dark schemes. Native checkboxes and radios use the scheme primary via a global `accent-color` rule. The sign-in hero deliberately keeps the app-bar background/text pairing even though it is also a card surface, and its feature chips use translucent shell styling instead of filled accent surfaces. Multi-corner gradients are painted into tiny canvas images once per scheme application and exposed as composed CSS variables; two-stop gradients use CSS `linear-gradient`. New built-in or custom schemes must keep shell/control tokens readable, not only page-card colors.

Task value backgrounds use a logarithmic intensity curve across the scheme's task min/base/max tokens. The three task tokens should be same-hue shades: small absolute values use the base shade, negative values move toward min, and positive values move toward max without introducing unrelated red/orange/green/blue card colors.

The color-scheme controls live in a shared `ColorSchemePanel` component embedded on the Settings page, the Dashboard page, and the Sign-in page, so users can recolor the app without leaving the dashboard and can try themes before signing in. The Dashboard uses the panel's `Compact` mode: a single dense bar (scheme select, small swatch strip, Random Preset, Random Theme) with advanced controls (copy/paste preset editor, random-theme save) collapsed behind a "Customize" disclosure toggle that auto-expands when a random-save flow is active. When that compact customization surface is open, its close action reads `Done` and hides the surface without reverting the current draft or generated theme. The Dashboard appearance section sits near the top of the page (just under the hero), collapsed by default behind a "Customize theme"/"Done" fold toggle so it is visible without dominating the page. The Settings appearance section uses the same collapsed-by-default fold toggle, but reveals the full (non-compact) panel with advanced controls always visible. Sign-in renders the full panel directly. Opening the advanced panel always reveals the custom-scheme editor directly: a saved custom scheme edits in place, and a built-in active scheme opens as an unsaved draft copy ready to tweak (no intermediate "Create Custom Copy" step). The panel lets users:

- choose a built-in scheme;
- build a fully custom palette by copying the active preset to the clipboard as readable v2 JSON, editing colors, optional gradients, text shadows, and the light/dark variant in any text editor, and pasting it back. Paste also accepts the legacy flat token shape. Pasting applies the colors to the live theme immediately as a transient preview; Save Scheme persists them and Cancel reverts the preview to the previously applied scheme;
- paste manually into an inline text box when the browser blocks `navigator.clipboard.readText` (iOS Safari and non-secure origins) — the panel reveals a JSON textarea with Apply/Cancel buttons and shows a specific error reason when the pasted text is empty, not valid JSON, not a JSON object, or carries no recognized color tokens;
- rename custom schemes and set their light/dark variant;
- delete custom schemes;
- reset by choosing any built-in scheme;
- roll a random preset (a random pick from built-in plus custom schemes), which is selected and persisted like any other scheme;
- roll a random theme (generated random colors), which is held in memory for the app session and applied without persisting;
- drag a chaos slider (Calm to Madness) before rerolling to scale hue and saturation divergence, from a calm single-hue palette up to a chaotic, high-saturation, multi-hue one (still valid, with readability guards strongest at calm/moderate settings and relaxed only near Madness);
- switch to other schemes and return to the last random theme through a "Generated" entry in the scheme dropdown;
- name and save the last random theme into the custom schemes list.

The random theme is generated as a palette around a random base hue with light/dark base, contrasting text tokens, valid CSS color/shadow values, and deterministic gradients for every gradient-capable surface. The chaos level (0..1, surfaced as the slider) scales solid and per-corner hue/saturation divergence, from subtle directional shading at 0 to deliberately clashing gradients and a heading glow at high chaos. It also relaxes readability guards: calm and moderate themes enforce stronger contrast for card/body text, shell text, and primary/secondary filled-button labels across generated gradient stops, while high chaos allows wilder combinations without dropping button labels to same-luminance collapse except at the extreme Madness end. A random theme is never written to `preferences/colorSchemes` until the user explicitly saves it with a name, so the persisted selection never points at the transient `random-theme` id. Because `ColorSchemeService` is a scoped (per-app) service, the pending random theme survives navigation between pages within a session.

User-created custom schemes are stored only in user data. Built-in schemes are not stored as editable records and cannot be deleted.

### Validation

Custom scheme names are trimmed and bounded. Custom token values must be supported CSS color/shadow values. Invalid values are rejected before saving and the broken scheme is not applied.

Color must not be the only state cue. Danger, warning, success, stale, conflict, and task-value states still need text labels, icons, copy, or layout context.

### Tests

Test:

- built-in schemes start with Gryphy Light/Dark defaults, carry balanced light/dark variants, and exclude removed legacy IDs;
- legacy built-in IDs migrate to supported replacements and persist the migrated selection;
- built-in schemes define app shell, button, disabled, and input tokens;
- missing legacy custom tokens are backfilled before validation or application;
- invalid custom token values are rejected;
- custom variant, gradient, and text-shadow fields survive save/reload and readable copy/paste round trips;
- readable v2 paste accepts legacy aliases and legacy flat-token payloads while rejecting conflicting aliases and partial gradients;
- CSS keeps shell, inputs, buttons, disabled controls, and reported nested surfaces routed through scheme tokens;
- CSS/JS keep drawer links, number counters, determinate progress bars, quest estimate alerts, task-value card backgrounds, and diagnostics warning panels routed through active scheme tokens;
- color-scheme preferences are portable user data;
- cloud sync maps `ColorSchemes` to `preferences/colorSchemes`;
- persisted appearance changes request a narrow `ColorSchemes` cloud-sync upload, while transient generated themes and paste previews stay local until saved;
- color-scheme cross-device merge unions custom schemes by `id` with newer `updatedAtUtc` winning, and `selectedSchemeId` follows the side with the newer `selectedAtUtc`;
- the editor card is shown directly when the advanced panel is open (no "Create Custom Copy" intermediate), a built-in active scheme opens as an unsaved draft, and pasting applies a live preview that Cancel reverts to the previously applied scheme;
- Settings renders scheme controls and saves custom scheme preferences;
- a generated random theme passes token validation across many seeds at low, moderate, and maximum chaos, with direct contrast checks for calm/moderate button labels and card text;
- random preset selection excludes the active scheme and the transient random id;
- the panel exposes random preset and random theme controls, generating a random theme does not persist the `random-theme` id, and saving a named random theme stores it as a custom scheme;
- compact mode hides advanced controls behind a disclosure toggle but auto-reveals random-save controls, and a high-chaos theme remains saveable as a valid custom scheme.

## 13. App shell and navigation

Status: Implemented
Owner module: `Habitica.WebApp.Layout` and `Habitica.WebApp.Components.Navigation`
Application entry point: `Habitica.WebApp.State.AppSessionController`
Primary Habitica data: local session state, task snapshot freshness, sync timestamp, diagnostics history
Mutates Habitica state: no
Requires confirmation: no
Offline behavior: fully available from local session, snapshot, and diagnostics stores
Rate-limit sensitivity: none by itself

### Goal

Provide a stable responsive PWA shell with routes for sign-in, dashboard, tasks, Equipment (`/inventory`), Pets & Mounts, party, quests, spells, settings, diagnostics, and privacy, while keeping the foldable feature drawer hidden until an authenticated session exists.

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
sync failure and stale snapshot predicates
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
compact sync status chip
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
2. Show `Dashboard`, `Tasks`, `Equipment`, `Pets & Mounts`, `Party`, `Quests`, `Spells`, `Settings`, and `Diagnostics` in the drawer once an authenticated session exists.
3. Keep refresh disabled unless authenticated credentials are available for the current session.
4. Surface compact sync state in the top bar:
   - `Synced h:mm AM/PM` for normal cached sync freshness;
   - `Sync stale` when cached task, account, or party freshness is stale/expired;
   - `Sync failed` when a tracked refresh domain has a last error;
   - `Cloud syncing` for active app-data sync.
5. Replace the Refresh button with a same-row `Syncing ...` chip while page refresh domains are active, using a specific domain label for one active area and a count for multiple areas.
6. Keep header identity text, sync status, and refresh/status action aligned with ellipsis or wrapping instead of overflowing on narrow widths.
7. Surface the latest workflow error above route content.
```

### Validation

Handle these states explicitly:

- no local data;
- cached data but no active authenticated session with the drawer still hidden;
- authenticated session with latest sync timestamp;
- failed refresh with cached data still available;
- cached diagnostics history without an active authenticated session.
- direct visits to authenticated data routes while signed out, with page-local Sign in actions and copy that does not suggest disabled Refresh.

### Error handling

Do not hide controller-level errors inside route components. Surface them once in the shell.

### Security / privacy

Never display raw API headers or token material in the shell.

### Tests

Test:

- authenticated navigation links;
- unauthenticated drawer suppression;
- shell error banner rendering;
- compact sync timestamp rendering without full date text;
- stale, failed, active refresh, and cloud-sync status rendering.
- signed-out empty states for Dashboard, Tasks, Equipment, Pets & Mounts, Party/Quests, and Spells.

### Open questions

Current implementation:

- `Sign In`, `Dashboard`, `Tasks`, `Equipment`, `Pets & Mounts`, `Party`, `Quests`, `Spells`, `Settings`, and `Diagnostics` routes;
- `/` resolves after session initialization, sending authenticated sessions to Dashboard and unauthenticated sessions to Sign In;
- signed-out pages expose a visible header `Sign in` link to `/sign-in`; authenticated sessions retain the Refresh/status action;
- saved local credentials are checked before the route body renders, avoiding a sign-in flash for returning authenticated users;
- authenticated drawer order is `Dashboard`, `Tasks`, `Equipment`, `Pets & Mounts`, `Party`, `Quests`, `Spells`, `Settings`, `Diagnostics`;
- top app bar with refresh action, active refresh count, cloud sync state, and latest sync timestamp fallback;
- responsive drawer navigation shown only after authentication;
- signed-out empty states on Dashboard, Tasks, Equipment, Pets & Mounts, Party, Quests, and Spells show a page-local Sign in action when no required cached snapshot is available, while authenticated empty states continue to point at Refresh;
- dashboard navigation cards for Tasks, Equipment, Pets & Mounts, Party, Quests, and Spells use local `Open` actions, while the Dashboard keeps a single primary `Open Habitica` web link;
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

Status: Implemented
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
required project-owned x-client value
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

Current records:

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
/party, /quests   -> Party, UserProfile, GearCatalog
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

- Deployment configuration must retain a nonempty project-owned `x-client`; authenticated account identity never supplies a fallback.
- Additional per-domain error presentation beyond compact status chips remains a UI improvement, not a sync architecture gap.

## 15. Equipment page and gear explorer

Status: Implemented
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
26. Snapshot eggs, food, hatching potion, pet, and mount ownership by key in addition to aggregate counts.
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

### Error handling

Show cached inventory/equipment data even when a previous refresh attempt failed.

Equip failures leave cached state visible, write an `Inventory` diagnostics log entry, and show snackbar feedback.

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
- equipment navigation label rendering on the `/inventory` route;
- diagnostics logging for inventory actions.

### Open questions

Current implementation:

- dedicated Equipment navigation entry in the app shell, preserving the `/inventory` route;
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

## 15.1 Pets and mounts workspace

Status: Implemented
Owner module: `Habitica.Domain.User`, `Habitica.Rules.Pets`, `Habitica.Api`, `Habitica.Storage`, and `Habitica.WebApp.Pages.PetsMountsPage`
Application entry point: `Habitica.WebApp.Pages.PetsMountsPage`
Primary Habitica data: cached eggs, food, hatching potions, pets, mounts, current pet, and current mount
Mutates Habitica state: yes for confirmed hatch queue execution, feed, saddle-as-feed, fast equip, and confirmed bulk sell actions
Requires confirmation: yes for hatch queue execution, bulk sell, per-card Transform to Mount, and Use Saddle; no for feed queue execution or equip
Offline behavior: collection browsing, search, type filters, missing-companion hints, feed planning, and bulk sell preview remain available from the local snapshot
Rate-limit sensitivity: medium for feed queues and high for bulk sell

### Goal

Provide a dedicated companion workspace without crowding the equipment explorer. The page uses checked-in Habitica stable catalog keys, groups large collections behind persistent local folds, surfaces missing hatching ingredients from cached inventory only, and moves the existing bulk sell planner out of Inventory.

### Local storage

```text
preferences/petsMountsPage
```

Fold preferences are browser-local UI state. They are intentionally excluded from portable exports and Cloudflare app-data sync. Per-pet and per-mount ownership maps stay in the local snapshot but are removed from the Cloudflare user-profile section before upload.

### API interaction

```text
POST /user/feed/:pet/:food?amount=:amount
POST /user/equip/pet/:key
POST /user/equip/mount/:key
POST /user/hatch/:egg/:hatchingPotion
POST /user/sell/:type/:key
GET /user
```

### Algorithm / rules

```text
1. Read per-key eggs, food, hatching potions, pets, and mounts from the cached user snapshot.
2. Group checked-in pet and mount catalog entries into base, magic-potion, quest, premium, and wacky collections; keep unknown owned special entries visible in a fallback group.
3. Persist fold state locally and expand matching groups while search is active.
4. Derive ready-to-hatch and missing-ingredient hints only from the cached inventory plus checked-in catalog.
5. Missing pet cards add stable `(egg, hatching potion)` rows to the Hatch Planner instead of hatching immediately. Hatch queue allocation reserves eggs and potions in row order, warns for missing or already-reserved ingredients, skips duplicate queue rows, and blocks already-owned or unsupported catalog pets from execution.
6. Hatch queue cards show pet, ownership state, egg, hatching potion, available/reserved counts, planned consumption, and local warnings. `Hatch queued pets` opens inline confirmation, then calls `POST /user/hatch/:egg/:hatchingPotion` sequentially in queue order, shows block-local determinate progress such as `Hatching 1 of 3`, and stops on the first failure.
7. Sort available normal food for a queued pet by growth value for that pet, highest first, and show the per-item growth value in the queue card. Saddle is excluded from normal food selectors and handled by a separate saddle action.
8. Model pet-to-mount growth locally from cached pet progress, mount ownership, food counts, and checked-in catalog data. Habitica hatches pets at 5 stored progress points, preferred food adds 5 points, premium pets gain 5 points from any normal food, non-preferred food adds 2 points, and mount conversion happens at 50 points; the UI maps that to 10%, +10%, +4%, and 100%.
9. Build deterministic feed-plan rows by consuming highest-value normal food first, then lower-value food without mutating cached inventory counts.
10. Show mount-growth progress in the feed planner and on every owned pet card. Growable pets with cached progress show their current percent, remaining progress, and concise food-needed copy; unavailable or unowned pets keep an empty bar for consistent scanning.
11. Preview per-pet feed queue cards before sending them. Queue allocation reserves food for earlier pets first, warns when later pets exhaust shared food, and recalculates after food changes or removals. Execute queue requests sequentially with block-local determinate progress such as `Feeding 2 of 5`, and stop on the first failure.
12. Feed and hatch planner progress bars sit at the bottom of the relevant planner block, use active color-scheme progress tokens, and guard matching queue edits/actions until execution finishes.
13. Validate cached ownership before hatch, feed, fast equip, or bulk sell mutations.
14. Refresh `/user`, save the refreshed snapshot, and write `Inventory` diagnostics after companion mutations.
15. Keep bulk sell planning limited to eggs, food, and hatching potions. Preserve the keep-count preview and explicit confirmation flow.
16. Missing mount cards can add their matching owned growable pet to the same feed queue with `Plan to grow`; unavailable missing mounts show a concise cached-data reason.
17. Companion group headers expose `Add All to Feeding Queue`, which appends valid owned growable pets for missing mounts in the currently visible group/filter set without sending any Habitica feed request. Already-owned mounts, missing pets, unknown/special/non-growable catalog entries, and already queued pets are skipped. Newly queued items use the same default food selection as single-card planning, including a no-food warning preview when no normal food is available.
18. Companion group headers expose `Add All to Hatching Queue`, which appends valid missing hatchable pets in the currently visible group/filter set without sending any Habitica hatch request. Owned pets, missing eggs, missing hatching potions, unknown/special/unhatchable catalog entries, and already queued pets are skipped. Hatch queue allocation then recalculates shared egg and potion reservations in queue order.
19. Queue-add actions preserve the clicked card or companion-group header position after the Feed Planner or Hatch Planner grows above the collection. The behavior applies to single feed, missing-mount growth planning, group feed planning, single hatch planning, and group hatch planning; remove, clear, equip, and fold actions do not run scroll correction.
20. Pet and mount sections expose creature type filters built from catalog egg keys, using readable names such as Wolf, Tiger Cub, and Flying Pig. Filters compose with search and temporarily expand folded companion groups.
21. Use Saddle requires inline confirmation and sends one `Saddle` feed request only after confirmation. The lower saddle block shows cached saddle count and explains that saddle purchase is not available in this app until shop behavior is verified.
```

### Tests

Test:

- empty and grouped collection rendering;
- local-only fold persistence;
- search across companion and potion keys/names;
- missing ingredient and ready-to-hatch states;
- food ordering by pet growth value and saddle exclusion from normal food selectors;
- pet-to-mount growth progress and feed-plan calculation;
- hatch queue empty state, resource warnings, duplicate prevention, allocation recalculation, clear, confirmation, and sequential failure handling;
- feed queue allocation, removal recalculation, missing-mount planning, group bulk feed planning, group bulk hatch planning, queue-add scroll-stability markers, saddle confirmation, type filters, and sequential failure handling;
- fast equip dispatch;
- bulk sell planner relocation and Inventory removal.

## 16. Party explorer

Status: Implemented
Owner module: `Habitica.WebApp.Pages.PartyPage`, `Habitica.WebApp.Pages.QuestsPage`
Application entry point: `Habitica.WebApp.Pages.PartyPage`, `Habitica.WebApp.Pages.QuestsPage`
Primary Habitica data: cached party group summary, quest state, party members, member CRON fields, user quest-scroll inventory, Habitica content quest metadata
Mutates Habitica state: no; mutates shared Cloudflare party quest queue state
Requires confirmation: no
Offline behavior: party overview is available from cached snapshots; shared queue/pool/history requires the Cloudflare party-sync endpoint
Rate-limit sensitivity: low for Habitica reads; shared queue actions use browser-local claims or optional tokenized invite proofs through party-sync

### Goal

Provide a Party overview for cached party identity, roles, members, and member-level CRON rhythm plus a dedicated Quests workspace for active quest state and shared planning without directly mutating Habitica quest state.

### Inputs

```text
cached user snapshot
cached party snapshot
party freshness state
party quest summary
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
party-sync tokenized invite-proof mode and proof lifecycle metadata
```

### Outputs

```text
combined party-name-and-notes summary
compact Party-page quest summary linking to the Quests workspace
quest progress snapshot
party pending boss damage/items, boss HP remaining, total boss HP when available, and pending damage to party
compact party member CRON list with HP/MP, member-name search, class filtering, sortable low-HP/low-MP modes, and foldable details
viewer-local CRON statistics graph
active quest card with real quest metadata and rewards when cached
active quest owner or starter and started-at metadata when cached directly or available from the matching active shared queue entry
foldable active quest details/rewards and participant-name drill-ins
quest invitation card with accepted, pending, and rejected response lists before the quest starts
Quests and Dashboard warnings with Accept/Reject actions when the current user has not answered a quest invitation
shared quest queue cards with vote counts and voter names
separate Next Quest card for the selected shared queue item
skipped and expired queue-state labels
owner/admin/Officer pin, select, skip, expire, and return-to-queue controls
quest pool cards open by default on Quests, with manual collapse, owner availability, and local name/reward/type/owner search
recently completed quest cards with manual vs automatic source labels and management removal controls
owner/admin/Officer strip grouped near bottom-page party-sync moderation
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
3. Show the latest cached party name and summary together. Keep member-count context in the member-list visible-count pill and keep one compact Party-page Quests summary link.
4. Show quest key, active state, party pending boss damage or collection items when member progress is available, boss HP remaining, total boss HP when content data is available, pending damage to party, and participant count when a quest snapshot exists. Active quests show one compact participant count instead of invitation-response totals. Show owner or starter and started-at metadata when the quest snapshot or matching active shared queue entry preserves it; otherwise render concise unavailable states. Keep description/rewards and participant names behind in-memory details and participants controls. Participant names use the same Party member-detail focus behavior as other member links.
5. Do not show a dedicated Party overview CRON summary or buff-timing recommendation block.
6. Show compact per-member cards with display name, class, subtle HP/MP values, CRON state, last CRON, average CRON time, and active-quest pending damage/items when available.
7. Keep member id, level, CRON reason, and stat breakdowns behind a collapsed in-memory details toggle on each member card.
8. Let the member list search cached public display names locally, filter to available cached Habitica classes while keeping members with unknown classes visible under `All classes`, then sort by name, average CRON, latest CRON, pending quest contribution, low HP, low MP, and CRON status. Search trims whitespace, matches partial names case-insensitively, and shows a distinct no-match state without fetching party data. HP/MP sorts are ascending so the lowest current value appears first; unknown values sort last.
9. Show viewer-local CRON graph points from local history.
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
21. Keep Active Quest "Open in Habitica" links on web URLs only; official mobile app party/quest deep links are documented as unsupported in `skill-tree/documents/habitica-deeplinks.md`.
22. Let role-strip and kick-list member names focus the same expanded member details UI used by the Active Quest finishing-member link.
23. When Habitica has a quest invitation that is not active yet, hide progress and finish estimates and show accepted, pending, and rejected member response lists instead; names focus the same expanded member details UI. For active quests, always show expected finish, show finishing member only when known, and show timing confidence plus the estimate alert only when completion timing exists. Post-CRON party refreshes must save the same enriched party snapshot as normal party refreshes, including CRON history, member average timing, active-quest progress, and completion estimate, so opening `/quests` immediately after Start New Day does not require a second manual refresh to show a finish prediction.
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
34. Keep the quest pool expanded by default on `/quests`, with an in-memory `Hide quest pool` / `Show quest pool` control that does not reopen after later component rerenders. Let members search the expanded quest pool by quest name, public reward display name, type, or visible owner. Compose the in-memory search with the owned-only filter and show a distinct no-match state.
35. Keep Party focused on summary, roles, settings, member review, and member-level CRON rhythm. Render active quest details, queue, pool, votes, controls, and recent completions on the dedicated `/quests` route.
36. Keep browser-only `local-claim-v1` as the default and owner/app-admin recovery path. Let owner/app admins optionally enable hashed `tokenized-invite-v1` proofs, issue labeled proof tokens, rotate/revoke/remove them, activate a shared proof in the current browser, and return that browser to local-claim fallback. Do not send Habitica credentials to Cloudflare.
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

Display only the locally cached group summary fields required for the explorer. Do not expose credentials or raw request headers. Shared party-sync sends a browser-local claim or an optional tokenized invite proof to Cloudflare instead of Habitica API tokens. Tokenized proof rows keep SHA-256 token hashes, labels, issuer metadata, and lifecycle timestamps; raw reusable tokens remain browser-local and are shown only once when issued or rotated.

### Tests

Test:

- `/groups/party` response mapping;
- party snapshot persistence;
- party and quests page rendering;
- compact member card details toggling;
- quest reward metadata rendering from cached Habitica content;
- navigation rendering for the `Party` and `Quests` routes.
- shared quest queue/pool rendering;
- party-sync queue and vote mutations.
- party-sync queue selection, pinning, skip, expiry, and requeue actions.
- safe markdown and supported inline HTML rendering for party and quest descriptions.
- local party-sync claims that do not pass API tokens to Cloudflare.
- optional tokenized party-sync proof header selection, local fallback, lifecycle rejection, and owner/admin recovery without Habitica API tokens.
- Officer/settings/kick visibility and management actions.

### Open questions

Current implementation:

- dedicated `Party` route in the app shell;
- dedicated `Quests` route in the app shell for active quest details and shared planning;
- compact Party-page quest summary linking to the Quests workspace;
- combined cached party-name-and-notes summary with one compact Quests link;
- cached quest progress snapshot.
- compact party member cards with foldable extra info and stats;
- local display-name search and class filtering for compact party member cards, with unknown classes kept in the unfiltered list;
- subtle HP/MP values and low-HP/low-MP member sorting;
- active quest card with real cached quest metadata and compact rewards;
- active quest owner/starter and started-at metadata with matching shared-queue fallback, concise unavailable states, and foldable details/rewards and participant-name drill-ins;
- inactive quest invitation response lists before quest progress exists;
- party detail section order of summary, compact Quests link, member and CRON sections, then party-sync roles, settings, and moderation;
- party-sync settings labels and helper descriptions for non-technical party members;
- shared quest pool from published member quest-scroll availability;
- quest pool expanded by default on `/quests`, with an in-memory manual collapse control;
- expanded quest-pool search by quest name, public reward display name, type, or visible owner, composed with the owned-only filter;
- shared quest queue with owner-only add/remove and one-vote-per-member voting;
- shared queue invite action, management pin/select/skip/expire/requeue controls, separate Next Quest card, and owned-only queue/pool filter;
- Quests/Dashboard quest-invitation warnings and Accept/Reject actions;
- recently completed shared quest history table, UI, and management removal;
- automatic recent-completion detection from structured Habitica party chat signals.
- optional tokenized invite-proof management with browser-local activation and local-claim recovery.

Next:

- add party-member explorer with throttled pagination and cancellation;
- add a stronger party-membership proof only if official Habitica support becomes available without sending Habitica credentials to Cloudflare.

## 17. Diagnostics workspace

Status: Implemented
Owner module: `Habitica.Application.Diagnostics`, `Habitica.Storage`, and `Habitica.WebApp.Pages.LiveTestsPage`
Application entry point: `Habitica.WebApp.Pages.LiveTestsPage`
Primary Habitica data: authenticated user snapshot, task snapshot, party snapshot, equipped battle gear, diagnostics log entries
Mutates Habitica state: yes for the reversible gear roundtrip only
Requires confirmation: yes for the reversible gear roundtrip
Offline behavior: diagnostics history and filters remain readable offline; live checks and presets require authenticated API access
Rate-limit sensitivity: low because tests are user-launched, sequential, and deliberately small

### Goal

Provide a Diagnostics workspace from the UI that validates implemented features against the real Habitica API, exposes curated read-only inspection presets, and keeps a persistent redacted diagnostics journal for cross-feature debugging.

`LiveTestsPage.razor` declares the canonical `/diagnostics` route and a legacy `/live-tests` compatibility alias. The authenticated drawer points to `/diagnostics`.

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

Persists a capped `diagnostics/logEntries` journal that stores newest-first redacted diagnostics entries across auth, inventory, preset inspection, and diagnostics-check workflows.

### API interaction

Current diagnostics-check flow uses:

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
5. Append successful and failed auth, preset, and diagnostics-check workflow events into the same redacted diagnostics journal.
6. Skip the reversible gear test when no alternate owned supported battle item exists.
7. For the reversible gear test, equip an alternate owned battle item, verify with a fresh `/user`, restore the original item, and verify restoration with another fresh `/user`.
8. If restoration or restore verification fails, report the test as failed and preserve the latest known local snapshot.
9. Keep normal user-facing mutations on their dedicated feature pages; Diagnostics is for verification, inspection, and operator tooling.
10. The diagnostics console filters by feature, severity, and mode.
11. Copy and download actions export the currently filtered entries as JSONL. With no filters, they export all stored entries.
12. The selected entry detail renders structured JSON instead of loose key/value text.
13. Diagnostics log rows force `minmax(0, 1fr)` grid tracks and `min-width: 0` descendants so long metadata wraps inside the console instead of creating internal horizontal scrolling on tablet or phone widths.
```

### Validation

Require:

- an authenticated session before any diagnostics check runs;
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

Keep all diagnostics checks and presets user-initiated. Do not introduce background polling or parallel batch execution through the Diagnostics workspace.

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

- Optional diagnostics checks for task mutations must use stronger warnings and dry-run summaries before any live mutation.
- Any diagnostics check or action check that consumes gold, mana, items, or irreversible state must use the shared guarded-mutation confirmation rules before execution.

## 18. Task workspace

Status: Implemented
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
compact task cards with title, notes, immediate scoring/checkoff controls, and a Details toggle
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
per-section rearrange toggle with task-card move to top/up/down/bottom controls
per-section Reset to Habitica task-order control when local custom order exists
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
preferences/taskOrder, removed when the last section custom order is reset
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
5. Sort items within each group by completion state, then the selected sort mode: Habitica snapshot order, name, highest value, lowest value, or due soon. Habitica snapshot order is the default.
6. Keep group fold state and completed visibility separately for each task type.
7. Persist task-page preferences by user id on the current device.
8. Show the numeric task value when available.
9. Tint open task cards with a continuous low-saturation value gradient from warm negative values to cool positive values.
10. Render completed tasks with neutral muted styling when the category is set to show completed.
11. Keep task cards compact with title, notes, immediate scoring/checkoff controls, disabled reasons, progress, and a Details toggle. Keep the task-scoped action row wrapping within the card at narrow widths instead of stretching each button to the full card width. Reveal status, value/priority/due metadata, task detail metadata, and charts inside the expanded card.
12. For Habit scoring, clamp multi-score count to 1-20 and show determinate progress while requests execute sequentially.
13. Apply saved per-type task order after filtering/sorting; unknown saved IDs are ignored and new task IDs append after ordered known IDs.
14. Drag handles move items within the currently visible list and persist the resulting per-type ID order for export/import and encrypted cloud sync. After drag/drop, keyboard reorder, or move-button reorder, the page saves `preferences/taskOrder` locally and asks the session controller to upload only the `task-order-preferences` cloud-sync section when credentials are available; signed-out or failed cloud uploads do not undo the local order.
15. Drag reordering is scoped to the current task type group and preserves hidden or completed items that are filtered out of the visible subset.
16. Focused drag handles support arrow-key reordering through the same local ordering path for keyboard precision.
17. Keep rearrange controls hidden by default. Each task group exposes an in-memory `Rearrange` toggle that reveals the drag handle plus one horizontal row of move-to-top, move-up, move-down, and move-to-bottom buttons. The buttons use the same local ordering path and disable edge moves that would not change the visible order.
18. Each task group with live custom order data exposes `Reset to Habitica`. Reset removes only that task type from `preferences/taskOrder`, leaves other task types unchanged, switches the task view back to Habitica snapshot order, and syncs the `task-order-preferences` section. Resetting the last custom section uploads an empty task-order section for cloud sync, then removes the local `preferences/taskOrder` record so opening the page does not create a blank placeholder. Importing an empty task-order section removes the local record.
19. Parse cached task history points when Habitica returns them and keep them attached to task snapshots.
20. Let users switch task statistics between week, month, and year periods.
21. Render aggregate task-history and month-activity charts from cached history without requiring a live refresh.
22. Render smaller history and month-activity charts inside expanded task details only, so task cards remain scannable by default.
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
- compact collapsed task cards with expanded metadata, mutation controls, and charts;
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

- richer expandable task details beyond the current compact details and chart surfaces;
- larger-data optimizations such as virtualization.

## 19. Task mutation controls

Status: Partial
Owner module: `Habitica.WebApp.Pages.TasksPage`, `Habitica.WebApp.State.AppSessionController`, `Habitica.Api`, and `Habitica.Application.Tasks`
Application entry point: `Habitica.WebApp.State.AppSessionController.ScoreTaskAsync`
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

Uses diagnostics logging and task snapshot invalidation metadata. Manual task ordering uses `preferences/taskOrder`; mutation controls refresh task/user snapshots instead of persisting a separate task-mutation store.

### API interaction

Current task scoring/checkoff mutations go through `Habitica.Api` endpoints documented in `skill-tree/documents/habitica-api.md`. Any additional task-editing or uncompletion endpoint must be documented there before wiring UI controls.

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

Mutating task actions must not ship without the conservative execution rules from `skill-tree/documents/rules.md` and `skill-tree/documents/technical.md`.

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
- scope and confirmation model for future task editing beyond current scoring/checkoff controls.

## 20. Feature status labels

Use these labels consistently:

```text
Implemented
Partial
Historical
Deprecated
Blocked
```

When changing a status, update the relevant feature section and include the reason when useful.

## Read-only MCP bootstrap

Status: Implemented locally; disabled in shipped configurations. `workers/chatgpt-mcp/src/tools.ts` exposes four legacy read tools alongside the canonical task tools through the locked MCP SDK. `get_profile` returns only opaque identity and available account labels, with the OpenAI profile marker and strict output schema. `get_account_status` returns known numeric stats, optional class/party ID, explicit or unknown Cron requirement and retrieval time. Missing optional values remain omitted or explicitly unknown.

`list_tasks` and `get_task` expose a labeled Habitica legacy bootstrap view. Filters include type, completion, confirmed due Dailies, text query and a maximum 100 results. Habitica evaluates Daily due state through `dueDate`; unknown due state remains unknown and excluded from due-only results. Completed Todo coverage contains at most the thirty most recent Habitica entries, separate from Companion completed history. Task IDs require verified mapping before future Companion mutations.

The named Auth service independently validates bearer tokens and required tool scope, decrypts credentials internally and issues whitelisted Habitica GET requests. The four bootstrap tools accept no account selectors, arbitrary routes or mutation fields. The public Auth handler exposes no read proxy. MCP receives filtered models and safe failures; credentials remain inside Auth. Profile identity reads cached account labels and avoid upstream traffic. Offline upstream reads return unavailable.

Habitica 401/403 marks the connection for credential repair; 429 and exhausted quota persist a cooldown using Retry-After and remaining/reset headers. Later reads respect the cooldown without automatic retry. Upstream redirects are rejected without forwarding credentials. Reads use ten-second upstream timeouts and bounded response bodies. Diagnostics contain correlation IDs, hashed profile references, tool/category, duration, status and rate metadata; task text, credentials and raw responses remain excluded. Persistent read state contains only cooldown and repair state for the connection epoch.

Local validation includes SDK client initialization/output-schema validation, Inspector 1.0.2 CLI initialization/tool listing/profile read, two-profile fixtures, strict input/output contracts, due-state filtering, completed coverage, scope/origin denial, safe 401/429/5xx handling and redirect rejection. Inspector 1.0.2 is the maintained v1 security-fix line compatible with the installed Node runtime; Inspector v2 and real ChatGPT remain rollout checks. PWA build and browser behavior remain qualified. No MCP tool mutates Habitica.

## Canonical Companion domain

Status: Domain implemented; workspace commands, persistence and UI integration remain separate dependencies. Owner: `Habitica.Domain.Workspace`. Versioned documents contain independent entity identities, project/section/label references, task state and planning metadata, recurrence definitions, occurrence/completion history and Habitica projection mappings. Domain operations are deterministic and offline; no domain code calls Habitica or mutates game state. Task completion requires matching completion evidence; Daily/Habit lifecycle remains separate from permanent Todo completion.

Strict serialization validates records before output and after import. Unsupported schema versions, missing core task fields, unknown/numeric enums, duplicate properties, invalid foreign references, parent/dependency cycles and duplicate occurrence/projection mappings fail validation. Goal progress follows eligible linked Todos and exposes unknown progress for an empty eligible set. Domain tests cover neutral JSON round-trip, relationship preservation, long dependency chains, recurring lifecycle, history and invalid imports. Legacy Tasks UI still consumes Habitica snapshots; canonical entity definitions alone do not enable task authoring or cloud upload.

## Companion journal and command validation

Status: Canonical command core, durable storage and separate PWA/MCP task controls implemented locally. Owner: `Habitica.Application.Workspace`, with records in `Habitica.Domain.Workspace` and the compare/exchange port in `Habitica.Storage`. Command acceptance separates canonical saved state from Habitica projection effects. Todo completion, Daily occurrences and bounded repeated Habit events remain independent of confirmed game effects. Legacy Habitica task controls remain visibly separate from the canonical editor.

Offline command logic performs no network calls. Revision conflicts and command-ID payload conflicts leave the original snapshot unchanged. Atomic-store acknowledgement loss returns unavailable/unknown acceptance rather than falsely confirming persistence. Projection state tracks accepted effects and confirmed effects separately, preserves partial progress, rejects stale lease tokens and requires evidence for unknown-outcome reconciliation. Journal history stores user command results; no credentials enter command models. Tests cover deduplication, corrupt journal rejection, stale revisions, typed task operations, lease recovery, quota/repair pauses, uncertain acceptance and cross-runtime payload identity.

## Local canonical persistence and recovery

Status: Storage and portability core implemented; Settings provides separate canonical workspace controls. Owner: `IndexedDbWorkspaceDatabase`, domain journal codec and `WorkspacePortabilityService`. Typed journal candidates persist canonical records, accepted commands and transition events atomically. Reads preserve command/event ordering across reload. Scoped adapters reject mismatched workspace identities and invalid journals before returning data. Revision conflicts leave existing records untouched. Ordinary storage failures surface through the command service as unavailable or unknown acceptance.

Portable workspace archives contain task notes and command history, with no credential model. Restore retains canonical IDs and accepted outcomes, rejects conflicting existing snapshots, and prevents copied in-flight requests from becoming blind retries. Legacy snapshots and private encrypted sync remain separate. Browser qualification uses an isolated synthetic origin: schema-1 upgrade, quota failure after canonical writing begins, transaction rollback, concurrent writers, immutable history, equivalent field order and full reload all pass. C# adapter/codec and export/restore fixtures qualify the matching typed boundary. Physical device eviction, interrupted browser upgrades and older PWA versions remain rollout checks.

## Credential revalidation continuity

Bootstrap read-tool metadata declares profile access plus the required operation scope. Bound refresh requests can inherit the approved audience, and temporary profile storage failures preserve recoverable grants. Successful validation of unchanged active Habitica credentials preserves healthy OAuth sessions and rotates management access independently. Changed credentials and disconnected/deleted profiles receive a fresh connection epoch. Concurrent enrollment uses a generation precondition to reject stale overwrites. Credential-repair status clears after successful verification; rate-limit cooldown remains intact. Synthetic fixtures qualify unchanged grants, stale management denial, concurrent enrollment, changed credentials and disconnect/reconnect behavior.

## Optional cloud workspace core

Status: Local implementation and synthetic qualification; shipped cloud access remains disabled. Owners: Workspace Worker, private Auth identity broker and `WorkspaceCloudClient`. Entry points: separately consented Settings workspace controls and the shared command HTTP API. The server stores readable canonical task data and history in isolated D1, with no Habitica credentials in that database. Authentication derives the account from bearer credentials; request arguments cannot choose another profile. Cloud consent neither grants a ChatGPT OAuth permission nor enables background execution.

Commands atomically persist canonical changes, command receipts and accepted events with revision checks and payload deduplication. Pending projection states remain distinct from saved Companion data. Synthetic tests cover two-account isolation, conflicts, concurrent writers, append rollback, malformed schemas, Daily completion, bounded Habit scores, lifecycle races and retained-history capacity. Canonical command acceptance performs no Habitica mutations. The separately enabled executor owns projection dispatch. Cloud outages leave browser workspaces and encrypted private sync independent.

Workspace Settings provides cloud connection, readable export, disconnect and confirmed deletion; local workspace listing, export and confirmed restore use account-scoped IndexedDB. Global browser clearing includes canonical workspaces. Local restore preserves accepted IDs/history and rejects conflicting existing data. Exports contain notes and command history but no credential fields. Cloud session tokens stay in tab memory; late responses cannot restore a forgotten session. The PWA validates Worker exports through the same C# journal codec and checks workspace identity. The confirmed local-to-cloud handoff freezes local ownership before upload and keeps the local copy frozen after uncertain results. Repeating the same transfer recovers the transfer receipt. Portable restores start with execution paused.

## Verified Habitica projection adapter and gateway

Status: Adapter, reviewed adoption API, private gateway and hosted execution implemented and qualified locally; manual task controls use the shared canonical command API. Owners: `projection-adapter.ts`, Auth `projection.ts`, shared projection contracts and canonical command snapshots. Workspace preview/adopt routes show canonical and external metadata separately, require a fresh reviewed fingerprint and reject unresolved projected commands. Adoption preserves canonical task IDs and fields and performs no Habitica mutation.

The gateway verifies task ownership and excludes group/challenge tasks. Supported writes use creator-owned headers, fixed v3 routes, bounded field whitelists and one consumed precondition per attempt. Atomic dispatch reservations precede upstream effects. Duplicate attempts return reduced receipts. Quota refusal and credential repair persist across calls. Every automated gateway call observes the persisted spacing gate. No credentials, upstream bodies or task notes enter gateway diagnostics or attempt metadata.

Synthetic composition tests project an accepted completed Todo through create and score in order while retaining one Companion identity. Separate fixtures cover external edits/deletions, aliases, unknown outcomes, positive create reconciliation, concurrent attempts, failed dispatch reservation, Daily due/Cron/day guards, revocation and retention cleanup. Browser tests preserve accepted task intent across journal transitions and reload. The gateway performs no blind replay after ambiguous scoring. Habitica preconditions use recent observations because the upstream API offers no conditional transaction spanning reads and writes; external changes during the observation window remain a live rollout concern.

## Optional hosted workspace executor

Status: Local implementation and synthetic qualification; shipped execution remains disabled. Owners: Workspace Worker `executor.ts`, `WorkspaceHandoffService`, `WorkspaceCloudClient` and Settings. The Worker requires workspace access, deployment configuration and separate `executor-v1` per-profile consent. Settings exposes enable and pause controls independently of cloud connection. The executor uses canonical accepted snapshots and the private Auth projection gateway; the executor never accepts arbitrary upstream endpoints or credentials.

Each activation owns one persisted profile lease and processes at most one projection effect. Scheduled recovery selects one eligible profile per minute, with persisted wake ordering. The executor records the leased and dispatched states before network effects. A single transaction persists each result, mapping, journal event, archive size and original request reservation. Calls use a persisted thirty-second spacing gate in both Workspace and Auth. Fresh account and Daily-due checks precede Daily scoring. The bounded activation stops after refusal, disconnect, disable or uncertainty. Accepted canonical commands remain stored while execution pauses.

Restart recovery looks up the original private Auth attempt receipt without another Habitica call. A confirmed receipt advances the original command once; a missing or uncertain receipt preserves unknown outcome and fences new effects. Credential changes cannot reinterpret an old-context refusal as proof that the original write failed. Expired undispatched leases return to pending. Unknown scores require review; automated recovery does not replay uncertain scores. Gateway operational metadata expires separately from retained user command history.

The explicitly confirmed `workspace-handoff-v1` transfer first freezes the local journal and blocks local edits and projection dispatch. Active attempts must finish or expire; copied dispatched attempts remain unknown. The server accepts the archive only into an empty, execution-disabled workspace, atomically preserves canonical IDs and history, and stores a transfer hash for duplicate acknowledgement recovery. A matching receipt marks the browser copy cloud-owned. Transfer never enables background execution. Portable imports remain paused and cannot claim execution ownership.

Synthetic tests cover duplicate wakes, receipt loss, restart, expired leases, unknown-outcome fencing, quota pauses, credential revocation, disconnect during pacing, atomic handoff rollback, cross-profile identity conflicts, Daily sequencing and Settings consent. Real browser checks preserve frozen ownership through reload and keep the default app usable with cloud access disabled. Real enrollment, deployment, wall-clock scheduler reliability, mobile eviction and live Habitica effects remain separate rollout checks.

## Whitelisted canonical task details

Status: Domain, command and projection support implemented locally; manual and MCP controls use the same bounded field contracts. `HabiticaTaskDetails` stores optional game difficulty, attribute and bounded checklist details independently of Companion urgency. The canonical and Worker codecs accept difficulty values `0.1`, `1`, `1.5` and `2`, attributes `str`, `int`, `per` and `con`, and at most one hundred checklist items with unique nonempty IDs, bounded text and explicit completion state. Unsupported values and unknown nested fields fail before acceptance.

Partial commands retain unspecified details; an explicit empty checklist clears the checklist. Accepted task snapshots retain the original details after later edits. Projection creation uses the accepted details, while partial updates send only explicitly patched fields. Native urgency never becomes Habitica difficulty. The command endpoint accepts bounded sixty-four-KiB requests so the supported checklist can fit without bypassing payload validation.

Legacy command hashes remain version one when the extra details are absent. Commands carrying details use a version-two length-prefixed UTF-8 hash. Neutral fixtures validate both hash versions across C# and TypeScript, including Unicode checklist text and partial commands. Browser immutable-history comparison treats only the newly introduced null detail fields as equivalent to absent legacy fields; accepted details cannot change. Cloud journal fixtures round-trip through the C# codec and retain canonical identity, history and details. No migration tool or upstream action is required to read an older archive.

## Reviewed canonical task commands in PWA and MCP

Status: Implemented locally; cloud, OAuth and projection flags remain disabled in shipped configuration. Owners: `task-api.ts`, `WorkspaceCommandService`, `WorkspaceTaskController`, `WorkspaceCloudClient` and `CompanionTasksPanel`. The Tasks page opens a separate Companion editor. Browser workspaces support reviewed offline canonical saves and retained receipts. Cloud workspaces use `/task-api`, which invokes the same preparation and acceptance functions as the private MCP Workspace service. The legacy Habitica task view remains separately labeled and retains the existing behavior.

Canonical MCP reads use `list_companion_tasks`, `get_companion_task` and `get_task_command`. Search returns stable Companion IDs and explicit canonical provenance. Multiple matching names remain candidates; mutation arguments require an exact ID. `prepare_task_command` validates the requested whitelist, current canonical state and revision, allocates server command/task identities and returns a ten-minute review handle. Acceptance tools include `create_todo`, `create_daily`, `create_habit`, `update_task`, `complete_task`, `score_habit`, `delete_task`, `skip_occurrence`, `pause_task` and `resume_task`. All acceptance tools require the matching prepared command ID. The canonical command API and MCP support optional Companion-only Daily recurrence; the basic browser editor still creates unscheduled Dailies. Recurrence authoring is limited to unlinked Dailies and retains prior occurrences when a schedule is cleared. Daily/Habit creation and deletion reject Habitica projection requests. Deletion archives an unlinked canonical task, hides the task from active reads and retains command, occurrence, completion and score history in exports. Linked or pending-projection tasks cannot be archived through `deleteTask`. No generic route, arbitrary JSON, upstream deletion or reward creation tool exists.

Preparation request IDs bind the original normalized input to one generated command. Preparation and command IDs remain independent of JSON-RPC transport IDs. Repeated acceptance returns the retained command receipt before checking current revision or preview expiry. The database acceptance predicate checks preview expiry and the current workspace access epoch inside the atomic commit. Disconnect invalidates unaccepted previews from the old epoch. Expired unaccepted previews are removed by bounded scheduled cleanup; accepted request-to-command bindings remain with user history until workspace deletion. Preview storage and cleanup participate in archive accounting and store-version comparisons.

OAuth task writes require `habitica.tasks.write` in addition to the MCP resource's base profile scope. Tool metadata and scope challenges declare the required scopes. The private Workspace entrypoint independently validates the bearer, exact resource audience, profile connection, active workspace consent and tool scope. The MCP Worker has no credential or database binding and sends task commands only to the Workspace service. Existing read grants cannot write. Consent describes canonical saves and possible later game effects. Scope declarations never enable cloud storage or execution consent.

Results report canonical acceptance separately from Habitica projection, accepted completion/score event count, confirmed effect count, failure position, correlation ID and refresh status. A pending or unknown projection never erases the saved command. Receipt absence after transport uncertainty does not prove nonacceptance. The PWA retains the original request/command identity during uncertain responses; repeated confirmation uses the same command. A saved command survives refresh failure, and the editor requires reload before another edit. The executor continues to enforce sequential effects, quota pauses and no ambiguous replay.

The PWA previews action, task fields, checklist replacement, occurrence or score count and possible game effects before confirmation. Local records remain independent of cloud upload. Frozen/cloud-owned browser copies stay read-only, and restored copies preserve paused execution. Reviewed linking shows canonical and external records separately, binds approval to workspace identity, access epoch, revision and fresh fingerprint, and never overwrites task fields or sends a Habitica mutation. Pending projected commands block linking.

Synthetic tests exercise native HTTP and actual MCP SDK calls, strict field and tenant validation, exact-ID selection, expired previews, duplicate acceptance, scope escalation, private-service authorization, partial updates, Todo/Daily completion, bounded Habit events, quota and unknown projections. Web/controller tests cover explicit confirmation, redacted failures, original-ID receipt recovery, refresh failures, account changes and separate saved/projection labels. Real deployment, mobile storage eviction and live Habitica effects remain rollout checks.

## Cloud access renewal and retained command recovery

Connected Workspace Settings offers an explicit renewal action with the existing readable-data and credential-storage disclosure. Renewal requires a fresh acknowledgement and uses the current signed-in PWA credentials through the established connection flow. Successful connection generation changes invalidate older in-flight responses. Late renewal after tab sign-out cannot restore forgotten access.

An unavailable workspace reload keeps a pending original command preview visible for the same account and source. The retained view is marked stale and blocks unrelated edits. The original command ID remains available for receipt lookup or exact retry after renewed access. Account changes still clear the retained view. A successful original receipt resolves acceptance uncertainty without accepting another command.

## Packaged Habitica workflows

Status: locally qualified source package; host installation and live enrollment remain unverified. Owner: `plugin/`. Entry points: daily review, account review and task-management skills through the packaged MCP connection. The portable manifest declares an inactive remote endpoint and no credentials, hooks or registered connector identity.

Daily/account review reads legacy Habitica status and bounded task lists. Task management resolves canonical Companion IDs, prepares a command, presents the preview for approval and accepts the original server-issued ID through Workspace. Saved canonical state and Habitica effects remain distinct. Missing inputs, multiple candidates, denied scope and unknown outcomes stop acceptance or replay as applicable. Offline tools report unavailable data; skills never invent results or use direct Habitica requests. No project/goal/calendar/full-history workflows are advertised.

Security remains server-enforced. Enrollment, cloud storage and background execution each retain separate consent. Skills never request API tokens in chat. Isolated schema and actual SDK fixtures cover two-profile isolation, read workflows, canonical acceptance, insufficient scope, missing fields and absence of upstream writes. Manual host/model activation scenarios and private setup reside in `plugin/README.md`.

## Verified party context

Status: implemented locally; disabled by default. Owner: Auth `PartyService`, Workspace party API and MCP `get_party_context`. The read-only tool requires independent `profile:read` and `party:read` grants plus active cloud workspace enrollment. Auth revalidates OAuth scope, derives the actor from decrypted enrollment, reads the current user and authenticated party, verifies exact account/party IDs, group type and leader shape, then rechecks enrollment. Missing party, changed identity, invalid data and unavailable verification produce no membership inference.

The public result contains minimal party identity/name, leader/member role, member count, optional quest key/active state and retrieval time. Chat, credentials, full member profiles, inventory and internal actor/leader IDs are excluded. Reads are bounded and inherit quota cooldown and credential-repair handling. No offline membership guarantee exists. No queue mutation, spell or new shared-planning record is exposed by the context tool. Legacy browser-claimed party planning remains separate. Synthetic actual-SDK tests cover two profiles, all service scope boundaries, malformed and changed membership, quota and mid-read enrollment invalidation.

## Authenticated shared party planning

Status: locally implemented and synthetically qualified; disabled by default. Owner: Workspace `party-planning.ts`, Auth party proof, MCP planning tools and PWA Settings controls. The namespace starts empty and remains separate from legacy browser-claimed queue/role records. `get_quest_planning_state`, `queue_quest`, `vote_quest` and `get_party_command` use server-derived identities through Workspace. Fresh membership, a matching cloud connection/access epoch and separate `party-planning-v1` consent are required for shared access. OAuth planning writes require `party.planning.write`; existing task or party-read grants supply no planning write access.

Members queue only currently owned scrolls, up to the verified inventory count already represented in the authenticated queue. Queue entries and votes do not start, accept or purchase Habitica quests. A vote explicitly sets the actor's desired boolean. Stable command IDs deduplicate identical retries; changed arguments, stale namespace/revision, outside-party access and exhausted capacity reject acceptance. Results distinguish durable planning saves from game effects (`none`). Missing original receipts remain uncertain. Current-state reads bound queue size, omit other actors' private identifiers and include up to 100 verified own scroll-key options with an explicit truncation flag.

The verified leader can restrict member queue insertion and delete the authenticated namespace after explicit confirmation. Management-authenticated exports and removal cover only the actor's contributions/receipts and remain available after departure. Removing an owned queue entry removes votes on that entry; unrelated contributions remain. Removal updates every affected namespace atomically or rolls back on a revision race. Namespace deletion removes queue data, votes, receipts and consent, and recreation has a new namespace ID. Shared records remain until removal; cloud workspace cleanup and credential cleanup remain separate operations.

Settings names readable shared data and retention, binds consent to the displayed verified party, separates leader actions and confirms destructive controls. Account changes discard late client/controller results; upstream failures remain redacted. Default app configuration exposes an unavailable state and performs no background party writes. Synthetic SDK/D1 fixtures cover ownership, explicit vote retries, concurrency, roles, namespace replacement, departure, cleanup rollback and no upstream gameplay writes. Component/client fixtures cover consent, deletion confirmation, role visibility, safe text rendering, export redaction and late-account responses.

## Private spell safety gateway

Status: implemented locally and disabled by default. Owner: Auth `SpellService`, shared `spells.ts`, OAuth grant validation and the existing gameplay attempt/lease store. Workspace exposes reviewed MCP actions through this private boundary as described below. The private service requires an active enrolled connection and independent profile, account and party read grants; casting additionally requires `habitica.spells.execute`.

The initial allowlist contains Blessing (`healAll`): healer, level fourteen, twenty-five mana, current verified party, one-to-three reviewed foreground casts. Each private dispatch sends quantity one. Fresh actor, class, level, mana, health, equipped gear, buffs, Cron flags and verified party/quest facts form the exact precondition hash. The gateway rejects changed facts, unavailable verification, inadequate aggregate mana, disabled classes, dead actors, expired attempts and active quota cooldowns. The gateway does not start a new day, swap gear or predict exact party healing. Habitica can post an automatic party-chat spell notice; the OAuth approval warns about that effect.

The shared Auth gameplay lease serializes task and spell effects. Dispatch is persisted before the upstream request. Applied and refused terminal receipts are immutable; identical attempts return the original receipt. Ambiguous network/server/malformed success responses retain an unknown outcome, block new task/spell effects and never trigger replay. Known semantic spell refusals do not mark otherwise valid credentials broken. Offline operation is unavailable. Only normalized result health/mana, outcome codes and state hashes enter receipts; raw users, party members and credentials are excluded. Operational receipts retain the existing thirty-day policy.

Synthetic private-binding tests cover fresh preconditions, independent scope/identity, aggregate affordability, duplicate and competing requests, both directions of task/spell fencing, quota, malformed success, storage failure and mid-preflight credential/grant revocation. A neutral JSON fixture checks C# spell metadata and single-cast affordability against Worker rules while explicitly distinguishing stricter server limits. Workspace integration is described below; no real casting or deployment is qualified.

## Reviewed foreground spell actions

Status: locally implemented and synthetically qualified; disabled by default. Owner: Workspace `spell-api.ts`, typed `prepared_spell_actions`, Auth `SpellService` and MCP spell definitions. `get_spell_context` reads fresh verified context. `prepare_spell_cast` requires independent spell-execution scope, active cloud Workspace access and separate authenticated planning consent. The preview fixes the party, Blessing count of one-to-three, total mana, warnings and a two-minute expiry. `execute_spell_cast` accepts only the exact reviewed action using the latest single-use token. No new PWA casting UI or background casting is introduced.

Preparation deduplicates the actor's request ID. An identical repeated preparation rotates the unconsumed token for the same action, rather than creating another intent; altered input rejects. Only token hashes enter storage. Acceptance atomically consumes the token and records the action. Concurrent execution returns the accepted receipt or a busy result without another cast. Stored connection/access epoch and authenticated namespace fences reject stale consent, Workspace reconnects and namespace replacement. Contributor removal and leader namespace deletion revoke unconsumed preparations. Already transmitted effects can finish after access ends and cannot be undone.

Foreground execution holds the same persisted Workspace runtime lease as task projection, preserves saved task request metadata and stores each single-cast dispatch before forwarding to Auth. The executor checks fresh state between calls and stops on the first refusal, uncertainty or foreground deadline. The effect loop has a fifty-five-second budget and reserves thirty-five seconds before another call; slow responses therefore produce explicit partial results. Returned requested, attempted-gateway and confirmed-effect counts, failure position, retry deadline, uncertainty and refresh success remain distinct. Refresh failure never repeats a known effect. No remainder resumes automatically.

`get_spell_command` retrieves only the actor's original action and recovers saved attempts through private receipt lookup. A missing receipt or unknown upstream outcome never proves failure. Retained accepted/dispatching/unknown spell records fence task and spell effects even after Auth operational receipts expire. Private action history remains until explicit Workspace deletion; shared queue deletion does not erase accepted private game-effect receipts. A separate management-authenticated `/spells/history` export includes normalized own previews/results, excludes tokens/private attempt payloads and remains available after party departure or Workspace disconnect. The task-only portable envelope stays unchanged. Storage rejects more than one thousand retained actions rather than silently losing history.

Synthetic actual-SDK/D1 cases cover count bounds, token rotation/consumption, replay, concurrency, exact fresh state, partial refusal, delayed responses, uncertain acknowledgement recovery, both shared ownership fences, retained uncertainty, scopes, consent, namespace deletion, epoch changes, credential-free export and Workspace deletion. No live host, enrollment, deployment or real Habitica effect is qualified.

## Current-page refresh status

Status: implemented. Owner: application `RefreshCoordinator` and WebApp `AppSessionController`. Overlapping requests share one underlying domain fetch while each caller receives the requested priority and reason. A newer page or sign-in refresh replaces the refresh-view generation. Earlier asynchronous notifications and final boot/page completions continue caching fetched data but cannot publish domain-status dictionaries over the current page view. Generation selection and status publication share a short synchronous guard; notification dictionaries are copied before asynchronous cache loading. No additional Habitica request, offline promise, telemetry or credential handling is introduced. Controlled tests hold a shared Party fetch and an earlier page cache read to verify caller metadata, one network call and latest-page priority.

## Verified account readiness

Auth account status and Daily projection consume authoritative top-level Cron readiness from full UserGet. Missing/malformed readiness stays unknown and cannot authorize Daily projection. The PWA prefers a valid server boolean before the existing local-day fallback, preserving server timezone-transition decisions. Faithful fixtures cover contradictory legacy flag values, malformed/missing server readiness and pending-Cron scoring refusal. No new effect or UI control is introduced.

## Private Cron safety boundary

Status: locally implemented and disabled by default. Owner: Auth `CronService`, `cron-observation.ts`, shared day/scope contracts and the existing gameplay lease/attempt tables. MCP reaches the private boundary through the reviewed Workspace ledger; there is no direct public Auth route. Status requires independent profile/account/task/party read grants; execution additionally requires `habitica.cron.execute`. Existing OAuth grants do not acquire that scope automatically.

Observation verifies the enrolled actor, full UserGet readiness, day start/timezone/lastCron, living account stats/buffs/gear, active tasks and complete Daily checklist/due-state, plus current verified party quest facts when present. Due-state evaluation uses the final millisecond of the previous fixed-offset Habitica day. The fingerprint includes task/schedule/checklist state and private quest participation facts without returning full snapshots or member lists. Public status reports bounded unfinished Dailies, current minimal quest progress and explicit unknown damage/reward estimates. Unknown readiness, timezone transitions and capacity limits prevent preparation; no-party accounts can proceed. The neutral day fixture covers only fixed-offset boundary parity with existing C# logic.

Execution rechecks exact day/lastCron/fingerprint, current grants, enrollment and final quota state. One durable dispatch reserves an owned shared gameplay lease before the constant `/cron` POST. Successful original responses produce immutable applied receipts; duplicates only retrieve the original outcome. Every dispatched non-success or malformed response remains uncertain because user/task progress can precede quest processing. The uncertainty blocks task, spell and Cron effects. A later readiness change or advanced lastCron is reported only as observed day progress and never clears the fence or proves request completion. Lookup sends no Habitica request. Offline execution and automatic replay are unavailable.

Synthetic private-binding tests cover independent scopes/identity, no-party accounts, malformed/unknown state, changed day/Daily/checklist/quest, timezone limits, quota, persistence failure, overlap/replay, credential/grant revocation and shared uncertainty fences. No real enrollment, deployment, message or gameplay effect is qualified. External clients can race fresh reads; no atomic upstream lock or exact effect prediction is claimed.

## Reviewed remote Start New Day

Status: locally implemented, disabled by default, and awaiting real multi-user activation qualification. MCP exposes `get_cron_status`, `prepare_start_new_day`, `execute_start_new_day` and `get_cron_command` through the authenticated Workspace service. Preparation requires explicit intent and independent Cron/read grants. Preparation returns the exact day, yesterday's unfinished Dailies and checklist counts, resting/quest context, unknown risk fields, warnings and a two-minute expiry. The host must show the review and obtain approval before consuming the latest single-use token. No shared planning enrollment is needed for the own-account operation.

The typed Cron ledger preserves one request/action identity and one saved dispatch. Duplicate accepted execution returns the existing receipt. Stale day/account/Daily/checklist/quest facts reject the action before upstream effects. Task, spell and Cron effects share runtime ownership and retained uncertainty fences. After a lost acknowledgement, receipt lookup alone can confirm the original request. Later day progression remains separate evidence and cannot clear an unknown result. A confirmed Cron with a failed refresh remains confirmed, with `refreshSucceeded=false`.

Own history at `/cron/history` exports the public preview and receipt only. Credentials, token hashes and private dispatch payloads stay excluded. Accepted history and unknown fences survive operational receipt retention; deleting the owned Workspace removes the private history. No scheduled Cron, automatic execution from readiness, replacement-intent retry or Cron/spell chain is supplied. Live flags remain false in Auth and Workspace.

## Integration management and grant revocation

Settings provides an explicit status read with connected account, current ChatGPT permissions, credential verification/repair state and latest tool authorization time. Permission enumeration is bounded and reports truncation. Status does not revalidate credentials with Habitica or report background Workspace activity as ChatGPT use. Labels are encoded by Razor; credential envelopes, bearer tokens and provider grant identifiers remain private.

Separate confirmation controls revoke ChatGPT access, replace the current tab management credential, or revalidate the current browser sign-in credentials into the encrypted vault. Grant-only revocation retains the vault, stable profile identity and ordinary Workspace access. New access and refresh calls require the current durable grant epoch even when provider cleanup is incomplete. Old linking tickets, pending consent and unaccepted task/spell/Cron previews lose authority. Accepted commands and uncertain gameplay retain receipts and safety fences. Already dispatched effects cannot be undone. The separately opted-in background task executor requires separate Workspace disable/disconnect to pause.

Management rotation invalidates the previous management bearer without granting or revoking ChatGPT permissions. Late responses cannot restore a forgotten or replaced tab session. A lost rotation acknowledgement requires credential revalidation. Revalidation uses the existing enrollment path and does not silently create a new OAuth grant; a changed Habitica credential changes connection identity while preserving stable profile identity. Stored API-token replacement uses updated browser sign-in credentials. No management action sends a Habitica mutation.

## Private credential encryption-key operations

Auth provides a disabled private `CredentialKeyService` operator entrypoint. No public route, MCP binding or Workspace binding reaches the operator service. Explicit jobs re-encrypt stored credential envelopes into the configured current key version in bounded resumable steps. Per-profile generation/ciphertext checks preserve concurrent enrollment, credential replacement and disconnect. A job lease serializes overlapping steps. D1 couples each envelope change with cursor/counter progress; an interrupted transaction cannot advance only one side. Unreadable envelopes block the job with a safe code and resume after repair.

Operator summaries return job identity, versions and counts without credentials or profile identities. Inventory reports supported/unknown version counts and missing referenced keys. Current, referenced, unfinished-job or unverifiable inventory states prevent a key from being declared removable. The operator workflow requires stopping old-version writers and a fresh inventory before any separately authorized secret removal. The helper cannot prevent external secret changes outside the workflow. Profile-ID key rotation remains separate. Inactive job metadata expires after thirty days through bounded cleanup; encrypted credentials remain untouched by job cleanup. Real migration and deployment remain unqualified.

## Re-enrollment with retained Workspace history

Deleting and re-enrolling an integration profile preserves stable account identity and can reconnect to retained Workspace data through the new verified connection. Observed OAuth authority resets for the replaced connection; ordinary same-connection epoch rollback stays rejected. Canonical tasks and accepted receipts remain, and unaccepted old previews cannot execute.

A denied or unavailable spell/Cron receipt lookup cannot prove original failure. Saved unknown effects remain uncertain and continue blocking gameplay even after credential-profile deletion/re-enrollment. Only successful lookup of the original stored receipt can report a known applied/refused result. No replacement dispatch or automatic unfencing occurs.

## Dev deployment preparation

Status: local configuration preparation implemented. Owner: `deploy/prepare-dev.mjs` and `deploy/frontend-settings.mjs`. Public-only input produces isolated dev bindings, consistent OAuth/resource addresses and staged plugin configuration. Initial enrollment/OAuth/Workspace flags enable the first dev lane; gameplay projection, spells, party planning, background execution, key operations and live Cron remain disabled. Configuration generation performs no remote operation and stores no credentials. Invalid, incomplete or shared committed resource identities reject before output. Node qualification covers safe overrides, rejection paths, staged resources and source restoration after actual build-script success/failure. Hosted launch and real-account acceptance remain unrun.

## Integration traffic and saved-work status

Status: local safeguards implemented. Auth owns atomic D1 profile/network/global quotas, dispatch budgets, anonymous daily denial/429 counts and shared upstream cooldown/repair enforcement. Network keys use a keyed digest; read logs omit profile hashes, credentials and raw bodies. MCP and Workspace enforce ingress and verified-profile limits, with separate bounded original-receipt recovery. Quota refusal precedes dispatch reservation and retains review preconditions.

`GET /operations` returns only active-workspace saved-state counts and timestamps. `WorkspaceCloudClient`, `WorkspaceDataController` and `WorkspaceSettingsPanel` provide a manual status action, bounded response validation and account/session late-result rejection. Pending, fifteen-minute stalled, uncertain, blocked and expired-lease states remain distinct. No status action calls Habitica, starts execution or retries an effect. Worker concurrency/isolation fixtures and app HTTP/component tests own local qualification; real traffic capacity remains unqualified.


## User actions and opt-in agent test controls

Status: normal source/client defaults and the production frontend use OwnerIsolatedTestingEnabled false and PartySyncEnabled true. Ordinary users retain existing spell, party-sync/management and manual Start New Day controls subject to normal permission, freshness and confirmation requirements. OwnerTestingPolicy, HabiticaMutationGuard and the application controls apply party-management/Cron restrictions only when an explicit agent test profile is selected.

All sixteen supported class spells retain ordinary class/unlock, mana, target, freshness, sequential execution and rate controls in both profiles. The UI, controller and API client add no test-mode self-spell whitelist. Party departure remains rejected independently of isolation, including encoded route variants. The production deployment generator explicitly selects ordinary user defaults rather than inheriting test settings. No optional backend gameplay flag or executor is enabled by the frontend correction.

Synthetic API checks cover every supported spell in normal/test profiles, controller checks cover targeted and party spells in both profiles, and Razor checks cover all four classes. Explicit test profiles still refuse party management and Start New Day before dispatch. Public production settings/asset verification is distinct from real account gameplay testing; a new live test round remains unselected.

## Foreground API pacing

Foreground Habitica requests slow before the estimated budget approaches exhaustion. Concurrent calls share one dispatch gate, retain cancellation and honor server cooldowns. Failed calls reduce subsequent throughput; sustained success restores throughput gradually. The client exposes anonymous pacing diagnostics and never automatically repeats failed gameplay or task actions. Independent browser devices and server traffic require separate admission coordination.

## Optional server admission

The server contains flag-controlled durable admission for shared and per-profile upstream budgets. Admission slows early, reserves headroom, retains cooldowns and recovers gradually after failures. Rejected requests return a retry deadline before dispatch. The feature remains disabled in shipped configurations; existing quotas and safety fences remain active independently. Local synthetic tests qualify admission and uncertain-effect protection.

## Reviewed Daily recurrence authoring

Canonical createDaily and updateTask commands accept optional daily, weekly or monthly recurrence with interval, start date, UTC or supported IANA region timezone and applicable weekdays/month dates. The reviewed MCP preview includes the complete schedule. A scheduled Daily completion must match the canonical rule; an unavailable timezone produces a refusal. Unscheduled Dailies retain explicit dated completion.

Recurrence edits and clearing remain Companion-only and reject linked, pending or uncertain projections. Clearing changes future scheduling without deleting occurrence history, completion records or original receipts. The basic PWA editor has no recurrence controls; mobile authoring, history review and agenda integration remain separate work.

## Companion-only recurring lifecycle

Reviewed skipOccurrence creates one skipped record for a scheduled pending occurrence of an active unlinked Daily. Completed/skipped dates, paused tasks, unavailable schedules and unsupported mappings are refused. Skipping creates no completion or Habit score. The receipt reports one accepted occurrence and zero game effects.

Reviewed pauseTask and resumeTask change an unlinked Daily/Habit between active and paused states. Paused work refuses new completion/scoring commands. Resume retains the rule and history and creates no automatic events. Explicit restart replaces recurrence through a separate reviewed updateTask command; past skipped/completed dates remain retained. No lifecycle command pauses Habitica penalties, changes a linked schedule or authorizes reward reversal. Basic PWA action controls remain pending; application adapters and MCP expose matching reviewed commands.

## Canonical combined task search

Status: implemented in the local Application service and optional canonical MCP/Workspace read API. WorkspaceTaskQuery and shared task-query.ts search task titles and notes with AND filters for kind, state, native priority, project, effective category, label, inclusive planned/deadline civil dates and projection mapping state. Direct task category overrides the project category. Archived tasks remain excluded; every other state remains included unless filtered. Undated tasks do not match bounded date filters. Metadata filters use exact canonical IDs. No Habitica request or mutation occurs. The Application service works on an offline canonical snapshot; remote MCP requires the existing connection and tasks:read grant.

Text uses NFKC normalization, invariant lowercase, explicit U+0130 expansion and Greek final-sigma folding. Search performs substring matching without accent stripping or a query language. Results retain document order and return at most one hundred tasks. Nonzero offsets require expectedRevision from the preceding result; a revision mismatch rejects the page so the client can restart. A linked projection mapping does not confirm outstanding effects; original command receipts remain authoritative. Invalid filters fail explicitly. Saved views, staleness/review policies, reminders and mobile filter controls remain Pending.

## Canonical civil-date agenda

Status: local Application service and optional read-only MCP/Workspace API implemented. WorkspaceAgenda and shared task-agenda.ts expose explicit inclusive civil-date ranges through list_companion_agenda. The service merges planned/deadline labels and active scheduled Daily occurrences into one task/date row, ordered by date then canonical task order. Retained completed/skipped Daily records override generated pending dates and remain visible after schedule replacement or pause. Archived occurrence dates cannot regenerate pending work. Unscheduled Dailies produce no virtual occurrences; old pending records outside the current active rule stay hidden. Explicit dates on unfinished waiting/someday/paused work remain visible. Completed/cancelled/archived tasks stay excluded; completed Todo history remains separate.

Reads create no task, occurrence, completion, command or game effect. No Habitica request or due-state assertion occurs. Application queries work on offline canonical snapshots; MCP requires the existing remote tasks:read connection. ruleTimeZoneId names the current rule and supplies no original historical timezone. Dates remain all-day labels without instant conversion. Invalid ranges, stale revisions, unavailable active schedules and capacity failures return no partial agenda. The maximum range is 366 days, page size one hundred, materialized row count ten thousand and active-rule date-evaluation count 250000. Nonzero offsets require the returned revision; capacity failures require narrower dates. Production calendar/planning controls, Inbox and completed-history/restore workflows remain Pending.

## Canonical organization reads

Status: local Application and optional MCP/Workspace read API implemented. WorkspaceOrganization and shared organization-query.ts expose nonarchived categories, projects, labels, goals and one project section list. Projects can be selected by exact category assignment, including a retained archived-category reference. Section reads require a nonarchived parent and sort stably by position. Other lists preserve canonical document order. Every result identifies the workspace and revision; one hundred records per page and revision-guarded continuation prevent silently mixing changed snapshots. Invalid fields, identities, bounds and stale pages fail explicitly.

Goal summaries reuse WorkspaceRules.Progress semantics: only explicitly linked nonarchived/noncancelled Todos are eligible. Completed Todos count toward completion; Daily occurrences and Habit scores never contribute. Zero eligible Todos supplies no calculable completion fraction. Application reads work on an offline canonical snapshot; MCP requires the existing connection and independently enforced tasks:read grant. Names and IDs remain own-workspace data and authorize no actions. No Habitica request, schema migration, command write or organization editing occurs. Local fixture and synthetic MCP tests qualify reads; production UI and actual host/model behavior remain unqualified.

## Task-only sync status

Status: optional read-only MCP/Workspace tool implemented. get_task_sync_status accepts no arguments and reports one active own-workspace observation: canonical revision/store version, executor opt-in and deployed availability, durable task-command state counts, requested/confirmed effect counts and the oldest pending/leased/executing acceptance timestamp. Saved commands and confirmed effects remain distinct. Unknown outcomes require the original get_task_command receipt; counts do not prove exactly-once rewards or refresh Habitica.

The Workspace query joins current active connection/epoch and task rows in one D1 statement. Missing authorization fails; malformed or unsupported durable rows return unavailable rather than zero or partial counts. The tool aggregates no party, spell or Cron actions, starts no execution and changes no tasks or executor flags. Normal authorization grant-epoch maintenance retains the existing lifecycle behavior before tool dispatch. MCP requires tasks:read independently at both boundaries. The browser operational-status UI remains a separate broader aggregate; the new tool adds no production UI. Synthetic D1/MCP checks qualify counts, corruption refusal, connection invalidation and profile isolation. Live status and host activation remain unqualified.

## Reviewed native task placement

Status: Application, browser command adapters and optional canonical MCP implemented; production editor controls remain Pending. planTask changes only requested native planned date and category/project/section/label assignments. Planned date remains independent of deadline and Habitica due state. Explicit clear flags remove dates/assignments, while omitted fields remain unchanged. labelIds is a full replacement, bounded to one hundred unique exact IDs. New assignments must exist and be nonarchived. Retained section/project consistency is mandatory; an incompatible existing section needs an explicit clear or replacement.

The command preserves canonical task order, task kind/state, text, notes, deadline, recurrence, history and existing projection mappings. Linked, pending, conflicted and uncertain mappings remain unchanged; planning produces no projection effect, completion or Habit score. A projection request or any game-facing patch field is rejected. Original previews/receipts and revision checks govern acceptance. Portable journals require matching accepted planning fields and zero game effects. Browser adapters reject altered targets/fields and contradictory effect receipts. The Application path persists offline; MCP requires the connected own-workspace write grant. No Habitica request or organization creation/edit occurs.

## Retained canonical completion history

Status: local Application service and optional read-only MCP/Workspace API implemented. WorkspaceCompletionHistory and get_completed_history expose retained Todo/Daily completion events, including archived task/event records with explicit flags. Each row preserves event/task/command identity, UTC completion timestamp and the separate Daily occurrence civil date. History does not depend on Habitica recent-completed coverage and does not perform a game refresh.

Matching completion-command snapshots supply stored task text/notes with detailSource commandSnapshot. Missing historical snapshots use explicitly labelled currentTask details; current renamed text or new notes are never presented as original snapshot data. Search applies to the reported details; exact taskId remains available for renamed tasks. Filters combine normalized text/notes, exact task, Todo/Daily kind and inclusive UTC timestamp bounds with seven fractional digits. Results sort newest timestamp, then revision, with stable document-order ties. Pages contain at most one hundred records and require the returned revision for nonzero offsets.

The Application service reads an offline canonical journal. MCP requires the existing own-workspace tasks:read grant. Current document and matching snapshot rows come from one epoch-guarded D1 batch; invalid source data, stale pages and lost connections fail without partial or optimistic empty history. No event, command, projection or reward is created, removed or reversed. Production history controls remain separate work; retained Habit activity uses the dedicated read boundary below.

## Retained Todo restoration-cycle model

Status: canonical data/validation, history metadata and reviewed unlinked Todo restoration implemented in the application layer and MCP; production editor controls remain unavailable. Optional restorations records reference exact Todo completion and restoration-command IDs with UTC time, causal revision and archive state. Completion records remain intact. Successive Todo completions require a unique restoration revision strictly between the completions; overlapping cycles and mismatched references fail validation. Tasks with restoration history are completed exactly when the latest completion remains unrestored. Daily occurrence uniqueness stays unchanged.

Archived restoration records retain causal meaning. Event revisions define causal ordering without assuming a monotonic wall clock. Goal progress remains based on current eligible Todo state, so repeated history never inflates progress. Completed-history rows expose optional restoration identity, command, timestamp and archive metadata. Missing extensions and restoration metadata remain absent from default serialized documents/rows. Backup restore and frozen handoff preserve cycles; no game effect or restore action is created by reading or importing the model. Reviewed restoreTodo execution uses the exact completion identity and preserves every earlier cycle.

## Reviewed unlinked Todo restoration

Status: application, browser controller/cloud adapter and MCP restore_todo implemented with synthetic qualification. Production editor selectors and real client qualification remain unavailable. WorkspaceCommandEngine and Workspace acceptance require a completed nonarchived Todo and patch.restoreTodo.completionId from canonical completion history. The selected completion must belong to the exact task, lack a Daily occurrence and remain unrestored. Any retained mapping with a game identity or nonunlinked state blocks restoration, including archived mappings. Projection requests are rejected.

Acceptance changes current state to active and appends one TaskRestoration with the original command identity, causal revision and UTC timestamp. Task metadata/order, completion events, earlier restorations and mappings remain intact. No Habitica call, reward reversal, projection or score is created. A subsequent completion creates another completion event. Duplicate acceptance and original receipt reads retain the first result across later cycles; changed payloads cannot reuse the identity. Local acceptance requires browser storage; cloud acceptance requires the existing Workspace consent and write grant. Previews require explicit review. Missing acknowledgement retains the original command for receipt recovery.

Portable journal validation requires a matching restoration record, active Todo snapshot and zero game effects. Cloud previews must match the exact requested native intent; cloud receipts require one accepted event and no projection. Archived task undelete, Daily/Habit undo and linked-game restoration remain unsupported. Default tests use synthetic profiles and local service fixtures only.

## Retained canonical Habit activity

Status: Application WorkspaceHabitHistory, shared habit-history query, Workspace D1 reader, MCP get_habit_history and packaged habitica-activity-review guidance implemented with synthetic qualification. Primary data is canonical HabitScores and matching accepted command snapshots; no Habitica API call is required. Local evaluation works from a stored snapshot, while MCP requires an active consented cloud workspace and tasks:read. Production activity controls and real model/host activation remain unqualified.

The read retains each up/down event with exact event/task/command identity, UTC recording time and separate task/event archive flags. Batched events from one command remain distinct. Exact task, direction, normalized title/notes and inclusive UTC bounds combine; newest precise timestamp, revision and stable event order determine results. Pages have at most 100 rows and guard the canonical revision. Unknown exact tasks return no records; stale pages, malformed source and lost authorization fail explicitly without optimistic empty history.

Matching scoreHabit snapshots supply recorded details. Missing snapshots explicitly label currentTask fallback, and historical null notes never borrow current notes. Present mismatched operation/task/direction or invalid snapshot details fail unavailable. No event, command, score, restoration or game effect is created. Accepted events do not establish confirmed rewards or streaks; original command receipts supply effect status separately. The narrow read has no account selector and does not expose credentials. Neutral and synthetic SDK checks cover archive retention, precise bounds, batches, coherent reads and authorization.

## Eligible unblocked Todo reads

Status: Application WorkspaceUnblockedTodos, shared unblocked-todos query, current Workspace D1 read, MCP get_unblocked_todos and read-only activity guidance implemented with synthetic qualification. Canonical task identities, states and DependsOn provide the data. Local evaluation requires a stored valid document; MCP requires current consented workspace access and tasks:read. No Habitica call or background executor is required. Production review controls remain unavailable.

Candidates are nonarchived active Todos whose every explicit dependency currently has Completed state. Completed archived dependencies satisfy the check; archiving incomplete work or cancelling a dependency does not satisfy completion. Restore removes the dependant from eligibility and can expose the restored Todo as active work. Dependency checks use the full document before project/category/date/text filters and paging. Existing combined task filters apply except kind/state overrides. Results preserve document order and the canonical revision; continuation requires the matching revision. Invalid graphs, source errors and lost authorization fail without optimistic candidates or empty data.

The read changes no task, history, command or projection. Eligibility is separate from automatic recommendation ranking, parent inheritance, Daily occurrence readiness and game permission. Task text remains untrusted data; no account selector or credential enters the read. Neutral cross-runtime and synthetic SDK tests cover filters, all task/dependency states, archives, completion/restoration transitions, paging, isolation and unavailable reads.

## Reviewed native Todo relationships

Status: Domain WorkspaceTaskRelationships, Application/Worker relateTodo acceptance, browser controller/cloud adapter, MCP relate_todo and packaged task guidance implemented with local synthetic qualification. Production selectors and real client activation remain unavailable. Canonical Todo parent/dependency identities provide the data; no Habitica API request occurs. Local saves require browser storage; cloud review requires existing consented workspace access and the task write grant.

The relationships-only patch supports partial parent changes, explicit parent clearing and an optional full dependsOn replacement of at most 100 unique Todo IDs. New references must identify nonarchived Todos and differ from the target. A parent can also be an explicit dependency; no implicit dependency is added. Combined parent/dependency cycles fail canonical validation. Omitted references remain unchanged. A completed/cancelled Todo retains the current state and history; relation edits never reopen work, inherit a project or complete children.

Native acceptance preserves task order, metadata, history and mappings, including pending/uncertain game fences, and rejects projection requests. Receipts report zero accepted activity events and zero game effects. Previews bind exact target, parent and full dependency set; lost acknowledgement retains the original command for recovery. Portable snapshots must match intended relationships and contain no fabricated effects. Invalid references, stale revisions, mixed patches and cycles never save. Test profiles and public users share the same canonical validation; no credentials enter arguments or receipts. Non-Todo relationship/state authoring and production controls remain separate.

## Portable native organization history

Status: Domain organization commands/snapshots/history validation and shared Worker model implemented with local synthetic persistence qualification. Organization acceptance is implemented by the local Application service and shared Worker engine; reviewed organization browser/MCP adapters are implemented locally. WorkspaceOrganizationCommand identifies an exact category, project, section, label or goal with a separate entity ID and create/update/archive action. WorkspaceDocument.OrganizationHistory retains payload hashes, UTC acceptance time and entity snapshots without reusing task or gameplay receipts.

Typed command patches allow names within the existing 256-character bound, project category set/clear, section project on creation with nonnegative Int32 position, and optional full goal task-ID replacement with at most 100 requested IDs. Historical snapshots can retain larger legacy task sets and omitted references. Combined history requires unique command identities, increasing causal revisions, exact snapshot transitions and latest metadata matching the canonical entity. Archived entries retain referenced entities and names without cascade deletion. Acceptance clocks need not increase when revisions remain causal.

Root parsing validates structure and metadata consistency; complete journal validation checks hashes and separation from task command/activity identities and revisions. Unknown effect fields, malformed snapshots, duplicate IDs, contradictory patches and corrupted portable hashes fail validation. Default serialization omits unused history and preserves explicit empty history. Existing native task saves retain metadata history, while goal progress continues to use current Todo state. Browser backup/restore and frozen cloud handoff/export preserve the extension. No Habitica request, game effect, new secret or D1 migration is introduced. Local evaluation works offline from a stored journal; cloud storage remains under existing workspace consent and current capacity limits. Production organization controls, publication and real-client qualification remain separate.


## Native organization acceptance

WorkspaceOrganizationEngine/WorkspaceOrganizationService and Worker acceptOrganization/submitOrganization create, update and archive exact categories, projects, sections, labels and goals. Creation appends an unused entity ID; updates retain array order and omitted metadata. New project category and section project references require nonarchived entities. Section project membership remains immutable after creation. Explicit goal replacement accepts at most 100 distinct nonarchived Todo IDs; omitted legacy sets remain intact.

Archival retains linked tasks, sections, progress source history and prior receipts without cascade deletion. Native edits preserve task state/order, projection mappings and gameplay journal entries, including pending or uncertain game outcomes. No Habitica request or effect is generated. Exact original command hashes recover retained receipts before revision, archived-state or authority checks; changed identity reuse and task-command collisions fail. Invalid references, mixed fields, stale revisions and local writes to frozen/cloud-owned workspaces cannot save.

The account-bound store commits the canonical document and typed receipt in one optimistic revision change. Conflicts reload a fully validated journal before duplicate recovery; ambiguous storage acknowledgements report unknown acceptance and preserve the original command for retry. Storage unavailability and cancellation never report successful saves. Local operation works offline from stored canonical state. Production controls, publication and live qualification remain separate.


## Reviewed native organization transport

Status: local Auth/MCP/Workspace adapters, typed browser client and packaged organization guidance implemented and synthetically qualified. prepare_organization_command reviews exact category/project/section/label/goal create/update/archive intent; accept_organization_command accepts the original prepared command; get_organization_command reads the exact native receipt. Existing organization list tools resolve canonical IDs. Preparation allocates creation entity IDs and command IDs server-side and accepts no account selector, generic JSON or URL proxy.

OAuth organization writes require independent workspace.organization.write consent at both MCP and private Workspace boundaries. Existing task-write grants remain unchanged. The authorization page separately names native metadata changes, retained tasks/history and zero game effects. Receipt reads require tasks:read. Browser own-data adapters use the existing authenticated Workspace session and cloud-storage consent; metadata writes still require exact preview review. Offline metadata authoring remains available through the account-bound local service.

Durable profile/request and profile/command bindings reuse workspace_preparations without a schema migration. Ten-minute previews bind exact normalized intent, canonical revision, connection/access/grant epochs and correlation identity. Preparation changes no canonical entity; acceptance commits the native history and metadata in one canonical revision with no task/gameplay entry. Archived relationships remain retained. Expired/stale or changed-epoch previews cannot save. Original accepted request bindings survive expiry cleanup and grant changes with retained workspace history. Workspace deletion removes bindings through existing ownership cascades.

The browser adapter binds every intent field and normalizes only server-issued command and creation entity IDs. Native snapshots/hashes and original receipt identities must match, including zero accepted task events and zero effects. Changed sessions discard late replies; failed receipt reads report unavailable state and preserve original IDs. Uncertain acceptance never authorizes a replacement action. Synthetic SDK, isolated D1 and browser transport tests cover identity, retention, consent, corruption and partial-failure paths with zero upstream writes. Production UI and real host enrollment remain unqualified; no backend publication is included.


## Reviewed native Todo planning states

Status: WorkspaceTodoState, SetTodoState acceptance/portable validation, shared Worker parser, scoped MCP and browser adapters implemented locally. The state-only patch accepts active, waiting, someday or cancelled on an exact existing nonarchived noncompleted Todo. Completed targets require the exact-event restoration contract; direct completed/paused desired states, Daily/Habit targets, mixed fields, projection requests and stale revisions fail. Legacy paused Todo sources may return to supported native planning states.

Acceptance preserves task order/details, organization, dates, dependencies, histories and game mappings. No task activity or Habitica effect is added. Existing game outbox commands remain independent and are neither cancelled nor replayed. Current goal progress excludes cancelled tasks from eligibility; unblocked candidates require active Todo state and completed explicit dependencies. Cancelling a prerequisite never satisfies a dependent task. Completion and restoration retain exact history ownership. Native operation works offline through the account-bound local store; optional cloud execution uses the same canonical rules and original receipts.

Identity v7 binds the exact desired state; unused extensions retain v1-v6 encodings and default serialization. Browser reviews bind exact target/state and preserve original request/command recovery after acknowledgement loss. MCP set_todo_state requires the existing task-write grant with matching private enforcement. Packaged project/goal guidance composes independently reviewed native organization, task placement, dependency, state and completion tools without implicit game projection. No credential or account selector enters command arguments. Production controls and live-client qualification remain separate; optional active-work stage metadata follows the contract below.


## Optional active Todo execution stages

Status: local implementation and synthetic qualification. Canonical Todo records may retain optional executionStage inbox/next/inProgress. Missing or cleared metadata means Inbox while active. Current native state takes precedence: waiting, someday, completed, cancelled and paused retain a dormant stage preference rather than another current status. Completion, restoration, state changes and archival preserve the preference. No stage inference comes from dates, project placement, parent links or dependency eligibility.

Reviewed setTodoStage accepts only patch.todoStage.stage or explicit clearStage on an exact nonarchived active Todo. Separate state/restoration commands handle inactive work. Native stage acceptance preserves order, state, dates, organization, dependencies, histories and every game mapping with zero new activity events or effects. Existing accepted game commands remain independent. Browser and MCP adapters bind exact stage/clear intent and recover original receipts; MCP set_todo_stage requires the existing task-write grant. Production board controls and real-client qualification remain separate; stage-aware reads follow below.


## Active Todo stage filters

Status: local canonical read implementation. list_companion_tasks and get_unblocked_todos accept optional executionStage inbox/next/inProgress. Stage predicates match only active nonarchived Todos. Missing stored preference matches Inbox; inactive or archived dormant metadata never matches. Explicit incompatible kind/state selectors fail instead of silently changing meaning. Omitted stage retains existing combined-query behavior.

Other filters combine with AND and preserve canonical order, revision-bound pages and totals. Dependency candidates still require every explicit prerequisite to be Completed, including retained completed archived prerequisites. Next and InProgress never bypass dependency checks or supply ranking, dates, completion or game permission. Existing own-profile tasks:read access applies with no write or effect. Named saved-view persistence and production list controls remain separate.


## Portable saved task-view definitions

Status: local data model and evaluator. Optional SavedTaskViews retain globally unique view IDs, revision, name, archive flag and a query definition. Source tasks uses combined task filters; source unblockedTodos additionally requires current explicit dependency completion. Definitions retain fixed text, kind/state, urgency, organization, civil-date, projection and active-stage predicates. A saved query contains no task results, pagination, account selector or arbitrary expression. Fixed dates remain fixed rather than automatically meaning Today.

Names, enums, source/filter compatibility, date ranges and exact organization references validate on import. Archived organization targets remain valid retained filter references. Missing/null collection preserves default serialization; explicit empty remains explicit. Local storage, backups and frozen handoff retain definitions inside existing document limits. Evaluation reads current task state with fresh revision-bound paging and never mutates tasks or game history. Missing/archived views fail explicitly. Transport tools, relative presets, reminders and production saved-list controls remain separate; reviewed native authoring follows below.


## Reviewed native saved-view commands

Status: local native command implementation. SavedTaskView is an appended native organization kind. Creation requires name and complete query; update accepts a name and/or full query replacement. Omitted query stays unchanged and an explicit empty filter replaces earlier predicates. Archive retains the last definition and all original native receipts. Filter targets may reference retained archived organization records. Exact active view identity, current revision and valid source/filter shape remain mandatory.

Saved-view edits use separate atomic OrganizationHistory with typed accepted query snapshots. No task activity, game outbox entry or projection is added. Original command receipts remain recoverable after later replacements or archival; changed intent cannot reuse an identity. Native v2 hashes bind the complete definition while older metadata commands retain v1 encodings and default serialization. Transport tools, actual client qualification, relative-date presets, reminders and production controls remain separate.


## Scoped saved-view workflows

Status: local browser/MCP adapter implementation and synthetic qualification. tasks:read exposes list_saved_task_views and query_saved_task_view with exact view ID and caller paging only. Current profile/connection/access epoch binds fresh canonical reads. Missing/archived views, stale pages and malformed definitions fail explicitly; unavailable reads return no fabricated counts or mutation uncertainty. Query results use current task/dependency state with fixed stored date semantics.

Independent workspace.organization.write grants reviewed savedTaskView create/update/archive through existing native preparation and acceptance. Browser reviews bind the full query definition and retain exact original receipt recovery. Task-write permission alone cannot edit saved views. Auth consent and cloud-storage disclosures name saved views. Packaged guidance explains complete query replacement, exact IDs, fixed dates, dormant stages and the absence of implied task mutation. Production saved-list controls, relative presets, optional review/reminders and real-client qualification remain separate.


## Explicit development capability profile

Current availability: the explicit development profile was published on 2026-10-19 from source aab2b87 to the existing site. Earlier disabled-by-default statements describe templates and pilot defaults. [Current evidence](../history/validation/development-publication.json) records enabled availability separately from account consent and actual live checks. Background consent remains separate; no scheduler casts spells or starts a Habitica day. Broad release/client/device qualification remains open.

Status: implemented public configuration generation. Owner: deploy/prepare-dev.mjs. Default pilot generation preserves disabled game-effect availability. Explicit featureProfile development enables only recognized implemented backend flags and authenticated frontend party-planning controls; unknown profile/flag mappings fail closed. Normal user controls retain existing defaults. No generated setting supplies OAuth grants, account planning/executor consent, prepared action approval, key-operation callers or credential values. Publication uses the selected existing website and matching current bundles. Real qualification and publication evidence remain separate from generation tests.

## Canonical task list controls

Status: local UI implementation. Owner: CompanionTaskList inside CompanionTasksPanel. The loaded account-bound browser/cloud snapshot supplies search, type, native state, urgency and active Todo stage filters plus named saved-list selection. Canonical application engines evaluate combined filters, fixed civil dates and dependency eligibility. Results preserve canonical order and show matching counts, empty states and twenty-row pages. Invalid or missing saved definitions report unavailable without all-task fallback.

Workspace identity changes clear selection and filters; revision and query changes reset paging. Pending task intents disable browsing controls without discarding original recovery. The surface renders task/view names as escaped text and performs no API call, task mutation, projection, consent change or background activation. Offline browser snapshots remain usable; cloud data freshness comes from explicit workspace loading. Saved-list authoring, relative-date presets and optional review/reminder policy remain separate. Component tests cover current evaluation, fixed dates, invalid definitions, pagination, isolation, escaping and pending controls; local layout evidence does not qualify real devices.
