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

- _None._

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Verify And Harden CRON Due-Daily Filtering

Goal: ensure Dashboard and Spells CRON daily lists show only incomplete Dailies that can actually cause CRON damage for the evaluated Habitica day, while preserving existing pending-damage and Start New Day behavior.

Source: promoted from pending queue on 2026-06-08. The item is partly implemented today through `PendingDamageEstimateFactory.GetIncompleteDailies()` filtering `TaskSnapshot.IsDue != false`; the work is to verify official behavior, close gaps, and add explicit coverage.

Current context:
- `src/Habitica.Application/Dashboard/PendingDamageEstimateFactory.cs` is the shared helper for pending-damage Daily inclusion and CRON mini-list source data.
- `src/Habitica.WebApp/Pages/DashboardPage.razor` and `src/Habitica.WebApp/Pages/SpellsPage.razor` both call `PendingDamageEstimateFactory.GetIncompleteDailies(...)` and then remove locally completed CRON Dailies.
- `src/Habitica.Api/HabiticaApiClient.cs` parses task `isDue` into `TaskSnapshot.IsDue` when Habitica returns the field.
- Existing tests cover due vs not-due when `IsDue` is present, but do not prove parity with official schedule logic or missing-field behavior.

Touch:
- `HABITICA_API.md` if official API/source verification changes the documented task fields or CRON assumptions
- `src/Habitica.Api/HabiticaApiClient.cs` if additional Daily schedule fields must be parsed
- `src/Habitica.Domain/Tasks/TaskSnapshot.cs` if more schedule context must be represented locally
- `src/Habitica.Application/Dashboard/PendingDamageEstimateFactory.cs`
- a new or existing shared rules helper under `src/Habitica.Rules` or `src/Habitica.Application` if local due evaluation is needed
- `src/Habitica.WebApp/Components/CronUnfinishedDailiesMiniList.razor` only if empty-state/copy behavior changes
- `src/Habitica.WebApp/Pages/DashboardPage.razor`
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `tests/Habitica.Api.Tests/HabiticaApiClientTests.cs`
- `tests/Habitica.Application.Tests/Dashboard/PendingDamageEstimateFactoryTests.cs`
- `tests/Habitica.WebApp.Tests/Components/CronUnfinishedDailiesMiniListTests.cs`
- `tests/Habitica.WebApp.Tests/Pages/DashboardPageTests.cs`
- `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if CRON list copy/layout changes

Out of scope:
- changing normal Tasks page visibility or filters;
- changing Habitica CRON execution endpoint behavior;
- adding undocumented Daily mutation endpoints;
- estimating negative Habit damage unless the required state becomes available and documented;
- replacing server-provided `isDue` with a local approximation when official behavior can be trusted from the API response.

Official verification:
1. Re-check current Habitica API task payloads and official Habitica repository logic for Daily due evaluation before changing code.
2. Prefer the server-provided `isDue` field when it is present and verified as the official due state for the current Habitica day.
3. If `isDue` is missing or insufficient, document the exact official fields needed before adding local fallback logic.
4. Verify behavior for weekday `repeat`, `frequency`, `everyX`, `startDate`, custom day start, timezone boundaries, Inn/resting damage, and group-plan or assigned Dailies if the current API model exposes them.
5. Document any remaining assumption in `HABITICA_API.md` and keep UI copy from overstating certainty.

Plan:
1. Audit current `TaskSnapshot.IsDue` parsing and fixture coverage for due and non-due Dailies.
2. Compare `PendingDamageEstimateFactory.GetIncompleteDailies()` against official Habitica due logic and current API fields.
3. If `isDue` is authoritative, keep the helper simple but add explicit tests and docs explaining that source of truth.
4. If a local fallback is required, create a shared helper outside Razor that accepts task schedule data plus user Habitica day context and returns due/damage eligibility.
5. Use the same shared helper for Dashboard pending damage, Dashboard CRON mini-list, and Spells CRON warning mini-list so counts cannot drift.
6. Keep completed-in-session `_completedCronDailyIds` filtering as a page-local overlay after shared due/damage filtering.
7. Preserve existing empty/safe states when every incomplete Daily is non-due or damage-exempt.

Edge cases:
- due weekday Daily;
- non-due weekday Daily;
- every-day Daily;
- every-X-days Daily due today;
- every-X-days Daily not due today;
- future-start Daily;
- completed due Daily;
- incomplete due Daily;
- incomplete non-due Daily;
- missing `isDue` with insufficient schedule data;
- stale cached task or user data;
- custom day-start boundary around midnight;
- timezone/date transition;
- Inn/resting or paused-damage state where supported by current snapshots.

Acceptance:
- Dashboard CRON mini-list excludes incomplete Dailies with `IsDue == false`.
- Spells CRON warning mini-list uses the same due/damage eligibility as Dashboard.
- Pending damage estimate and CRON mini-list Daily counts do not disagree for the same snapshot, aside from page-local completed IDs.
- Due incomplete Dailies remain visible and actionable.
- Completed Dailies, non-Dailies, and known non-due Dailies are excluded.
- Missing/unknown due data is handled conservatively and documented.
- Existing Inn/resting or paused-damage behavior is preserved if currently supported; unsupported cases remain explicitly out of certainty.
- Tasks page behavior remains unchanged.
- Empty CRON state renders correctly when no damaging Dailies remain.

Tests:
- Add/confirm API parsing tests for `isDue: true`, `isDue: false`, and missing `isDue`.
- Add application tests for due weekday, non-due weekday, every-day, every-X due/non-due, future-start where modeled, completed due, incomplete due, incomplete non-due, and unknown due data.
- Add Dashboard page tests that the mini-list count and inline complete controls exclude non-due Dailies.
- Add Spells page tests that CRON warnings list the same filtered Dailies as Dashboard.
- Add component tests for empty CRON list rendering after filtering.
- Add day-start/timezone boundary tests only if local fallback logic is introduced.

### Spell Bulk Casting Controls And 350 ms API Spacing

Goal: improve spell bulk-cast safety and ergonomics by adding `Spend All Mana`, a cancellable one-second `Preparing...` stage, card-local cancellation, and a default Habitica API minimum request spacing of 350 ms.

Source: promoted from pending queue on 2026-06-08. This entry intentionally combines spell UX and request-spacing update because the pending item couples them and both affect multi-cast pacing.

Current context:
- `src/Habitica.WebApp/Pages/SpellsPage.razor` already renders spell cards, cast-count input, mana previews, CRON-sensitive buff warnings, equipment recommendations, and card-local progress.
- `src/Habitica.WebApp/State/AppSessionController.cs` already executes spell casts sequentially, optionally auto-equips/restores gear, refreshes snapshots, and uses `DelayBetweenHabiticaRequestsAsync(...)` between relevant requests.
- `src/Habitica.Api/HabiticaApiClientOptions.cs` currently defaults `MinRequestSpacingMilliseconds` to 300.
- `src/Habitica.WebApp/Program.cs` currently uses 300 as the fallback for `Habitica:MinRequestSpacingMilliseconds`.
- `src/Habitica.WebApp/wwwroot/appsettings.json` currently has no spacing override and should remain that way.

Touch:
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/State/IAppSessionController.cs`
- `src/Habitica.WebApp/State/AppSessionController.cs`
- `src/Habitica.WebApp/State/SessionViewModel.cs`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `src/Habitica.Api/HabiticaApiClientOptions.cs`
- `src/Habitica.WebApp/Program.cs`
- `src/Habitica.WebApp/wwwroot/appsettings.json` only to confirm no override is added
- `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs`
- `tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs`
- `tests/Habitica.Api.Tests/HabiticaApiClientTests.cs` or a new API options test file
- `FEATURES.md`
- `TECHNICAL.md`
- `docs/UX_UI_MANIFEST.md`

