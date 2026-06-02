# Future Work

Last validated: 2026-06-02.

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

## Pending Queue

### Queued items to be added to `Prioritized Next Changes`

Work top to bottom. This is an intake list for rough notes that must become self-contained `Prioritized Next Changes` entries before implementation. Preserve the `Priority Instructions` and `Entries` structure.

### Priority Instructions

- Top – add to the top of the `Prioritized Next Changes` list (max priority).
- Middle – right after the `Top` entries and before current `Prioritized Next Changes` list items.
- Bottom – (default) the lowest priority entries, add to the bottom of the `Prioritized Next Changes` list.

### Entries:


## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Spells Auto-Equip Best Option Default With Dropdown

Goal: when a spell's auto-equip feature has multiple equipment options, default-select the most profitable option and offer the rest via a dropdown sorted most→least profitable.

Touch:
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- spell equipment-recommendation logic under `src/Habitica.Rules` / `src/Habitica.Application` (only the selection/ordering surface; do not change scoring formulas)
- direct tests under `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs` and rule tests under `tests/`
- `FEATURES.md`

Out of scope:
- changing how profitability/stat deltas are computed;
- changing cast execution order or CRON-warning semantics;
- changing two-handed weapon pairing logic.

Acceptance:
- With multiple options, the most profitable option is preselected.
- A dropdown lists remaining options sorted most→least profitable.
- Selecting a non-default option updates the equip plan; single-option case shows no dropdown.
- Tests cover default selection, dropdown ordering, and selection change.

### Split Party Page Into Party And Quests Pages

Goal: relieve Party-page overload by separating quest-heavy blocks onto a dedicated Quests page, reusing existing data/logic.

Touch:
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- new `src/Habitica.WebApp/Pages/QuestsPage.razor`
- `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor` and Dashboard nav cards in `DashboardPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/`
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if navigation guidance changes

Layout split:
- `Party` page keeps: party description/info, PARTY SYNC ROLES, PARTY SYNC SETTINGS, a small quest card linking to the Quests page, members list, buff info, CRON graph.
- `Quests` page holds: all quest and quest-queue-related blocks (active quest, shared pool, queue, voting, recent completions, quest controls). Update intra-quest-card links to point at the Quests page.

Out of scope:
- new database fields or party-sync data-contract changes;
- changing quest/queue/sync logic, permissions, or stale-data guards;
- changing Habitica party/quest links.

Acceptance:
- Quest blocks move to a new Quests page; Party page retains the listed sections plus a quest summary card linking to Quests.
- All existing actions keep current authorization and freshness guards; no schema change.
- Quest-related links updated to the Quests page.
- Navigation exposes the Quests page; related docs updated.
- Tests cover both pages rendering their sections and at least one guarded quest action still working on the Quests page.

### Party Sync Tokenized Invite Proofs

Goal: add an optional manager-issued party-sync proof mode. Parties continue to work with browser-only `local-claim-v1` by default, but an owner/app admin can enable tokenized invite proofs so shared party queue access no longer depends only on client-supplied local claim headers.

Touch:
- `functions/api/party-sync/[partyId].js`
- `src/Habitica.WebApp/wwwroot/js/sync/cloudflarePartySync.js`
- `src/Habitica.WebApp/State`
- `migrations/`
- direct tests under `tests/Functions/` and `tests/Habitica.WebApp.Tests/`
- `TECHNICAL.md`
- `FEATURES.md`
- `docs/DEPLOY_CLOUDFLARE_PAGES.md`

Implementation shape:
- Add a D1 migration for invite-proof state. Store party id, proof id or token hash, display label, issued/revoked/expires timestamps, issuer metadata, and an enabled/disabled party setting. Do not store raw reusable proof tokens if a hash is enough.
- Keep `local-claim-v1` as the default and as the recovery path. If tokenized proof mode is disabled or no active proof exists, existing party-sync behavior must remain unchanged.
- Add owner/app-admin management actions to create, list, revoke, rotate, remove, enable, and disable tokenized proofs. Existing Officer permissions should not automatically grant proof-management powers unless the code explicitly already treats the caller as owner/app admin.
- Extend `readAccessProof()` to parse both `local-claim-v1` and the new proof version. Extend `resolvePartySyncAccess()` so tokenized proof identity still passes through the same owner/admin/Officer/kick checks used by local claims.
- Update the browser sync bridge to send the new proof headers only when local state has an active tokenized proof. Do not send Habitica API tokens, raw credentials, or authorization headers to Cloudflare.
- Surface concise UI/state feedback for proof mode: disabled, enabled, active proof, revoked/expired proof, and fallback to local claim.

