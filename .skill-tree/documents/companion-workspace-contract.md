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
| Task identity | Every Companion Todo, full subtask, Daily and Habit requires a separate verified Habitica task mapping | Account consent and execution availability remain prerequisites; missing correspondence is incomplete integration rather than a successful reward-free task workflow. Imported Habitica tasks require verified adoption. |
| External Habitica deletion | Block projection and request review for a verified external deletion | No automatic resurrection of intentionally deleted tasks. Explicit repair can recreate only an adopted Companion projection. |
| Completed history | Retain canonical completion events until explicit workspace/history deletion | Thirty-day operational audit retention does not silently delete user task history. Exports and deletion controls cover retained history. |
| Unknown outcomes | Continue durable Companion saves and reads; pause profile game-effect execution until reconciliation resolves the unknown outcome | No automatic replay of ambiguous scoring or creation; pending status remains visible. |
| Background executor | Separate per-profile opt-in; one persisted execution lease; bounded scheduled recovery from the D1 ledger | Browser and server cannot both own the same projection. Disabling the executor pauses new execution while preserving accepted records. |
| Habitica pacing | Persist quota/cooldown state and use at least thirty seconds between automated background calls | Accepted commands may remain pending; transport retries never create duplicate game effects. |
| Workspace disconnect/delete | Revoke cloud access and executor eligibility; explicit workspace deletion removes canonical cloud records and command history | Habitica records and local browser copies are separate. Already transmitted upstream effects cannot be undone by deleting the workspace. |

## Required Habitica correspondence

The product invariant applies to browser and agent workflows, conversion, existing records and retained history. Companion owns canonical identity, planning and command history; Habitica owns game effects and rewards.

| Companion entity | Required Habitica representation |
| --- | --- |
| Todo and each full Todo subtask | Independent Habitica Todo; flattening the hierarchy is acceptable. A parent mapping never covers children. |
| Daily | Habitica Daily with a supported corresponding schedule. Unsupported schedules require an explicit integration limitation. |
| Habit | Habitica Habit with supported scoring directions. |
| Project, category, section, label and goal | Native Habitica tag with a stable account-bound mapping; related tasks carry the corresponding tags. Metadata never creates an extra reward-bearing Todo. |
| Saved task view | Native tag association for the saved selection; dynamic membership and retained-view lifecycle require the tag-coverage task contract. |
| Inline checklist item | Checklist field of the mapped task. Conversion to a full subtask requires an independent task mapping. |
| Completed or archived task | Retained task identity and confirmed command history. Completion or archive must not silently discard correspondence. |
| Statistics, completion events and activity records | Derived data or retained history; no additional reward-bearing task. |

Local capture without cloud consent or available execution remains visibly incomplete/pending. No implicit enrollment, credential upload, executor activation or reward claim follows from local capture. Existing account consent remains mandatory. Current ordinary task defaults and recurring creation do not satisfy the invariant; P0 tasks in future.md own remediation.

Stable mappings, rather than matching names, establish task and tag identity. Tag changes must preserve unrelated user tags. A stored identity never proves a pending score succeeded: original command receipts establish confirmed effects. Unknown outcomes block replay. Historical completion imports and mapping repair never backfill rewards automatically. Conversion must verify replacement mappings and content/history before supported retirement of the exact legacy container. Separately approved source retirement retains the complete obsolete container as immutable audit history while removing the native source; only verified retired containers are excluded from current counterpart and desired tag membership gaps. Every full replacement Todo and ordinary completed/archived task retains the native correspondence requirement.

Companion urgency uses Low, Normal, High and Urgent. Habitica game difficulty is separate; changing urgency must not silently change game difficulty or awards.

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

Live gameplay testing remains closed until an explicit new test round. The owner authorizes the production frontend permission correction and public settings/asset verification; the authorization does not select real Habitica mutations, authenticated connector checks, background activation or ChatGPT qualification. During a separately selected agent test round, supported class spells may use self, task, all-task and party effects under ordinary game eligibility. Bounded owner task/equipment checks remain available within the selected test scope. Test records use explicit synthetic labels; ordinary personal records require exact verified identity before mutation.

