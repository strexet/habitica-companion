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
- The remaining **E01–E14 product, UX and reliability epics** below are raw research/decomposition intake. Use exact P- IDs or full task titles with `Process pending`; E01–E14 are provenance labels. Decompose selected intake into independently verifiable tasks, preserving every originating Pending ID and recording required T- dependency edges.
- The remaining prioritized contracts below carry explicit dependencies and blockers. `Ready` means the task contract has no unanswered blocking question; unfinished prerequisites remain in `depends_on` and must complete before execution. `Implement next` must not skip a blocked highest-ranked task.
- Once Companion-owned task/project/goal storage is established, Habitica is a game projection. No parallel direct-to-Habitica MCP write path is allowed.
- Persist accepted application commands, but do **not** promise exactly-once remote rewards when Habitica reports a timeout without upstream idempotency. Preserve unknown outcomes for reconciliation.
- Conduct hands-on competitor/mobile usability work in E01 before broader release of redesigned UI. Owner selection on 2026-10-19 permits development implementation and feature exposure using the adopted local baseline before human/device qualification; missing observations remain unverified.


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

### Resolve mass spell rate limits and explicit gear recovery

```yaml
id: P-8e7323df-2f47-4e19-864d-821ed108e7f2
priority: 106
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- Capture the codev P0 incident involving acknowledged 17 of 40 casts, HTTP 429 and uncertain auto-equip cleanup. Preserve coordinated account pacing across casts, gear and background requests, safe structured diagnostics, confirmed progress and separately visible restoration outcomes. Full original incident details remain in the source artifact.

Expected behavior:
- Bound request admission by Retry-After and reset evidence; stop or pause safely on quota failures, never replay unknown casts, retain acknowledged partial counts, persist exact manual gear recovery and expose mobile phase/cooldown/cleanup state. Cover failures during cast, restore, refresh and cancellation without hidden errors or fabricated success.

Current context:
- Existing accepted work includes T-2b2f2c03-8085-4ff0-ba49-f735c04dfeee for server pacing and T-e7d542d4-4a47-4155-afc9-ab0dfb457fa0 for original gear recovery. Process Pending must compare retained source intent, current code and accepted evidence before defining residual work; local evidence does not establish deployed iPhone incident closure.

Source:
- Owner-requested transfer from Companion Development, alias codev, project c5f8a175-2bee-484a-9b72-018737dc10fd, task fe2403b8-ab96-489f-b6ac-5e72f9dee4a9, task revision 115, workspace revision 158. Complete original text: skill-tree/history/intake/codev-2026-10-28/source-tasks.json. Source completion acknowledges intake only.

Priority rationale:
- Preserve source urgent P0 ordering above the current highest open priority 105; no live spell execution or release authorization is implied.

Processing blocker:
- Static reinspection confirms acknowledged-count, quota/cooldown, unknown-outcome and original gear recovery controls remain implemented; accepted outcomes T-b89fac23-3ac2-4c64-8560-be7a5d796a81 and T-e7d542d4-4a47-4155-afc9-ab0dfb457fa0 already cover the feature scope. Fresh loaded PWA build identity and sanitized iPhone evidence remain unavailable; any live reproduction requires a bounded owner profile. No duplicate implementation or deployed incident closure follows. See skill-tree/history/processing/note-folders-2026-10-29/research.md.

## Prioritized Next Changes

Highest numeric priority first; unmet dependencies and blocking questions stop implementation. Imported source provenance and product decisions remain distinct from newly executed research.

### Qualify the selected Netlify callback gateway deployment

```yaml
id: T-ec421773-5864-4bc5-b17f-ce04e0d818c2
priority: 101
status: Ready
parent_pending_ids: ["P-2d43941f-f8b5-489c-9467-dd2dcc7383a4"]
processing_run_id: PR-0397ff3e-5942-44c8-8fbf-32966ad290af
root_task_id: T-6a8db6be-b721-4b88-b62b-c6566d759080
related_task_id: T-280cb859-c0f7-46e5-97db-bbd41dc6a99a
relation: blocker
depends_on: ["T-dc0e4117-f2a0-43c8-8d29-72e640d7bba9"]
```

Goal:
- Provide the owner-selected Netlify Functions Free callback egress boundary without changing callback-secret custody or activating subscriptions.

Current behavior:
- Auth has no callback egress grant fence. The retained protocol prototype uses fake network adapters. The owner selected Netlify Functions Free; deployment and real callback execution require a concrete account and explicit scope.

Desired behavior:
- Qualify the exact candidate on a selected owner Netlify Free account/site and matching Auth deployment using a bounded operator-approved profile. Record runtime, deployment/rollback identity, Free-plan hard limit, environment secret names, redacted provider logging configuration and permitted test destinations.
- Verify actual signed challenge bytes, public IPv4/IPv6 connection policy, controlled DNS changes, original-host TLS, redirect/private/mixed-address rejection, concurrency/replay, cold-start deadline and unavailable/quota behavior. Keep subscription activation unavailable until evidence passes.

Touch:
- Operator setup, selected Netlify/Auth deployment configuration and hosted evidence only; scoped repairs return to the existing implementation checkpoint.

Discovery allowance:
- Inspect connected provider access without exposing credentials. Obtain missing exact account/site, controlled fixture destinations and publication scope from the owner after the local candidate is reviewable.

Out of scope:
- Paid resources, automatic upgrades, provider fallback, real user callback enrollment, ChatGPT messages, subscriptions and gameplay.

Implementation constraints:
- Account creation, secret entry, remote migration and publication require explicit operator scope. No provider documentation or local TLS fixture substitutes for hosted acceptance. Do not probe unowned private services.

Questions and required clarifications:
- [Resolved - execution prerequisite] Netlify Functions Free is selected. The owner supplies the concrete account/site and hosted execution scope when the local implementation and profile are ready. No live access is inferred from the planning decision.

Validation:
- Execute the bounded hosted profile against owned fixtures after explicit scope; retain sanitized original receipts, negative controls, runtime/configuration and rollback evidence. Failed or unavailable controls leave the dependency incomplete.

Acceptance:
- The exact deployed gateway and Auth claim endpoint pass the approved hosted DNS/TLS, signed-byte, replay, deadline and unavailable/quota controls with sanitized evidence and a verified rollback path.

Documentation updates:
- Record actual hosted coverage and remaining host/device limitations; retain no real callback URL, signing material or account credentials.

Risks:
- Missing owner account, publication authority or controlled fixtures blocks execution. Free quota exhaustion must preserve undelivered state without paid fallback.

### Browse and manage standalone and project Notes folders

```yaml
id: T-23342106-269e-4cd6-992e-4ff95be3e62d
priority: 100
status: Ready
parent_pending_ids: ["P-51d5734b-82dc-4f91-82c6-d4eca50a3114"]
processing_run_id: PR-510172b6-67f0-4cdd-8340-aaa0ec3e9d20
root_task_id: T-23342106-269e-4cd6-992e-4ff95be3e62d
related_task_id: null
relation: main
depends_on: ["T-94b18897-2c2f-44ab-b89b-34e58b3903c5", "T-a48aa46e-fd3c-43ff-b9b3-3becddb47599"]
```

Goal:
- Make thematic folders usable in general Notes and the exact Companion project view.

Current behavior:
- NotesPanel exposes a flat title/Markdown library. The existing task T-a48aa46e-fd3c-43ff-b9b3-3becddb47599 owns the project picker and project Notes composition; folder UI extends the accepted project composition instead of duplicating the predecessor.

Desired behavior:
- Expose All notes, notes without a folder, standalone folders and project-scoped folders with clear current context. Allow folder create/rename/remove-empty, folder browsing, new-note-in-folder and moving existing notes into or out of a folder.
- Support the owner Altai project with Hotels and Interesting places folders, separate hotel/place notes and exact descriptive content. Keep project notes outside folders visible. Combine folder selection with text/trash/project filters and paging.

Touch:
- NotesController and volatile drafts, NotesPanel.razor, TodoProjectsPage.razor and accepted project Notes components, scoped styles, diagnostics operation names and focused component/browser fixtures.

Discovery allowance:
- Inspect the completed project UI and folder API/MCP contracts before editing. Inventory routes sharing changed Notes controls and preserve canonical selection, source and notebook identity.

Out of scope:
- New backend semantics, nested folders, multi-folder membership, drag-and-drop as the only control, bulk note deletion, project deletion cascades, remote deployment and real device/account qualification.

Implementation constraints:
- Preserve the existing primary Notes editor, exact Markdown, retained versions, drafts, consent/source isolation, conflict handling, export/import and original uncertain-command recovery. Importing a Markdown file remains an unsaved draft rather than a silent folder assignment.
- Review exact before/after folder/project scope for note moves. Clearing a folder preserves the project. A move into a standalone/project folder that changes project scope requires an explicit reviewed project change; stale or unavailable references remain visible and detachable.
- Explain nonempty-folder removal refusal, including trashed current members; provide normal navigation/move controls without implicit deletion. Folder labels never select identities. Account/source/project changes clear obsolete selection and cannot apply late responses to another notebook.
- Use existing light/dark semantic surfaces, visible keyboard focus, readable wrapping and primary actions at phone/desktop widths. Source navigation never uploads Notes or broadens permissions.

Questions and required clarifications:
- [Resolved - direct owner replies] Single-level folders and zero-or-one membership are recorded in owner-decisions.json. Folder project scope is fixed; notes without folders remain supported.

Validation:
- Component journeys cover standalone and project folders, create/rename, note capture/move/detach, dirty drafts, exact accepted-version verification, nonempty removal including trash, stale projects, duplicate names, source/account races and original receipt recovery.
- Render every changed/shared route in built-in light and dark schemes including obsidian-glow at phone and desktop widths, normal/200-percent text, long folder/project/note names, empty/disabled/error states, visible primary actions, keyboard focus, geometry and contrast. Record physical-device, assistive-technology and hosted-path gaps separately.

Acceptance:
- Users can organize and find separate thematic notes outside projects and inside exact project folders, with no content/history loss or hidden reassociation.
- All scoped UI actions have reviewed MCP counterparts; unknown acceptance, private access, stale context and unsupported removal remain explicit, and required interaction/rendered checks pass.

Documentation updates:
- Update features.md, ux-ui-manifest.md, testing.md and the canonical folder contract for actual Notes/project routes and verified interaction boundaries.

Risks:
- Shared selection/draft ownership or crowded project controls can discard content or hide primary actions. Validate every affected route and retain no-folder/project context explicitly.

### Expose reviewed Notes folder workflows through MCP

```yaml
id: T-94b18897-2c2f-44ab-b89b-34e58b3903c5
priority: 100
status: Ready
parent_pending_ids: ["P-51d5734b-82dc-4f91-82c6-d4eca50a3114"]
processing_run_id: PR-510172b6-67f0-4cdd-8340-aaa0ec3e9d20
root_task_id: T-94b18897-2c2f-44ab-b89b-34e58b3903c5
related_task_id: null
relation: main
depends_on: ["T-7aa20175-78af-40c9-86a3-e1ea37109c34"]
```

Goal:
- Provide complete reviewed agent parity for discovering folders, managing folder metadata and placing Notes.

Current behavior:
- search_notes/get_note/prepare_note_command/accept_note_command/get_note_command support Notes and project links, but folder schemas and folder command receipts are absent.

Desired behavior:
- Expose bounded folder list/search, exact folder reads and create/rename/remove-empty preview/accept/original-receipt tools through the existing Auth/MCP/Workspace boundary. Extend note preview/read/search with exact folder membership, no-folder filters and accepted-version verification.
- Audit the full folder workflow, including creating a note directly in a folder, moving an existing note, detaching, duplicate labels, empty-folder removal and recovery after uncertain preparation/acceptance. Update packaged habitica-notes guidance for English and Russian folder intent.

Touch:
- workers/shared/note-tools.ts, workers/chatgpt-mcp/src/note-tools.ts, note-tools-api.ts, scoped tool registration, plugin/skills/habitica-notes/SKILL.md and actual-SDK tests.

Discovery allowance:
- Inspect current tool discovery/scopes and the accepted folder-cloud contract. Reuse existing reviewed preparation and epoch guards or add a separately typed folder preparation lane without weakening exact identity/hash checks.

Out of scope:
- Browser UI automation as a permanent workflow, folder recursion/sharing, arbitrary API passthrough, task writes, hosted account/device tests and package publication.

Implementation constraints:
- Require separate Notes consent/read/write as appropriate; active canonical tasks:read is additionally required when creating a project folder or explicitly changing project placement. Folder names and note bodies remain untrusted data.
- Previews bind exact notebook/folder/note identities and revisions, before/after folder and project membership, unchanged content hashes, lifecycle consequences, grant/access epochs and expiry. Clarify ambiguous names rather than inferring a target.
- After uncertainty reuse the original request and arguments or recover the original command receipt. Verify accepted metadata/version before reporting the result; missing receipts never authorize replacement writes. Empty-folder removal never purges member notes or retained content.

Questions and required clarifications:
- [Resolved - repository rule] Agent-required browser actions require reviewed MCP parity with matching ownership, consent and receipt recovery. Explicit unambiguous folder intent permits matching acceptance under the existing Notes policy.

Validation:
- Actual MCP SDK controls cover advertised schemas/annotations, read/write and project permission separation, duplicate names, untrusted folder/note instructions, full Altai hotel/place journeys, unchanged Markdown, stale/expired/revoked previews, two profiles and original receipt recovery after later rename/removal.
- Keep existing note/project tool discovery and content-preservation fixtures; qualify server/tool behavior separately from installed host or actual model decisions.

Acceptance:
- An authorized agent can complete every scoped folder workflow through reviewed MCP tools and verify exact original outcomes without browser-only gaps.
- Ambiguous targets, foreign ownership, nonempty removal, missing project authority and uncertain outcomes cannot create unintended changes or task effects.

Documentation updates:
- Update the folder contract, plugin guidance, features.md and testing.md with actual tool identities, permission requirements and unqualified host boundaries.

Risks:
- A partial tool inventory can leave management/recovery browser-only; renamed labels and stale notebook identities must never redirect a command.

### Persist Notes folders through the cloud owner API

```yaml
id: T-7aa20175-78af-40c9-86a3-e1ea37109c34
priority: 100
status: Ready
parent_pending_ids: ["P-51d5734b-82dc-4f91-82c6-d4eca50a3114"]
processing_run_id: PR-510172b6-67f0-4cdd-8340-aaa0ec3e9d20
root_task_id: T-7aa20175-78af-40c9-86a3-e1ea37109c34
related_task_id: null
relation: main
depends_on: ["T-5111060c-76ae-4a0e-a48b-260ecbb8885f"]
```

Goal:
- Provide cloud persistence and browser-client transport for the shared Notes folder contract.

Current behavior:
- Workspace D1 stores current note rows, retained version JSON, immutable note receipts and separate Notes preparations. Migration0017 adds project_json; no folder storage or folder API exists.

Desired behavior:
- Add an additive Workspace migration and bounded owner routes for folder discovery/exact reads, explicit folder commands and original receipts. Extend note create/update and search with the shared membership/filter contract.
- Update NotesCloudClient and application interfaces to validate extended folder backups, identities, current/retained metadata and exact original receipts. Browser and cloud sources retain independent namespaces.

Touch:
- workers/shared/notes.ts, notes-api.ts, Workspace migrations, Notes lifecycle cleanup, INotesCloudClient.cs, NotesCloudClient.cs and Worker/client fixtures.

Discovery allowance:
- Use the folder-model wire fixtures and current Notes/project transaction guards. Inspect private Auth Notes identity, current D1 batch conventions and revocation/delete cleanup before extending routes. Consult official platform documentation only for new platform behavior.

Out of scope:
- MCP tool exposure, folder UI, remote migration/publication, real private content, new consent grants and Habitica API calls.

Implementation constraints:
- Commit folder/note membership, notebook revision, retained history and original receipts atomically. Recheck current Notes connection/access epoch and exact project authority at acceptance; a failed guard or receipt insert rolls back every write.
- Preserve existing Notes consent, owner/profile isolation, quotas, content-free diagnostics and original request identity. Separate Notes permissions from project access; project-only readers cannot see private folder names, note titles or snippets.
- Remove folder names/current metadata and retained content copies during explicit notebook/profile deletion. Disconnect revokes access without deleting stored content. Never automatically copy browser folders or rebind imported project references.
- Support complete-set text/trash/project/folder/no-folder filtering before twenty-row paging with unchanged notebook revision. Retain stale references visibly and recover original receipts before prospective folder/project validation.

Questions and required clarifications:
- [Resolved - repository contract] The private Notes namespace owns folders. Folder commands do not use Workspace organization/task commands or native tag consent.

Validation:
- Synthetic D1/HTTP and browser-client controls cover legacy migration, backup roundtrip, Unicode names, exact folder IDs, more than twenty matches, stale paging, foreign profiles, removed/archived projects, grant/session epochs, rollback, concurrent changes and lost acknowledgement.
- Prove deletion/disconnect cleanup, malformed or foreign response refusal, expired sessions and late-account responses. Keep existing Notes and project wire regression controls.

Acceptance:
- The owner API and typed client implement the shared folder behavior with exact C#/Worker bytes and original-receipt parity.
- Cloud changes preserve legacy Notes content/history and fail atomically under ownership, revision, scope or receipt faults. No task/game effect or live provider call follows folder work.

Documentation updates:
- Extend the canonical folder contract, technical.md, features.md, testing.md and migration guidance with verified local boundaries.

Risks:
- D1 rollback, notebook deletion and preparation cleanup must include folder metadata without changing accepted legacy receipts or leaving private name copies.

### Add versioned Notes folders and local persistence

```yaml
id: T-5111060c-76ae-4a0e-a48b-260ecbb8885f
priority: 100
status: Ready
parent_pending_ids: ["P-51d5734b-82dc-4f91-82c6-d4eca50a3114"]
processing_run_id: PR-510172b6-67f0-4cdd-8340-aaa0ec3e9d20
root_task_id: T-5111060c-76ae-4a0e-a48b-260ecbb8885f
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Support single-level thematic folders in the existing private Notes notebook, with zero or one folder per note.

