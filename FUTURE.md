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

- Refine header refresh button and sync status layout
    - Description
        - Rework the top header refresh/sync status area so it stays compact, readable, and fully inside the header bar.
        - The Refresh button should not be pushed outside the header.
        - Prefer showing compact sync status inside the header between player info and the Refresh button.
        - During an active refresh, replace the disabled Refresh button with the current refresh status.
        - If this does not fit reliably on supported viewport sizes, fall back to a non-conflicting notification bubble/toast approach.

    - Preferred header layout
        - Keep player info on the left/primary header area.
        - Place compact sync status between player info and the Refresh button.
        - Keep the Refresh button inside the header action area.
        - Make sure all header elements remain vertically aligned.
        - Make sure no header element overflows outside the header bar.
        - Avoid placing sync status in a way that pushes header controls out of the bar.

    - Last refresh / sync status info
        - Minimize the last refresh timestamp.
        - Use compact status-like wording instead of verbose labels.
        - Preferred normal state:
            - `Synced 12:42 PM`
        - If using 24-hour format:
            - `Synced 12:42`
        - Do not show full date, year, seconds, or timezone in the header.
        - If sync data is too old, show a compact stale state.
        - Preferred stale state:
            - `Sync stale`
        - Alternative stale labels:
            - `Data stale`
            - `Refresh needed`
            - `Outdated`
        - Preferred error state:
            - `Sync failed`
        - Use smaller font size and muted/secondary styling for normal synced state.
        - Use warning styling for stale state.
        - Use danger/error styling for failed state.
        - Keep the full timestamp available elsewhere only if needed, such as tooltip/title text.

    - Ongoing refresh state
        - When refresh is in progress, show an ongoing refresh status instead of the Refresh button.
        - This is acceptable because the Refresh button is disabled during refresh anyway.
        - Reuse the existing refresh status labels/states that already describe what is currently refreshing.
        - Do not introduce a generic `Refreshing…` label if a more specific existing status is available.
        - If existing statuses are too long for the header, map them to concise display labels.
        - Suggested compact in-progress labels:
            - `Syncing…`
            - `Syncing tasks…`
            - `Syncing party…`
            - `Syncing inventory…`
        - The ongoing status should occupy roughly the same header space as the Refresh button to avoid layout jumps.
        - The status can include a small spinner/progress indicator if already supported by the app UI.
        - Do not show both a disabled Refresh button and a large sync status if that makes the header crowded.

    - Existing refresh statuses
        - Reuse the existing refresh status labels/states that already describe what is currently refreshing.
        - Do not add a new parallel refresh status system if the app already has refresh status state/data.
        - Show the current specific refresh status in the header action/status area.
        - Replace the disabled Refresh button with the active refresh status while refresh is running.
        - Keep the status compact enough to fit in the header.
        - If there are multiple internal refresh states, map them to concise display labels.
        - Avoid duplicating refresh status text in multiple places at the same time.

    - Stale sync behavior
        - Define a threshold for when synced data should be considered stale.
        - If the existing app already has stale-data logic, reuse it.
        - If no threshold exists, add a reasonable centralized threshold rather than hardcoding it in the header.
        - When data is stale, show `Sync stale` in the compact header sync status.
        - Stale status should encourage refresh without being too visually noisy.
        - Stale status should not replace the Refresh button unless refresh is actively running.
        - User should still be able to click Refresh when sync is stale.

    - Fit check
        - Check whether the compact in-header layout fits on supported desktop and mobile/narrow widths.
        - Verify that player info, compact sync status, and Refresh/current refresh status can coexist without overflow.
        - If it fits:
            - Use the in-header compact sync info layout.
        - If it does not fit:
            - Use a fallback approach where sync info appears outside the header, such as a notification bubble/toast.
            - Keep the Refresh button inside the header regardless.

    - Fallback notification behavior
        - Use this only if compact in-header status does not fit reliably.
        - Sync info can appear as a temporary notification bubble near the top of the app.
        - The bubble should be outside the header bar.
        - It should not push, resize, or conflict with header elements.
        - It should appear when sync state changes or when sync feedback is needed.
        - It should disappear automatically after a short time.
        - Even when this fallback is used, the Refresh button should stay inside the header.

    - Expected behavior
        - Refresh button is always inside the top header bar when refresh is not running.
        - During refresh, the Refresh button area shows the current specific refresh status instead of a disabled Refresh button.
        - Normal last sync state is shown as compact text, such as `Synced 12:42 PM` or `Synced 12:42`.
        - Stale sync state is shown as `Sync stale`.
        - Failed sync state is shown as `Sync failed`.
        - Header sync status uses smaller, less prominent styling in normal state.
        - Header remains stable when refresh starts/finishes.
        - Header elements do not conflict with sync status.
        - No full date/timezone/seconds are shown in the header.
        - Existing refresh statuses are reused instead of creating duplicate status logic.

    - Suggested fix
        - Review the top header component/layout and CSS.
        - Add a compact sync status slot between player info and refresh action.
        - Format last sync time as:
            - `Synced h:mm AM/PM` if the app uses 12-hour time.
            - `Synced HH:mm` if the app uses 24-hour time.
        - Style normal sync info with smaller font and muted color.
        - Style `Sync stale` with warning color.
        - Style `Sync failed` with danger/error color.
        - Replace the disabled Refresh button with the current active refresh status while refresh is running.
        - Reuse existing refresh status state/data.
        - Reuse existing stale-data logic if available.
        - Map long internal statuses to concise display labels only if needed for header fit.
        - Add responsive rules for narrow screens.
        - If narrow layout cannot fit the compact sync info, hide/move sync info to a notification bubble while keeping Refresh inside the header.

    - Acceptance criteria
        - Refresh button stays inside the header bar.
        - Refresh button is replaced by the current specific refresh status while refresh is running.
        - Existing refresh status state/data is reused.
        - No duplicate parallel refresh status system is introduced.
        - Normal sync state is shown as `Synced 12:42 PM` or `Synced 12:42`.
        - Sync timestamp does not include full date, year, seconds, or timezone in the header.
        - Stale sync state is shown as `Sync stale`.
        - Failed sync state is shown as `Sync failed`.
        - Sync status uses smaller, less prominent styling in normal state.
        - Header layout remains stable and aligned on desktop.
        - Header layout remains usable on mobile/narrow widths.
        - Sync status does not push Refresh outside the header.
        - If compact in-header sync info does not fit, fallback notification bubble is used without breaking header layout.

