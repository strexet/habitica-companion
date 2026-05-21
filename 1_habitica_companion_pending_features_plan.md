# Habitica Companion App — Pending Feature Implementation Plan

Status note, 2026-05-21: this is a historical source plan. Many MVP items in this file are now implemented. Use `FUTURE.md` for the validated remaining backlog and `FEATURES.md` for the current implemented feature behavior.

This document is optimized for AI coding agents. It is intentionally explicit about feature boundaries, data ownership, UI behavior, edge cases, and MVP scope.

## Agent Rules

- Do not redesign the whole app unless explicitly requested.
- Keep existing architecture, routing, naming, and visual system where possible.
- Prefer small, incremental changes per feature section.
- Implement features in the order listed in this document.
- Do not invent Habitica mechanics. Use Habitica API data and known Habitica concepts.
- Use real user-facing names wherever the UI is visible to the user.
- Avoid showing internal IDs on cards unless the app is in developer/debug mode.
- Use loading skeletons and partial refreshes instead of blocking the whole app.
- Do not hide the side menu during refresh when user data already exists.
- Do not break current pages while improving them.
- If API fields are missing, add safe placeholders and clear TODOs instead of hardcoding fake values.
- For DB changes, add migration-safe schema additions. Do not delete existing data.
- For all shared party state, assume multiple party members can update data at the same time.
- Use optimistic concurrency/versioning for shared queue state.
- Keep animations subtle and useful. Do not add distracting motion.
- Before implementation, inspect the current repo instead of assuming this document is complete.
- Follow repository docs in this order: `RULES.md`, `HABITICA_API.md`, `HABITICA_TOOL_REFERENCES.md` when fetching/parsing Habitica data, `TECHNICAL.md`, `FEATURES.md`, then affected code.
- Current primary client is `Habitica.WebApp`, a Blazor WebAssembly PWA using MudBlazor. Do not introduce React/TanStack/SWR or another UI/data-fetching framework.
- Current application orchestration belongs in `Habitica.Application`; UI pages/components must call application services/query facades, not raw Habitica API or storage code.
- Current API calls belong in `Habitica.Api`; do not call Habitica API directly from Razor pages/components.
- Current local storage belongs in `Habitica.Storage`, backed by IndexedDB through the existing Dexie boundary.
- Current shared party backend exists under Cloudflare Pages Functions + D1/KV-style bindings. Extend existing `functions/api/party-sync` and `migrations` surfaces when shared party state is needed.
- Never send Habitica API tokens to Cloudflare party-sync or app-data sync endpoints.
- When implementation changes feature behavior, update `FEATURES.md`. When sync architecture/foundational backend behavior changes, update `TECHNICAL.md` too.

## Current Repository Baseline

The current repository state should shape implementation choices.

```text
App shell:
- `src/Habitica.WebApp`
- Blazor WebAssembly PWA
- MudBlazor UI components
- Pages already exist for Dashboard, Inventory, Party, Spells, Tasks, Settings, Sign-In, and diagnostics/live tests.

Architecture:
- `Habitica.WebApp` is UI only.
- `Habitica.Application` owns use-case orchestration and sync workflows.
- `Habitica.Api` owns Habitica API v3 calls, headers, rate-limit handling, DTOs, and redaction.
- `Habitica.Storage` owns IndexedDB/Dexie local snapshot storage.
- `Habitica.Domain` and `Habitica.Rules` own models, formulas, and deterministic calculations.

Current implemented behavior:
- Credential sign-in exists.
- Manual sync exists.
- Cached account dashboard exists.
- Read-only task browsing exists.
- Read-only inventory/equipment explorer exists.
- Read-only party overview exists.
- Party page already includes active quest progress, member counts, CRON summary, CRON graph, and member CRON state.
- Local-first account/task snapshots exist.
- Cloudflare app-data sync exists for encrypted sync payloads.
- Cloudflare party-sync already exists and stores shared party state / CRON events / basic quest queue / quest votes.

Important consequence:
- Do not implement new feature state as unrelated ad-hoc browser globals or isolated Razor-page fields.
- Reuse the existing session controller/state patterns until a dedicated application query/store abstraction is created.
- For shared party queue work, extend the existing party-sync D1 schema and endpoint instead of creating a separate backend API.
```

---

## Reference Notes

- Habitica API v3 is the source of truth for user, task, party, quest, inventory, skill, and cron actions.
- Habitica has a Cron API route. The API documentation describes Cron as a route that runs Cron and assumes the user has already been shown the “Record Yesterday’s Activity” flow when relevant.
- Habitica party buffs are temporary and persist until each affected party member’s next Cron.
- Existing Habitica tooling such as Habit History Connector shows that task history can be useful for graphs and reports.
- Use skeleton screens for page/content loading states where the final content structure is known. Skeletons should mimic the target layout and should not be used for instant sub-1-second loads.
- For long operations or explicit forced refreshes, visible loading state is required.
- Health-style charts are a good model for task statistics: summary cards, trend/highlight sections, and week/month/year detail views.

---

# 1. Party Page Quest Improvements

Improve the party page quest experience by making all quest-related UI consistent and by adding shared party-level queue logic.

Current repo adaptation:

```text
Affected existing UI:
- `src/Habitica.WebApp/Pages/PartyPage.razor`

Affected shared backend:
- `functions/api/party-sync/[partyId].js`
- `migrations/0001_party_sync.sql` or a new forward migration

Existing party page already has:
- party summary
- quest progress snapshot
- current progress
- user's pending progress
- pending party progress
- estimated post-Cron progress
- CRON summary
- CRON graph
- member CRON states

Existing D1 migration already has:
- `party_state`
- `party_cron_events`
- basic `party_quest_queue`
- basic `party_quest_votes`

Do not replace these. Extend them.
```


The party page should include four connected quest sections:

```text
Active Quest Card     -> currently running party quest
Party Quest Queue     -> quests the party is considering next
Quest Pool            -> quests available from party members
Recently Completed    -> short history of completed party quests
```

## 1.1 Active Quest Card

The party page already has a current quest card for the active quest being progressed by party members. Update this card to use the same visual language as the new quest cards used in the quest pool and queue.

The active quest card can be larger than regular quest cards because it needs to include live quest-specific information that is not relevant for queued quests.

The active quest card should show:

```text
Real quest name
Quest owner / starter
Quest type
Quest description
Current progress
Pending damage
Boss HP / collection progress, depending on quest type
Participant status
Started date
Small rewards section
```

The rewards section must be compact and must use real user-facing Habitica reward names, not internal IDs.

Example:

```text
Rewards
- 450 Gold
- 300 XP
- Wolf Cub
- Moonstone
```

The active quest card must not have voting controls because the quest is already active.

Allowed active-card actions:

```text
View quest details
View participants
View rewards
Open in Habitica
Refresh quest state
```

If the active quest was started from the party queue, preserve that relationship internally:

```text
activeQuest.queueEntryId = originalQueueEntryId
```

This allows the app to move the quest cleanly through the lifecycle:

```text
Queued -> Active -> Recently Completed
```

## 1.2 Quest Pool

The quest pool is the full list of quests available to the party.

Every party member contributes to the quest pool automatically when their Habitica data is refreshed. If a member owns quest scrolls, those quests should appear in the party quest pool.

The quest pool must use real quest names, not internal quest IDs. Quest rewards must also be displayed using real user-facing names.

A quest pool card should show:

```text
Real quest name
Quest owner / available owners
Quest type or category
Small rewards section
Availability count, if multiple members own the same quest
Action to add the quest to the party queue
```

Only the quest owner can add their own quest copy to the queue. Party members must not be able to spend or queue another member’s quest scroll directly.