Party management tools remain read-only during agent tests. Party departure is prohibited through Companion in every profile, including the production app. Shared quest management, party departure and live Cron remain outside the selected agent test scope. Supported spells are permitted independently of the party-management restriction. Optional backend party, projection, spell and background-executor activation retains separate release gates; normal browser user controls do not inherit disabled agent-test settings.

The browser owner-isolated profile is explicit opt-in. Normal source/client defaults and production frontend settings keep OwnerIsolatedTestingEnabled false and PartySyncEnabled true. An explicit agent profile restricts shared-party sync, party management and Start New Day, while supported spells and owned equipment retain ordinary eligibility checks. No self-spell whitelist or target restriction is added by test mode. The low-level API rejects party departure independently of the profile setting.

Prepared commands retain exact reviewed previews, stable command identities and original receipt recovery. The owner's explicit live-test authorization covers bounded own-account test changes; previews and parameters remain visible before dispatch. Unknown results remain fenced and never authorize replacement intents or blind retries. Native Codex, browser behavior and synthetic checks remain distinct evidence lanes.

## Native lifecycle testing extension

The owner-approved Codex mutation scope includes native Daily/Habit creation, task field edits, dated Daily completion, explicit Habit scores and archival of exact unlinked task IDs. New Dailies require an explicit supported schedule and typed Habitica correspondence. Archival remains Companion-only; Habit creation uses the typed Habit correspondence contract. No unsupported upstream deletion or recurrence projection is implied. Archive operations retain command and activity history until separate workspace deletion. Party writes and live Cron remain excluded.

Owner-provided Habitica test credentials may persist only in the ignored root `.dev.vars.owner-test` file with owner-only file permissions. Normal builds, CI, exports, logs and deployment must not consume or include that file. Live tests are explicit, opt-in own-account checks; test evidence stores outcomes without credentials.

## Adaptive admission boundary

Local adaptive admission uses the existing foreground and background authorization boundaries. The feature flag defaults to false; local synthetic fixtures enable the flag explicitly. Shared and profile estimates use conservative header reconciliation, finite reservations and positive failure jitter. Admission rejection does not authorize action replay, clear unknown outcomes or remove credential repair. Own-profile ceilings preserve capacity for other profiles when a profile repeatedly receives admission denials. Independent browser devices remain outside server coordination.

## Local recurrence calendar contract

Canonical recurrence uses civil dates in the selected UTC or supported IANA region-style timezone. Daily intervals anchor to the start date; weekly intervals use Monday-anchored periods; monthly rules skip missing dates. Expansion has an inclusive maximum of 366 days and produces no persisted occurrences, task mutations or gameplay effects. Unknown timezone support fails explicitly. A calendar occurrence never proves Habitica eligibility, current game day or confirmed scoring. Remaining authoring and lifecycle controls require shared canonical command contracts.

## Reviewed recurrence authoring boundary

Local application commands and MCP share reviewed createDaily/updateTask authoring for unlinked Companion Dailies. Explicit schedule clearing retains history. Recurrence edits cannot request Habitica projection or edit a linked/pending/uncertain mapping; new creation follows the supported Daily correspondence contract. Scheduled dates remain civil calendar facts and require separate server due-state/Cron verification before any existing game projection. Default request serialization and legacy command identities remain stable; extended recurrence intents use a distinct versioned hash. The PWA creation editor exposes the complete supported schedule before review.

## Companion-only occurrence lifecycle

Local skip, pause and resume commands require an unlinked Companion Daily/Habit and explicit reviewed identity. Skip applies only to an active Daily with a supported schedule and a pending occurrence date. Pause blocks new canonical completion/scoring; resume preserves the schedule without automatic backfill. Restart uses a separately reviewed schedule replacement and retains past history. Linked, pending and uncertain mappings are refused; no Habitica penalty, schedule, reward or party state is changed. Lifecycle receipts report accepted canonical records separately from zero game effects.

