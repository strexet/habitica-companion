# Future Work

Last validated: 2026-05-27.

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
- Staged sign-in refresh UX, scoped refresh indicators, per-section cloud sync status, sync exclusions, and explicit cloud-sync conflict choices.
- Two-handed weapon awareness in spell equipment recommendations: weapon/shield selected as a `twoHanded`-aware pair; shield omitted when the two-handed weapon outscores the best one-handed + shield combination.
- Task history statistics, aggregate history charts, expanded-card per-task charts, manual task ordering, drag handles, keyboard reordering, and move buttons.
- Inventory equipment optimizer with goal selector, before/after stat deltas, recommendation equip/save actions, and two-handed weapon handling.
- Bulk sell planner for eggs, food, and hatching potions with safe surplus preview, explicit confirmation, sequential sell execution, diagnostics, and post-sell refresh.
- Dashboard Start New Day optional gear optimization: INT for post-CRON mana, CON/survival for lower damage risk, previewed stat deltas, already-equipped state, and sequential equip-before-CRON execution.
- Spells sticky current-mana bar with available MP, max MP, class, and persistent scroll visibility above spell cards.

## Pending Queue

### Queued items to be added to `Prioritized Next Changes`

Work top to bottom. This is an intake list for rough notes that must become self-contained `Prioritized Next Changes` entries before implementation. Preserve the `Priority Instructions` and `Entries` structure.

### Priority Instructions

- Top – add to the top of the `Prioritized Next Changes` list (max priority).
- Middle – right after the `Top` entries and before current `Prioritized Next Changes` list items.
- Bottom – (default) the lowest priority entries, add to the bottom of the `Prioritized Next Changes` list.

### Entries:

(empty)

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### App Color Scheme System

Goal: centralize app color tokens and add selectable color schemes, including editable user custom schemes, so the app can switch palettes consistently without scattering raw colors across CSS and page code.

Touch:
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.WebApp/Pages/SettingsPage.razor`
- `src/Habitica.WebApp`
- `src/Habitica.Storage`
- `src/Habitica.Application/Sync`
- direct tests under `tests/Habitica.WebApp.Tests/`, `tests/Habitica.Storage.Tests/`, and `tests/Habitica.Application.Tests/`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md`
- `README.md` only if setup or user-facing feature summary changes

Required inputs:
- Current app palette from existing `:root` CSS variables and remaining hard-coded app colors. This built-in scheme is named `Alpha`.
- Habitica-inspired built-in palette named `Habitica`. Use current Habitica web/app colors as reference; verify from public/current sources before implementing.
- `/Users/petr/Projects/habitica-tool/gryphy/Gryphy.png`. Sample the image and create two readable built-in Gryphy-derived schemes named `Gryphy Light` and `Gryphy Dark`; adjust sampled colors as needed for contrast and usability.
- Review current color-scheme patterns in other popular apps before implementation. Record only durable product/design guidance in `docs/UX_UI_MANIFEST.md`; do not cite transient marketing pages unless needed.

Implementation shape:
- Define built-in developer-editable schemes in one code/config location, not as duplicated page-specific constants. The schema should cover semantic tokens used by the app, such as background, surface, border, text, muted text, primary/accent, warning/gold, danger, success, focus, shadows, and chart/task-value colors.
- Convert app styling to consume semantic CSS variables from the active scheme. Keep domain/status meaning clear and do not rely on color alone.
- Add a small theme runtime that applies the selected scheme early enough to avoid a visible wrong-theme flash on normal app startup.
- Add Settings controls for selecting a built-in scheme, selecting saved custom schemes, creating/renaming/deleting a custom scheme, and editing custom scheme token values.
- Persist the selected scheme locally for fast reload and in portable user data so backup/import/cloud sync can preserve it. Persist user-created custom schemes as user data, separate from built-in developer schemes.
- Custom scheme editing should validate color values, prevent deleting built-in schemes, and provide a reset path back to a built-in scheme.
- Preserve accessibility: text/background combinations must meet readable contrast targets for normal app text, controls, warnings, and danger states.

