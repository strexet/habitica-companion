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
- Dedicated Pets & Mounts page with grouped companion grids, feed queue planner, hatching and equip actions, local fold preferences, and relocated bulk sell planner while keeping per-pet/per-mount maps out of Cloudflare app-data uploads.
- Dashboard gem-for-gold purchase action with visible availability states, no-subscription Subscribe link, quantity clamp, explicit confirmation, sequential stop-on-failure requests, diagnostics, and post-purchase account refresh.
- Party page overview no longer shows the buff-timing recommendation block, while member review, CRON graph context, and Quests workspace remain intact.
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

- Add Reset to Habitica action for Tasks page section ordering
   - Description
      - On the Tasks page, each reorderable section should have a `Reset to Habitica` button while reordering is available/active.
      - This button should remove the locally/custom-synced ordering data for that specific section.
      - After reset, the section should fall back to Habitica’s default task order.
      - If there is no saved reorder data for the user, the section should already sync/use Habitica order by default.

   - Sections affected
      - Tasks / To-Dos section.
      - Dailies section.
      - Habits section.
      - Any other reorderable task section if it uses the same persisted ordering model.

   - Reset behavior
      - `Reset to Habitica` should delete the saved order data only for the selected section.
      - Resetting Tasks / To-Dos should not reset Dailies or Habits.
      - Resetting Dailies should not reset Tasks / To-Dos or Habits.
      - Resetting Habits should not reset Tasks / To-Dos or Dailies.
      - After reset, the section should immediately render in Habitica’s order.
      - Future refreshes/page visits should continue using Habitica order until the user creates a new custom order.
      - If the user reorders again after reset, new order data should be saved normally.

   - Default ordering behavior
      - If no reorder data exists for the current user and section, use Habitica’s synced/default ordering.
      - Do not create empty or placeholder reorder data just because the user opened the page.
      - Do not treat missing reorder data as an error.
      - Missing reorder data should mean:
         - use Habitica order;
         - allow user to create custom order by reordering;
         - show `Reset to Habitica` as disabled or unnecessary if already using Habitica order.

   - Button behavior
      - Show `Reset to Habitica` near the reorder controls for each section.
      - Disable or hide the button when the section is already using Habitica order.
      - If visible but disabled, explain that there is no custom order to reset.
      - Consider confirmation only if reset is not easily reversible.
      - Preferred behavior:
         - No heavy confirmation.
         - Apply reset immediately.
         - Optionally show a small status message like `Order reset to Habitica`.

   - Expected behavior
      - User can reset only the current section’s custom order.
      - Reset removes persisted order data for that section.
      - Section falls back to Habitica ordering immediately.
      - Users without saved reorder data see Habitica order by default.
      - Reordering after reset creates new saved order data as expected.

   - Suggested fix
      - Locate the persisted task ordering data model.
      - Make ordering state section-specific:
         - Tasks / To-Dos.
         - Dailies.
         - Habits.
      - Add a section-level reset action that deletes only that section’s stored order data.
      - Update ordering resolution logic:
         - If custom order exists, apply it.
         - If custom order is missing, use Habitica order.
      - Add UI button near section reorder controls.
      - Re-render section after reset.
      - Sync/remove updated ordering state from user data after reset.

   - Acceptance criteria
      - Each reorderable Tasks page section has a `Reset to Habitica` action.
      - Reset deletes custom ordering data only for that section.
      - Reset does not affect other task sections.
      - Missing reorder data defaults to Habitica order.
      - Section immediately returns to Habitica order after reset.
      - Refreshing/reopening the page preserves Habitica order after reset.
      - Reordering again after reset saves a new custom order normally.