## Combined canonical query direction

The standing local contract direction selects AND filters over existing canonical task fields and bounded pages. Direct task category overrides inherited project category; an archived metadata assignment remains searchable until changed. Civil-date bounds are inclusive and require a stored date. Search includes titles and notes using NFKC/invariant lowercase with explicit U+0130 and final-sigma folding. Mapping state remains distinct from original command effect confirmation. Preserve document order, exclude archived tasks and include other states by default. Continuation requires the preceding revision and unchanged filters; changed revisions require restarting. Local implementation and synthetic checks supply no live activation or mobile UX acceptance.

## Civil agenda direction

Standing local contract adoption selects explicit all-day date ranges and one canonical task/date row with separate planned/deadline/occurrence meanings. Active Daily rules generate virtual pending dates; retained completed/skipped dates remain authoritative after pause or schedule replacement. Paused work generates no new occurrences, and unscheduled Dailies have no inferred schedule. Completed/cancelled/archived tasks remain outside the planning agenda. Current rule timezone is labelled without inventing historical timezone or game due proof. Pagination guards the document revision; invalid, unavailable or excessive ranges fail without partial results. No planning read authorizes writes or live activation.

## Native placement direction

Standing local contract adoption selects a separate planTask command for native planned date and exact category/project/section/label assignments. The command edits existing tasks only, preserves omitted metadata and task order, and requires explicit clearing or full label replacement. New references are active canonical entities; changing project cannot silently clear an incompatible section. Deadline, recurrence, urgency, text and game details remain outside the planning patch. Existing linked/pending/uncertain mappings and previous command fences remain untouched, and no game effect is enqueued. Organization CRUD, lifecycle/hierarchy authoring, production controls and live qualification remain separate work.

## Retained completion read direction

Standing local direction selects retained Todo/Daily completion reads independent of Habitica recent-completed coverage. Original event IDs, UTC completion timestamps, distinct occurrence civil dates and archive flags remain visible. Matching command snapshots supply stored details; missing snapshots explicitly fall back to current task details. Search uses the selected details, with exact task lookup for renamed work. UTC bounds retain seven fractional digits, and pages guard the canonical revision. Reads create no completion, restore or game action. Retained TaskRestoration records and reviewed restoreTodo commands support unlinked Todo cycles under the explicit restoration boundary below.

## Todo restoration-cycle data direction

Standing local direction selects retained TaskRestoration records rather than deletion of completion events. A restoration references one exact Todo completion with a distinct command identity and later causal revision. A new completion requires an intervening restoration; duplicate or overlapping cycles are invalid. Current state matches the latest cycle for tasks with restoration history. Archived events remain causal records. Revision order supplies causality even when clock observations move backward. Goal progress counts current Todo state rather than historical events.

The optional data extension preserves default serialization when unused and supplies explicit history metadata when present. Strict incompatible clients require coordinated updates. Data import retains history and existing paused-authority safeguards without reversing rewards. Reviewed unlinked Todo execution follows the exact boundary below; linked-game, archived-task and recurring recovery remain outside the initial execution contract.

## Exact unlinked Todo restoration direction

Standing local direction selects a reviewed restoreTodo operation for a completed nonarchived Todo with an exact canonical completion ID. Only patch.restoreTodo.completionId is accepted. The referenced completion must belong to the task and remain unrestored. Any retained game identity or nonunlinked mapping blocks restoration, including archived mappings. Acceptance appends one restoration and changes current state to active while retaining metadata, order and all completion cycles. No game effect, reward reversal or Daily/Habit undo occurs. Original command receipts remain recoverable after later cycles. Production controls, archived-task recovery and linked-game policies remain separate.

## Retained Habit activity direction

Standing local direction selects get_habit_history over retained canonical HabitScores. Every up/down event remains distinct, including same-command batches and archived task/event records. Optional exact task, direction, normalized details and inclusive UTC bounds combine with AND. Sorting uses precise UTC time, revision and stable event order. Continuation guards the canonical revision. Matching scoreHabit snapshots supply historical details; missing snapshots explicitly use current details. Present mismatched task, operation, direction or malformed snapshots fail unavailable.

