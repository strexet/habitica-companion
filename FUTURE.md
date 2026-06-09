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

### Verify Incoming Damage Prediction And Move It Into Dashboard CRON Menu

Goal: verify incoming damage against current official Habitica CRON behavior, correct overestimation sources, and move the user-facing damage summary out of the standard Dashboard body into the Dashboard Start New Day / CRON menu. The merged CRON view should be compact and answer only what the user needs before starting the new Habitica day: which due unfinished Dailies can still reduce risk, estimated damage, boss contribution when applicable, estimated HP after CRON, and knockout risk.

Touch:
- `src/Habitica.Domain/Dashboard/PendingDamageEstimate.cs`
- `src/Habitica.Application/Dashboard/PendingDamageEstimateFactory.cs`
- `src/Habitica.Rules` for source-backed CRON damage and due-Daily eligibility rules
- `src/Habitica.WebApp/Pages/DashboardPage.razor`
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/Components/CronUnfinishedDailiesMiniList.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.Application.Tests/Dashboard/`, `tests/Habitica.Rules.Tests/`, `tests/Habitica.WebApp.Tests/Pages/DashboardPageTests.cs`, and `tests/Habitica.WebApp.Tests/Components/CronUnfinishedDailiesMiniListTests.cs`
- `FEATURES.md`
- `HABITICA_API.md` if endpoint field meanings or official CRON behavior notes are added or corrected
- `docs/UX_UI_MANIFEST.md` if CRON-menu layout or shared due-Daily UI guidance changes
- `TECHNICAL.md` only if responsibility moves between Application, Rules, and WebApp layers

Out of scope:
- reducing displayed damage heuristically without source-backed formula changes;
- adding undocumented Habitica mutation endpoints;
- changing Start New Day mutation sequencing except where stale damage state must be cleared after CRON;
- duplicating damage summaries elsewhere on the Dashboard;
- presenting party-wide damage as the authenticated user's incoming CRON damage;
- changing unrelated Dashboard cards, navigation, spells, inventory, or party UI.

Official verification plan:
- Inspect current official Habitica server behavior before changing formulas: Daily due selection, CRON Daily damage, boss quest damage, `party.quest.progress.down`, Inn/resting behavior, paused damage, multiple missed days, group-plan Daily edge cases, and quest participant eligibility.
- Confirm exact API field meanings used by this app, especially whether saved pending boss damage represents current-user damage, party-wide damage, already-applied damage, or damage that will be applied independently.
- Verify whether the public API exposes enough data for exact prediction. When exact prediction is blocked by server-only data, mark that component as estimated or unavailable rather than fabricating precision.
- Add short source comments only near non-obvious formula reproductions, pointing to official Habitica implementation inspected for the rule.

Model and rules plan:
- Replace the current loose Daily selector (`isDue` not explicitly false) if it includes unknown or non-due Dailies. A Daily may enter the numeric damage estimate only when it is confirmed due and damage-eligible for the evaluated Habitica day.
- Use official due behavior and available fields such as `isDue`, repeat schedule, frequency, `everyX`, start date, `nextDue`, Custom Day Start, timezone context, and group-plan assignment/completion state where supported by current snapshots.
- Separate confirmed due Dailies, excluded Dailies, and unknown-eligibility Dailies. Unknowns must not silently count as damage.
- Build an explicit `CronDamageEstimate` model with confirmed due Dailies, excluded Dailies, unknown-eligibility Dailies, personal Daily damage, boss quest damage, other/unavailable sources, estimated total, current HP, estimated remaining HP, risk state, confidence/readiness, source explanations, and diagnostics.
- Keep one shared eligibility helper for Dashboard CRON list, damage estimate, and Spells CRON warning so the same task set drives all CRON warnings.
- Verify personal Daily damage formula inputs: task value/color, priority/difficulty, Constitution including buffs, level/stat scaling if official behavior uses it, checklist/group-plan state if applicable, and pre-CRON stats.
- Verify boss quest damage formula: boss strength, task value/difficulty effects, Constitution effects, per-participant versus party-wide semantics, participant eligibility, Inn/resting behavior, paused damage, and already-applied source handling.
- Do not multiply current-user damage by party size. Do not mix other members' pending/future boss damage into the current user's Start New Day total.
- Do not multiply damage by missed-day count unless the official server does so for the exact case.
- Respect Inn/resting and paused-damage behavior; when damage is skipped, show concise compact copy such as `Damage is paused while resting in the Inn.`

Dashboard CRON UI plan:
- Remove the standalone incoming-damage prediction block from the normal Dashboard body.
- Merge damage information into the Start New Day section only.
- Keep default CRON view compact: estimated total damage, estimated HP after CRON, risk badge (`Safe`, `Warning`, `Knockout risk`, or incomplete estimate state), optional Dailies/Boss breakdown, due damaging Daily mini-list with inline completion, temporary CRON equipment recommendation when useful, Start New Day action, confirmation, progress, result, and error state.
- Put formula details, unavailable sources, source confidence, raw field meanings, and diagnostic explanations behind a collapsed disclosure.
- Avoid repeated HP totals, duplicate damage totals, large source explanations, raw API field names, party-wide totals for current-user damage, and technical confidence details in the default view.
- Recalculate the CRON section when a Daily is completed, due state changes, equipment optimization toggles, current gear or Constitution changes, active quest or participant state changes, boss data changes, Inn/paused state changes, HP changes, snapshots refresh, or CRON completes.
- After inline Daily completion, remove it from the CRON list and recalculate personal damage, boss damage, estimated HP, and risk.
- After successful CRON, clear or replace the pre-CRON estimate so stale damage is not displayed.
- Preserve responsive alignment and color-scheme readability for warning and danger states.

Diagnostics plan:
- Add structured diagnostics for evaluated Habitica day, CRON-needed state, incomplete Daily count, confirmed due count, excluded non-due count, unknown due-state count, personal Daily damage, boss damage, saved pending-down value, whether pending-down was included/excluded, Constitution used, Inn/paused state, quest participation, final total, confidence/readiness, and unavailable-source reasons.
- Do not log task text, credentials, API tokens, or sensitive headers unless existing diagnostics policy explicitly permits it.

Acceptance:
- Standard Dashboard no longer renders a standalone incoming-damage prediction card.
- Incoming damage appears only inside the Dashboard Start New Day / CRON menu.
- Compact CRON summary shows estimated damage, estimated HP after CRON, risk state, due damaging Dailies, and boss contribution when applicable.
- Detailed formulas and unavailable-source explanations are collapsed by default.
- Every Daily included in the numeric estimate is confirmed due and damage-eligible.
- Dailies with `isDue: false` are excluded; Dailies with unknown due state are separated and do not inflate the total.
- Schedule, recurrence, start date, Custom Day Start, timezone, group-plan, Inn/resting, paused-damage, and multiple-missed-day behavior match official Habitica behavior as closely as available API data allows.
- Personal Daily damage and boss quest damage formulas match the current official implementation or are clearly marked approximate with unavailable components separated.
- `party.quest.progress.down` or equivalent pending damage is interpreted correctly and never double-counted.
- Current-user damage is not multiplied by party size and does not include unrelated other-member damage.
- Completing a Daily updates the list, total damage, remaining HP, and risk state.
- Existing Start New Day confirmation, temporary gear optimization, inline Daily completion, progress, result, and error handling still work.
- Successful CRON clears stale pre-CRON damage prediction.
- Mobile and desktop layouts remain aligned and readable in all color schemes.

Tests:
- Add rules tests for confirmed due incomplete Daily, completed due Daily, explicitly non-due Daily, unknown `isDue`, weekday schedule, every-X-days schedule, future start date, Custom Day Start boundary, timezone boundary, group-plan Daily edge states, personal damage across task values/priorities/Constitution, boss damage with active quest, no active quest, user not participating, Inn/resting, paused damage, multiple missed days, and saved pending-down without double counting.
- Add Dashboard component tests for standalone damage card absence, unified compact Start New Day section, compact damage summary, optional breakdown, expanded source/formula details, due-Daily mini-list, unknown-source warning, estimated HP after CRON, safe/warning/knockout/incomplete states, inline Daily completion recalculation, temporary equipment preview, mobile/desktop structure, successful CRON clearing the estimate, and refresh failure stale indication.
- Add comparison/integration coverage with representative official Habitica scenarios: no quest plus one missed Daily, boss quest plus one missed Daily, several missed Dailies, mixed due and non-due Dailies, user in Inn, user returning after multiple inactive days, and existing non-zero pending-down value.

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