Out of scope:
- sending Habitica API tokens to Cloudflare;
- changing role names (`app admin`, `party owner`, `Officer`);
- removing the existing `local-claim-v1` reader;
- replacing party-sync roles, queue permissions, or kick semantics;
- requiring tokenized proofs for existing parties by default.

Acceptance:
- With no invite proof created, and with tokenized mode disabled, all existing party-sync reads/writes still work through `local-claim-v1`.
- Owner/app admin can enable and disable tokenized proof mode.
- Owner/app admin can create, list, revoke, rotate, and remove invite proofs without exposing Habitica credentials. Removing the active proof invalidates the old proof; the party can issue a new proof later and falls back to browser-only `local-claim-v1` while no active proof exists.
- `readAccessProof()` accepts both the new proof version and `local-claim-v1`; unsupported proof versions still fail with a clear 401.
- `resolvePartySyncAccess()` rejects malformed, expired, revoked, wrong-party, and kicked-user tokenized proofs.
- Owner/app-admin recovery remains possible when tokenized proofs are missing, expired, revoked, or misconfigured.
- Frontend bridge sends tokenized proof headers only when an active proof is available, and otherwise keeps the existing local-claim headers.
- Worker tests cover: local-claim fallback, valid proof, malformed proof, expired proof, revoked proof, removed proof, wrong-party proof, kicked-user rejection, owner/admin bypass/recovery, enable/disable mode behavior, and rotate invalidating the old proof.
- WebApp tests cover proof-mode state mapping and header selection without sending Habitica API tokens to Cloudflare.

### Active Quest Metadata And Detail Affordances

Goal: fill remaining active quest card metadata and drill-ins when Habitica or cached shared state exposes the data.

Touch:
- `src/Habitica.Api`
- `src/Habitica.Domain/Party`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- direct tests under `tests/`
- `FEATURES.md`

Out of scope:
- mobile app deep links; keep web fallback from `docs/HABITICA_DEEPLINKS.md`;
- fake values when Habitica data is missing.

Acceptance:
- Active quest snapshot preserves nullable owner/starter and started-at fields when the API or shared queue state exposes them.
- Active quest card shows owner or starter, started date, details view, participants view, and rewards/details affordances when cached data exists.
- Missing owner/starter/started-at fields render concise unavailable states without inventing values.
- Participant names use the same member-detail focus behavior as the party member list.

### Pets And Mounts Page With Bulk Sell Planner Relocation

Goal: build a dedicated Pets & Mounts page that surfaces per-pet/per-mount ownership, fast equip, search, missing-collection gaps, market-status hints, and feed-with-best-food. Move the BULK SELL PLANNER from the Inventory page onto this new page.

Touch:
- new `src/Habitica.WebApp/Pages/PetsMountsPage.razor`
- `src/Habitica.WebApp/Pages/InventoryPage.razor` (remove bulk-sell UI block and its helpers; preserve all other inventory behavior)
- `src/Habitica.WebApp/Pages/DashboardPage.razor` (add a `RenderDashboardLink("Pets & Mounts", ...)` nav card around line 286-289)
- `src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor` (add Pets & Mounts entry between Inventory and Party)
- `src/Habitica.Api/HabiticaApiClient.cs` and `src/Habitica.Api/IHabiticaSyncClient.cs` (add `FeedPetAsync`, `EquipPetAsync`, `EquipMountAsync`, `HatchPetAsync`; surface per-key pet/mount ownership maps and food/hatching-potion ownership in the user snapshot mapper)
- `src/Habitica.Domain/User/UserSnapshot.cs` (extend `InventorySnapshot` with `OwnedPets`, `OwnedMounts`, food/egg/hatching-potion per-key maps if not already present; small additive change, additive nullable defaults to preserve existing call sites)
- `src/Habitica.Domain` catalog: pets/mounts/food catalog records (egg group, potion group, favorite-food mapping). Static data, derive from a checked-in catalog file rather than a live API call.
- `src/Habitica.WebApp/State/AppSessionController.cs` (`FeedPetAsync`, `EquipPetAsync`, `EquipMountAsync` orchestration mirroring `BuyHealthPotionAsync` — fresh-state guard, sequential execution with stop-on-failure, post-action `GetUserSnapshotAsync` refresh, diagnostics logging)
- `src/Habitica.Storage/StorageKeys.cs` (new local-only key `PetsMountsViewPreferences`; NOT added to `PortableDataKeys`)
- direct tests under `tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs`, `tests/Habitica.WebApp.Tests/Pages/InventoryPageTests.cs` (assert bulk-sell removal), rule tests for feed-recommendation ordering
- `HABITICA_API.md` if new contract details are pinned down during implementation
- `FEATURES.md`
- `docs/UX_UI_MANIFEST.md` if a new page-level guidance is added

