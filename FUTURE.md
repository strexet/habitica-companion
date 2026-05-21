# Future Work

Last validated: 2026-05-21.

This file is the implementation queue. Each entry is written so an autonomous coding agent can pick it up, ship it, and stop — without drifting into unrelated refactors. Entries higher in the file are higher priority; finish them first.

Implemented items are deleted from this file (not kept as strikethrough). Current implemented behavior belongs in `FEATURES.md`, foundational architecture notes in `TECHNICAL.md`, Habitica endpoint rules in `HABITICA_API.md`, and UI/UX guidance in `docs/UX_UI_MANIFEST.md`.

## Implementor Rules (read before picking a task)

These rules apply to every entry below. Violating them is drift.

1. **Authoritative docs.**
   - UI/UX decisions (layout, copy, controls, responsiveness): `docs/UX_UI_MANIFEST.md` is binding. Read the relevant page section before writing markup or CSS. If a change creates a new reusable pattern, update the manifest in the same change set.
   - Architecture, sync, storage: `TECHNICAL.md`.
   - Habitica API rules (rate limits, headers, allowed endpoints): `HABITICA_API.md`. Do not call endpoints that are not documented as allowed.
   - Deployment / D1 / KV: `docs/DEPLOY_CLOUDFLARE_PAGES.md`.

2. **Scope discipline.**
   - Implement only the entry you picked. Do not bundle "while I'm here" cleanups, renames, or refactors in unrelated files.
   - If a task lists `Touch:` paths, you may edit those plus their direct tests. Editing files outside that list requires either the task explicitly allowing it ("may also touch …") or a separate task entry.
   - If a task lists `Out of scope:`, those items are forbidden in this change set even if they look related.
   - Do not introduce new abstractions, helper layers, feature flags, or "future-proof" parameters unless the task asks for them.

3. **Done means done.**
   - Every task lists `Acceptance:` bullets. All of them must be true before marking the task complete.
   - Add or update tests next to the affected code (same project / folder pattern as existing tests). UI behavior changes require Razor component tests where similar tests already exist (`tests/Habitica.WebApp.Tests/Pages/*`).
   - Run the relevant test project(s) and the JS Node tests under `tests/Functions/` if you touched a Cloudflare Function.
   - After completing a task, delete its entry from this file and add a one-line summary to `FEATURES.md` under the matching section.

4. **No silent behavior change.**
   - User-facing copy changes must be reflected in any test that asserts on the old copy.
   - Schema or sync-shape changes need a numbered SQL migration under `migrations/` (next free `NNNN_*.sql`) and a corresponding update to `docs/DEPLOY_CLOUDFLARE_PAGES.md` step 4.
   - Storage-key changes that affect synced user data must update `src/Habitica.Storage/StorageKeys.cs` plus the export/import path in `LocalUserDataPortabilityService.cs`.

5. **Don't drift on naming.**
   - Reuse existing C# record/property names from `src/Habitica.Domain/` and `src/Habitica.Application/` rather than coining new ones.
   - The companion-app management role taxonomy is fixed: `app admin` (cross-party, stored in D1 `app_admins`), `party owner` (companion-app role assigned per party), `Officer` (per-party, assigned by owner/admin). Do not invent additional roles.

6. **Ask only when blocked.**
   - If a task is ambiguous on UI copy, pick the shortest plain-English label and proceed; the manifest's tone wins ties.
   - If a task is ambiguous on a Habitica API call that is not documented in `HABITICA_API.md`, stop and leave a follow-up entry; do not guess endpoints.

---

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

> Not currently supported: "Open in Habitica" cannot deep-link to the official mobile app's party/quest view. See `docs/HABITICA_DEEPLINKS.md`. Keep the existing web fallback until Habitica documents and ships iOS/Android party or quest deep links.

### P5 — Clickable member names in Party Sync Roles and Kicked users list

- **Goal:** make member display names in **Party Sync Roles** and **Party Sync Moderation → Kicked users** behave like the clickable quest completor name in the **Active Quest** section.
- **Touch:**
  - `src/Habitica.WebApp/Pages/PartyPage.razor`.
  - Reuse the existing component / handler the Active Quest completor name uses. Locate it first (grep for the existing click handler in `PartyPage.razor`); do not introduce a new component.
- **Out of scope:** changing the destination of the click (open the same affordance the completor name already opens), adding new profile data, restyling the rest of the role/kick rows.
- **Acceptance:**
  - Clicking a name in Roles or in the Kicked list opens the same UI as clicking the Active Quest completor name.
  - Keyboard focus + accessible name match the Active Quest pattern (same component → same a11y).
  - Tests assert click behavior in the new locations.
- **UX/UI reference:** manifest's "interaction safety" and link/affordance rules.

### P6 — App admin can assign the companion-app party-owner role

