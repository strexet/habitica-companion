# Future Work

Last validated: 2026-06-08.

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
- Party page active quest metadata/rewards, member CRON graph, shared quest pool, queue, voting, recent completions, owner/admin/Officer controls, and quest start action.
- Dashboard pending damage estimate, knockout warning, and manual health-potion purchase action.
- Split-key encrypted Cloudflare app-data sync, legacy single-blob restore fallback, per-section payload guard, partial-success sync behavior, and refresh coordinator deduplication.
- Refresh-domain invalidation basics after implemented mutations.
- Staged sign-in refresh UX, scoped refresh indicators, per-section cloud sync status, sync exclusions, and explicit cloud-sync conflict choices.
- Two-handed weapon awareness in spell equipment recommendations: weapon/shield selected as a `twoHanded`-aware pair; shield omitted when the two-handed weapon outscores the best one-handed + shield combination.
- Task history statistics, aggregate history charts, expanded-card per-task charts, manual task ordering, drag handles, keyboard reordering, and move buttons.
- Inventory equipment optimizer with goal selector, before/after stat deltas, recommendation equip/save actions, and two-handed weapon handling.
- Bulk sell planner for eggs, food, and hatching potions with safe surplus preview, explicit confirmation, sequential sell execution, diagnostics, and post-sell refresh.
- Dashboard Start New Day default-enabled temporary gear optimization: compact INT-for-mana and CON/survival preview, sequential equip-before-CRON execution, and previous-battle-gear restoration after CRON.
- Dashboard and Spells CRON blocks expose compact due unfinished-Daily mini lists with guarded inline completion.
- Spells sticky current-mana bar with available MP, max MP, class, and persistent scroll visibility above spell cards.
- App color scheme system with centralized semantic tokens, Alpha/Habitica/Gryphy built-in schemes, Settings picker, custom editable schemes, shell/button/disabled/input theming, mobile localStorage fallback, fast local reload persistence, and portable sync storage.
- Random color scheme controls: shared color-scheme panel on Settings, Dashboard, and Sign-in, random-preset pick from built-in plus custom schemes, fully-random theme generation with a chaos slider (Calm to Madness) scaling hue/saturation divergence, held as a session-only pending theme (selectable via a "Generated" dropdown entry, applied without persisting), naming/saving the last random theme into custom schemes, and copy/paste of presets for building fully custom palettes.
- Quests-page quest pool expanded by default with an in-memory manual collapse control.
- Party-page combined summary and bottom-grouped sync administration; active quests compact participant and unavailable-finish-estimate rendering.
- Quest-pool search by public reward display name, including partial case-insensitive matches.
- Active-quest owner/starter and started-at metadata with shared-queue fallback, unavailable states, and foldable details/rewards and participant-name drill-ins.
- Post-CRON party refresh now saves enriched party snapshots with CRON history, member average timing, active-quest progress, and completion estimates before reloading state, so `/quests` can show finish predictions immediately after Start New Day.
- Incoming damage prediction now lives only inside the Dashboard Start New Day panel, uses confirmed-due unfinished Dailies for numeric totals, separates unknown due-state Dailies into collapsed details, includes active boss pending damage once for current-user CRON risk, and keeps the standalone Dashboard damage card removed.
- Dedicated Pets & Mounts page with grouped companion grids, feed queue planner, hatching and equip actions, local fold preferences, and relocated bulk sell planner while keeping per-pet/per-mount maps out of Cloudflare app-data uploads.
- Dashboard gem-for-gold purchase action with visible availability states, no-subscription Subscribe link, quantity clamp, explicit confirmation, sequential stop-on-failure requests, diagnostics, and post-purchase account refresh.
- Party page overview no longer shows the buff-timing recommendation block, while member review, CRON graph context, and Quests workspace remain intact.
- Manual task arrangement now persists locally and triggers a narrow encrypted upload of the task-order cloud-sync section without blocking or undoing local reorder changes.
- Random theme generation now guards calm/moderate card text and primary/secondary filled-button label contrast across generated gradient stops, with readability thresholds intentionally relaxed only toward high-chaos Madness output.
- Persisted appearance changes now request a narrow encrypted upload of the color-schemes cloud-sync section, while transient random themes, rerolls, chaos changes, and paste previews stay local until saved; Appearance close actions read `Done` unless they truly discard a preview/edit.
- Healer Blessing estimates now use concise per-member HP copy, keep fresh-HP capping and aggregate scoring, and no longer expose group-total HP wording.
- Inventory page user-facing labels now read Equipment across drawer navigation, page title, dashboard link, tests, and user-facing docs while preserving the `/inventory` route and inventory data/API terminology.
- Spell cards now support Spend All Mana count planning, a cancellable one-second Preparing stage, card-local Cancel for active cast runs, cancellation-aware sequential spell execution, and a 350 ms default Habitica API minimum request spacing without adding an appsettings override.
- Party member status now has local display-name search that trims whitespace, matches partial names case-insensitively, composes with class filtering and sort order, shows a clearable no-match state, and avoids API requests while typing.

