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

## Companion-First Integration Planning Gates

- This file is the **only active implementation queue**. The architecture and [competitive mobile UX research](docs/COMPANION_FIRST_MCP_RESEARCH.md) are reference material, not a second queue.
- The **14 E01–E14 high-level product, UX and reliability epics** below are raw research/decomposition intake. Promote one independently verifiable subtask at a time with `Process pending`.
- The **11 numbered MCP/OAuth contracts** below carry explicit dependencies and blockers. `Implement next` must not skip a blocked first task.
- Once Companion-owned task/project/goal storage is established, Habitica is a game projection. No parallel direct-to-Habitica MCP write path is allowed.
- Persist accepted application commands, but do **not** promise exactly-once remote rewards when Habitica reports a timeout without upstream idempotency. Preserve unknown outcomes for reconciliation.
- Conduct hands-on competitor/mobile usability work in E01 before implementation-specific UI tasks are promoted.

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

## Pending Task Format

Use the existing Skill-Tree `PENDING_TASK_FORMAT.md` nested format. Every pending epic should be decomposed with: Description, Current context, Source verification requirements, Requirements, Runtime/product behavior, Data/model behavior, Edge cases, Expected behavior, Suggested implementation, Acceptance criteria, Tests, Documentation updates. Pending does not authorize coding.

Canonical nested template for any newly added pending entry (omit inapplicable fields, preserve nested lists):

```markdown
- Task title
  - Description
    - Explain the intended change and why it matters.
  - Current context
    - Name current code, modules, UX and constraints.
  - Source verification requirements
    - Inspect live code/config/tests and current official behavior before promotion.
  - Requirements
    - State testable functional and safety requirements.
  - Runtime/product behavior
    - Describe user-facing and system-visible states.
  - Data/model behavior
    - Specify durable records, migration, sync, and projections.
  - Edge cases
    - Cover offline, timeouts, concurrency, conflicts and partial success.
  - Expected behavior
    - State the final observable outcome.
  - Suggested implementation
    - Suggest modules/tests without prescribing unverified symbols.
  - Acceptance criteria
    - Give measurable user/system outcomes.
  - Tests
    - Specify isolated and integrated validation scenarios.
  - Documentation updates
    - Specify RULES/TECHNICAL/FEATURES/API/UX/deployment as appropriate.
```

## Pending Queue

### Queued items to be added to `Prioritized Next Changes`

Work top to bottom. This is an intake list for rough notes that must become self-contained `Prioritized Next Changes` entries before implementation. Preserve the `Priority Instructions` and `Entries` structure.

### Priority Instructions

- Top – add to the top of the `Prioritized Next Changes` list (max priority).
- Middle – right after the `Top` entries and before current `Prioritized Next Changes` list items.
- Bottom – (default) the lowest priority entries, add to the bottom of the `Prioritized Next Changes` list.

### Entries:

- **E01 — Competitive UX/IA study and mobile-first product contract**
  - Description: Benchmark Todoist, Things 3, TickTick, Amazing Marvin, Microsoft To Do and selective Asana/Linear/Jira flows. Produce a screenshot/flow inventory, 'borrow/adapt/reject' matrix, mobile IA, core personas and default-vs-advanced rules.
  - Current context: Existing Tasks page is a Habitica task-type list, not a modern daily planner or project workspace.
  - Requirements: Real screenshots/documented workflows; task capture, planning, project organization, calendar, completed history, recurrence, error/pending states; mobile-first and accessibility criteria; explicit no-corporate-PM scope.
  - Suggested implementation: `docs/UX_UI_MANIFEST.md`, design/research artifacts under `docs/`, low-fidelity mobile prototypes.
  - Acceptance: Written benchmark with dated links, documented observations vs hypotheses, tested journeys, and approved navigation prototype before detailed UI tickets.

- **E02 — Companion-owned canonical workspace and task domain**
  - Description: Define first-class Categories, Goals, Projects, Sections, Labels, Tasks, Subtasks, TaskOccurrences and lifecycle events; distinguish user task state from Habitica game state.
  - Current context: `TaskSnapshot` is Habitica-centric and lacks Companion-owned IDs, project/goals, lifecycle and recurrence history.
  - Requirements: Stable IDs, task kinds (Todo/Daily/Habit vs game projection), dates/planned/deadline, priority, states, goals progress, optional dependencies and staleness metadata, extensible schemas, safe deletion/archive.
  - Data/model behavior: Versioned local entities with opaque IDs and a separate external projection mapping. Define primary project vs multiple project memberships explicitly during decomposition.
  - Acceptance: Approved schema/use-case contract, migration examples, invariants, round-trip fixtures, backward compatibility plan; no dependency on Habitica fields for Companion entity identity.

- **E03 — Local-first persistent storage, migration, and optional cloud workspace**
  - Description: Convert snapshot-only cache to durable task domain/event/outbox stores; plan cross-device sync and server-readable opt-in workspace for remote MCP without silently weakening existing encrypted KV sync.
  - Current context: IndexedDB Dexie currently has one `kv` store; existing encrypted KV sync is browser-derived/opaque to backend.
  - Requirements: Atomic entity+event+outbox commits, IndexedDB schema upgrades, restore/export, storage eviction handling, multi-tab/multi-device versioning, merge/conflict semantics, opt-in account identity and privacy contract.
  - Edge cases: Offline client vs server changes, reset browser, import duplicate, stale cloud snapshot, migration rollback, mixed PWA versions.
  - Acceptance: Documented local-only vs hosted guarantee levels, migratable schema, no lost preexisting data, explicit server-readability consent, migration rehearsals and recovery tests.

- **E04 — Habitica projection and reconciliation adapter**
  - Description: Treat Habitica API as gamification projection. Map Companion Tasks/Labels to Habitica task types/tags and reconcile missing, changed, completed and externally deleted tasks.
  - Current context: API reads/writes Habitica tasks directly, and task IDs currently come from Habitica.
  - Requirements: Stable Companion-to-Habitica IDs/aliases; import ownership policy; reserved alias/tag namespace only after upstream verification; safe create-then-complete chain; no duplicate awards after ambiguous timeout.
  - Edge cases: Externally deleted Todo, external edit, alias collision, already completed Todo, unknown score result, Habitica tag rename/delete, daily/habit repeated events.
  - Acceptance: Companion-completed Todo absent in Habitica is safely created and completed when reconcilable; tests show no silent duplicate creates/scores and no resurrection loops.

