# Future Work

Task definitions reside in `skill-tree/documents/future.md`; `future.json` contains the generated index and authoritative active checkpoint. `skill-tree/skill-tree.json` indexes document ownership and freshness. Current behavior belongs in features.md; architecture belongs in technical.md. Research reference material never supplies implementation selection or authorization.

## Implementor Rules

1. Read the relevant source-of-truth docs before editing:
   - UI/UX: `skill-tree/documents/ux-ui-manifest.md`
   - Architecture, sync, storage: `skill-tree/documents/technical.md`
   - Habitica API rules: `skill-tree/documents/habitica-api.md`
   - Habitica party/quest link behavior: `skill-tree/documents/habitica-deeplinks.md`
   - Cloudflare deployment and D1/KV: `skill-tree/documents/deploy-cloudflare-pages.md`
2. Implement one entry only. Do not bundle unrelated cleanup, renames, or opportunistic refactors.
3. Respect `Touch:` and `Discovery allowance:`. Research missing paths before implementation; capture required scope changes through the selected controller instead of silently broadening the task.
4. Add or update tests next to affected code. UI behavior changes need Razor component tests where similar tests exist.
5. User-facing behavior changes must update `skill-tree/documents/features.md`; sync architecture or backend behavior changes must update `skill-tree/documents/technical.md`.
6. Schema changes need the next numbered migration in the owning database directory and a deployment-doc update. Existing party D1 uses `migrations/`; the proposed integration database uses `workers/chatgpt-auth/migrations/`. Never mix credential-vault migrations into party D1.
7. Never send Habitica API tokens to Cloudflare party-sync or app-data sync endpoints.
8. Keep labels short and plain. If UI copy is ambiguous, choose the smallest clear label and proceed.
9. If a needed Habitica endpoint is not documented in `skill-tree/documents/habitica-api.md`, stop and add a follow-up entry instead of guessing.
10. For Habitica party/quest links, use stable web URLs only. Do not add `habitica://`, Android `intent://`, app-opening probes, or mobile-app-specific party/quest links unless `skill-tree/documents/habitica-deeplinks.md` is updated with new official support.
11. Process Pending performs research only. Standalone processing reports the prioritized list and presents one recommendation last; implementation requires an actual developer selection. Explicit Work Through Next or Implement All continues only inside the selected root or frozen snapshot.
12. Select the highest numeric prioritized priority; document order breaks ties. A blocked highest-ranked task stops default selection. Exact selection never falls back to another task.
13. Record acceptance and terminal outcomes before removing tasks. An empty queue or absent task never proves completion.
14. Task dependencies use stable T- identities; Pending epic gates require researched promotion and recorded completion before dependent implementation. Readiness grants no live API, deployment, credential, commit or publication permission.


## Companion-First Integration Planning Gates

- `skill-tree/documents/future.md` is the **only active implementation queue**. The architecture and [competitive mobile UX research](companion-first-mcp-research.md) are reference material, not a second queue.
- The **14 E01–E14 high-level product, UX and reliability epics** below are raw research/decomposition intake. Use exact P- IDs or full task titles with `Process pending`; E01–E14 are provenance labels. Decompose selected intake into independently verifiable tasks, preserving every originating Pending ID and recording required T- dependency edges.
- The **11 MCP/OAuth contracts** below carry explicit dependencies and blockers. `Ready` means the task contract has no unanswered blocking question; unfinished prerequisites remain in `depends_on` and must complete before execution. `Implement next` must not skip a blocked highest-ranked task.
- Once Companion-owned task/project/goal storage is established, Habitica is a game projection. No parallel direct-to-Habitica MCP write path is allowed.
- Persist accepted application commands, but do **not** promise exactly-once remote rewards when Habitica reports a timeout without upstream idempotency. Preserve unknown outcomes for reconciliation.
- Conduct hands-on competitor/mobile usability work in E01 before implementation-specific UI tasks are promoted.


## Pending Task Format

Pending intake requires a stable P- identity and the fixed metadata fields. Add Pending records understood intent; Process Pending performs source research before promotion. Use a level-three title immediately followed by the metadata fence, then concise nested bullets:

