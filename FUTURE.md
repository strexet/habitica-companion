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

- Fix Quests page finish prediction after first daily CRON
   - Description
      - The Quests page can show enough quest data to calculate the expected finish while still displaying `EXPECTED FINISH: Unknown`.
      - Reported example:
         - `CURRENT PROGRESS: 211.44 / 500 HP`
         - `PENDING PARTY PROGRESS: 1711.43 damage`
         - `EXPECTED FINISH: Unknown`
      - After one additional refresh, the same quest shows:
         - `EXPECTED FINISH: Today around 10:10`
         - `FINISHING MEMBER: Marek50818`
         - `TIMING CONFIDENCE: High`
         - `Expected to finish when Marek50818 checks in today around 10:10.`
      - The issue occurred during the user’s first login of the Habitica day:
         - The app refreshed before CRON had been processed.
         - The user then completed CRON.
         - The user opened the Quests page.
         - Current boss HP and pending party damage were visible.
         - Expected finish remained `Unknown`.
         - One more refresh produced the correct prediction.
      - This strongly suggests stale or inconsistently refreshed post-CRON prediction inputs.
      - Fix the post-CRON invalidation, refresh ordering, and derived-state recalculation so the complete quest prediction appears without requiring a second manual refresh.

   - Important reproduction scenario
      - Treat this sequence as the primary reproduction path:
         - Start the app on the first login of a new Habitica day.
         - Perform the initial app refresh before the user has processed CRON.
         - Confirm that cached snapshots represent the pre-CRON state.
         - Process CRON successfully.
         - Open the Quests page immediately after CRON.
         - Observe whether:
            - Boss HP is updated.
            - Pending party damage is updated.
            - Expected finish is still `Unknown`.
         - Do not perform another manual refresh.
      - The expected finish, finishing member, and timing confidence should be available after the successful CRON flow whenever enough prediction data exists.

   - Project context
      - The Quests page consumes cached local snapshots prepared by the sync workflow.
      - `/quests` refresh prioritizes domains such as:
         - Party.
         - User Profile.
         - Gear Catalog.
      - Refresh domains may complete independently and trigger per-domain cached-state reloads or UI notifications.
      - Quest completion prediction can depend on:
         - Current boss HP.
         - Per-member pending boss damage.
         - Quest participant membership.
         - Member `lastCron`.
         - Member average CRON time/history.
         - Inn state.
         - Inactivity eligibility.
         - User timezone/day-start context where relevant.
      - CRON changes several of these inputs.
      - The prediction should therefore be invalidated and recomputed after successful CRON processing.

   - Likely post-CRON consistency problem
      - CRON is not merely a timestamp update.
      - It can change:
         - User `lastCron`.
         - Quest damage applied to the boss.
         - Pending quest progress.
         - Daily/task state.
         - User stats and damage consequences.
         - Party-visible quest state.
         - Locally stored CRON history.
         - Average CRON timing used by the prediction model.
      - The current behavior may be combining:
         - Post-CRON boss HP.
         - Post-CRON or partially refreshed pending damage.
         - Pre-CRON `lastCron`.
         - Pre-CRON local CRON history.
         - A cached estimate calculated before post-CRON refresh completion.
      - This mixed state can explain why current progress looks correct while expected finish remains unavailable until another refresh.

   - Current problem
      - The first post-CRON page load provides current boss HP and pending party damage.
      - The page still renders `EXPECTED FINISH: Unknown`.
      - A second refresh immediately produces a valid finishing member and expected time.
      - Possible causes to investigate:
         - CRON response updates only part of the local state.
         - Party or member data is persisted after the prediction is calculated.
         - Local CRON history is updated after the Quests page has already built its view model.
         - Cached state reload occurs before all post-CRON data has been merged.
         - The estimate model is built from the previous party snapshot while progress UI uses the new snapshot.
         - Party-sync or local CRON-history changes do not trigger prediction recomputation.
         - The component computes the estimate only during initialization.
         - Multiple refresh notifications complete out of order.
         - A pre-CRON estimate remains cached after CRON.
         - An older refresh generation overwrites newer post-CRON state.

   - Investigation requirements
      - Trace the complete sequence from first refresh through CRON completion and Quests page rendering.
      - Record the order of:
         - Initial pre-CRON user refresh.
         - Initial pre-CRON party refresh.
         - CRON request.
         - CRON response handling.
         - Current-user snapshot persistence.
         - Task snapshot persistence.
         - Party group fetch.
         - Public party-member fetch.
         - Party snapshot normalization.
         - Party snapshot persistence.
         - Local CRON-history update.
         - Average CRON-time recalculation.
         - Shared party-sync merge where relevant.
         - Cached-state reload.
         - Quests page initialization/rerender.
         - Quest finish estimate calculation.
      - Identify which exact prediction input is missing or stale during the first `Unknown` result.
      - Compare estimator inputs before and after the second refresh.
      - Confirm whether the valid result depends on:
         - Updated member `party.quest.progress.up`.
         - Updated boss HP.
         - Updated `lastCron`.
         - Persisted CRON history.
         - Average CRON-time data.
         - Participant mapping.
         - Inn/inactivity eligibility.
      - Add diagnostics for prediction readiness, not only the final result.

   - CRON completion handling
      - After successful CRON:
         - Mark every affected cached domain as stale.
         - Refresh or replace the current user profile snapshot.
         - Refresh task state where CRON changed task completion/due state.
         - Refresh party and public party-member state.
         - Update the active quest state.
         - Update local CRON history using the new `lastCron`.
         - Recompute average CRON timing where applicable.
         - Rebuild the active quest derived model.
         - Recalculate expected finish.
      - Do not consider the post-CRON update complete until the derived quest prediction has been rebuilt.
      - Opening the Quests page immediately after CRON should use the latest post-CRON state.
      - The user should not need another manual refresh.

   - Refresh-domain invalidation after CRON
      - Explicitly identify every cache/domain affected by the CRON endpoint.
      - At minimum, investigate invalidation and refresh of:
         - User Profile.
         - Tasks.
         - Party group.
         - Public party members.
         - Active quest state.
         - Local CRON history.
         - Shared party-sync CRON state where applicable.
      - Do not rely only on the CRON response if it does not contain every field required by the estimator.
      - Do not update only the current user while leaving the party/quest derived model unchanged.
      - If User Profile is not required for a particular prediction input, do not unnecessarily block calculation on it.
      - If User Profile is required for timezone, day-start, or viewer-local CRON context, make that dependency explicit.
      - Gear Catalog should not block boss finish prediction unless the estimator genuinely uses it.

   - Post-CRON event propagation
      - Ensure successful CRON completion emits or invokes the same relevant state-change notifications as a normal refresh.
      - The Quests page should respond correctly when:
         - It was closed while CRON ran and opened immediately afterward.
         - It was already open while CRON ran.
         - It has cached page state from before CRON.
      - Invalidate any memoized/cached estimate created from pre-CRON data.
      - Prevent page initialization from reusing pre-CRON derived state when a newer CRON generation exists.
      - Do not require page navigation or a second refresh to trigger recomputation.

   - Refresh consistency requirements
      - Build the prediction from a consistent post-CRON snapshot generation.
      - Do not combine:
         - New boss HP with old member pending damage.
         - New pending damage with old participant state.
         - New member progress with stale `lastCron`.
         - New `lastCron` with stale average CRON history.
         - Post-CRON progress cards with a pre-CRON prediction object.
      - Rebuild the quest prediction only after all required refreshed inputs have been normalized and persisted.
      - If local/shared CRON history updates after party refresh, recompute after that update too.
      - Use refresh generation/version checks if callbacks can complete out of order.
      - Older pre-CRON callbacks must not overwrite a newer post-CRON result.

   - Derived-state invalidation
      - Invalidate the active quest estimate whenever any of these inputs change:
         - Active quest key or state.
         - Current boss HP.
         - Quest participant membership.
         - Member pending boss damage.
         - Member `lastCron`.
         - Member average CRON time/history.
         - Member Inn state.
         - Member inactivity eligibility.
         - Viewer timezone/day-start context if used.
         - CRON generation/version.
      - Recompute after cached state is reloaded.
      - Avoid retaining `Unknown` from an earlier partial state after valid inputs become available.
      - Avoid requiring a second page load, navigation, or refresh.

   - Atomic active-quest view model
      - Prefer one derived active-quest view model built from a consistent snapshot set.
      - These fields should be generated together:
         - Current progress.
         - Pending party progress.
         - Expected finish.
         - Finishing member.
         - Timing confidence.
         - Explanatory prediction text.
      - Do not calculate progress cards and expected finish through unrelated state paths that update independently.
      - If refresh is incomplete:
         - Preserve the previous complete prediction with a refreshing indicator; or
         - Show a temporary `Calculating…` state.
      - Do not prematurely replace a valid prediction with `Unknown` while required post-CRON dependencies are still loading.

   - Unknown state behavior
      - `EXPECTED FINISH: Unknown` should mean the app genuinely lacks enough valid data to calculate a prediction.
      - It should not be a transient result caused by refresh ordering.
      - If calculation is waiting for dependencies, prefer:
         - `Calculating…`
         - Or preserving the previous valid estimate.
      - Once enough data is available:
         - Show expected finish.
         - Show finishing member when identifiable.
         - Show timing confidence when a real timing estimate exists.
         - Show explanatory prediction text.
      - If prediction remains unavailable after refresh fully completes, record a meaningful diagnostic reason:
         - No eligible participants.
         - Pending eligible damage does not reach remaining boss HP.
         - Missing CRON timing history.
         - Missing member progress.
         - Missing participant mapping.
         - Stale or expired required data.
      - Preserve the simplified UI rule:
         - If prediction is genuinely unavailable, show only `EXPECTED FINISH: Unknown`.
         - Do not show `FINISHING MEMBER: Unknown`.
         - Do not show timing confidence without a valid timing estimate.

   - Refresh completion behavior
      - The Quests page should not consider refresh complete before the active quest view model is rebuilt.
      - If Party refresh has several stages, prediction recalculation should occur after the final relevant stage.
      - Deduplicated/background refreshes must not overwrite a newer complete estimate with older partial state.
      - Preserve valid cached local data when shared party-sync fails.
      - Party-sync failure should block prediction only when the missing input exists exclusively in party-sync data.

   - UI behavior
      - Current progress, pending damage, expected finish, finishing member, confidence, and explanation should update together.
      - During post-CRON recalculation, avoid flashing `Unknown` if calculation is still in progress.
      - The page may show a compact `Calculating…` state while waiting for required post-CRON data.
      - Do not introduce disruptive layout shifts.
      - Preserve existing quest details, participant information, reward data, links, and active-quest actions.

   - Expected behavior
      - The initial pre-CRON refresh may correctly show pre-CRON state.
      - After successful CRON, affected snapshots and derived models refresh automatically.
      - Opening the Quests page after CRON shows:
         - Updated boss HP.
         - Updated pending party damage.
         - Updated member CRON timing.
         - Expected finish when sufficient data exists.
         - Finishing member when identifiable.
         - Timing confidence when calculable.
      - No second manual refresh is required.
      - One successful post-CRON refresh/update produces a consistent active quest view model.

   - Suggested implementation
      - Inspect the current quest estimate factory/view-model construction.
      - Identify every estimator dependency explicitly.
      - Add a prediction-input readiness model.
      - Add a CRON generation/version marker to prevent pre-CRON state reuse where appropriate.
      - On successful CRON:
         - Invalidate affected cached domains.
         - Complete required refreshes.
         - Update local CRON-history-derived data.
         - Rebuild the quest prediction.
      - Trigger recomputation from the final relevant refresh callback rather than only component initialization.
      - Coalesce multiple domain callbacks into one derived-state rebuild where practical.
      - Ignore results from older refresh generations.
      - Keep progress and prediction fields in one active-quest view model.
      - Add diagnostics containing:
         - Refresh/CRON generation.
         - Quest key.
         - Remaining boss HP.
         - Eligible participant count.
         - Total eligible pending damage.
         - `lastCron` readiness.
         - CRON-history readiness.
         - Prediction availability reason.
      - Do not log credentials or sensitive API headers.

   - Acceptance criteria
      - The exact first-login sequence is fixed:
         - Initial refresh occurs before CRON.
         - User completes CRON.
         - User opens the Quests page.
         - Complete quest prediction appears without another refresh.
      - Successful CRON invalidates every stale quest-prediction dependency.
      - Post-CRON `lastCron` and local CRON history are available before prediction is finalized.
      - Current progress, pending party progress, and expected finish use the same post-CRON data generation.
      - If pending eligible damage defeats the boss and timing data exists:
         - Expected finish is shown.
         - Finishing member is shown.
         - Timing confidence is shown.
         - Explanation text is shown.
      - A pre-CRON cached prediction cannot overwrite the post-CRON result.
      - Older refresh callbacks cannot overwrite newer derived state.
      - A valid estimate is not replaced by transient `Unknown`.
      - Genuine unavailable cases still render only:
         - `EXPECTED FINISH: Unknown`
      - Existing active-quest progress, participant count, details, rewards, and member links remain functional.

   - Tests
      - Add tests for:
         - First refresh before CRON followed by successful CRON and immediate Quests page navigation.
         - CRON updates boss HP and `lastCron`, then prediction recomputes automatically.
         - Pre-CRON cached estimate is invalidated after CRON.
         - Quests page created after CRON receives the latest derived state.
         - Quests page already open during CRON receives and renders the updated state.
         - Party progress arrives before CRON-history timing data, then prediction recomputes.
         - CRON timing data arrives before party progress, then prediction recomputes.
         - Delayed party refresh after CRON does not leave prediction permanently `Unknown`.
         - Delayed CRON-history persistence triggers final prediction recomputation.
         - Older pre-CRON refresh generation cannot overwrite post-CRON state.
         - Previous valid prediction is preserved or marked calculating during partial refresh.
         - Sufficient pending damage plus valid timing identifies finishing member and expected time.
         - Insufficient pending damage produces a genuine unavailable prediction.
         - Missing CRON timing history produces a documented unavailable reason.
         - Inn and inactive members remain excluded correctly.
         - Refresh failure preserves the previous complete prediction with stale/failure indication.
         - Only one post-CRON update/refresh is required to produce the complete prediction.

