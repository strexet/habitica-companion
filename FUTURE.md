# Future Work

Last validated: 2026-06-03.

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
- Dedicated Pets & Mounts page with grouped companion grids, feed queue planner, hatching and equip actions, local fold preferences, and relocated bulk sell planner while keeping per-pet/per-mount maps out of Cloudflare app-data uploads.
- Dashboard gem-for-gold purchase action with visible availability states, no-subscription Subscribe link, quantity clamp, explicit confirmation, sequential stop-on-failure requests, diagnostics, and post-purchase account refresh.
- Party page overview no longer shows the dedicated CRON summary or buff-timing recommendation block, while member review and Quests workspace remain intact.
- Manual task arrangement now persists locally and triggers a narrow encrypted upload of the task-order cloud-sync section without blocking or undoing local reorder changes.
- Random theme generation now guards calm/moderate card text and primary/secondary filled-button label contrast across generated gradient stops, with readability thresholds intentionally relaxed only toward high-chaos Madness output.
- Persisted appearance changes now request a narrow encrypted upload of the color-schemes cloud-sync section, while transient random themes, rerolls, chaos changes, and paste previews stay local until saved; Appearance close actions read `Done` unless they truly discard a preview/edit.
- Healer Blessing estimates now use concise per-member HP copy, keep fresh-HP capping and aggregate scoring, and no longer expose group-total HP wording.

## Pending Queue

### Queued items to be added to `Prioritized Next Changes`

Work top to bottom. This is an intake list for rough notes that must become self-contained `Prioritized Next Changes` entries before implementation. Preserve the `Priority Instructions` and `Entries` structure.

### Priority Instructions

- Top – add to the top of the `Prioritized Next Changes` list (max priority).
- Middle – right after the `Top` entries and before current `Prioritized Next Changes` list items.
- Bottom – (default) the lowest priority entries, add to the bottom of the `Prioritized Next Changes` list.

### Entries:

_None._


## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Sign-In And Theme Contrast Pass

Goal: fix sign-in hero and theme-token contrast so first-run UI remains readable across built-in light/dark themes and mobile widths.

Source finding:
- Deployed mobile audit at `390x844` showed the Gryphy Light sign-in hero as a large pale card with the hero H1 and copy effectively invisible; computed styles showed white hero text on a near-white `card-surface` background.
- Theme switching to dark presets made the hero readable, confirming the issue is the light-theme hero token pairing rather than layout overflow.
- Feature chips and secondary/accent buttons also use pale text on gold/orange accent surfaces in some themes, which can read weakly even when layout is aligned.

Touch:
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.WebApp/Components/ColorSchemePanel.razor` only if preview markup must expose clearer token roles
- `src/Habitica.WebApp/Theme/ColorSchemeService.cs`
- direct tests under `tests/Habitica.WebApp.Tests/Components/ColorSchemePanelTests.cs` or static asset regression tests if contrast helpers exist
- `docs/UX_UI_MANIFEST.md`
- `FEATURES.md` if user-facing theme guarantees change

Out of scope:
- redesigning the sign-in page structure;
- removing built-in schemes;
- changing random theme save/sync behavior;
- adding image assets.

Implementation plan:
- Give sign-in hero text a readable token for light surfaces, or give the hero a deliberately dark/readable treatment when it uses white hero text.
- Normalize feature-chip and accent-button text tokens so filled accent surfaces choose a high-contrast foreground per scheme.
- Add a small contrast guard for built-in scheme surfaces used by hero, feature chips, primary buttons, secondary buttons, and disabled topbar refresh text.
- Recheck Gryphy Light, Gryphy Dark, Toxic Swamp, and Boss Battle on mobile and desktop.
- Keep existing spacing/layout rhythm from the recent UI pass; this task is contrast/readability only unless a contrast fix requires small spacing support.

Acceptance:
- Gryphy Light sign-in H1 and hero copy are readable on mobile and desktop.
- Built-in dark schemes keep readable hero/card/button/chip text.
- Feature chips have sufficient contrast against their background in the audited built-in schemes.
- Disabled Refresh remains visibly disabled but not mistaken for missing text.
- No horizontal overflow appears on sign-in after the contrast changes.

Need to run build:

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet build Habitica.sln -m:1 -nodeReuse:false
```