~~~~markdown
### Task title
```yaml
id: P-<uuid>
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- State the change, reason and relevant context.

Expected behavior:
- State an observable outcome.

Source:
- Record request or finding provenance.

Current context:
- Name verified implementation, affected paths and unresolved constraints when relevant.
~~~~

No full implementation plan is required for Pending intake. Preserve supplied requirements. Recheck code, tests, configuration and official external behavior before promotion. Promoted tasks receive T- identities and all originating Pending IDs; deferred tasks retain B- identities.

## Pending Queue

Research/decomposition intake; no implementation selection. Equal numeric priorities follow document order.

### E01 Competitive UX/IA study and mobile-first product contract

```yaml
id: P-4f98b2b3-af24-5424-b2f9-066913f7133d
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E01` (`P-4f98b2b3-af24-5424-b2f9-066913f7133d`); requirements are intake awaiting current-source research and decomposition.

Description:
- Benchmark Todoist, Things 3, TickTick, Amazing Marvin, Microsoft To Do and selective Asana/Linear/Jira flows. Produce a screenshot/flow inventory, 'borrow/adapt/reject' matrix, mobile IA, core personas and default-vs-advanced rules.

Current context:
- Existing Tasks page is a Habitica task-type list, not a modern daily planner or project workspace.

Requirements:
- Real screenshots/documented workflows; task capture, planning, project organization, calendar, completed history, recurrence, error/pending states; mobile-first and accessibility criteria; explicit no-corporate-PM scope.

Suggested implementation:
- `skill-tree/documents/ux-ui-manifest.md`, canonical design/research notes under `skill-tree/documents/` and large artifacts under ignored `skill-tree/artifacts/`, low-fidelity mobile prototypes.

Expected behavior:
- Written benchmark with dated links, documented observations vs hypotheses, tested journeys, and approved navigation prototype before detailed UI tickets.

### E02 Companion-owned canonical workspace and task domain

```yaml
id: P-9d3fb820-05c3-525b-8769-85c5423d2f53
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E02` (`P-9d3fb820-05c3-525b-8769-85c5423d2f53`); requirements are intake awaiting current-source research and decomposition.

Description:
- Define first-class Categories, Goals, Projects, Sections, Labels, Tasks, Subtasks, TaskOccurrences and lifecycle events; distinguish user task state from Habitica game state.

Current context:
- `TaskSnapshot` is Habitica-centric and lacks Companion-owned IDs, project/goals, lifecycle and recurrence history.

Requirements:
- Stable IDs, task kinds (Todo/Daily/Habit vs game projection), dates/planned/deadline, priority, states, goals progress, optional dependencies and staleness metadata, extensible schemas, safe deletion/archive.

Data/model behavior:
- Versioned local entities with opaque IDs and a separate external projection mapping. Define primary project vs multiple project memberships explicitly during decomposition.

Expected behavior:
- Approved schema/use-case contract, migration examples, invariants, round-trip fixtures, backward compatibility plan; no dependency on Habitica fields for Companion entity identity.

### E03 Local-first persistent storage, migration, and optional cloud workspace

```yaml
id: P-4f954f5a-1084-5c87-8e26-1bfc5fd95035
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E03` (`P-4f954f5a-1084-5c87-8e26-1bfc5fd95035`); requirements are intake awaiting current-source research and decomposition.

Description:
- Add durable task domain/event/outbox stores alongside existing key/value snapshots, preferences and presets; plan cross-device sync and server-readable opt-in workspace for remote MCP without silently weakening existing encrypted KV sync.

Current context:
- IndexedDB Dexie currently has one `kv` store; existing encrypted KV sync is browser-derived/opaque to backend.

Requirements:
- Atomic entity+event+outbox commits, IndexedDB schema upgrades, restore/export, storage eviction handling, multi-tab/multi-device versioning, merge/conflict semantics, opt-in account identity and privacy contract.

Edge cases:
- Offline client vs server changes, reset browser, import duplicate, stale cloud snapshot, migration rollback, mixed PWA versions.

Expected behavior:
- Documented local-only vs hosted guarantee levels, migratable schema, no lost preexisting data, explicit server-readability consent, migration rehearsals and recovery tests.

### E04 Habitica projection and reconciliation adapter

```yaml
id: P-bbb755c5-37d7-5637-862d-17faa8e12f99
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E04` (`P-bbb755c5-37d7-5637-862d-17faa8e12f99`); requirements are intake awaiting current-source research and decomposition.

Description:
- Treat Habitica API as gamification projection. Map Companion Tasks/Labels to Habitica task types/tags and reconcile missing, changed, completed and externally deleted tasks.

Current context:
- API reads/writes Habitica tasks directly, and task IDs currently come from Habitica.

Requirements:
- Stable Companion-to-Habitica IDs/aliases; import ownership policy; reserved alias/tag namespace only after upstream verification; safe create-then-complete chain; no duplicate awards after ambiguous timeout.

Edge cases:
- Externally deleted Todo, external edit, alias collision, already completed Todo, unknown score result, Habitica tag rename/delete, daily/habit repeated events.

Expected behavior:
- Companion-completed Todo absent in Habitica is safely created and completed when reconcilable; tests show no silent duplicate creates/scores and no resurrection loops.

### E05 Durable command journal and typed failure/recovery engine

```yaml
id: P-4779d03b-eecc-5d86-a4d0-4dfba9ee9178
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E05` (`P-4779d03b-eecc-5d86-a4d0-4dfba9ee9178`); requirements are intake awaiting current-source research and decomposition.

Description:
- Wrap the existing awaited Habitica action flows in a persisted application-level transactional outbox and consistent retry/reconcile semantics across manual PWA and MCP.

Current context:
- `AppSessionController.ScoreTaskAsync` freshness-gates and sequentially awaits scores and snapshot saves. The C# API client throttle is in-memory; no durable application command ledger exists.

Requirements:
- Submission persistence, dependency graph, per-task ordering, retries, state machine, leases, cancellation boundaries, typed API failures, unknown-outcome reconciliation and visible queue inspection.

Runtime behavior:
- UI never crashes from ordinary upstream 401/429/5xx/timeouts; pauses then resumes eligible commands; permanent blockers remain visible and are not silently dropped.

Expected behavior:
- Fault-injection tests for offline/timeout/429/5xx/concurrent tabs; accurately distinguished local save vs game sync; no blind retry of non-idempotent mutation.

### E06 Early-slowdown adaptive limiter and automatic controlled recovery

```yaml
id: P-676c0cb1-82b5-51e1-92c1-74303b58c59d
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E06` (`P-676c0cb1-82b5-51e1-92c1-74303b58c59d`); requirements are intake awaiting current-source research and decomposition.

Description:
- Redesign outbound Habitica traffic scheduling to slow earlier, preserve headroom and recover progressively on sustained success.

Current context:
- `HabiticaApiClient` token bucket uses adaptive ramp near depletion, but lacks coordinated cross-client durable backpressure and recovery logic.

Requirements:
- Actual header reconciliation, forward-looking budget, safety reserve, per-user plus possible shared-egress quotas, AIMD/circuit-breaker separation, backoff+jitter, fair scheduling, observability.

Expected behavior:
- In synthetic load tests rate reduction begins before near-zero budget; 429 and timeouts do not spin; stable success restores throughput gradually; no credential exposure in traces.

### E07 Optional always-on server executor and queue ownership handoff

```yaml
id: P-2dcaca70-8d94-55b1-aaee-7e7262e60f07
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E07` (`P-2dcaca70-8d94-55b1-aaee-7e7262e60f07`); requirements are intake awaiting current-source research and decomposition.

Description:
- Let opted-in users process Companion outbox commands and MCP edits after the browser closes, with durable server ledger, leases and safe resumption.

Current context:
- PWA cannot guarantee unattended background execution; the planned remote MCP integration requires an authenticated account connection; no MCP endpoint exists yet.

Requirements:
- Per-profile server authentication, accepted-command ACK boundary, local-to-server handoff/dedup, single execution owner, persisted retries, bounded sweep, optional Cloudflare Queues as transport, cleanup/revocation.

Edge cases:
- Split brain browser+Worker, disconnect mid-execution, credential revoked, Cloudflare Queue DLQ/expiry, duplicate delivery, mobile app shutdown.

Expected behavior:
- Simulated restart/redeploy/outage does not silently lose accepted commands; ambiguous upstream effects remain blocked for reconciliation rather than double-applied.

### E08 Mobile-first task capture, edit, and navigation redesign

```yaml
id: P-0ddc8784-c740-5a23-819c-b17db8f6829f
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E08` (`P-0ddc8784-c740-5a23-819c-b17db8f6829f`); requirements are intake awaiting current-source research and decomposition.

Description:
- Redesign Todos, Dailies and Habits for smartphone-first use, with minimal fast entry and advanced configuration when requested.

Current context:
- Existing `TasksPage.razor` is a large grouped list with expanding cards, filters and score controls.

Requirements:
- Fast Add from main views, one-handed touch design, clean task rows, keyboard/safe-area behavior, simple vs advanced detail sheet, quick reschedule, undo where safe, accessible date picker, responsive secondary desktop views.

Expected behavior:
- Common capture/completion/reschedule flows need few interactions on a narrow phone; no core task property requires desktop; WCAG/touch/keyboard tests and mobile usability review.

### E09 Today, Upcoming, Calendar, Inbox and Completed/Logbook views

```yaml
id: P-c8632138-39c6-5797-8e03-a3414824ee96
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E09` (`P-c8632138-39c6-5797-8e03-a3414824ee96`); requirements are intake awaiting current-source research and decomposition.

Description:
- Add a unified agenda/planning surface and durable completed history, not just the current Habitica task-type grouping.

Requirements:
- Today planner, Inbox, near-term Upcoming, Week/Month + agenda calendar, overdue, unscheduled/Anytime, completed history and restore, explicit occurrence/recurrence handling.

Data behavior:
- Distinguish all-day date/time, planned date/deadline, game Daily due-state and completion timestamp; calendar uses Companion records, not API-only recent completed limit.

Expected behavior:
- Fully usable on mobile; accurate date boundary across time zones/DST; no duplicate recurring Dailies; completed items remain visible/searchable after Habitica removes completed items.

### E10 Categories, goals, projects, progress and lightweight workflows UX

```yaml
id: P-0529505a-68be-5308-a805-44a2f4ddd6a9
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E10` (`P-0529505a-68be-5308-a805-44a2f4ddd6a9`); requirements are intake awaiting current-source research and decomposition.

Description:
- Offer real personal-project power with categories/areas, projects, sections, optional goals and progress, simple stages (Inbox/Next/In Progress/Waiting/Done) and cross-project organization.

Requirements:
- Simple mode works without hierarchy; optional advanced fields and project board; move task between projects; goal linking and progress rules; useful stale/blocked/missing-next-action warnings.

Out of scope:
- Corporate user performance dashboards, employee monitoring, burnout scoring, sprint velocity, capacity forecasting and mandatory PM rituals.

Expected behavior:
- A casual user creates a Todo with one field; a power user creates a project with tasks/deadlines/labels and reviews progress from a phone.

### E11 Habits and Dailies as first-class recurring personal workflows

```yaml
id: P-f2bb7e37-6700-58ac-aa56-b537d193c777
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E11` (`P-f2bb7e37-6700-58ac-aa56-b537d193c777`); requirements are intake awaiting current-source research and decomposition.

Description:
- Make habitual and scheduled work ergonomic in Companion while respecting distinct Habitica Habit/Daily semantics and Cron day boundaries.

Requirements:
- Recurrence patterns, per-occurrence completion history, skip/pause/restart, streaks where determinable, habit up/down, Daily schedule with Habitica due-state reconciliation, calendar/Today integration, optional dedicated habit dashboard.

Edge cases:
- Day-start offset, missed Cron, timezone travel, duplicate reward count, skipped occurrence, delayed sync and task type conversion.

Expected behavior:
- Recurring work remains consistent across date changes and offline reconnect; reward projections never assume scores confirmed before Habitica responds.

### E12 Powerful search, saved views, reviews and task hygiene

```yaml
id: P-0018dcbc-f179-5b27-b0a8-8d57cf2fcb4d
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E12` (`P-0018dcbc-f179-5b27-b0a8-8d57cf2fcb4d`); requirements are intake awaiting current-source research and decomposition.

Description:
- Add cross-project filters, saved smart lists, stale/unreviewed tasks, unblocked next actions, and a gentle review flow.

Requirements:
- Search text, label, project, category, state, planned date, deadline, priority, game sync state; per-type staleness thresholds and exclude waiting/someday/recurring as appropriate; configurable reminders without punitive wording.

Expected behavior:
- Users can find stalled or forgotten tasks without mandatory status management; backlog review works in a few mobile gestures.

### E13 MCP and Skills for Companion-native project/task/goal operations

```yaml
id: P-5bb5cb9e-47d4-51dc-bf9b-0086175ed156
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E13` (`P-5bb5cb9e-47d4-51dc-bf9b-0086175ed156`); requirements are intake awaiting current-source research and decomposition.

Description:
- Extend the planned OAuth/MCP integration with canonical Companion domain commands for categories, projects, goals, labels, advanced task fields, filters, planning, activity log and queue status.

Current context:
- The read-only bootstrap contract targets Habitica data. Companion-native project/goal operations remain planned; no OAuth/MCP plugin is implemented.

Requirements:
- Narrow tools such as `list_projects`, `create_project`, `list_categories`, `list_goals`, `create_companion_task`, `update_companion_task`, `list_upcoming`, `get_completed_history`, `get_sync_status`; schema-level validation, per-profile auth, confirmation for sensitive actions, no generic API proxy.

Dependencies:
- Canonical domain, workspace accessible to MCP, authoritative command API, sync/reconciliation and permission model must exist first.

Expected behavior:
- ChatGPT performs the same advanced task workflows as the manual app, sees pending-vs-confirmed game projection, and cannot bypass validation or access another user's workspace.

### E14 Mobile UX validation, observability, migration and gradual adoption

```yaml
id: P-29925105-1015-5ced-8844-d56fa47e33c9
priority: 100
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Source:
- Companion-first epic `E14` (`P-29925105-1015-5ced-8844-d56fa47e33c9`); requirements are intake awaiting current-source research and decomposition.