History reports accepted Companion records without inferring streaks, rewards, penalties or confirmed game projection. D1 reads bind the active profile, connection and access epoch in one document/snapshot batch. Read-only tools require tasks:read, create no effects and expose no account selector. Production mobile review and sufficient-history streak semantics remain separate.

## Dependency-aware Todo candidate direction

Standing local direction selects get_unblocked_todos as a read of nonarchived active Todos with every explicit dependency currently Completed. Completed archived dependencies satisfy the check; archived incomplete, cancelled, paused, waiting, someday or active dependencies remain unsatisfied. A restored dependency blocks dependants again and can enter the candidate list as active work. Eligibility uses the full valid document before optional filters and guarded pagination.

Existing combined task filters apply except caller-specified kind/state. Document order supplies stable results without automatic ranking. Parent links, Daily occurrence completion, recurring schedules and game projection do not supply implicit dependency satisfaction or gameplay permission. Current own-profile/connection/access-epoch authorization and tasks:read remain mandatory. Candidate reads create no task change, score or effect. Saved views, review policy, production controls and prioritization semantics remain separate.

## Reviewed Todo relationship direction

Standing local direction selects relateTodo with only patch.relationships on an existing nonarchived Todo. An optional parentTaskId or explicit clearParentTask changes the parent; optional dependsOn replaces the complete set, including an explicit empty set. New references must be nonarchived Todos distinct from the target, and replacement dependency IDs must be unique with at most 100 entries. A parent may also be an explicit dependency. Omitted historical references remain intact.

Combined parent/dependency graph validation rejects cycles. Parent assignment never moves project membership, inherits completion/eligibility, changes state or completes children. Task order, other metadata, history and mappings remain unchanged. Projection requests are rejected; linked/pending/uncertain mappings retain existing fences with zero new game effects. Exact preview approval and original request/command receipts remain mandatory. Organization/state authoring, non-Todo relationships, production controls and real client qualification remain separate.

## Native organization command-history direction

Standing local direction selects optional OrganizationHistory in the canonical document for separate category/project/section/label/goal receipts. Commands use entityId, kind and create/update/archive identities rather than task IDs. Retained entries include the exact command, versioned payload hash, UTC acceptance time and typed entity snapshot. No projection, lease, effect or game-result field belongs to native organization history.

Root validation checks typed fields, references, causal order, snapshot transitions and current metadata. Full journal/persistence validation additionally checks command hashes and task/native identity/revision separation. An absent/null extension preserves unused defaults; explicit empty history remains explicit. Immutable history stays inside existing atomic document storage and backup/handoff; no D1 migration is required. Existing document/export byte limits also bound retained metadata history. Strict older clients require coordinated updates before use. Scoped transport and production controls remain separate units.


## Native organization edit and archival contract

Standing local direction selects exact create/update/archive acceptance for canonical metadata. New category/project references must be nonarchived; section project membership is immutable after creation. New explicit goal references must be nonarchived Todos, with at most 100 IDs per full replacement. Omitted fields and legacy goal references remain intact. Archive retains linked metadata, tasks, histories and source progress without cascading or reversing game effects.

Original hash-bound native receipts precede current-state/revision checks. Changed intent cannot reuse an identity. Local frozen/cloud authority cannot accept fresh edits. One optimistic store commit retains canonical metadata plus native history with no task/gameplay entry. Unknown acknowledgements preserve original command recovery. Production UI and live qualification remain separate.


## Reviewed native organization access contract

Standing local direction selects independent workspace.organization.write OAuth access for reviewed metadata preparation and acceptance. Existing task-write grants remain unchanged; read receipts require tasks:read. Auth consent names metadata edits and retained related records separately from game effects. The own-data browser adapter uses the existing cloud Workspace session and storage consent, with exact native preview validation before acceptance.