Current behavior:
- NotesModel retains note content, receipts and one optional versioned project association. NotesEngine has no folder entity or folder commands. IndexedDB commits assume the current note-only revision/receipt sequence. See source-fingerprints.json and research.md in skill-tree/history/processing/note-folders-2026-10-29.

Desired behavior:
- Add stable folder identities, revisions, names, optional exact project scope, retained removal identity and explicit create/rename/remove-empty commands. Folder project scope remains immutable; nested folders and multiple-folder membership are excluded.
- Allow a note create/update to select or clear one folder. Omitted membership preserves the existing folder. Scope-consistent placement preserves exact Markdown, note identity and prior versions. Detaching from a folder preserves the project association; moving to a differently scoped folder must explicitly review and atomically apply the matching project change.
- Support folder discovery and combined text/trash/project/folder/no-folder filtering before revision-bound paging. Distinguish notes without a folder from notes without a project. Keep folder metadata in account-bound Notes exports/imports.

Touch:
- NotesModel.cs, NotesEngine.cs, NotesService.cs, NotesProjects.cs, NotesQueries.cs, IndexedDbNotesDatabase.cs, indexedDbStorage.js and focused model/service/storage fixtures.

Discovery allowance:
- Define a portable folder command/receipt encoding alongside the existing note contract. Inspect global notebook revision and receipt invariants before extending storage. Explicitly version the extended backup shape and refuse unsupported readers without losing metadata.