- **Goal:** users present in D1 `app_admins` can promote any current party member to the companion-app party-owner role (separate from the Habitica party leader).
- **Touch:**
  - `functions/api/party-sync/[partyId].js` — new authorized action that writes a role row (reuse `party_sync_roles` table; role value `"Owner"` if a value is not already chosen — check the table first and reuse if present).
  - `src/Habitica.WebApp/Pages/PartyPage.razor` — admin-only "Assign party owner" control in the Party Sync Roles section (visible only when the current user is an app admin).
  - `src/Habitica.WebApp/Sync/CloudflarePartyDataSyncProvider.cs` and `IRemotePartyDataSyncProvider.cs` — new client method matching the existing role-assignment patterns.
  - Tests: `tests/Functions/party-sync-access.test.mjs` and `tests/Habitica.WebApp.Tests/Pages/PartyPageTests.cs`.
- **Authorization rule:** only callers where `isAppAdmin(db, userId)` returns true may invoke the action. The Habitica party leader does NOT get this ability via this entry.
- **Out of scope:** revoking the Habitica leader role, changing how `isOwner` is computed for non-admin flows, building a UI for managing app admins themselves (admins are still seeded via `migrations/seed_app_admins.sql`).
- **Acceptance:**
  - App admins see and can use the Assign Party Owner control; non-admins do not see it.
  - Endpoint rejects non-admin callers with 403 and a clear message.
  - Tests cover the allow path (admin succeeds), the deny path (non-admin 403), and the UI visibility rule.
- **UX/UI reference:** manifest's role/permission badge patterns; new control must match existing officer-assignment styling.

### P7 — Quest owner can start the selected quest from the companion app

- **Goal:** the Active Quest section gains a "Start quest" action visible only to the selected quest's owner, which calls Habitica to actually start the quest.
- **Precondition:** the exact Habitica endpoint and request shape are documented in `HABITICA_API.md`. If they are not, stop and add a follow-up entry instead of guessing; do not call undocumented endpoints.
- **Touch (only once precondition holds):**
  - `src/Habitica.Api/HabiticaApiClient.cs` — new method following the existing call/retry/logging conventions.
  - `src/Habitica.WebApp/Pages/PartyPage.razor` — Start-quest control inside the Active Quest section, gated on `currentUserId == selectedQuestOwnerId`.
  - `src/Habitica.WebApp/State/AppSessionController.cs` + `IAppSessionController.cs` — new session-level action that wraps the API call and triggers a refresh of party + quest state on success.
  - Tests: API client test (mock HTTP), session controller test, Razor test for visibility and click.
- **Out of scope:** invite flows, quest-creation flows, automatic start triggers, retry loops beyond what `HabiticaApiClient` already provides.
- **Acceptance:**
  - Control is visible only when the current user owns the selected quest and the quest is in a startable state.
  - Successful click triggers a Party state refresh and the Active Quest card updates to the active state without a full reload.
  - Failure surfaces an inline error matching the manifest's error-display pattern.
- **UX/UI reference:** manifest's mutation-control placement rules ("close to the state they change").

### P8 — Reorderable tasks, dailies, and habits on the Tasks page

- **Goal:** users can reorder items inside each Tasks-page list (tasks, dailies, habits) and the order persists across reloads and devices.
- **Touch:**
  - `src/Habitica.WebApp/Pages/TasksPage.razor` — reorder UI (prefer the simplest control that is keyboard-accessible; up/down arrow buttons are acceptable, drag-and-drop only if a shared primitive already exists).
  - New storage key in `src/Habitica.Storage/StorageKeys.cs` (e.g. `TaskOrderPreferences`) holding a per-list array of task IDs.
  - `src/Habitica.Application/Sync/LocalUserDataPortabilityService.cs` — include the new key in export, import-merge, and clear-data paths.
  - `src/Habitica.WebApp/Sync/CloudflareUserDataSyncProvider.cs` and `src/Habitica.WebApp/wwwroot/js/sync/cloudflareSync.js` — include the new key in the KV sync section list.
  - Tests: storage test for the new key, portability test for export/import round-trip, Razor test for reorder UI.
- **Conflict / merge rule:** on import-merge, the imported order wins for IDs present in both; IDs only on one side keep their relative position and append after shared IDs.
- **Out of scope:** server-side reordering on Habitica itself (this is local-only display order), reordering across list types, bulk-edit UI, filtering or grouping.
- **Acceptance:**
  - User can move any item up/down within its list; new order persists after reload.
  - New order is included in the export blob and survives a clear-data + import round trip.
  - KV sync uploads and downloads the new order section; verified by an existing sync-section test pattern.
  - Adding/removing tasks on the Habitica side does not break the saved order (unknown IDs are ignored; new IDs appear at the end).