- Remove non-damaging Dailies from the Dashboard CRON task list
   - Description
      - The Dashboard CRON menu currently includes some incomplete Dailies that will not cause damage during the relevant cron.
      - Dailies that are not due because of the user-defined schedule should not be shown in the CRON task list.
      - The CRON list should contain only incomplete tasks that are actually eligible to cause cron damage for the current cron period.
      - Do not determine eligibility only from task type and completion state.
      - Use Habitica’s official scheduling/due logic as the source of truth.

   - Main behavior
      - Include an incomplete Daily only when it is due for the cron period being evaluated.
      - Exclude an incomplete Daily when its schedule makes it inactive or avoidable for that cron.
      - Examples of schedule-based exclusions may include:
         - A Daily configured only for specific weekdays when the evaluated day is not selected.
         - A Daily using an every-X-days schedule when it is not due for the evaluated cron.
         - A Daily whose start date or recurrence configuration means it is inactive for that period.
      - Keep tasks that are genuinely due and can contribute damage if left incomplete.
      - Preserve any separate handling for paused damage, Inn state, or other user-level cron rules already supported by the app.

   - Official logic verification
      - Check the current Habitica API model and official Habitica repository before implementing the filter.
      - Find and reuse or reproduce the same logic Habitica uses to determine whether a Daily is due on a specific cron date.
      - Verify all relevant Daily fields, including where applicable:
         - `type`
         - `completed`
         - `repeat`
         - `frequency`
         - `everyX`
         - `startDate`
         - `streak`
         - cron date/day
         - user day-start/timezone context
      - Verify how Habitica handles:
         - Day-of-week schedules.
         - Every-X-days schedules.
         - Start dates.
         - User custom day-start time.
         - Timezone boundaries.
         - Paused damage / Inn state.
         - Group-plan or assigned Dailies if supported by the current data model.
      - Do not create a simplified local rule that can disagree with Habitica cron behavior.
      - Prefer an existing shared cron/daily-due helper if one already exists in the project.

   - CRON list filtering
      - Start from the tasks currently considered for the Dashboard CRON list.
      - Keep only tasks that:
         - Are Dailies relevant to cron damage.
         - Are incomplete for the evaluated cron.
         - Are due according to Habitica scheduling rules.
         - Are not otherwise exempt from damage under existing supported rules.
      - Remove tasks that:
         - Are not scheduled for the evaluated day.
         - Are not due under their recurrence settings.
         - Cannot cause damage during that cron.
      - Do not remove a Daily merely because it is hidden in one particular UI view if Habitica still considers it due.
      - Do not include To-Dos, Habits, Rewards, or other task types unless the existing CRON feature intentionally uses them for another clearly defined purpose.

   - Date and cron context
      - Evaluate due state for the correct Habitica cron period, not simply the device’s current calendar date.
      - Account for the user’s custom day-start setting and timezone where required.
      - Reuse the same cron date/context already used by the Dashboard CRON feature.
      - Avoid off-by-one-day errors around midnight, custom day start, timezone changes, and daylight-saving transitions.

   - UI behavior
      - Non-damaging scheduled Dailies should disappear from the CRON task list.
      - The list should remain focused on tasks the user actually needs to complete to avoid damage.
      - If every incomplete Daily is non-damaging for the evaluated cron, show the existing empty/safe state rather than an empty broken container.
      - Do not change the normal Tasks page visibility or filtering.
      - This filtering applies only to the CRON task list and related cron-risk summary.

   - Expected behavior
      - A Monday-only Daily is not shown in the CRON list for a Tuesday cron.
      - A weekday Daily is not shown for an excluded weekend day.
      - An every-X-days Daily is shown only when Habitica considers it due.
      - A Daily that is due and incomplete remains in the CRON list.
      - The CRON list does not warn the user about tasks that cannot cause damage during that cron.
      - Results match Habitica’s official cron behavior.

   - Suggested implementation
      - Inspect the current CRON task-list generation logic.
      - Identify whether the project already has:
         - A Daily due-date helper.
         - A cron date calculator.
         - User day-start/timezone normalization.
      - Move due-state evaluation into a shared rules/helper layer if it currently exists only in the UI.
      - Pass the correct user/cron context into the helper.
      - Filter the Dashboard CRON list using the resulting damage-eligible/due state.
      - Keep the UI component responsible only for rendering the filtered result.
      - Add comments or documentation pointing to the corresponding Habitica behavior/source.

   - Edge cases
      - Daily scheduled for only one day of the week.
      - Daily scheduled for multiple selected weekdays.
      - Daily due every day.
      - Every-X-days Daily.
      - Daily with a future start date.
      - Daily around the user’s custom day-start boundary.
      - Timezone/date transition.
      - User resting in the Inn or with paused damage.
      - Completed due Daily.
      - Incomplete due Daily.
      - Incomplete but non-due Daily.
      - Unknown or incomplete schedule data.
      - Stale cached task/user data.

   - Acceptance criteria
      - Dashboard CRON list excludes incomplete Dailies that are not due for the evaluated cron.
      - Dashboard CRON list includes incomplete Dailies that Habitica considers due and damage-eligible.
      - Day-of-week schedule filtering matches Habitica behavior.
      - Every-X-days schedule filtering matches Habitica behavior.
      - Custom day-start and timezone context are handled consistently with existing cron logic.
      - Existing Inn/paused-damage behavior is preserved.
      - Tasks page behavior remains unchanged.
      - Empty CRON state renders correctly when no damaging tasks remain.
      - Tests cover:
         - Due weekday Daily.
         - Non-due weekday Daily.
         - Daily due every day.
         - Due and non-due every-X-days Daily.
         - Future-start Daily.
         - Completed due Daily.
         - Incomplete due Daily.
         - Incomplete non-due Daily.
         - Custom day-start boundary where supported.
         - Inn/paused-damage behavior where supported.