Server-issued creation/command IDs and retained request/correlation bindings recover the original native receipt after uncertainty. Exact canonical revision, profile/connection/access/grant epochs and short expiry bind fresh acceptance. Accepted bindings remain with history after preview cleanup or grant changes. No new account selector, generic proxy, upstream request or gameplay entry is introduced. Production UI, actual grants and publication remain separate.


## Native Todo planning state contract

Standing local direction selects setTodoState with only patch.todoState.state and an exact existing nonarchived noncompleted Todo. Desired states are active, waiting, someday and cancelled. Completion remains owned by completeTask and completed-work reopening remains owned by exact-event restoreTodo. Daily/Habit lifecycle, archiving and optional Inbox/Next/InProgress stages remain separate.

Native state changes preserve task order/details, planning, dependencies, organization, completion/restoration history and all game mappings. Projection requests fail; accepted task activity and game-effect counts remain zero. Existing accepted game commands are neither cancelled nor retried by native state changes. Current goal/candidate rules use the selected native state; cancelled tasks are excluded from goal eligibility but never counted as completed. Exact preview approval, original command recovery and existing task-write scope remain mandatory. Production UI and actual client qualification remain separate.


## Optional active Todo stage contract

Standing local direction selects optional executionStage inbox/next/inProgress as remembered active-work preference. Missing or cleared preference means Inbox while active. Current native waiting/someday/completed/cancelled/paused state dominates presentation. State changes, completion, restoration and archival retain dormant metadata; only explicit active state makes the preference current again. No project, parent, date or dependency inference supplies a stage.

Reviewed setTodoStage accepts only patch.todoStage with an exact stage or explicit clearStage true, on a nonarchived active Todo. Inactive or completed work requires separate reviewed state/restoration. Task order, current state, details, dates, organization, dependencies, history and mappings remain intact. Projection requests fail and accepted task activity/game effects remain zero. Exact original preview/receipt ownership and existing task-write scope remain mandatory. Optional serialization and v8 identity retain older unused encodings. Production board controls, named saved views and real-client qualification remain separate.


## Active-stage query contract

Standing local direction selects optional executionStage inbox/next/inProgress for canonical task and dependency-candidate reads. A selector matches only active nonarchived Todos; absent preference means Inbox. Dormant preferences never match current-stage queries. Explicit incompatible kind/state filters fail. Omitted stage leaves existing results unchanged.

All remaining filters combine with AND. Canonical order, matching totals and revision-bound continuation remain intact. Dependency candidates require every explicit dependency currently Completed regardless of stage; no stage supplies ranking or game permission. Existing own-profile tasks:read authorization remains mandatory. Named saved views, production UI and live qualification remain separate.


## Fixed saved task-view direction

Standing local direction selects optional canonical saved task views with exact ID, revision, name, archive state and a complete query definition. Definitions contain source tasks/unblockedTodos and only existing scalar combined predicates, including current active-stage filters. Saved definitions exclude result IDs, paging/revision, account selectors, arbitrary expressions and relative-date presets. Stored civil dates remain fixed; a name never changes date semantics.

Organization filter references must exist, including retained archived metadata. Candidate definitions refuse kind/state overrides and retain full explicit dependency eligibility. Current-state evaluation uses caller paging only with fresh revision guards; missing/archived views fail explicitly. Views share canonical identity/capacity limits and generic backup/storage/handoff. Optional default omission preserves unused older serialization. No read causes task/game effects. Reviewed authoring, public transport, relative dates, optional reminders and production controls remain separate units.


## Native saved-view authoring contract

Standing local direction selects savedTaskView as an appended native organization kind under existing separate OrganizationHistory. Creation requires a name and complete query. Update accepts name and/or complete query replacement; omitted query remains unchanged. Archive accepts no patch, retains the definition and history, and prevents fresh edits. Exact retained organization filter references may include archived metadata. No saved query name supplies additional date or mutation semantics.

Native v2 identity binds the full query and source while older unused native encodings remain unchanged. Typed snapshots retain exact accepted definitions; altered portable query/history/effect fields fail validation. One optimistic canonical commit saves views plus native receipts with no task activity, game command or dispatch. Original identities recover after later changes or acknowledgement loss; changed intent cannot reuse an identity. Scoped transport and production controls remain separate.


