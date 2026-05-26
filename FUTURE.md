# Future Work

Last validated: 2026-05-22.

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

## Pending Queue

### Queued items to be added to `Prioritized Next Changes`

Work top to bottom. This is an intake list for rough notes that must become self-contained `Prioritized Next Changes` entries before implementation. Preserve the `Priority Instructions` and `Entries` structure.

### Priority Instructions

- Top – add to the top of the `Prioritized Next Changes` list (max priority).
- Middle – right after the `Top` entries and before current `Prioritized Next Changes` list items.
- Bottom – (default) the lowest priority entries, add to the bottom of the `Prioritized Next Changes` list.

### Entries:

(empty)

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### Party Access Proof Hardening

Goal: replace trust-only local party-sync claims with tokenized manager-invite proofs if local claims are too easy to abuse in real parties.

Touch:
- `functions/api/party-sync/[partyId].js`
- `src/Habitica.WebApp/wwwroot/js/sync/cloudflarePartySync.js`
- `src/Habitica.WebApp/State`
- direct tests under `tests/Functions/` and `tests/Habitica.WebApp.Tests/`
- `TECHNICAL.md`
- `FEATURES.md`

Out of scope:
- sending Habitica API tokens to Cloudflare;
- changing role names (`app admin`, `party owner`, `Officer`).

Acceptance:
- `readAccessProof()` / `resolvePartySyncAccess()` can accept the new proof without breaking existing local-claim migration.
- Owner/admin recovery remains possible.
- Worker tests cover invalid, expired, wrong-party, kicked-user, and owner/admin bypass cases.

### Active Quest Metadata And Detail Affordances

Goal: fill remaining active quest card metadata and drill-ins when data is available.

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
- Active quest card shows owner or starter, started date, details view, participants view, and rewards/details affordances when cached data exists.
- Missing fields render concise unavailable states.
- Participant names use the same member-detail focus behavior as the party member list.

## Backlog

These entries are lower priority but already merged from the historical plans. Before coding, split a broad bullet into the same `Goal / Touch / Out of scope / Acceptance / UX-UI reference` shape used above.

### Advanced Party Quest Features

- Add optional limited vote budgets only if requested as an advanced voting mode.
- Add historical quest analytics beyond the recent-completion list and soft queue penalty.
- Split current party quest state and queue planning into clearer modes, such as tabs or a segmented switch.

### Skill Macros

- Add a macro collection for predefined skill/equipment sequences.
- Add dry-run previews with planned equipment changes, target selection, mana cost, expected requests, warnings, and stop conditions.
- Keep macro execution sequential and stop on validation failures or unexpected state changes.

### Action Result Estimates

- Add estimates for selected actions, including expected damage, gold, skill effects, boss progress, and player damage risk.
- Clearly distinguish exact API-returned values from local estimates and assumption-based formulas.

### UX Cleanup

- Add confirmation to Settings destructive actions such as clearing local browser data.
- Reduce repeated hero/help copy for returning authenticated users.
- Add sticky first-column or label context for mobile stat tables.
- Consider compact spell cards after the current spell card layout has been tested with real use.