- **E05 — Durable command journal and typed failure/recovery engine**
  - Description: Replace direct fire-and-forget Habitica actions with a persisted application-level transactional outbox and consistent retry/reconcile semantics across manual PWA and MCP.
  - Current context: Current C# API client throttle is in-memory; no durable application command ledger exists.
  - Requirements: Submission persistence, dependency graph, per-task ordering, retries, state machine, leases, cancellation boundaries, typed API failures, unknown-outcome reconciliation and visible queue inspection.
  - Runtime behavior: UI never crashes from ordinary upstream 401/429/5xx/timeouts; pauses then resumes eligible commands; permanent blockers remain visible and are not silently dropped.
  - Acceptance: Fault-injection tests for offline/timeout/429/5xx/concurrent tabs; accurately distinguished local save vs game sync; no blind retry of non-idempotent mutation.

- **E06 — Early-slowdown adaptive limiter and automatic controlled recovery**
  - Description: Redesign outbound Habitica traffic scheduling to slow earlier, preserve headroom and recover progressively on sustained success.
  - Current context: `HabiticaApiClient` token bucket uses adaptive ramp near depletion, but lacks coordinated cross-client durable backpressure and recovery logic.
  - Requirements: Actual header reconciliation, forward-looking budget, safety reserve, per-user plus possible shared-egress quotas, AIMD/circuit-breaker separation, backoff+jitter, fair scheduling, observability.
  - Acceptance: In synthetic load tests rate reduction begins before near-zero budget; 429 and timeouts do not spin; stable success restores throughput gradually; no credential exposure in traces.

- **E07 — Optional always-on server executor and queue ownership handoff**
  - Description: Let opted-in users process Companion outbox commands and MCP edits after the browser closes, with durable server ledger, leases and safe resumption.
  - Current context: PWA cannot guarantee unattended background execution; MCP already requires remote account connection for live interactions.
  - Requirements: Per-profile server authentication, accepted-command ACK boundary, local-to-server handoff/dedup, single execution owner, persisted retries, bounded sweep, optional Cloudflare Queues as transport, cleanup/revocation.
  - Edge cases: Split brain browser+Worker, disconnect mid-execution, credential revoked, Cloudflare Queue DLQ/expiry, duplicate delivery, mobile app shutdown.
  - Acceptance: Simulated restart/redeploy/outage does not silently lose accepted commands; ambiguous upstream effects remain blocked for reconciliation rather than double-applied.

- **E08 — Mobile-first task capture, edit, and navigation redesign**
  - Description: Redesign Todos, Dailies and Habits for smartphone-first use, with minimal fast entry and advanced configuration when requested.
  - Current context: Existing `TasksPage.razor` is a large grouped list with expanding cards, filters and score controls.
  - Requirements: Fast Add from main views, one-handed touch design, clean task rows, keyboard/safe-area behavior, simple vs advanced detail sheet, quick reschedule, undo where safe, accessible date picker, responsive secondary desktop views.
  - Acceptance: Common capture/completion/reschedule flows need few interactions on a narrow phone; no core task property requires desktop; WCAG/touch/keyboard tests and mobile usability review.

- **E09 — Today, Upcoming, Calendar, Inbox and Completed/Logbook views**
  - Description: Add a unified agenda/planning surface and durable completed history, not just the current Habitica task-type grouping.
  - Requirements: Today planner, Inbox, near-term Upcoming, Week/Month + agenda calendar, overdue, unscheduled/Anytime, completed history and restore, explicit occurrence/recurrence handling.
  - Data behavior: Distinguish all-day date/time, planned date/deadline, game Daily due-state and completion timestamp; calendar uses Companion records, not API-only recent completed limit.
  - Acceptance: Fully usable on mobile; accurate date boundary across time zones/DST; no duplicate recurring Dailies; completed items remain visible/searchable after Habitica removes them.

- **E10 — Categories, goals, projects, progress and lightweight workflows UX**
  - Description: Offer real personal-project power with categories/areas, projects, sections, optional goals and progress, simple stages (Inbox/Next/In Progress/Waiting/Done) and cross-project organization.
  - Requirements: Simple mode works without hierarchy; optional advanced fields and project board; move task between projects; goal linking and progress rules; useful stale/blocked/missing-next-action warnings.
  - Out of scope: Corporate user performance dashboards, employee monitoring, burnout scoring, sprint velocity, capacity forecasting and mandatory PM rituals.
  - Acceptance: A casual user creates a Todo with one field; a power user creates a project with tasks/deadlines/labels and reviews progress from a phone.

- **E11 — Habits and Dailies as first-class recurring personal workflows**
  - Description: Make habitual and scheduled work ergonomic in Companion while respecting distinct Habitica Habit/Daily semantics and Cron day boundaries.
  - Requirements: Recurrence patterns, per-occurrence completion history, skip/pause/restart, streaks where determinable, habit up/down, Daily schedule with Habitica due-state reconciliation, calendar/Today integration, optional dedicated habit dashboard.
  - Edge cases: Day-start offset, missed Cron, timezone travel, duplicate reward count, skipped occurrence, delayed sync and task type conversion.
  - Acceptance: Recurring work remains consistent across date changes and offline reconnect; reward projections never assume scores confirmed before Habitica responds.

- **E12 — Powerful search, saved views, reviews and task hygiene**
  - Description: Add cross-project filters, saved smart lists, stale/unreviewed tasks, unblocked next actions, and a gentle review flow.
  - Requirements: Search text, label, project, category, state, planned date, deadline, priority, game sync state; per-type staleness thresholds and exclude waiting/someday/recurring as appropriate; configurable reminders without punitive wording.
  - Acceptance: Users can find stalled or forgotten tasks without mandatory status management; backlog review works in a few mobile gestures.

- **E13 — MCP and Skills for Companion-native project/task/goal operations**
  - Description: Extend the existing OAuth/MCP plugin to use canonical Companion domain commands for categories, projects, goals, labels, advanced task fields, filters, planning, activity log and queue status.
  - Current context: Original MCP task plan targets Habitica tasks and does not know Companion-owned projects/goals.
  - Requirements: Narrow tools such as `list_projects`, `create_project`, `list_categories`, `list_goals`, `create_companion_task`, `update_companion_task`, `list_upcoming`, `get_completed_history`, `get_sync_status`; schema-level validation, per-profile auth, confirmation for sensitive actions, no generic API proxy.
  - Dependencies: Canonical domain, workspace accessible to MCP, authoritative command API, sync/reconciliation and permission model must exist first.
  - Acceptance: ChatGPT performs the same advanced task workflows as the manual app, sees pending-vs-confirmed game projection, and cannot bypass validation or access another user's workspace.

- **E14 — Mobile UX validation, observability, migration and gradual adoption**
  - Description: Progressive rollout with feature flags, backward-compatible migration, optional adoption of imported tasks, qualitative testing with consenting users, and accessibility/performance gates.
  - Requirements: Real iOS Safari/PWA/Android tests, offline/network-fault drills, migration backup/restore, screen-reader and large-text support, honest sync progress/diagnostics, no unconsented behavior tracking.
  - Acceptance: Legacy users retain their data and existing gameplay workflows; users can switch advanced features off without corrupting records; recovery after interrupted migration is validated.