## Pending Queue

### Queued items to be added to `Prioritized Next Changes`

Work top to bottom. This is an intake list for rough notes that must become self-contained `Prioritized Next Changes` entries before implementation. Preserve the `Priority Instructions` and `Entries` structure.

### Priority Instructions

- Top – add to the top of the `Prioritized Next Changes` list (max priority).
- Middle – right after the `Top` entries and before current `Prioritized Next Changes` list items.
- Bottom – (default) the lowest priority entries, add to the bottom of the `Prioritized Next Changes` list.

### Entries:

_No pending entries._

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Dashboard Mobile Habitica Link Placement

Priority: Bottom intake item, currently first because no higher-priority entries exist.

Goal: move the primary external `Open Habitica` Dashboard action into the Dashboard `NAVIGATION / Companion links` section on mobile and narrow viewports while leaving desktop placement unchanged.

Touch:
- `src/Habitica.WebApp/Pages/DashboardPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct Dashboard tests under `tests/Habitica.WebApp.Tests/Pages/DashboardPageTests.cs`
- `FEATURES.md` if Dashboard responsive behavior is documented there
- `docs/UX_UI_MANIFEST.md` if Dashboard responsive hierarchy guidance changes

Current context:
- Dashboard companion navigation cards use local app navigation with `Open` actions.
- Dashboard currently has one primary external `Open Habitica` web link in the top Dashboard block.
- Habitica links must remain stable web URLs; do not add `habitica://`, Android `intent://`, app-opening probes, or mobile-specific deep links.
- The Habitica action must stay visually distinct from local companion-page `Open` actions.

Required behavior:
- Desktop and wide tablet:
  - Keep the existing top Dashboard placement unchanged.
  - Do not add an `Open Habitica` action to the `NAVIGATION / Companion links` section.
  - Preserve current hierarchy, spacing, alignment, target, `rel`, and external-link behavior.
- Mobile and narrow layouts:
  - Hide or move the top-block `Open Habitica` action so it is not visible or focusable there.
  - Render the same external `Open Habitica` action inside the `NAVIGATION / Companion links` section.
  - Place it after local companion navigation entries.
  - Keep label exactly `Open Habitica`; local companion actions remain `Open`.
  - Ensure exactly one visible and keyboard-focusable `Open Habitica` action exists at a given viewport width.

Implementation plan:
- Inspect Dashboard markup to identify the existing top `Open Habitica` action, href, target, rel, and styling hooks.
- Prefer a shared render fragment, shared constants, or shared action definition so the external URL and link attributes cannot drift between desktop and mobile placements.
- Use established responsive breakpoint conventions from `app.css`.
- If two responsive render locations are used, ensure CSS hides the inactive placement from pointer and keyboard interaction, not only visually.
- Keep the mobile action inside the existing companion-links section boundary without adding a large standalone card.
- Add spacing that separates the external Habitica action from local page links without creating excessive vertical gaps.
- Verify the action fits full-width mobile layouts and remains readable across theme tokens.

Out of scope:
- Changing the Habitica URL.
- Adding mobile app deep links or custom URL schemes.
- Renaming local companion navigation actions.
- Reworking Dashboard navigation cards beyond the placement required here.
- Changing desktop Dashboard hierarchy.