Need to run test(s): `ColorSchemePanelTests` and static asset regression tests

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter "FullyQualifiedName~ColorSchemePanelTests|FullyQualifiedName~StaticAssetRegressionTests"
```

### Signed-Out Empty-State Actionability

Goal: make direct visits to authenticated pages actionable when the user is signed out or has no saved data.

Source finding:
- Deployed direct-route checks for `/dashboard`, `/tasks`, `/inventory`, `/pets-mounts`, `/party`, `/quests`, and `/spells` render page shells and empty-state copy, but the app bar Refresh button is disabled and there is no visible Sign in action in the content.
- Several empty states say "Sign in or refresh" even though refresh is disabled while signed out, creating a dead-end UX for users who land on a deep link.
- Layout itself did not horizontally overflow at `1280x720`, `820x900`, or `390x844`, so the issue is actionability/copy rather than sizing.

Touch:
- `src/Habitica.WebApp/Pages/DashboardPage.razor`
- `src/Habitica.WebApp/Pages/TasksPage.razor`
- `src/Habitica.WebApp/Pages/InventoryPage.razor`
- `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct page tests under `tests/Habitica.WebApp.Tests/Pages/`
- `FEATURES.md`

Out of scope:
- changing authenticated page data loading;
- showing fake/skeleton data;
- adding drawer navigation while signed out unless required by the empty-state CTA design.

Implementation plan:
- Add a consistent signed-out empty-state action row with `Sign in` linking to `/sign-in` on pages that require cached or authenticated Habitica data.
- Update empty-state copy to stop suggesting Refresh when refresh is unavailable because the user is signed out.
- Keep `Open Saved Data` behavior on sign-in if cached data exists; do not duplicate that behavior in every page unless local cache is present.
- Add a shared helper/component or CSS utility only if it removes repeated markup across the affected pages.
- Verify the action row wraps cleanly on mobile and aligns with each page header/card content.

Acceptance:
- Deep links to authenticated pages expose a clear Sign in action when signed out.
- Empty-state copy accurately names the next available action.
- Refresh still remains disabled while signed out.
- Desktop, tablet, and mobile layouts keep no horizontal overflow.
- Tests cover at least Dashboard, Tasks, Party/Quests, and Pets & Mounts signed-out empty states.

Need to run build:

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet build Habitica.sln -m:1 -nodeReuse:false
```

Need to run test(s): affected signed-out page tests

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter "FullyQualifiedName~DashboardPageTests|FullyQualifiedName~TasksPageTests|FullyQualifiedName~PartyPageTests|FullyQualifiedName~PetsMountsPageTests|FullyQualifiedName~SpellsPageTests|FullyQualifiedName~InventoryPageTests"
```

### Authenticated Foldable UI Review Follow-Up

Goal: repeat the full deployed UI/UX review after sign-in is reliable, then fix authenticated layout issues in the pages and foldable blocks that could not be inspected during the deployed audit.

Source finding:
- Requested deployed audit could not reach authenticated data because sign-in visibly retained credentials but submitted an empty request.
- Unauthenticated route shells and Settings/Diagnostics/Privacy were checked at desktop, tablet, and mobile widths with no obvious horizontal overflow.
- Remaining required surfaces still need real data/foldable inspection: Tasks details, Party member details, active quest details/participants, quest pool, Pets & Mounts groups, Inventory/equipment cards, Spells cards, Dashboard appearance and stat/action blocks, diagnostics filters/results, and color scheme editor under authenticated state.

Touch:
- `src/Habitica.WebApp/Pages/DashboardPage.razor`
- `src/Habitica.WebApp/Pages/TasksPage.razor`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- `src/Habitica.WebApp/Pages/QuestsPage.razor`
- `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/Pages/InventoryPage.razor`
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/Pages/SettingsPage.razor`
- `src/Habitica.WebApp/Pages/LiveTestsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct page tests under `tests/Habitica.WebApp.Tests/Pages/`
- `docs/UX_UI_MANIFEST.md` if shared spacing/alignment rules change

