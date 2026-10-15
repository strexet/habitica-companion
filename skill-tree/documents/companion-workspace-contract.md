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

## Authenticated party planning boundary

Status: Owner-approved on 2026-10-15 for local implementation and synthetic tests of `T-1a84d40a-f7e3-50f9-9601-daf3b101c7ec`. Real enrollment and deployment remain separate.

Verified repository evidence: `functions/api/party-sync/[partyId].js` accepts `local-claim-v1` headers containing user, party and leader IDs. Stored owner/officer/admin records then influence queue permissions. Queue insertion accepts the claimed owner, and voting toggles an existing vote. Forwarding authenticated MCP requests into the existing public endpoint would leave the same rows mutable through local claims. A toggled vote also reverses intent on an ordinary duplicate request.

Verified upstream evidence: Habitica's authenticated `GET /groups/:groupId` controller calls `Group.getGroup` without optional membership. The group model resolves `party` from the authenticated user's party, returns no unrelated private group and represents the leader as a user UUID. A successful credential-backed `GET /groups/party` supplies a current party/leader fact; a client-supplied ID or cached Companion officer role does not supply equivalent proof. These source facts establish a candidate identity check, not approval of a Companion permission model.

Selected default: a separate authenticated planning namespace in Workspace D1, inaccessible to legacy party-sync handlers. Auth verifies the enrolled actor and fresh party through stored credentials; an internal service binding returns only the required identity, current party, leader and quest-ownership facts. MCP arguments never choose actor or party. Independent party-read, planning-write and spell-execution consent scopes must be checked again inside the owning service. Missing verification, changed party, revoked enrollment or unavailable upstream reads deny access.

Selected permissions: verified current members read the authenticated planning queue and set only the actor's own vote to an explicit desired boolean. Members queue only an owned quest scroll verified from current Habitica inventory. The verified current leader may manage authenticated queue settings; browser-claimed officers/admins and legacy settings grant no authenticated privilege. The initial queue starts empty. No silent import of legacy queue entries, votes, owners, officer roles or invite proofs occurs. Legacy planning remains visibly separate until a separately scoped PWA integration is approved.

Selected lifecycle: queue and vote commands use durable IDs, revision checks and atomic receipts in the shared command service. Duplicate votes set the same desired state rather than toggle. Planning changes do not start, accept or purchase a Habitica quest. Spell work follows in a separate implementation unit: a narrow verified allowlist, short-lived server preview, single-use acceptance, fresh actor/target/precondition checks and the durable gameplay ledger. Unknown effects remain fenced without replay; no prepared-action token or credential enters portable exports. No arbitrary macros, background casts, extra member-profile enumeration or new real upstream mutation is enabled.

Selected data disclosure: opted-in party members can read the authenticated party planning records, including quest selections and contributor labels. Auth stores credentials under the existing consent boundary; Workspace stores only bounded shared planning data and command/audit records. Shared party planning needs an explicit PWA disclosure/opt-in before activation. Management authentication permits export or removal of the actor's own contributions even after departure; the freshly verified leader can delete the entire authenticated planning namespace. Observed departure blocks new party access and pending actions. Shared records remain until explicit removal; deletion revokes prepared actions and retains only non-identifying operational metadata for at most thirty days. Approval of local implementation/tests does not grant real enrollment, deployment or public distribution.

The owner approved the separate authenticated namespace, permissions and shared-data boundary. Required implementation units must preserve the contract. Cron and team rollout retain separate unresolved gates.

Sources checked on 2026-10-15: [Habitica group controller](https://github.com/HabitRPG/habitica/blob/develop/website/server/controllers/api-v3/groups.js), [Habitica group membership model](https://github.com/HabitRPG/habitica/blob/develop/website/server/models/group.js). The proposal is an application design inferred from current source; Habitica does not define Companion planning roles or policy.