- Simplify Dashboard NAVIGATION companion links
    - Description
        - On the Dashboard, update the NAVIGATION section, specifically the Companion and Habitica links block.
        - Remove the extra Habitica buttons from this block.
        - Keep the main Open Habitica button in the top main Dashboard block.
        - In the NAVIGATION companion links block, rename all remaining buttons to Open.
    - Expected behavior
        - The top main Dashboard block still has its Open Habitica button.
        - The NAVIGATION / Companion and Habitica links block no longer contains separate Habitica buttons.
        - All remaining buttons in that NAVIGATION block use the label Open.
        - The block feels cleaner and avoids repeating Habitica access actions.
    - Suggested fix
        - Locate the Dashboard NAVIGATION section.
        - Remove Habitica-related buttons from the Companion and Habitica links block.
        - Keep only companion/tool navigation items in that block.
        - Change each remaining button label in the block to Open.
        - Verify that the top main Open Habitica button is unchanged.
    - Acceptance criteria
        - Extra Habitica buttons are removed from the Dashboard NAVIGATION companion links block.
        - Top main Open Habitica button remains visible and functional.
        - Remaining buttons in the NAVIGATION block are all labeled Open.
        - No empty spacing or broken layout remains after removing the buttons.

- Companion group bulk planning actions
   - Add bulk planning actions to every COMPANION GROUP section.
   - This includes:
      - COMPANION GROUP - Base collection
      - Other COMPANION GROUP sections
   - Each companion group should have a button for adding all growable missing mounts to the feeding queue.
   - Suggested button labels:
      - Add All to Feeding Queue
      - Plan All Missing Mounts
      - Add Growable Mounts
   - Preferred label:
      - Add All to Feeding Queue
   - The button should find all unavailable/missing mounts inside that companion group.
   - For each unavailable/missing mount, derive the corresponding pet.
   - Add only pets that are:
      - Owned.
      - Available in current user inventory.
      - Feedable.
      - Not special/non-growable.
      - Not already converted into the corresponding mount.
      - Not already present in the feeding queue.
   - Do not add invalid rows for missing pets, special pets, already-owned mounts, or pets that cannot be grown.
   - If no valid pets can be added from the group, the button should be disabled or hidden.
   - The disabled state should explain why no pets can be added.
   - After adding multiple pets, the feeding queue should recalculate food allocation for all queued items.
   - The action should not execute feeding immediately.
   - It should only prepare visible queue rows for user review.

