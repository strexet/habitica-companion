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
- App color scheme system with centralized semantic tokens, Alpha/Habitica/Gryphy built-in schemes, Settings picker, custom editable schemes, shell/button/disabled/input theming, mobile localStorage fallback, fast local reload persistence, and portable sync storage.

## Pending Queue

### Queued items to be added to `Prioritized Next Changes`

Work top to bottom. This is an intake list for rough notes that must become self-contained `Prioritized Next Changes` entries before implementation. Preserve the `Priority Instructions` and `Entries` structure.

### Priority Instructions

- Top – add to the top of the `Prioritized Next Changes` list (max priority).
- Middle – right after the `Top` entries and before current `Prioritized Next Changes` list items.
- Bottom – (default) the lowest priority entries, add to the bottom of the `Prioritized Next Changes` list.

### Entries:

_(empty — pending note promoted into `Prioritized Next Changes`.)_

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Color Scheme Second-Pass Fixes (Light Contrast, Counters, Progress Bars)

Goal: close the remaining color-scheme defects from the prior theming pass. Two classes of bug: (a) light schemes regressed — light drawer background with light/whitish drawer text is unreadable; (b) several dark-scheme controls still bypass tokens (native counter arrows, drifted backgrounds, an uncolored prediction label, progress bars). Root cause across most defects: hardcoded `rgba(...)` values and browser-native input chrome instead of semantic tokens. Fix all without re-breaking either light or dark schemes.

Touch:
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.WebApp/wwwroot/js/colorSchemes.js`
- `src/Habitica.WebApp/Theme/ColorSchemeCatalog.cs`
- `src/Habitica.WebApp/Theme/ColorSchemeService.cs`
- `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor`
- `src/Habitica.WebApp/Pages/DashboardPage.razor`
- `src/Habitica.WebApp/Pages/TasksPage.razor`
- `src/Habitica.WebApp/Pages/InventoryPage.razor`
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- `src/Habitica.WebApp/Pages/LiveTestsPage.razor`
- direct tests under `tests/Habitica.WebApp.Tests/`
- `docs/UX_UI_MANIFEST.md` if progress-bar / token-usage guidance changes

Reported defects + located targets:

1. Light-scheme drawer contrast (regression — Alpha must not have changed):
   - Side menu unreadable: light drawer background + whitish text. Alpha tokens at `ColorSchemeCatalog.cs:15` (`DrawerText` `#f5efe2` line ~24, `DrawerBackground` `#163431` line ~36). Nav color rules: `app.css:279` (`.app-nav-menu .mud-nav-link` mixes `--drawer-text` 86%), `app.css:288` (`.active` mixes `--drawer-text` 8%).
   - Fix so every light scheme keeps readable drawer text/background contrast; confirm Alpha renders exactly as before the prior pass. Check all built-in schemes (`BuiltInSchemes` `ColorSchemeCatalog.cs:9` — Alpha, Habitica, Gryphy Light/Dark).

2. Native number-counter arrows black/invisible on dark backgrounds:
   - Dashboard "Spend gold – Bulk armoire": input `DashboardPage.razor:473`, css `.armoire-controls input` `app.css:1710` (hardcoded `background: rgba(255,255,255,0.82)` — replace with token).
   - Inventory "Bulk sell planner": input `InventoryPage.razor:183`.
   - Spells card counters: input `SpellsPage.razor:126`.
   - Shared input base `.app-input` `app.css:552`. Style the spinner/stepper arrows from tokens (or replace native spinners with token-styled controls) so arrows are visible in dark and light schemes.

3. Tasks card backgrounds — remove multi-color shades:
   - Task-value background must derive from one scheme-driven main color, computed as a gradient (not multiple fixed red/orange/green/blue shades). Targets in `TasksPage.razor` task card markup + `app.css` task-card value classes. (Same intent as the earlier task-value gradient request — implement the single-gradient model here.)

4. Party ACTIVE QUEST prediction label not themed:
   - "Expected to finish when XXX checks in today around YY:ZZ" — `PartyPage.razor:385` (inside `MudAlert` line 384), text from `FormatEstimateSummary()` `PartyPage.razor:2265`. Ensure label color comes from scheme tokens.

5. Spells header mana bar sizing:
   - `.spell-mana-bar` `SpellsPage.razor:41`, css `app.css:652`. Bar should fill the full space between the AVAILABLE MANA value and the class MAX-mana value labels.

6. Progress bars must follow the active scheme (docs reference per line 76 request):
   - Spell casting: `SpellsPage.razor:232` (`MudProgressLinear` Color.Primary).
   - Spell equipment-change: `SpellsPage.razor:240` (Color.Secondary).
   - Task mutation: `TasksPage.razor:340`.
   - Inventory preset execution: `InventoryPage.razor:830`.
   - CSS: `.spell-progress` `app.css:1479`, `.task-progress` `app.css:2409`, MudBlazor bar overrides `app.css:2607` (`--primary`) and `app.css:2611` (`--accent`).
   - Docs: `docs/UX_UI_MANIFEST.md:423` documents determinate progress for spell casting, equipment slot changes, and multi-step diagnostics — confirm all listed bars are token-driven; update manifest if a bar location is added/changed.