- Add Spend All Mana, cancellable spell preparation, and updated API request spacing
   - Description
      - Improve bulk spell casting on the Spells page.
      - Add a `Spend All Mana` action to every spell card.
      - The action should set the spell’s cast-count input to the maximum number of casts currently affordable with the user’s available mana.
      - Add a one-second `Preparing…` stage before the first spell-casting action begins.
      - The preparation stage should not perform any API requests or mutate any state.
      - Add a card-local `Cancel` button that becomes visible when casting begins.
      - Users should be able to cancel during preparation and between every later step of the casting flow.
      - Update the default minimum spacing between Habitica API requests from 300 ms to 350 ms.
      - Preserve the existing sequential casting architecture, progress UI, CRON warning flow, dynamic equipment recommendations, and post-cast refresh behavior.

   - Project context
      - The Spells page already provides:
         - Spell cards with cast-count input.
         - Current mana and after-cast mana previews.
         - Sequential multi-casting.
         - Card-local determinate progress such as `Casting 2 of 5`.
         - Dynamic equipment recommendations and optional auto-equip.
         - CRON-sensitive buff warnings.
         - Cancellation stop conditions in the casting orchestration.
         - Responsive two-zone card layout that stacks on narrow screens.
      - Extend these existing systems rather than adding a second casting/progress/cancellation implementation.
      - Reuse the current casting cancellation state and progress model where possible.

   - Touch
      - `src/Habitica.WebApp/Pages/SpellsPage.razor`
      - `src/Habitica.WebApp/wwwroot/css/app.css`
      - spell-casting orchestration in the application/session layer
      - `HabiticaApiClientOptions`
      - `Program.cs`
      - `appsettings.json` only to confirm that no override is added
      - direct Spells page tests under `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs`
      - spell/session orchestration tests
      - Habitica API client/options tests
      - `FEATURES.md`
      - `docs/UX_UI_MANIFEST.md` if spell-card action/progress guidance changes
      - `TECHNICAL.md` if API request-spacing documentation records the default value

   - Spend All Mana action
      - Add a `Spend All Mana` button to every unlocked spell card that can be cast.
      - When clicked, calculate the maximum affordable cast count from:
         - Current available mana.
         - Mana cost per cast.
      - Use:
         - `floor(available mana / mana cost per cast)`
      - Set the spell card’s cast-count value to that result.
      - Reuse the same mana source and spell-cost data already used by the card’s mana preview and Cast validation.
      - Do not maintain a separate mana calculation that can drift from the existing preview logic.
      - After updating the count, immediately update:
         - Total mana cost.
         - Expected remaining mana.
         - Effect estimate.
         - Quest damage estimate where applicable.
         - Equipment-based estimate where applicable.
      - Do not automatically start casting.
      - The user should still explicitly press `Cast`.
      - Do not include future mana regeneration or mana gained from spell effects.
      - Use only the currently available cached mana considered valid by the existing casting flow.

   - Spend All Mana edge states
      - If the user cannot afford one cast:
         - Set cast count to the existing valid minimum only if the current input model requires it.
         - Keep Cast disabled.
         - Prefer disabling `Spend All Mana` with a concise reason such as `Not enough mana`.
      - If the spell is locked:
         - Keep `Spend All Mana` unavailable.
      - If account data is stale, expired, or missing:
         - Follow the existing casting safety rules.
         - Do not calculate a misleading maximum from invalid data.
      - If the calculated count exceeds an existing supported input maximum:
         - Respect that validated maximum.
         - Do not silently create an unsupported cast count.
      - If available mana changes after the value was filled:
         - Revalidate at cast time using the existing validation flow.
      - For zero-cost or malformed spell-cost data:
         - Block the calculation safely instead of dividing by zero or producing an unbounded count.

   - Spend All Mana button placement
      - Place `Spend All Mana` close to the existing cast-count input because it modifies that value.
      - Keep it visually secondary to the main `Cast` action.
      - Do not make the button so large that it crowds the card or creates another fragile action row.
      - Ensure the count input, `Spend All Mana`, auto-equip controls, and Cast action remain aligned.
      - Check desktop/browser and mobile/narrow layouts.
      - On narrow layouts, allow the controls to stack or wrap cleanly without creating stair-like alignment or overflowing the card.

   - One-second preparation stage
      - Add a mandatory one-second preparation stage after the user confirms the cast flow and immediately before the first actual casting step.
      - During this stage:
         - Show `Preparing…` in the active spell card’s casting progress area.
         - Show the casting progress bar in its initial state.
         - Do not equip gear.
         - Do not call the Habitica API.
         - Do not spend mana.
         - Do not mutate local user/task/party state.
         - Wait for one second using an asynchronous cancellable delay.
      - The preparation delay is a UX cancellation window, not request throttling.
      - Do not add the one-second delay before every individual cast unless explicitly required later.
      - Apply it once per user-initiated casting run, before the first mutation step.
      - If the flow pauses for a CRON warning or other confirmation:
         - Start the preparation stage only after the user has made the final decision to proceed.
         - Do not show `Preparing…` while waiting for user confirmation.

   - Preparing progress UI
      - Reuse the current card-local casting progress area.
      - During preparation, show:
         - `Preparing…`
         - Initial/zero determinate progress, or the closest visual state supported by the current progress component.
      - After preparation finishes, transition directly into the existing casting stages, such as:
         - Equipping recommended gear.
         - Casting `1 of N`.
         - Refreshing affected snapshots.
         - Restoring gear, if the current flow does so.
      - Avoid hiding and recreating the progress block between preparation and casting.
      - The transition should not cause card-height or page-layout jumps.

   - Card-local Cancel button
      - Add cancellation support to every spell card.
      - Each spell card should be able to render a `Cancel` button when that card’s casting flow is active.
      - The `Cancel` button should appear as soon as casting preparation begins.
      - Keep it visible through all cancellable steps of that casting run.
      - Hide it when:
         - Casting completes.
         - Cancellation cleanup completes.
         - Casting fails and the flow has stopped.
         - No casting operation is active for that card.
      - The button should be visually distinct from `Cast` without overpowering the card.
      - It should fit cleanly in desktop/browser and mobile layouts.
      - Do not render inactive Cancel buttons permanently on every card.
      - Only the currently active spell card should show its active cancellation control.

   - Cancellation behavior
      - User should be able to request cancellation during every stage:
         - The one-second `Preparing…` delay.
         - Auto-equip planning/execution.
         - Between equipment requests.
         - Before each spell cast.
         - Between sequential spell casts.
         - During configured spacing/delay waits.
         - Before post-cast refresh requests.
         - Between user/tasks/party refresh requests.
         - Before gear restoration steps, if restoration is part of the current flow.
      - Pass the casting cancellation token through all cancellable delays and operations where supported.
      - Check cancellation before starting each mutation or network step.
      - Stop scheduling additional casts immediately after cancellation is observed.
      - Cancellation during preparation should result in:
         - Zero API requests.
         - Zero mana spent.
         - No gear changes.
         - No state mutation.
      - Cancellation after some casts have completed should:
         - Preserve successful completed casts.
         - Stop remaining casts.
         - Report completed/requested count.
         - Refresh affected local state where needed so mana, task, quest, party, and equipment state remain accurate.
      - Do not attempt to roll back successful Habitica mutations.
      - Do not report the entire operation as untouched when some casts or equipment changes already succeeded.

   - In-flight request cancellation safety
      - Treat cancellation of already-sent non-idempotent Habitica mutation requests carefully.
      - If an API mutation has already been sent, the server may complete it even if the local cancellation token is triggered.
      - Prefer cancellation boundaries before requests and between steps.
      - If the current HTTP layer safely supports cancelling an in-flight request:
         - Do not assume cancellation proves that the server did not apply the mutation.
         - Refresh relevant snapshots before presenting final state.
      - Ensure the user-facing result distinguishes:
         - Cancelled before any action.
         - Cancelled after partial completion.
         - Failed request.
         - Successfully completed run.

   - Cancellation result messaging
      - Use the existing non-alert feedback pattern.
      - Suggested results:
         - `Casting cancelled before it started.`
         - `Casting cancelled after 2 of 5 casts.`
         - `Casting completed: 5 of 5.`
      - Keep API errors separate from user cancellation.
      - Do not present user cancellation as an error.
      - Preserve diagnostics logging with:
         - Spell id.
         - Requested cast count.
         - Completed cast count.
         - Cancellation stage where useful.
      - Do not log credentials or sensitive headers.

   - Interaction locking
      - While a spell card is actively casting:
         - Prevent starting a second casting run for that same card.
         - Preserve existing global/session guards that prevent conflicting mutation flows.
         - Disable or guard count, target, auto-equip, and recommendation controls if changing them would invalidate the active execution plan.
         - Keep `Cancel` enabled.
      - Other spell cards should not start conflicting casting operations while one sequential cast flow is active unless the current application architecture explicitly supports concurrent mutations.
      - Do not allow repeated Cast clicks to enqueue duplicate casting runs.

   - Progress behavior
      - Preserve existing determinate casting progress such as `Casting 2 of 5`.
      - Add preparation as an explicit initial stage.
      - Keep completed/requested cast count accurate after cancellation.
      - If auto-equip or refresh steps already have progress/status labels, reuse them.
      - Do not create competing progress indicators for the same spell card.
      - Active casting progress and errors should remain prominent after the compact spell-card density pass.
      - Progress should remain readable with all supported color schemes.

   - Request-spacing update
      - Change `HabiticaApiClientOptions.MinRequestSpacingMilliseconds` default:
         - From `300`
         - To `350`
      - Update the optional configuration fallback in `Program.cs`:
         - From `300`
         - To `350`
      - Keep the existing optional configuration key:
         - `Habitica:MinRequestSpacingMilliseconds`
      - Do not add an override to the current `appsettings.json`.
      - With no explicit configuration override, the effective value should therefore be 350 ms.
      - Preserve existing adaptive token-bucket throttling.
      - Preserve handling of:
         - `Retry-After`
         - `X-RateLimit-Limit`
         - `X-RateLimit-Remaining`
         - `X-RateLimit-Reset`
      - Preserve the rule that failed non-idempotent mutations are not automatically replayed.
      - Do not confuse the new 350 ms minimum request spacing with the separate one-second spell preparation delay.
      - The preparation delay happens once before a casting run.
      - Minimum request spacing continues to apply between applicable API requests.

   - Expected behavior
      - Clicking `Spend All Mana` fills the cast-count value with the maximum currently affordable cast count.
      - The action updates all existing mana and effect previews.
      - It does not start casting automatically.
      - Pressing Cast starts a one-second `Preparing…` stage before any API request or gear mutation.
      - `Cancel` is visible on the active spell card during preparation and later casting stages.
      - Cancelling during preparation performs no mutations.
      - Cancelling after partial completion preserves completed casts and stops later casts.
      - Progress accurately reports preparation, current cast count, completion, cancellation, and failure.
      - Spell-card controls remain usable and aligned on desktop/browser and mobile.
      - The effective default Habitica API minimum request spacing is 350 ms when no configuration override is present.

   - Suggested implementation
      - Reuse the existing spell card cast-count state.
      - Add a helper or computed value for maximum affordable casts using the same validated mana/cost inputs as the existing preview.
      - Add a card-local `Spend All Mana` handler that updates count and triggers normal preview recalculation.
      - Extend the existing sequential casting state machine with an initial preparation stage.
      - Use an asynchronous one-second delay with the active casting cancellation token.
      - Reuse or extend the existing cancellation-token source owned by the casting session.
      - Add explicit cancellation checks before each execution step.
      - Add one card-local Cancel action bound to the active casting operation.
      - Ensure cancellation cleanup resets busy state and leaves progress/result messaging coherent.
      - Update API option and composition-root fallback values from 300 to 350.
      - Keep `appsettings.json` without a spacing override.
      - Update project documentation to describe:
         - Spend All Mana.
         - Preparing stage.
         - Card-local cancellation.
         - Partial-cast cancellation semantics.
         - 350 ms API spacing default.

   - UI acceptance criteria
      - Every eligible spell card has a `Spend All Mana` button near the cast-count control.
      - `Spend All Mana` is visually secondary to `Cast`.
      - Active spell card shows `Preparing…` for one second before execution.
      - Active spell card shows a `Cancel` button as soon as preparation begins.
      - Cancel remains accessible through the entire active flow.
      - The action/progress area does not overflow the spell card.
      - Desktop/browser layout remains aligned.
      - Mobile/narrow layout stacks or wraps without overlap, clipping, or stair-like controls.
      - Preparation-to-casting transition does not cause a disruptive layout jump.
      - Existing CRON-warning controls still fit and work.
      - Existing target, count, mana preview, auto-equip, progress, and recommendation controls still render.

   - Functional acceptance criteria
      - Maximum cast count equals `floor(available mana / mana cost)` for a valid spell.
      - Spend All Mana does not initiate an API request.
      - Preparation always occurs after final confirmation and before the first mutation.
      - Preparation waits approximately one second.
      - Cancelling preparation results in no API requests and no state changes.
      - Cancellation is checked before every cast and between sequential workflow steps.
      - Partial cancellation reports completed/requested counts correctly.
      - Successful casts are never rolled back.
      - Post-cancellation refresh leaves local snapshots consistent with the latest known server state.
      - Only one conflicting spell-casting operation can run at a time.
      - Default request spacing is 350 ms in `HabiticaApiClientOptions`.
      - `Program.cs` uses 350 ms as the fallback for `Habitica:MinRequestSpacingMilliseconds`.
      - `appsettings.json` does not override the value.
      - Existing rate-limit header handling and non-idempotent retry safeguards remain intact.

   - Tests
      - Add/update Spells page tests for:
         - `Spend All Mana` rendering on eligible spell cards.
         - Maximum affordable cast count calculation.
         - Mana/effect preview update after filling the count.
         - Unaffordable state.
         - Locked spell state.
         - Stale/missing snapshot state.
         - Zero/invalid mana-cost guard.
         - Preparing progress state.
         - Cancel button visibility only on the active spell card.
         - Desktop/action layout markup.
         - Narrow/mobile responsive classes or structure.
         - Existing target, count, Cast, auto-equip, CRON warning, and recommendation controls remaining present.
      - Add/update casting orchestration tests for:
         - One-second preparation before the first mutation.
         - No request during preparation.
         - Cancellation during preparation.
         - Cancellation before auto-equip.
         - Cancellation between equipment requests.
         - Cancellation before first cast.
         - Cancellation between casts.
         - Cancellation during request-spacing delay.
         - Cancellation before refresh.
         - Partial completion result.
         - Failure remaining distinct from cancellation.
         - Busy state cleared after cancellation.
         - Existing sequential cast order preserved.
      - Add/update API configuration tests for:
         - `HabiticaApiClientOptions` default equals 350.
         - `Program.cs` fallback equals 350 when configuration is absent.
         - Explicit `Habitica:MinRequestSpacingMilliseconds` configuration still overrides the default.
         - Current `appsettings.json` contains no override.

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

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