Out of scope:
- parallel spell casting;
- background or unattended automation;
- rollback of successful Habitica mutations;
- changing spell formulas or target recommendation scoring;
- changing Habitica rate-limit header handling beyond the default spacing value;
- adding `Habitica:MinRequestSpacingMilliseconds` to `appsettings.json`.

Spend All Mana plan:
1. Add a compact secondary `Spend All Mana` action near each eligible spell card's cast-count input.
2. Compute maximum casts from the same cached mana and spell cost used by the existing mana preview: `floor(availableMana / spell.ManaCost)`.
3. Respect the existing validated count maximum, currently clamped by the page model.
4. Update the existing count state so total mana cost, remaining mana, estimated effect, quest damage estimate, and equipment-influenced estimates recalculate through current rendering.
5. Do not start casting automatically and do not make an API request from `Spend All Mana`.
6. Disable or hide the action for locked spells, stale/missing account data, unaffordable spells, and zero/malformed mana cost.

Preparation and cancellation plan:
1. Add an active spell-cast operation/cancellation model owned by the page or session controller, choosing the narrowest integration that can cancel preparation, request-spacing delays, and all before-request boundaries.
2. Start preparation only after final user confirmation, including after any CRON warning decision.
3. Set active progress immediately, render `Preparing...`, show initial progress, and expose `Cancel` only on the active spell card.
4. Wait approximately one second with a cancellable async delay before any gear equip, spell cast, refresh, or local snapshot mutation.
5. Check cancellation before auto-equip, between equipment requests, before each cast, between casts, during request-spacing delays, before refresh requests, between refreshes, and before restore-gear steps.
6. Prefer cancellation boundaries before non-idempotent requests. If cancellation happens after a request was sent, never assume the server ignored it; refresh relevant snapshots before final state is shown.
7. Report cancellation separately from API failure and success, with completed/requested counts when partial work happened.
8. Keep successful Habitica mutations and never attempt rollback.