Habitica endpoints (already documented in `HABITICA_API.md:270-275`):
- `POST /user/feed/:pet/:food` — supports `?amount=<n>`
- `POST /user/equip/pet/:key` and `POST /user/equip/mount/:key` (via `/user/equip/:type/:key`)
- `POST /user/hatch/:egg/:hatchingPotion`
- Pets/mounts/food/eggs/hatchingPotions data already flows through the user snapshot endpoint; the API mapper currently keeps only counts. Extend it to capture per-key ownership maps. If a needed endpoint is not documented, stop and add a follow-up entry rather than guessing.

Feature shape:
- Groups: pets and mounts grouped by egg family (e.g. base, magic-potion, quest, premium) plus a separate hatching-potion section. Group names come from a static catalog. Empty groups still render with an empty-state hint.
- Each group is foldable. Folded state persists to local browser storage via `PetsMountsViewPreferences` (NOT portable sync). Survives reload; not synced across devices.
- Search box filters across all pets/mounts/potions by key and display name.
- Missing-collection view per group: list not-yet-owned pets/mounts and indicate hatching ingredients still needed (egg + potion missing from inventory). Display "ready to hatch" when both ingredients are owned.
- Market-status hints derived only from current inventory + catalog: "can hatch X with current inventory", "need egg Y" or "need potion Z to complete this group". Do NOT scrape live prices or invent gem costs.
- Feed UI: select a pet → food dropdown pre-sorted by recommendation (favorite food for the pet's egg group first, then generic food, then non-matching). Allow a multi-food queue with pre-feed preview. Execute sequentially with stop-on-failure. Refresh user snapshot after the queue finishes.
- Fast equip buttons on every owned pet/mount card; refresh snapshot after equip.
- Bulk sell planner is moved verbatim — same plan computation, sell execution, diagnostics, and refresh hooks. Inventory page must no longer render it. Diagnostics area stays `Inventory` (renaming the area is out of scope; a follow-up entry may rename it).

Out of scope:
- syncing per-pet/per-mount ownership maps to Cloudflare app-data sync;
- release-pets / release-mounts actions;
- gem-currency purchases (covered by the Dashboard buy-gems entry);
- changing existing sell execution, confirmation copy, or sell-result UX;
- redesigning the Inventory page beyond removing the bulk-sell block and any helpers that become dead code.

Acceptance:
- Pets & Mounts page is reachable from the Dashboard nav card and the side nav.
- Pet, mount, and hatching-potion groupings render with foldable state. Folded state survives reload from local storage; the value does NOT appear in any portable-sync payload.
- Search filter narrows visible entries across all groups by display name and key.
- Missing-collection view enumerates not-owned pets/mounts and the hatching ingredients still needed.
- Market-status hints only reference items derivable from current inventory + catalog.
- Feed action shows a pre-feed preview, supports a multi-food queue, runs sequentially with stop-on-failure, and refreshes the snapshot afterwards.
- Fast-equip changes the current pet/mount and refreshes the snapshot.
- Bulk sell planner appears on Pets & Mounts with identical behavior; Inventory page no longer renders it.
- No Habitica API tokens are forwarded to Cloudflare. No portable-sync entry is added for folded-group state.
- Tests cover: empty pets/mounts rendering, group rendering, fold persistence (with mocked storage), search filter, missing-collection enumeration, feed dry-run preview, multi-food queue failure handling, fast-equip success, bulk-sell relocation rendering, and Inventory-page bulk-sell removal.

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



## [6.1.0] — 2026-05-27

### Добавлено

- Добавлен метод `GDPRWindowController.Close()` для закрытия GDPR-окна из кода.
- Добавлен метод `UserConsentManager.CloseGdprUnityUI()` для закрытия активного GDPR-окна из меню настроек игры напрямую (без необходимости иметь ссылку на `GDPRWindowController`).

### Исправлено

- При уничтожении GDPR-окна через `Destroy` теперь корректно завершается `Task`, возвращаемый из `ShowGdprUnityUI`.