Description:
- Progressive rollout with feature flags, backward-compatible migration, optional adoption of imported tasks, qualitative testing with consenting users, and accessibility/performance gates.

Requirements:
- Real iOS Safari/PWA/Android tests, offline/network-fault drills, migration backup/restore, screen-reader and large-text support, honest sync progress/diagnostics, no unconsented behavior tracking.

Expected behavior:
- Legacy users retain existing data and existing gameplay workflows; users can switch advanced features off without corrupting records; recovery after interrupted migration is validated.

## Prioritized Next Changes

Highest numeric priority first; unmet dependencies and blocking questions stop implementation. Imported source provenance and product decisions remain distinct from newly executed research.

### Correct Habitica `x-client` Identity

```yaml
id: T-09f1fa5d-fa07-592d-9a2d-7ef12705cf5e
priority: 200
status: Blocked pending answers
parent_pending_ids: ["P-0dbbdf75-02e8-5f09-9671-3c24537845f3"]
processing_run_id: null
root_task_id: T-09f1fa5d-fa07-592d-9a2d-7ef12705cf5e
related_task_id: null
relation: main
depends_on: []
```

Migration source:
- Imported numbered contract 0 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Origin:
- Integration-plan review.
- Existing repository/API contract mismatch.

Evidence:
- Habitica's API Usage Guidelines require the tool creator's User ID in `x-client`.
- `skill-tree/documents/habitica-api.md` documents the same rule.
- Current `HabiticaApiClient.CreateRequest()` falls back to the authenticated end user's ID when no configured header exists.

Goal:
- Make `x-client` stable and project-owned before adding any new Habitica execution surface.

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
- `skill-tree/documents/habitica-api.md`
- `skill-tree/documents/technical.md`
- `skill-tree/documents/deploy-cloudflare-pages.md`
- `tests/Habitica.Api.Tests/HabiticaApiClientTests.cs`

Discovery allowance:
- Add validation for missing production configuration.
- Add a development-only configuration path.
- Do not hardcode an unknown creator User ID.

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
- Allow local development to provide the header through local configuration.
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
- `skill-tree/documents/habitica-api.md`
- `skill-tree/documents/technical.md`
- `skill-tree/documents/deploy-cloudflare-pages.md`

Risks:
- Existing deployments with empty configuration will fail until configured.
- Missing configuration remains an explicit release blocker.

### Define ChatGPT Integration Architecture, Privacy, And Retention Contract

```yaml
id: T-c6f6b09d-4625-5898-b91c-016c611b60df
priority: 199
status: Ready
parent_pending_ids: ["P-ebb08836-4b75-5fb9-bf2f-6aed5789b9e8"]
processing_run_id: null
root_task_id: T-c6f6b09d-4625-5898-b91c-016c611b60df
related_task_id: null
relation: main
depends_on: []
```

Migration source:
- Imported numbered contract 1 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Origin:
- Owner request for multi-user ChatGPT integration.
- Security-sensitive architecture change.

Goal:
- Document the new optional trust boundary before implementation.

Current behavior:
- Habitica credentials remain on-device.
- private sync is browser-encrypted.
- party sync never receives the API token.
- privacy UI says credentials stay in the browser and are sent only to Habitica.

Desired behavior:
- Existing local-only behavior remains default.
- A proposed ChatGPT enrollment contract describes explicit consent for encrypted server-side credential storage.
- Planning text distinguishes current browser-only use from the proposed integration.
- Retention/deletion defaults are documented as proposals awaiting approval before implementation.

Touch:
- `skill-tree/documents/rules.md`
- `skill-tree/documents/technical.md`
- `skill-tree/documents/features.md`
- `skill-tree/documents/habitica-api.md`
- `skill-tree/documents/deploy-cloudflare-pages.md`
- `skill-tree/documents/ux-ui-manifest.md`
- `skill-tree/documents/future.md`
- `skill-tree/documents/companion-first-mcp-research.md`

Discovery allowance:
- Current OpenAI MCP/Plugin auth documentation.
- current MCP authorization specification.
- current Cloudflare Workers OAuth Provider documentation.
- current Habitica API Usage Guidelines.

Out of scope:
- Worker code.
- OAuth code.
- credential persistence.
- Runtime privacy UI changes; update `src/Habitica.WebApp/Pages/PrivacyPage.razor` with enrollment implementation, when the behavior exists.

Implementation constraints:
- Explicit opt-in.
- No silent migration of current users.
- No reuse of personal-sync or party-sync storage for integration secrets.
- Write a concrete proposed deletion contract and identify metadata retained after revocation, if any.
- Current-state documents retain current browser-only guarantees and link to the proposal. Completing documentation does not approve a new backend or credential-storage boundary.

