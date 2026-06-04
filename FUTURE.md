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

- Top. Merge all pet related tasks from the Prioritized Next Changes list with this (create one unified Pet & Mounts task):
  - Rework Pet & Mounts growth progress, missing-mount planning, feed queue, saddle flow, and creature type filters
  - Description
     - Merge all related Pet & Mounts tasks into one combined update.
     - This task replaces/absorbs the following tasks from the Prioritized Next Changes list:
        - Pets And Mounts Pet Card Growth Progress
        - Pets And Mounts Missing-Mount Growth Planning
        - Pets And Mounts Creature Type Filters
     - Do not implement these as separate parallel features.
     - Treat them as one unified Pet & Mounts page upgrade covering:
        - Pet card growth progress.
        - Food-needed summaries.
        - Missing mount growth planning.
        - Feed planner queue.
        - Best-food recommendation.
        - Saddle flow.
        - Creature type filters.
     - Avoid duplicate UI blocks, duplicate calculations, duplicate queue models, and duplicate helper logic.
     - Preserve offline cached behavior wherever possible.
     - Avoid live calls for passive progress/summary rendering.
     - Keep user review before any mutation/API-consuming action.
  - Main goal
     - Improve the Pet & Mounts page so users can understand pet growth progress, plan missing mounts, queue feeding actions safely, use saddles intentionally, and filter large pet/mount collections by creature type.
     - The page should clearly answer:
        - Which owned pets can still become mounts.
        - How close each pet is to becoming a mount.
        - How much more food is needed.
        - Which food is best to use.
        - Whether planned feeding will consume available food correctly.
        - Which missing mounts can be grown from currently owned pets.
        - Which pets/mounts belong to a selected creature type.
  - Very important merge instruction
     - Merge this task with all related Prioritized Next Changes items listed above.
     - Reconcile overlapping requirements before implementation.
     - Use one shared rules/calculation model for:
        - Pet progress.
        - Favorite/recommended food.
        - Food growth value.
        - Remaining food requirement.
        - Missing mount to corresponding pet mapping.
        - Feed queue allocation.
        - Creature type extraction.
        - Saddle availability.
     - Keep UI consistent between pet cards, missing mount cards, and feed planner queue cards.
     - Do not create a separate “pet growth progress” implementation that is disconnected from the feed planner.
     - Do not create a separate “missing mount plan” implementation that bypasses the queue model.
     - Do not create separate creature-type parsing logic inside Razor if it should live in catalog/rules/domain helpers.
  - Source verification requirements
     - Check the official Habitica app repo and Habitica API before finalizing growth/feed/saddle logic.
     - Verify how pet feeding progress is represented in current user data.
     - Verify whether newly hatched feedable pets currently have 10%, 20%, or another minimum progress value in the API/app.
     - Treat the observed 20% minimum as an explicit investigation item.
     - Verify the official formula used for food progress.
     - Public references may indicate:
        - Hatched pets start with 10% progress.
        - Favorite/preferred food adds 10% progress.
        - Non-preferred food adds 4% progress.
        - A pet transforms into a mount at 100% progress.
        - Saddles instantly transform a pet into a mount.
     - Do not hardcode assumptions until current official repo/API behavior is checked.
     - Verify how saddles are stored in inventory.
     - Verify how saddle use is performed through the API.
     - Verify special/unfeedable pet behavior.
     - Verify pets that already have corresponding mounts and cannot be grown again.
     - Verify canonical pet/mount keys and how mount keys map back to corresponding pet keys.
     - Verify favorite/recommended food mappings.
     - Verify readable creature type names from catalog/domain data.
  - Touch
     - `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
     - `src/Habitica.WebApp/wwwroot/css/app.css`
     - `src/Habitica.Rules/Pets`
     - `src/Habitica.Domain/User/PetsMountsCatalog.cs`
     - direct tests under `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`
     - direct pet rules tests under `tests/Habitica.Rules.Tests/`
     - direct catalog/domain tests under `tests/Habitica.Domain.Tests/` if creature type helper logic moves out of Razor
     - `FEATURES.md`
     - `docs/UX_UI_MANIFEST.md` if companion-card, filter-control, or action guidance changes
  - Out of scope
     - Auto-executing feed requests directly from a missing mount card without user review.
     - Changing Habitica endpoint contracts.
     - Planning growth for mounts whose corresponding pet is missing, special, wacky, already converted, or unsupported by the rules model.
     - Showing exact unavailable progress for unknown/special pets unless the rules model supports it.
     - Replacing existing text search.
     - Filtering hatching potions or bulk-sell rows unless already naturally covered by shared section filtering.
     - Changing collection grouping or fold persistence semantics beyond making visible matches understandable.
     - Changing catalog membership without source-backed data.
     - Implementing actual saddle purchase action before API/shop logic is verified.
  - Pet card growth progress
     - Build per-card growth summaries from cached local data:
        - `snapshot.Inventory.Pets`
        - `snapshot.Inventory.Mounts`
        - `snapshot.Inventory.Food`
        - `PetsMountsCatalog`
     - Render compact growth progress on owned normal pet cards.
     - Show current progress percentage.
     - Show remaining progress percentage.
     - Show mount-ready state when progress is already 100%.
     - Show already-owned-mount state when the corresponding mount is already owned.
     - Show concise unavailable state for owned pets that cannot grow into a normal mount.
     - Keep unowned pet cards focused on hatching requirements.
     - Do not imply feed progress before the pet exists.
     - Preserve existing hatch, equip, feed preview, fold, search, and bulk-sell controls.
  - Pet card food-needed summary
     - Show a short food-needed line for owned feedable pets.
     - Use the best available local feeding plan.
     - Prefer favorite food when enough favorite food exists.
     - Use mixed/alternative food only when favorite food is insufficient or absent.
     - Make the copy concise.
     - Avoid overwhelming explanations.
     - Do not prompt unnecessary feeding for:
        - Already mount-complete pets.
        - Pets whose corresponding mount is already owned.
        - Unknown/special/non-growable pets.
  - Missing mount growth planning
     - For each missing normal mount, derive the corresponding pet key from the mount key.
     - Look up current pet ownership and progress.
     - Add a `Plan to grow` action on missing mount cards only when:
        - The corresponding pet exists.
        - The corresponding pet is feedable.
        - The mount is not already owned.
        - The pet can grow into that mount.
     - When clicked, select the matching pet and add/prep it in the feed planner queue.
     - The user should see generated queue rows before any feeding or saddle action is executed.
     - If the pet is unavailable or cannot be grown, render a concise disabled/unavailable reason near the missing mount card.
     - Keep stale-data and busy-state guards already used by feed/equip actions.
  - Feed planner behavior
     - Rework the current FEED PLANNER / Feed with best food block into a queue-based planner.
     - `PLAN FEED` should add the selected pet to the feed queue.
     - Missing mount `Plan to grow` should add the corresponding pet to the same feed queue.
     - Adding the same pet multiple times should be prevented or handled clearly.
     - The feed queue should preserve the order in which pets were added.
     - Each queued pet should be represented by its own queued pet card.
     - Existing feed queue clear/execution behavior should remain compatible unless intentionally revised.
     - Generated queue rows should require the existing explicit feed execution action before consuming inventory.
  - Queued pet card content
     - Show pet name/type/potion in readable form.
     - Show current feeding progress.
     - Show progress bar for current progress toward mount conversion.
     - Show selected/assigned food.
     - Show selected food growth value.
     - Show available count for selected food.
     - Show planned consumption count for this pet.
     - Show expected resulting progress after planned feeding.
     - Show whether selected food is the pet’s favorite/recommended food.
     - Show warning when selected food is not favorite/recommended.
     - Show warning when selected food is exhausted by earlier queued pets.
     - Show warning when no selected food can be assigned and another food type should be selected.
     - Show remove button for deleting this pet from the queue.
  - Food selection behavior
     - Each queued pet card should have assignable food.
     - Default selected food should be the best available food for that pet.
     - Favorite/recommended food should be selected by default when available.
     - If favorite food is unavailable or insufficient, use the highest-value available alternative according to the rules model.
     - Keep existing feed selection controls available where relevant.
     - Selecting a pet for feeding should keep using dropdown ordering by highest growth value for that pet.
     - The food selector should show:
        - Food display name.
        - Growth value for this pet.
        - Available count.
        - Favorite/recommended marker.
     - The user should be able to manually change selected food.
     - If user selects non-favorite food, show warning that it is less efficient.
     - Saddles should not be selected as normal food in this flow.
     - Saddle handling should be separate.
  - Planned food consumption calculation
     - For each queued pet, calculate how much selected food is needed to reach 100% progress.
     - Use the maximum available amount that can bring the pet to 100%, without overcommitting inventory.
     - Do not allocate more food than the pet needs to reach 100%.
     - Do not allocate more food than the user owns.
     - When multiple queued pets use the same food, calculate total planned consumption across the queue.
     - Earlier queued pets reserve their planned food first.
     - Later queued pets using the same food can only consume the remaining available count.
     - If no remaining selected food is available for a later queued pet, set its planned consumption to 0.
     - Show warning that another food type should be selected when selected food is exhausted by earlier queued pets.
     - If only part of the needed food is available, show partial planned progress and make it clear the pet will not reach 100%.
     - Recalculate queue allocations after:
        - Adding a pet.
        - Removing a pet.
        - Changing selected food.
        - Refreshing inventory/user data.
        - Completing a feed action.
        - Completing a mount transform action.
        - Completing a saddle action.
  - Transform to Mount action
     - Each queued pet card should have a `Transform to Mount` button.
     - `Transform to Mount` should be enabled only when the pet will be at 100% progress.
     - `Transform to Mount` should be disabled when progress is below 100%.
     - Disabled state should explain why the action is unavailable.
     - If the button performs actual feeding/API actions, confirm the intended execution flow before enabling destructive/consuming operations.
     - After successful transformation:
        - Update pet/mount inventory state.
        - Remove or mark the queued card as completed.
        - Recalculate remaining queue food allocation.
  - Use Saddle action
     - Each queued pet card should have a separate `Use Saddle` button.
     - `Use Saddle` should be separate from normal food selection.
     - `Use Saddle` should be enabled when:
        - The user has at least one saddle available.
        - The pet can be transformed with a saddle.
        - The corresponding mount is not already owned.
     - When clicked, `Use Saddle` should show a confirmation prompt.
     - Prompt should clearly explain that a saddle instantly transforms the pet into a mount.
     - Prompt should warn that any already-fed progress/food investment is not refunded when using a saddle.
     - If the user confirms, perform the saddle action through the correct API flow.
     - If the user cancels, do nothing.
     - If the user has no saddles, `Use Saddle` should not perform the action.
     - If no saddles are available, clicking or interacting with the disabled state should guide the user to the buy saddle block.
  - Buy saddle block
     - Add a new buy saddle block lower on the Pet & Mounts page.
     - If the user tries to use a saddle without having saddles, scroll down to the buy saddle block.
     - The buy saddle block should explain that saddles instantly transform pets into mounts.
     - The buy saddle block should show saddle availability and purchase state if supported.
     - Check API/shop logic before adding any actual purchase action.
  - Queue remove behavior
     - Each queued pet card should have a remove button.
     - Removing a pet from the queue should immediately recalculate planned food allocation for remaining queued pets.
     - Removing an earlier pet should free its reserved food for later queued pets.
     - Removing a later pet should not affect earlier queued pet allocations except through normal recalculation.
  - Creature type filters
     - Add creature-type filters to pet and mount sections.
     - Pet sections should have a filter at the top for creature type.
     - Mount sections should have a filter at the top for creature type.
     - Filters should use public/readable creature names where possible.
     - Example filter values:
        - Wolf
        - Fox
        - Cactus
        - Dragon
        - Tiger Cub
        - Panda Cub
        - Lion Cub
        - Flying Pig
     - Use display names for labels while preserving canonical keys internally.
     - Add `All types` reset option.
     - Pet type filter should narrow visible pet cards.
     - Mount type filter should narrow visible mount cards.
     - Filters should compose with existing text search.
     - Search text and selected type should both narrow visible cards.
     - Folded groups should not hide matching results unexpectedly.
     - Empty filtered states should explain that no companions match the selected type/search.
     - Keep mobile layout compact.
     - Avoid repeated per-card filter labels.
  - Creature type metadata/helper logic
     - Add or expose creature type metadata from `PetCatalogItem.EggKey` and corresponding mount keys.
     - Use readable display names from `PetsMountsCatalog.ToReadableName`.
     - Build filter options from catalog entries.
     - Include unknown owned entries only where a readable type can be safely derived.
     - If helper logic moves out of Razor, cover it with catalog/rules/domain tests.
     - Avoid hardcoded display names in the page where catalog/domain helpers can provide them.
  - Progress and formula behavior
     - Use official app/API logic as source of truth.
     - Calculate current progress from API/user inventory data.
     - Calculate food value based on whether selected food is favorite/preferred.
     - Calculate remaining progress to 100%.
     - Calculate planned consumed count based on remaining progress and available food.
     - Avoid overfeeding beyond 100% in the plan.
     - Account for edge cases:
        - Already mount-owned pet.
        - Re-hatched pet that cannot become a second mount.
        - Special/unfeedable pets.
        - Wacky/non-standard pets.
        - Missing pet.
        - Missing food.
        - Missing saddle.
        - Unknown owned pet.
        - Unowned pet.
        - No food available.
        - Insufficient food available.
        - Busy state.
        - Stale API/user snapshot.
        - API state changed after sync.
  - Expected behavior
     - Owned feedable pet cards show current progress and remaining progress toward mount conversion.
     - Food-needed copy uses favorite food when available.
     - Food-needed copy accounts for alternative available food when favorite food is insufficient or absent.
     - Already mount-complete or already-owned-mount states do not prompt unnecessary feeding.
     - Unowned pets, unknown owned pets, and special/non-growable entries render without broken progress UI.
     - Missing mount cards show `Plan to grow` when the corresponding owned pet can be fed toward that mount.
     - Clicking `Plan to grow` selects the matching pet and prepares visible feed-plan queue rows with calculated food amounts.
     - Planned rows use highest-value food first and include lower-value food only when needed and available.
     - Missing pet, non-growable/special pet, already-owned mount, no-food, busy, and stale states do not produce invalid queued feed requests.
     - `PLAN FEED` adds pets to a queue instead of only affecting a single planner state.
     - Queued pet cards clearly show feeding progress and planned food usage.
     - Best available food is selected by default.
     - Non-favorite food selection is allowed but clearly warned.
     - Multiple queued pets cannot overconsume the same food inventory.
     - Later queued pets correctly receive only the remaining available food count.
     - `Transform to Mount` is available only when the pet reaches 100% progress.
     - Saddle usage is separate, confirmation-based, and only enabled when available.
     - Users without saddles are guided to the buy saddle block.
     - Queue items can be removed and the plan recalculates correctly.
     - Pet sections can be filtered by creature type and reset to all types.
     - Mount sections can be filtered by creature type and reset to all types.
     - Type filters compose with search and folded groups without hiding matching results unexpectedly.
     - Existing hatch, equip, feed preview, fold, search, bulk-sell, queue clear, and queue execution behavior still render/work.
  - Suggested implementation plan
     - First reconcile the existing Prioritized Next Changes tasks into this single implementation plan.
     - Inspect current `PetsMountsPage.razor` state and existing feed planner behavior.
     - Inspect `Habitica.Rules/Pets` and identify where shared progress/food planning logic should live.
     - Inspect `PetsMountsCatalog` and identify where creature type metadata/helper logic should live.
     - Add/extend shared rules helpers for:
        - Pet progress.
        - Favorite food lookup.
        - Food progress value.
        - Required food count.
        - Best available food plan.
        - Mixed food plan when favorite food is insufficient.
        - Mount key to pet key mapping.
        - Queue-wide food allocation.
        - Saddle availability.
        - Creature type extraction/display name.
     - Add per-card growth summary model.
     - Add missing-mount growth planning action using the same queue model.
     - Replace or rework feed planner state into a queue model.
     - Add queue item data:
        - Pet key/id.
        - Current progress.
        - Selected food key/id.
        - Selected food value.
        - Available food count.
        - Planned food consumption.
        - Expected progress after planned feeding.
        - Favorite/recommended state.
        - Warning state.
        - Transform availability.
        - Saddle availability.
     - Add queue recalculation after relevant state changes.
     - Add UI states for:
        - Ready to transform.
        - Needs more food.
        - Selected food unavailable.
        - Non-favorite food selected.
        - Saddle available.
        - No saddle available.
        - Pet cannot be fed/transformed.
        - Already has mount.
        - Missing corresponding pet.
        - Unknown/special/non-growable pet.
     - Add section-level creature type filters for pets and mounts.
     - Update CSS for compact progress, queue cards, filter controls, warnings, and responsive layout.
     - Update `FEATURES.md`.
     - Update `docs/UX_UI_MANIFEST.md` if card/action/filter display guidance changes.
     - Preserve offline cached behavior and avoid live calls for passive rendering.
     - Keep user review before any mutation/API action.
  - Acceptance criteria
     - Owned feedable pet cards show current progress and remaining progress toward mount conversion.
     - Food-needed copy uses favorite food when enough favorite food exists.
     - Food-needed copy falls back to alternative available food when favorite food is insufficient or absent.
     - Already-owned mount and mount-complete states do not prompt unnecessary feeding.
     - Unowned pets, unknown owned pets, and special/non-growable entries render safely.
     - Missing mount cards show `Plan to grow` only when the corresponding owned pet can be grown.
     - Clicking `Plan to grow` adds/selects the matching pet in visible feed-plan queue rows.
     - Generated feed-plan rows use highest-value food first and lower-value food only when needed and available.
     - Missing pet, non-growable/special, already-owned mount, no-food, busy, and stale states do not produce invalid queued feed requests.
     - Existing feed queue clear and execution behavior remains unchanged or is intentionally updated with tests.
     - Queued pet cards show progress, selected food, food value, available count, planned consumption, expected resulting progress, and warnings.
     - Multiple queued pets using the same food cannot exceed the available food count.
     - Later queued pets correctly show reduced or zero available allocation when earlier queued pets consume the same food.
     - `Transform to Mount` is disabled below 100% and enabled at 100%.
     - `Use Saddle` is separate from food selection and requires confirmation.
     - `Use Saddle` is enabled only when saddles are available and the pet can use one.
     - No-saddle state guides the user to the buy saddle block.
     - Queued pets can be removed and queue allocation recalculates immediately.
     - Pet sections can be filtered by creature type and reset to all types.
     - Mount sections can be filtered by creature type and reset to all types.
     - Filtering by `Wolf`, `Fox`, `Cactus`, `Dragon`, `Tiger Cub`, `Panda Cub`, `Lion Cub`, and `Flying Pig` shows only matching pet or mount cards.
     - Type filters compose with existing text search and folded groups.
     - Empty filtered states explain that no companions match selected type/search.
     - Component tests cover:
        - Owned partial-progress pet.
        - Enough favorite food.
        - Fallback food.
        - Already-owned mount.
        - Unowned pet card state.
        - Successful missing-mount planning.
        - Unavailable corresponding pet.
        - Insufficient/no food messaging.
        - Generated queue execution handoff.
        - Queue allocation with multiple pets using the same food.
        - Removing queued pet recalculates allocation.
        - Non-favorite food warning.
        - Saddle available state.
        - No-saddle guidance.
        - Pet filter.
        - Mount filter.
        - Filter reset behavior.
        - Search composition.
        - Multi-word creature display name.
     - Pet rules tests cover:
        - Progress formula.
        - Favorite food value.
        - Non-favorite food value.
        - Required food count.
        - Mixed food plan.
        - Queue allocation.
        - Edge cases for unknown/special/non-growable pets.
     - Catalog/domain tests cover creature type extraction/display names if helper logic moves there.
  - Build command
     - Run:
       ```bash
       DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet build Habitica.sln -m:1 -nodeReuse:false
       ```
  - Test commands
     - Run `PetsMountsPageTests`:
       ```bash
       DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~PetsMountsPageTests
       ```
     - Run pet rules tests:
       ```bash
       DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Rules.Tests/Habitica.Rules.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~Pets
       ```
     - Run catalog/domain tests if helper logic moves to domain/catalog layer:
       ```bash
       DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Domain.Tests/Habitica.Domain.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~PetsMounts
       ```


## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

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