Out of scope:
- changing layout, information architecture, or page content beyond Settings controls needed for scheme management;
- user-uploaded images for palette extraction;
- per-page themes;
- sending theme preferences or custom colors to Habitica;
- redesigning MudBlazor component internals beyond setting supported theme/css variables.

Acceptance:
- All current root palette values are represented by the `Alpha` built-in scheme and the app looks materially the same when `Alpha` is selected.
- Built-in schemes exist for `Alpha`, `Habitica`, `Gryphy Light`, and `Gryphy Dark`.
- A developer can add or adjust built-in schemes from one obvious config/code location.
- Settings lets the user choose a scheme, create a named custom scheme, edit custom colors, rename it, delete it, and reset back to a built-in scheme.
- The selected scheme survives browser reload through local storage and is included in portable user data backup/import/cloud sync.
- User-created schemes are not mixed with built-in scheme definitions and do not require code changes.
- Invalid custom color input is rejected with concise feedback and does not partially apply a broken scheme.
- Existing status, danger, warning, success, task-value, and chart states remain distinguishable in every built-in scheme without relying only on color.
- Tests cover built-in scheme config, storage keys/portable sync mapping, Settings selection/custom edit behavior, reload persistence hook, and rejection of invalid custom colors.
- `docs/UX_UI_MANIFEST.md` documents scheme token rules, contrast expectations, and when future UI should add new semantic tokens instead of hard-coded colors.

### Party Sync Tokenized Invite Proofs

Goal: add an optional manager-issued party-sync proof mode. Parties continue to work with browser-only `local-claim-v1` by default, but an owner/app admin can enable tokenized invite proofs so shared party queue access no longer depends only on client-supplied local claim headers.

Touch:
- `functions/api/party-sync/[partyId].js`
- `src/Habitica.WebApp/wwwroot/js/sync/cloudflarePartySync.js`
- `src/Habitica.WebApp/State`
- `migrations/`
- direct tests under `tests/Functions/` and `tests/Habitica.WebApp.Tests/`
- `TECHNICAL.md`
- `FEATURES.md`
- `docs/DEPLOY_CLOUDFLARE_PAGES.md`

Implementation shape:
- Add a D1 migration for invite-proof state. Store party id, proof id or token hash, display label, issued/revoked/expires timestamps, issuer metadata, and an enabled/disabled party setting. Do not store raw reusable proof tokens if a hash is enough.
- Keep `local-claim-v1` as the default and as the recovery path. If tokenized proof mode is disabled or no active proof exists, existing party-sync behavior must remain unchanged.
- Add owner/app-admin management actions to create, list, revoke, rotate, remove, enable, and disable tokenized proofs. Existing Officer permissions should not automatically grant proof-management powers unless the code explicitly already treats the caller as owner/app admin.
- Extend `readAccessProof()` to parse both `local-claim-v1` and the new proof version. Extend `resolvePartySyncAccess()` so tokenized proof identity still passes through the same owner/admin/Officer/kick checks used by local claims.
- Update the browser sync bridge to send the new proof headers only when local state has an active tokenized proof. Do not send Habitica API tokens, raw credentials, or authorization headers to Cloudflare.
- Surface concise UI/state feedback for proof mode: disabled, enabled, active proof, revoked/expired proof, and fallback to local claim.

Out of scope:
- sending Habitica API tokens to Cloudflare;
- changing role names (`app admin`, `party owner`, `Officer`);
- removing the existing `local-claim-v1` reader;
- replacing party-sync roles, queue permissions, or kick semantics;
- requiring tokenized proofs for existing parties by default.