## Prioritized Next Changes

Work top to bottom. Each entry is self-contained.

### 0. Correct Habitica `x-client` Identity

Priority: Top

Status: Blocked pending creator User ID configuration decision

Origin:
- Integration-plan review.
- Existing repository/API contract mismatch.

Evidence:
- Habitica's API Usage Guidelines require the tool creator's User ID in `x-client`.
- `HABITICA_API.md` documents the same rule.
- Current `HabiticaApiClient.CreateRequest()` falls back to the authenticated end user's ID when no configured header exists.

Goal:

Make `x-client` stable and project-owned before adding any new Habitica execution surface.

Current behavior:
- `ClientHeaderValue` may be empty.
- Empty configuration produces `<end-user-id>-habitica-tool`.

Desired behavior:
- Production requires a project-owned `x-client` value.
- PWA and future MCP backend use the same logical app identity.
- End-user Habitica IDs are never used as the creator portion of `x-client`.

Touch:
- `src/Habitica.Api/HabiticaApiClient.cs`
- `src/Habitica.Api/HabiticaApiClientOptions.cs`
- `src/Habitica.WebApp/Program.cs`
- `src/Habitica.WebApp/wwwroot/appsettings.json`
- `README.md`
- `HABITICA_API.md`
- `TECHNICAL.md`
- deployment docs
- API-client tests

Discovery allowance:
- Add validation for missing production configuration.
- Add a development-only configuration path.
- Do not hardcode an unknown creator User ID.

Dependencies:
- None.

Out of scope:
- MCP.
- OAuth.
- server-side credentials.

Implementation constraints:
- No fallback to the active user's User ID.
- No token or other secret in `x-client`.
- `x-client` is not a secret and may be present in client-visible config.
- Deployment must explicitly provide the project-owned creator User ID/application name.

Possible implementation approaches:
1. Fail when `ClientHeaderValue` is empty.
2. Keep a repository-defined creator/application constant.
3. Supply the value at build/deploy time.

Recommended approach:
- Require an explicit full `x-client` value for production.
- Allow local development to provide it through local configuration.
- Fail clearly rather than silently substituting the end user.

Questions and required clarifications:
- [Unresolved - blocks implementation] What exact Habitica User ID should identify the tool creator?
  Required answer from:
  - repository owner.

Validation:
- API-client test verifies configured header.
- API-client test verifies no end-user fallback.
- Production-like configuration test fails cleanly when missing.

Acceptance:
- Every Habitica API request uses a stable tool-creator `x-client`.
- Different end users produce the same creator/application `x-client`.

Documentation updates:
- `README.md`
- `HABITICA_API.md`
- `TECHNICAL.md`
- deployment docs.

Risks:
- Existing deployments with empty configuration will fail until configured.
- This is intentional and preferable to API-guideline noncompliance.

---

---

### 1. Define ChatGPT Integration Architecture, Privacy, And Retention Contract

Priority: Top

Status: Ready

Origin:
- Owner request for multi-user ChatGPT integration.
- Security-sensitive architecture change.

Goal:

Document the new optional trust boundary before implementation.

Current behavior:
- Habitica credentials remain on-device.
- private sync is browser-encrypted.
- party sync never receives the API token.
- privacy UI says credentials stay in the browser and are sent only to Habitica.

Desired behavior:
- Existing local-only behavior remains default.
- ChatGPT enrollment explicitly allows encrypted server-side credential storage.
- Privacy copy distinguishes normal Companion use from ChatGPT-enabled use.
- Data-retention and deletion semantics are documented.

Touch:
- `RULES.md`
- `TECHNICAL.md`
- `FEATURES.md`
- `HABITICA_API.md`
- `docs/DEPLOY_CLOUDFLARE_PAGES.md`
- `docs/UX_UI_MANIFEST.md`
- `src/Habitica.WebApp/Pages/PrivacyPage.razor`
- `FUTURE.md` if the plan is imported into the repository queue.

Discovery allowance:
- Current OpenAI MCP/Plugin auth documentation.
- current MCP authorization specification.
- current Cloudflare Workers OAuth Provider documentation.
- current Habitica API Usage Guidelines.

Dependencies:
- Task 0 should be decided before server-side API calls ship.

Out of scope:
- Worker code.
- OAuth code.
- credential persistence.

Implementation constraints:
- Explicit opt-in.
- No silent migration of current users.
- No reuse of personal-sync or party-sync storage for integration secrets.
- Publish a concrete deletion contract.
- Publish what metadata is retained after revocation, if any.

Recommended retention contract:
- active encrypted credential envelope: until disconnect/delete;
- OAuth grants/tokens: until revocation/expiry;
- one-time linking tickets: <= 10 minutes;
- operational audit metadata: short bounded retention, e.g. 30 days;
- raw prompts/conversation text: not stored by Companion.

Questions and required clarifications:
- [Recommended default - confirmation optional] Operational audit retention?
  Recommendation:
  - 30 days.
- [Recommended default - confirmation optional] Keep anonymized aggregate metrics after deletion?
  Recommendation:
  - only non-user-identifying counters, if needed at all.

Validation:
- Search all privacy/security docs for contradictory "credentials never leave browser" statements.
- Verify the final text matches actual planned network behavior.

Acceptance:
- A user can understand exactly what changes when enabling ChatGPT integration.
- Non-opted-in behavior is unchanged.
- retention/deletion rules are explicit.

Documentation updates:
- all touched docs.

Risks:
- privacy text drifting from implementation.
- accidental reuse of legacy sync concepts.

---

---

### 2. Add Dedicated Auth And MCP Worker Projects

Priority: Top

Status: Ready

Origin:
- Cloudflare OAuth Provider v1 split architecture.
- Need to isolate authentication/credentials from MCP execution.

Goal:

Create independently deployable Workers with explicit bindings and environments.

Desired layout:

```text
workers/
  chatgpt-auth/
    package.json
    package-lock.json
    wrangler.jsonc
    src/
    migrations/

  chatgpt-mcp/
    package.json
    package-lock.json
    wrangler.jsonc
    src/
```

Current behavior:
- root `wrangler.toml` configures Cloudflare Pages.
- no dedicated Worker application exists.

Desired behavior:
- Auth Worker owns OAuth and integration application state.
- MCP Worker owns tools and Habitica execution.
- MCP Worker validates OAuth tokens over Service Binding.

Bindings:

Auth Worker:

```text
OAUTH_KV
HABITICA_INTEGRATION_DB
PROFILE_ID_KEY                 secret
CREDENTIAL_KEY_V1             secret
CREDENTIAL_CURRENT_KEY_VERSION
HABITICA_X_CLIENT_HEADER
```

MCP Worker:

```text
AUTH_SERVER                    service binding
HABITICA_X_CLIENT_HEADER
```

If credentials are decrypted only in the auth Worker, MCP should request a narrow internal credential-bearing operation or credential lease over Service Binding rather than receive a long-lived secret.

Preferred design:
- MCP asks auth/internal service for an authenticated short-lived operation context.
- Keep plaintext Habitica credentials in Worker memory for the minimum request duration.

Dependencies:
- Task 1.

Out of scope:
- full OAuth authorization UI.
- MCP tools.

Implementation constraints:
- use current pinned compatible versions of:
  - `@cloudflare/workers-oauth-provider`;
  - MCP TypeScript SDK / Cloudflare handler.
- `OAUTH_KV` is bound to the Auth Worker.
- MCP Worker has no direct OAuth KV binding.
- exact canonical MCP resource URI is configured once.
- preview and production resources are separate unless explicitly documented otherwise.

Questions and required clarifications:
- [Recommended default - confirmation optional] Separate Workers vs combined Worker?
  Recommendation:
  - separate Workers.
- [Recommended default - confirmation optional] Use root Wrangler config for both?
  Recommendation:
  - no; each Worker should own its deployment config.

Validation:
- deploy preview Auth Worker.
- deploy preview MCP Worker.
- Service Binding health check.
- protected-resource metadata resolves to the correct authorization issuer.
- wrong-resource tokens are rejected.

Acceptance:
- Workers deploy independently.
- MCP resource Worker can validate tokens through Auth Worker.
- no existing Pages binding is reused for integration state.

Documentation updates:
- `TECHNICAL.md`
- deployment docs.
- repository map if one is later added.

Risks:
- preview/production binding mistakes.
- configuration drift between two Workers.

---

Touch:
- Proposed `workers/chatgpt-auth/**` and `workers/chatgpt-mcp/**`, each with deployment configuration.
- Worker package manifests, integration database migrations and focused tests.
- `TECHNICAL.md`, `docs/DEPLOY_CLOUDFLARE_PAGES.md`.

Discovery allowance:
- Create isolated Worker directories and dedicated integration D1/OAuth KV bindings.
- Inspect current SDK APIs and Wrangler conventions before deciding specific filenames.

---

### 3. Implement Stable Profile Identity, Credential Vault, And Connection Lifecycle

Priority: Top

Status: Ready

Goal:

Create a stable multi-user integration identity and encrypted per-user Habitica credential vault.

Data model:

```text
integration_profiles
--------------------
profile_id                  primary key
credential_ciphertext
credential_iv
credential_key_version
credential_schema_version
display_name_cache          nullable
created_at_utc
updated_at_utc
last_verified_at_utc
last_used_at_utc
revoked_at_utc
management_token_hash
management_token_rotated_at_utc
```

The encrypted credential payload should contain:

```json
{
  "habiticaUserId": "...",
  "apiToken": "..."
}
```

No plaintext Habitica User ID is required in the row.

Profile ID:

```text
HMAC-SHA256(PROFILE_ID_KEY, canonicalHabiticaUserId)
```

Credential encryption:
- AES-256-GCM;
- random 96-bit IV;
- explicit `keyVersion`;
- AAD binds profile ID, credential schema version, and a purpose string.

Current behavior:
- no server-side Habitica credential storage.

Desired behavior:
- valid Habitica credentials can be explicitly enrolled.
- same Habitica user resolves to same opaque profile ID.
- re-enrollment rotates credentials rather than producing a second logical profile.
- encrypted credentials can be deleted/revoked.

Touch:
- Auth Worker.
- dedicated integration migration(s).
- cryptography tests.
- deployment secrets documentation.

Dependencies:
- Task 2.

Out of scope:
- OAuth consent.
- MCP tools.

Implementation constraints:
- validate Habitica credentials before persistence.
- never log enrollment bodies.
- management token is at least 256 random bits.
- store only management-token hash.
- compare hashes in constant-time where practical.
- separate profile-ID key from credential-encryption key.
- support multiple credential encryption key versions from day one.

Failure handling:
- invalid Habitica credentials -> no row.
- encryption failure -> no row.
- database failure -> no secret in response/error.
- unknown key version -> fail closed.
- revoked profile -> cannot issue credential context.

Questions and required clarifications:
- [Recommended default - confirmation optional] Cache display name?
  Recommendation:
  - yes, but treat it as optional user data and refresh opportunistically.
- [Recommended default - confirmation optional] One active credential set per Habitica profile?
  Recommendation:
  - yes.

Validation:
- deterministic same-user profile ID.
- different users produce different IDs.
- profile ID does not contain User ID.
- encrypt/decrypt round-trip.
- wrong AAD fails.
- wrong key fails.
- key-version rotation test.
- known test token absent from logs and database fixtures.

Acceptance:
- two Habitica users remain fully isolated.
- same user can rotate credentials without changing profile ID.
- no raw credential is persisted.

Documentation updates:
- `TECHNICAL.md`
- privacy/security docs.
- deployment docs.

Risks:
- misuse of master keys.
- decryptable server-side storage is intentionally a different trust model than current browser-only use.

---

Discovery allowance:
- Add a dedicated integration database schema, credential envelope utilities and isolated cryptography tests.
- Preserve existing personal-sync KV and party-sync D1 security boundaries.

---

### 4. Implement One-Time Linking And OAuth 2.1 Authorization

Priority: Top

Status: Ready

Goal:

Allow a Companion user to link ChatGPT without creating email/password accounts.

Enrollment flow:

```text
Companion Settings
    -> Enable ChatGPT Integration
    -> privacy disclosure
    -> send current credentials to Auth Worker over HTTPS
    -> validate against Habitica
    -> create/update profile
    -> issue management token
    -> issue one-time linking code
```

OAuth flow:

```text
ChatGPT
    -> protected resource metadata
    -> Auth Worker /authorize
    -> linking page
    -> user enters one-time code
    -> display requested scopes and Habitica account label
    -> explicit consent
    -> authorization code + PKCE
    -> access / refresh token
```

OAuth Provider requirements:
- `OAuthAuthorizationServer`;
- `OAUTH_KV`;
- exact resource registration;
- PKCE S256;
- protected-resource metadata;
- CIMD enabled when deployment supports it;
- DCR only as compatibility fallback where needed;
- resource parameter preserved;
- token audience validation;
- per-tool scopes;
- step-up authorization through `insufficientScope()` where appropriate.

Use:

```text
OAuth userId = profileId
OAuth props = { profileId }
```

One-time linking ticket model:

```text
linking_tickets
---------------
ticket_id
profile_id
code_hash
created_at_utc
expires_at_utc
consumed_at_utc
failed_attempts
```

Recommended code:
- 12 Crockford Base32 characters;
- random;
- single use;
- <= 10 minute TTL.

