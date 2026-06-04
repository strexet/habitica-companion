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

_No queued entries._

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Unified Pet And Mounts Growth Planner

Goal: rework Pet & Mounts growth progress, missing-mount planning, feed queue, saddle flow, and creature type filters as one combined page upgrade.

This task replaces and absorbs these former split tasks:
- Pets And Mounts Pet Card Growth Progress
- Pets And Mounts Missing-Mount Growth Planning
- Pets And Mounts Creature Type Filters

Do not implement these as separate parallel features. Use one shared rules/calculation model for pet progress, favorite/recommended food, food growth value, remaining food requirement, missing-mount mapping, feed queue allocation, creature type extraction, and saddle availability.

Main goal:
- Show which owned pets can still become mounts.
- Show how close each owned pet is to becoming a mount.
- Show how much more food is needed.
- Recommend best available food.
- Make planned feeding consume available food correctly across a queue.
- Show which missing mounts can be grown from currently owned pets.
- Keep saddle usage separate, intentional, and confirmation-based.
- Let users filter large pet/mount collections by creature type.
- Preserve offline cached rendering wherever possible.
- Avoid live calls for passive progress/summary rendering.
- Keep user review before any mutation/API-consuming action.

Touch:
- `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.Rules/Pets`
- `src/Habitica.Domain/User/PetsMountsCatalog.cs`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`
- direct pet rules tests under `tests/Habitica.Rules.Tests/`
- direct catalog/domain tests under `tests/Habitica.Domain.Tests/` if creature type helper logic moves out of Razor
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if companion-card, filter-control, or action guidance changes

Out of scope:
- Auto-executing feed requests directly from a missing mount card without user review.
- Changing Habitica endpoint contracts.
- Planning growth for mounts whose corresponding pet is missing, special, wacky, already converted, or unsupported by the rules model.
- Showing exact unavailable progress for unknown/special pets unless the rules model supports it.
- Replacing existing text search.
- Filtering hatching potions or bulk-sell rows unless already naturally covered by shared section filtering.
- Changing collection grouping or fold persistence semantics beyond making visible matches understandable.
- Changing catalog membership without source-backed data.
- Implementing actual saddle purchase action before API/shop logic is verified.

Source verification requirements:
- Check the official Habitica app repo and Habitica API before finalizing growth/feed/saddle logic.
- Verify how pet feeding progress is represented in current user data.
- Verify whether newly hatched feedable pets currently have 10%, 20%, or another minimum progress value in the API/app.
- Treat observed 20% minimum as an explicit investigation item.
- Verify official formula used for food progress.
- Public references may indicate:
  - hatched pets start with 10% progress;
  - favorite/preferred food adds 10% progress;
  - non-preferred food adds 4% progress;
  - a pet transforms into a mount at 100% progress;
  - saddles instantly transform a pet into a mount.
- Do not hardcode assumptions until current official repo/API behavior is checked.
- Verify how saddles are stored in inventory.
- Verify how saddle use is performed through the API.
- Verify special/unfeedable pet behavior.
- Verify pets that already have corresponding mounts and cannot be grown again.
- Verify canonical pet/mount keys and how mount keys map back to corresponding pet keys.
- Verify favorite/recommended food mappings.
- Verify readable creature type names from catalog/domain data.

Pet card growth progress:
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

Pet card food-needed summary:
- Show a short food-needed line for owned feedable pets.
- Use the best available local feeding plan.
- Prefer favorite food when enough favorite food exists.
- Use mixed/alternative food only when favorite food is insufficient or absent.
- Keep copy concise.
- Avoid prompting unnecessary feeding for:
  - already mount-complete pets;
  - pets whose corresponding mount is already owned;
  - unknown/special/non-growable pets.

Missing mount growth planning:
- For each missing normal mount, derive the corresponding pet key from the mount key.
- Look up current pet ownership and progress.
- Add `Plan to grow` on missing mount cards only when:
  - corresponding pet exists;
  - corresponding pet is feedable;
  - mount is not already owned;
  - pet can grow into that mount.
- On click, select the matching pet and add/prep it in the feed planner queue.
- Show generated queue rows before any feeding or saddle action executes.
- If pet is unavailable or cannot be grown, render concise disabled/unavailable reason near the missing mount card.
- Keep stale-data and busy-state guards already used by feed/equip actions.

Feed planner behavior:
- Rework current FEED PLANNER / Feed with best food block into queue-based planner.
- `PLAN FEED` should add selected pet to feed queue.
- Missing mount `Plan to grow` should add corresponding pet to same feed queue.
- Adding same pet multiple times should be prevented or handled clearly.
- Feed queue should preserve added order.
- Each queued pet should render as its own queued pet card.
- Existing feed queue clear/execution behavior should remain compatible unless intentionally revised.
- Generated queue rows should require existing explicit feed execution action before consuming inventory.

Queued pet card content:
- Show pet name/type/potion in readable form.
- Show current feeding progress.
- Show progress bar for current progress toward mount conversion.
- Show selected/assigned food.
- Show selected food growth value.
- Show available count for selected food.
- Show planned consumption count for this pet.
- Show expected resulting progress after planned feeding.
- Show whether selected food is favorite/recommended.
- Show warning when selected food is not favorite/recommended.
- Show warning when selected food is exhausted by earlier queued pets.
- Show warning when no selected food can be assigned and another food type should be selected.
- Show remove button for deleting this pet from the queue.

Food selection behavior:
- Each queued pet card should have assignable food.
- Default selected food should be best available food for that pet.
- Favorite/recommended food should be selected by default when available.
- If favorite food is unavailable or insufficient, use highest-value available alternative according to rules model.
- Keep existing feed selection controls available where relevant.
- Selecting a pet for feeding should keep using dropdown ordering by highest growth value for that pet.
- Food selector should show:
  - food display name;
  - growth value for this pet;
  - available count;
  - favorite/recommended marker.
- User can manually change selected food.
- If user selects non-favorite food, show warning that it is less efficient.
- Saddles should not be selected as normal food.
- Saddle handling should be separate.

Planned food consumption calculation:
- For each queued pet, calculate selected food needed to reach 100% progress.
- Use maximum available amount that can bring pet to 100%, without overcommitting inventory.
- Do not allocate more food than pet needs to reach 100%.
- Do not allocate more food than user owns.
- When multiple queued pets use same food, calculate total planned consumption across queue.
- Earlier queued pets reserve planned food first.
- Later queued pets using same food can only consume remaining available count.
- If no remaining selected food is available for later queued pet, set planned consumption to 0.
- Show warning that another food type should be selected when selected food is exhausted by earlier queued pets.
- If only part of needed food is available, show partial planned progress and make clear pet will not reach 100%.
- Recalculate queue allocations after:
  - adding a pet;
  - removing a pet;
  - changing selected food;
  - refreshing inventory/user data;
  - completing a feed action;
  - completing a mount transform action;
  - completing a saddle action.

Transform to Mount action:
- Each queued pet card should have a `Transform to Mount` button.
- `Transform to Mount` should be enabled only when pet will be at 100% progress.
- `Transform to Mount` should be disabled when progress is below 100%.
- Disabled state should explain why action is unavailable.
- If button performs actual feeding/API actions, confirm intended execution flow before enabling destructive/consuming operations.
- After successful transformation:
  - update pet/mount inventory state;
  - remove or mark queued card as completed;
  - recalculate remaining queue food allocation.

Use Saddle action:
- Each queued pet card should have a separate `Use Saddle` button.
- `Use Saddle` should be separate from normal food selection.
- `Use Saddle` should be enabled when:
  - user has at least one saddle available;
  - pet can be transformed with a saddle;
  - corresponding mount is not already owned.
- On click, show confirmation prompt.
- Prompt should explain that saddle instantly transforms pet into mount.
- Prompt should warn that already-fed progress/food investment is not refunded when using saddle.
- If user confirms, perform saddle action through correct API flow.
- If user cancels, do nothing.
- If user has no saddles, `Use Saddle` should not perform action.
- If no saddles are available, disabled state should guide user to buy saddle block.

Buy saddle block:
- Add a new buy saddle block lower on Pet & Mounts page.
- If user tries to use a saddle without saddles, scroll down to buy saddle block.
- Explain that saddles instantly transform pets into mounts.
- Show saddle availability and purchase state if supported.
- Check API/shop logic before adding any actual purchase action.

Queue remove behavior:
- Each queued pet card should have a remove button.
- Removing a pet from queue should immediately recalculate planned food allocation for remaining queued pets.
- Removing an earlier pet should free its reserved food for later queued pets.
- Removing a later pet should not affect earlier queued pet allocations except through normal recalculation.

Creature type filters:
- Add creature-type filters to pet and mount sections.
- Pet sections should have a filter at top for creature type.
- Mount sections should have a filter at top for creature type.
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
- Empty filtered states should explain no companions match selected type/search.
- Keep mobile layout compact.
- Avoid repeated per-card filter labels.

Creature type metadata/helper logic:
- Add or expose creature type metadata from `PetCatalogItem.EggKey` and corresponding mount keys.
- Use readable display names from `PetsMountsCatalog.ToReadableName`.
- Build filter options from catalog entries.
- Include unknown owned entries only where a readable type can be safely derived.
- If helper logic moves out of Razor, cover it with catalog/rules/domain tests.
- Avoid hardcoded display names in page where catalog/domain helpers can provide them.

Progress and formula behavior:
- Use official app/API logic as source of truth.
- Calculate current progress from API/user inventory data.
- Calculate food value based on whether selected food is favorite/preferred.
- Calculate remaining progress to 100%.
- Calculate planned consumed count based on remaining progress and available food.
- Avoid overfeeding beyond 100% in plan.
- Account for edge cases:
  - already mount-owned pet;
  - re-hatched pet that cannot become a second mount;
  - special/unfeedable pets;
  - wacky/non-standard pets;
  - missing pet;
  - missing food;
  - missing saddle;
  - unknown owned pet;
  - unowned pet;
  - no food available;
  - insufficient food available;
  - busy state;
  - stale API/user snapshot;
  - API state changed after sync.

Implementation plan:
- Inspect current `PetsMountsPage.razor` state and existing feed planner behavior.
- Inspect `Habitica.Rules/Pets` and identify where shared progress/food planning logic should live.
- Inspect `PetsMountsCatalog` and identify where creature type metadata/helper logic should live.
- Verify official app/API behavior before finalizing formula, saddle, and transform logic.
- Add/extend shared rules helpers for:
  - pet progress;
  - favorite food lookup;
  - food progress value;
  - required food count;
  - best available food plan;
  - mixed food plan when favorite food is insufficient;
  - mount key to pet key mapping;
  - queue-wide food allocation;
  - saddle availability;
  - creature type extraction/display name.
- Add per-card growth summary model.
- Add missing-mount growth planning action using same queue model.
- Replace or rework feed planner state into queue model.
- Add queue item data:
  - pet key/id;
  - current progress;
  - selected food key/id;
  - selected food value;
  - available food count;
  - planned food consumption;
  - expected progress after planned feeding;
  - favorite/recommended state;
  - warning state;
  - transform availability;
  - saddle availability.
- Add queue recalculation after relevant state changes.
- Add UI states for:
  - ready to transform;
  - needs more food;
  - selected food unavailable;
  - non-favorite food selected;
  - saddle available;
  - no saddle available;
  - pet cannot be fed/transformed;
  - already has mount;
  - missing corresponding pet;
  - unknown/special/non-growable pet.
- Add section-level creature type filters for pets and mounts.
- Update CSS for compact progress, queue cards, filter controls, warnings, and responsive layout.
- Update `FEATURES.md`.
- Update `docs/UX_UI_MANIFEST.md` if card/action/filter display guidance changes.
- Preserve offline cached behavior and avoid live calls for passive rendering.
- Keep user review before any mutation/API action.

Acceptance:
- Owned feedable pet cards show current progress and remaining progress toward mount conversion.
- Food-needed copy uses favorite food when enough favorite food exists.
- Food-needed copy falls back to alternative available food when favorite food is insufficient or absent.
- Already-owned mount and mount-complete states do not prompt unnecessary feeding.
- Unowned pets, unknown owned pets, and special/non-growable entries render safely.
- Missing mount cards show `Plan to grow` only when corresponding owned pet can be grown.
- Clicking `Plan to grow` adds/selects matching pet in visible feed-plan queue rows.
- Generated feed-plan rows use highest-value food first and lower-value food only when needed and available.
- Missing pet, non-growable/special, already-owned mount, no-food, busy, and stale states do not produce invalid queued feed requests.
- Existing feed queue clear and execution behavior remains unchanged or is intentionally updated with tests.
- Queued pet cards show progress, selected food, food value, available count, planned consumption, expected resulting progress, and warnings.
- Multiple queued pets using same food cannot exceed available food count.
- Later queued pets correctly show reduced or zero available allocation when earlier queued pets consume same food.
- `Transform to Mount` is disabled below 100% and enabled at 100%.
- `Use Saddle` is separate from food selection and requires confirmation.
- `Use Saddle` is enabled only when saddles are available and pet can use one.
- No-saddle state guides user to buy saddle block.
- Queued pets can be removed and queue allocation recalculates immediately.
- Pet sections can be filtered by creature type and reset to all types.
- Mount sections can be filtered by creature type and reset to all types.
- Filtering by `Wolf`, `Fox`, `Cactus`, `Dragon`, `Tiger Cub`, `Panda Cub`, `Lion Cub`, and `Flying Pig` shows only matching pet or mount cards.
- Type filters compose with existing text search and folded groups.
- Empty filtered states explain no companions match selected type/search.
- Existing hatch, equip, feed preview, fold, search, bulk-sell, queue clear, and queue execution behavior still render/work.
- Component tests cover:
  - owned partial-progress pet;
  - enough favorite food;
  - fallback food;
  - already-owned mount;
  - unowned pet card state;
  - successful missing-mount planning;
  - unavailable corresponding pet;
  - insufficient/no food messaging;
  - generated queue execution handoff;
  - queue allocation with multiple pets using same food;
  - removing queued pet recalculates allocation;
  - non-favorite food warning;
  - saddle available state;
  - no-saddle guidance;
  - pet filter;
  - mount filter;
  - filter reset behavior;
  - search composition;
  - multi-word creature display name.
- Pet rules tests cover:
  - progress formula;
  - favorite food value;
  - non-favorite food value;
  - required food count;
  - mixed food plan;
  - queue allocation;
  - edge cases for unknown/special/non-growable pets.
- Catalog/domain tests cover creature type extraction/display names if helper logic moves there.

Need to run build:

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet build Habitica.sln -m:1 -nodeReuse:false
```

Need to run test(s): `PetsMountsPageTests`, pet rules tests, and catalog/domain tests if helper logic moves to domain/catalog layer

```bash
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~PetsMountsPageTests
DOTNET_CLI_HOME=/tmp/habitica-tool-dotnet-home dotnet test tests/Habitica.Rules.Tests/Habitica.Rules.Tests.csproj -m:1 -nodeReuse:false --filter FullyQualifiedName~Pets
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
