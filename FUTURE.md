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

* Fix Start New Day estimated-damage styling and verify CRON damage formula
   * Description
      * Fix the CSS/theme styling for the compact ESTIMATED DAMAGE section inside the Dashboard Start New Day / CRON panel.
      * The section currently appears visually detached from the active color theme.
      * It should use the same theme tokens, typography, borders, surfaces, spacing, and warning styles as the rest of the app.
      * Also verify the estimated damage formula against the current official Habitica GitHub/API implementation.
      * The current estimate appears to overshoot the damage actually applied after CRON.
      * The estimate may not be accounting for important parameters such as user stats, Constitution, buffs, task value, task priority, boss mechanics, or other official CRON modifiers.
   * Current problematic UI example
      * Current copy/layout includes:
         * ESTIMATED DAMAGE 29,2 HP
         * HP after CRON: 20,8 / 50
         * Incomplete estimate
         * Due Dailies: 29,2 HP
         * 12 confirmed due unfinished Daily tasks using local difficulty-weight estimate.
         * Party boss damage is unavailable because the synced quest state has no pending boss damage.
         * Negative Habit damage is not included because pending negative Habit state is not available in saved task data.
         * Inn and paused-damage state are not included because saved account data does not expose the official CRON damage pause flag.
      * Problems:
         * The visual block does not look theme-aware.
         * The expanded details are too prominent.
         * The numeric value appears too high compared with actual post-CRON damage.
         * The wording suggests a technical estimate but does not clearly explain which parts are approximate and why.
   * Source verification requirements
      * Inspect the current official Habitica server source before finalizing the damage formula.
      * Inspect official API behavior for:
         * CRON.
         * Due Dailies.
         * User damage.
         * Boss quest damage.
         * Inn/resting or pause-damage behavior.
         * Stat/buff contribution to damage reduction.
      * Inspect the current project code and docs for:
         * PendingDamageEstimateFactory
         * PlayerDamageEstimator
         * BossDamageEstimator
         * CronDamageEstimate
         * Start New Day / Dashboard rendering component.
         * Task snapshot isDue handling.
         * Account snapshot stat and buff fields.
         * Quest pending damage mapping.
      * Do not rely on the current local difficulty-weight estimate until it is checked against official behavior.
      * If the official formula cannot be reproduced exactly from saved API data:
         * Keep the value labeled approximate.
         * Show unavailable parameters in collapsed details only.
         * Avoid exact-sounding wording.
      * Compare the estimate against at least one real before/after CRON scenario when possible.
   * Formula accuracy requirements
      * Verify whether the current estimate accounts for:
         * User Constitution.
         * Buffed Constitution.
         * Other relevant stats.
         * Task value/color.
         * Task priority/difficulty.
         * Due state.
         * Custom Day Start.
         * Boss quest participation.
         * Boss strength.
         * Saved quest.progress.down semantics.
         * Inn/resting state.
         * Pause-damage state.
         * Multiple missed-day behavior.
      * Do not count unknown due-state Dailies as damaging.
      * Do not count non-due Dailies as damaging.
      * Do not double-count boss damage already represented by synced pending damage.
      * Do not include party-wide damage as current-user damage.
      * Do not include unavailable negative Habit damage in the numeric total unless official/source-backed data exists.
      * If negative Habit pending damage cannot be known from saved task data, keep it excluded and mention this only in collapsed details.
      * If Inn or pause-damage state cannot be known from saved account data, do not claim the estimate is exact.
      * If stats or buffs are missing, degrade confidence instead of pretending the local estimate is complete.
   * Calibration requirements
      * Add a focused comparison workflow:
         * Capture current HP before CRON.
         * Capture due unfinished Dailies included in the estimate.
         * Capture relevant stats and buffs.
         * Capture quest state.
         * Run CRON.
         * Refresh account/tasks/party state.
         * Compare actual HP delta with predicted damage.
      * Use this comparison to identify which parameters the local estimate is missing.
      * Add diagnostics so future mismatches can be investigated without guessing.
      * Do not silently lower the estimate with an arbitrary multiplier.
      * Fix the formula or clearly downgrade confidence.
   * CSS and theme requirements
      * Remove hardcoded colors that bypass the active color scheme.
      * Use existing app theme tokens/classes for:
         * Card/surface background.
         * Border.
         * Primary text.
         * Secondary text.
         * Danger/warning/safe states.
         * Muted explanatory text.
         * Collapsed details.
         * Stat chips or value rows.
      * Ensure the compact CRON damage section works in:
         * Dark themes.
         * Light themes.
         * High-contrast/custom themes.
         * Mobile layout.
         * Desktop layout.
      * Ensure warning/danger styles remain readable.
      * Ensure the block does not visually look like unstyled browser/default markup.
   * UI behavior
      * Keep the default damage view compact.
      * Default view should show:
         * Estimated total damage.
         * HP after CRON.
         * Risk or confidence badge.
         * Compact Daily/Boss breakdown when available.
      * Keep long explanation text inside collapsed Estimate details.
      * Avoid showing large paragraphs in the default Start New Day view.
      * Make unavailable-source details less visually dominant.
      * Do not display a large standalone damage card on the standard Dashboard.
      * Damage estimate remains inside the Start New Day / CRON panel only.
   * Suggested wording
      * Prefer concise default labels:
         * Estimated damage: 29.2 HP
         * HP after CRON: 20.8 / 50
         * Estimate incomplete
         * Dailies: 29.2 HP
         * Boss: unavailable
      * In expanded details, use clearer uncertainty wording:
         * This estimate uses confirmed due unfinished Dailies from the latest task refresh.
         * Some official CRON modifiers may be unavailable in cached data, so the final damage can differ.
         * Boss damage is not included because no current-user pending boss damage was available in the synced quest state.
      * Avoid making unavailable inputs look like additional damage sources.
      * Avoid technical wall-of-text in the default visible state.
   * Data/model behavior
      * The UI should render confidence based on model state, not string matching.
      * Keep separate fields for:
         * Estimated Daily damage.
         * Estimated boss damage.
         * Excluded/unknown due Dailies.
         * Unavailable negative Habit damage.
         * Missing official pause/Inn data.
         * Confidence/readiness.
         * Source notes.
      * Keep numeric totals separate from explanatory notes.
      * Do not mix unavailable values into the total.
      * Recalculate after:
         * Task refresh.
         * Daily completion.
         * Equipment/stat preview change.
         * Party/quest refresh.
         * Health potion purchase.
         * CRON completion.
   * Diagnostics
      * Add structured diagnostics for:
         * Included due Daily IDs/count.
         * Excluded Daily IDs/count and reasons.
         * Task values and priorities used.
         * User stats and buffs used.
         * Formula branch used.
         * Quest/boss state used.
         * Estimate confidence.
         * HP before CRON.
         * Predicted damage.
         * Actual HP delta after CRON when available.
         * Difference between predicted and actual damage.
      * Do not log task text if diagnostics policy avoids user content.
      * Do not log credentials or sensitive headers.
   * Expected behavior
      * The Start New Day damage estimate uses the active theme correctly.
      * The section visually matches the rest of the app.
      * The default view is compact and readable.
      * Expanded details are still available but not visually overwhelming.
      * The numeric estimate is checked against official Habitica behavior.
      * If exact prediction is not possible, the UI clearly marks the estimate as approximate or incomplete.
      * The estimate no longer overshoots actual CRON damage because of missing known parameters that the app can access.
      * Missing inaccessible parameters are represented as confidence limitations, not fake precision.
   * Suggested implementation
      * Inspect the Start New Day damage component markup and CSS.
      * Replace local/hardcoded styling with existing theme-aware utility classes or CSS variables.
      * Inspect PendingDamageEstimateFactory and related damage estimators.
      * Compare local formula with official Habitica server/API behavior.
      * Add or update a dedicated CronDamageEstimate confidence model if needed.
      * Keep unavailable-source explanations collapsed by default.
      * Add a small local calibration/debug summary in diagnostics, not in normal UI.
      * Update FEATURES.md, CRON.md, HABITICA_API.md, and docs/UX_UI_MANIFEST.md if formula behavior or uncertainty wording changes.
   * Acceptance criteria
      * ESTIMATED DAMAGE block uses the active color theme.
      * No unthemed/default-looking CSS remains in the section.
      * The section is readable in light, dark, and custom themes.
      * Default damage UI is compact.
      * Long unavailable-source explanations are collapsed by default.
      * Formula is verified against current official Habitica source/API behavior.
      * User stats and accessible official modifiers are included when available.
      * Missing inaccessible modifiers lower confidence instead of being ignored silently.
      * Unknown or unavailable damage sources are not included in the numeric total.
      * Numeric estimate no longer systematically exceeds actual CRON damage because of known missing accessible parameters.
      * Estimate is labeled approximate/incomplete when exact prediction is impossible.
      * Daily completion, stat/equipment changes, potion purchase, and CRON completion recalculate or clear the estimate correctly.
   * Tests
      * Add UI/component tests for:
         * Theme classes/tokens applied to compact damage summary.
         * Light theme readability.
         * Dark theme readability.
         * Custom theme readability.
         * Collapsed details by default.
         * Expanded estimate details.
         * Compact mobile layout.
         * Compact desktop layout.
      * Add rules/model tests for:
         * Due Daily damage with stats.
         * Due Daily damage with buffs.
         * Due Daily damage with task value.
         * Due Daily damage with priority/difficulty.
         * Non-due Daily excluded.
         * Unknown due-state Daily excluded from numeric total.
         * Boss damage unavailable.
         * Boss damage included only when source-backed.
         * Inn/pause unavailable reduces confidence.
         * Missing stats reduces confidence.
         * Actual-vs-estimated comparison model.
      * Add integration-style tests for:
         * Completing a Daily updates estimate.
         * Running CRON clears stale estimate.
         * Health potion purchase updates HP-after-CRON.
         * Unavailable sources remain in details only.
