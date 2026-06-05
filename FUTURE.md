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

_No pending entries._

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Dashboard Navigation Companion Link Cleanup

Goal: simplify the Dashboard `NAVIGATION` block by removing repeated Habitica access buttons while preserving the primary Dashboard `Open Habitica` action.

Touch:
- `src/Habitica.WebApp/Pages/DashboardPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css` only if removing buttons leaves spacing or alignment gaps
- `tests/Habitica.WebApp.Tests/Pages/DashboardPageTests.cs`
- `FEATURES.md`

Out of scope:
- changing the top hero/main Dashboard `Open Habitica` link;
- changing routes for companion pages;
- changing navigation drawer behavior;
- redesigning Dashboard page structure beyond this link block.

Implementation plan:
1. Locate the Dashboard `NAVIGATION` section and the `Companion and Habitica links` card/grid.
2. Identify all Habitica-specific buttons inside that block.
3. Remove the extra Habitica buttons from that block only.
4. Keep companion/tool navigation cards or rows that open app pages such as Tasks, Party, Quests, Pets & Mounts, Inventory, Spells, Settings, or diagnostics.
5. Rename every remaining button in that block to `Open`.
6. Verify the main Dashboard account/top block still contains the primary `Open Habitica` action with existing URL behavior.
7. Clean up any empty columns, gaps, or awkward card footers caused by removing the extra buttons.

UX details:
- The navigation block should read as app navigation, not a mixed list of repeated external Habitica links.
- All remaining action labels in the block should be visually consistent.
- Cards/rows should keep aligned titles, descriptions, and action rows after button removal.

Tests:
- Update Dashboard render test that currently asserts `Companion and Habitica links`.
- Assert the top/main `Open Habitica` action remains present.
- Assert the navigation block no longer renders extra Habitica-specific actions.
- Assert remaining navigation-block buttons use `Open`.

Acceptance:
- Extra Habitica buttons are removed from the Dashboard navigation companion links block.
- Top/main `Open Habitica` button remains visible and functional.
- Remaining buttons in the navigation block are all labeled `Open`.
- No empty spacing, broken grid item, or uneven action row remains.

### Companion Group Bulk Feed Planning Actions

Goal: add per-companion-group bulk actions that enqueue all currently growable missing mounts into the existing Feed Planner without executing any feeding.