Proposed retention contract:
- active encrypted credential envelope: until disconnect/delete;
- OAuth grants/tokens: until revocation/expiry;
- one-time linking tickets: <= 10 minutes;
- operational audit metadata: short bounded retention, e.g. 30 days;
- raw prompts/conversation text: not stored by Companion.

Questions and required clarifications:
- [Resolved - imported planning constraint] The project-owned `x-client` identity must be decided before server-side Habitica requests ship; architecture documentation can proceed without live requests.
- [Recommended default - confirmation optional] Operational audit retention?
  Recommendation:
  - 30 days.
- [Recommended default - confirmation optional] Keep anonymized aggregate metrics after deletion?
  Recommendation:
  - only non-user-identifying counters, if needed at all.

Validation:
- Check current privacy/security statements against `PrivacyPage.razor` and the browser credential/network paths.
- Verify future network behavior and retention choices are explicitly marked proposed, with the approval boundary recorded.

Acceptance:
- The proposal explains the consent, storage, revocation and deletion changes for opted-in users.
- Current behavior remains accurately documented; no planned storage is described as implemented.
- Proposed retention defaults and required owner approval are explicit.

Documentation updates:
- all touched docs.

Risks:
- privacy text drifting from implementation.
- accidental reuse of legacy sync concepts.

### Add Dedicated Auth And MCP Worker Projects

```yaml
id: T-b883e33b-05a6-53d8-a9a5-d11d7008887f
priority: 198
status: Blocked pending answers
parent_pending_ids: ["P-509dc30e-afbf-5eff-a2cc-cd9737e6236d"]
processing_run_id: null
root_task_id: T-b883e33b-05a6-53d8-a9a5-d11d7008887f
related_task_id: null
relation: main
depends_on: ["T-c6f6b09d-4625-5898-b91c-016c611b60df"]
```

Migration source:
- Imported numbered contract 2 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Origin:
- Imported proposal for a split Cloudflare OAuth/MCP architecture; package compatibility requires current official verification.
- Need to isolate authentication/credentials from MCP execution.

Goal:
- Create independently deployable Workers with explicit bindings and environments.

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
- MCP Worker has a narrow Auth Service Binding boundary; scaffolding denies protected requests until OAuth validation is implemented by the authorization task.

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

Touch:
- Proposed `workers/chatgpt-auth/**` and `workers/chatgpt-mcp/**`, each with deployment configuration.
- Worker package manifests, integration database migrations and focused tests.
- `skill-tree/documents/technical.md`, `skill-tree/documents/deploy-cloudflare-pages.md`.

Discovery allowance:
- Create isolated Worker directories and dedicated integration D1/OAuth KV bindings.
- Inspect current SDK APIs and Wrangler conventions before deciding specific filenames.

Out of scope:
- full OAuth authorization UI.
- MCP tools.

Implementation constraints:
- Verify current official APIs, select compatible versions and commit new Worker-local lockfiles; no existing Worker package pins are present. Candidate dependencies:
  - `@cloudflare/workers-oauth-provider`;
  - MCP TypeScript SDK / Cloudflare handler.
- `OAUTH_KV` is bound to the Auth Worker.
- MCP Worker has no direct OAuth KV binding.
- exact canonical MCP resource URI is configured once.
- preview and production resources are separate unless explicitly documented otherwise.

Questions and required clarifications:
- [Unresolved - blocks implementation] Has the owner approved the proposed backend, credential-storage and trust boundary produced by `T-c6f6b09d-4625-5898-b91c-016c611b60df`?
  Reason: `skill-tree/documents/rules.md` requires proposal and approval for changes to backend and credential-storage boundaries. Documentation completion alone is insufficient.
  Required answer from: repository owner after review of the architecture proposal.
- [Recommended default - confirmation optional] Separate Workers vs combined Worker?
  Recommendation:
  - separate Workers.
- [Recommended default - confirmation optional] Use root Wrangler config for both?
  Recommendation:
  - no; each Worker owns a dedicated deployment config.

Validation:
- Build each Worker bundle independently with the selected locked toolchain.
- Exercise the Service Binding health/stub contract in isolated local fixtures.
- Check distinct environment/resource bindings and verify protected stubs fail closed.
- OAuth metadata/resource-token enforcement belongs to the authorization task. Live preview smoke checks require explicit deployment scope and are not scaffolding acceptance.

Acceptance:
- Both Worker bundles build independently and have dedicated environment configurations.
- Local binding fixtures reach the Auth stub; protected operations are denied until authorization exists.
- No existing Pages binding is reused for integration state.

Documentation updates:
- `skill-tree/documents/technical.md`
- `skill-tree/documents/deploy-cloudflare-pages.md`
- `skill-tree/documents/repository-map.md`

Risks:
- preview/production binding mistakes.
- configuration drift between two Workers.

### Implement Stable Profile Identity, Credential Vault, And Connection Lifecycle

```yaml
id: T-e33bccdc-22f1-5043-9230-3d12d9e9563d
priority: 197
status: Ready
parent_pending_ids: ["P-55d076da-8a7f-50c7-af15-f4a106a697cb"]
processing_run_id: null
root_task_id: T-e33bccdc-22f1-5043-9230-3d12d9e9563d
related_task_id: null
relation: main
depends_on: ["T-b883e33b-05a6-53d8-a9a5-d11d7008887f", "T-09f1fa5d-fa07-592d-9a2d-7ef12705cf5e"]
```

Migration source:
- Imported numbered contract 3 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Goal:
- Create a stable multi-user integration identity and encrypted per-user Habitica credential vault.

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
- Proposed `workers/chatgpt-auth/src/**`, `workers/chatgpt-auth/migrations/**` and Worker-local credential/cryptography tests.
- `skill-tree/documents/technical.md`, `skill-tree/documents/deploy-cloudflare-pages.md`.

Discovery allowance:
- Add a dedicated integration database schema, credential envelope utilities and isolated cryptography tests.
- Preserve existing personal-sync KV and party-sync D1 security boundaries.

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
  - yes, but treat the display-name cache as optional user data and refresh opportunistically.
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
- `skill-tree/documents/technical.md`, `skill-tree/documents/rules.md` (current guarantees and approved integration boundary).
- `skill-tree/documents/deploy-cloudflare-pages.md`

Risks:
- misuse of master keys.
- decryptable server-side storage is intentionally a different trust model than current browser-only use.

### Implement One-Time Linking And OAuth 2.1 Authorization

```yaml
id: T-16a38176-f2fe-5f7d-a809-546f17357b49
priority: 196
status: Ready
parent_pending_ids: ["P-2db8e8bd-00de-53f2-823d-06dc23f53eeb"]
processing_run_id: null
root_task_id: T-16a38176-f2fe-5f7d-a809-546f17357b49
related_task_id: null
relation: main
depends_on: ["T-e33bccdc-22f1-5043-9230-3d12d9e9563d"]
```

Migration source:
- Imported numbered contract 4 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Goal:
- Allow a Companion user to link ChatGPT without creating email/password accounts.

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

Authorization requirements (verify candidate SDK symbols against the selected locked provider and current official specification before coding):
- `OAuthAuthorizationServer`;
- `OAUTH_KV`;
- exact resource registration;
- PKCE S256;
- protected-resource metadata;
- CIMD enabled when deployment supports CIMD;
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

Touch:
- Proposed `workers/chatgpt-auth/**` and integration D1 migrations.
- `src/Habitica.WebApp/Pages/SettingsPage.razor`, `src/Habitica.WebApp/Pages/PrivacyPage.razor`.
- New narrowly scoped linking client under `src/Habitica.WebApp/`.
- Focused OAuth and WebApp tests; `skill-tree/documents/features.md`, `skill-tree/documents/technical.md`, `skill-tree/documents/ux-ui-manifest.md`, deployment docs.