- **UX/UI reference:** manifest's Tasks page section; "Keep mutation controls explicit" applies — reorder controls must live on the row they affect.

---

## Backlog (lower priority)

The entries below are not yet broken down into Touch/Acceptance form. Before picking one, restructure it into the same format as the prioritized section above (Goal / Touch / Out of scope / Acceptance / UX-UI reference). Do not start coding until the entry is restructured and the Implementor Rules are re-read.

### From `1_habitica_companion_pending_features_plan.md`

#### Party Page Quest Improvements

- Add tokenized manager-invite party-sync proofs if local claims become too easy to abuse in real parties. The current access path is isolated behind `readAccessProof()` / `resolvePartySyncAccess()` so a future proof version can replace `local-claim-v1`.
- Fill remaining active quest card metadata and actions: quest owner or starter, started date, details view, participants view, and reward/details affordances when the data is available.
- Add an owner readiness mutation flow. The database field and read-only display exist, but the shared queue UI does not expose a toggle action yet.
- Add party leader queue controls for manual pinning, force-selecting, conflict resolution, and locking queue changes during selection.
- Add user-facing actions and handling for `Selected`, `InviteSent`, `Skipped`, and `Expired` queue states beyond the current queued, active, completed, and removed path.
- Add queue expiration and stale-owner cleanup rules.
- Add optional limited vote budgets only if requested as an advanced voting mode.
- Add historical quest analytics beyond the recent-completion list and soft queue penalty.

#### Tasks Page Enhancements

- Add week/month/year period selector for task statistics.
- Add task-history histogram and month activity chart on the Tasks page.
- Add a smaller activity chart inside expanded task details.

#### Dashboard Improvements

- Add a pending damage estimate box.
- Explain which damage sources are included and excluded from the estimate.
- Add warning state when estimated damage may kill or nearly kill the user.
- Add a manual Buy Health Potion action near damage information. Do not make potion purchase automatic.
- Add dashboard section cards with direct navigation.
- Add an Open Habitica button and context-sensitive Habitica links where stable URLs are known.

#### Login and Refresh Improvements

- Add a redirect guard that skips the sign-in page for authenticated stored credentials without flashing the login UI.
- Return to the dashboard after the minimal successful user fetch; defer non-critical domain refreshes behind usable cached/current data.
- Add stale-while-revalidate UI behavior so cached values stay visible while stale domains refresh in the background.
- Add field/card-level refresh indicators for manual refresh and Cloudflare sync progress.
- Add subtle changed-value animation after background updates.
- Add loading skeletons where delayed content has a stable final structure.

### From `2_cloud_sync_split_key_refresh_instructions.md`

#### Cloud Sync Improvements

- Expand cloud sync metadata from uploaded/failed section lists into per-section status records with section key, updated time, payload size, and status.
- Surface per-section sync status in Settings so users can see which sections succeeded, failed, or were skipped.
- Add configurable section-level sync exclusions, for example skipping diagnostics sync to save storage.
- Add cloud sync conflict resolution UI when remote and local sections diverge.
- Add diagnostics for sync section key, payload size, upload/download status, partial skipped sections, refresh domain, refresh reason, refresh duration, deduplication hit/miss, and mutation invalidation result.

#### Refresh Optimization Follow-Up

- Keep visible page data interactive while background domains refresh; avoid global busy states for background-only work.
- Surface refresh status next to the affected card or field, not only as page-level busy state.
- Log request deduplication hit/miss and refresh duration per domain.
- Make mutation invalidation results visible in diagnostics.

### Other Future Features

#### Gear and Equipment Planning

- Add inventory before/after stat deltas for equip actions.
- Add equipment optimization for goals such as Perception, Strength, balanced stats, boss damage, and survival.
- Allow saving optimizer recommendations as named gear sets or presets.

#### Skill Macros

- Add a macro collection for predefined skill/equipment sequences.
- Add dry-run previews with planned equipment changes, target selection, mana cost, expected requests, warnings, and stop conditions.
- Keep macro execution sequential and stop on validation failures or unexpected state changes.

#### Bulk Sell Planner

- Add a bulk sell helper that identifies items likely safe to sell.
- Include explanation for why each item is considered safe or unsafe.
- Require preview and explicit confirmation before any sell action.

#### Action Result Estimates

- Add estimates for selected actions, including expected damage, gold, skill effects, boss progress, and player damage risk.
- Clearly distinguish exact API-returned values from local estimates and assumption-based formulas.

#### UX Cleanup

- Split current party quest state and queue planning into clearer modes, such as tabs or a segmented switch.
- Add task filters for type, status, due window, and value polarity.
- Add confirmation to Settings destructive actions such as clearing local browser data.
- Reduce repeated hero/help copy for returning authenticated users.
- Add sticky first-column or label context for mobile stat tables.
- Consider compact spell cards after the current spell card layout has been tested with real use.
