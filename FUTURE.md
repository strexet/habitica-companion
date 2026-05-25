# Future Work

Last validated: 2026-05-22.

This is the single implementation queue. Historical source plans were merged here and removed after implemented items were filtered out. Entries higher in the file are higher priority; finish them first.

Implemented behavior belongs in `FEATURES.md`, foundational architecture notes in `TECHNICAL.md`, Habitica endpoint rules in `HABITICA_API.md`, and UI guidance in `docs/UX_UI_MANIFEST.md`. Delete an entry from this file when it ships.

## Implementor Rules

1. Read the relevant source-of-truth docs before editing:
   - UI/UX: `docs/UX_UI_MANIFEST.md`
   - Architecture, sync, storage: `TECHNICAL.md`
   - Habitica API rules: `HABITICA_API.md`
   - Habitica party/quest link behavior: `docs/HABITICA_DEEPLINKS.md`
   - Cloudflare deployment and D1/KV: `docs/DEPLOY_CLOUDFLARE_PAGES.md`
2. Implement one entry only. Do not bundle unrelated cleanup, renames, or opportunistic refactors.
3. If a task lists `Touch:`, edit only those paths and direct tests unless the task explicitly permits more.
4. Add or update tests next to affected code. UI behavior changes need Razor component tests where similar tests exist.
5. User-facing behavior changes must update `FEATURES.md`; sync architecture or backend behavior changes must update `TECHNICAL.md`.
6. Schema changes need the next numbered migration under `migrations/` and a deployment-doc update.
7. Never send Habitica API tokens to Cloudflare party-sync or app-data sync endpoints.
8. Keep labels short and plain. If UI copy is ambiguous, choose the smallest clear label and proceed.
9. If a needed Habitica endpoint is not documented in `HABITICA_API.md`, stop and add a follow-up entry instead of guessing.
10. For Habitica party/quest links, use stable web URLs only. Do not add `habitica://`, Android `intent://`, app-opening probes, or mobile-app-specific party/quest links unless `docs/HABITICA_DEEPLINKS.md` is updated with new official support.
11. When interacting with this file, process `Pending to be added to Prioritized Next Changes` before starting implementation work. Move one pending item at a time into `Prioritized Next Changes`, either as a new self-contained entry or merged into an existing matching entry, then remove the moved item from pending. Keep `Top` additions before current prioritized entries, `Middle` additions after all `Top` additions and before current prioritized entries, and `Bottom` additions at the bottom of `Prioritized Next Changes`.

## Validated Implemented And Removed From Backlog

- Web-app MVP shell, sign-in, staged refresh, cached dashboard/task/party/inventory snapshots, diagnostics, and local/cloud data controls.
- Inventory preset layout, stat highlighting, equipment explorer, and preset persistence.
- Task browsing, type/status filters, guarded task scoring controls, expandable details, and task mutation freshness gates.
- Spell page, target recommendations, resource checks, and not-CRONed buff warning flow.
- Dashboard Start New Day action with explicit CRON confirmation, result feedback, and post-CRON refresh.
- Party page active quest metadata/rewards, CRON summary, member CRON graph, shared quest pool, queue, voting, recent completions, owner/admin/Officer controls, and quest start action.
- Dashboard pending damage estimate, knockout warning, and manual health-potion purchase action.
- Split-key encrypted Cloudflare app-data sync, legacy single-blob restore fallback, per-section payload guard, partial-success sync behavior, and refresh coordinator deduplication.
- Refresh-domain invalidation basics after implemented mutations.

## Pending to be added to `Prioritized Next Changes`

Work top to bottom. This is an intake list for rough notes that must become self-contained `Prioritized Next Changes` entries before implementation. Preserve the `Priority Instructions` and `Entries` structure.

### Priority Instructions

- Top – add to the top of the `Prioritized Next Changes` list (max priority).
- Middle – right after the `Top` entries and before current `Prioritized Next Changes` list items.
- Bottom – (default) the lowest priority entries, add to the bottom of the `Prioritized Next Changes` list.

### Entries:

- None.

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Spells Summary Field Cleanup

Goal: remove irrelevant top-of-page spell summary fields and move conditional context into spell cards where it helps the action.

Touch:
- `src/Habitica.Rules/Spells`
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- direct tests under `tests/Habitica.Rules.Tests/Spells/SpellViewModelFactoryTests.cs` and `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs`
- `docs/UX_UI_MANIFEST.md`
- `FEATURES.md`

Out of scope:
- changing spell target recommendation formulas;
- compact spell-card redesign beyond fields needed for this cleanup.

Acceptance:
- Top spell summary no longer shows duplicated or irrelevant fields: available mana, quest, current progress, your pending, party pending, class, MP, and stat points.
- Boss progress and party pending damage appear only on damaging spell cards when an active boss quest makes them relevant, merged into one concise info block.
- Stat-point context appears only on spell cards that can grant XP or otherwise make unspent points actionable, and only when stat allocation is unlocked.
- Existing spell casting warnings and resource checks still render in their current action flow.