Discovery allowance:
- Implement a short-lived linking ticket store and small consent page; verify current OAuth SDK behavior before coding.

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

Questions and required clarifications:
- [Recommended default - confirmation optional] Should pairing be used instead of an account/password system?
  Recommendation: Use single-use, short-lived pairing tickets tied to validated local Habitica credentials.
  Rationale: Avoids a separate login system and cross-origin cookie assumptions.

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
- Two isolated profile fixtures can authorize the same MCP resource independently; real teammate enrollment is qualified during the scoped rollout task.
- OAuth access for A never resolves B's vault.
- no standalone Companion account/password system is required.

Documentation updates:
- `skill-tree/documents/features.md`
- `skill-tree/documents/technical.md`
- `skill-tree/documents/deploy-cloudflare-pages.md`
- `src/Habitica.WebApp/Pages/PrivacyPage.razor`
- `skill-tree/documents/ux-ui-manifest.md`

Risks:
- pairing code becomes account authentication and must be protected accordingly.
- consent UI may be confused with Habitica's own login unless wording is explicit.

### Implement Read-Only MCP Baseline

```yaml
id: T-53352a18-54d2-57b6-accc-91f8e6dfad13
priority: 195
status: Ready
parent_pending_ids: ["P-9cf8044d-e032-54f0-b10b-647e6bcfb067"]
processing_run_id: null
root_task_id: T-53352a18-54d2-57b6-accc-91f8e6dfad13
related_task_id: null
relation: main
depends_on: ["T-09f1fa5d-fa07-592d-9a2d-7ef12705cf5e", "T-16a38176-f2fe-5f7d-a809-546f17357b49"]
```

Migration source:
- Imported numbered contract 5 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Scope gate: The read-only baseline may ship a legacy Habitica read-only bootstrap (`get_profile`, `get_account_status` and clearly labeled Habitica task views) while Companion product epics are pending. Direct Habitica task lists must never imply support for Companion-only projects, goals, plans or completed history. Canonical Companion read tools are implemented after E02/E03/E04 and are part of E13.

Goal:
- Provide a standards-compliant, useful, read-only integration before any Habitica mutations.

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

Candidate profile output from the imported proposal; verify the current official target-client profile contract before implementation and update the schema if required:

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
- Return stable Habitica task IDs with explicit Habitica provenance. Never pass bootstrap IDs directly to Companion mutation tools; resolve the verified projection mapping first.

#### `get_task`

Prefer a verified direct Habitica endpoint if documented at implementation time.

Otherwise:
- reuse the fetched user-task set and resolve by stable ID.

Do not introduce an undocumented API assumption merely for efficiency.

Current behavior:
- No OAuth-protected MCP endpoint exists; the browser C# client reads Habitica directly.

Desired behavior:
- Authenticated semantic read tools with validated schemas. Habitica-only task reads are explicitly **legacy/bootstrap** until E02/E03/E04 define the canonical Companion workspace.

Touch:
- Proposed `workers/chatgpt-mcp/src/**`, a narrow TypeScript Habitica client and Worker-local read/auth fixtures/tests.
- `skill-tree/documents/features.md`
- `skill-tree/documents/technical.md`
- `skill-tree/documents/habitica-api.md` only if newly verified contracts are added.

Discovery allowance:
- Add narrow TypeScript read models and fixtures without porting the whole C# application layer.
- Verify current official MCP transport, target-client profile schema and tool metadata; imported examples are candidates, not compatibility evidence.

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

Questions and required clarifications:
- [Recommended default - confirmation optional] Can legacy Habitica task reads ship before Companion-owned records?
  Recommendation: Yes, for read-only bootstrap only; label Habitica task provenance explicitly.
  Rationale: Connection/authentication can be tested without creating a parallel task authority.

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
  - `"What are my current stats?"`
  - "Which Dailies are still due?"
  - "Show unfinished To Dos."
- no read-only-baseline tool can mutate Habitica.

Documentation updates:
- `skill-tree/documents/features.md`
- `skill-tree/documents/technical.md`
- `skill-tree/documents/deploy-cloudflare-pages.md`

Risks:
- TypeScript/C# mapping drift.
- over-sharing data in tool results.

Mitigation:
- keep server schemas narrow;
- reuse neutral JSON fixtures for parity where useful.

### Add Safe Task Mutation Tools

```yaml
id: T-4a1de222-156a-52cf-8966-262a2c8216ed
priority: 194
status: Blocked pending answers
parent_pending_ids: ["P-90b811d4-3aea-509c-8dca-84d5c892a637"]
processing_run_id: null
root_task_id: T-4a1de222-156a-52cf-8966-262a2c8216ed
related_task_id: null
relation: main
depends_on: ["T-53352a18-54d2-57b6-accc-91f8e6dfad13"]
```

Pending scope gates:
- E02 `P-9d3fb820-05c3-525b-8769-85c5423d2f53`, E03 `P-4f954f5a-1084-5c87-8e26-1bfc5fd95035`, E04 `P-bbb755c5-37d7-5637-862d-17faa8e12f99`, E05 `P-4779d03b-eecc-5d86-a4d0-4dfba9ee9178`, and E07 `P-2dcaca70-8d94-55b1-aaee-7e7262e60f07` for unattended execution. Process the selected epic scope before recording promoted T- dependencies; never place P- IDs in `depends_on`.

Migration source:
- Imported numbered contract 6 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Integration gate: **Blocked pending E02/E03/E04/E05** (and E07 for unattended hosted execution). The legacy section below documents useful semantic tool contracts and validation, but **the target write implementation MUST call the Companion authoritative command API** (`create task / update / completion event / outbox`) rather than directly calling Habitica from MCP. MCP response must report `savedInCompanion` separately from `habiticaProjectionStatus`. Do not expose a direct-write parallel path even as a convenient MVP shortcut. Add native category/project/goal/label tools under E13; authoring native tool schemas is a high-level epic until domain definitions are approved.

Goal:
- Add the smallest useful Companion task-command surface with separate Habitica projection outcomes.

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
- stable Companion task ID required;
- partial updates only;
- whitelist fields;
- prefetch current task when type/state matters.

#### `complete_task`

Rules:
- stable Companion task ID;
- only supported completable task types;
- validate current Companion state before durable command acceptance; the projection executor verifies Habitica preconditions before any upstream effect;
- mark `destructiveHint=true` because Habitica scoring has game-state effects;
- refresh affected state after success.

#### `score_habit`

Rules:
- verify target is a Habit;
- explicit `up|down`;
- bounded count;
- recommended initial maximum: 10;
- persist bounded ordered scoring events through the shared command API;
- projection executor sends sequential requests and stops on the first failure or unknown outcome;
- report accepted event count separately from confirmed projection count and failure position;
- `destructiveHint=true`.

Idempotency/replay policy:

Do not assume MCP request ID is a durable idempotency key.

Before implementation:
- verify exact ChatGPT/MCP retry behavior available to the Worker;
- record a durable Companion command ID and correlation ID;
- reuse the command API deduplication contract instead of treating transport request IDs as command identities;
- never automatically retry a non-idempotent upstream call after an ambiguous network result.

For operations where a duplicate would be materially harmful, promote the action to a server-issued prepare/execute workflow.

Mutation result contract:
- operation;
- Companion task ID and durable command ID;
- task text where safe/useful;
- `savedInCompanion` after durable acceptance;
- `habiticaProjectionStatus`: pending, confirmed, `UnknownOutcome` or blocked;
- accepted event count separately from confirmed upstream sub-operation count;
- refresh success;
- updated stats where returned and useful;
- explicit ambiguous-state flag when the upstream result is uncertain.

