# Habitica Companion — Cloud Sync Split-Key Fix and Refresh Optimization Instructions

Status note, 2026-05-21: this is a historical implementation instruction. Split-key Cloudflare sync, legacy single-blob restore fallback, and the refresh coordinator are implemented. Use `FUTURE.md` for the remaining cloud-sync/refresh follow-up backlog and `FEATURES.md` / `TECHNICAL.md` for the current behavior.

This document is optimized for AI coding agents working in the `strexet/habitica-companion` repository.

## Goal

Fix this production error:

```text
cloud-sync
Auth
Encrypted cloud sync was skipped: Cloud sync upload failed. Cloud sync payload is too large.
uploadData@https://habitica-companion.pages.dev/js/sync/cloudflareSync.js:18:20
```

The original cloud sync flow likely uploaded one large encrypted payload. Replace it with separated encrypted sync records keyed by data domain, then improve refresh logic so the app does not fetch/update everything at once.

---

## Project Constraints

Follow the current project architecture.

```text
UI:
- src/Habitica.WebApp
- Blazor WebAssembly
- MudBlazor
- Razor pages/components must call application services only

Application:
- src/Habitica.Application
- Owns sync orchestration, refresh orchestration, invalidation, use cases

API:
- src/Habitica.Api
- Owns Habitica API calls and DTO parsing

Storage:
- src/Habitica.Storage
- Owns IndexedDB/Dexie local snapshots and local read models

Cloudflare:
- functions/
- Existing app cloud sync and party-sync endpoints
- Do not send Habitica API tokens or credentials to Cloudflare
```

Do not introduce React, TanStack Query, SWR, Redux, or another frontend caching framework. Implement this with the existing C#/Blazor/Application-layer architecture.

Before changing code, inspect the current cloud sync path:

```text
src/Habitica.WebApp/wwwroot/js/sync/cloudflareSync.js
src/Habitica.Application
src/Habitica.Storage
functions/api
```

Also search for current error/limit logic:

```bash
grep -R "payload is too large\|too large\|uploadData\|cloudflareSync\|Encrypted cloud sync" -n .
```

---

# 1. Replace Single Large Cloud Sync Blob With Split Keys

## Required Behavior

Cloud sync must upload multiple smaller encrypted records instead of one huge encrypted blob.

Use stable per-section keys. Suggested key format:

```text
user:{userId}:profile
user:{userId}:preferences
user:{userId}:saved-presets
user:{userId}:dashboard-summary
user:{userId}:tasks-current
user:{userId}:inventory-current
user:{userId}:party-current
user:{userId}:skills-current
user:{userId}:task-history:{yyyy-MM}
user:{userId}:quest-history:{partyId}:{yyyy-MM}
user:{userId}:sync-metadata
```

Adapt prefixes to existing project conventions if needed, but keep the per-section design.

## Recommended Sync Sections

```text
profile
preferences
saved-presets
dashboard-summary
tasks-current
inventory-current
party-current
skills-current
task-history:{yyyy-MM}
quest-history:{partyId}:{yyyy-MM}
sync-metadata
```

## Sync Only Useful Compact State

Sync:

```text
User preferences
Saved equipment presets
Small profile/account summary
Small dashboard summary
Latest compact task snapshot
Latest compact inventory snapshot
Latest compact party snapshot
Latest compact skills snapshot
Task history chunks by month
Quest history chunks by party/month
Sync metadata
```

Do not sync:

```text
Habitica API tokens
Habitica credentials
Derived data that can be recalculated
Static Habitica content metadata
Huge raw API responses when compact read models are enough
Repeated duplicate snapshots
Chart-ready aggregates if they can be recalculated locally
```

Do not sync these derived values:

```text
Best gear calculations
Quest queue scores
Pending damage estimate
Task blueness colors
Dashboard warning state
Sorted/card-ready UI models
```

---

# 2. Add Sync Metadata

Add a small metadata record for section state.

Example shape:

```json
{
  "schemaVersion": 2,
  "updatedAtUtc": "2026-05-19T06:16:33Z",
  "sections": {
    "preferences": {
      "key": "user:{userId}:preferences",
      "updatedAtUtc": "2026-05-19T06:16:33Z",
      "payloadBytes": 1234,
      "status": "ok"
    },
    "task-history:2026-05": {
      "key": "user:{userId}:task-history:2026-05",
      "updatedAtUtc": "2026-05-19T06:16:33Z",
      "payloadBytes": 1200000,
      "status": "skipped-too-large"
    }
  }
}
```

Rules:

```text
schemaVersion = 2 for split-key sync
metadata must be small
metadata must never contain decrypted content
metadata must not contain Habitica credentials or tokens
metadata should reflect actual uploaded section results
```

Prefer uploading metadata after section uploads, so it describes what actually succeeded.

---

# 3. Upload Flow

Upload sections in priority order:

```text
1. preferences
2. saved-presets
3. profile
4. dashboard-summary
5. tasks-current
6. inventory-current
7. party-current
8. skills-current
9. current-month task history
10. older task history chunks
11. quest history chunks
12. sync-metadata
```

If a section is too large:

```text
Skip only that section.
Continue uploading other sections.
Record section name, key, plain JSON size if known, encrypted payload size, and status.
Log a warning.
Do not fail the entire cloud sync if critical sections succeeded.
```

Section priority:

```text
Critical:
- preferences
- saved-presets
- profile
- sync-metadata

Important:
- dashboard-summary
- tasks-current
- inventory-current
- party-current
- skills-current

Optional:
- task-history chunks
- quest-history chunks
```

If an optional section is too large, user-facing sync state should be:

```text
Cloud sync partial
```

not:

```text
Cloud sync failed
```

Suggested user-facing message:

```text
Cloud sync partially completed. Large history data was kept on this device only.
```

---

# 4. Download / Restore Flow

Restore in this order:

```text
1. Download sync-metadata.
2. Download critical sections: preferences, saved-presets, profile.
3. Download current snapshots: dashboard, tasks, inventory, party, skills.
4. Download history chunks lazily or in background.
5. Merge sections into IndexedDB/local read models.
```

If metadata is missing:

```text
Try old single-blob legacy key once.
If legacy data exists, restore it using existing logic.
Split it into new local sections.
Upload split-key format on next sync.
Do not require the user to log in again.
Do not delete legacy cloud data until split-key upload succeeds.
```

Support both formats:

```text
schemaVersion 1: old single encrypted blob
schemaVersion 2: split encrypted section records
```

---

# 5. Payload Size Guard

Add pre-upload size checks per section.

Track and log:

```text
sectionName
sectionKey
plainJsonBytes, if available
encryptedPayloadBytes
automatic/manual sync flag
upload status
```

Behavior:

```text
If encryptedPayloadBytes > configured max:
    skip section;
    log structured warning;
    continue sync.

If critical section is too large:
    mark cloud sync failed or degraded;
    include structured diagnostic metadata.

If optional section is too large:
    mark cloud sync partial;
    continue.
```

Do not hardcode limits in Razor/UI code. Put sync limits in the application/sync layer or shared sync configuration.

---

# 6. Cloudflare Endpoint Requirements

Update existing Cloudflare sync endpoint/JS bridge to support section-based upload/download.

Required capabilities:

```text
Upload one encrypted section by key.
Download one encrypted section by key.
Download sync metadata.
Return structured JSON for expected errors.
Reject oversized section with structured payload_too_large error.
Do not throw raw Worker exceptions for expected size errors.
```

Structured oversized error:

```json
{
  "ok": false,
  "error": "payload_too_large",
  "section": "task-history:2026-05",
  "payloadBytes": 1234567,
  "maxPayloadBytes": 900000
}
```

Add top-level try/catch to Cloudflare functions:

```js
try {
  return await handleRequest(context);
} catch (error) {
  console.error("cloud-sync failed", error);

  return jsonResponse({
    ok: false,
    error: "cloud_sync_worker_exception",
    message: error?.message ?? "Unknown cloud sync error."
  }, 500);
}
```

Cloudflare function rules:

```text
Never log encrypted payload content.
Never log decrypted content.
Never accept or store Habitica API tokens.
Never use app cloud sync endpoint for shared party queue state.
```

---

# 7. Refresh Optimization

## Goal

Stop refreshing all Habitica data at the same time. Make the app responsive by refreshing visible and relevant data first, then updating everything else progressively.

## Add Application-Layer Refresh Coordinator

Create or adapt an application-layer refresh coordinator.

Suggested refresh domains:

```text
UserSummary
Tasks
Party
Inventory
Skills
StaticContent
TaskHistory
PartyQuestQueue
CloudAppSync
DerivedDashboard
DerivedInventory
DerivedTasks
```

Each domain should expose state:

```text
HasValue
IsFetching
IsManualRefresh
IsBackgroundRefresh
LastUpdatedAtUtc
LastError
FreshnessState
```

UI pages should read domain states and render partial loading states. Pages must not trigger one global “refresh everything” call unless the user explicitly performs a hard refresh.

---

# 8. Page-First Refresh Priority

When refreshing, prioritize the current page.

Dashboard:

```text
1. UserSummary
2. Party
3. Tasks
4. Inventory
5. Skills
6. DerivedDashboard
7. CloudAppSync in background
```

Party page:

```text
1. Party
2. PartyQuestQueue
3. Inventory quest scroll data if needed
4. UserSummary
5. DerivedDashboard in background
```

Tasks page:

```text
1. Tasks
2. UserSummary
3. TaskHistory current period
4. DerivedTasks
5. Older history in background
```

Inventory page:

```text
1. Inventory
2. UserSummary
3. Skills if needed
4. DerivedInventory
```

Spells page:

```text
1. Skills
2. UserSummary
3. Party if party buffs/state are displayed
```

---

# 9. Manual Refresh Behavior