* Improve Blessing effect preview wording and add low-value healing warnings
   * Description
      * Improve the EFFECT PREVIEW wording for the Healer Blessing spell.
      * The current copy is confusing:
         * Restores approximately 0-6,62 HP per covered party member. Total for 3 casts: approximately 497,1 effective party HP restored.
      * Avoid showing a large aggregate party HP total as the primary result.
      * Prefer a per-party-member description that is easier to understand.
      * Add warnings when most party members are already too close to full HP for the spell to be useful.
      * Add a stronger warning when all or almost all party members are already near full HP and no meaningful healing is needed.
   * Current problematic UI example
      * Current effect preview:
         * Restores approximately 0-6,62 HP per covered party member. Total for 3 casts: approximately 497,1 effective party HP restored.
      * Problems:
         * The 0-6,62 HP range is hard to interpret.
         * The aggregate 497,1 effective party HP restored feels misleading or too abstract.
         * The wording does not clearly say what each party member receives per cast or for all casts.
         * The preview does not warn clearly when most healing will be wasted because party members are already close to full HP.
         * The preview does not clearly say when healing is not needed.
   * Required wording direction
      * Prefer wording like:
         * Restores approximately X HP per party member.
         * Total for N casts: approximately Y HP per party member.
      * Use per-member totals as the primary explanation.
      * Do not use aggregate party-wide HP as the main displayed value.
      * If aggregate effective healing is kept for diagnostics or expanded details, keep it secondary and clearly labeled.
      * Avoid the word overshoot in user-facing copy.
      * Use friendlier wording such as:
         * Most members are already near full HP, so part of this healing would have no effect.
         * Healing value is limited because many members are already close to full HP.
         * Party HP is already high; Blessing may not be worth casting right now.
         * No meaningful healing is needed right now.
   * Source verification requirements
      * Inspect the current project spell-estimation code for:
         * Blessing
         * Healer spell preview model.
         * Party HP coverage logic.
         * Per-member healing cap.
         * Multi-cast preview calculation.
         * Fresh/stale party snapshot handling.
      * Check the current official Habitica spell definition/source for Blessing before changing the formula.
      * Verify whether the formula should use:
         * Intelligence.
         * Buffed stats.
         * Level bonus.
         * Equipment preview stats.
         * Number of casts.
         * Party member missing HP cap.
      * Do not change the spell formula merely to improve wording unless the source check shows the formula is wrong.
   * Preview calculation requirements
      * Keep separate values for:
         * Raw healing per member per cast.
         * Raw healing per member for selected cast count.
         * Effective healing per member after HP cap.
         * Number of party members covered by fresh HP data.
         * Number of party members who would receive full value.
         * Number of party members who would receive partial value.
         * Number of party members who would receive no value because they are full or nearly full.
      * The default visible text should prioritize per-member values.
      * If member HP data is fresh:
         * Cap each member’s effective healing by their missing HP.
         * Use warnings when healing value is mostly wasted.
      * If member HP data is stale or incomplete:
         * Show the raw per-member estimate.
         * Add a concise note that actual effective healing depends on current party HP.
         * Do not invent effective healing for members with unknown HP.
   * Suggested default copy
      * For one cast with useful healing:
         * Restores approximately X HP per party member.
      * For multiple casts:
         * Restores approximately X HP per party member per cast.
         * Total for N casts: approximately Y HP per party member.
      * If fresh HP capping is available:
         * Effective healing may be lower for members already near full HP.
      * If the preview is capped for many members:
         * Most party members are already near full HP, so much of this healing would have no effect.
      * If all or almost all members are healthy:
         * Party HP is already high. Blessing is probably not needed right now.
      * If nobody needs meaningful healing:
         * No meaningful healing is needed right now.
      * If some party members lack HP data:
         * Some party HP data is unavailable, so effective healing may differ.
   * Warning thresholds
      * Add a warning when more than half of covered party members would not receive the full raw healing value because they are already too close to full HP.
      * Do not describe this as overshoot.
      * Use wording such as:
         * Healing value is limited because most covered members are already near full HP.
      * Add a stronger warning when all or almost all covered party members would receive little or no effective healing.
      * Suggested threshold:
         * More than 50% capped or no-effect members:
            * show limited-value warning.
         * At least 80% capped/no-effect members, or total effective healing is near zero:
            * show low-need warning.
         * 100% no-effect members:
            * show No meaningful healing is needed right now.
      * Let implementation choose exact thresholds, but they must be deterministic and covered by tests.
      * Make the thresholds configurable or centralized if the spell preview model already has similar warning logic.
   * UI behavior
      * Keep EFFECT PREVIEW concise.
      * Show the primary per-member estimate first.
      * Show warnings below the primary estimate.
      * Use warning styling that matches the active theme.
      * Do not make warnings look like errors unless casting would fail.
      * Do not hide the Cast button solely because healing is inefficient.
      * Keep the warning near the Cast button so the user sees it before spending mana.
      * Avoid large paragraphs and aggregate party-total numbers in the default view.
      * Keep detailed member coverage in expanded details if needed.
   * Multi-cast behavior
      * For N casts:
         * Per-cast line should reflect one cast.
         * Total line should reflect total healing per party member across all selected casts.
      * Example structure:
         * Restores approximately 6.62 HP per party member per cast.
         * Total for 3 casts: approximately 19.86 HP per party member.
      * If effective healing is capped:
         * Do not claim every member will receive the full total.
         * Add a warning or note:
            * Effective healing may be lower for members already near full HP.
      * If most members are capped:
         * Show the limited-value warning.
      * If almost nobody needs healing:
         * Show the low-need warning.
   * Data/model behavior
      * Avoid embedding preview logic directly in UI text.
      * Spell preview model should expose:
         * RawHealPerMemberPerCast
         * RawHealPerMemberTotal
         * CoveredMemberCount
         * FullValueMemberCount
         * PartialValueMemberCount
         * NoEffectMemberCount
         * EffectiveHealTotal
         * HasFreshPartyHealth
         * HealingEfficiencyWarning
         * NoHealingNeededWarning
      * UI should format these values consistently using existing number formatting.
      * Preserve locale-aware decimal formatting.
      * Avoid showing misleading ranges unless the range is actually meaningful and explained.
      * Prefer exact source-backed per-member values over 0-X ranges in the primary text.
   * Edge cases
      * No party members covered by fresh HP data.
      * User is solo or party data is unavailable.
      * Some party members have unknown HP.
      * All covered members are at full HP.
      * Most covered members are near full HP.
      * Some members need full healing and others need none.
      * Multi-cast would heal some members to full after the first cast.
      * Stale party snapshot.
      * Auto-equip changes INT and therefore healing estimate.
      * Spend All Mana changes selected cast count.
      * Cast count is invalid or unaffordable.
      * Party member max HP is missing.
   * Expected behavior
      * Blessing preview uses clear per-member wording.
      * Multi-cast preview shows total healing per party member, not a confusing aggregate party HP total.
      * Users are warned when most healing would have little effect because members are already near full HP.
      * Users are warned when party HP is already high and healing is probably unnecessary.
      * The preview remains approximate and source-backed.
      * The formula is not changed unless official/source verification shows it is wrong.
      * The warning uses theme-aware styling and does not look like a fatal error.
   * Suggested implementation
      * Inspect the current spell preview factory/model for Blessing.
      * Split raw healing and effective healing into explicit model fields.
      * Update the UI formatter for Blessing effect previews.
      * Add deterministic warning classification for capped/no-effect healing.
      * Keep aggregate party healing out of the primary copy.
      * Place detailed aggregate/member coverage in collapsed details only if still useful.
      * Recalculate warnings when:
         * Cast count changes.
         * Auto-equip recommendation changes stats.
         * Party HP snapshot refreshes.
         * Mana changes.
         * Spell is cast.
      * Update FEATURES.md and docs/UX_UI_MANIFEST.md if the documented spell preview wording changes.
   * Acceptance criteria
      * Blessing preview no longer uses confusing 0-X HP per covered party member wording as the primary line.
      * Blessing preview no longer shows aggregate party HP restored as the main value.
      * One-cast preview says approximately how much HP is restored per party member.
      * Multi-cast preview says approximately how much HP is restored per party member for all selected casts.
      * Effective healing caps are still respected when fresh party HP is available.
      * More than half of covered members being capped/no-effect shows a limited-value warning.
      * All or almost all covered members being healthy shows a stronger no-need warning.
      * Unknown/stale party HP data produces a concise uncertainty note.
      * Cast action remains available unless normal casting rules disable it.
      * Warnings and preview text are theme-aware and readable.
      * Decimal formatting remains consistent with the rest of the app.
      * Official/source verification confirms the formula or records why the estimate remains approximate.
   * Tests
      * Add spell preview tests for:
         * One Blessing cast with useful healing.
         * Multiple Blessing casts with useful healing.
         * All party members missing enough HP for full value.
         * More than half of members near full HP.
         * Almost all members near full HP.
         * All members at full HP.
         * Mixed full, partial, and no-effect members.
         * Unknown party HP data.
         * Stale party HP data.
         * Auto-equip changing healing value.
         * Spend All Mana changing cast count.
         * Locale-aware decimal formatting.
      * Add component/UI tests for:
         * New primary wording.
         * Multi-cast per-member total wording.
         * Limited-value warning.
         * No-healing-needed warning.
         * No aggregate party HP total in the default preview.
         * Expanded details if aggregate/member coverage is retained.

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