Out of scope:
- changing Habitica data mutations;
- changing formulas or business logic;
- adding new app features beyond layout/UX repair;
- running destructive actions during review.

Implementation plan:
- After the sign-in reliability fix lands, open the deployed app and authenticate with a test or user-approved account.
- Visit Dashboard, Tasks, Party, Quests, Pets & Mounts, Inventory, Spells, Settings, Diagnostics, and Privacy at `1280x720`, `820x900`, and `390x844`.
- Expand all foldable UI available in that state: task details, party member details, active quest details/rewards, active quest participants, quest pool, companion groups, appearance/custom scheme details, diagnostics filters/results, and any card-level Details buttons.
- Only use non-mutating interactions: navigation, filters, sort controls, theme changes, Details toggles, folds, and safe read-only panels.
- Record and fix alignment, spacing, wrapping, card footer drift, long-label/value alignment, button/input height mismatches, and contrast/readability issues.

Acceptance:
- Authenticated pages have no obvious unintended stair-step layouts or card action drift.
- Task details and party member details expand without broken grids, cramped rows, or horizontal overflow.
- Quest, companion, inventory, spell, diagnostics, and appearance controls wrap cleanly on mobile.
- Header/description/control/list spacing is consistent across authenticated pages.
- Theme switching does not hide important controls or text.
- Follow-up findings are either fixed in the same pass or added back to `FUTURE.md` as scoped implementation entries.

Need to run build:

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet build Habitica.sln -m:1 -nodeReuse:false
```

Need to run test(s): affected web app page tests

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false
```

### Pets And Mounts Pet Card Growth Progress

Goal: show each owned, feedable pet's progress toward becoming a mount and summarize how much more food is needed using the local feeding rules model.

Touch:
- `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.Rules/Pets`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if companion-card display guidance changes

Out of scope:
- adding missing-mount "Plan to grow" buttons;
- adding type filters;
- changing feed queue execution or API calls;
- showing exact unavailable progress for unknown/special pets unless the rules model supports it.

Implementation plan:
- Build per-card growth summaries from cached `snapshot.Inventory.Pets`, `snapshot.Inventory.Mounts`, `snapshot.Inventory.Food`, and `PetsMountsCatalog`.
- Render a compact progress indicator on owned normal pet cards: current progress percentage, remaining percentage, and mount-ready/already-owned state.
- Show a short food-needed line based on the best available plan, such as favorite-food count when enough favorite food exists or a mixed-food count when alternatives are needed.
- Keep unowned pet cards focused on hatching requirements and avoid implying feed progress before the pet exists.
- For owned pets that cannot grow into a normal mount, show a concise unavailable state instead of a misleading progress number.
- Keep existing feed selection controls available; selecting a pet for feed should keep using the current dropdown ordered by highest growth value for that pet.
- Preserve offline cached behavior: growth summaries should render from local snapshots and avoid live calls.

Acceptance:
- Owned feedable pet cards show current progress and remaining progress toward mount conversion.
- Food-needed copy uses favorite food when available and accounts for alternative available food when favorite food is insufficient or absent.
- Already mount-complete or already-owned-mount states do not prompt unnecessary feeding.
- Unowned pets, unknown owned pets, and special/non-growable entries render without broken progress UI.
- Existing hatch, equip, feed preview, fold, search, and bulk-sell controls still render.
- Component tests cover owned partial-progress pet, enough favorite food, fallback food, already-owned mount, and unowned pet card states.

Need to run build:

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet build Habitica.sln -m:1 -nodeReuse:false
```

Need to run test(s): `PetsMountsPageTests` and pet rules tests

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~PetsMountsPageTests
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Rules.Tests/Habitica.Rules.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~Pets
```

### Pets And Mounts Missing-Mount Growth Planning

Goal: let users turn a missing mount card into an actionable feed plan for the corresponding owned pet.

Touch:
- `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.Rules/Pets`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if companion action guidance changes