Acceptance:
- With no invite proof created, and with tokenized mode disabled, all existing party-sync reads/writes still work through `local-claim-v1`.
- Owner/app admin can enable and disable tokenized proof mode.
- Owner/app admin can create, list, revoke, rotate, and remove invite proofs without exposing Habitica credentials. Removing the active proof invalidates the old proof; the party can issue a new proof later and falls back to browser-only `local-claim-v1` while no active proof exists.
- `readAccessProof()` accepts both the new proof version and `local-claim-v1`; unsupported proof versions still fail with a clear 401.
- `resolvePartySyncAccess()` rejects malformed, expired, revoked, wrong-party, and kicked-user tokenized proofs.
- Owner/app-admin recovery remains possible when tokenized proofs are missing, expired, revoked, or misconfigured.
- Frontend bridge sends tokenized proof headers only when an active proof is available, and otherwise keeps the existing local-claim headers.
- Worker tests cover: local-claim fallback, valid proof, malformed proof, expired proof, revoked proof, removed proof, wrong-party proof, kicked-user rejection, owner/admin bypass/recovery, enable/disable mode behavior, and rotate invalidating the old proof.
- WebApp tests cover proof-mode state mapping and header selection without sending Habitica API tokens to Cloudflare.

### Active Quest Metadata And Detail Affordances

Goal: fill remaining active quest card metadata and drill-ins when Habitica or cached shared state exposes the data.

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
- Active quest snapshot preserves nullable owner/starter and started-at fields when the API or shared queue state exposes them.
- Active quest card shows owner or starter, started date, details view, participants view, and rewards/details affordances when cached data exists.
- Missing owner/starter/started-at fields render concise unavailable states without inventing values.
- Participant names use the same member-detail focus behavior as the party member list.

## Backlog

These entries are lower priority. Each entry is self-contained and should be promoted into `Prioritized Next Changes` before implementation.

### Party Quest Workspace Modes

Goal: separate the Party page's current quest, shared queue planning, quest pool, recent completions, and member/CRON sections into clearer scan modes.

Touch:
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/PartyPageTests.cs`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if shared navigation or mode guidance changes

Out of scope:
- changing party-sync data contracts;
- adding new quest analytics;
- changing Habitica party or quest links.

Acceptance:
- Party page provides a tab or segmented mode switch for current quest, planning, history, and members/CRON.
- Existing actions keep their current authorization and stale-data guards.
- Empty and offline cached states remain visible in the relevant mode.
- Component tests cover mode switching and at least one guarded action still rendering in its mode.

### Party Quest History Analytics

Goal: summarize stored shared quest completion history beyond the current recent-completion cards and queue penalty labels.

Touch:
- `src/Habitica.Domain/Party`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- `functions/api/party-sync/[partyId].js` only if the existing recent-completion payload is not enough
- direct tests under `tests/`
- `FEATURES.md`

Out of scope:
- collecting Habitica data that is not already available through current snapshots, chat completion signals, or shared queue records;
- optional vote budgets;
- changing quest queue ordering.

Acceptance:
- History view shows aggregate completions by quest and by owner from available shared history.
- Analytics clearly state the covered time window and when data is unavailable or sparse.
- Existing recent-completion removal permissions remain unchanged.
- Tests cover aggregate calculations and sparse/no-history rendering.

### Skill Macro Collection MVP

Goal: implement the planned local macro collection for predefined equipment and skill sequences.

Touch:
- `src/Habitica.Rules/Skills`
- `src/Habitica.Application`
- `src/Habitica.WebApp`
- direct tests under `tests/`
- `FEATURES.md`
- `TECHNICAL.md` if storage or execution architecture changes

Out of scope:
- arbitrary user code execution;
- loops or unbounded repeat-until macros;
- storing credentials in exported macros;
- server-side macro execution.

Acceptance:
- Users can create, edit, delete, and run local declarative macros using initial step types from `FEATURES.md`.
- Dry-run preview shows planned equipment changes, selected targets, mana cost, expected requests, warnings, and stop conditions.
- Execution runs sequentially, persists progress, refreshes or updates local state after mutating steps, and stops on validation failures, API errors, stale state, or unexpected state changes.
- Macro steps can reference existing inventory preset ids and dynamic gear strategies without copying transient recommendation data.
- Tests cover parsing/validation, missing gear, insufficient mana, stale data, restore-original-gear behavior, and partial execution failure.

### Task Mutation Dry-Run Summaries

Goal: add stronger pre-action summaries for existing task scoring/checkoff controls where local data can make the mutation clearer.

Touch:
- `src/Habitica.Api`
- `src/Habitica.Application`
- `src/Habitica.WebApp/Pages/TasksPage.razor`
- direct tests under `tests/`
- `HABITICA_API.md` if endpoint response assumptions are added or corrected
- `FEATURES.md`

Out of scope:
- duplicating spell estimates, Dashboard pending-damage/health-potion helpers, Inventory equip deltas, or bulk-sell previews;
- adding undocumented Habitica mutation endpoints;
- claiming exact GP/XP/HP deltas unless the value comes from a live API response or a documented formula.

Acceptance:
- Task cards show a concise dry-run summary for supported scoring/checkoff actions before multi-score or ambiguous mutations execute.
- Summaries distinguish exact API-returned values, local estimates, and unavailable values.
- Multi-score habit actions still run sequentially and stop on failure.
- Tests cover summary rendering, stale-data blocking, and unavailable-estimate copy.

### Settings Danger Zone Confirmation

Goal: require an explicit confirmation step before clearing local browser data from Settings.

Touch:
- `src/Habitica.WebApp/Pages/SettingsPage.razor`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/`
- `FEATURES.md`

