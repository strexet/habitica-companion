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

## Proposed local Cron continuation

Status: owner-approved on 2026-10-16 for local implementation and synthetic tests of `T-b92db026-a8cf-5922-8556-666cbea947ab`. Approval covers disabled local code and synthetic tests. Real multi-user qualification remains mandatory before exposing Cron in a live environment; no deployment or real Cron call is proposed here.

Current evidence: the inspected [Cron controller](https://github.com/HabitRPG/habitica/blob/develop/website/server/controllers/api-v3/cron.js) sends a successful empty response after `cronWrapper` completes. The [wrapper](https://github.com/HabitRPG/habitica/blob/develop/website/server/libs/cron.js) saves user/tasks and `lastCron` before processing quest progress. An exception can therefore follow the personal day transition. Source checked on 2026-10-16. The following reconciliation policy is an application design inferred from that ordering.

Adopted reconciliation: only the original durable successful response proves complete execution of the accepted request. Receipt lookup can recover a lost successful acknowledgement without another Cron call. A later `lastCron` advance or `needsCron=false` is reported as observed day progress, without attributing success to this request or proving complete quest processing. Missing/unchanged timestamps, missing receipts and partial progress retain `UnknownOutcome` and the shared gameplay fence. No automatic replay or automatic unfencing occurs. Unsupported cases require an explicit later reconciliation design rather than guessed failure.

Adopted workflow: read status, prepare the exact day transition, present the unfinished-Daily review and buff/quest warnings, obtain explicit approval, then consume a short-lived single-use token. Preparation records fresh user, timezone/day-start, `lastCron`, Cron requirement, complete relevant Daily/checklist state, resting/buff state and available quest facts. Missing information remains unknown; unavailable safety-critical verification prevents preparation. Execution rechecks the exact fingerprint/day, uses the authoritative Workspace command service and shared gameplay leases, saves one typed Cron dispatch before forwarding and never sends another POST for the accepted action. Changed Daily, quest, day or account state aborts. No spell chain or scheduled Cron exists.

Adopted qualification: independent `habitica.cron.execute` consent, cross-profile isolation, expiry/replay, concurrent task/spell ownership, changed day/Dailies/quest, known success with failed refresh, unknown server/network outcomes, retained fences after operational retention and lookup-only recovery. The app and Workers build after implementation. Local acceptance does not qualify the existing real multi-user activation gate.

## Proposed hardening and release scope

Status: owner-selected on 2026-10-16 for local implementation and synthetic tests of `T-96866c8f-9554-55d8-a638-6d8b5afaf21c`. The selected scope covers all locally implemented tools: profile/account reads, canonical tasks and projection, authenticated party planning, and reviewed Blessing casts. Cron remains disabled for live release until the separate gate passes. Live activation remains separate for every capability.

Current source evidence: vault disconnect/deletion already remove encrypted credentials and revoke new access; OAuth/linking cleanup and stable re-enrollment identity have synthetic coverage. Settings already exposes linking, disconnect and profile deletion. Workspace owns task/history export/deletion, planning contributor cleanup and separate spell-history export. Separate grant-only revocation, operator encryption-key migration, complete permission/usage status, aggregate operational limits and E14 release qualification remain distinct hardening work. Existing local fixtures do not prove real device behavior or two-user host enrollment.

After scope selection, Process Pending records required implementation dependencies for these concrete units:

1. Management controls: current permission/account/credential status, last use, grant-only revocation distinct from credential deletion, management-token rotation and safe credential revalidation. Revocation immediately denies new effects and invalidates unaccepted actions; accepted uncertain effects retain safety evidence.
2. Encryption-key operations: bounded resumable re-encryption under versioned keys, refusal to remove referenced keys, interruption/restart tests and credential-free summaries. Stable profile-ID key rotation remains a separate design. Real key migration requires an authorized operator environment.
3. Operational protection: bounded profile/network/global limits, persistent cooldowns, queue-age/stalled-command visibility and aggregate quota diagnostics. No conversation logging, credential logging, identifying telemetry or new background Habitica polling.
4. E14 local qualification: feature flags, legacy/browser data preservation, explicit adoption, backup/restore, interrupted storage/network flows, conflicts and multiple-session version cases. Responsive, large-text and accessible control checks have explicit evidence; synthetic/browser checks remain distinct from actual mobile-device qualification.
5. Release qualification: documented owner-first and one-consenting-teammate stages, separate preview/production resource checks, real iOS Safari/PWA and Android checks, and independent enrollment/tool/revocation with two consenting accounts in an explicitly authorized environment. ChatGPT and Codex desktop are required first-dev clients; each client requires independent OAuth, tool, plugin/skill and revocation evidence against the shared dev backend. Owner-first client checks precede the two-account release stage; Codex CLI provides supplementary diagnostics. The deployment guide and plugin client matrix own concrete checks. Production log inspection uses redaction and minimum retention. Missing devices, accounts, endpoints, host access or deployment authorization remain recorded release blockers.

Selection authorizes local implementation and synthetic tests for the chosen capabilities. Deployment, production secret changes, real enrollment, host installation, messages and real game effects remain separate. The hardening root cannot complete the real two-user acceptance criterion using synthetic accounts. Included E14 capability dependencies are captured before implementation; no unrelated Pending or Backlog work is promoted.

## Standing local contract direction

The owner directed automatic adoption of further contracts on 2026-10-16 within the existing Implement All snapshot and required dependencies. This direction covers local implementation and synthetic checks, including the approved conservative Cron policy and all implemented tools for hardening. Agents record concrete contracts and source evidence before implementation without repeated contract-approval prompts. Missing facts remain unresolved; automatic adoption does not invent external state or qualify unrun acceptance checks. Deployment, production secret changes, real enrollment, host installation, device qualification, messages and live Habitica effects remain separate.

## Adopted private Cron limits

The standing local contract direction covers the following source-driven limits. Auth reads full UserGet for authoritative readiness, the complete bounded active task set with server-evaluated previous-day Daily due-state, and the current party quest when applicable. Fixed-offset day boundaries match a neutral C#/Worker fixture; server travel/DST eligibility is not reimplemented. An unresolved timezone transition, unknown readiness, missing critical account/task data, more than two hundred relevant tasks, more than one hundred unfinished due Dailies or an oversized/deep fingerprint denies preparation. Accounts without a party remain eligible when the account/day checks pass.

The private operation uses independent read and Cron-execution grants, a sixty-second shared gameplay lease and one persisted dispatch before a single POST. Every non-success or malformed response after dispatch remains unknown, including an error that might follow partial processing. Read-only original-receipt lookup and fresh observed day progress never authorize another POST or automatic unfencing. Only normalized minimal day/Daily/quest facts leave Auth; raw user/task/member data and credentials remain private. Both shipped Cron flags remain false. MCP execution passes through the reviewed Workspace acceptance and recovery ledger; Auth has no public Cron route.

Fresh reads are preconditions rather than a lock on Habitica. An independent Habitica client can race the final read; the integration does not claim atomic coordination with outside clients or exact damage/reward prediction. Real activation remains outside local qualification.

## Adopted reviewed Cron action boundary

Cron actions use a typed Workspace ledger separate from task and spell payloads, with an expiring two-minute preview, rotating token hash, single acceptance and persisted original dispatch identity. The shared Workspace runtime lease serializes task, spell and Cron effects. Accepted, dispatched and unknown Cron rows continue fencing all gameplay after Auth receipt retention. Recovery only looks up the original attempt; optional fresh progress observation cannot clear uncertainty. The foreground action has a sixty-second budget inside a ninety-second lease; refresh failure preserves original confirmed success.

Cron is an own-account day operation: ordinary Workspace enrollment and independent Cron/read grants apply; shared party planning consent is not required. Auth verifies any applicable current quest facts without creating or changing shared planning records. There is no new browser Cron control, scheduler or spell chain. Own Cron history exports only the explicit public receipt and preview projection; token hashes, raw credentials and private dispatch payloads remain excluded. Live Cron stays disabled in both Auth and Workspace in every environment.

## Adopted integration management boundary

Grant-only revocation rotates a durable OAuth epoch while retaining credential enrollment, stable profile identity and ordinary Workspace access. New tool/token exchanges require the current epoch; bounded provider cleanup is secondary to the immediate epoch barrier. Linking tickets and pending consent from the old epoch are invalid. Fresh grants invalidate unaccepted task, spell and Cron previews on Workspace entry; accepted commands and uncertain effects retain receipts and fences. Calls already dispatched cannot be undone. Existing background task execution remains a separate Workspace opt-in and requires the separate Workspace disconnect/disable control to pause.

Management status returns bounded account labels, active current-epoch permissions, latest tool authorization time, credential verification/repair status and cleanup completeness. Management-token rotation changes only the management credential; the browser replaces the token only for the same active tab generation. Lost rotation acknowledgement requires credential revalidation. Credential revalidation uses the existing explicit enrollment path, preserves stable profile identity and does not silently grant ChatGPT permissions. Status and management controls send no Habitica mutation. Tokens, grant identifiers and raw credential envelopes never enter status or user messages.

## Adopted credential key operations boundary

Credential encryption-key operations use a disabled private operator entrypoint with no public, MCP or Workspace binding. An explicit job identity selects the old version and the configured current target version. Bounded steps own an expiring lease, re-encrypt envelopes with optimistic generation/ciphertext checks and atomically record progress. A cursor remains private in D1; operator responses contain job identity, versions, counts and safe failure codes. Interrupted steps resume the same job. Unreadable envelopes block progress without exposing plaintext. Concurrent enrollment, disconnect or credential changes cannot be overwritten by a stale migration read.

Key inventory counts all retained envelopes by supported version. A current key, referenced key or key used by an unfinished job cannot be declared removable. The operator must stop old-version writers and repeat inventory immediately before any separately authorized key removal. The application cannot prevent an operator from changing external secret configuration outside the guarded workflow. Profile-ID key rotation remains excluded. Inactive job metadata expires after thirty days through bounded cleanup without deleting credentials. Local synthetic tests qualify the helper and private boundary; real migration and secret changes require an authorized operator environment.

## Verified connection replacement and receipt access

Workspace resets observed OAuth authority only when management authentication proves a different connection identity. Same-connection revocation epochs remain monotonic. Canonical data and accepted history survive reconnection, while unaccepted old previews remain unusable. A spell/Cron lookup transport or authorization failure is an unavailable original receipt, never an original known refusal. Only a successful original-receipt lookup can establish applied/refused completion. Inaccessible receipts keep uncertainty and shared gameplay fences without replay.

## Adopted operational protection boundary

Durable fixed-window quotas cover Auth public/enrollment ingress, MCP and Workspace ingress and verified profiles. Sixty-second limits are public/MCP/Workspace global 600 and network 120; enrollment global 30, network 10 and verified profile 5; management profile 30 with a separate disconnect/delete pool of 20; MCP profile 60; Workspace profile 120. Receipt/status recovery has a separate 20-request profile pool. Each upstream HTTP dispatch consumes global 300 and verified-profile 60 budgets before dispatch reservation. Limits use atomic D1 increments; expired counters are removed in batches of at most 100. Network buckets use a keyed digest, never raw addresses. Fixed anonymous daily counters describe local denials and actual upstream 429 responses. Credential repair and upstream Retry-After cooldowns remain authoritative. Local quota refusal includes a retry deadline; dispatched uncertain effects never acquire retry permission.

Own operational status aggregates accepted task/spell/Cron state within the active workspace. Counts distinguish pending, waiting over fifteen minutes, uncertain, blocked and expired execution leases. A manual Settings action reads the snapshot without Habitica polling or automatic execution. Account/session changes discard late results. Responses exclude task text, credential/command identifiers and raw payloads. Anonymous quota aggregates remain an operator-side helper; user status never exposes another profile or global usage.


## Owner-isolated Codex testing

Live testing is closed by owner direction. Implementation uses local builds, synthetic credentials and isolated test services. No Habitica requests, authenticated production connector calls, background activation, real-account browser checks or deployment qualification may resume until a new explicit owner request. ChatGPT validation also remains deferred. The bounded owner pilot permits destructive changes to the enrolled owner's tasks, Dailies and Habits, self-only spells and owned equipment only during a separately selected live round. Test records use explicit synthetic labels; ordinary personal records require exact verified identity before mutation.

Party tools remain read-only. Party departure is prohibited through Companion without a feature/profile override. Shared quest actions, other-user spell targets, party spells, party chat effects and live Cron are outside the test scope. Production party, projection, spell and background-executor flags remain disabled until the applicable safe lane is implemented and explicitly selected. Task scoring can record pending own-account quest progress; shared quest processing remains prohibited.

The browser owner-isolated profile disables shared-party sync, party gameplay, task-targeted spells and Start New Day. The profile permits only known self-spell identifiers without another target and owned equipment actions. The low-level API blocks party departure independently of the profile setting. Synthetic transport fixtures can qualify generic endpoint shapes with isolation disabled; the departure prohibition still applies.

Prepared commands retain exact reviewed previews, stable command identities and original receipt recovery. The owner's explicit live-test authorization covers bounded own-account test changes; previews and parameters remain visible before dispatch. Unknown results remain fenced and never authorize replacement intents or blind retries. Native Codex, browser behavior and synthetic checks remain distinct evidence lanes.

## Native lifecycle testing extension

The owner-approved Codex mutation scope includes native Daily/Habit creation, task field edits, dated Daily completion, explicit Habit scores and archival of exact unlinked task IDs. New Dailies remain unscheduled until recurrence editing is implemented. Creation and archival remain Companion-only; no unsupported upstream deletion or recurrence projection is implied. Archive operations retain command and activity history until separate workspace deletion. Party writes and live Cron remain excluded.

Owner-provided Habitica test credentials may persist only in the ignored root `.dev.vars.owner-test` file with owner-only file permissions. Normal builds, CI, exports, logs and deployment must not consume or include that file. Live tests are explicit, opt-in own-account checks; test evidence stores outcomes without credentials.

## Adaptive admission boundary

Local adaptive admission uses the existing foreground and background authorization boundaries. The feature flag defaults to false; local synthetic fixtures enable the flag explicitly. Shared and profile estimates use conservative header reconciliation, finite reservations and positive failure jitter. Admission rejection does not authorize action replay, clear unknown outcomes or remove credential repair. Own-profile ceilings preserve capacity for other profiles when a profile repeatedly receives admission denials. Independent browser devices remain outside server coordination.

## Local recurrence calendar contract

Canonical recurrence uses civil dates in the selected UTC or supported IANA region-style timezone. Daily intervals anchor to the start date; weekly intervals use Monday-anchored periods; monthly rules skip missing dates. Expansion has an inclusive maximum of 366 days and produces no persisted occurrences, task mutations or gameplay effects. Unknown timezone support fails explicitly. A calendar occurrence never proves Habitica eligibility, current game day or confirmed scoring. Remaining authoring and lifecycle controls require shared canonical command contracts.

## Reviewed recurrence authoring boundary

Local application commands and MCP share reviewed createDaily/updateTask authoring for unlinked Companion Dailies. Explicit schedule clearing retains history. Recurrence intents cannot request Habitica projection or edit a linked/pending/uncertain mapping. Scheduled dates remain civil calendar facts and require separate server due-state/Cron verification before any existing game projection. Default request serialization and legacy command identities remain stable; extended recurrence intents use a distinct versioned hash. The basic PWA editor remains unscheduled until the separate UI gate is satisfied.

## Companion-only occurrence lifecycle

Local skip, pause and resume commands require an unlinked Companion Daily/Habit and explicit reviewed identity. Skip applies only to an active Daily with a supported schedule and a pending occurrence date. Pause blocks new canonical completion/scoring; resume preserves the schedule without automatic backfill. Restart uses a separately reviewed schedule replacement and retains past history. Linked, pending and uncertain mappings are refused; no Habitica penalty, schedule, reward or party state is changed. Lifecycle receipts report accepted canonical records separately from zero game effects.

## Combined canonical query direction

The standing local contract direction selects AND filters over existing canonical task fields and bounded pages. Direct task category overrides inherited project category; an archived metadata assignment remains searchable until changed. Civil-date bounds are inclusive and require a stored date. Search includes titles and notes using NFKC/invariant lowercase with explicit U+0130 and final-sigma folding. Mapping state remains distinct from original command effect confirmation. Preserve document order, exclude archived tasks and include other states by default. Continuation requires the preceding revision and unchanged filters; changed revisions require restarting. Local implementation and synthetic checks supply no live activation or mobile UX acceptance.

## Civil agenda direction

Standing local contract adoption selects explicit all-day date ranges and one canonical task/date row with separate planned/deadline/occurrence meanings. Active Daily rules generate virtual pending dates; retained completed/skipped dates remain authoritative after pause or schedule replacement. Paused work generates no new occurrences, and unscheduled Dailies have no inferred schedule. Completed/cancelled/archived tasks remain outside the planning agenda. Current rule timezone is labelled without inventing historical timezone or game due proof. Pagination guards the document revision; invalid, unavailable or excessive ranges fail without partial results. No planning read authorizes writes or live activation.

## Native placement direction

Standing local contract adoption selects a separate planTask command for native planned date and exact category/project/section/label assignments. The command edits existing tasks only, preserves omitted metadata and task order, and requires explicit clearing or full label replacement. New references are active canonical entities; changing project cannot silently clear an incompatible section. Deadline, recurrence, urgency, text and game details remain outside the planning patch. Existing linked/pending/uncertain mappings and previous command fences remain untouched, and no game effect is enqueued. Organization CRUD, lifecycle/hierarchy authoring, production controls and live qualification remain separate work.