Current behavior:
- Companion's current task mutations target Habitica; the canonical Companion command/outbox layer is not implemented.

Desired behavior:
- MCP writes persist Companion commands via the same authoritative command API used by the PWA, and separately report locally saved vs Habitica-projected outcomes.

Touch:
- Proposed `workers/chatgpt-mcp/**` and shared Companion command API (final paths depend on E02/E03 decomposition).
- `src/Habitica.Application/**`, `src/Habitica.Domain/**` as justified by the canonical domain contract.
- Targeted tests and `skill-tree/documents/features.md`, `skill-tree/documents/technical.md`, `skill-tree/documents/habitica-api.md` where relevant.

Discovery allowance:
- Research selected P- IDs for E02/E03/E04/E05 and resolve E07 for unattended execution before implementation; preserve produced T- IDs and record required dependency edges atomically, using task keys for same-batch dependencies.
- Complete required promoted tasks and update exact command symbols, persistence paths and field schemas before removing the blocking gate. E13 governs later native category/project/goal tools.

Out of scope:
- delete.
- bulk delete.
- purchases.
- spells.
- Cron.
- equipment.

Implementation constraints:
- `habitica.tasks.write`.
- step-up scope when the current grant lacks the required scope.
- write annotations accurate.
- server-side validation independent of Skills.
- no blind retry.
- no mutation based only on fuzzy task-name match when multiple candidates exist.

Failure handling:
- ambiguous fuzzy match -> return candidates; do not mutate.
- upstream timeout after request may have been sent -> return `outcomeUnknown`; do not replay automatically.
- durable save + projection or refresh failure -> preserve the command and report each stage separately.
- partial repeated scoring -> report accepted and confirmed counts separately; preserve unknown outcomes for reconciliation.

Questions and required clarifications:
- [Unresolved - blocks implementation] What is the authoritative Companion command API and trusted background executor boundary?
  Reason: Direct MCP-to-Habitica writes would bypass the Companion source of truth, command journal and reconciliation.
  Required answer from: E02/E03/E04/E05/E07 decomposition and implementation.

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
- Natural-language task management resolves Companion IDs and submits through the same authoritative command API as the PWA; no direct MCP-to-Habitica write path exists.
- Durable save, pending projection, confirmed projection and unknown outcomes remain distinguishable in tool results.
- every mutation outcome is auditable and unambiguous where the upstream response is known.
- ambiguous network outcomes are never replayed automatically.

Documentation updates:
- `skill-tree/documents/features.md`
- `skill-tree/documents/technical.md`
- `skill-tree/documents/habitica-api.md` if new verified endpoint details are added.

Risks:
- duplicate mutations.
- users not realizing task completion affects game state.
- fuzzy matching mistakes.

### Package The Plugin And Add Core Skills

```yaml
id: T-3987ba88-f69f-59f1-811e-f51cabfa5978
priority: 100
status: Ready
parent_pending_ids: ["P-1590c3c7-25e6-589e-8efb-91f35d873b4e"]
processing_run_id: null
root_task_id: T-3987ba88-f69f-59f1-811e-f51cabfa5978
related_task_id: null
relation: main
depends_on: ["T-4a1de222-156a-52cf-8966-262a2c8216ed"]
```

Migration source:
- Imported numbered contract 7 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Integration gate: Add Skills for project planning, goal review, upcoming/calendar, completed-history and sync-status analysis only after E13 exposes the matching authenticated Companion-native tools. Until then Skills must accurately describe the legacy Habitica-only bootstrap capabilities.

Goal:
- Package stable MCP capabilities with focused reusable workflows after the tool contract has proven reliable.

Candidate target layout (final paths depend on the selected distribution host and verified official manifest schema):

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

Alternative host-specific layout from the imported proposal, requiring verification:

```text
.codex-plugin/plugin.json
.mcp.json
```

Do not claim a universal portable manifest. Select the host-supported layout during research; keep only verified compatibility files.

Current behavior:
- Eight Skill Tree development skills are installed. No Habitica-facing MCP workflow/plugin package exists.

Desired behavior:
- A package for the selected distribution host with distinct Skills driving validated MCP workflows, using Companion-native contracts where available.

Touch:
- Proposed `plugin/**`; `README.md`; `skill-tree/documents/features.md`; plugin setup/validation docs; focused tests.

Discovery allowance:
- Use current official OpenAI plugin/Skills schemas and avoid unverifiable compatibility packages.

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

Questions and required clarifications:
- [Resolved - imported planning constraint] The initial integration target is ChatGPT. Verify the current official distribution format and remote-MCP support during package research; record any unsupported capability as a blocker before coding.
- [Recommended default - confirmation optional] Publish publicly immediately?
  Recommendation: No; validate private/team use first.
  Rationale: Auth and credential-handling integration should be proven before public distribution.

Validation:
- Validate plugin/Skill schemas, positive and negative triggers, denied scopes, account isolation and real MCP call routing.

Acceptance:
- Package schema and MCP routing pass isolated two-profile fixtures; real two-user enrollment is part of the scoped team rollout.
- no user copies a Habitica token into ChatGPT.
- skills consistently use MCP tools rather than inventing results.

Documentation updates:
- `README.md`
- `skill-tree/documents/features.md`
- plugin testing/setup docs.

Risks:
- overlapping skill activation.
- Skills drifting into hidden business logic.

### Add Party And Spell Capabilities Incrementally

```yaml
id: T-1a84d40a-f7e3-50f9-9601-daf3b101c7ec
priority: 99
status: Blocked pending answers
parent_pending_ids: ["P-d0950eec-bfbf-561f-b192-dcf8dde67488"]
processing_run_id: null
root_task_id: T-1a84d40a-f7e3-50f9-9601-daf3b101c7ec
related_task_id: null
relation: main
depends_on: ["T-4a1de222-156a-52cf-8966-262a2c8216ed"]
```

Migration source:
- Imported numbered contract 8 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Integration gate: The eventual unified scheduler/outbox from E05/E07 owns command lifecycle for server-managed gameplay mutations. Existing party-sync `local-claim-v1` is not proof of identity and must not grant new MCP party writes. Require authenticated party authorization and role checks from trusted evidence before exposing queue/vote operations.

Goal:
- Expand beyond task management without copying the existing C# application layer wholesale into TypeScript.

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

Current behavior:
- Party, quest and spell flows are implemented in the browser C# application and existing trust-based party-sync functions.

Desired behavior:
- Advanced MCP operations use narrow shared authorization and command contracts without copying C# workflows or treating local party claims as trusted remote identity.

Touch:
- Proposed `workers/chatgpt-mcp/**`, targeted Companion domain/application contracts, relevant party-sync code only after authenticated design review.
- Direct tests and `skill-tree/documents/features.md`, `skill-tree/documents/technical.md`, `skill-tree/documents/habitica-api.md`; party documentation as needed.

Discovery allowance:
- Add test fixtures and verified internal reads; do not silently change party-sync authorization.

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

Questions and required clarifications:
- [Unresolved - blocks implementation] What proof authorizes party writes initiated through MCP?
  Reason: Existing party local claims do not prove a remote user has the required permissions.
  Required answer from: Party authorization research and E05 shared command layer.

Validation:
- Verify party role checks, cross-profile isolation, stale state, spell plans, sequential execution, partial failure and parity fixtures.

Acceptance:
- party and spell tools preserve existing Companion safety assumptions.
- repeated mutations stop on failure.
- stale/changed state prevents execution.

Documentation updates:
- `skill-tree/documents/features.md`
- `skill-tree/documents/technical.md`
- `skill-tree/documents/habitica-api.md`.
- party docs as relevant.