Touch:
- `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.Rules/Pets/PetGrowthPlanFactory.cs` only if candidate/growth eligibility logic should move out of Razor
- `src/Habitica.Rules/Pets/PetFeedRecommendationFactory.cs` only if food availability helpers need reuse
- `src/Habitica.Domain/User/PetsMountsCatalog.cs` only if catalog helpers are missing for pet/mount pairing
- `tests/Habitica.Rules.Tests/Pets/PetGrowthPlanFactoryTests.cs` if rule helpers change
- `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if group action layout guidance changes

Out of scope:
- executing feed actions from the group button;
- changing Feed Planner execution semantics;
- adding hatching behavior;
- adding support for special/non-growable pets beyond documented catalog rules;
- changing Habitica API requests.

Implementation plan:
1. Read current Pets & Mounts group rendering and existing single-card `Plan feed` / `Plan to grow` logic.
2. Create a reusable helper that returns valid feed-queue candidates for a companion group.
3. Candidate rules:
   - mount belongs to the current group;
   - mount is not already owned;
   - corresponding pet key can be derived from catalog data;
   - corresponding pet is owned and has a positive growable progress state;
   - pet/mount is not special, unknown, or non-growable;
   - pet is not already queued;
   - corresponding mount is not already available;
   - at least one usable normal food option exists, or current Feed Planner can show a valid warning row if food is unavailable.
4. Add a group header action labeled `Add All to Feeding Queue`.
5. Prefer a disabled button with a concise reason when no candidates exist; hide only if the group header becomes too crowded on mobile.
6. On click, append all valid group candidates to `_feedQueue` in stable catalog order.
7. Preserve any existing queued item food choices.
8. For new items, select the same default food choice used by single `Plan feed` behavior.
9. After adding items, let the existing feed allocation calculation recalculate reserved food and warnings across the whole queue.
10. Ensure folded group behavior remains intact; adding from a group should not require unrelated groups to open.
11. Keep all action labels short and aligned with existing card/grid spacing.

UX details:
- Button belongs in the companion group header/action row, aligned with collapse/filter controls.
- Disabled reason should be short, such as `No growable mounts`.
- Adding multiple items should make queue contents visible enough for review before mutation.
- No feed action should be sent until user explicitly uses Feed Planner execution controls.

Tests:
- Group with two missing mounts backed by owned growable pets adds both to queue.
- Already-owned mount is skipped.
- Missing pet is skipped.
- Special/non-growable pet is skipped.
- Already queued pet is skipped.
- Button disabled/reason rendered when no valid candidates exist.
- Existing queue allocation updates after bulk add.
- Single-card planning still works.

Acceptance:
- Every companion group exposes `Add All to Feeding Queue` when valid candidates exist.
- Bulk action adds only valid owned growable pets for missing mounts in that group.
- Invalid pets/mounts never create queue rows.
- Already queued pets are not duplicated.
- Feed queue recalculates food allocation and warnings after bulk add.
- Bulk action performs no Habitica mutation.
- Desktop and mobile group headers remain aligned and readable.

### Hatch Planner Queue MVP

Goal: add a Hatch Planner on Pets & Mounts that lets users queue pet hatches, review required eggs and hatching potions, and execute only after explicit confirmation.

Touch:
- `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.Rules/Pets` for a hatch queue/allocation helper if allocation logic should be testable outside Razor
- `src/Habitica.Domain/User/PetsMountsCatalog.cs` only if catalog helpers are missing for egg/potion/pet lookup
- `src/Habitica.WebApp/State/AppSessionController.cs` and `src/Habitica.WebApp/State/IAppSessionController.cs` only if a batch hatch helper is needed instead of calling existing `HatchPetAsync`
- `tests/Habitica.Rules.Tests/Pets` if rule helpers are added
- `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`
- `tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs` only if session controller API changes
- `FEATURES.md`
- `HABITICA_API.md` only if hatch endpoint assumptions are corrected
- `docs/UX_UI_MANIFEST.md` if queue/card guidance changes

Out of scope:
- group-level bulk hatching actions, which are covered by the next task;
- changing existing single pet hatch API behavior;
- hatching unknown/special pets not supported by catalog data;
- auto-hatching immediately after a card button click;
- changing feed queue semantics.

Implementation plan:
1. Model hatch queue entries as `(EggKey, HatchingPotionKey)` or equivalent stable catalog keys.
2. Add a hatch allocation helper that walks queued entries in order and reserves eggs and hatching potions.
3. Allocation rules:
   - earlier queue entries reserve resources first;
   - later entries only consume remaining egg/potion counts;
   - already-owned pets are warning/invalid rows and should not consume resources;
   - missing egg sets planned egg consumption to `0` and warns;
   - missing potion sets planned potion consumption to `0` and warns;
   - unsupported/special/unhatchable catalog entries are invalid and warn;
   - duplicate queue entries are blocked or skipped.
4. Add a `Hatch planner` / `Hatch queue` section near the existing Feed Planner.
5. Empty state should tell users to choose hatch actions from companion cards.
6. Each queue card should show:
   - pet display name;
   - egg type;
   - hatching potion;
   - owned/not owned status;
   - egg available/reserved count;
   - potion available/reserved count;
   - planned egg consumption;
   - planned potion consumption;
   - warnings for unavailable, owned, duplicate, or reserved resources;
   - Remove action.
7. Add explicit queue execution controls:
   - `Hatch queued pets` disabled unless at least one queued row is executable;
   - `Clear queue`;
   - inline confirmation before mutation, matching Feed Planner safety.
8. Execution should call the existing/current Habitica hatch flow for each executable item in queue order, stop on first failure, show result feedback, and refresh/update local account data through existing mutation refresh behavior.
9. Removing or clearing queue items recalculates allocation for remaining rows.
10. Single-card hatch actions should become planner actions where appropriate, or existing immediate hatch buttons should keep confirmation and not bypass review.
11. Keep queue rows themed with semantic tokens and responsive spacing.

UX details:
- Hatch Planner should visually match Feed Planner rhythm but remain distinct.
- Resource chips should align with Feed Planner count pills.
- Warning copy should be concise and local to the affected queue row.
- Execution controls should sit at the bottom of the queue and align with Feed Planner actions.

Tests:
- Empty Hatch Planner renders empty state.
- Adding hatchable pet creates one queue row with egg and potion counts.
- Already-owned pet row is blocked or skipped according to chosen UX.
- Missing egg warns and cannot execute.
- Missing potion warns and cannot execute.
- Two queued pets sharing one egg reserve first item and warn on later item.
- Remove recalculates allocation.
- Clear removes all rows.
- Confirmed execution calls existing hatch action with expected egg/potion keys.
- Execution stops on failure if multiple queued rows exist.

Acceptance:
- Users can prepare hatch queue rows without immediate mutation.
- Queue rows show pet, egg, potion, ownership, resource availability, planned consumption, and warnings.
- Queue allocation never overcommits eggs or hatching potions.
- Earlier queued items reserve resources before later items.
- Remove and clear recalculate remaining allocation.
- Hatch execution requires explicit confirmation.
- Existing Habitica hatch API flow is reused.
- Desktop and mobile layouts stay aligned with no horizontal overflow.

### Companion Group Bulk Hatch Planning Actions

Goal: add per-companion-group bulk actions that enqueue all currently hatchable missing pets into the Hatch Planner without executing hatches.

Depends on:
- `Hatch Planner Queue MVP`

Touch:
- `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.Rules/Pets` hatch candidate/allocation helpers if added by the Hatch Planner task
- `src/Habitica.Domain/User/PetsMountsCatalog.cs` only if group/pet lookup helpers are missing
- `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`
- `tests/Habitica.Rules.Tests/Pets` if rule helpers change
- `FEATURES.md`

Out of scope:
- executing hatches directly from group buttons;
- adding feed-queue behavior;
- queueing pets without required egg or potion;
- changing hatch endpoint behavior.

Implementation plan:
1. Reuse Hatch Planner candidate and allocation helpers from the previous task.
2. Add a group header action labeled `Add All to Hatching Queue`.
3. Candidate rules:
   - pet belongs to the current companion group;
   - pet is not already owned;
   - required egg exists in current inventory;
   - required hatching potion exists in current inventory;
   - pet is supported by catalog/rules data;
   - pet is not special, unknown, or unhatchable;
   - pet is not already in the hatching queue.
4. Add valid candidates in stable catalog order.
5. Skip invalid pets silently from queue insertion, but expose a disabled reason when the whole group has no candidates.
6. After bulk add, recalculate egg/potion allocation for the entire hatch queue.
7. Keep group header actions aligned with `Add All to Feeding Queue`; if both buttons render together, use consistent sizing and wrapping.

UX details:
- Preferred label is `Add All to Hatching Queue`.
- Disabled reason should be concise, such as `No hatchable pets`.
- Bulk hatching action should feel like preparation, not execution.
- On mobile, buttons may wrap, but should not create stair-step misalignment or overflow.

Tests:
- Group with two hatchable missing pets adds both queue rows.
- Owned pet is skipped.
- Pet missing egg is skipped.
- Pet missing potion is skipped.
- Special/unhatchable pet is skipped.
- Already queued pet is skipped.
- Disabled state renders when no hatchable pets exist.
- Allocation recalculates when multiple queued pets share eggs or potions.

Acceptance:
- Every companion group exposes `Add All to Hatching Queue` when valid candidates exist.
- Bulk action only queues hatchable, missing, supported pets with required resources.
- Invalid pets do not create queue rows.
- Existing hatch queue entries are not duplicated.
- Hatch queue allocation recalculates after bulk add.
- Bulk action performs no Habitica mutation.
- Group header actions remain aligned on desktop and mobile.

### Queue Add Scroll Stability

Goal: preserve the user's perceived scroll position when adding feed or hatch queue items expands planner blocks above the current content.

Depends on:
- `Companion Group Bulk Feed Planning Actions`
- `Hatch Planner Queue MVP`
- `Companion Group Bulk Hatch Planning Actions`

Touch:
- `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.WebApp/wwwroot/js/petsMountsPage.js` if JS interop is needed
- `src/Habitica.WebApp/wwwroot/index.html` if a new JS module/script is added
- `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`
- `docs/UX_UI_MANIFEST.md` if shared scroll-stability guidance is added

Out of scope:
- virtualizing the companion grid;
- changing queue candidate eligibility;
- adding new mutation behavior;
- animated page transitions beyond minimal scroll correction.

Implementation plan:
1. Identify all queue-add entry points:
   - single pet `Plan feed`;
   - missing mount `Plan to grow`;
   - group `Add All to Feeding Queue`;
   - single pet hatch queue action;
   - group `Add All to Hatching Queue`.
2. Before a queue-add action changes state, capture a stable anchor:
   - preferred: bounding rect top of the clicked card/group header or nearest planner-independent content anchor;
   - fallback: current `window.scrollY`.
3. Apply the queue state change.
4. After render, measure the same anchor's new top and adjust scroll by the delta so the anchor remains in the same viewport position.
5. Use immediate or near-immediate correction; avoid slow smooth scrolling that makes the page feel delayed.
6. Keep correction local to queue-add actions only. Removing/clearing queue items should not surprise-scroll unless testing shows a similar issue.
7. Ensure the behavior is safe when the anchor disappears due to filtering, folding, or route change.
8. Prefer a small JS interop helper for DOM measurement and scroll adjustment if CSS alone cannot solve the issue.
9. Guard JS calls so prerender/test environments do not fail.

UX details:
- Adding an item should keep the card or group the user clicked visually stable.
- Queue growth should not make the page jump downward and lose context.
- Desktop and mobile should behave consistently.
- Scroll correction should not fight user scrolling if the user starts another interaction quickly.

Tests:
- Component tests verify each queue-add path calls the scroll-stability flow or marks the pending correction state.
- Existing queue add/remove tests still pass.
- Manual browser verification required at desktop and mobile widths because real scroll measurement is browser-owned.

Acceptance:
- Adding one feed item preserves visible position.
- Adding multiple feed items preserves visible position.
- Adding one hatch item preserves visible position.
- Adding multiple hatch items preserves visible position.
- Missing mount planning preserves visible position.
- Scroll correction does not run on unrelated interactions.
- Behavior works on desktop and mobile/narrow layouts.

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