Out of scope:
- Cloud HTTP/MCP transports, browser folder UI, recursive folders, shared folders, folder scope moves, multi-folder membership, bulk deletion, task entities and Habitica tag projection.

Implementation constraints:
- Keep existing accepted command bytes/hashes and legacy backups readable; absent folder metadata means no folder. Use neutral C#/TypeScript wire fixtures for new commands without rewriting existing fixtures.
- Use exact owner/source/notebook/folder/project identities. New project folders and assignments require active canonical own-project authority checked atomically with the Notes commit. Retained stale project/folder references remain readable, content-editable and explicitly detachable. A project deletion never deletes Notes.
- Reject folder removal while any current note, including trash, references the folder. Retain an identity tombstone for historical memberships; never silently delete or detach notes. Preserve original receipts after rename/removal and uncertain acknowledgements.
- Keep revision/CAS and immutable-receipt invariants valid across note and folder commands. Bound folder metadata/capacity and preserve Notes lifecycle reserve, explicit quota failures, account separation and source isolation. Names never establish ownership or enter diagnostics.

Questions and required clarifications:
- [Resolved - direct owner replies] owner-decisions.json records single-level folders and zero-or-one folder membership with exact content/history preservation.
- [Resolved - bounded implementation scope] Folder scope is immutable. Empty-only removal, explicit project-change review and no-folder/project distinction avoid implicit cascades or hidden reassociation. Expanded folder lifecycle is outside the selected contract.