Request-spacing plan:
1. Change `HabiticaApiClientOptions.MinRequestSpacingMilliseconds` default from 300 to 350.
2. Change the `Program.cs` fallback for `Habitica:MinRequestSpacingMilliseconds` from 300 to 350.
3. Keep `src/Habitica.WebApp/wwwroot/appsettings.json` without a spacing override.
4. Preserve adaptive token-bucket behavior, `Retry-After`, `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`, and the rule that failed non-idempotent mutations are not automatically replayed.
5. Keep the one-second preparation delay separate from API throttling; preparation happens once per casting run, while minimum request spacing applies between API requests.

UI behavior:
- `Spend All Mana` is visually secondary to `Cast` and placed near the cast-count input.
- `Cancel` appears only on the actively preparing/casting spell card.
- Count, target, auto-equip, and recommendation controls are disabled or guarded when changing them would invalidate an active execution plan.
- Other spell cards cannot start conflicting mutation flows while one sequential spell run is active.
- Desktop layout remains aligned, and narrow/mobile layout wraps without overlap, clipping, or stair-step controls.
- Preparation-to-casting transition reuses the same progress area and avoids card-height jumps.

Result behavior:
- Cancelling during preparation returns a non-error message such as `Casting cancelled before it started.` and sends zero Habitica requests.
- Cancelling after partial completion returns a non-error message such as `Casting cancelled after 2 of 5 casts.`
- Completing all casts returns success with the completed/requested count.
- API errors remain errors and are not collapsed into cancellation copy.
- Diagnostics include spell id, requested count, completed count, cancellation/failure stage when useful, and no credentials or sensitive headers.

Acceptance:
- Eligible spell cards render `Spend All Mana` near the count input.
- `Spend All Mana` fills the maximum affordable count and updates existing previews without casting.
- Locked, stale, unaffordable, and malformed-cost states do not produce misleading counts.
- Pressing `Cast` starts `Preparing...` for about one second before any mutation or Habitica request.
- `Cancel` is visible and usable during preparation and later cancellable steps.
- Cancelling during preparation performs no API requests, no gear changes, and no local snapshot mutation.
- Cancelling after partial completion preserves completed casts, stops later casts, and refreshes enough state to avoid stale mana/task/party/equipment display.
- Duplicate/conflicting spell casting runs cannot be queued.
- Default effective Habitica API minimum request spacing is 350 ms when no explicit configuration override exists.
- Existing CRON warning, auto-equip, restore-gear, spell progress, and post-cast refresh behavior still works.

Tests:
- Add Spells page tests for `Spend All Mana` rendering, max count calculation, preview update, unaffordable state, locked spell state, stale/missing snapshot state, zero/malformed mana-cost guard, active `Preparing...` progress, and Cancel visibility only on the active card.
- Add Spells page tests that existing target, count, Cast, auto-equip, CRON warning, and recommendation controls remain present.
- Add layout/markup tests for desktop action grouping and narrow responsive structure where existing test style supports this.
- Add session/controller tests for one-second preparation before first mutation, zero requests during preparation, cancellation during preparation, cancellation before auto-equip, cancellation between equipment requests, cancellation before first cast, cancellation between casts, cancellation during request-spacing delay, cancellation before refresh, partial completion result, failure remaining distinct from cancellation, busy state cleanup, and existing sequential cast order.
- Add API/config tests that the options default is 350, the composition-root fallback is 350, explicit configuration still overrides the fallback, and `appsettings.json` contains no spacing override.

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