Goal: implement a local macro collection for predefined equipment and skill sequences.

Touch:
- proposed new `src/Habitica.Rules/Skills` area
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

Primary data:
- current user snapshot;
- current task snapshot;
- current equipment snapshot;
- current party/quest snapshot;
- local equipment presets;
- owned gear catalog;
- available skill metadata.

Expected outputs:
- compiled macro plan;
- dry-run preview;
- mana cost estimate;
- expected result estimate where current formulas support it;
- ordered Habitica API execution steps;
- validation warnings;
- execution log.

Storage model:
- add macro collection, macro, macro-step, dry-run, execution-log, and execution-step-log records through the existing local storage abstractions;
- reference existing per-user Equipment battle preset ids instead of copying preset slot mappings into macro definitions;
- persist partial execution state after every step.

Macro model:
- macros are declarative, named sequences, not executable user code;
- initial step types are `equip`, `cast`, `selectBestTask`, `assertManaAtLeast`, `assertCurrentClass`, `refreshSnapshot`, `stopIfWarning`, and `restoreOriginalGear`;
- spell references use stable Habitica spell ids, with text shorthand such as `spell:fireball`, `spell:pickPocket`, and `spell:healAll`;
- structured cast steps use an explicit shape such as `{ "action": "castSpell", "spellId": "fireball", "targetTaskId": "task-id", "count": 1 }`;
- task-targeting spells may use an explicit `targetTaskId` or a `selectBestTask` result;
- dynamic spell equipment recommendations are strategies such as `maximize:int`, `maximize:per`, or `balanced:int,per`, compiled against the current gear snapshot at execution time;
- dynamic gear recommendations must not be saved as generated preset ids.