Validation:
- Model/service tests cover standalone/project folders, duplicate labels with distinct IDs, create/rename/remove-empty, placement/clear, scope changes, trashed members, stale projects, foreign/source rejection, retained versions, quotas and original receipts.
- Use disposable browser IndexedDB assertions for legacy upgrade/import, concurrent note/folder writes, project-authority races, rollback, export/reload and lost acknowledgement. Preserve unrelated stores and remove only generated synthetic records.

Acceptance:
- Browser Notes storage round-trips folders and note membership without changing legacy receipts, exact text or history. Invalid, stale or nonempty-folder commands leave the notebook unchanged.
- Folder-only operations and association-only note changes recover the original accepted receipt after later edits or removal; Notes remain independent of tasks and Habitica effects.

Documentation updates:
- Document the folder wire/storage and lifecycle contract in one canonical lowercase kebab-case document; update technical.md, features.md and testing.md only for implemented behavior.

Risks:
- Adding a second command kind can invalidate the current receipt-count/revision invariant or legacy JSON readers. Backups and historical membership require explicit compatibility controls.

### Deliver due task reminder events with durable original receipts

```yaml
id: T-569ff3a0-8765-49b6-9b37-a62e091617ae
priority: 99
status: Ready
parent_pending_ids: ["P-5d1f3c86-1df1-4b4c-ab7f-96762dc2b192"]
processing_run_id: PR-9722057b-0549-4478-8c12-6ee286276060
root_task_id: T-6a8db6be-b721-4b88-b62b-c6566d759080
related_task_id: T-6a8db6be-b721-4b88-b62b-c6566d759080
relation: followup
depends_on: ["T-280cb859-c0f7-46e5-97db-bbd41dc6a99a"]
```

Goal:
- Add the default-off exact-task reminder Events channel on the qualified protocol and subscription foundation.

Current behavior:
- Durable reminder state and Web Push have separate schedule/receipt contracts. The Events prototype can model signed delivery but is not a production sender and has no actual host reply evidence.

Desired behavior:
- Match due exact-task reminder state to independently authorized live subscriptions and send generic schema-valid signed events through the Auth-owned protected callback gateway. Persist stable event IDs, original attempt receipts, expiry and connection/resource fences.
- Report scheduled, accepted-by-host, refused, retryable and unknown delivery separately from an actual subscribed-chat response; unsubscribed, revoked and expired resources stop delivery.

Touch:
- Workspace reminder/event outbox and scheduler, shared event payload/status contracts, Auth private signing/dispatch gateway, Events subscription lifecycle, MCP read-only original event status and synthetic composition fixtures.

Discovery allowance:
- Reuse existing reminder civil-time/DST and original delivery identity rules without coupling Events permission to push/foreground consent. Validate the final protocol/event schema against the official profile and the completed predecessor contracts.

Out of scope:
- Raw conversation IDs, local Codex control sockets, task completion/scoring, implicit subscription or notification consent, actual chat messages, enabling rollout gates, key provisioning and hosted/device publication.

Implementation constraints:
- Use generic minimum payloads with exact task/event identity and no private Notes, task text, callback URL or signing secret. Enforce current account/connection/grant/resource authorization again before dispatch.
- Persist dispatch intent before the send; preserve event ID across only contract-permitted bounded transient retries, with fresh signing timestamp/signature and exact body bytes. Stop on410/413, expired subscriptions, revoke and exhausted attempts. Original uncertainty never authorizes a new event identity or a duplicated game action.
- Use the predecessor protected HTTPS adapter and Auth vault. Keep server schedule state, transport receipt and actual host rendering distinct; a2xx callback alone cannot confirm a subscribed-chat response or Codex support.
- Retain default-off configuration and bounded quotas/cleanup. Any actual host rescan/subscription/callback operation requires a separately selected qualification profile; local acceptance supplies no real-chat delivery claim.

Questions and required clarifications:
- [Resolved - owner/repository scope] Auth-vault ownership and separate consent are selected. Protocol migration and verified subscription transport/lifecycle are explicit required dependencies; live host/device operations remain outside local implementation scope.

Validation:
- Synthetic end-to-end due/cancel/edit/DST cases cover matching/nonmatching exact tasks, repeated wakeups, account and permission changes, signed body/header identity, duplicates, bursts, expiry/restart, transient retry limits,410/413 and original unknown receipts.
- Assert no unmatched/unsubscribed delivery, secret exposure, native task effects or feedback loops. Retain a concrete owner-run host qualification checklist for rescan, subscribe, callback verification, actual response and unsubscribe, with each live outcome left unqualified until observed.

Acceptance:
- Only due authorized exact-task subscriptions produce bounded signed Events with stable original identities, durable lifecycle receipts and enforced retry/revocation/expiry controls.
- Public status and documentation distinguish local scheduling, host transport acknowledgment, uncertainty and actual chat response; no default gate, unrelated consent or live provider/chat operation changes during local qualification.