- Verify incoming damage prediction and move it into the Dashboard CRON menu
   - Description
      - Review and correct the incoming damage prediction.
      - The currently calculated value appears too high.
      - Verify every formula, input, and damage source against:
         - The current official Habitica repository.
         - Official Habitica API behavior and data structures.
         - Existing project CRON rules and cached data.
      - Remove the standalone incoming-damage prediction section from the standard Dashboard page.
      - Damage information should be available only from the Dashboard CRON / Start New Day menu.
      - Merge incoming damage into the CRON flow as a compact, focused summary.
      - The merged CRON view should contain less irrelevant information than the current standalone damage prediction section.
      - It should answer only what the user needs before starting the new Habitica day:
         - Which incomplete Dailies can cause damage.
         - How much damage is currently expected.
         - Whether an active boss contributes additional damage.
         - Estimated HP after CRON.
         - Whether there is a knockout risk.
         - Which remaining Dailies can still be completed to reduce that risk.

   - Current project context
      - The Dashboard currently has:
         - A Start New Day / CRON flow.
         - A compact unfinished-Dailies list.
         - A separate incoming/pending damage estimate.
         - A knockout-risk warning.
         - Temporary equipment recommendations for CRON.
      - The current incoming damage implementation uses `PendingDamageEstimateFactory`.
      - It combines:
         - Estimated damage from incomplete Dailies.
         - Saved active boss quest pending damage.
      - `PendingDamageEstimateFactory.GetIncompleteDailies` currently selects:
         - Task type is Daily.
         - Task is incomplete.
         - `isDue` is not explicitly `false`.
      - The same Daily selector is shared by:
         - The incoming damage estimate.
         - The Dashboard CRON unfinished-Dailies list.
         - The Spells CRON warning list.
      - Preserve the shared selector architecture, but correct it if its eligibility rules do not match official Habitica behavior.

   - Main goals
      - Determine why incoming damage may be overestimated.
      - Correct the formula and source selection.
      - Remove the separate standard Dashboard damage card.
      - Present a minimized damage summary only inside the CRON / Start New Day menu.
      - Avoid duplicating the same task list, damage warning, or risk information across multiple Dashboard sections.
      - Keep the CRON menu actionable and easy to scan.

   - Standard Dashboard cleanup
      - Remove the standalone incoming-damage prediction block from the normal Dashboard page.
      - Do not show detailed damage calculations outside the CRON menu.
      - Do not keep a second compact duplicate of the same prediction on the main Dashboard.
      - The standard Dashboard may still show a small CRON-required indicator or entry point if already part of the design.
      - Detailed damage information should appear only after opening the CRON / Start New Day section.
      - Remove any leftover empty spacing, duplicate warning banners, or detached damage-related helper text.

   - CRON-only damage information
      - Damage prediction should live inside the Dashboard CRON / Start New Day menu.
      - The CRON menu should include only the most useful damage information by default.
      - Keep the primary view compact.
      - Suggested default summary:
         - `Estimated damage: 12.4 HP`
         - `HP after CRON: 37.6 / 50`
         - Risk badge:
            - `Safe`
            - `Warning`
            - `Knockout risk`
         - Optional concise breakdown:
            - `Dailies: 7.4 HP`
            - `Boss: 5.0 HP`
      - Keep detailed formulas, source explanations, unavailable inputs, and diagnostics behind:
         - An expandable details section.
         - A tooltip.
         - Or another secondary disclosure control.
      - Do not display large blocks of explanatory text in the default CRON view.
      - Do not show technical fields that do not help the user decide whether to complete tasks, change equipment, or start the new day.

   - Information to keep in the compact CRON view
      - Current CRON-needed state.
      - Estimated total incoming damage.
      - Current HP.
      - Estimated HP after CRON.
      - Knockout-risk state.
      - Confirmed due unfinished Dailies.
      - Inline task-completion controls.
      - Boss damage component when applicable.
      - Temporary CRON equipment recommendation when useful.
      - Start New Day action.
      - Progress, result, or error state.

   - Information to minimize or hide by default
      - Long descriptions of every formula.
      - Repeated current HP values in multiple places.
      - Duplicate damage totals.
      - Raw API field names.
      - Repeated task counts that are already clear from the list.
      - Large source-by-source explanations.
      - Internal confidence/debug information.
      - Damage sources that do not apply to the current user.
      - Party-wide totals when the section is predicting the current user’s CRON result.
      - Historical values unrelated to the next CRON.
      - Technical uncertainty details unless the user opens the expanded view.

   - Main investigation goal
      - Do not reduce the displayed value heuristically.
      - Trace the estimate back to its exact sources and formulas.
      - Compare the calculated preview with what Habitica actually applies when CRON runs.
      - Separate:
         - Direct damage from unfinished Dailies.
         - Boss attack damage caused by those unfinished Dailies.
         - Already-saved pending boss damage.
         - Damage caused by other party members.
         - Damage that has already been applied.
         - Damage expected at the current user’s next CRON.

   - Possible overestimation causes to verify
      - Incomplete Dailies with missing or unknown `isDue` may currently be treated as due.
      - Dailies excluded by the user’s weekday or recurrence schedule may be included.
      - Future-start Dailies may be included.
      - Every-X-days Dailies may be treated as due when they are not.
      - Group-plan Dailies may be included even when assignment, approval, or completion rules prevent damage.
      - Personal Daily damage and boss damage may be combined incorrectly.
      - Saved `party.quest.progress.down` may already include damage derived from the same missed Dailies, causing double counting.
      - Party-level pending boss damage may be mistaken for damage that will be applied again to the current user.
      - Damage already applied during an earlier CRON may remain in the estimate.
      - Damage from other party members may be included in a current-user Start New Day estimate.
      - Boss damage may be multiplied by participant count when the displayed value should represent damage to one user.
      - The estimate may ignore:
         - Constitution.
         - Task value/color.
         - Task difficulty.
         - Inn/resting state.
         - Paused damage.
         - Quest participation.
         - Boss strength.
         - Current-user eligibility for boss damage.
      - The estimate may simulate multiple missed days even though Habitica prevents repeated damage from the same Daily across an inactive period.
      - Cached pre-CRON and post-CRON values may be combined.

   - Official source verification
      - Inspect the current Habitica repository implementation for:
         - CRON Daily selection.
         - Daily due-date calculation.
         - Direct user damage from incomplete Dailies.
         - Boss quest damage from incomplete Dailies.
         - `party.quest.progress.down`.
         - Quest participant damage application.
         - Inn/resting behavior.
         - Paused-damage behavior.
         - Multiple missed-day handling.
         - Group-plan Daily handling.
      - Confirm the exact meaning and lifecycle of:
         - User `party.quest.progress.down`.
         - Party/group quest pending damage fields.
         - Any API field currently mapped as active boss pending damage.
      - Determine whether those fields represent:
         - Damage accumulated but not yet applied.
         - Damage already calculated from missed Dailies.
         - Damage from the current user.
         - Damage from the party.
         - Damage intended for each participant.
      - Verify whether the official API exposes enough data for an exact prediction.
      - Where server-only internals prevent exact prediction:
         - Mark the result clearly as an estimate.
         - Use the closest source-backed formula.
         - Show unavailable components separately in expanded details.
         - Do not fabricate precision.

   - Daily eligibility rules
      - Replace the current loose condition of “not explicitly `isDue: false`” if it can include unknown or non-due tasks.
      - A Daily should contribute to the CRON damage estimate only when it is confirmed to be damage-eligible for the evaluated Habitica day.
      - Verify eligibility using the official due logic and relevant fields:
         - `isDue`
         - `repeat`
         - `frequency`
         - `everyX`
         - `startDate`
         - `nextDue`
         - Group-plan assignment/completion state
         - User Custom Day Start
         - Timezone context
      - If due state cannot be determined:
         - Do not silently include the Daily in the numeric total.
         - Put it in an unavailable or uncertain source list.
         - Keep this uncertainty in expanded details rather than cluttering the compact summary.
      - Continue sharing one eligibility helper between:
         - CRON task list.
         - Incoming damage estimate.
         - Spells CRON warning.
      - Align this with the separate task to remove non-damaging Dailies from the CRON list.

   - Damage source separation
      - Build the estimate from explicit components.
      - At minimum, distinguish:
         - `Incomplete Daily damage`
         - `Boss quest damage`
         - `Other/unavailable damage sources`
         - `Estimated total`
      - Do not add a source twice.
      - For each source, record internally:
         - Where the data came from.
         - Whether it is exact or estimated.
         - Whether it is already included in another source.
         - Whether it applies to the current user.
      - If `party.quest.progress.down` already represents boss damage from the same unfinished Dailies:
         - Do not calculate and add the same boss damage again.
      - If it represents previously accumulated damage that will be applied independently:
         - Include it once with a clear label.
      - If its meaning cannot be verified:
         - Exclude it from the confident total.
         - Show it as unavailable or uncertain only in expanded details.

   - Personal Daily damage
      - Verify the exact server formula for damage caused directly by each unfinished due Daily.
      - Confirm how the formula uses:
         - Task value/color.
         - Task priority/difficulty.
         - User Constitution.
         - Buffed Constitution.
         - Any level/stat scaling.
         - Checklist or group-plan state if applicable.
      - Use the user’s relevant pre-CRON stats.
      - If temporary CRON equipment optimization is enabled:
         - Show which equipment/stat state the estimate assumes.
         - Recalculate the estimate using the proposed temporary gear if the preview is intended to show post-equip damage.
         - Clearly distinguish current-gear and optimized-gear estimates if both are shown.
      - Do not label an assumption-based value as exact.

   - Boss quest damage
      - Verify the official formula for boss damage caused by unfinished Dailies.
      - Confirm:
         - How boss strength is applied.
         - Whether task value or difficulty affects boss damage.
         - Whether Constitution reduces boss damage.
         - Whether the damage is the same for each participant.
         - Whether the current user receives both personal Daily damage and boss damage.
         - Whether non-participants are excluded.
         - Whether resting in the Inn skips boss damage.
      - Make sure the displayed amount represents incoming damage to the current user, not total damage across the party.
      - Do not multiply a per-participant value by party size for a personal health-loss prediction.
      - Do not include active quest boss damage when:
         - There is no active boss quest.
         - The user is not a participant.
         - The user is resting in the Inn and official behavior skips it.
         - Damage is paused.
         - The source has already been applied.

   - Other party-member damage
      - Keep the merged CRON section scoped to damage expected from the authenticated user’s own next CRON.
      - Do not mix uncertain future boss attacks from other members into the current-user CRON total.
      - If other-member pending risk is useful, show it separately and only in expanded details.
      - Never present party-wide cumulative risk as the damage caused by pressing `Start New Day`.

   - Multiple missed days
      - Do not multiply each incomplete Daily by the number of days since `lastCron` unless the official repository does so for that exact case.
      - Habitica intentionally avoids repeatedly damaging the user for the same Daily across every inactive day.
      - Use actual CRON and due behavior rather than a local “days missed × Daily damage” simulation.
      - Add tests for users returning after several inactive days.

   - Inn and paused-damage behavior
      - Verify and preserve official behavior for users resting in the Inn.
      - When the user is resting:
         - CRON still runs.
         - Negative Daily damage may be skipped.
         - Boss quest damage consequences may be skipped.
         - Other CRON effects still occur.
      - The CRON section should not display a large incoming-damage value if official CRON will not apply that damage.
      - Show a concise compact explanation such as:
         - `Damage is paused while resting in the Inn.`
      - Reuse any existing paused-damage or Inn state from the cached user snapshot.

   - Merged CRON section layout
      - Replace the separate Dashboard CRON and incoming-damage blocks with one unified CRON / Start New Day menu.
      - Suggested section title:
         - `START NEW DAY`
      - Default compact summary:
         - `Estimated damage: 12.4 HP`
         - `HP after CRON: 37.6 / 50`
         - Risk badge:
            - `Safe`
            - `Warning`
            - `Knockout risk`
      - Optional compact breakdown:
         - `Dailies: 7.4 HP`
         - `Boss: 5.0 HP`
      - Due unfinished Dailies:
         - Reuse `CronUnfinishedDailiesMiniList`.
         - Show only confirmed due and damaging Dailies.
         - Keep inline completion controls.
      - Preparation:
         - Temporary equipment recommendation.
         - Current versus recommended CON/survival estimate where supported.
      - Action:
         - `Start New Day`
         - Confirmation copy.
         - Progress/result state.
      - Detailed formulas, unavailable sources, and confidence explanation should be collapsed by default.

   - Dynamic recalculation
      - Recalculate the merged CRON section whenever any relevant state changes:
         - A Daily is completed.
         - A Daily’s due state changes.
         - Equipment preview/optimization is toggled.
         - Current equipment changes.
         - Constitution changes.
         - Active quest changes.
         - Quest participant state changes.
         - Boss data changes.
         - Inn/paused-damage state changes.
         - User HP changes.
         - User/tasks/party snapshots refresh.
         - CRON completes.
      - After inline completion of a Daily:
         - Remove it from the CRON list.
         - Recalculate personal Daily damage.
         - Recalculate boss damage if that Daily contributes.
         - Recalculate estimated HP after CRON.
         - Recalculate risk level.
      - After CRON:
         - Clear or replace the estimate with the completed result.
         - Do not leave stale pre-CRON damage displayed.

   - Prediction confidence and unavailable sources
      - Preserve included and unavailable sources separately in the underlying model.
      - Keep confidence details secondary.
      - Suggested readiness states:
         - `High`
         - `Estimated`
         - `Incomplete`
      - In the compact CRON view:
         - Show confidence only if it materially changes user interpretation.
         - Prefer a small warning icon or concise label over a large information block.
      - Do not include unavailable sources as zero without explanation.
      - Do not include unknown Dailies as definitely damaging.
      - Keep detailed confidence/source explanations in the expanded view.

   - Risk thresholds
      - Preserve or review the current thresholds:
         - Danger when estimated damage is greater than or equal to current HP.
         - Warning when estimated damage is at least 75% of current HP.
         - Informational when estimated damage is greater than zero.
      - Apply thresholds only to the corrected current-user damage total.
      - If the estimate is incomplete:
         - Avoid a definitive `Safe` state.
         - Use wording such as `Damage estimate incomplete`.
      - Calculate estimated remaining HP without misleading negative formatting.
      - Keep knockout warning prominent.

   - UI behavior
      - The standard Dashboard no longer contains an incoming-damage prediction card.
      - Damage information appears only inside the CRON / Start New Day menu.
      - The CRON view is more compact than the current damage prediction section.
      - Use one clear hierarchy:
         - CRON status.
         - Estimated consequence.
         - Tasks that can still be completed.
         - Optional preparation.
         - Start New Day action.
      - Keep detailed formula information collapsed by default.
      - Avoid overwhelming text.
      - Preserve desktop and mobile responsiveness.
      - Keep inputs, buttons, mini-list rows, and damage values aligned.
      - Avoid layout jumps as Dailies are completed and the estimate changes.
      - Ensure warning and danger states remain readable in all color schemes.

   - Diagnostics
      - Add structured diagnostics for the estimate:
         - Evaluated Habitica day.
         - Current-user CRON-needed state.
         - Number of incomplete Dailies.
         - Number confirmed due.
         - Number excluded as non-due.
         - Number with unknown due state.
         - Estimated personal Daily damage.
         - Estimated boss damage.
         - Saved pending-down value.
         - Whether saved pending-down was included or excluded.
         - Current Constitution used.
         - Inn/paused-damage state.
         - Quest participation state.
         - Final total.
         - Confidence/readiness reason.
      - Do not log task text, credentials, or sensitive API headers unless already allowed by diagnostics policy.

   - Expected behavior
      - Incoming damage matches official Habitica CRON behavior as closely as available API data allows.
      - Non-due Dailies do not inflate the estimate.
      - Unknown due state is not silently counted as damage.
      - Personal Daily damage and boss damage are calculated separately.
      - The same boss damage source is not counted twice.
      - Party-wide damage is not mistaken for personal incoming damage.
      - Inn and paused-damage state are respected.
      - Multiple missed days do not multiply damage incorrectly.
      - The standard Dashboard contains no standalone damage prediction card.
      - Damage information exists only inside the CRON / Start New Day menu.
      - The CRON damage summary is smaller, clearer, and more relevant than the current standalone section.
      - Completing a Daily immediately lowers the predicted damage.
      - The Start New Day action uses the same task set and assumptions shown by the estimate.

   - Suggested implementation
      - Inspect `PendingDamageEstimateFactory`.
      - Inspect `GetIncompleteDailies`.
      - Inspect the active quest snapshot mapping for pending/down damage.
      - Create an explicit `CronDamageEstimate` model containing:
         - Confirmed due Dailies.
         - Excluded Dailies.
         - Unknown-eligibility Dailies.
         - Personal Daily damage.
         - Boss quest damage.
         - Other pending damage.
         - Estimated total.
         - Current HP.
         - Estimated remaining HP.
         - Risk state.
         - Confidence/readiness.
         - Source explanations.
      - Move official formula reproduction into `Habitica.Rules`.
      - Keep the Dashboard responsible only for rendering and invoking actions.
      - Reuse the corrected selector for:
         - Merged Dashboard CRON section.
         - Spells CRON warning.
         - Any other due-Daily preview.
      - Remove the old standalone incoming-damage block from the standard Dashboard.
      - Integrate a minimized summary into the CRON / Start New Day menu.
      - Put detailed source/formula information behind an expandable section.
      - Update `FEATURES.md`, CRON documentation, and UX guidance.
      - Add source comments pointing to the official Habitica implementation used as reference.

   - Acceptance criteria
      - Standard Dashboard no longer renders the standalone incoming-damage prediction section.
      - Incoming damage is available only inside the CRON / Start New Day menu.
      - The CRON damage summary contains less irrelevant information than the current standalone section.
      - Compact summary shows:
         - Estimated damage.
         - Estimated HP after CRON.
         - Risk state.
         - Due damaging Dailies.
         - Boss contribution when applicable.
      - Detailed formulas and unavailable source explanations are collapsed by default.
      - Every Daily included in the numeric estimate is confirmed due and damage-eligible.
      - Dailies with `isDue: false` are excluded.
      - Dailies with unknown due state are separated rather than silently included.
      - Schedule, recurrence, start date, Custom Day Start, and timezone behavior match official Habitica rules.
      - Personal Daily damage formula matches the current official implementation or is clearly marked approximate.
      - Boss quest damage formula matches the current official implementation or is clearly marked approximate.
      - `party.quest.progress.down` or equivalent pending damage is interpreted correctly.
      - No damage source is double-counted.
      - Current-user damage is not multiplied by party size.
      - Non-participants do not receive predicted quest damage.
      - Inn/paused-damage users do not receive damage official CRON would skip.
      - Several missed days do not incorrectly replay the same Daily damage.
      - Completing a Daily updates list, total damage, remaining HP, and risk state.
      - Existing Start New Day confirmation, temporary gear optimization, inline Daily completion, progress, and result handling still work.
      - After successful CRON, stale damage prediction is cleared.
      - Mobile and desktop layouts remain aligned and readable.

   - Tests
      - Add rules tests for:
         - Confirmed due incomplete Daily.
         - Completed due Daily.
         - Explicitly non-due Daily.
         - Unknown `isDue`.
         - Weekday schedule.
         - Every-X-days schedule.
         - Future start date.
         - Custom Day Start boundary.
         - Timezone boundary.
         - Personal damage with different task values.
         - Personal damage with different priorities.
         - Personal damage with different Constitution values.
         - Boss damage with active quest.
         - No active quest.
         - User not participating.
         - Inn/resting state.
         - Paused-damage state.
         - Multiple missed days.
         - Group-plan Daily edge states.
         - Saved pending-down value without double counting.
      - Add Dashboard component tests for:
         - Standalone damage card is absent from the standard Dashboard.
         - Unified compact Start New Day section.
         - Compact damage summary.
         - Optional damage breakdown.
         - Expanded source/formula details.
         - Due-Daily mini list.
         - Unknown-source warning.
         - Estimated HP after CRON.
         - Safe, warning, and knockout-risk states.
         - Inline Daily completion recalculation.
         - Temporary equipment preview.
         - Mobile and desktop layout structure.
         - Successful CRON clearing the estimate.
         - Refresh failure preserving state with a stale indication.
      - Add integration/comparison tests using representative official Habitica scenarios:
         - No quest and one missed Daily.
         - Boss quest and one missed Daily.
         - Several missed Dailies.
         - Mixed due and non-due Dailies.
         - User in the Inn.
         - User returning after multiple inactive days.
         - Existing non-zero pending-down value.   

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