Risks:
- C#/TypeScript logic drift.
- tool surface growing too broad.

### Add Start New Day Only As A Dedicated High-Risk Feature

```yaml
id: T-b92db026-a8cf-5922-8556-666cbea947ab
priority: 98
status: Blocked pending answers
parent_pending_ids: ["P-46dde168-2e75-53fd-b2c0-0c1769f7711d"]
processing_run_id: null
root_task_id: T-b92db026-a8cf-5922-8556-666cbea947ab
related_task_id: null
relation: main
depends_on: ["T-09f1fa5d-fa07-592d-9a2d-7ef12705cf5e", "T-c6f6b09d-4625-5898-b91c-016c611b60df", "T-b883e33b-05a6-53d8-a9a5-d11d7008887f", "T-e33bccdc-22f1-5043-9230-3d12d9e9563d", "T-16a38176-f2fe-5f7d-a809-546f17357b49", "T-53352a18-54d2-57b6-accc-91f8e6dfad13", "T-4a1de222-156a-52cf-8966-262a2c8216ed", "T-3987ba88-f69f-59f1-811e-f51cabfa5978", "T-1a84d40a-f7e3-50f9-9601-daf3b101c7ec"]
```

Migration source:
- Imported numbered contract 9 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Integration gate: The prepared Cron operation enters the same durable command ledger. A timeout must transition to `UnknownOutcome` and reconcile with current Habitica `needsCron`/Cron state; never automatically send `POST /cron` again merely because the first response is missing.

Goal:
- Expose Cron only after the integration has proven safe under real multi-user use.

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

Current behavior:
- The PWA implements an explicitly confirmed Start New Day flow; no remote Cron executor exists.

Desired behavior:
- Two-phase Cron through a durable command journal, with unknown-outcome resolution and no automatic replay.

Touch:
- Proposed `workers/chatgpt-mcp/**`, shared command/safety executor from E05.
- `skill-tree/documents/cron.md`, `skill-tree/documents/features.md`, `skill-tree/documents/technical.md`, `skill-tree/documents/habitica-api.md`, focused Worker/Cron tests.

Discovery allowance:
- Review live Cron rules and tests and add prepare/execute only after the durable command contract is defined.

Out of scope:
- scheduled Cron.
- automatic Cron because `needsCron=true`.
- chaining Cron + spell without a separately prepared plan.

Implementation constraints:
- Require scope `habitica.cron.execute`, explicit intent, short-lived single-use prepared action and fresh precondition verification.
- Treat timeout/unknown result as potentially successful; never blindly rerun Cron.
- Preserve Habitica day boundary, buffs and quest-risk handling from `skill-tree/documents/cron.md`.

Questions and required clarifications:
- [Unresolved - blocks implementation] How will an ambiguous Cron result be reconciled safely?
  Reason: A second Cron call may have irreversible gameplay effects.
  Required answer from: E05 and current Habitica server/API verification.

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
- `skill-tree/documents/cron.md`
- `skill-tree/documents/features.md`
- `skill-tree/documents/technical.md`
- skill docs if added.

Risks:
- irreversible gameplay impact.
- double Cron if replay handling is wrong.
- stale client assumptions.

### Security Hardening, Revocation, Key Rotation, And Team Rollout

```yaml
id: T-96866c8f-9554-55d8-a638-6d8b5afaf21c
priority: 97
status: Blocked pending answers
parent_pending_ids: ["P-0ea957c0-5210-5931-b545-82b9f13197db"]
processing_run_id: null
root_task_id: T-96866c8f-9554-55d8-a638-6d8b5afaf21c
related_task_id: null
relation: main
depends_on: ["T-3987ba88-f69f-59f1-811e-f51cabfa5978"]
```

Migration source:
- Imported numbered contract 10 from PR #1; originating Pending identity records imported intent, not a new Process Pending result.

Integration gate: Add owned Companion workspace export/deletion, migration and browser-storage recovery; conflict-state review; multi-device version tests; mobile accessibility/performance; queue aging and stalled-command observability; and hosted-executor revocation behavior. Confirm the listed release requirements via E14 before broader release.

Goal:
- Make the integration operationally safe before broad availability.

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
- old key cannot be removed while active rows reference the old key.
- profile-ID key rotation is a separate migration problem and should not be casually rotated because the profile-ID key defines stable OpenAI identities.

Rate-limit hardening:
- auth endpoint limits;
- pairing attempt limits;
- MCP per-profile limits;
- global protection;
- upstream 429 monitoring;
- no retry storm.

Habitica-specific operational monitoring:
- detect unusual 429 rates across different users.
- inspect evidence of shared Worker egress contributing to IP-level throttling.
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

Current behavior:
- The existing application has no ChatGPT-specific grant revocation, credential deletion lifecycle, security dashboard, or key rotation workflow.

Desired behavior:
- Team-ready multi-user integration with auditable revocation, vault deletion, key rotation, protected deployment environments and controlled release gates.

Touch:
- Proposed `workers/chatgpt-auth/**`, `workers/chatgpt-mcp/**`, integration migrations.
- `src/Habitica.WebApp/Pages/SettingsPage.razor`, `src/Habitica.WebApp/Pages/PrivacyPage.razor`.
- Direct tests, `skill-tree/documents/features.md`, `skill-tree/documents/technical.md`, `skill-tree/documents/rules.md`, UX and deployment documentation.

Discovery allowance:
- Add narrowly scoped security tooling and metrics without changing unrelated Pages party sync.

Out of scope:
- Public plugin listing, background automation, full conversation logging and general-purpose usage analytics.

Implementation constraints:
- Grant revocation and credential deletion are separate operations.
- Secrets never enter logs or error bodies; preview/production resources are isolated.
- Credential encryption-key rotation and stable profile-ID key rotation require separate procedures.

Questions and required clarifications:
- [Unresolved - blocks implementation] Define the selected rollout capabilities, then record every included capability as a required T- dependency before hardening and rollout validation. E14 adoption and migration gates remain mandatory.
- [Recommended default - confirmation optional] Which rollout group is appropriate?
  Recommendation: Owner first, one trusted teammate second, then an expanded opt-in group.
  Rationale: Verify revocation, privacy, isolation and rate-limit behavior before expanding.

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
- In an explicitly authorized rollout environment, verify two consenting team accounts can enroll independently, use the packaged tools and revoke access without cross-profile effects.

Acceptance:
- a user can fully remove server-side Habitica credentials.
- revocation becomes effective immediately for new tool calls.
- Two consenting team accounts complete the scoped enrollment/tool/revocation smoke flow with separate vault identities and no cross-profile access.
- no secret appears in production logs.
- non-opted-in Companion users remain local-only.

Documentation updates:
- `skill-tree/documents/features.md`
- `skill-tree/documents/technical.md`
- `skill-tree/documents/rules.md`
- `skill-tree/documents/deploy-cloudflare-pages.md`
- `src/Habitica.WebApp/Pages/PrivacyPage.razor`
- `skill-tree/documents/ux-ui-manifest.md`

Risks:
- operational misconfiguration.
- losing a key before migration.
- OAuth grants surviving credential deletion.
- upstream IP-level rate limiting after centralizing requests.

## Backlog

Deferred work requires an explicit promotion request before implementation.

### Party Quest Workspace Modes

```yaml
id: B-c8422883-6554-5a8a-a48f-12b1a6752515
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Goal: separate the Party page's current quest, shared queue planning, quest pool, recent completions, and member/CRON sections into clearer scan modes.

Touch:
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/PartyPageTests.cs`
- `skill-tree/documents/features.md`
- `skill-tree/documents/ux-ui-manifest.md` if shared navigation or mode guidance changes