Documentation updates:
- Update features.md, originating-chat-reminders.md, protocol/scope/retention contracts, testing.md and packaged reminder guidance with the implemented channel and explicit host/device limits.

Risks:
- A retry or stale subscription can duplicate notifications or leak task context. Preserve exact event identity, minimal payloads and current resource/connection checks at every dispatch.

### Implement Auth-owned callback secrets and reviewed event subscriptions

```yaml
id: T-280cb859-c0f7-46e5-97db-bbd41dc6a99a
priority: 99
status: Ready
parent_pending_ids: ["P-5d1f3c86-1df1-4b4c-ab7f-96762dc2b192"]
processing_run_id: PR-9722057b-0549-4478-8c12-6ee286276060
root_task_id: T-6a8db6be-b721-4b88-b62b-c6566d759080
related_task_id: T-6a8db6be-b721-4b88-b62b-c6566d759080
relation: followup
depends_on: ["T-273899d8-6f17-43ee-b82f-41013e1021b9", "T-dc0e4117-f2a0-43c8-8d29-72e640d7bba9", "T-ec421773-5864-4bc5-b17f-ce04e0d818c2"]
```

Goal:
- Implement the separately consented subscription foundation using the owner-selected Auth-vault callback boundary and exact resource ownership.

Current behavior:
- The current MCP exposes tools only; no production Events subscription store, callback vault or protected callback transport exists. The offline prototype uses fake authorization/network adapters and does not establish production persistence, encryption or egress safety.

Desired behavior:
- Add authenticated Events discovery/list/subscribe/unsubscribe with deterministic subscription identity, finite expiry/refresh and original lifecycle receipts. Auth encrypts callback URL and signing secrets under separate versioned keys; Workspace stores opaque callback IDs, owner/resource-bound subscriptions and receipts.
- Activate a subscription only after signed challenge verification through a qualified connection-safe HTTPS adapter. Raw callback URL/secrets never enter Workspace records, MCP results, exported workspaces or diagnostic logs.

Touch:
- Auth private callback-vault lifecycle and narrow service binding, Workspace subscription/migration/consent records, MCP2 method handlers and scope catalogue, profile/grant cleanup and offline/local Worker security fixtures.

Discovery allowance:
- Reuse existing credential/push vault encryption and cleanup patterns with separate keys and consent. Verify an actual supported callback transport before activation; official TCP socket or fetch documentation alone does not prove validated-address/TLS-host binding. Capture a required transport blocker if the deployment adapter cannot meet the contract.

Out of scope:
- Sending application reminder events, reusing Web Push consent/keys, accepting arbitrary account/desktop-thread selectors, exposing a local app-server, actual key/callback provisioning, real subscriptions and publication.

Implementation constraints:
- [Owner decision2026-10-28] Auth owns encrypted callback URL/signing secrets; Workspace owns opaque IDs, subscriptions and receipts. Require independent subscription consent, current resource access, short-lived verification challenges, expiry/refresh, replacement-secret rotation and cleanup on revoke/disconnect/delete.
- [Owner decision2026-10-29] The protected HTTPS egress gateway targets Netlify Functions Free. Preserve Auth/Workspace ownership and the existing connection-safety contract. Required Pending P-2d43941f-f8b5-489c-9467-dd2dcc7383a4 includes deployable configuration and owner setup assistance; free-quota exhaustion must not trigger a paid upgrade or an unapproved hosting fallback.
- Validate exact event/filter schemas and current principal ownership. Canonicalize subscription arguments and deduplicate same owner/callback/event identity without extending an expired or revoked authority context.
- Require HTTPS, connection-time public-destination enforcement, original-host TLS verification, bounded time/bytes, no redirects and no private/local address access for challenge and later delivery. DNS preflight followed by unconstrained fetch is insufficient; fail closed when secure connection binding is unavailable.
- Persist lifecycle changes and original IDs atomically across concurrent subscribe/refresh/unsubscribe. A challenge acknowledgment or stored callback is not proof of a rendered chat response. Preserve legacy tools, existing grants and all default-off rollout gates.

Questions and required clarifications:
- [Resolved - explicit owner answer2026-10-29] Use Netlify Functions Free for the protected Node.js HTTPS egress gateway and assist with setup during implementation. The owner requested a planning update in the current turn. Required Pending P-2d43941f-f8b5-489c-9467-dd2dcc7383a4 produces local gateway T-dc0e4117-f2a0-43c8-8d29-72e640d7bba9 and hosted qualification T-ec421773-5864-4bc5-b17f-ce04e0d818c2. Complete both recorded dependencies before implementing the parent. See skill-tree/history/validation/callback-egress/owner-decision.md.
- [Resolved - explicit owner answer] Callback URL/signing-secret ownership is Auth vault with separate consent and revocation cleanup; the prior Web Push decision is no longer used as a substitute. Protocol compatibility is a required predecessor.

Validation:
- Local Auth/Workspace/MCP2 tests cover exact-byte signed challenges, wrong echo/signature, non-public/redirect/rebinding targets, TLS hostname, denied resource access, two profiles, changed grants/connections, deterministic duplicates, concurrent refresh/unsubscribe, rotation, expiry and cleanup failure.
- Inspect exports/logs/database rows to prove raw callback credentials stay only in encrypted Auth storage. Unsupported transport/configuration keeps subscriptions inactive and emits no application data; fake transport assertions remain labelled synthetic.

Acceptance:
- Separately consented exact-resource subscriptions persist idempotently with Auth-only encrypted callback secrets, finite lifecycle and cleanup while Workspace retains only opaque callback identity and original receipts.
- No subscription activates without qualified signed challenge and connection-safe HTTPS destination controls; denied ownership, expiry, revocation, races and unsupported transport fail closed without secret leakage or a chat-delivery claim.

Documentation updates:
- Update technical/privacy/retention and originating-chat-reminders contracts, scope/consent guidance and testing.md; distinguish local security qualification from actual host callbacks.

Risks:
- A callback is both a secret-bearing delivery channel and an outbound network target. Enforce vault isolation, resource authorization and protected egress before any data transfer.

### Qualify ChatGPT, Codex, multi-user and mobile release gates