Acceptance:
- Desktop `Open Habitica` placement is unchanged.
- Mobile `Open Habitica` appears at the bottom of `NAVIGATION / Companion links`.
- Only one `Open Habitica` action is visible and focusable per viewport size.
- Companion page buttons remain labeled `Open`.
- External link destination, `target`, `rel`, and safety behavior remain unchanged.
- Mobile layout has no overflow, stair-step alignment, duplicate spacing, or touch-target regression.
- Tests cover desktop and mobile responsive visibility/placement behavior.

Tests:
- Add/update Dashboard component tests for the desktop render: top-block `Open Habitica` present and companion-section `Open Habitica` not active for desktop-visible markup/classes.
- Add/update Dashboard component tests for mobile render/classes: mobile companion-section `Open Habitica` present and top placement hidden by the responsive class contract.
- Add assertions that local companion actions still read `Open`.
- Add assertions that both responsive instances, if present in markup, share href, target, and rel and only one is intended to be focusable at a time by class/attribute contract.

### Equipment Navigation Battle Icon

Priority: Bottom intake item, placed after Dashboard mobile Habitica link placement.

Goal: replace the user-facing Equipment side-menu icon with a battle-equipment icon that better matches equipped gear, loadouts, stat comparison, and equipment optimization.

Touch:
- `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor`
- direct navigation tests under `tests/Habitica.WebApp.Tests/AppNavMenuTests.cs` if icon values or rendered menu entries are asserted
- `FEATURES.md` only if user-facing navigation documentation explicitly names the old icon
- `docs/UX_UI_MANIFEST.md` only if navigation icon guidance changes

Preferred icon:
- `@Icons.Material.Outlined.Shield`

Fallback icons, only if the preferred icon is unavailable in the installed MudBlazor icon set:
- a sword or crossed-weapons Material icon
- an armor, vest, or helmet Material icon
- another outlined combat/equipment icon that is unambiguous at drawer icon size

Current context:
- The route and page label are already `Equipment`.
- The page is focused on battle gear, equipped slots, gear stats, loadouts, presets, optimization, and comparing/equipping items.
- A shield communicates battle equipment better than a generic storage, box, cart, backpack, settings, or tools icon.
- Existing side-menu icons use MudBlazor/Material icon values; keep that style.

Required behavior:
- Update only the Equipment navigation icon.
- Keep unchanged:
  - label `Equipment`
  - route/navigation destination
  - drawer order
  - active-link behavior
  - authentication visibility
  - click/tap behavior
  - row size, alignment, spacing, hover/focus/active theme behavior
- The visible text label remains the accessible name; do not add redundant screen-reader text for the icon.
- Treat the icon as decorative when adjacent visible text already names the destination.

Implementation plan:
- Inspect `AppNavMenu.razor` and identify the Equipment entry.
- Confirm whether `Icons.Material.Outlined.Shield` is available in the installed MudBlazor version by checking existing package/API usage or compile-time known icon namespace conventions.
- Replace only the Equipment icon value.
- If the preferred shield value is unavailable, choose the closest supported outlined battle-equipment icon and document the reason in the implementation summary.
- Check neighboring nav entries to avoid reusing an icon already assigned to another primary destination.
- Review tests for assumptions about icon strings, rendered SVG, nav row count, labels, or routes; update only direct expectations affected by the icon swap.

Out of scope:
- Changing the `Equipment` label.
- Changing the `/inventory` route or any route alias.
- Changing drawer ordering or visibility rules.
- Adding remote Habitica artwork, custom fantasy art, images, emoji, text glyphs, or new icon libraries.
- Renaming domain/API concepts from inventory to equipment outside this nav icon.

Acceptance:
- Equipment side-menu entry uses an outlined shield or another clearly battle-oriented equipment icon.
- The icon remains visually consistent with MudBlazor/Material navigation icons.
- Label remains `Equipment`.
- Route remains unchanged.
- Drawer order remains unchanged.
- Active, hover, focus, expanded desktop drawer, narrow/mobile drawer, and theme states remain aligned with neighboring entries.
- No row-height, spacing, alignment, accessibility, or layout regression is introduced.

Tests:
- Update `AppNavMenuTests` if they assert the Equipment icon value.
- Keep or add assertions that the Equipment label and route still render.
- Add a narrow assertion for the new icon value only if existing tests already validate icon values for nav entries.
- Do not add broad snapshot-style tests for all generated SVG paths.

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