Atomic consume:
- use one conditional D1 mutation;
- exactly one authorization flow can consume a ticket.

Current behavior:
- no OAuth identity.

Desired behavior:
- ChatGPT authorization always maps to exactly one stable profile.
- scope grant is explicit.
- write scopes can be added later by step-up.

Dependencies:
- Task 3.

Out of scope:
- actual Habitica MCP tools.

Implementation constraints:
- no API token in OAuth state, code, token, props, redirect URI, or browser URL.
- OAuth state and CSRF protections enforced.
- strict redirect-URI validation delegated to/provider-verified through OAuth library.
- pairing attempts rate-limited by ticket/auth session and network source.
- consumed ticket cannot be replayed.
- authorization denial issues no grant.

Initial scope policy:
- do not request all scopes at installation.
- request the scope needed by the invoked tool.
- later write requests trigger step-up authorization.

Failure handling:
- expired ticket -> user generates a new one.
- consumed ticket -> reject.
- revoked profile -> authorization fails.
- lost local management token -> revalidate Habitica credentials and rotate management token.

Validation:
- auth code + PKCE happy path.
- wrong PKCE verifier.
- invalid redirect URI.
- missing/wrong resource.
- expired/consumed pairing code.
- concurrent consume race.
- brute-force attempt limit.
- OAuth denial.
- scope step-up.
- token for another resource rejected.
- two-profile isolation.

Acceptance:
- two different teammates can connect the same MCP server to their own Habitica accounts.
- OAuth access for A never resolves B's vault.
- no standalone Companion account/password system is required.

Documentation updates:
- `FEATURES.md`
- `TECHNICAL.md`
- deployment docs.
- privacy UI.
- UX manifest.

Risks:
- pairing code becomes account authentication and must be protected accordingly.
- consent UI may be confused with Habitica's own login unless wording is explicit.

---

Touch:
- Proposed `workers/chatgpt-auth/**` and integration D1 migrations.
- `src/Habitica.WebApp/Pages/SettingsPage.razor`, `src/Habitica.WebApp/Pages/PrivacyPage.razor`.
- New narrowly scoped linking client under `src/Habitica.WebApp/`.
- Focused OAuth and WebApp tests; `FEATURES.md`, `TECHNICAL.md`, `docs/UX_UI_MANIFEST.md`, deployment docs.

Discovery allowance:
- Implement a short-lived linking ticket store and small consent page; verify current OAuth SDK behavior before coding.

Questions and required clarifications:
- [Recommended default - confirmation optional] Should pairing be used instead of an account/password system?
  Recommendation: Use single-use, short-lived pairing tickets tied to validated local Habitica credentials.
  Rationale: Avoids a separate login system and cross-origin cookie assumptions.

---

### 5. Implement Read-Only MCP Baseline

**Revised scope gate (2026-10-12):** This task may ship a legacy Habitica read-only bootstrap (`get_profile`, `get_account_status` and clearly labeled Habitica task views) while Companion product epics are pending. It must **not** pretend that direct Habitica task lists include Companion-only projects, goals, plans or completed history. Canonical Companion read tools are implemented after E02/E03/E04 and are part of E13.

Priority: Top

Status: Ready

Goal:

Provide a standards-compliant, useful, read-only integration before any Habitica mutations.

Initial tools:

```text
get_profile
get_account_status
list_tasks
get_task
```

#### `get_profile`

Purpose:
- OpenAI multi-account identity only.

Input:

```json
{}
```

Output must match the OpenAI profile schema exactly:

```json
{
  "id": "prf_...",
  "name": "Habitica display name",
  "nickname": "Habitica — <display name>"
}
```

Do not return HP, MP, class, task count, party, credentials, or other fields.

Metadata:

```text
_meta["openai/profile"] = true
readOnlyHint = true
destructiveHint = false
openWorldHint = false
```

#### `get_account_status`

Return structured Companion-owned fields such as:
- class;
- level;
- HP;
- max HP;
- MP;
- max MP;
- XP;
- next-level target when available;
- GP;
- `needsCron`;
- party presence/id where appropriate;
- retrieval timestamp.

#### `list_tasks`

Filters:
- type;
- completed/active;
- due only;
- text query;
- optional limit.

Rules:
- request Habitica due-state evaluation for current Dailies.
- do not invent schedule results when `isDue` is unavailable.
- return stable task IDs.

#### `get_task`

Prefer a verified direct Habitica endpoint if documented at implementation time.

Otherwise:
- reuse the fetched user-task set and resolve by stable ID.

Do not introduce an undocumented API assumption merely for efficiency.

Touch:
- MCP Worker.
- narrow TypeScript Habitica API client.
- fixtures/tests.
- `FEATURES.md`
- `TECHNICAL.md`
- `HABITICA_API.md` only if newly verified contracts are added.

Dependencies:
- Task 4.

Out of scope:
- writes.
- Cron.
- spells.
- party mutations.
- custom UI.

Implementation constraints:
- Streamable HTTP.
- stateless handler for new MCP tools.
- every tool resolves profile from validated request credentials.
- no tool accepts `userId`, API token, or profile selector as an authorization argument.
- per-tool `securitySchemes`.
- `outputSchema` for every structured result.
- `openWorldHint=false`.
- stable tool-creator `x-client`.
- respect upstream rate-limit headers.
- no background polling.

Failure handling:
- Habitica 401 -> mark credential repair required.
- Habitica 429 -> propagate retry guidance; do not loop.
- malformed optional fields -> return partial known state.
- vault unavailable -> service error without secret details.

Diagnostics:
- correlation ID;
- profile ID in hashed/redacted form;
- tool name;
- upstream route category;
- duration;
- status;
- rate-limit metadata.
- no raw request/response dumps by default.

Validation:
- MCP Inspector initialization.
- tool list.
- schema validation.
- auth required.
- wrong scopes.
- profile strict schema.
- two-account profile identity.
- Daily due-state.
- 401/429/5xx handling.
- secret scan.

Acceptance:
- ChatGPT can correctly answer:
  - "Which Habitica account is connected?"
  - "What are my current stats?"
  - "Which Dailies are still due?"
  - "Show unfinished To Dos."
- no tool in this phase can mutate Habitica.

Documentation updates:
- `FEATURES.md`
- `TECHNICAL.md`
- MCP deployment docs.

Risks:
- TypeScript/C# mapping drift.
- over-sharing data in tool results.

Mitigation:
- keep server schemas narrow;
- reuse neutral JSON fixtures for parity where useful.

---

Current behavior:
- No OAuth-protected MCP endpoint exists; the browser C# client reads Habitica directly.

Desired behavior:
- Authenticated semantic read tools with validated schemas. Habitica-only task reads are explicitly **legacy/bootstrap** until E02/E03/E04 define the canonical Companion workspace.