UX-UI reference: `docs/UX_UI_MANIFEST.md` spells sections.

### Party Queue Control Completion

Goal: finish the remaining shared quest queue controls now that the base pool, queue, voting, recent-completion, and quest-start path exist.

Touch:
- `functions/api/party-sync/[partyId].js`
- `migrations/`
- `src/Habitica.Domain/Party`
- `src/Habitica.WebApp/State`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- direct tests under `tests/`
- `docs/DEPLOY_CLOUDFLARE_PAGES.md`
- `FEATURES.md`

Out of scope:
- optional vote budgets;
- historical analytics beyond the existing recent-completion list.

Acceptance:
- Party owner/admin/Officer controls can pin, force-select, resolve conflicts, and lock queue changes during selection.
- `Selected`, `Skipped`, and `Expired` states have user-facing actions and clear read states beyond the implemented invite/start flow.
- Queue expiration and stale-owner cleanup are deterministic and migration-safe.

UX-UI reference: `docs/UX_UI_MANIFEST.md` party quest planning sections.

### Party Access Proof Hardening

Goal: replace trust-only local party-sync claims with tokenized manager-invite proofs if local claims are too easy to abuse in real parties.

Touch:
- `functions/api/party-sync/[partyId].js`
- `src/Habitica.WebApp/wwwroot/js/sync/cloudflarePartySync.js`
- `src/Habitica.WebApp/State`
- direct tests under `tests/Functions/` and `tests/Habitica.WebApp.Tests/`
- `TECHNICAL.md`
- `FEATURES.md`

Out of scope:
- sending Habitica API tokens to Cloudflare;
- changing role names (`app admin`, `party owner`, `Officer`).

Acceptance:
- `readAccessProof()` / `resolvePartySyncAccess()` can accept the new proof without breaking existing local-claim migration.
- Owner/admin recovery remains possible.
- Worker tests cover invalid, expired, wrong-party, kicked-user, and owner/admin bypass cases.

### Active Quest Metadata And Detail Affordances

Goal: fill remaining active quest card metadata and drill-ins when data is available.

Touch:
- `src/Habitica.Api`
- `src/Habitica.Domain/Party`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- direct tests under `tests/`
- `FEATURES.md`

Out of scope:
- mobile app deep links; keep web fallback from `docs/HABITICA_DEEPLINKS.md`;
- fake values when Habitica data is missing.

Acceptance:
- Active quest card shows owner or starter, started date, details view, participants view, and rewards/details affordances when cached data exists.
- Missing fields render concise unavailable states.
- Participant names use the same member-detail focus behavior as the party member list.

### Dashboard Pending Quest Response Warning

Goal: show a dashboard warning when the party has an active quest invitation and the current user has not accepted or rejected it.

Touch:
- `src/Habitica.Domain/Party`
- `src/Habitica.Application/Dashboard`
- `src/Habitica.WebApp/Pages/DashboardPage.razor`
- direct tests under `tests/Habitica.Application.Tests/Dashboard/` and `tests/Habitica.WebApp.Tests/Pages/DashboardPageTests.cs`
- `FEATURES.md`

Out of scope:
- accepting or rejecting the quest from Dashboard;
- changing Party page quest response actions.

Acceptance:
- Dashboard shows a concise warning with a link to Party when the current user has not responded to the active quest invitation.
- Warning does not show after the user accepts, rejects, or when no active quest invitation exists.
- Link lands on the Party page where the response action is available.

UX-UI reference: `docs/UX_UI_MANIFEST.md` dashboard and party quest planning sections.

### Refresh UX And Cloud Sync Status

Goal: make refresh, background sync, and cloud sync state visible at the right scope while keeping cached data usable and moving sign-in quickly to Dashboard.

Touch:
- `docs/UX_UI_MANIFEST.md`
- `src/Habitica.Application/Sync`
- `src/Habitica.WebApp/State`
- `src/Habitica.WebApp/Pages/SettingsPage.razor`
- `src/Habitica.WebApp/Pages/DashboardPage.razor`
- pages/cards with manual refresh or domain freshness indicators
- diagnostics surfaces under `src/Habitica.WebApp/Pages/LiveTestsPage.razor` or existing diagnostics UI
- direct tests under `tests/Habitica.Application.Tests/Sync/` and `tests/Habitica.WebApp.Tests/`
- `TECHNICAL.md`
- `FEATURES.md`

Out of scope:
- changing Cloudflare encryption or key derivation;
- sending Habitica API tokens to Cloudflare sync endpoints;
- automatic destructive conflict resolution without explicit user choice;
- replacing local-first cached rendering with server-blocked page loads.