Cards owned by the current app user should be visually highlighted. Cards not owned by the current app user should use a neutral grayish style, but the information must remain readable.

## 1.3 Party Quest Queue

The party quest queue is a shared queue stored in the party database. It represents the list of quests the party is considering for the next run.

Quest owners can add their own quests from the quest pool into the queue. Other party members can vote for queued quests, but they cannot start or spend another user’s quest scroll.

Queued quest cards should be large and readable.

Each queued quest card should show:

```text
Real quest name
Quest owner
Quest type or category
Queue position
Vote count
Voter list
Small rewards section
Owner readiness state, if available
Recently completed indicator, if applicable
```

The rewards section must be compact and must use real reward names instead of internal IDs.

Cards owned by the current app user should have a distinct background or accent. Other cards should use a neutral style, but should still support voting and display the same information.

## 1.4 Voting Logic

Voting is shared between all party members through the party database.

MVP behavior:

```text
Each party member can vote once per queued quest.
Clicking the vote button again removes the vote.
Votes are stored per party member, not only as a number.
The vote count is visible on the quest card.
Hovering or tapping the vote count shows the list of party members who voted.
```

Example voter popover:

```text
5 votes
- Alice
- Bob
- Petr
- Kate
- Max
```

The queue order should be primarily based on votes:

```text
priorityScore =
    votesCount * 1000
    + ownerReadinessBonus
    + ageBonus
    - recentlyCompletedPenalty
```

Suggested scoring:

```text
votesCount =
    number of unique party members who voted for this quest

ownerReadinessBonus =
    +100 if the quest owner is active/recently synced
    +50 if the quest owner manually marked the quest as ready
    +0 otherwise

ageBonus =
    min(hoursInQueue, 72)

recentlyCompletedPenalty =
    300 if the same quest was completed in the last 7 days
    150 if the same quest was completed in the last 30 days
    0 otherwise
```

Voting should influence ordering, but should not fully block party decisions. A recently completed quest can still be queued, voted for, and started if the party really wants it.

## 1.5 Optional Advanced Voting

MVP should use simple one-vote-per-quest voting.

Later, the app can support limited vote budgets:

```text
Each party member has 3 active votes across the entire queue.
A member can move votes between quests.
The UI shows how many votes the current user has used.
```

Example:

```text
You used 2 / 3 votes
```

Do not implement limited vote budgets in the MVP unless specifically requested.

## 1.6 Queue States

Each queue entry should have an explicit state.

Recommended states:

```text
Queued
Selected
InviteSent
Active
Completed
Skipped
Removed
Expired
```

Recommended lifecycle:

```text
Quest Pool -> Queued -> Selected -> InviteSent -> Active -> Completed
```

Alternative endings:

```text
Queued -> Removed
Queued -> Expired
Selected -> Skipped
InviteSent -> Skipped
```

State meanings:

```text
Queued
The quest is in the shared queue and can receive votes.

Selected
The app recommends this quest as the next quest.

InviteSent
The quest owner has sent the Habitica quest invitation.

Active
The quest is currently active in Habitica.

Completed
The quest was completed and moved to recently completed history.

Skipped
The quest was selected or invited but not started.

Removed
The quest was manually removed from the queue.

Expired
The quest became stale or the owner no longer has the quest.
```

## 1.7 Recently Completed Quests

The party database should store a short history of recently completed quests.

This list is separate from the active quest and queue. It is used for queue prioritization, anti-repeat logic, analytics, and better UX.

When a quest is completed, record it in the party database:

```text
partyId
questKey
questName
completedAt
startedAt
ownerUserId
ownerDisplayName
participantsCount
rewardSummary
```

Recommended retention:

```text
Last 30-90 days
or
Last 20-50 completed quests per party
```

Recently completed quests should create a soft queue penalty, not a hard restriction.

Example UI label:

```text
Moonstone Chain
12 votes
Completed recently: 5 days ago
```

A recently completed quest must remain visible, voteable, queueable, and startable. The app should only make it slightly less likely to be automatically recommended as the next quest.

## 1.8 Party Leader and Owner Controls

Quest owners should be able to:

```text
Add their own quest to the queue
Remove their own quest from the queue
Mark their quest as ready / not ready
Start the quest invite when their quest is selected
```

The party leader should be able to:

```text
Remove stale queue entries
Pin a quest to the top
Force-select the next quest
Resolve queue conflicts
Lock queue changes during active quest selection
```

The party leader should not replace party voting by default, but should have moderation tools for broken, stale, or blocked queue states.

## 1.9 Database Design

The queue should be stored as shared party state using the existing Cloudflare party-sync backend.

Current migration already defines these D1 tables:

```text
party_state
party_cron_events
party_quest_queue
party_quest_votes
```

Do not create a second independent queue backend. Extend the existing migration set with a forward migration when schema changes are needed.

Current `party_quest_queue` is too small for the final feature. Extend it toward this shape:

```text
queue_item_id TEXT PRIMARY KEY
party_id TEXT NOT NULL
quest_key TEXT NOT NULL
quest_name TEXT NULL
owner_user_id TEXT NOT NULL
owner_display_name TEXT NULL
status TEXT NOT NULL DEFAULT 'Queued'
created_at_utc TEXT NOT NULL
updated_at_utc TEXT NOT NULL
selected_at_utc TEXT NULL
started_at_utc TEXT NULL
completed_at_utc TEXT NULL
sort_order INTEGER NOT NULL
manual_pin_rank INTEGER NULL
owner_ready INTEGER NOT NULL DEFAULT 0
version INTEGER NOT NULL DEFAULT 1
```

Current `party_quest_votes` already stores one vote per user per queue item. Extend only if needed:

```text
party_id TEXT NOT NULL
queue_item_id TEXT NOT NULL
voter_user_id TEXT NOT NULL
voter_display_name TEXT NULL
vote_weight INTEGER NOT NULL DEFAULT 1
created_at_utc TEXT NOT NULL
updated_at_utc TEXT NULL
PRIMARY KEY (party_id, queue_item_id, voter_user_id)
```

Add a recently completed quest table:

```text
party_id TEXT NOT NULL
quest_key TEXT NOT NULL
quest_name TEXT NULL
completed_at_utc TEXT NOT NULL
started_at_utc TEXT NULL
owner_user_id TEXT NULL
owner_display_name TEXT NULL
participants_count INTEGER NULL
reward_summary_json TEXT NULL
source_queue_item_id TEXT NULL
PRIMARY KEY (party_id, quest_key, completed_at_utc)
```

Quest pool storage can be implemented in one of two ways:

```text
Preferred MVP:
- derive quest pool from synced local user inventory + shared party snapshot when available;
- do not store every pool entry in D1 until cross-member quest inventory sharing is explicitly designed.

Future shared pool:
- add `party_quest_pool_entries` only when the app intentionally publishes members' owned quest scroll availability to shared party-sync.
```

If a shared pool table is added later, suggested fields:

```text
party_id TEXT NOT NULL
quest_key TEXT NOT NULL
quest_name TEXT NULL
owner_user_id TEXT NOT NULL
owner_display_name TEXT NULL
reward_summary_json TEXT NULL
available_count INTEGER NOT NULL DEFAULT 1
last_seen_at_utc TEXT NOT NULL
PRIMARY KEY (party_id, quest_key, owner_user_id)
```

Concurrency rules:

```text
Every queue mutation must check `version`.
Every successful queue mutation increments `version`.
Vote add/remove can rely on the vote table primary key plus updated queue read model.
If stale version is submitted, return conflict and let the client refresh shared party queue.
Do not overwrite `party_state.snapshot_json` as a side effect of a queue-only vote.
```

Security rules:

```text
Party-sync must verify membership before reading/writing party queue state.
Do not accept Habitica API tokens in the party-sync payload.
Do not store raw Habitica credentials in D1.
Do not trust display names from client for authorization.
```

## 1.10 Party Quest MVP Scope

Implement the first version with the following behavior:

```text
1. Update the active quest card to match the new quest card style.
2. Show real quest names and real reward names everywhere.
3. Add a compact rewards section to active, queued, and pool quest cards.
4. Build a shared quest pool from party members’ available quests.
5. Allow quest owners to add their own quests to the shared queue.
6. Allow each party member to vote once per queued quest.
7. Show vote count and voter list.
8. Sort the queue by vote count, then queue age.
9. Store recently completed quests in party history.
10. Apply a soft penalty to recently completed quests.
11. Let the party leader remove stale queue entries.
12. Move quests through clear states: Queued -> Active -> Completed.
```

Future improvements:

```text
Limited vote budget per member
Owner readiness toggle
Manual leader pinning
Recently completed quest labels
Advanced scoring settings
Queue expiration rules
Historical quest analytics
Automatic queue cleanup after quest start
```

---

# 2. CRON Button and Buff Warning

Implement a manual CRON button on the dashboard and protect users from casting party buffs before they have CRONed for the current Habitica day.

## 2.1 Goal

The app should help users avoid wasting buffs or creating confusing party timing.

Habitica buffs are temporary and persist until each affected party member’s next Cron. If a user has not Croned yet today and casts a buff, the buff may expire sooner than expected for that user or create confusing results around quest damage and daily reset timing.

## 2.2 Dashboard CRON Button

Add a dashboard button:

```text
Start New Day
```

Alternative label if the app already uses Habitica terminology:

```text
Run Cron
```

Preferred user-facing label:

```text
Start New Day
```

The button should only be available when the app believes the user has not Croned for the current Habitica day.

Visibility logic:

```text
Show button if currentUser.needsCron == true
Hide button if currentUser.needsCron == false
Show disabled loading state if needsCron is unknown and user data is currently loading
Do not show the button if no authenticated user exists
```

If Habitica exposes a reliable `needsCron`/equivalent field, use it. If not available, derive carefully from Habitica user state and custom day start rules, and mark the derived state as approximate.

## 2.3 CRON Button Flow

When the user taps the button:

```text
1. Show confirmation dialog.
2. Explain that this starts the new Habitica day.
3. Warn that missed Dailies may be processed.
4. Let the user cancel.
5. If confirmed, call the Habitica Cron route.
6. Refresh dashboard-critical data first.
7. Refresh tasks, party quest state, buffs, stats, and inventory after that.
```

Confirmation copy:

```text
Start new Habitica day?

This will run Habitica’s daily reset for your account. Missed Dailies may be processed, active quest progress may be updated, and temporary buffs may expire.

[Cancel] [Start New Day]
```

## 2.4 Buff Warning for Not-Croned State

When a user that has not Croned yet attempts to cast a buff, show a warning dialog.

This warning should appear before casting these party/self buff-type skills:

```text
Warrior: Defensive Stance — self CON buff
Warrior: Valorous Presence — party STR buff
Warrior: Intimidating Gaze — party CON buff
Mage: Earthquake — party INT buff
Rogue: Tools of the Trade — party PER buff
Healer: Protective Aura — party CON buff
```

Also consider showing a lighter warning for party-impacting non-stat support skills if needed:

```text
Mage: Ethereal Surge — restores MP to non-Mage party members
Healer: Blessing — restores HP to party members
```

MVP warning should be required for stat buffs only. Do not block offensive skills such as Brutal Smash or Burst of Flames unless there is a separate reason.

Warning dialog:

```text
You haven’t started your Habitica day yet.

Buffs last until Cron. If you cast this buff before starting your new day, it may expire sooner than you expect.

What do you want to do?

[Cast anyway] [Start New Day and Cast] [Cancel]
```

Button behavior:

```text
Cast anyway
- Immediately cast the selected skill.
- Do not run Cron.
- Refresh affected stats/buffs after success.

Start New Day and Cast
- Call Cron first.
- Refresh user state enough to confirm Cron completed.
- Cast the selected skill.
- Refresh affected stats/buffs after success.

Cancel
- Close dialog.
- Do not cast anything.
```

## 2.5 Buff Warning Suppression

Add a local-only optional suppression checkbox:

```text
Don’t warn me again today
```

Rules:

```text
Suppression applies only to the current user.
Suppression applies only for the current Habitica day.
Suppression must reset after Cron.
Do not suppress permanently unless a separate setting is added.
```

## 2.6 Error Handling

If Cron fails:

```text
Do not cast automatically.
Show error message.
Allow user to cast anyway as a separate explicit action.
Refresh user state if error may be stale.
```

If cast fails after successful Cron:

```text
Show cast failure.
Do not retry automatically unless the existing app already has retry logic.
Refresh user stats/buffs because Cron may still have changed state.
```

## 2.7 CRON MVP Scope

Implement:

```text
1. Dashboard Start New Day button.
2. Button visible only when user has not Croned for the current Habitica day.
3. Confirmation dialog before Cron.
4. Buff warning when not-Croned user attempts to cast stat buff.
5. Options: Cast anyway, Start New Day and Cast, Cancel.
6. Refresh dashboard-critical data immediately after Cron.
7. Do not implement automation/scheduled Cron in MVP.
```

---

# 3. Inventory Final Improvement

Improve the inventory page layout and make highest-stat values easier to recognize across all equipment-related card types.

## 3.1 Preset Layout

Current issue:

```text
When there is one preset, the preset card looks good.
When a second preset is added, it appears to the right.
This makes the layout feel cramped and less readable.
```

Required behavior:

```text
Preset cards should take the full available width.
Multiple presets should stack vertically.
Do not place preset cards side-by-side on desktop or wide screens unless a future explicit compact/grid mode is added.
```

Implementation rule:

```text
Use one-column preset layout by default.
Each preset card width = 100% of content container.
Preserve internal item-card layout inside each preset.
```

Do not redesign saved presets. Only change how multiple preset cards are arranged.

## 3.2 Highest Stat Highlighting

For equipment items, make the highest stat value visually recognizable.

Apply this feature to:

```text
Normal equipment cards
Best-in-category item cards
Battle gear item cards
Saved preset item cards
```

## 3.3 Highlight Logic

For each item card, inspect visible stat values:

```text
STR
CON
PER
INT
```

Find the highest stat value on that item.

Rules:

```text
If exactly one stat is highest, highlight that stat.
If multiple stats tie for highest, highlight all tied highest stats.
If all stats are zero or missing, do not highlight any stat.
If stat values are hidden/unavailable, do not invent values.
```

Example:

```text
STR 12  <- highlighted
CON 6
PER 0
INT 4
```

Tie example:

```text
STR 8   <- highlighted
CON 8   <- highlighted
PER 2
INT 0
```

## 3.4 Visual Style

The highlight should be subtle but clearly more recognizable than regular stats.

Do not use pure black if the app already uses a softer modern palette. Better approach:

```text
Regular stat text: muted/gray token, for example var(--text-muted)
Highest stat text: stronger text token with slight accent, for example var(--text-strong) or var(--accent-stat)
Optional: medium font weight, for example 600
Optional: very subtle tinted background pill
```

Recommended style:

```text
color: var(--stat-highlight-text)
font-weight: 600
background: optional very subtle accent tint, only if it fits the current design
border-radius: small radius if background pill is used
```

Design intent:

```text
The user should notice the highest stat when scanning.
The card should not become noisy.
The highlight should not look like a button.
The highlight should remain readable in light and dark themes.
```

