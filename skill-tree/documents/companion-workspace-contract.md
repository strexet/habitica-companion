# Companion Workspace Contract

Status: Owner-approved contract for local implementation and synthetic tests. Live activation requires separate scope.

Scope: Local implementation and synthetic tests for E02/E03/E04/E05/E07 dependencies of the prioritized task-mutation milestone. Real enrollment, data upload, hosting activation and background execution remain separate actions.

## Source evidence

`TaskSnapshot` currently represents Habitica task identity and cached game-facing state. `IKeyValueStorage` and Dexie version 1 provide one key/value store without an atomic entity/event/outbox transaction. `AppSessionController.ScoreTaskAsync` directly awaits Habitica scoring and snapshot persistence. Existing encrypted private sync remains unreadable to the backend. The Auth vault and read-only MCP bootstrap establish account authorization but no canonical Companion workspace or durable command API.

The task-mutation contract requires the PWA and MCP to submit through the same authoritative Companion command API. The [Companion research](companion-first-mcp-research.md#companion-authority-vs-reliable-cloud-synchronization) identifies workspace privacy, model choices, deletion policy, retention and executor ownership as explicit owner decisions. The owner-selected defaults govern dependency decomposition.

## Approved decisions

| Decision | Selected default | Consequence |
| --- | --- | --- |
| Cloud audience | Separate explicit cloud-workspace opt-in, available independently of ChatGPT linking | Existing private device sync remains encrypted and separate; enabling ChatGPT alone does not upload existing Companion records. |
| Server visibility | Opted-in workspace stores tasks and notes, categories/projects/sections/labels/goals, occurrence/completion history, command results and projection mappings in server-readable storage | The backend can serve authorized MCP queries and execute accepted commands. Cloud disclosure lists the exact data categories; workspace storage cannot claim end-to-end opacity. |
| Storage boundary | A dedicated Companion Workspace Worker and workspace D1 own canonical cloud records, command journal and execution leases | Auth retains Habitica credentials and exposes only narrow authorized projection operations; MCP has no vault or database binding. |
| Local-only authority | Browser IndexedDB owns local-only records and a durable local journal processed while the app is open | Browser clearing/eviction can remove local-only data. No closed-browser delivery promise exists. |
| Cloud authority | Server acceptance atomically records canonical changes, events and outbox intent with revision checks and durable command IDs | Offline edits remain visibly pending until accepted; stale revisions become explicit conflicts instead of silent overwrite. |
| Categories | Flat optional category membership | Nested categories remain deferred. |
| Projects | One optional primary project per task; one optional section within the selected project | Labels support overlapping views without multiple project ownership. |
| Goals | Progress derives from completed linked eligible Todos divided by eligible linked Todos; archived and cancelled Todos are excluded; zero eligible Todos means unknown progress | Daily/Habit occurrence metrics and manually entered custom metrics remain separate later choices. |
| Task identity | Companion Todos, Dailies and Habits receive independent opaque IDs; Habitica projection is optional and has a separate mapping | Imported Habitica tasks require explicit adoption before Companion commands become authoritative. |
| External Habitica deletion | Block projection and request review for a verified external deletion | No automatic resurrection of intentionally deleted tasks. Explicit repair can recreate only an adopted Companion projection. |
| Completed history | Retain canonical completion events until explicit workspace/history deletion | Thirty-day operational audit retention does not silently delete user task history. Exports and deletion controls cover retained history. |
| Unknown outcomes | Continue durable Companion saves and reads; pause profile game-effect execution until reconciliation resolves the unknown outcome | No automatic replay of ambiguous scoring or creation; pending status remains visible. |
| Background executor | Separate per-profile opt-in; one persisted execution lease; bounded scheduled recovery from the D1 ledger | Browser and server cannot both own the same projection. Disabling the executor pauses new execution while preserving accepted records. |
| Habitica pacing | Persist quota/cooldown state and use at least thirty seconds between automated background calls | Accepted commands may remain pending; transport retries never create duplicate game effects. |
| Workspace disconnect/delete | Revoke cloud access and executor eligibility; explicit workspace deletion removes canonical cloud records and command history | Habitica records and local browser copies are separate. Already transmitted upstream effects cannot be undone by deleting the workspace. |

## Dependency decomposition

1. E02 defines versioned C# domain records, invariants and neutral JSON fixtures for the selected relationships and progress metric.
2. E03 defines atomic IndexedDB entity/event/outbox storage, compatible upgrade/export/restore, and an independently consented cloud workspace boundary.
3. E05 defines shared command validation, revision/conflict handling, command-ID deduplication, typed outcomes and persisted execution ownership.
4. E04 defines verified projection mapping, explicit adoption, sequential effects and unknown-outcome reconciliation.
5. E07 defines opted-in hosted execution and local-to-server ownership handoff with synthetic restart, duplicate-delivery, revocation and outage fixtures.
6. Task mutation tools integrate only after the required promoted dependencies establish the shared PWA/MCP command API.

Contract approval and task metadata do not establish implementation acceptance. Each promoted dependency requires source evidence, passing fixtures and recorded acceptance before completion.