Out of scope:
- changing the data actually cleared by `ClearLocalDataAsync()`;
- adding new import/export behavior.

Acceptance:
- Clear Local Data opens or reveals a confirmation that names credentials, cached Habitica data, party history, diagnostics, and setup data.
- The destructive action does not call `ClearLocalDataAsync()` until the confirmation control is activated.
- Cancel/close keeps local data untouched.
- Tests cover initial click, cancellation, and confirmed clearing.

### Returning User Copy Compression

Goal: reduce repeated hero/help copy for authenticated returning users while keeping first-run empty states understandable.

Touch:
- `src/Habitica.WebApp/Pages`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct page tests under `tests/Habitica.WebApp.Tests/Pages/`
- `docs/UX_UI_MANIFEST.md` if shared copy rules change

Out of scope:
- redesigning navigation;
- removing first-run or unauthenticated guidance;
- changing data loading behavior.

Acceptance:
- Returning authenticated users see denser top sections on Dashboard, Tasks, Party, Inventory, and Spells.
- First-run, signed-out, stale-data, and empty-cache states still explain the next action.
- Tests cover at least one authenticated returning state and one unauthenticated/empty state.

### Mobile Stat Table Context

Goal: keep row labels visible or repeated when wide stat tables scroll horizontally on small screens.

Touch:
- `src/Habitica.WebApp/wwwroot/css/app.css`
- pages using `.stats-table`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/` if markup changes
- `docs/UX_UI_MANIFEST.md` if table guidance changes

Out of scope:
- replacing table data with cards across desktop;
- changing stat calculations.

Acceptance:
- Mobile-width stat tables keep row identity visible through a sticky first column or repeated row-label context.
- Horizontal scrolling remains available for dense table values.
- Desktop table layout remains unchanged except for harmless label-context support.

### Compact Spell Card Density Pass

Goal: make spell cards easier to scan after the current full-card layout, recommendations, and CRON warning flow have been exercised.

Touch:
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs`
- `FEATURES.md`

Out of scope:
- changing spell estimate formulas;
- changing dynamic gear recommendation selection;
- changing cast execution order or CRON-warning semantics.

Acceptance:
- Spell cards keep target selection, count, mana preview, cast action, estimate text, CRON warning, and equipment recommendations available.
- Repeated low-priority explanatory copy is collapsed, summarized, or moved behind local detail affordances.
- Active casting progress and errors remain prominent.
- Tests cover key controls still rendering after the density change.