## Scoped saved-view access contract

Standing local direction selects tasks:read for named-view listing and current-state evaluation. Caller arguments select only exact view ID and paging; definitions cannot be overridden. Current profile, connection and access epoch bind fresh reads. Missing/archived views and stale pages fail; malformed/unsupported data remains unavailable without fake empty results. View names and query text are untrusted data and grant no action permission.

Reviewed savedTaskView creation/edit/archive uses separate workspace.organization.write. Full query previews, original server-issued identities and native request/correlation receipts remain mandatory across lost acknowledgements and later edits or grants. Browser read adapters discard changed sessions and validate exact provenance/identity/page shape. Existing task-write access does not grant view edits. Cloud disclosures name stored view definitions. No view read/edit dispatches task or game effects. Production list controls, relative dates, optional review/reminder policy and actual client/device qualification remain separate.

## Prospective ordinary Todo correspondence

Policy todo-required-v1 requires effective projection intent for newly prepared ordinary Todo create/update/complete commands, including independent full subtasks. Omitted/false inputs normalize at preparation; fresh raw submissions with false fail. The server preview names the effective intent before acceptance. Active unlinked completion queues original create before score; completed unlinked edits require separate historical review. Local capture remains pending without automatic consent, credential upload, ownership transfer or executor activation. Native planning/state/organization remain non-scoring. Accepted historical command hashes, effect lists and original receipts remain immutable and recoverable; rejected old unaccepted previews need a new review. Recurring creation, grouping tags, explicit unchanged-task linking and historical conversion remain separate work.

## Unchanged-task repair contract

Reviewed ensureProjection requires an exact active Todo, projectToHabitica true and no patch. Acceptance preserves task content and activity, records one Create effect and uses the original correlation identity. Unresolved original creation, uncertain mappings and retained archived mappings require original-command recovery or explicit review. Completed/archived missing counterparts are not recreated or scored. Typed adoption previews retain separate native/remote identities, kind and completion state; exact workspace/access epoch, revision and fingerprint bind approval. Repeated accepted adoption returns the retained original mapping result without a new write. Existing-state adoption remains available for matching retained Todos, Dailies and Habits.

## Habit counterpart creation

New Habit preparation carries required game intent and explicit up/down defaults true/true. All Boolean direction combinations remain supported; disabled directions refuse scoring. Creation contains no score event, reward, Todo deadline or checklist payload. Supported unchanged active Habits can use the original create-only repair ledger. Habit-owned response parsing accepts absent completed and requires Boolean direction flags. Kind, alias and requested-field verification protect mapping; uncertainty preserves the original private receipt and forbids replacement creation.


## Supported Daily correspondence

Ordinary new Dailies require an explicit complete supported schedule and one requested correct-kind Habitica counterpart. UTC or an explicitly fixed Etc/GMT zone must match the account observation; native parity currently requires midnight Habitica dayStart. Multiweek intervals require a Monday start. Unsupported zones, boundaries, yearly/weeks-of-month authoring and missing rules are unavailable rather than silently approximated. Canonical IDs, recurrence history and original command receipts remain durable. Creating the counterpart grants no rewards for past dates and invokes no Cron. Existing adopted records and linked schedule edits retain separate review contracts.


## Shared task-default policy

Ordinary task creation, field edits, completion and Habit scoring request supported Habitica correspondence for every task kind and full child. New omitted/false compatibility intent cannot select a reward-free ordinary workflow. Canonical acceptance remains available locally without implicit server credential consent or execution, but required game effects remain pending and unconfirmed. Unschedulable Daily activity and retained identity conflicts require schedule/adoption review. Explicit native planning, schedule preparation and lifecycle metadata remain separately disclosed non-scoring operations. Accepted historical commands retain original bytes, hashes and receipts; historical imports never auto-score.


## Portable tag ledger contract