Discovery allowance:
- Add narrow TypeScript read models and fixtures without porting the whole C# application layer.

Questions and required clarifications:
- [Recommended default - confirmation optional] Can legacy Habitica task reads ship before Companion-owned records?
  Recommendation: Yes, for read-only bootstrap only; label their provenance explicitly.
  Rationale: Connection/authentication can be tested without creating a parallel task authority.

---

### 6. Add Safe Task Mutation Tools

**Revised status:** **Blocked pending E02/E03/E04/E05** (and E07 for unattended hosted execution). The legacy section below documents useful semantic tool contracts and validation, but **the target write implementation MUST call the Companion authoritative command API** (`create task / update / completion event / outbox`) rather than directly calling Habitica from MCP. MCP response must report `savedInCompanion` separately from `habiticaProjectionStatus`. Do not expose a direct-write parallel path even as a convenient MVP shortcut. Add native category/project/goal/label tools under E13; authoring those tool schemas is a high-level epic until domain definitions are approved.

Priority: Top

Status: Blocked pending E02/E03/E04/E05 and E07 for unattended execution

Goal:

Add the smallest useful write surface for everyday Habitica task management.

Tools:

```text
create_todo
update_task
complete_task
score_habit
```

Later, after contract verification:

```text
create_daily
create_habit
```

Do not initially expose:

```text
delete_task
create_reward
generic_create_task
generic_score_task
```

#### `create_todo`

Allowed fields:
- text;
- notes;
- priority;
- due date;
- attribute;
- checklist.

Whitelist fields.

Do not accept arbitrary Habitica JSON.

#### `update_task`

Rules:
- stable task ID required;
- partial updates only;
- whitelist fields;
- prefetch current task when type/state matters.

#### `complete_task`

Rules:
- stable task ID;
- only supported completable task types;
- re-read current state immediately before mutation;
- mark `destructiveHint=true` because Habitica scoring has game-state effects;
- refresh affected state after success.

#### `score_habit`

Rules:
- verify target is a Habit;
- explicit `up|down`;
- bounded count;
- recommended initial maximum: 10;
- sequential requests;
- stop on first failure;
- report successful count and failure position;
- `destructiveHint=true`.

Idempotency/replay policy:

Do not assume MCP request ID is a durable idempotency key.

Before implementation:
- verify exact ChatGPT/MCP retry behavior available to the Worker;
- record an operation correlation id;
- never automatically retry a non-idempotent upstream call after an ambiguous network result.

For operations where a duplicate would be materially harmful, promote the action to a server-issued prepare/execute workflow.

Mutation result contract:
- operation;
- task ID;
- task text where safe/useful;
- mutation confirmed/not confirmed;
- successful sub-operation count;
- refresh success;
- updated stats where returned and useful;
- explicit ambiguous-state flag when the upstream result is uncertain.

Dependencies:
- Task 5.

Out of scope:
- delete.
- bulk delete.
- purchases.
- spells.
- Cron.
- equipment.

Implementation constraints:
- `habitica.tasks.write`.
- step-up scope when current grant lacks it.
- write annotations accurate.
- server-side validation independent of Skills.
- no blind retry.
- no mutation based only on fuzzy task-name match when multiple candidates exist.

Failure handling:
- ambiguous fuzzy match -> return candidates; do not mutate.
- upstream timeout after request may have been sent -> return `outcomeUnknown`; do not replay automatically.
- success + refresh failure -> report mutation success separately.
- partial repeated scoring -> report exact successful count.

Validation:
- create valid Todo.
- invalid priority.
- invalid checklist.
- partial update.
- completion of Todo.
- completion of Daily.
- attempt to complete Habit through wrong tool.
- habit up/down.
- bounded repeated scoring.
- timeout/ambiguous outcome.
- 429.
- insufficient OAuth scope.
- cross-user task-ID attempt.
- no secret leakage.

Acceptance:
- natural-language task management works without generic API access.
- every mutation outcome is auditable and unambiguous where the upstream response is known.
- ambiguous network outcomes are never replayed automatically.

Documentation updates:
- `FEATURES.md`
- `TECHNICAL.md`
- `HABITICA_API.md` if new verified endpoint details are added.

Risks:
- duplicate mutations.
- users not realizing task completion affects game state.
- fuzzy matching mistakes.

---

Current behavior:
- Companion's current task mutations target Habitica; the canonical Companion command/outbox layer is not implemented.

Desired behavior:
- MCP writes persist Companion commands via the same authoritative command API used by the PWA, and separately report locally saved vs Habitica-projected outcomes.

Touch:
- Proposed `workers/chatgpt-mcp/**` and shared Companion command API (final paths depend on E02/E03 decomposition).
- `src/Habitica.Application/**`, `src/Habitica.Domain/**` as justified by the canonical domain contract.
- Targeted tests and `FEATURES.md`, `TECHNICAL.md`, `HABITICA_API.md` where relevant.

Discovery allowance:
- First research/complete E02/E03/E04/E05 and resolve E07 for unattended execution; update exact symbols and persistence choices before implementation.

Questions and required clarifications:
- [Unresolved - blocks implementation] What is the authoritative Companion command API and trusted background executor boundary?
  Why it matters: Direct MCP-to-Habitica writes would bypass the Companion source of truth, command journal and reconciliation.
  Required answer from: E02/E03/E04/E05/E07 decomposition and implementation.

---

### 7. Package The Plugin And Add Core Skills

**Revised integration:** Add Skills for project planning, goal review, upcoming/calendar, completed-history and sync-status analysis only after E13 exposes the matching authenticated Companion-native tools. Until then Skills must accurately describe the legacy Habitica-only bootstrap capabilities.

Priority: Middle

Status: Ready

Goal:

Package stable MCP capabilities with focused reusable workflows after the tool contract has proven reliable.

Portable target layout:

```text
plugin/
  plugin.json
  mcp.json
  skills/
    habitica-daily-review/
      SKILL.md
    habitica-task-management/
      SKILL.md
    habitica-account-review/
      SKILL.md
```

Optional compatibility layout may also be produced when needed:

```text
.codex-plugin/plugin.json
.mcp.json
```

Do not treat compatibility files as the canonical portable manifest.

Dependencies:
- Task 6.

Out of scope:
- public Plugin Directory submission.
- custom MCP UI.
- advanced spell/Cron skills.

#### Skill: `habitica-daily-review`

Workflow:
1. call `get_account_status`;
2. call `list_tasks` for current due Dailies;
3. separate confirmed-due, completed, and unavailable/unknown state;
4. mention `needsCron` only as status;
5. do not start Cron;
6. do not complete tasks without explicit user intent;
7. produce a compact prioritized summary.

#### Skill: `habitica-task-management`

