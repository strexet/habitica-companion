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
- Party page active quest metadata/rewards, CRON summary, member CRON graph, shared quest pool, queue, voting, recent completions, owner/admin/Officer controls, and quest start action.
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

## Pending Queue

### Queued items to be added to `Prioritized Next Changes`

Work top to bottom. This is an intake list for rough notes that must become self-contained `Prioritized Next Changes` entries before implementation. Preserve the `Priority Instructions` and `Entries` structure.

### Priority Instructions

- Top – add to the top of the `Prioritized Next Changes` list (max priority).
- Middle – right after the `Top` entries and before current `Prioritized Next Changes` list items.
- Bottom – (default) the lowest priority entries, add to the bottom of the `Prioritized Next Changes` list.

### Entries:

- None.


## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Dashboard Spend Gold Buy Gems Action

Goal: add a "Buy gems with gold" action inside the Dashboard SPEND GOLD section. Visible only when the Habitica user is eligible to convert gold to gems (subscription-gated and respecting Habitica's monthly cap).

Touch:
- `src/Habitica.Api/HabiticaApiClient.cs` and `src/Habitica.Api/IHabiticaSyncClient.cs` (add `PurchaseGemsForGoldAsync(int quantity)` and extend the user snapshot mapper to expose subscription status and remaining gem-for-gold cap when the API provides them)
- `src/Habitica.Domain/User/UserSnapshot.cs` (add nullable `bool? CanBuyGemsForGold` and `int? RemainingGemPurchases`, OR a small `SubscriptionSnapshot` record referenced from `UserSnapshot`; additive nullable fields to preserve existing call sites)
- `src/Habitica.WebApp/Pages/DashboardPage.razor` (SPEND GOLD section around line 483-507; add the gems card and `BuyGemsForGoldAsync` UI handler with `CanBuyGems()` validation, mirroring `BuyArmoireAsync`/`CanBuyArmoire`)
- `src/Habitica.WebApp/State/AppSessionController.cs` (`BuyGemsForGoldAsync` orchestration mirroring `BuyHealthPotionAsync` and `BuyArmoireAsync`: fresh-state guard, sequential per-gem execution if multi, refresh snapshot, diagnostics)
- direct tests under `tests/Habitica.WebApp.Tests/Pages/DashboardPageTests.cs` and `tests/` for the controller orchestration
- `HABITICA_API.md` (pin endpoint shape and quantity behavior if the implementation confirms the bulk-quantity body parameter works)
- `FEATURES.md`

Habitica endpoint:
- `POST /user/purchase/gems/gem` with optional `quantity` body parameter — already documented at `HABITICA_API.md:269` and `:282-288`.
- Cost: 20 GP per gem (Habitica default). Monthly cap depends on subscription tier; if the cap is not present on the user snapshot, rely on Habitica's error response and degrade gracefully.

UI shape:
- New card titled "Buy gems with gold" inside the Spend Gold block. Hidden entirely when `Snapshot.CanBuyGemsForGold != true`.
- Quantity input clamped to `min(floor(Gold / 20), RemainingGemPurchases ?? floor(Gold / 20))`.
- Explicit confirmation modal/inline confirm required before purchase (per `HABITICA_API.md:290`: "Do not expose destructive or premium-currency actions without explicit confirmation").
- If the bulk `quantity` body parameter is verified to work in one call, send a single request. Otherwise loop sequentially per gem with stop-on-failure. Document the chosen path in `HABITICA_API.md` during implementation.
- Snackbar result + refresh of user snapshot on success; surface remaining cap and updated gem balance.
- Diagnostics logging under `DiagnosticsFeatureArea.Inventory` (rename to `Currency` is out of scope; follow-up acceptable).

Out of scope:
- selling gems back for gold;
- mystery hourglass purchase;
- subscription management UI beyond the gem-for-gold eligibility gate;
- exposing other subscription perks anywhere else in the UI;
- adding a dedicated currency page.

Acceptance:
- Buy-gems card appears only when the user is eligible to buy gems for gold; otherwise the SPEND GOLD section renders unchanged.
- Quantity input clamps to the affordable maximum and the remaining monthly cap when the cap is known.
- Action requires explicit confirmation. Cancel keeps state untouched.
- Successful purchase refreshes the user snapshot and updates the displayed gold and gem totals.
- Failed purchase (e.g. cap reached, API error) surfaces a concise error and stops further per-gem requests when looping.
- No Habitica credentials are forwarded to Cloudflare.
- Tests cover: card hidden when ineligible, visible when eligible, quantity clamp, confirmation gate, success refresh, partial failure during multi-gem sequence, and snapshot mapping for the new subscription fields.

### Remove Party Page CRON Summary And Buff Timing Window

Goal: remove the Party page overview's CRON summary and buff-timing recommendation block while preserving the rest of the Party and Quests workspaces.

Touch:
- `src/Habitica.WebApp/Pages/PartyPage.razor` (remove the Party overview section/card that renders the `CRON summary` heading, `CRON applied`, `Data gaps`, `Average best buff time`, `Self-first buff time`, and the low-confidence warning copy)
- `src/Habitica.WebApp/wwwroot/css/app.css` only if removing the block leaves unused spacing, empty grid tracks, or orphaned styling
- direct tests under `tests/Habitica.WebApp.Tests/Pages/PartyPageTests.cs`
- `FEATURES.md` (update the Party buff timing / Party page current-implementation notes so they no longer claim the overview exposes the removed CRON summary or buff-timing window)

Keep:
- Party overview summary card and quest summary card;
- member list, member filters/sorting, member details expansion, HP/MP/pending quest status, and stat details unless they are inside the removed CRON summary block;
- `/quests` active-quest forecast data, including pending progress and estimated post-CRON labels;
- stored party CRON history, party snapshot models, and CRON calculators.

Out of scope:
- deleting `PartyCronDashboardSnapshot`, CRON history stores, CRON graph calculation, or login-rhythm domain logic;
- changing the Quests workspace or active quest estimates;
- changing the party-sync D1 schema;
- adding new Party page modes or redesigning the full Party workspace.

Acceptance:
- `/party` no longer renders `CRON summary`, `Buff timing window`, `CRON applied`, `Average best buff time`, or `Self-first buff time`.
- Removing the section does not leave an empty card, blank grid slot, doubled margin, or broken responsive layout.
- `/party` still renders cached party name/summary, quest summary, members, member filters/sorts, HP/MP labels, pending quest labels, and member detail expansion.
- `/quests` still renders active quest current progress, pending party progress, estimated post-CRON progress, participant details, queue, pool, and recent completions.
- Tests update the existing Party page assertions from positive CRON-summary expectations to negative assertions for the removed copy, while keeping coverage for remaining Party and Quests content.

### Persist Manual Task Arrangement Through Cloud Sync

Goal: after a user rearranges tasks on the Tasks page, persist the updated per-type task order locally and trigger encrypted app-data sync for `preferences/taskOrder` so the order survives page visits and can propagate through the existing user-data sync flow.

Current state:
- `TasksPage.razor` already writes `TaskOrderPreferences` to `StorageKeys.TaskOrderPreferences` after drag/drop, keyboard reorder, and move-button reorder.
- `StorageKeys.TaskOrderPreferences` is already portable and mapped to `CloudSyncSection.TaskOrderPreferences`.
- `LocalUserDataPortabilityService` already merges task-order preferences by task type during import/cloud-sync merge.
- The missing behavior is a reliable post-save sync trigger from the reorder workflow.

Touch:
- `src/Habitica.WebApp/Pages/TasksPage.razor` (after `SaveTaskOrderPreferencesAsync`, trigger a narrow sync for the task-order section without blocking the visible reorder)
- `src/Habitica.WebApp/State/IAppSessionController.cs` and `src/Habitica.WebApp/State/AppSessionController.cs` (add a small public method for syncing one portable app-data section, or another narrow app-data mutation hook that uploads `CloudSyncSection.TaskOrderPreferences`)
- `tests/Habitica.WebApp.Tests/FakeAppSessionController.cs`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/TasksPageTests.cs`
- direct controller tests under `tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs` if a new session-controller method is added
- `FEATURES.md`

Implementation shape:
- Prefer a section-scoped controller method over calling `PushCloudSyncAsync()` from the page, so a single reorder does not force a full cloud-sync merge/upload of every section.
- Reuse `TryUploadCloudSyncSectionAsync(credentials, CloudSyncSection.TaskOrderPreferences, ...)` or equivalent existing upload machinery, preserving encrypted sync behavior and section status updates.
- Keep local persistence as the source of immediate UI truth; cloud sync should be best-effort and must not revert local order when upload fails.
- If the user is signed out or no credentials are available, skip cloud upload and keep the local order.

Out of scope:
- changing task sorting/filtering semantics;
- adding remote task-order storage outside the existing encrypted Cloudflare app-data sync;
- changing `TaskOrderPlanner` merge semantics;
- changing Habitica task order on the official Habitica server;
- sending Habitica API tokens to Cloudflare endpoints.

Acceptance:
- Drag/drop, keyboard reorder, and move-button reorder still update the visible order immediately.
- Reopening the Tasks page in the same local store restores the saved order.
- For an authenticated user with encrypted sync available, completing a reorder attempts upload of the `task-order-preferences` cloud-sync section and updates section sync status.
- Upload failure or excluded section state surfaces through existing sync status/diagnostics patterns and does not block or undo the local reorder.
- Signed-out reorder remains local-only and does not show an action failure.
- Tests cover local persistence after reorder, section-sync trigger after each reorder path or shared persistence path, signed-out/no-credential skip behavior, and upload-failure nonblocking behavior.

### Improve Random Theme Readability Guards

Goal: make generated color schemes reliably readable at calm and moderate chaos values, with explicit contrast checks for primary and secondary button text against their generated button backgrounds.

Touch:
- `src/Habitica.WebApp/Theme/ColorSchemeCatalog.cs` (random theme generation, `ApplyRandomContrastGuards`, contrast helpers, and any needed average-background helpers)
- direct tests under `tests/Habitica.WebApp.Tests/Theme/ColorSchemeCatalogTests.cs`
- `tests/Habitica.WebApp.Tests/Components/ColorSchemePanelTests.cs` only if UI behavior or random-theme save/display behavior changes
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if the documented chaos/readability contract changes

Implementation shape:
- Add contrast enforcement for `PrimaryButtonText` against the average or worst practical color of `PrimaryButtonGradient`.
- Add contrast enforcement for `SecondaryButtonText` against the average or worst practical color of `SecondaryButtonGradient`.
- Review existing generated pairs that affect persistent UI readability: `Ink` on card/background surfaces, app bar text, drawer text, disabled text, input border/background, focus outline, and danger/success/primary separation.
- Use stricter thresholds for lower chaos and intentionally looser thresholds only near the highest chaos levels:
  - calm to moderate chaos should prefer readable app-like themes;
  - high chaos may be wilder, but primary/secondary filled button labels should not collapse into the same luminance as their backgrounds unless the selected chaos level is explicitly in the extreme range.
- Keep generated token values valid CSS and avoid adding browser-only contrast calculations to tests.

Out of scope:
- changing built-in preset palettes except where a shared helper requires harmless normalization;
- changing the custom scheme editor or pasted custom scheme validation beyond preserving new token names;
- adding a full accessibility audit UI;
- changing random preset selection behavior.

Acceptance:
- Deterministic tests over representative random seeds verify generated calm and moderate themes keep readable contrast for:
  - `PrimaryButtonText` over `PrimaryButtonGradient`;
  - `SecondaryButtonText` over `SecondaryButtonGradient`;
  - body/card text over primary card surfaces.
- Tests include at least one high-chaos/MADNESS case that verifies generated schemes remain valid while allowing looser contrast than calm themes.
- Existing random-theme behaviors still hold: temporary generated themes are not persisted until saved, saved random themes validate, rerolled themes are saveable, and the `Generated` dropdown entry still works.
- Documentation explains that chaos controls how aggressively readability guards are relaxed.

### Sync Appearance Changes And Rename Customization Close Action

Goal: make the shared Appearance/color-scheme controls trigger encrypted app-data sync after persisted appearance changes, and rename the final customization close action from `Cancel` to `Done` so finishing the flow reads as completion rather than abandonment.

Current state:
- Color scheme preferences are stored under `StorageKeys.ColorSchemePreferences`.
- `CloudSyncSection.ColorSchemes` already maps to `preferences/colorSchemes` and has merge behavior for custom schemes plus selected-scheme timestamps.
- `ColorSchemePanel` persists selected presets and saved custom/random schemes through `ColorSchemeService`, but it does not request cloud sync directly.
- The compact advanced toggle currently shows `Cancel` while a random/custom edit flow is active; docs describe that as an abandon-and-collapse action.

Touch:
- `src/Habitica.WebApp/Components/ColorSchemePanel.razor`
- `src/Habitica.WebApp/Theme/ColorSchemeService.cs` only if the service needs to expose a persisted-change result or separate transient preview from persisted mutations more clearly
- `src/Habitica.WebApp/State/IAppSessionController.cs` and `src/Habitica.WebApp/State/AppSessionController.cs` if a reusable app-data-section sync method is added for this and task-order persistence
- `tests/Habitica.WebApp.Tests/Components/ColorSchemePanelTests.cs`
- `tests/Habitica.WebApp.Tests/Pages/DashboardPageTests.cs` and `tests/Habitica.WebApp.Tests/Pages/SettingsPageTests.cs` if page-level wiring is used
- `tests/Habitica.WebApp.Tests/FakeAppSessionController.cs`
- direct controller tests under `tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs` if a new session-controller method is added
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md`

Implementation shape:
- Trigger `CloudSyncSection.ColorSchemes` upload only after persisted appearance mutations:
  - selecting a built-in or custom preset;
  - selecting a random preset;
  - saving a random theme as a custom scheme;
  - saving a custom scheme;
  - deleting a custom scheme when that changes stored preferences.
- Do not trigger cloud sync for transient preview actions:
  - generating a temporary random theme;
  - rerolling or adjusting chaos before save;
  - paste preview before `Save Scheme`;
  - canceling/reverting an unsaved custom edit.
- Prefer the same narrow section-sync hook used for task-order sync if that task has already added it.
- Keep sync best-effort; failures should use existing cloud-sync status/diagnostics and must not prevent local theme application or saving.
- Rename the compact flow-closing label from `Cancel` to `Done` and update behavior/docs so `Done` means "close this customization surface"; retain explicit cancel/revert controls only where they truly discard unsaved preview state.

Out of scope:
- changing color scheme storage schema unless required for accurate persisted-change detection;
- changing random theme generation rules beyond sync trigger behavior;
- adding background polling for color-scheme sync;
- changing encrypted sync key derivation or Cloudflare endpoints;
- sending Habitica API tokens to Cloudflare endpoints.

Acceptance:
- Selecting a color preset persists the selected scheme and attempts a `color-schemes` section upload when authenticated.
- Saving a random or custom scheme persists the scheme, selects it when the existing behavior does, and attempts a `color-schemes` section upload when authenticated.
- Transient random generation, chaos slider changes, rerolls before save, and paste previews do not upload `color-schemes`.
- Signed-out Appearance changes remain local and do not show a sync failure.
- Cloud-sync failure does not undo local theme application, localStorage fast persistence, or IndexedDB preferences.
- The compact customization close action shows `Done`, not `Cancel`; any remaining `Cancel` labels are attached only to controls that actually discard an unsaved paste/edit flow.
- Tests cover persisted select sync, saved random/custom sync, transient random no-sync, signed-out no-sync, and the `Done` label/close behavior.

### Simplify Blessing Estimate Copy

Goal: shorten the Healer `Blessing` estimate so it focuses on the formula-calculated HP value in the same concise style as `Healing Light`, without secondary group-wide totals.

Touch:
- `src/Habitica.Rules/Spells/SpellViewModelFactory.cs` (`BuildPartyHealEstimate`)
- direct tests under `tests/Habitica.Rules.Tests/Spells/SpellViewModelFactoryTests.cs`
- `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs` only if page assertions depend on the old copy
- `FEATURES.md`

Implementation shape:
- Keep the current formula and fresh-party-health capping behavior:
  - theoretical maximum: `(CON + INT + 5) * 0.04`;
  - when fresh party HP is available, cap each known member by that member's missing HP;
  - when fresh party HP is unavailable, show the theoretical per-member maximum.
- Replace verbose aggregate copy with one concise sentence focused on per-member value:
  - no fresh HP: "Restores up to approximately X HP to each party member."
  - same effective heal for covered members: "Restores approximately X HP to each covered party member."
  - varied effective heal for covered members: "Restores approximately X-Y HP per covered party member."
- Preserve a short missing-coverage warning only when useful, but do not include total HP restored across the whole group.
- Keep `SpellEffectValue` useful for sorting/recommendations; if the score remains aggregate total heal, ensure UI copy no longer exposes that aggregate as the main description.

Out of scope:
- changing Blessing formula or confidence level;
- changing Healing Light behavior;
- changing party-health freshness rules;
- changing spell-cast execution or CRON warning behavior;
- changing spell equipment recommendation ranking except as a direct consequence of existing `SpellEffectValue` score semantics.

Acceptance:
- Blessing estimate no longer contains aggregate group-total wording such as `HP total`.
- Blessing estimate still shows the formula-calculated per-member theoretical maximum when fresh party health is unavailable.
- With fresh party health, Blessing estimate shows the capped per-member value or range for covered members.
- Missing party-member HP coverage, if shown, is short and secondary.
- Healing Light estimate remains unchanged.
- Tests cover no fresh party health, uniform covered member healing, varied covered member healing, missing coverage, and absence of group-total copy.

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