Out of scope:
- changing party-sync data contracts;
- adding new quest analytics;
- changing Habitica party or quest links.

Acceptance:
- Party page provides a tab or segmented mode switch for current quest, planning, history, and members/CRON.
- Existing actions retain current authorization and stale-data guards.
- Empty and offline cached states remain visible in the relevant mode.
- Component tests cover mode switching and at least one guarded action still rendering in the assigned mode.

### Party Quest History Analytics

```yaml
id: B-4c8b1fe1-f37f-5b78-803c-419204fe0de1
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Goal: summarize stored shared quest completion history beyond the current recent-completion cards and queue penalty labels.

Touch:
- `src/Habitica.Domain/Party`
- `src/Habitica.WebApp/Pages/PartyPage.razor`
- `functions/api/party-sync/[partyId].js` only if the existing recent-completion payload is not enough
- direct tests under `tests/`
- `skill-tree/documents/features.md`

Out of scope:
- collecting Habitica data unavailable through current snapshots, chat completion signals, or shared queue records;
- optional vote budgets;
- changing quest queue ordering.

Acceptance:
- History view shows aggregate completions by quest and by owner from available shared history.
- Analytics clearly state the covered time window and when data is unavailable or sparse.
- Existing recent-completion removal permissions remain unchanged.
- Tests cover aggregate calculations and sparse/no-history rendering.

### Skill Macro Collection MVP

```yaml
id: B-e135d729-1688-53e4-95ed-913c41ef3495
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Goal: implement a local macro collection for predefined equipment and skill sequences.

Touch:
- proposed new `src/Habitica.Rules/Skills` area
- `src/Habitica.Application`
- `src/Habitica.WebApp`
- direct tests under `tests/`
- `skill-tree/documents/features.md`
- `skill-tree/documents/technical.md` if storage or execution architecture changes

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
- expected result estimate where current formulas support the estimate;
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
1. Equip gear maximizing perception.
2. Cast Tools of the Trade 3 times.
3. Equip gear maximizing strength.
4. Plan a finite Backstab count from available MP during dry-run; execute at most the confirmed count and stop on failure or changed preconditions.
5. Restore gear equipped before the macro started.
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
- reject plans exceeding available mana at dry-run time;
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
- confirm each skill endpoint and target semantic against `skill-tree/documents/habitica-api.md` and current Habitica API docs before execution support is enabled.

Acceptance:
- Users can create, edit, delete, and run local declarative macros using the initial step types listed in the macro contract.
- Dry-run preview shows planned equipment changes, selected targets, mana cost, expected requests, warnings, and stop conditions.
- Execution runs sequentially, persists progress, refreshes or updates local state after mutating steps, and stops on validation failures, API errors, stale state, or unexpected state changes.
- Macro steps can reference existing Equipment battle preset ids and dynamic gear strategies without copying transient recommendation data.
- Tests cover parsing/validation, missing gear, insufficient mana, stale data, deleted preset references, preset-first gear selection, task target resolution, restore-original-gear behavior, partial execution failure, stop-on-failure, and rate-limit response handling.

### Task Mutation Dry-Run Summaries

```yaml
id: B-5173c126-f1af-5dd6-a41b-c7542f135b2c
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Goal: add stronger pre-action summaries for existing task scoring/checkoff controls where local data can make the mutation clearer.

Touch:
- `src/Habitica.Api`
- `src/Habitica.Application`
- `src/Habitica.WebApp/Pages/TasksPage.razor`
- direct tests under `tests/`
- `skill-tree/documents/habitica-api.md` if endpoint response assumptions are added or corrected
- `skill-tree/documents/features.md`

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

```yaml
id: B-855ea05e-e6ce-549a-b7cd-26d4c6cbec81
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Goal: require an explicit confirmation step before clearing local browser data from Settings.

Touch:
- `src/Habitica.WebApp/Pages/SettingsPage.razor`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/`
- `skill-tree/documents/features.md`

Out of scope:
- changing the data actually cleared by `ClearLocalDataAsync()`;
- adding new import/export behavior.

Acceptance:
- Clear Local Data opens or reveals a confirmation naming credentials, cached Habitica data, party history, diagnostics, and setup data.
- The destructive action does not call `ClearLocalDataAsync()` until the confirmation control is activated.
- Cancel/close keeps local data untouched.
- Tests cover initial click, cancellation, and confirmed clearing.

### Returning User Copy Compression

```yaml
id: B-6cd10c0f-7350-5677-a81d-1de0f696e421
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Goal: reduce repeated hero/help copy for authenticated returning users while keeping first-run empty states understandable.

Touch:
- `src/Habitica.WebApp/Pages`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct page tests under `tests/Habitica.WebApp.Tests/Pages/`
- `skill-tree/documents/ux-ui-manifest.md` if shared copy rules change

Out of scope:
- redesigning navigation;
- removing first-run or unauthenticated guidance;
- changing data loading behavior.

Acceptance:
- Returning authenticated users see denser top sections on Dashboard, Tasks, Party, Equipment, and Spells.
- First-run, signed-out, stale-data, and empty-cache states still explain the next action.
- Tests cover at least one authenticated returning state and one unauthenticated/empty state.

### Mobile Stat Table Context

```yaml
id: B-e1e16021-1a1a-5af4-b97a-2ec2d43edfaf
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Goal: keep row labels visible or repeated when wide stat tables scroll horizontally on small screens.

Touch:
- `src/Habitica.WebApp/wwwroot/css/app.css`
- pages using `.stats-table`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/` if markup changes
- `skill-tree/documents/ux-ui-manifest.md` if table guidance changes

Out of scope:
- replacing table data with cards across desktop;
- changing stat calculations.

Acceptance:
- Mobile-width stat tables keep row identity visible through a sticky first column or repeated row-label context.
- Horizontal scrolling remains available for dense table values.
- Desktop table layout remains unchanged except for harmless label-context support.

### Compact Spell Card Density Pass

```yaml
id: B-57737f45-83d5-5e6d-866e-28e80c882602
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Goal: make spell cards easier to scan after the current full-card layout, recommendations, and CRON warning flow have been exercised.

Touch:
- `src/Habitica.WebApp/Pages/SpellsPage.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- direct tests under `tests/Habitica.WebApp.Tests/Pages/SpellsPageTests.cs`
- `skill-tree/documents/features.md`

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

```yaml
id: B-105e6701-8fb7-5cd7-91ca-3be091b69533
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

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

```yaml
id: B-14bc7f41-23f9-5ce0-b99f-d89c0c909d06
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Potentially useful for:
- ambiguous task selection;
- spell target review;
- quest planning;
- Start New Day preparation review.

Do not add UI merely to duplicate normal model text.

### Background Automation

```yaml
id: B-9465b12f-4e30-50f2-b5f8-697d397f8129
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

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

### Align inherited documentation prose with current document rules

```yaml
id: B-5320ebab-2a0c-5b9d-9840-15d1f944e5f8
priority: 25
status: Backlog
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Evidence:
- The installed `validate-document-rules.py` reports inherited explicit-entity and declarative-prose violations across relocated current-state documents.
- Plan documents and newly created baseline documents pass the scoped prose check; full-document strict validation remains incomplete.

Impact:
- Repository-wide strict documentation validation cannot pass until inherited wording receives review or upstream lint policy gains a supported legacy mode.

Suggested direction:
- Review affected wording in place without changing product constraints, quoted labels, formulas or API contracts. Resolve upstream false positives before broad mechanical rewriting.

Touch:
- Existing current-state documents under `skill-tree/documents/`; preserve plan scope and installed generated packages.

Acceptance:
- Current-state prose describes named entities and current behavior.
- Repository-wide strict documentation validation passes under an agreed rule set.
- API examples, product requirements and source links retain meaning.