Workflow:
1. identify the requested task operation;
2. resolve existing tasks by read tools;
3. if more than one candidate matches, ask/select instead of mutating;
4. call only semantic allowlisted write tools;
5. report actual server result;
6. never construct raw Habitica requests.

#### Skill: `habitica-account-review`

Workflow:
1. load account status;
2. load relevant tasks only when needed;
3. distinguish source facts from recommendations;
4. do not invent exact HP/XP/GP/Cron outcomes.

Implementation constraints:
- Skills contain no secrets.
- Skills do not enforce security policy; server does.
- Skills do not duplicate Habitica formulas unnecessarily.
- descriptions should target distinct user goals.
- validate current OpenAI plugin packaging schema at implementation time.

Testing:
- direct activation.
- indirect activation.
- incomplete input.
- negative/non-trigger examples.
- write intent vs read-only intent.
- denied scope.
- ambiguous task.

Acceptance:
- plugin can be tested by at least two team users.
- no user copies a Habitica token into ChatGPT.
- skills consistently use MCP tools rather than inventing results.

Documentation updates:
- `README.md`
- `FEATURES.md`
- plugin testing/setup docs.

Risks:
- overlapping skill activation.
- Skills drifting into hidden business logic.

---

Current behavior:
- There is no validated repository-owned ChatGPT plugin or Agent Skills package.

Desired behavior:
- A portable plugin with distinct Skills driving validated MCP workflows, using Companion-native contracts where available.

Touch:
- Proposed `plugin/**`; `README.md`; `FEATURES.md`; plugin setup/validation docs; focused tests.

Discovery allowance:
- Use current official OpenAI plugin/Skills schemas and avoid unverifiable compatibility packages.

Questions and required clarifications:
- [Recommended default - confirmation optional] Publish publicly immediately?
  Recommendation: No; validate private/team use first.
  Rationale: Auth and credential-handling integration should be proven before public distribution.

Validation:
- Validate plugin/Skill schemas, positive and negative triggers, denied scopes, account isolation and real MCP call routing.

---

### 8. Add Party And Spell Capabilities Incrementally

**Revised integration:** The eventual unified scheduler/outbox from E05/E07 owns command lifecycle for server-managed gameplay mutations. Existing party-sync `local-claim-v1` is not proof of identity and must not grant new MCP party writes. Require authenticated party authorization and role checks from trusted evidence before exposing queue/vote operations.

Priority: Middle

Status: Blocked pending E05 and authenticated party write authorization

Goal:

Expand beyond task management without copying the existing C# application layer wholesale into TypeScript.

Rollout order:

```text
1. get_party_context
2. get_quest_planning_state
3. queue_quest
4. vote_quest
5. get_spell_context
6. prepare_spell_cast
7. execute_spell_cast
```

Dependencies:
- stable Tasks MVP.
- Skill packaging can happen before or after this task.

Out of scope:
- automatic background spell casting.
- unlimited cast loops.
- arbitrary macro execution.
- purchases.
- Start New Day in the same implementation change.

Implementation constraints:
- one advanced mutation category per implementation change.
- verify each Habitica endpoint against project docs and current upstream source.
- reuse existing party-sync state only through a narrow internal contract.
- do not expose party-sync administrative actions automatically.
- bounded spell repetitions only.
- prefetch fresh user/target state.
- action plans expire quickly.
- execute token is single-use.

Prepare/execute record:

```text
prepared_actions
----------------
action_id
profile_id
action_type
payload_hash
precondition_hash
created_at_utc
expires_at_utc
consumed_at_utc
```

Do not persist API token, raw OAuth token, or unnecessary full Habitica snapshots.

Parity strategy:
- identify representative C# behaviors.
- encode externally observable cases as neutral JSON fixtures.
- run C# tests against existing application logic.
- run Worker tests against matching expected safety outcomes.
- do not claim full parity when not actually covered.

Acceptance:
- party and spell tools preserve existing Companion safety assumptions.
- repeated mutations stop on failure.
- stale/changed state prevents execution.

Documentation updates:
- `FEATURES.md`
- `TECHNICAL.md`
- `HABITICA_API.md`.
- party docs as relevant.

Risks:
- C#/TypeScript logic drift.
- tool surface growing too broad.

---

Current behavior:
- Party, quest and spell flows are implemented in the browser C# application and existing trust-based party-sync functions.

Desired behavior:
- Advanced MCP operations use narrow shared authorization and command contracts without copying C# workflows or treating local party claims as trusted remote identity.

Touch:
- Proposed `workers/chatgpt-mcp/**`, targeted Companion domain/application contracts, relevant party-sync code only after authenticated design review.
- Direct tests and `FEATURES.md`, `TECHNICAL.md`, `HABITICA_API.md`; party documentation as needed.

Discovery allowance:
- Add test fixtures and verified internal reads; do not silently change party-sync authorization.

Questions and required clarifications:
- [Unresolved - blocks implementation] What proof authorizes party writes initiated through MCP?
  Why it matters: Existing party local claims do not prove a remote user has the required permissions.
  Required answer from: Party authorization research and E05 shared command layer.

Validation:
- Verify party role checks, cross-profile isolation, stale state, spell plans, sequential execution, partial failure and parity fixtures.

---

### 9. Add Start New Day Only As A Dedicated High-Risk Feature

**Revised integration:** The prepared Cron operation enters the same durable command ledger. A timeout must transition to `UnknownOutcome` and reconcile with current Habitica `needsCron`/Cron state; never automatically send `POST /cron` again merely because the first response is missing.

Priority: Middle

Status: Blocked pending E05 and safe Cron reconciliation

Goal:

Expose Cron only after the integration has proven safe under real multi-user use.

Tools:

```text
get_cron_status
prepare_start_new_day
execute_start_new_day
```

Do not provide:

```text
start_new_day()
```

as an immediate one-step mutation.

Preparation must gather:
- current `needsCron`;
- fresh user state;
- current due unfinished Dailies;
- relevant quest state where available;
- warnings about temporary buffs;
- estimated/unknown risk fields according to existing Companion rules;
- expiration timestamp.

Execution:
- consumes single-use action token;
- re-fetches safety-critical state;
- aborts if important state changed;
- calls Cron once;
- never auto-retries Cron;
- refreshes user/tasks/party;
- if refresh fails after Cron, returns "Cron succeeded; refresh failed".

OAuth scope:

```text
habitica.cron.execute
```

Annotations:

```text
readOnlyHint=false
destructiveHint=true
openWorldHint=false
```

Dependencies:
- Tasks 0-8.
- dedicated review of `CRON.md`.

Out of scope:
- scheduled Cron.
- automatic Cron because `needsCron=true`.
- chaining Cron + spell without a separately prepared plan.