## 3.5 Inventory MVP Scope

Implement:

```text
1. Preset cards stack vertically and take full available width.
2. Highest-stat highlighting works for normal equipment cards.
3. Highest-stat highlighting works for best-in-category cards.
4. Highest-stat highlighting works for battle gear item cards.
5. Highest-stat highlighting works for saved preset item cards.
6. Ties are handled correctly.
7. Zero/missing stats are not highlighted.
```

Do not implement:

```text
New equipment scoring formulas
New preset management system
Drag-and-drop preset layout
Custom user-defined stat highlight colors
```

---

# 4. Tasks Page Enhancements

Enhance the Tasks page with collapsible categories, scoring actions, completion visibility controls, blueness-based styling, habit multi-score controls, and task history visualization.

## 4.1 Task Categories

Task categories should be foldable:

```text
To-Dos
Dailies
Habits
```

The app should remember which categories are folded.

Storage:

```text
Local user preference is enough for MVP.
Persist by authenticated user ID.
Persist per device unless cloud settings already exist.
```

Example preference shape:

```json
{
  "tasksPage": {
    "foldedCategories": {
      "todos": false,
      "dailies": false,
      "habits": true
    }
  }
}
```

## 4.2 Default Completion Filtering

By default, each category should show only incomplete/open tasks.

Default behavior:

```text
To-Dos: show incomplete To-Dos
Dailies: show active/incomplete Dailies relevant to current state
Habits: show active Habits
```

Each category should have a control:

```text
Show completed
```

When enabled:

```text
Completed tasks are mixed into the same list with incomplete tasks.
Completed tasks keep their original/order position where possible.
Completed tasks look visually distinct.
```

The control should toggle back to:

```text
Hide completed
```

When disabled:

```text
Completed tasks are hidden again.
```

This visibility state should be remembered per category.

## 4.3 Completing and Scoring Tasks

Users should be able to score tasks directly from the Tasks page.

Required actions:

```text
To-Do: complete / uncomplete if supported by API and current app behavior
Daily: complete / uncomplete if supported by API and current app behavior
Habit: score + and/or score -, depending on habit configuration
```

Every task should display its numeric blue/red value.

Display:

```text
Value: 2.7
```

or compact:

```text
2.7
```

Use the current app’s number formatting convention. Do not over-format.

## 4.4 Completed Task Visual Style

When completed tasks are shown, they should be clearly distinct from incomplete tasks.

Recommended style:

```text
Lower opacity
Muted text
Subtle gray/neutral background
Completion checkmark
Optional completed timestamp if available
```

Do not make completed tasks unreadable. They should be de-emphasized, not hidden visually.

## 4.5 Blueness Logic

Implement continuous task-color logic similar to Habitica’s task color concept, but smoother and less discrete.

Goal:

```text
Tasks with negative value feel warmer/redder.
Neutral tasks feel soft yellow/neutral.
High-value tasks feel cooler/bluer.
```

Important:

```text
Use a continuous gradient.
Do not use only discrete red/orange/yellow/green/cyan/blue buckets.
Keep saturation low.
Use the color as a nuance, not a loud badge.
```

Apply blueness as task background color.

Completed tasks:

```text
Option A: use neutral gray completed background.
Option B: use the task color but heavily whitened/desaturated.
```

Recommended MVP:

```text
Incomplete tasks: subtle value-based background color.
Completed tasks: neutral muted completed background.
```

Suggested mapping:

```text
Very negative value -> soft red background
Negative value      -> soft orange background
Neutral value       -> soft warm neutral background
Positive value      -> soft green/cyan background
Very positive value -> soft blue background
```

Implementation guidance:

```text
Normalize task.value into a stable range, for example -20 to +20.
Clamp values outside the range.
Interpolate continuously across color stops.
Use alpha/tint mixing with the page background to keep saturation low.
Ensure text contrast stays accessible.
```

Pseudo-code:

```text
normalized = clamp((task.value + 20) / 40, 0, 1)
backgroundColor = interpolateRelaxedTaskGradient(normalized)
```

Do not make the exact gradient a gameplay mechanic. It is a visual aid.

## 4.6 Habit Multi-Score

For Habits, users should be able to score multiple instances at once.

Use a selection counter similar to spell target/cast-count controls.

Behavior:

```text
Default score count = 1.
User can increase/decrease score count.
Minimum = 1.
Maximum should be reasonable, for example 20.
User chooses + or - direction if the Habit supports both.
If Habit supports only +, show only + action.
If Habit supports only -, show only - action.
```

Example UI:

```text
Score Habit
[-] 3 [+]
[Score +3]
```

On submit:

```text
Send score requests sequentially or via existing batch logic if available.
Show progress/loading state.
If one request fails, stop and report how many succeeded.
Refresh task value and user stats after scoring.
```

Do not silently fire many requests without visible state.

## 4.7 Expandable Task Details

Every task should be expandable.

Expanded task view should show:

```text
Task notes/description, if available
Task value
Task streak/counter, if available
Recent score/completion history
Statistics for selected time period
Graph for selected time period
Small activity chart
```

Default time period:

```text
Week
```

Available periods:

```text
Week
Month
Year
```

## 4.8 Task Statistics

Statistics should be based on available task history.

For each task, compute what is possible from stored data:

```text
Completions/scores in selected period
Positive Habit scores
Negative Habit scores
Daily completion count
Daily missed count if available
To-Do completion date if available
Current streak if available
Average activity per day/week
```

If history data is incomplete:

```text
Show partial statistics.
Label them as based on available synced history.
Do not fake missing history.
```

## 4.9 Charts

Expanded task view should include a scrollable histogram inspired by Apple Health-style metric detail views.

Chart behavior:

```text
Week: daily bars for current/selected week
Month: daily bars for selected month
Year: weekly or monthly bars depending on data density
```

The chart should be horizontally scrollable if needed.

Use a small activity chart similar to GitHub activity heatmap:

```text
Main Tasks page: month activity chart
Expanded task view: smaller task-specific activity chart
```

Activity chart rules:

```text
Use subtle colors.
Do not make it too large.
Show tooltip/popover with date and count.
Use empty/low-activity neutral cells.
Use stronger but still relaxed color for higher activity.
```

## 4.10 Inspiration: Habit History Connector

The Habit History Connector is useful as product inspiration: it focuses on Habit/Daily activity reports and visualizing history. Use the same use case direction, not the same UI.

Required idea to carry forward:

```text
Task history should help users understand behavior over time, not only current task state.
```

## 4.11 Tasks MVP Scope

Implement:

```text
1. Foldable To-Dos, Dailies, Habits categories.
2. Remember folded state.
3. Default to incomplete/open tasks only.
4. Add Show completed / Hide completed per category.
5. Completed tasks appear visually distinct when shown.
6. Show numeric task value on every task.
7. Add subtle continuous blueness background for incomplete tasks.
8. Allow task scoring/completion from the page.
9. Add Habit multi-score control.
10. Add expandable task details.
11. Add week/month/year period selector for task statistics.
12. Add basic histogram for task history.
13. Add month activity chart on Tasks page.
14. Add smaller activity chart in expanded task view.
```

Do not implement in MVP:

```text
Advanced predictive analytics
AI task advice
Full custom chart builder
Export to CSV/PDF
Cloud-synced task UI preferences unless already available
```

---

# 5. Main Page / Dashboard Improvements

Improve the main dashboard so it becomes a useful overview and navigation hub, not only a static user data page.

## 5.1 Pending Damage Overview

The dashboard should show approximate pending damage the user might receive.

This should include:

```text
Damage from user’s own missed Dailies / negative Habits, if available
Damage from active party quest / boss, if available
Damage caused by party members, if available from synced party state
Any other damage source available in current app data
```