An optional schema-1 tagLedger binds one exact Habitica account UUID to typed group mappings and separately identified ensureTag/setMembership intents. Every mapping keeps the desired canonical revision/name, internal workspace/kind/entity alias, correlation and optional remote identity/evidence. Membership intents retain the exact target tag and native/remote task identities with explicit present/remove intent. Confirmation requires an original receipt reference, UTC observation and fingerprint. Unknown or ambiguous outcomes remain distinct and grant no replay.

Canonical storage/export preserves archived and removed metadata and accepted native command history. Upgrading or importing the model never generates tasks, scores, tag mutations, credential consent or executor permission. Account-scoped coverage refuses missing/foreign context and distinguishes tag identity from recorded membership receipts. Authenticated verification, complete derived membership and actual reconciliation belong to subsequent milestones.


## Private native tag operation contract

habitica-tags-v1 is a separate owner-managed account consent. habitica.tags.write is an additive agent scope; validated Workspace identity reports tagWriteAllowed, and future public canonical adapters must require that scope. Private Auth tag operations require the restricted binding, current consent epoch, credential connection, exact group context and recent single-use observation. Existing native organization/task permission never implicitly supplies tag consent or scoped agent authority.

Tag creates bind one planned UUID and reviewed name; adoption binds an exact native tag. Renaming requires adopted identity. Membership adds/removes one adopted tag on one owned non-party/non-challenge task and preserves unrelated observed membership. Tag deletion, reward tasks, scores, spells and party effects are outside the tag allowlist. All mutations share original durable attempt identity and gameplay unknown fences.

Unknown effects are not replayed. Original receipt lookup sends nothing. A separate explicit recovery may review matching current metadata against the original request/observation and record reviewed_observation on the original attempt. Observation review is distinct from proof of the original successful response and performs no second native mutation. Missing or conflicting evidence remains unknown. Canonical reconciliation, complete saved-selection membership, browser controls and actual accounts remain the following milestones.

## Native archival and occurrence recovery

unarchiveTask and correctOccurrence are separate reviewed commands. The existing request/preparation/acceptance/original-receipt contract, expected workspace revision and profile scopes remain authoritative. Both operations produce one causal native recovery event and zero projection effects. Current task order, schedules, deadline, review cues and accepted external commands remain unchanged. Retained game identities and nonunlinked mappings, including archived mappings, refuse recovery. Unresolved task effect journals also refuse before acceptance. Habit inverse events and upstream unchecking remain separate contracts.

patch.unarchive requires archivedRevision matching the exact archived task revision. Unarchive preserves task state: an archived completed Todo remains completed. Retained inactive category/project/section/label/parent/dependency references refuse with unarchive_repair_required. Structurally missing references keep an imported document invalid before preparation. An explicit nested planning/relationships repair can replace or clear affected references in the same reviewed command; dates, task content and recurrence cannot be edited through repair. The complete resulting graph must validate. Acceptance appends TaskUnarchive with the prior archive revision. A second command cannot reuse the same archive cycle. A retained archive receipt supplies an additional exact before/after task-field check during journal import.

patch.correctOccurrence requires occurrenceId, occurrenceRevision and targetCommandId. Only a current terminal completed/skipped native Daily occurrence with a matching retained zero-effect completeTask/skipOccurrence source receipt is eligible. Archived tasks require separate unarchive first. Missing source receipts remain unavailable, including imported snapshots without the source journal. Acceptance retains the original completion event and an immutable PreviousOccurrence snapshot in OccurrenceCorrection, then returns the same occurrence ID/date to Pending. No next occurrence is generated and no schedule is broadened. A later explicit completion or skip can start another terminal cycle; correction binds the new cycle rather than replaying an old target.

New terminal occurrence writes carry optional TerminalCommandId; unused legacy occurrences keep the prior wire shape. Corrections retain exact source identity. Portable document validation checks task/date/revision, causal ordering, terminal source binding, unique correction targets, completion cycles and pending state. Full journal validation additionally verifies source operation/date/effects, recovery patch/hash and exact zero-effect receipt. Completion reads expose occurrenceCorrection alongside the original completion rather than erasing history. New optional Unarchives and OccurrenceCorrections collections remain absent for unused legacy documents. Strict older readers require coordinated client/Worker publication; schema version and database columns remain unchanged.