Out of scope:
- auto-executing feed requests from a mount card;
- planning growth for mounts whose corresponding pet is missing, special, wacky, or already converted;
- changing feed queue validation in `AppSessionController`;
- changing Habitica endpoint contracts.

Implementation plan:
- For each missing normal mount, derive the corresponding pet key from the mount key and look up current pet ownership/progress.
- Add a `Plan to grow` action on missing mount cards only when the corresponding pet exists, is feedable, and the mount is not already owned.
- On click, select that pet in the existing feed planner and enqueue or stage the smallest calculated feed plan based on current progress and available food.
- Use the rules model's food plan so favorite food is preferred and available alternatives are included only when needed.
- If the pet is unavailable or cannot be grown, render a concise disabled/unavailable reason near the missing mount card.
- Keep user review before mutation: generated queue rows should still be visible in the feed planner and require the existing `Feed queued items` action.
- Preserve stale-data and busy-state guards already used by feed and equip actions.

Acceptance:
- Missing mount cards show `Plan to grow` when the corresponding owned pet can be fed toward that mount.
- Clicking `Plan to grow` selects the matching pet and prepares visible feed-plan rows with the calculated food amounts.
- Planned rows use highest-value food first and include lower-value food only when needed and available.
- Missing pet, non-growable/special, already-owned mount, no-food, busy, and stale states do not produce invalid queued feed requests.
- Existing feed queue clear and execution behavior remains unchanged.
- Component tests cover successful planning, unavailable corresponding pet, insufficient/no food messaging, and generated queue execution handoff.

Need to run build:

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet build Habitica.sln -m:1 -nodeReuse:false
```

Need to run test(s): `PetsMountsPageTests` and pet rules tests

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~PetsMountsPageTests
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Rules.Tests/Habitica.Rules.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~Pets
```

### Pets And Mounts Creature Type Filters

Goal: add creature-type filters to pet and mount sections so users can narrow large companion collections by public creature names such as Wolf, Fox, Cactus, Dragon, Tiger Cub, Panda Cub, Lion Cub, and Flying Pig.

Touch:
- `src/Habitica.Domain/User/PetsMountsCatalog.cs`
- `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`
- direct catalog/rules tests under `tests/Habitica.Rules.Tests/` or `tests/Habitica.Domain.Tests/` if helper logic moves out of Razor
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if shared filter-control guidance changes

Out of scope:
- replacing existing search;
- changing collection grouping or fold persistence semantics beyond expanding visible matches;
- filtering hatching potions or bulk-sell rows;
- changing catalog membership without source-backed data.

Implementation plan:
- Add or expose creature type metadata from `PetCatalogItem.EggKey` and the corresponding mount key, using readable display names from `PetsMountsCatalog.ToReadableName`.
- Build filter options from catalog entries plus unknown owned entries where a readable type can be safely derived.
- Add independent pet and mount type filters near the relevant section controls, with `All types` reset options.
- Apply filters together with existing search and group folds: search text and selected type should both narrow visible cards.
- Ensure filters use display names for labels while preserving canonical keys internally.
- Reset or keep filters predictably when search changes; avoid hidden selected state that leaves all groups empty without a clear reset path.
- Keep mobile layout compact and avoid adding per-card repeated filter labels.

Acceptance:
- Pet sections can be filtered by creature type and reset to all types.
- Mount sections can be filtered by creature type and reset to all types.
- Filtering by `Wolf`, `Fox`, `Cactus`, `Dragon`, `Tiger Cub`, `Panda Cub`, `Lion Cub`, and `Flying Pig` shows only matching pet or mount cards.
- Type filters compose with existing text search and folded groups without hiding matching results unexpectedly.
- Empty filtered states explain that no companions match the selected type/search.
- Component tests cover pet filter, mount filter, reset behavior, search composition, and a multi-word creature display name.

Need to run build:

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet build Habitica.sln -m:1 -nodeReuse:false
```

Need to run test(s): `PetsMountsPageTests` and catalog/type helper tests

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~PetsMountsPageTests
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Domain.Tests/Habitica.Domain.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~PetsMounts
```

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