Important:

```text
This value is approximate.
The UI must explain what is included.
Do not claim exactness if data is incomplete.
```

Example UI:

```text
Estimated incoming damage: 18 HP

Includes:
- Your pending Daily damage: 6 HP
- Party boss damage share: 8 HP
- Party member pending damage: 4 HP

This is an estimate based on synced data.
```

If some data is unavailable:

```text
Estimated incoming damage: 14 HP

Includes:
- Your pending Daily damage: 6 HP
- Party boss damage share: 8 HP

Party member pending damage is unavailable or not synced yet.
```

## 5.2 Death Warning

If estimated damage is close to killing the user, show a warning.

Suggested thresholds:

```text
Danger: estimatedDamage >= currentHp
Warning: estimatedDamage >= currentHp * 0.75
Info: estimatedDamage > 0
```

Warning copy:

```text
You may be knocked out after Cron.

Estimated incoming damage is close to or higher than your current HP.
Consider completing Dailies, healing, or buying a health potion.
```

## 5.3 Health Potion Button

Near the pending damage box, add a button:

```text
Buy Health Potion
```

Behavior:

```text
Button should be visible when user can buy potion or when the app can attempt purchase.
Disable with explanation if user does not have enough gold or API action is unavailable.
After purchase, refresh user HP, gold, and dashboard damage warning state.
```

Do not auto-buy potion. It must be an explicit user action.

## 5.4 Dashboard Section Cards

Dashboard should have very short info about every major section of the app.

Each section card should include:

```text
Section name
One-line description
Important status summary, if available
Button/link to open section
```

Suggested cards:

```text
Party
Current quest, party queue, pending party state.

Tasks
Today’s Dailies, To-Dos, Habits, task history.

Inventory
Equipment, best gear, battle presets.

Spells / Skills
Available skills, buffs, cast planning.

Stats / Character
HP, MP, XP, gold, attributes.

Settings / Sync
Account, refresh state, data sync diagnostics.
```

The dashboard should allow users to navigate directly to every section, not only through the side menu.

## 5.5 Open Habitica Button

Add a button to open Habitica:

```text
Open Habitica
```

Base URL:

```text
https://habitica.com
```

Preferred behavior:

```text
Add this button to the dashboard.
Also consider adding it to the app header as a context-sensitive external link.
```

## 5.6 Context-Sensitive Habitica Links

If the app header includes an `Open Habitica` button, use page-specific links where known.

Suggested mapping:

```text
Dashboard -> https://habitica.com
Inventory -> https://habitica.com/inventory/equipment
Party -> https://habitica.com/party
Tasks -> https://habitica.com
Spells / Skills -> https://habitica.com
Settings -> https://habitica.com/user/settings/site
```

If a URL is uncertain, use `https://habitica.com` instead of inventing a wrong deep link.

## 5.7 Dashboard MVP Scope

Implement:

```text
1. Pending damage estimate box.
2. Clear explanation of included/excluded damage sources.
3. Warning state if damage may kill or nearly kill the user.
4. Buy Health Potion button near damage info.
5. Dashboard section cards with direct navigation.
6. Open Habitica button.
7. Context-sensitive Habitica header link where URL is known.
```

Do not implement in MVP:

```text
Automatic potion purchase
Complex survival optimizer
Push notifications
Exact damage guarantee when party data is incomplete
```

---

# 6. Login and Refresh Improvements

Make login and refresh feel fast, granular, responsive, and modern.

Current repo adaptation:

```text
Affected existing UI/state:
- `src/Habitica.WebApp/State/AppSessionController.cs`
- `src/Habitica.WebApp/State/SessionViewModel.cs`
- `src/Habitica.WebApp/Sync`
- `src/Habitica.WebApp/Pages/DashboardPage.razor`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- `src/Habitica.WebApp/Pages/TasksPage.razor`
- `src/Habitica.WebApp/Pages/InventoryPage.razor`
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/Layout` and navigation components if refresh state affects shell/header/side menu

Affected application/API/storage layers:
- `src/Habitica.Application/Sync`
- existing application auth/session services
- `src/Habitica.Api`
- `src/Habitica.Storage`
- Cloudflare sync only where app-data or party-sync state is involved

Do not solve refresh performance by adding a frontend framework cache library.
Implement a Blazor/C# application-layer refresh coordinator with domain-specific refresh keys, in-flight request deduplication, freshness metadata, and progressive UI state.
```


## 6.1 Login Page Behavior

The login page should not be shown if the user has already logged in.

Behavior:

```text
If valid stored credentials/session exists:
    route directly to dashboard
else:
    show login page
```

Do not briefly flash the login page before redirecting. Show an app-level boot/loading state if auth state is still being resolved.

## 6.2 Fast Login Strategy

Login should feel almost immediate.

Definition:

```text
As soon as the server confirms authentication or the smallest required user data is successfully fetched, route to dashboard.
Do not wait for all inventory, party, quests, tasks, history, and derived calculations before entering the app.
```

Dashboard should render partial data with skeleton placeholders for missing fields.

## 6.3 Granular Data Refresh Model

Split data refresh into stages.

Recommended data groups:

```text
Group A: Auth/session validation
- Stored credentials/session
- Minimal user identity

Group B: Dashboard-critical user summary
- HP, MP, XP, level
- Gold
- Class
- Stats
- needsCron / last Cron-related state if available
- Current buffs
- Basic sync timestamp

Group C: Tasks summary
- Dailies
- To-Dos
- Habits
- Task values
- Completion state

Group D: Party summary
- Party members
- Active quest
- Pending quest/quest invite state
- Pending damage / quest progress if available

Group E: Inventory/equipment summary
- Current equipment
- Owned equipment
- Quest scroll inventory
- Rewards metadata

Group F: Skills/spells
- Available skills
- Mana costs
- Cast availability
- Buff skill metadata

Group G: History and analytics
- Task history
- Recently completed quests
- Activity charts
- Derived long-range statistics

Group H: Derived app calculations
- Best equipment by stat/category
- Battle gear recommendations
- Pending damage estimate
- Quest queue scores
- Dashboard warnings
```

Current-state refactor target:

```text
The existing manual sync appears to behave too much like a broad account refresh.
Refactor it toward an internal refresh coordinator without breaking existing pages.

Do first:
1. Identify every existing refresh/sync entry point in `AppSessionController`, `Habitica.Application/Sync`, and page refresh buttons.
2. Map each existing API call to a data domain.
3. Add domain-level refresh result models before changing UI heavily.
4. Keep existing manual sync button working while introducing separated domain refresh.
5. Add tests for freshness/invalidation rules before adding many UI effects.

Do not do first:
- Do not rewrite all pages at once.
- Do not replace `AppSessionController` with a new global state library in one large change.
- Do not introduce a new backend for normal Habitica refresh.
- Do not call Cloudflare sync as a proxy for Habitica API refresh.
```

## 6.3A Habitica API Refresh Separation Requirement

The current refresh is slow because too much data is refreshed at the same time. Refactor refresh logic so data is fetched in independent, page-relevant groups whenever Habitica API endpoints allow it.

Core rule:

```text
Do not refresh the whole Habitica account payload when the current screen needs only one slice of data.
Use the smallest available Habitica API endpoint that can satisfy the visible UI.
Use full-account/user refresh only when a smaller endpoint is not available or when multiple dependent fields truly require it.
```

Before implementing this refactor, agents must inspect the existing Habitica API client/wrapper in the project and map available endpoint methods. Do not invent endpoint names in production code. If the wrapper does not expose a needed smaller endpoint, add a thin wrapper around the official Habitica API v3 endpoint only after verifying it exists.

Suggested endpoint separation, subject to actual API/wrapper availability:

```text
Minimal user/dashboard data
- Fetch user profile/stats/preferences/current buffs/needsCron-related state if available.
- Use this for dashboard boot, header state, HP/MP/gold, class, stats, and Cron button visibility.