- Hatch planner
   - Add a Hatch Planner with queue behavior similar to the Feed Planner.
   - The Hatch Planner should allow users to prepare a queue of pets to hatch.
   - It should not immediately execute hatch actions without user review.
   - Each queued hatch item should represent one planned pet hatch.
   - Each queued hatch item should show:
      - Pet type / egg.
      - Hatching potion.
      - Whether the pet is already owned.
      - Whether required egg is available.
      - Whether required hatching potion is available.
      - Planned egg consumption.
      - Planned potion consumption.
      - Warning state if resources are unavailable or already reserved by earlier queued items.
   - Each queued hatch item should have a remove button.
   - Removing a hatch item should recalculate reserved eggs and hatching potions for the remaining queue.
   - The Hatch Planner should use the same queue-safety principles as the Feed Planner:
      - Do not overcommit inventory.
      - Earlier queued items reserve resources first.
      - Later queued items only use remaining available resources.
      - If no required resource remains, show warning and set planned consumption to 0 where appropriate.
   - Hatch execution, if implemented, should use existing/current Habitica API flow and preserve user review before mutation.

- Companion group bulk hatching actions
   - Every COMPANION GROUP section should also have a button for adding hatchable missing pets to the Hatch Planner.
   - Suggested button labels:
      - Add All to Hatching Queue
      - Plan All Hatchable Pets
      - Add Hatchable Pets
   - Preferred label:
      - Add All to Hatching Queue
   - The button should be available only if the group contains pets that can currently be hatched.
   - A pet is hatchable when:
      - The pet is not already owned.
      - The required egg is available.
      - The corresponding hatching potion is available.
      - The pet is supported by current catalog/rules data.
      - The pet is not special/unknown/unhatchable.
      - The pet is not already present in the hatching queue.
   - When clicked, add all valid hatchable pets from that companion group to the Hatch Planner queue.
   - Do not add invalid rows for pets without required eggs or potions.
   - If no pets can be hatched from the group, the button should be disabled or hidden.
   - The disabled state should explain why no pets can be added.
   - After adding multiple pets, the hatching queue should recalculate egg and potion allocation for all queued items.

- Scroll stability when adding queue items
   - Fix the current jumpy behavior when adding pets or mounts to a queue.
   - Adding items to the feeding queue makes the queue block larger, which shifts the rest of the UI down.
   - This causes the page to visually jump and makes the user lose their position.
   - When adding items to the feeding queue or hatching queue, preserve the user’s perceived scroll position.
   - Measure the layout offset caused by the queue size change.
   - After adding items, adjust scroll position by the offset value so the visible content does not jump.
   - Apply this behavior for:
      - Adding one pet to the feeding queue.
      - Adding multiple pets through Add All to Feeding Queue.
      - Adding one pet to the hatching queue.
      - Adding multiple pets through Add All to Hatching Queue.
      - Adding pets from missing mount cards.
   - Make the scroll correction smooth enough to feel stable, but avoid animated jumps that feel delayed or distracting.
   - Ensure this works on desktop and mobile/narrow layouts.

- Simplify Dashboard NAVIGATION companion links
    - Description
        - On the Dashboard, update the NAVIGATION section, specifically the Companion and Habitica links block.
        - Remove the extra Habitica buttons from this block.
        - Keep the main Open Habitica button in the top main Dashboard block.
        - In the NAVIGATION companion links block, rename all remaining buttons to Open.
    - Expected behavior
        - The top main Dashboard block still has its Open Habitica button.
        - The NAVIGATION / Companion and Habitica links block no longer contains separate Habitica buttons.
        - All remaining buttons in that NAVIGATION block use the label Open.
        - The block feels cleaner and avoids repeating Habitica access actions.
    - Suggested fix
        - Locate the Dashboard NAVIGATION section.
        - Remove Habitica-related buttons from the Companion and Habitica links block.
        - Keep only companion/tool navigation items in that block.
        - Change each remaining button label in the block to Open.
        - Verify that the top main Open Habitica button is unchanged.
    - Acceptance criteria
        - Extra Habitica buttons are removed from the Dashboard NAVIGATION companion links block.
        - Top main Open Habitica button remains visible and functional.
        - Remaining buttons in the NAVIGATION block are all labeled Open.
        - No empty spacing or broken layout remains after removing the buttons.

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

_No prioritized entries._

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