```yaml
id: T-d76d2a9a-1318-4e63-9bb4-73f620b108cc
priority: 24
status: Blocked pending answers
parent_pending_ids: ["P-29925105-1015-5ced-8844-d56fa47e33c9"]
processing_run_id: PR-a888ac19-1755-417e-8a69-4126028348c3
root_task_id: T-96866c8f-9554-55d8-a638-6d8b5afaf21c
related_task_id: T-96866c8f-9554-55d8-a638-6d8b5afaf21c
relation: subtask
depends_on: ["T-3afa6af6-cc43-4ba9-818e-5cbd48bcf17e", "T-0b3a2a66-56a8-4b0b-8f47-9fb2853d3735", "T-29ef8846-6aa7-4255-990d-a8651e689ec2", "T-34491faa-090e-4d97-b496-1fe947770688", "T-bf42656d-57e8-4a20-98b6-cb1b01aafd58", "T-159a415e-0d56-4028-96a4-339a5d5040f8", "T-8f8f1dba-9d90-4bf0-8958-fea5ede83941", "T-88a59bd6-a8d4-4cb2-9c0b-e32fecc96108", "T-b38dad4c-c3fa-431a-a429-50d6fed97650", "T-70901e97-738c-42c2-ae50-68dd71417a32", "T-65810da7-9524-45ce-b280-668d04c86441"]
```

Goal:
- Preserve and execute real multi-user and mobile release qualification before broader availability.
Current behavior:
- Local synthetic checks do not prove actual two-account host enrollment, production logs, iOS Safari/PWA or Android behavior. The selected production website and backends are published with public boundary checks. Native Codex task/account reads, Companion-only Todo lifecycle, dated Daily completion, Habit scoring and field edits pass. Hosted PWA Daily/Habit creation and Todo/Daily/Habit archival retain receipts readable through Codex. Direct native invocation of the three newly advertised execution tools awaits refreshed client discovery; background projection and complete client, multi-user and device qualification remain open.
Desired behavior:
- Owner-first checks qualify ChatGPT and Codex desktop independently against the same selected production backend, including OAuth callbacks/refresh, MCP discovery, scoped tools, original receipts and plugin/skill activation. The consenting-teammate stage verifies separate identities and revocation, alongside mobile and production-log evidence.
Touch:
- Release qualification matrix and explicitly authorized deployment/account/device lanes when supplied.
Discovery allowance:
- Prepare concrete reviewable runbooks and inspect local environment separation without publishing or enrolling.
Out of scope:
- New enrollment of another account, secret changes, real Start New Day tests, messages and device execution remain outside the selected owner Codex test sequence.
Implementation constraints:
- Owner authorization permits the explicit development capability rollout and bounded own-account Codex tests. Broader qualification remains separate from development availability.
- All implemented tools require isolation/revocation qualification. The owner development profile exposes reviewed manual Cron availability; real Start New Day tests remain excluded. Real log inspection verifies absence of secrets with minimum redacted evidence. Missing infrastructure remains a blocker.
Questions and required clarifications:
- [Unresolved - blocks implementation] E01 source P-c5795748-6f25-4c0a-a471-fe72c1a85862 requires developer-selected existing competitor sessions and actual device/human participation. The prepared local journey pack is not authenticated competitor or physical-device evidence. Retain the exact unrun checks until actual observations exist.
- [Unresolved - blocks implementation] Existing-site resources, protected secret provisioning and Worker/Pages publication are complete. The owner reports initial OAuth completion in both clients and ChatGPT reads, followed by immediate repeated reconnection. Native Codex repeated reads, approved Companion-only lifecycle commands and retained receipt reads pass. The owner explicitly reopened bounded own-account Codex tests and selected implemented development capability publication on 2026-10-19. Agent party mutations, real Start New Day tests and real key operations remain excluded. Development implementation and availability may proceed while ChatGPT, independent consenting-account, device and human usability gates remain unverified. Complete token lifecycle and revocation checks, native discovery of the added tools, background projection, packaged-skill qualification, a second consenting owner and iOS/Android device qualification remain broader release requirements. Preparation cannot satisfy real release acceptance.
Validation:
- Verify local environment/resource separation and prepare exact owner/teammate/device checks. Maintain independent ChatGPT and Codex desktop results in the plugin client matrix; direct MCP access, packaged skills and optional CLI diagnostics remain distinct checks. Execute real checks only inside separately authorized scope.
Acceptance:
- ChatGPT and Codex desktop each complete the documented owner-first pilot OAuth, scoped-tool, original-receipt, revocation and plugin/skill checks with client/version and sanitized evidence recorded; success in one client never substitutes for the other.
- Two consenting team accounts complete independent real enrollment, packaged-tool and revocation flows with no cross-profile effects.
- Real iOS Safari/PWA, Android, virtual keyboard, safe-area, screen-reader and authenticated competitor task-flow checks plus redacted production-log inspection qualify release or retain explicit failed gates.
Documentation updates:
- Deployment and release qualification matrix; preserve unrun gates in future.md.
Risks:
- Exposing live effects or claiming release success from synthetic evidence.

### Show coverage-aware Daily and Habit activity streaks

```yaml
id: T-3c84f362-0f7b-4f0c-9e23-75d9a1e0725f
priority: 99
status: Ready
parent_pending_ids: ["P-438c79fe-385d-4657-be24-85fad9a49c31"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-3c84f362-0f7b-4f0c-9e23-75d9a1e0725f
related_task_id: null
relation: main
depends_on: ["T-aa82feee-97b1-4f14-8b5a-d642cca014e3", "T-b92d23ba-8586-4869-a6b8-f8eb180f1b02"]
```

Goal:
- Present useful streak statistics with explicit limits and no invented lifetime records.
Current behavior:
- Retained completion/Habit event reads exist; statistics and history UI are planned, but current history cannot establish contiguous completeness.
Desired behavior:
- Bounded streak reads and statistics distinguish verified scheduled-day Daily runs, positive-Habit calendar-day runs and unknown/lower-bound history.
Touch:
- Workspace activity queries, shared recurrence/calendar helpers, MCP read definitions, statistics/history view models and browser components.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Use explicit UTC as-of plus the displayed IANA timezone. Habit days with at least one positive event count once; negative events remain separate and do not erase a positive day. Negative-only habits display activity frequency rather than a positive streak. Daily streaks count scheduled dates; skipped/paused intervals are neutral and missing eligible dates break a run only inside proven coverage. Current unfinished days retain the previous run until the local day closes. Historical schedules/corrections determine prior dates. Label lower bounds and refuse lifetime-best claims without complete coverage; no game effects or synthetic backfill.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- Neutral cross-runtime cases cover DST/day boundaries, monthly gaps, positive/negative events, duplicate completions, pauses/skips/corrections, history gaps and paging. Component checks verify unknown and lower-bound labels plus normal-theme chart readability.
Acceptance:
- Daily/Habit statistics match the explicit schedule/day rules and distinguish verified, lower-bound and unavailable results.
- Missing history, current-day grace or negative-only activity cannot produce fabricated complete-history or lifetime streak claims.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