Tasks data
- Fetch tasks separately from inventory and party state.
- Use task-specific endpoints for Dailies, To-Dos, Habits, or all tasks depending on available API support.
- After task score/complete actions, invalidate only tasks + dashboard summary + derived damage/stat calculations.

Party data
- Fetch party/group data separately from user inventory and tasks.
- Use this for party members, active quest, quest progress, party invitation state, and party-level sync.
- After party quest actions, invalidate only party + quest queue + dashboard pending damage.

Inventory/equipment data
- Fetch inventory/equipment separately from tasks and party.
- Use this for equipment cards, battle gear, saved presets, owned quest scrolls, rewards metadata.
- After equipment changes, invalidate inventory + dashboard stats + derived best-gear calculations.

Skills/spells metadata and cast state
- Fetch skill metadata/static content separately where possible.
- Treat static metadata as long-lived cache.
- After casting a skill, invalidate user stats/buffs + party state if the skill affects party members.

Static content / metadata
- Quest names, reward names, equipment metadata, and skill descriptions should be cached long-term.
- Refresh static content rarely, on app version change, cache miss, or manual hard refresh.
- Do not block login on static metadata if stale usable metadata exists.

History / analytics
- Fetch or compute history separately and late.
- Never block dashboard, tasks list, party page, or inventory page on long-range history/chart calculations.
```

If Habitica API does not provide a separated endpoint for a specific slice:

```text
1. Use the smallest available parent endpoint.
2. Store the returned snapshot by data domain.
3. Update only affected UI slices.
4. Do not force unrelated visible components into loading state.
5. Add a TODO comment that identifies the missing endpoint/wrapper limitation.
```

This is a performance and UX requirement, not a backend rewrite requirement. Prefer incremental endpoint separation over a large sync redesign.

## 6.3B Best-in-Class Refresh Pattern

Use a stale-while-revalidate style data model:

```text
If cached data is fresh:
    show cached data immediately and do not fetch unless the user explicitly refreshes.

If cached data is stale but still usable:
    show cached data immediately.
    start background refresh.
    update changed fields in place when the response arrives.

If cached data is missing or too old to trust:
    show skeletons for missing fields.
    fetch required data.
    render each section as soon as its data arrives.
```

Separate these states in code:

```text
Data state:
- empty / pending / success / error

Fetch state:
- idle / fetching / paused

Freshness state:
- fresh / stale / expired

Sync source state:
- local-cache / cloudflare-cache / habitica-api / derived
```

Important distinction:

```text
A component can have valid data and still be fetching in the background.
Do not replace valid data with a full loading screen just because a background fetch is running.
```

Use query keys or equivalent cache keys by data domain:

```text
['habitica', userId, 'user-summary']
['habitica', userId, 'tasks']
['habitica', userId, 'party']
['habitica', userId, 'inventory']
['habitica', userId, 'skills']
['habitica', userId, 'content-metadata']
['app', userId, 'task-history']
['app', partyId, 'quest-queue']
['app', partyId, 'recently-completed-quests']
['derived', userId, 'dashboard-damage-estimate']
['derived', userId, 'best-equipment']
```

Do not use one global `userData` cache key for everything. One global key causes unnecessary reloads and makes the app feel slow.

## 6.3C Dependency-Based Invalidation

After user actions, invalidate only the data domains that are actually affected.

Examples:

```text
Task scored/completed:
- invalidate tasks
- invalidate user summary if HP/MP/XP/gold/stats can change
- invalidate dashboard damage estimate
- invalidate task history for that task if history is tracked locally
- do not invalidate inventory unless the API response proves inventory changed

Habit multi-score:
- invalidate tasks once after the batch, not after every individual score if avoidable
- invalidate user summary after the batch
- show progress inside the habit action control

Buff cast:
- invalidate user summary/stats/buffs
- invalidate party summary if the buff affects party members
- invalidate dashboard warnings/damage if relevant
- do not invalidate full inventory

Equipment changed:
- invalidate inventory/equipment
- invalidate user summary/stats
- invalidate best-equipment derived calculations
- do not invalidate tasks or party unless a visible dependency exists

Cron completed:
- invalidate user summary
- invalidate tasks
- invalidate party summary / active quest progress
- invalidate buffs and dashboard damage estimate
- invalidate task history if new day state affects charts
- do not force static content metadata refresh

Quest queue vote changed:
- invalidate app party quest queue only
- do not call Habitica API unless active Habitica party state is also affected

Quest started/completed:
- invalidate Habitica party state
- invalidate app quest queue
- invalidate recently completed quests
- invalidate dashboard party/pending damage box
```

Mutation rule:

```text
On successful mutation, update local UI optimistically only when the result is deterministic.
Then invalidate/refetch the affected domains in the background.
If mutation fails, rollback optimistic UI and show a local error.
```

## 6.3D Request Scheduling and Concurrency

Avoid starting all refresh requests at once.

Use a small request scheduler:

```text
High priority:
- current page visible data
- user summary needed by header/dashboard/Cron button
- mutation follow-up refreshes

Medium priority:
- nearby navigation targets
- party/tasks/inventory summaries after login
- derived calculations needed by visible cards

Low priority:
- history
- long-range charts
- static metadata refresh
- non-visible page prefetch
```

Recommended behavior:

```text
Run high-priority requests first.
Limit concurrent Habitica API requests to a small number, for example 2-4.
Deduplicate identical in-flight requests.
Cancel or deprioritize non-visible page refreshes when the user navigates.
Do not cancel mutation follow-up requests that are needed for consistency.
Apply exponential backoff for retryable network errors.
Do not retry non-idempotent mutations automatically unless existing code already has safe retry protection.
```

Request deduplication rule:

```text
If a request for ['habitica', userId, 'tasks'] is already running, reuse that in-flight request instead of starting another one.
```

Rate-limit safety:

```text
Prefer fewer, targeted requests over one huge refresh storm.
Do not poll aggressively.
Use focus/reconnect refresh only when data is stale.
Add jitter to automatic background refreshes if multiple domains refresh at the same time.
```

## 6.3E Progressive Rendering Requirements

Every page should render as soon as its minimum required data is available.

Dashboard minimum viable render:

```text
User display name or fallback
HP/MP/gold/level if available
Skeletons for missing widgets
Navigation cards
Refresh/sync status
```

Party page minimum viable render:

```text
Party header or skeleton
Active quest card or empty state
Quest queue from app DB if available
Skeleton for Habitica party state if still loading
```

Tasks page minimum viable render:

```text
Task category headers
Cached task rows if available
Skeleton rows only for missing task categories
Folded state from local preferences immediately
```

Inventory page minimum viable render:

```text
Cached equipment/presets if available
Skeletons for missing equipment sections
Static metadata from cache if available
```

Rules:

```text
Do not wait for history before rendering tasks.
Do not wait for inventory before rendering dashboard summary.
Do not wait for static metadata refresh if cached metadata exists.
Do not wait for party state before rendering local quest queue if the app DB has queue data.
```

## 6.3F Refresh Status UI

Show refresh state at the smallest useful scope.

Required UI levels:

```text
App-level boot state:
- only before authentication/minimal user state is known.

Page-level refresh state:
- current page data is being manually refreshed.
- keep navigation and side menu visible.

Card-level refresh state:
- one dashboard/party/inventory card is refreshing.

Field-level refresh state:
- one number/value is stale or being updated.

