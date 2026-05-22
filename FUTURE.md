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