When user presses Refresh:

```text
Refresh current page domains first.
Show visible field/card-level refreshing state on the current page.
Keep old values visible if they exist.
Use skeletons only where no usable value exists.
Do not hide the side menu.
Do not blank the whole app.
Do not wait for unrelated domains before updating visible data.
```

Recommended UI behavior:

```text
Old value exists:
    show old value dimmed + small refreshing indicator.

No value exists:
    show skeleton placeholder.

Button/action in progress:
    show button-level loading state.
```

---

# 10. Background Refresh Behavior

When data becomes stale:

```text
Refresh in background.
Do not replace visible values with skeletons.
Do not interrupt user actions.
Do not hide navigation.
When new values arrive, update in place.
Show subtle change animation for changed values.
```

Suggested freshness windows:

```text
UserSummary: 1-5 minutes
Tasks: 1-5 minutes when Tasks or Dashboard is active
Party: 1-5 minutes when Party or Dashboard is active
Inventory: 10-30 minutes
Skills/static content: long-lived cache
TaskHistory: 30-60 minutes or on demand
```

Use existing project freshness conventions if already defined.

---

# 11. Request Deduplication

Prevent duplicate simultaneous refreshes.

Required behavior:

```text
If same user/domain/parameter refresh is already running:
    await or reuse the existing task.

Do not start duplicate API calls for same user/domain/parameters.

Do not deduplicate mutations.
```

Deduplication key:

```text
(userId, refreshDomain, parameterHash)
```

Do not deduplicate:

```text
Task scoring
Health potion purchase
Skill cast
Quest queue vote
Quest queue status changes
Cron
```

---

# 12. Dependency Invalidation After Mutations

After a mutation, refresh only affected domains.

Examples:

Task scored:

```text
Refresh Tasks
Refresh UserSummary
Refresh DerivedDashboard
Optionally refresh TaskHistory current period
```

Skill cast:

```text
Refresh UserSummary
Refresh Skills
Refresh Party if party buff/support skill
```

Health potion bought:

```text
Refresh UserSummary
Refresh DerivedDashboard
```

Quest queue vote:

```text
Refresh PartyQuestQueue only
Do not refresh full Habitica account
```

Party quest action:

```text
Refresh PartyQuestQueue
Refresh Party only if active quest may have changed
```

Cron:

```text
Refresh UserSummary first
Then Tasks
Then Party
Then Skills/buffs
Then Inventory if needed
Then derived calculations
```

---

# 13. UI Loading Rules

Required:

```text
Skeletons for first load when no data exists.
Dimmed stale values for manual refresh when old data exists.
Button-level loading for direct actions.
Card/field-level loading for page data.
Subtle animation when background refresh changes a value.
Side menu remains visible during refresh if user is authenticated.
```

Not allowed:

```text
Whole app blanking during refresh
Side menu disappearing during refresh
Full dashboard skeleton when only task history is loading
Global loading overlay for background refresh
Blocking UI because optional cloud sync section is uploading
```

---

# 14. Implementation Order

Implement in this order:

```text
1. Inspect current cloud sync code path and old single-blob key format.
2. Add cloud sync section model and section key builder in Application layer.
3. Add per-section encrypted upload/download support.
4. Add sync metadata record.
5. Add legacy single-blob restore fallback.
6. Add per-section payload size guard.
7. Change cloud sync to partial-success behavior.
8. Update Cloudflare function error handling to return structured JSON.
9. Add refresh coordinator with domain-level state.
10. Convert manual refresh to current-page-first refresh.
11. Add request deduplication.
12. Add dependency invalidation after mutations.
13. Update UI loading states to field/card-level behavior.
14. Update logs and diagnostics.
15. Update `TECHNICAL.md` and `FEATURES.md`.
```

---

# 15. Acceptance Criteria

Cloud sync is fixed when:

```text
Large task/history data no longer breaks full cloud sync.
Preferences and saved presets sync even when history is too large.
Cloud sync uses multiple section keys, not one huge payload.
Oversized optional sections are skipped with structured warnings.
User sees partial sync state, not full sync failure.
Legacy old sync blob can still be restored.
No Habitica tokens are sent to Cloudflare.
Worker returns structured errors instead of generic 1101 where possible.
```

Refresh optimization is complete when:

```text
Login/dashboard becomes usable after minimal critical data is available.
Manual refresh updates current page data first.
Unrelated domains are not refetched before visible data.
Existing visible values stay visible while refreshing.
Background stale refresh does not interrupt the user.
Side menu does not disappear during refresh.
Duplicate API calls for same domain are deduplicated.
Mutations refresh only affected domains.
```

Diagnostics are acceptable when logs include:

```text
sync section name
section key
section payload size
section upload/download status
partial sync skipped sections
refresh domain
refresh reason
refresh duration
deduplication hit/miss
mutation invalidation result
```

Do not log:

```text
Habitica API tokens
raw encrypted payload
decrypted cloud sync content
sensitive credentials
```