Background sync indicator:
- small non-blocking indicator that sync is running.
```

Do not show a full-page loader when only one data domain is refreshing.

Refresh labels:

```text
Fresh now
Updated 2 min ago
Refreshing...
Partially updated
Some data may be stale
Offline / waiting for network
```

Use these labels sparingly. Prefer compact status text/tooltips over noisy banners.

## 6.3G Derived Calculation Scheduling

Derived calculations must be separated from network refresh.

Examples of derived calculations:

```text
Pending damage estimate
Best equipment by stat/category
Battle gear recommendations
Quest queue priority scores
Task activity chart aggregation
Recently completed quest penalty
Dashboard warnings
```

Rules:

```text
Run derived calculations after their dependencies update.
Do not block network response handling on heavy chart aggregation.
Use memoization/cache by input snapshot version.
If dependencies are incomplete, compute partial result and mark it as partial.
If calculation is expensive, schedule it after visible raw data renders.
```

Example:

```text
Tasks API response arrives:
1. Render updated task list.
2. Update task values/blueness.
3. Schedule task statistics/chart aggregation.
4. Update charts when aggregation completes.
```

## 6.3H Hard Refresh vs Normal Refresh

Support two refresh modes internally.

Normal refresh:

```text
Refresh current page first.
Use cached static metadata when possible.
Respect stale/fresh windows.
Do not clear all caches.
Do not reload unrelated data domains.
```

Hard refresh:

```text
Explicit developer/debug action or rare user action.
Bypass freshness windows.
Refresh static metadata.
Refresh all major domains in priority order.
Keep progressive rendering.
Do not hide side menu if user data exists.
```

Do not make the normal Refresh button behave like a hard refresh. The normal button should be fast and relevant to the current page.

## 6.3I Blazor/Application-Layer Implementation Guidance

Implement refresh separation inside the current C# architecture.

Recommended model names are illustrative; agents should adapt to current naming:

```text
RefreshDomain:
- UserSummary
- Tasks
- Party
- Inventory
- Skills
- StaticContent
- TaskHistory
- AppPartyQueue
- CloudflareAppSync
- DerivedDashboard
- DerivedInventory
- DerivedTasks

RefreshPriority:
- Visible
- UserActionFollowUp
- NavigationPrefetch
- BackgroundStale
- HardRefresh

RefreshReason:
- AppBoot
- ManualRefresh
- PageEntered
- MutationCompleted
- SnapshotStale
- CloudSyncCompleted
```

Application-layer responsibilities:

```text
Habitica.Application:
- decides which domains to refresh;
- owns freshness checks;
- owns dependency invalidation;
- owns request deduplication;
- calls `Habitica.Api` and `Habitica.Storage`;
- returns UI-facing refresh/read models.

Habitica.WebApp:
- requests refresh for current page/domain;
- displays current data and refresh states;
- does not know endpoint sequencing details;
- does not call Habitica API directly.
```

Suggested UI-facing result shape:

```text
DomainData<T>:
- T? Value
- SnapshotFreshnessState Freshness
- bool HasValue
- bool IsFetching
- bool IsManualRefresh
- bool IsBackgroundRefresh
- string? LastError
- DateTimeOffset? UpdatedAtUtc
- string SourceLabel
```

In-flight request deduplication:

```text
Use a dictionary keyed by `(userId, refreshDomain, parameterHash)`.
If a matching request is already running, return/await the same task.
Remove completed/failed requests from the dictionary.
Do not deduplicate mutating operations; only deduplicate reads.
```

Progressive page updates:

```text
SessionViewModel should expose domain-level state, not only one global sync state.
Razor pages should subscribe to existing session/state change events and re-render when each domain completes.
A page should not switch to full skeleton state when only a non-visible or background domain is refreshing.
```

Cloudflare sync relationship:

```text
Cloudflare app-data sync and party-sync are not replacements for Habitica API refresh.
Cloudflare sync can update app-specific/shared data domains.
Habitica API refresh updates Habitica-owned domains.
When Cloudflare sync finishes, invalidate only affected app/shared domains.
When Habitica API refresh finishes, optionally publish party snapshot/CRON events through party-sync if existing feature flow already does this.
```

## 6.3J API Endpoint Availability Audit

Before changing refresh code, add or create an implementation note mapping existing wrapper methods to domains.

Minimum audit table:

```text
Domain | Existing wrapper method | Habitica endpoint | Can be separated? | Notes
UserSummary | ... | GET /user or GET /user?userFields=... | yes/partial | full user needed for computed stats
Tasks | ... | /tasks/... | yes/partial | preserve tasksOrder where needed
Party | ... | GET /groups/party, members endpoint | yes | member public fields may be expensive
Inventory | ... | GET /user or inventory endpoints | partial | user inventory may come from user document
Skills | ... | static/client metadata or user class/stats | partial | cache static skill metadata
StaticContent | ... | content endpoint/static source | yes | long-lived cache
```

Rules:

```text
If a narrow endpoint exists and returns enough data, use it for page-specific refresh.
If a narrow endpoint does not return computed helpers or required fields, use the smallest parent endpoint.
If only `GET /user` can satisfy the current UI, still store/update only affected domain read models.
Do not present endpoint separation as complete until the audit proves it.
```

## 6.4 Initial Login Refresh Order

After login:

```text
1. Group A: Auth/session validation
2. Group B: Dashboard-critical user summary
3. Route to dashboard as soon as Group B has minimum viable data
4. Group D: Party summary
5. Group C: Tasks summary
6. Group E: Inventory/equipment summary
7. Group F: Skills/spells
8. Group G: History and analytics
9. Group H: Derived calculations as soon as dependencies become available
```

Reason:

```text
The user sees useful dashboard state quickly.
Party/current quest state is usually high-value.
Tasks and inventory follow.
History and heavy calculations come last.
```

## 6.5 Refresh Button Relevance Logic

When the user presses Refresh, prioritize the current page.

Examples:

```text
Current page = Dashboard:
    refresh Group B first, then D, C, E, F, G, H

Current page = Party:
    refresh Group D first, then quest pool/inventory quest scrolls from E, then B, C, F, G, H

Current page = Tasks:
    refresh Group C first, then B, G, H, D, E, F

Current page = Inventory:
    refresh Group E first, then B, F, H, D, C, G

Current page = Spells:
    refresh Group F first, then B, D, H, C, E, G
```

Implementation rule:

```text
Refresh visible page data first.
Then refresh supporting data.
Then refresh low-priority/background data.
```

## 6.6 Forced Refresh Loading State

After the user presses Refresh, this is a forced user action. Display visible refresh state for currently displayed values.

Required UI behavior:

```text
Values on the current page should turn into subtle gray/skeleton placeholders or show a shimmer/flicker refresh animation.
Do not blank the whole page.
Do not hide navigation.
Do not hide side menu.
Preserve page layout.
Refresh values in place as they arrive.
```

If old data exists:

```text
Option A: show old values dimmed with refreshing indicator.
Option B: replace specific fields with skeletons.
```

Recommended approach:

```text
For critical numeric fields: show old value dimmed + small refreshing indicator.
For unloaded/missing fields: show skeleton block.
For cards/lists being fully refetched: use skeleton rows matching the final layout.
```

## 6.7 Background Stale Snapshot Refresh

If the app thinks a snapshot became stale, it should refresh automatically in the background.

Behavior:

```text
Do not interrupt user experience.
Do not replace visible values with loading UI.
Do not hide side menu.
Do not navigate away.
When updated data arrives, update values in place.
If a value changed, show a brief subtle change animation.
```

Suggested stale rules:

```text
Dashboard summary stale after 1-5 minutes
Tasks stale after 1-5 minutes when page is active
Party stale after 1-5 minutes when page is active
Inventory stale after 10-30 minutes
History stale after 30-60 minutes
```

Do not hardcode these if the app already has a freshness system. Use existing configuration if present.

## 6.8 Loading Skeletons

Use skeletons when:

```text
Data is not available yet.
The final layout is known.
Loading is expected to take noticeable time.
A page or card is being loaded for the first time.
```

Do not use skeletons when:

```text
The operation is instant.
A small button action is in progress; use button loading state instead.
The page structure is unknown.
```

Skeleton guidelines:

```text
Skeleton blocks should match the size and shape of final content.
Use subtle animation only.
Avoid aggressive shimmer.
Respect reduced-motion accessibility settings if available.
```

## 6.9 Sync State With Cloudflare Storage

The app uses Cloudflare-backed data storage/sync. Display field-level loading state if sync is currently in progress and the specific field/page depends on that sync.

Rules:

```text
If sync is in progress and a field has no usable value:
    show skeleton/loading placeholder.