7. Diagnostics "GUARDED TESTS – Optional gear check" drifted background:
   - Panel `LiveTestsPage.razor:47` (`warning-card`), heading line 49, button "Run reversible gear test" `LiveTestsPage.razor:57`. CSS `.warning-card` `app.css:1721` (hardcoded `background: rgba(255,246,226,0.88)`) and any duplicate `app.css:2855`. Tone the drifted background to scheme tokens and keep the button readable after the fix.

Out of scope:
- changing scheme storage/sync format, scheme persistence, or adding/removing built-in schemes (Alpha must stay as originally defined);
- changing any non-color behavior (sync, scoring, quest/cast logic, diagnostics actions);
- adding new pages or controls.

Acceptance:
- Alpha and all light schemes render readable drawer background + text; no whitish-on-light or bright-on-bright blocks anywhere after the change.
- Number-counter arrows (armoire, bulk sell, spell cards) are clearly visible in both dark and light schemes.
- Task-value card backgrounds use a single scheme-derived gradient — no fixed red/orange/green/blue shades remain.
- Party ACTIVE QUEST completion-estimate label color tracks the active scheme.
- Spells header mana bar spans the full width between the AVAILABLE MANA and MAX-mana labels.
- All four progress bars (spell cast, spell equip-change, task mutation, preset execution) recolor with the scheme via tokens.
- Diagnostics Optional gear check panel background no longer drifts; its button stays readable.
- No hardcoded `rgba(...)` background remains in the listed `app.css` rules — replaced with semantic tokens.
- Switching across Alpha / Habitica / Gryphy Light / Gryphy Dark leaves every listed surface readable.
- Tests assert token-driven classes/variables on representative controls (drawer link, a counter input, a progress bar, the quest estimate label, the diagnostics panel).

### Dashboard Navigation Card Title/Description Spacing

Goal: fix navigation link cards (Companion and Habitica link sections) rendering title and body with no separation — e.g. "TasksScore and inspect cached tasks." should read as a title line plus a description line.

Touch:
- `src/Habitica.WebApp/Pages/DashboardPage.razor` (`RenderDashboardLink`, ~line 852 and link cards ~268-271)
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/`

Out of scope:
- changing nav targets, labels, or descriptions;
- redesigning the cards beyond title/body separation.

Acceptance:
- Every navigation link card renders title and description as distinct lines/elements with visible spacing.
- Applies to all affected nav menus (Companion and Habitica link cards).
- Test asserts title and description are separate nodes (not concatenated text).

### Compact Task Cards

Goal: shrink task cards to ease working through the task list. Collapsed card shows only task title and description plus move buttons and a Details toggle; all other current task info hides behind Details.

Touch:
- `src/Habitica.WebApp/Pages/TasksPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/`
- `FEATURES.md`
- reference `docs/UX_UI_MANIFEST.md` (do not violate its density/affordance rules)

Out of scope:
- removing any existing task detail data — only hide it behind Details;
- changing scoring/checkoff/reorder logic or freshness gates;
- changing task filters.

Acceptance:
- Collapsed task card shows title, description, move-card buttons (repositioned for the smaller card), and a Details toggle only.
- Details reveals all previously-visible per-task information; nothing is lost.
- Move buttons and keyboard reordering still function with the new layout.
- Layout follows `docs/UX_UI_MANIFEST.md`.
- Tests cover collapsed vs expanded rendering and move buttons still present.

### Spells Auto-Equip Best Option Default With Dropdown

Goal: when a spell's auto-equip feature has multiple equipment options, default-select the most profitable option and offer the rest via a dropdown sorted most→least profitable.

Touch:
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- spell equipment-recommendation logic under `src/Habitica.Rules` / `src/Habitica.Application` (only the selection/ordering surface; do not change scoring formulas)
- direct tests under `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs` and rule tests under `tests/`
- `FEATURES.md`

Out of scope:
- changing how profitability/stat deltas are computed;
- changing cast execution order or CRON-warning semantics;
- changing two-handed weapon pairing logic.

Acceptance:
- With multiple options, the most profitable option is preselected.
- A dropdown lists remaining options sorted most→least profitable.
- Selecting a non-default option updates the equip plan; single-option case shows no dropdown.
- Tests cover default selection, dropdown ordering, and selection change.

### Split Party Page Into Party And Quests Pages

Goal: relieve Party-page overload by separating quest-heavy blocks onto a dedicated Quests page, reusing existing data/logic.

Touch:
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- new `src/Habitica.WebApp/Pages/QuestsPage.razor`
- `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor` and Dashboard nav cards in `DashboardPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if navigation guidance changes

Layout split:
- `Party` page keeps: party description/info, PARTY SYNC ROLES, PARTY SYNC SETTINGS, a small quest card linking to the Quests page, members list, buff info, CRON graph.
- `Quests` page holds: all quest and quest-queue-related blocks (active quest, shared pool, queue, voting, recent completions, quest controls). Update intra-quest-card links to point at the Quests page.

Out of scope:
- new database fields or party-sync data-contract changes;
- changing quest/queue/sync logic, permissions, or stale-data guards;
- changing Habitica party/quest links.

Acceptance:
- Quest blocks move to a new Quests page; Party page retains the listed sections plus a quest summary card linking to Quests.
- All existing actions keep current authorization and freshness guards; no schema change.
- Quest-related links updated to the Quests page.
- Navigation exposes the Quests page; related docs updated.
- Tests cover both pages rendering their sections and at least one guarded quest action still working on the Quests page.

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