get_task_recovery is a reviewed tasks:read capability for an exact canonical task, including archived tasks. The bounded revision-guarded response reports current task, unarchive repair reason and retained occurrence snapshots with original source IDs and explicit eligibility. Reads create no history or external dispatch. unarchive_task and correct_occurrence accept only the original reviewed server-issued commandId under habitica.tasks.write. Neither acceptance schedules tag reconciliation. Lost acknowledgements recover the original receipt after later revisions; a missing receipt never authorizes a replacement intent.

The typed browser cloud adapter validates exact recovery intent and refuses game-bearing previews or receipts. The [history recovery controls](history-recovery.md) expose completed-Todo restoration, native archival/Daily recovery and a separate linked uncheck review. Local cross-runtime qualification supplies no real-account, physical-device, hosted-client or reward-reversal evidence.

## Reviewed linked completion recovery

Linked unchecking uses a separate expiring reviewed action and original native receipt. Linked recovery does not extend ordinary non-Habit score/down or native-only restore/correction commands. Canonical `LinkedTaskRecoveries` pairs with the retained Todo restoration or Daily occurrence correction, keeping the original completion/command/dispatch history. Both C# and Worker journals validate source hashes, original score attempt and causal pairing; a game-effect source cannot establish native recovery without the paired linked receipt.

A profile lease and `linked_recovery_fence` hold canonical writes and competing game effects across dispatch or uncertainty. Only the original successful receipt can finish the canonical commit; current task state cannot prove a score, refund or recovery. Public MCP and typed browser adapters use the same preview/accept/receipt policy. The [history recovery controls](history-recovery.md) expose the supported native and linked reviews. See [linked task recovery](linked-task-recovery.md) for exact consent, scope, capacity, account/day, unknown-outcome and deployment boundaries.

## Recovery presentation ownership

The history route exposes exact reviewed recovery through the existing native task controller and a separate scoped linked recovery state. An exact task/completion eligibility read returns an active original recovery before attempting fresh eligibility; the response exposes public review/receipt fields only. Reopened history can recover the original identity without another dispatch. Source/account changes, stale reviews and unresolved effects preserve the established server boundaries. See [history recovery controls](history-recovery.md).

## Canonical history completeness

[Task activity coverage](task-activity-coverage.md) defines optional portable accepted-command witnesses and effective schedule/state intervals. Consumers require full-journal causal validation, preserve unknown legacy prefixes/gaps/tails and never infer completeness from task creation, current recurrence, paginated counts or query time. Canonical coverage does not prove Habitica history, rewards or streaks. Missing referenced sources invalidate a coverage assertion; unsupported special transitions remain unknown.

## Durable exact task adoption

Browser and MCP use the shared metadata-only prepare_task_adoption, accept_task_adoption and get_task_adoption_command contract. Reviews bind the exact owned canonical/native pair, fresh fingerprint, revision and connection/access/grant identity; original receipts survive later revisions. Retained completed/archived tasks keep content and history without scores or native writes. Old mutable-revision adoption routes refuse. [Exact adoption and migration parity](task-adoption.md) defines quotas, atomic persistence, uncertainty and intentional direct-user boundaries.


## Scoped stored tag correspondence

Status and source-retirement prerequisites share the complete desired-member comparison. Group identity/name/lifecycle, coherent mapping/intent scope, exact native tag/task IDs, unique latest intent and confirmed per-member evidence establish stored correspondence. Unrelated global revisions preserve matching evidence; workspace revision still binds coherent paging. Group revision beyond retained scope, uncertain intents, changed membership/remaps and missing receipts require review. Cloud reads additionally require durable confirmed work in the current connection/access epoch; imported or inaccessible provenance remains unverified. Portable prerequisite checks grant no live authority, deletion or fresh provider claim. Source retirement retains the separate current consent, ownership, unresolved-effect and fresh native verification gates.