If sync is in progress and a field has old usable value:
    show old value with subtle stale/refreshing indicator.

If no sync is currently in progress:
    do not show unnecessary loading state.
```

Do not globally block the UI because cloud sync is running.

## 6.10 Side Menu Rule

The side menu should not be hidden during page refresh.

Allowed side menu hidden state:

```text
No authenticated user data exists at all.
App is on login/auth page.
Screen size/mobile layout explicitly uses hidden menu.
```

Not allowed:

```text
Hide side menu because current page is refreshing.
Hide side menu because background sync is running.
Hide side menu because one data group is loading.
```

## 6.11 Animations

All changes should have subtle animations.

Use animations for:

```text
Value updated after background refresh
Card content loaded
Skeleton to content transition
Warning state appearing
Section card hover/tap feedback
Task expand/collapse
Category fold/unfold
```

Animation rules:

```text
Keep durations short, usually 120-250ms.
Use easing consistent with the app.
Avoid motion that shifts large layout unexpectedly.
Respect reduced-motion settings if available.
```

## 6.12 Login and Refresh MVP Scope

Implement:

```text
1. Skip login page when user is already authenticated.
2. Route to dashboard after minimal successful user data fetch.
3. Split refresh into independent data groups.
4. Use separated Habitica API endpoints when available instead of refreshing the whole user/account payload.
5. Use stale-while-revalidate behavior: show usable cached data immediately, refresh stale data in the background.
6. Use domain-specific cache/query keys instead of one global userData refresh key.
7. Invalidate only affected data domains after mutations.
8. Limit and prioritize concurrent requests so current-page data loads first.
9. Initial login refresh uses staged order.
10. Refresh button prioritizes current page data.
11. Current page shows visible field/card-level refresh state after manual refresh.
12. Background stale refresh does not interrupt UI.
13. Changed values animate subtly after background update.
14. Side menu remains visible during refresh when user data exists.
15. Cloudflare sync progress creates field-level loading state only where relevant.
```

Do not implement in MVP:

```text
Full offline mode
Conflict resolution UI for every data type
Advanced sync debugging dashboard
Push-based real-time sync
Custom user refresh priority settings
```

---

# Implementation Order Summary

Implement in this order:

```text
1. Party page quest improvements
2. CRON button and buff warning
3. Inventory final improvement
4. Tasks page enhancements
5. Main page / dashboard improvements
6. Login and refresh improvements
```

Suggested sub-order for safer delivery:

```text
Phase 1: Shared data models and non-invasive UI updates
- Party active card visual update
- Inventory preset layout
- Highest-stat highlight utility
- Dashboard section cards
- Login redirect guard

Phase 2: User actions with API calls
- Cron button
- Buff warning flow
- Task scoring/completion
- Health potion button

Phase 3: Shared party DB state
- Quest pool
- Quest queue
- Votes
- Recently completed quests
- Queue scoring

Phase 4: Analytics and history UI
- Task expanded statistics
- Histograms
- Activity charts
- Recently completed display

Phase 5: Refresh architecture
- Habitica API endpoint separation where available
- Domain-specific cache/query keys
- Dependency-based invalidation
- Request scheduling and concurrency limits
- Granular refresh groups
- Current-page refresh priority
- Background stale refresh / stale-while-revalidate
- Progressive rendering
- Skeletons and change animations
```

# Current-Repo Implementation Notes

Use these notes when turning this plan into code.

```text
Read first:
1. RULES.md
2. HABITICA_API.md
3. HABITICA_TOOL_REFERENCES.md for sync/fetch/party/task parsing work
4. TECHNICAL.md
5. FEATURES.md
6. Affected source files only
```

Expected code placement:

```text
UI:
- Razor page/card markup and CSS only.
- No Habitica API calls.
- No formulas beyond display formatting.

Application:
- refresh orchestration
- mutation plans
- invalidation
- execution logs
- feature use cases

Api:
- typed Habitica endpoint wrappers
- response DTO parsing
- rate-limit and Retry-After handling

Rules:
- scoring, formulas, quest queue priority, damage estimates, task color normalization

Domain:
- stable models and value objects

Storage:
- IndexedDB/Dexie schema, local snapshots, read models, local preferences

Cloudflare functions/migrations:
- shared party state only
- app-data sync only
- no Habitica credentials
```

Specific current-state constraints:

```text
- Party page already has CRON and active quest progress UI. Extend it; do not duplicate it.
- Party-sync already has `party_quest_queue` and `party_quest_votes`. Extend via migration; do not create a second queue store.
- Existing task UI is read-only. Add task mutation workflows conservatively with validation, visible progress, and follow-up refresh.
- Existing inventory has read-only explorer and presets. Keep preset data local/app-specific unless explicit shared sync is designed.
- Existing diagnostics/live test area should remain guarded. Do not route normal user actions through diagnostics.
- Existing side menu/app shell should remain stable during refresh if authenticated user data exists.
```

# Non-Goals

Do not implement these unless explicitly requested:

```text
Automatic Cron scheduling
Automatic buff casting
Automatic quest starting
Automatic potion purchase
AI-generated task advice
Full export/reporting system
Full offline-first architecture
Major design system rewrite
Habitica mechanics rebalancing
```

# Acceptance Checklist

The implementation is acceptable when:

```text
Party page:
- Active quest card uses new card style and compact rewards.
- Quest pool shows real quest names and reward names.
- Quest owners can queue their own quests.
- Party members can vote once per queued quest.
- Vote count and voter list are visible.
- Recently completed quests are stored and used as a soft penalty.

CRON:
- Start New Day button appears only when needed.
- Buff warning appears for not-Croned user casting stat buffs.
- Cast anyway / Start New Day and Cast / Cancel all work correctly.

Inventory:
- Presets stack vertically at full width.
- Highest stat is subtly highlighted everywhere required.

Tasks:
- Categories fold/unfold and remember state.
- Incomplete tasks are default.
- Completed tasks can be shown/hidden.
- Task values are visible.
- Blueness background is continuous and subtle.
- Habits support multi-score.
- Expanded task details include basic stats and charts.

Dashboard:
- Pending damage estimate is visible and explained.
- Near-death warning appears when appropriate.
- Buy Health Potion button is available.
- Dashboard has navigation cards.
- Open Habitica button exists.

Login/refresh:
- Logged-in user goes straight to dashboard.
- Data refresh is staged.
- Refresh logic uses separated Habitica API endpoints where available.
- Current page data has priority over non-visible data.
- Cached stale data can remain visible while background refresh runs.
- Mutations invalidate only affected data domains.
- Manual refresh shows visible field/card refresh state.
- Background stale refresh does not interrupt the user.
- Side menu does not disappear during refresh.
```