### Edit supported linked Daily schedules with explicit reconciliation

```yaml
id: T-e77f6c7c-cf2f-487d-bb82-831e6f6167f9
priority: 99
status: Ready
parent_pending_ids: ["P-438c79fe-385d-4657-be24-85fad9a49c31", "P-3736aa26-c58b-402d-a736-ff6ce23907bd"]
processing_run_id: PR-662bd939-3fbc-401b-ba37-204067e17989
root_task_id: T-e77f6c7c-cf2f-487d-bb82-831e6f6167f9
related_task_id: null
relation: main
depends_on: ["T-aa82feee-97b1-4f14-8b5a-d642cca014e3", "T-68cb7da7-62e9-4136-b5c5-07da20f97c30", "T-07d27408-3019-423f-a5de-4cda1f9e2818"]
```

Goal:
- Make linked recurrence authoring available where the current Habitica schedule has a lossless supported mapping.
Current behavior:
- Canonical Daily recurrence and native authoring exist; HabiticaTaskDetails contains difficulty/attribute/checklist only, and lifecycle edits reject linked work.
Desired behavior:
- A reviewed linked Daily schedule edit displays separate Companion and Habitica semantics and records fresh verified schedule normalization.
Touch:
- Habitica task schedule parsing, Auth update allowlist/preconditions, Workspace projection engine, schedule history, recurrence editor, MCP contracts and shared fixtures.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Support only losslessly represented daily/weekly/monthly day-of-month rules. Bind fresh account timezone/dayStart and upstream frequency/everyX/repeat/startDate/daysOfMonth facts; unsupported yearly, weeks-of-month and mixed rules remain explicitly read-only rather than discarded. Preserve actual Habitica normalized fields and record schedule epochs. Preview current/next dates and warn that native pause/skip does not pause Habitica penalties. Do not change native-only pause into an upstream resting setting, auto-score past dates, run Cron or dispatch while outcome is unknown. Revalidate official controller/update semantics before editing the allowlist.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- Synthetic gateway and cross-runtime fixtures cover supported round trips, default weekdays, monthly gaps, timezone/dayStart differences, external edits, stale previews, unsupported fields, lost acknowledgement and absence of score/Cron effects.
Acceptance:
- Supported exact linked schedule edits preserve representable rules and retain original receipts plus effective-dated schedule facts.
- Unsupported or uncertain schedules remain clearly unavailable without field loss, penalty promises, backfilled scores or implicit account changes.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

### Convert task types through reviewed linked copies

```yaml
id: T-70251ccf-7d6a-4e89-b4d4-98cd15c9d528
priority: 99
status: Ready
parent_pending_ids: ["P-438c79fe-385d-4657-be24-85fad9a49c31"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-70251ccf-7d6a-4e89-b4d4-98cd15c9d528
related_task_id: null
relation: main
depends_on: ["T-2355607b-ba77-46c0-9495-aae61e16a8f0", "T-ea3c2df0-7c13-4a77-a920-1451801b9946"]
```

Goal:
- Offer task-type changes without corrupting retained event semantics or Habitica mappings.
Current behavior:
- Current commands create explicit kinds and history validation compares accepted snapshot kind with current task kind; in-place conversion would invalidate prior activity.
Desired behavior:
- Conversion creates a new native target-kind task with explicit lineage and a preview of copied/reset fields. The original remains intact unless a separate supported native archive is selected.
Touch:
- Workspace typed commands/identity, portable lineage validation, command engine and Worker parity, browser capture/editor and MCP descriptions.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Default to a new unlinked copy; never rewrite historical task kind, transfer mappings/history, create a Habitica task automatically or silently archive linked work. Copy name/notes/priority and valid chosen organization; require explicit target Daily recurrence, reset kind-specific state/stage/occurrence fields, and show all losses. The optional native archive is allowed only with available references and no unresolved effects. One reviewed atomic command binds source revision, target identity and field choices; repeated acceptance retains the same target. Original Habitica records remain unchanged and separate explicit linking remains available.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- Domain/Worker/bUnit cases cover all Todo/Daily/Habit pairs, historical and linked sources, invalid recurrence, organization references, stale source, uncertain commands, duplicate acceptance and export/import lineage.
Acceptance:
- Type conversion creates exactly one reviewed native target with retained lineage and an accurate copied/reset-field preview.
- Original history/kind/mappings and upstream records remain intact; stale or uncertain acceptance cannot duplicate the target or silently archive linked work.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

### Evaluate saved lists with explicit relative-date predicates

```yaml
id: T-916deee7-8be7-44aa-9cd2-b9d86e90e56d
priority: 99
status: Ready
parent_pending_ids: ["P-9dbdf081-4b8e-495c-a8fc-5c94c34d3a78"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-916deee7-8be7-44aa-9cd2-b9d86e90e56d
related_task_id: null
relation: main
depends_on: ["T-66aec83d-1d40-4c42-87ec-6566e1ac3769"]
```

Goal:
- Keep Today/overdue/upcoming saved lists useful after dates advance.
Current behavior:
- SavedTaskPredicates stores fixed DateOnly ranges; saved-view evaluation delegates the fixed query to canonical filters.
Desired behavior:
- Optional typed relative predicates evaluate from one explicit clock/timezone context per query and return the evaluated bounds.
Touch:
- WorkspaceSavedTaskView models/validators, query identity, organization save/history, C#/Worker read evaluators, saved-list editor and MCP read results.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Adopt presets Today, BeforeToday, NextSevenDays inclusive of today, ThisWeek Monday through Sunday and NoDate. Planned and deadline predicates remain separate and cannot silently override an absolute range on the same field. Store relative intent, not a frozen date. Capture one UTC as-of and explicit IANA timezone; bind pagination to the document revision and evaluation-day context. Clock/day changes invalidate old cursors. Reads remain side-effect free and unavailable timezones fail explicitly. Existing absolute saved lists remain unchanged.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- Neutral C#/Worker fixtures cover midnight, DST, leap dates, week/year boundaries, absent dates, conflicting filters, day-changed paging, identity/export and no read mutations. Editor checks show interpreted date ranges.
Acceptance:
- Relative saved definitions retain intent across reload and evaluate matching bounded results with an explicit clock/timezone context.
- Invalid combinations, unsupported timezone and stale evaluation cursors fail without changing definitions or duplicating canonical tasks.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