Validation:
- no-Cron-needed state.
- normal prepared execution.
- plan expiration.
- Daily state changed.
- quest state changed.
- upstream Cron failure.
- Cron success + refresh failure.
- repeated execution token.
- network ambiguity.
- scope denial.

Acceptance:
- Cron cannot execute from a stale preparation.
- same action token cannot execute twice.
- no automatic retry can repeat Cron.

Documentation updates:
- `CRON.md`
- `FEATURES.md`
- `TECHNICAL.md`
- skill docs if added.

Risks:
- irreversible gameplay impact.
- double Cron if replay handling is wrong.
- stale client assumptions.

---

Current behavior:
- The PWA implements an explicitly confirmed Start New Day flow; no remote Cron executor exists.

Desired behavior:
- Two-phase Cron through a durable command journal, with unknown-outcome resolution and no automatic replay.

Touch:
- Proposed `workers/chatgpt-mcp/**`, shared command/safety executor from E05.
- `CRON.md`, `FEATURES.md`, `TECHNICAL.md`, `HABITICA_API.md`, focused Worker/Cron tests.

Discovery allowance:
- Review live Cron rules and tests and add prepare/execute only after the durable command contract is defined.

Implementation constraints:
- Require scope `habitica.cron.execute`, explicit intent, short-lived single-use prepared action and fresh precondition verification.
- Treat timeout/unknown result as potentially successful; never blindly rerun Cron.
- Preserve Habitica day boundary, buffs and quest-risk handling from `CRON.md`.

Questions and required clarifications:
- [Unresolved - blocks implementation] How will an ambiguous Cron result be reconciled safely?
  Why it matters: A second Cron call may have irreversible gameplay effects.
  Required answer from: E05 and current Habitica server/API verification.

---

### 10. Security Hardening, Revocation, Key Rotation, And Team Rollout

**Revised integration:** Add owned Companion workspace export/deletion, migration and browser-storage recovery; conflict-state review; multi-device version tests; mobile accessibility/performance; queue aging and stalled-command observability; and hosted-executor revocation behavior. Confirm these via E14 before broader release.

Priority: Middle

Status: Ready

Goal:

Make the integration operationally safe before broad availability.

Settings should show:

```text
ChatGPT Integration

Connected account: <Habitica display name>
Permissions: ...
Last used: ...
Credential status: valid / repair needed

[Generate new linking code]
[Revoke ChatGPT grants]
[Rotate stored Habitica credentials]
[Disconnect and delete server credentials]
```

Distinguish:

#### Revoke ChatGPT grants

- revoke OAuth grants/tokens for `profileId`;
- retain encrypted credential enrollment if user wants to reconnect later.

#### Disconnect and delete server credentials

- revoke OAuth grants;
- delete credential envelope;
- delete management token hash;
- invalidate linking tickets;
- delete prepared actions;
- retain only permitted short-lived/non-identifying audit data.

Key rotation:
- `CREDENTIAL_KEY_V1`, `V2`, etc.
- one configured current version.
- new writes use current key.
- old versions remain readable until migrated.
- old key cannot be removed while active rows reference it.
- profile-ID key rotation is a separate migration problem and should not be casually rotated because it defines stable OpenAI identities.

Rate-limit hardening:
- auth endpoint limits;
- pairing attempt limits;
- MCP per-profile limits;
- global protection;
- upstream 429 monitoring;
- no retry storm.

Habitica-specific operational monitoring:
- detect unusual 429 rates across different users.
- watch for evidence that shared Worker egress contributes to IP-level throttling.
- keep server call patterns conservative.
- no background polling.

Rollout:

```text
Stage 1: owner account
Stage 2: one trusted teammate
Stage 3: several known team users
Stage 4: optional wider Companion users
Stage 5: public plugin review only if intentionally desired
```

Dependencies:
- all capabilities included in the chosen release.

Validation:
- cross-user isolation.
- OAuth revocation.
- full credential deletion.
- management-token rotation.
- encryption-key rotation.
- expired access/refresh token.
- invalid resource/audience.
- invalid scopes.
- pairing brute-force controls.
- production/preview separation.
- log scan for secrets.
- upstream 429 behavior.
- account reconnect preserves profile ID.

Acceptance:
- a user can fully remove server-side Habitica credentials.
- revocation becomes effective immediately for new tool calls.
- multiple users remain isolated.
- no secret appears in production logs.
- non-opted-in Companion users remain local-only.

Documentation updates:
- `FEATURES.md`
- `TECHNICAL.md`
- `RULES.md`
- deployment docs.
- privacy docs.
- UX manifest.

Risks:
- operational misconfiguration.
- losing a key before migration.
- OAuth grants surviving credential deletion.
- upstream IP-level rate limiting after centralizing requests.

---

Current behavior:
- The existing application has no ChatGPT-specific grant revocation, credential deletion lifecycle, security dashboard, or key rotation workflow.

Desired behavior:
- Team-ready multi-user integration with auditable revocation, vault deletion, key rotation, protected deployment environments and controlled release gates.

Touch:
- Proposed `workers/chatgpt-auth/**`, `workers/chatgpt-mcp/**`, integration migrations.
- `src/Habitica.WebApp/Pages/SettingsPage.razor`, `src/Habitica.WebApp/Pages/PrivacyPage.razor`.
- Direct tests, `FEATURES.md`, `TECHNICAL.md`, `RULES.md`, UX and deployment documentation.

Discovery allowance:
- Add narrowly scoped security tooling and metrics without changing unrelated Pages party sync.

Out of scope:
- Public plugin listing, background automation, full conversation logging and general-purpose usage analytics.

Implementation constraints:
- Grant revocation and credential deletion are separate operations.
- Secrets never enter logs or error bodies; preview/production resources are isolated.
- Credential encryption-key rotation and stable profile-ID key rotation require separate procedures.

Questions and required clarifications:
- [Recommended default - confirmation optional] Which rollout group is appropriate?
  Recommendation: Owner first, one trusted teammate second, then an expanded opt-in group.
  Rationale: Verify revocation, privacy, isolation and rate-limit behavior before expanding.


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

---

### Public Plugin Directory Submission

Do only after private/team usage is stable.

Before submission:
- verify current OpenAI review requirements;
- organization/individual publisher verification;
- test/demo account requirements;
- review test cases;
- privacy/support URLs;
- stable production endpoints;
- no developer-only config.

Private/workspace usage should continue through custom MCP/plugin distribution until public publication is intentional.

### MCP Custom UI

Potentially useful for:
- ambiguous task selection;
- spell target review;
- quest planning;
- Start New Day preparation review.

Do not add UI merely to duplicate normal model text.

### Background Automation

Separate architecture/security project.

Examples:
- Daily review notification;
- quest state alerts;
- Cron reminders.

Constraints:
- read-only by default;
- no silent mutations;
- no aggressive polling;
- Habitica automated-background calls must respect the documented minimum interval.