Acceptance:
- UI/UX manifest defines page-level, card-level, and field-level refresh status rules before implementation work starts.
- After sign-in, the app returns to Dashboard after the minimal successful user fetch and defers non-critical domain refreshes behind usable cached/current data.
- Visible page data remains interactive while background-only refresh or cloud sync work runs; global busy states are reserved for blocking mutations or first-load surfaces that have no usable cached data.
- Manual refresh, background refresh, mutation invalidation, and Cloudflare sync progress surface next to the affected page, card, field, or Settings section instead of only at page level.
- Cloud sync metadata uses per-section status records with section key, updated time, payload size, upload/download direction, and status.
- Settings shows which sections succeeded, failed, skipped, or were excluded, and supports configurable section-level sync exclusions such as diagnostics.
- Cloud sync conflict UI appears when remote and local sections diverge and requires an explicit keep-local, use-remote, or section-by-section choice.
- Diagnostics includes sync section key, payload size, upload/download status, partial skipped sections, refresh domain, refresh reason, refresh duration, deduplication hit/miss, and mutation invalidation result.
- Background updates can use subtle changed-value animation when a visible value changes, respecting reduced-motion preferences.
- Loading skeletons are used only where delayed content has a stable final structure; fast or background refreshes avoid skeleton flashes.

Research findings for implementation:
- Apple HIG feedback guidance supports passive status for routine state, stronger feedback for success/failure/warnings, and matching feedback weight to consequence: https://developer.apple.com/design/human-interface-guidelines/feedback
- Material progress guidance says use one indicator style per operation type, determinate progress when completion is knowable, indeterminate progress when duration is unknown, and whole-operation progress for sequences: https://m1.material.io/components/progress-activity.html
- Todoist treats offline mode as automatic and syncs offline changes after reconnect, which supports keeping task data interactive instead of blanking surfaces during connectivity loss: https://get.todoist.help/hc/en-us/articles/205144561-Use-Todoist-while-offline
- Notion exposes offline/download progress, an Offline settings tab, unavailable actions while offline, automatic resync, and a sync indicator confirming saved state; this maps well to Settings-level per-section visibility: https://www.notion.com/help/guides/working-offline-in-notion-everything-you-need-to-know
- Linear shows `Syncing` near the workspace when local changes are queued, includes a pending-change count, retries after restart, and documents overwrite risk in offline failsafe mode; use this pattern for concise persistent sync state and conflict warnings: https://linear.app/docs/get-the-app
- Dropbox avoids data loss in conflicting edits by preserving both versions instead of silently merging; cloud sync conflict UI should preserve local and remote sections until the user chooses: https://learn.dropbox.com/self-guided-learning/help-desk-course/common-team-member-challenges
- Expo local-first guidance frames fast UI as direct local reads/writes with background sync, but warns that sync/permissions/conflict handling remain product responsibilities; this matches Habitica Tool's local-first cache plus explicit cloud sync model: https://docs.expo.dev/guides/local-first/

UX-UI reference: `docs/UX_UI_MANIFEST.md` app-wide interaction rules.

## Backlog

These entries are lower priority but already merged from the historical plans. Before coding, split a broad bullet into the same `Goal / Touch / Out of scope / Acceptance / UX-UI reference` shape used above.

### Tasks Page Enhancements

- Add week/month/year period selector for task statistics.
- Add task-history histogram and month activity chart on the Tasks page.
- Add a smaller activity chart inside expanded task details.

### Dashboard Navigation And Habitica Links

- Add dashboard section cards with direct navigation.
- Add an Open Habitica button and context-sensitive Habitica web links where stable URLs are known; follow `docs/HABITICA_DEEPLINKS.md` and do not add mobile app deep links or custom-scheme fallbacks.

### Advanced Party Quest Features

- Add optional limited vote budgets only if requested as an advanced voting mode.
- Add historical quest analytics beyond the recent-completion list and soft queue penalty.
- Split current party quest state and queue planning into clearer modes, such as tabs or a segmented switch.

### Gear And Equipment Planning

- Add inventory before/after stat deltas for equip actions.
- Add equipment optimization for goals such as Perception, Strength, balanced stats, boss damage, and survival.
- Allow saving optimizer recommendations as named gear sets or presets.

### Skill Macros

- Add a macro collection for predefined skill/equipment sequences.
- Add dry-run previews with planned equipment changes, target selection, mana cost, expected requests, warnings, and stop conditions.
- Keep macro execution sequential and stop on validation failures or unexpected state changes.

### Bulk Sell Planner

- Add a bulk sell helper that identifies items likely safe to sell.
- Include explanation for why each item is considered safe or unsafe.
- Require preview and explicit confirmation before any sell action.

### Action Result Estimates

- Add estimates for selected actions, including expected damage, gold, skill effects, boss progress, and player damage risk.
- Clearly distinguish exact API-returned values from local estimates and assumption-based formulas.

### UX Cleanup

- Add confirmation to Settings destructive actions such as clearing local browser data.
- Reduce repeated hero/help copy for returning authenticated users.
- Add sticky first-column or label context for mobile stat tables.
- Consider compact spell cards after the current spell card layout has been tested with real use.