Equip references:
- preset id;
- single gear item key;
- best-gear query such as maximize perception or maximize strength;
- restore original battle gear or costume captured when the macro starts.

Selection UX:
- list matching presets first, then individual owned gear items;
- preset labels include kind, name, and battle preset stat totals when available;
- individual gear labels use the Equipment gear catalog display name with raw key fallback.

Example macro:
```text
1. Equip gear that maximizes perception.
2. Cast Tools of the Trade 3 times.
3. Equip gear that maximizes strength.
4. Cast Backstab until there is no mana left.
5. Restore gear that was equipped before the macro started.
```

Execution flow:
```text
1. Load macro definition.
2. Compile into explicit steps.
3. Validate against latest snapshot.
4. Produce dry-run preview.
5. Require user confirmation.
6. Execute one step at a time through Habitica.Api.
7. After each mutating step, update local state or refresh relevant data.
8. Stop on unexpected state, API error, insufficient mana, or rate-limit delay requiring user-visible wait.
9. Persist execution log.
```

Restore rules:
- snapshot original battle gear and costume gear before the first mutating step when any restore action is present;
- restore actions use the captured start state, not currently edited preset definitions.

Validation:
- reject unknown step types;
- reject missing task targets;
- reject unavailable gear;
- reject deleted equipment preset references;
- reject plans that exceed available mana at dry-run time;
- reject unsupported class skills;
- reject destructive decisions based on stale data;
- reject unbounded loops.

Error handling:
- stop by default when an API call fails;
- show completed steps;
- show failed step;
- show whether local state may be stale;
- offer manual refresh.

Security:
- macros must not store credentials;
- exported macros must not include user API tokens, raw API headers, or private snapshots unless explicitly exported as a debug bundle.

Pre-implementation checks:
- confirm each skill endpoint and target semantic against `HABITICA_API.md` and current Habitica API docs before execution support is enabled.

Acceptance:
- Users can create, edit, delete, and run local declarative macros using the initial step types listed in this task.
- Dry-run preview shows planned equipment changes, selected targets, mana cost, expected requests, warnings, and stop conditions.
- Execution runs sequentially, persists progress, refreshes or updates local state after mutating steps, and stops on validation failures, API errors, stale state, or unexpected state changes.
- Macro steps can reference existing Equipment battle preset ids and dynamic gear strategies without copying transient recommendation data.
- Tests cover parsing/validation, missing gear, insufficient mana, stale data, deleted preset references, preset-first gear selection, task target resolution, restore-original-gear behavior, partial execution failure, stop-on-failure, and rate-limit response handling.

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
- Returning authenticated users see denser top sections on Dashboard, Tasks, Party, Equipment, and Spells.
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