### Security Hardening, Revocation, Key Rotation, And Team Rollout

```yaml
id: T-96866c8f-9554-55d8-a638-6d8b5afaf21c
priority: 24
status: Ready
parent_pending_ids: ["P-0ea957c0-5210-5931-b545-82b9f13197db"]
processing_run_id: null
root_task_id: T-96866c8f-9554-55d8-a638-6d8b5afaf21c
related_task_id: null
relation: main
depends_on: ["T-3987ba88-f69f-59f1-811e-f51cabfa5978", "T-5737120b-52a5-4a57-9072-fce514adaf33", "T-642c474f-619b-41e0-bc07-d1c72fbcf4d4", "T-fd23e672-62b7-4dac-b0c4-d8ba321aa2ca", "T-3afa6af6-cc43-4ba9-818e-5cbd48bcf17e", "T-d76d2a9a-1318-4e63-9bb4-73f620b108cc", "T-4dd09108-6149-4ab2-9326-b6f53e05f811"]
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
- Settings and Auth implement linking, disconnect/profile deletion, token cleanup and stable re-enrollment identity. Workspace implements typed task/party/spell safety and own-data controls. Separate grant-only revocation, complete status/rotation controls, operational protection and real release qualification remain required hardening work.

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
- [Resolved - owner decision] The owner selected all implemented tools with live Cron off on 2026-10-16, then selected development capability availability on 2026-10-19; real Start New Day tests remain excluded. Record every included capability as a required T- dependency before hardening and rollout validation. E14 adoption and migration gates remain mandatory. The concrete choices and required dependency units reside in `companion-workspace-contract.md#proposed-hardening-and-release-scope`.
- [Resolved - owner decision] Local qualification covers all implemented tools; live rollout remains separately scoped.
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

### Add project-filtered retained task history navigation

```yaml
id: T-16e1b491-18c3-498e-82da-81a97ff1294a
priority: 25
status: Ready
parent_pending_ids: ["P-762549ef-d24d-4caa-ab3a-573a8e60eb21"]
processing_run_id: PR-662bd939-3fbc-401b-ba37-204067e17989
root_task_id: T-16e1b491-18c3-498e-82da-81a97ff1294a
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Add exact project filtering and direct project-to-history navigation while reusing completed history and recovery.
Current behavior:
- Completion and Habit history already support exact task, text and UTC dates with safe restoration. Queries and TodoHistoryPanel lack a project filter; statistics and extended recovery have separate existing Ready tasks.
Desired behavior:
- Project-filtered completion/Habit history is reachable and truthful about current versus historical placement, with all existing safe recovery semantics retained.
Touch:
- WorkspaceCompletionHistory/WorkspaceHabitHistory filters and parity models, Worker history reads/MCP schemas, TodoHistoryPanel, Projects entrypoints and retained-history skill.
Discovery allowance:
- Inspect adjacent serializers, permission checks, adapters and affected tests. Expand the selected scope only for required dependencies captured through the active controller.
Out of scope:
- Unrelated features, implicit account consent, publication and live account changes without the applicable owner scope.
Implementation constraints:
- Define project filtering as current retained canonical task assignment, labelled clearly rather than claiming event-time membership when snapshots lack placement. Include archived/removed project identities and completed archived tasks; preserve accepted event text/time provenance. Distinguish current Unassigned from a removed historical project selector. Combine text/project/UTC filters with AND, stable revision-bound paging and full counts. Existing exact-event restoration and linked/uncertain refusal remain unchanged. Reuse the planned statistics and extended-recovery tasks instead of implementing duplicate pages or unsafe undo.
Questions and required clarifications:
- [Resolved - repository and owner evidence] The owner request and inspected implementation establish the requested behavior. Source evidence and remaining validation limits are recorded in skill-tree/history/validation/pending-processing-research.json.
Validation:
- Cross-runtime/component tests cover two projects, project reassignment after completion, archived/removed references, Unassigned, completed archives, duplicate names, text/date intersections, paging, stale revision, original restore receipt and project-to-history navigation.
Acceptance:
- Project-filtered completion/Habit history is reachable and truthful about current versus historical placement, with all existing safe recovery semantics retained.
Documentation updates:
- Update the relevant feature, technical, API, testing and UX contracts for delivered behavior. Keep actual qualification separate from unrun host, device and live checks.
Risks:
- Stale identity, partial acceptance or ambiguous outcome can duplicate effects or misstate saved state. Preserve original command identity and explicit recovery.

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

Current context:
- `/party` and `/quests` already separate member/CRON overview from the quest workspace through `QuestsPage.razor` and `PartyPage.QuestWorkspaceOnly`. The quest page still stacks current quest, queue, pool and recent completions; dedicated modes and mode-switch acceptance coverage remain outstanding.

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

Current context:
- `PartyPage.razor` renders the eight latest shared completion cards and uses completion history for queue penalties. Aggregate completions by quest/owner, coverage-window disclosure and sparse-history analytics remain outstanding.

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

Current context:
- Equipment presets, recommendation equip flows and bounded spell casting already exist in `InventoryPage.razor`, `SpellsPage.razor` and `AppSessionController`. No saved declarative macro collection, step compiler or persisted per-step execution ledger exists; reuse the existing equipment/spell operations.

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

Current context:
- `TasksPage.razor` already has inline score/checkoff controls and progress; `AppSessionController.ScoreTaskAsync` freshness-gates and sequentially executes scores. Pre-action outcome summaries and exact/estimated/unavailable result labeling remain outstanding.

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

Current context:
- The Clear Local Data button directly calls `HandleClearLocalDataAsync`, which immediately awaits `ClearLocalDataAsync`. A confirmation/cancel state and confirmation-specific tests remain outstanding.

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

Current context:
- Dashboard, Tasks, Party, Equipment and Spells render introductory help before authentication/cache branching. Existing folded appearance/equipment panels do not implement returning-user hero/help compression.

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

Current context:
- `.stats-table-wrap` supports horizontal overflow and `.stats-table` has a wide minimum width. Table row labels have no sticky-first-column or repeated-label mechanism; existing sticky rules apply to the spell mana bar and diagnostics console.

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

Current context:
- Spell cards already expose target/count, mana preview, cast controls, progress, warnings and equipment recommendations. Description, effect preview and recommendation rationale remain expanded; explanatory-copy disclosure and density-specific regression coverage remain outstanding.

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
