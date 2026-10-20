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

### Default the equipment optimizer to Intelligence for every class

```yaml
id: P-4b315ee2-e2b4-4fa0-82fb-1c43e7336a8e
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
- Use Intelligence as the initial equipment optimization goal in the inventory Gear Planning / Equipment optimizer section for every character class.

Expected behavior:
- Opening the optimizer recommends an Intelligence-focused set by default. Explicit user-selected goals and spell-specific equipment choices remain available; selecting a default never equips gear automatically.

Current context:
- InventoryPage initializes _optimizationGoal and invalid-selection fallback to Balanced. The owner prefers Intelligence for routine gameplay across classes and different equipment only for explicit spell workflows. Formula claims and the exact handling of remembered goals require Process Pending research.

Source:
- Owner request on 2026-10-19; InventoryPage.razor equipment-optimizer-goal control and _optimizationGoal default.

## Prioritized Next Changes

Highest numeric priority first; unmet dependencies and blocking questions stop implementation. Imported source provenance and product decisions remain distinct from newly executed research.

### Complete safe project retirement and agent project lifecycle

```yaml
id: T-264e3162-adc3-45f0-943e-a55feccc9d8e
priority: 100
status: Ready
parent_pending_ids: ["P-cea6ca25-8602-4b3a-8a4c-4a9c348dff85"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-264e3162-adc3-45f0-943e-a55feccc9d8e
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Close project lifecycle gaps without deleting contained tasks.
Current behavior:
- Independent WorkspaceProject records and native create/update/archive organization commands already exist with organization-write scopes and no task cascade. Project deletion is absent; client-exposed tool discovery may be stale. Planned browser project workflows already cover creation, rename, placement and archive.
Desired behavior:
- Expose the existing project lifecycle consistently to agent clients and add a reviewed safe logical deletion/removal command with retained task/history references.
Touch:
- Organization models/commands and history validation in C#/TypeScript, Workspace organization API, MCP schemas/package contracts, project task queries and shared fixtures.
Discovery allowance:
- Inspect affected implementation, fixtures, owned documentation and current official platform references; capture required additional work before changing unrelated contracts.
Out of scope:
- Unrelated refactors, implicit user consent, real account mutations, production publication and device qualification outside explicit owner scope.
Implementation constraints:
- Reuse existing exact project identities and independent organization-write scope. Adopt logical deletion with retained audit tombstone rather than destructive row removal. Explicit deletion review lists affected project/section references and preserves every task, completion, receipt and Habitica mapping. Define a consistent Unassigned presentation without silently completing/moving descendants. Preserve one primary project, cross-project section validity and arbitrary-depth parent links. No cascading deletion or upstream project object.
Questions and required clarifications:
- [Resolved - developer request and repository evidence] Adopt the bounded behavior described in the selected contract. Platform-owned permission and selection controls remain authoritative.
Validation:
- C#/TypeScript shared command/history fixtures cover create/read/rename/archive/remove, task reassignment, original receipts, missing/stale IDs, cross-account denial, existing sections and parent links, tombstone export/import and zero Habitica effects. MCP discovery tests enumerate actual supported project commands.
Acceptance:
- Agent clients can discover and execute exact authorized first-class project lifecycle commands with original receipt recovery.
- Reviewed project removal preserves tasks, hierarchy, history and game mappings with passing parity/isolation tests.
Documentation updates:
- Update features.md, testing.md and applicable technical.md, UX and agent integration contracts; preserve explicit unverified host/device limitations.
Risks:
- Stale account, identity, subscription or revision must not change unrelated tasks, deliver private information to another account or replay uncertain effects.

### Qualify automatic Companion discovery and invocation

```yaml
id: T-9dce9211-4c9d-478a-952c-12adba3e013f
priority: 100
status: Ready
parent_pending_ids: ["P-4db3bb11-31fa-4cac-ab7b-7841cd79ae11"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-9dce9211-4c9d-478a-952c-12adba3e013f
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Determine realistic automatic selection behavior in ChatGPT.
Current behavior:
- Current local MCP task/project schemas and installed client discovery differ; official OpenAI metadata guidance describes direct/indirect/negative prompt evaluation rather than guaranteed invocation in every conversation.
Desired behavior:
- Document installation, authorization, discovery and tool selection separately; build a reproducible prompt set and improve exact tool metadata/skill routing where supported.
Touch:
- MCP tool descriptions, plugin package/skills, integration setup/research docs and synthetic discovery fixtures.
Discovery allowance:
- Inspect affected implementation, fixtures, owned documentation and current official platform references; capture required additional work before changing unrelated contracts.
Out of scope:
- Unrelated refactors, implicit user consent, real account mutations, production publication and device qualification outside explicit owner scope.
Implementation constraints:
- Use https://developers.openai.com/plugins/guides/optimize-metadata and current official client guidance. Include new/existing chats, explicit product names, natural task intent and negative unrelated prompts. Do not confuse reconnect/token failures with selection. Do not force global app access, spoof read-only annotations or promise all-chat invocation. Live ChatGPT tests require a separately selected host session; synthetic checks must remain labeled.
Questions and required clarifications:
- [Resolved - developer request and repository evidence] Adopt the bounded behavior described in the selected contract. Platform-owned permission and selection controls remain authoritative.
Validation:
- Metadata/schema regression fixtures plus a documented host evaluation table record selection, available tools, arguments, permissions and result. Provide manual app selection and connection-repair alternatives where automatic selection is unavailable.
Acceptance:
- A sourced capability decision and reproducible direct/indirect/negative prompt set define automatic selection limits.
- Supported metadata/package improvements preserve honest tool annotations and authorization; live host outcomes remain distinct from synthetic results.
Documentation updates:
- Update features.md, testing.md and applicable technical.md, UX and agent integration contracts; preserve explicit unverified host/device limitations.
Risks:
- Stale account, identity, subscription or revision must not change unrelated tasks, deliver private information to another account or replay uncertain effects.

### Expose iPhone PWA reminder setup and recovery

```yaml
id: T-1f1eb5fd-febd-44fc-b475-69121e11400a
priority: 100
status: Ready
parent_pending_ids: ["P-fcbb5693-fdf8-408d-8f3b-361f3cea5036"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-1f1eb5fd-febd-44fc-b475-69121e11400a
related_task_id: null
relation: main
depends_on: ["T-ce099b7b-f9cc-49b8-aadd-04b5623b1094", "T-ea3c2df0-7c13-4a77-a920-1451801b9946", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Make push reminder permissions, schedules and delivery limits understandable.
Current behavior:
- No task-level push editor, per-device subscription settings or notification-click handler exists.
Desired behavior:
- Provide task reminder creation/edit/cancel, device subscribe/unsubscribe and clear iPhone installation/permission recovery with safe task deep links.
Touch:
- Todo settings, task reminder UI, notification interop, service-worker push and notificationclick handlers and rendered fixtures.
Discovery allowance:
- Inspect affected implementation, fixtures, owned documentation and current official platform references; capture required additional work before changing unrelated contracts.
Out of scope:
- Unrelated refactors, implicit user consent, real account mutations, production publication and device qualification outside explicit owner scope.
Implementation constraints:
- Reuse canonical reminder/subscription state. Explain denied permission and standalone prerequisites without repeated prompts. Show per-device state and delivery limits; disabled schedules remain visible. Keep task bodies out of lock-screen defaults. Notification clicks validate same-origin routes and reauthenticate before revealing private task content. No background gameplay or silently enabled subscriptions.
Questions and required clarifications:
- [Resolved - developer request and repository evidence] Adopt the bounded behavior described in the selected contract. Platform-owned permission and selection controls remain authoritative.
Validation:
- Component/browser fixtures cover grant/deny/unsupported, one-time/repeat edits, subscribe/unsubscribe, schedule failure/recovery, offline click/login and focus return. Qualify mobile/200-percent/theme cases. Track actual physical iPhone closed-app delivery in release qualification.
Acceptance:
- Users can explicitly configure and revoke task/device push reminders with accurate prerequisites and delivery state.
- Notification-click routing stays same-origin and authenticated; denied/offline/mobile states pass synthetic interaction and layout checks.
Documentation updates:
- Update features.md, testing.md and applicable technical.md, UX and agent integration contracts; preserve explicit unverified host/device limitations.
Risks:
- Stale account, identity, subscription or revision must not change unrelated tasks, deliver private information to another account or replay uncertain effects.

### Dispatch durable scheduled Web Push reminders

```yaml
id: T-ce099b7b-f9cc-49b8-aadd-04b5623b1094
priority: 100
status: Ready
parent_pending_ids: ["P-fcbb5693-fdf8-408d-8f3b-361f3cea5036"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-ce099b7b-f9cc-49b8-aadd-04b5623b1094
related_task_id: null
relation: main
depends_on: ["T-0e166dfc-1eb0-42f7-ba4d-5949f476d31b", "T-b05e848e-9bb0-461c-aee0-8c4293fcbeb2"]
```

Goal:
- Deliver opted-in reminder intents while the app is closed.
Current behavior:
- Foreground reminder cues are planned; no persisted push schedule/delivery ledger or server sender exists.
Desired behavior:
- Persist one-time and repeating civil-time reminder intents, dispatch through a bounded server scheduler and retain delivery outcomes independently of task state.
Touch:
- Workspace reminder model, D1 migration and scheduling/dispatch module, Web Push signing/encryption integration, quota/error handling and deployment instructions.
Discovery allowance:
- Inspect affected implementation, fixtures, owned documentation and current official platform references; capture required additional work before changing unrelated contracts.
Out of scope:
- Unrelated refactors, implicit user consent, real account mutations, production publication and device qualification outside explicit owner scope.
Implementation constraints:
- Depend on explicit valid subscriptions. Reuse task/review cue identities, explicit timezone rules and DST policy; record schedule revision and occurrence identity. Dedupe application dispatch intent, expire stale cues, stop revoked subscriptions, handle 404/410 removal and 429/Retry-After with bounded retries. Distinguish accepted-by-push-service from shown/read; no exactly-once guarantee or automatic Habitica action. No timer-based upstream polling. Keep live scheduler activation and VAPID provisioning explicit.
Questions and required clarifications:
- [Resolved - developer request and repository evidence] Adopt the bounded behavior described in the selected contract. Platform-owned permission and selection controls remain authoritative.
Validation:
- Fake-clock and push-transport tests cover closed-app schedule dispatch, one-time/repeating/DST boundaries, duplicate scheduler invocations, changed/cancelled schedules, revoked profiles/devices, expired cues, cooldowns, failed acknowledgements and bounded retry.
Acceptance:
- Durable one-time and recurring reminder dispatch obeys consent, timezone, schedule revision, dedupe and quota controls.
- Receipts distinguish scheduled, dispatched, accepted, refused and unknown delivery without task/game mutation or a false delivered/read guarantee.
Documentation updates:
- Update features.md, testing.md and applicable technical.md, UX and agent integration contracts; preserve explicit unverified host/device limitations.
Risks:
- Stale account, identity, subscription or revision must not change unrelated tasks, deliver private information to another account or replay uncertain effects.

### Persist explicit account-bound Web Push subscriptions

```yaml
id: T-0e166dfc-1eb0-42f7-ba4d-5949f476d31b
priority: 100
status: Ready
parent_pending_ids: ["P-fcbb5693-fdf8-408d-8f3b-361f3cea5036"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-0e166dfc-1eb0-42f7-ba4d-5949f476d31b
related_task_id: null
relation: main
depends_on: ["T-51ce8719-0131-46d3-882a-4219d1bef929"]
```

Goal:
- Establish an opt-in private Web Push subscription lifecycle.
Current behavior:
- The PWA has a service worker but no push handler, accepted subscription API or stored push permission contract. Foreground reminder work explicitly excludes background push.
Desired behavior:
- Provide authenticated own-account subscribe/status/unsubscribe, per-device identity, minimal encrypted endpoint/key storage and independent reminder consent.
Touch:
- Existing Workspace Worker API/storage migrations, C# client/notification adapter, browser service worker, secret configuration and privacy contract.
Discovery allowance:
- Inspect affected implementation, fixtures, owned documentation and current official platform references; capture required additional work before changing unrelated contracts.
Out of scope:
- Unrelated refactors, implicit user consent, real account mutations, production publication and device qualification outside explicit owner scope.
Implementation constraints:
- Use supported installed iOS/iPadOS Home Screen PWA capability detection, explicit browser gesture and userVisibleOnly. Notification consent is separate from cloud workspace and ChatGPT enrollment. Do not place endpoints, keys or task text in operational logs. Validate bounded HTTPS public push destinations and refuse redirects/private endpoints. Provision VAPID secrets outside source. Adopt task-neutral default notification text; opening the app resolves private task details. Support revocation and account/device isolation without deleting tasks.
Questions and required clarifications:
- [Resolved - developer request and repository evidence] Adopt the bounded behavior described in the selected contract. Platform-owned permission and selection controls remain authoritative.
Validation:
- Synthetic authenticated API/storage and browser PushManager fixtures cover grant/deny/unsupported states, duplicate subscription identity, account mismatch, endpoint/SSRF rejection, expired/revoked subscriptions, redaction and unsubscribe. Verify migration and portable data/export/deletion behavior.
Acceptance:
- Explicit per-device Web Push subscription lifecycle preserves account isolation and separate consent with safe storage and endpoint validation.
- No permission request occurs on page load and no notification endpoint/key enters logs or source-controlled configuration.
Documentation updates:
- Update features.md, testing.md and applicable technical.md, UX and agent integration contracts; preserve explicit unverified host/device limitations.
Risks:
- Stale account, identity, subscription or revision must not change unrelated tasks, deliver private information to another account or replay uncertain effects.

### Determine supported originating-chat reminder delivery

```yaml
id: T-6a8db6be-b721-4b88-b62b-c6566d759080
priority: 100
status: Ready
parent_pending_ids: ["P-4dfb12ea-3f5d-4e8e-b178-a02a02571e63"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-6a8db6be-b721-4b88-b62b-c6566d759080
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Establish a supported path for service-initiated Companion reminders in agent clients.
Current behavior:
- Companion MCP uses the installed SDK and has no event subscription dispatcher or verified conversation callback mapping. Official OpenAI MCP Events now documents discovery/subscription/signed webhook delivery; Codex app-server provides authenticated thread resume and turn start, without proving access to an existing desktop-owned thread.
Desired behavior:
- Produce a current capability matrix and bounded synthetic prototype for supported host events or an explicit sourced limitation with a PWA notification/in-app alternative.
Touch:
- Integration research documentation, plugin/MCP capability schemas, local protocol fixtures and optional prototype module isolated from live dispatch.
Discovery allowance:
- Inspect affected implementation, fixtures, owned documentation and current official platform references; capture required additional work before changing unrelated contracts.
Out of scope:
- Unrelated refactors, implicit user consent, real account mutations, production publication and device qualification outside explicit owner scope.
Implementation constraints:
- Inspect official https://developers.openai.com/plugins/build/mcp-events and https://learn.chatgpt.com/docs/app-server . Distinguish an event accepted by a host from a rendered reply in the originating chat. Verify subscription owner, conversation binding exposure, callback challenge/signature, endpoint security, expiry, revocation and dedupe. Never invent a chat ID, expose a local app-server publicly, scrape private APIs or send messages to actual chats during synthetic research. Capture supported implementation as a separately bounded task.
Questions and required clarifications:
- [Resolved - developer request and repository evidence] Adopt the bounded behavior described in the selected contract. Platform-owned permission and selection controls remain authoritative.
Validation:
- Record source dates and exact supported/unsupported client features; exercise local fake subscription/event or app-server binding only where a documented contract exists. Include forged owner, invalid endpoint/signature, expired subscription and duplicate-event cases in a supported prototype.
Acceptance:
- A sourced ChatGPT/Codex capability matrix distinguishes host event delivery, originating-chat binding and actual message rendering.
- A reproducible local prototype or documented platform limitation includes security requirements and an actionable UX alternative.
Documentation updates:
- Update features.md, testing.md and applicable technical.md, UX and agent integration contracts; preserve explicit unverified host/device limitations.
Risks:
- Stale account, identity, subscription or revision must not change unrelated tasks, deliver private information to another account or replay uncertain effects.

### Complete iPhone PWA installation and relaunch support

```yaml
id: T-51ce8719-0131-46d3-882a-4219d1bef929
priority: 100
status: Ready
parent_pending_ids: ["P-02b78b3e-3fe9-4871-b019-c6baebafe810"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-51ce8719-0131-46d3-882a-4219d1bef929
related_task_id: null
relation: main
depends_on: ["T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Make existing PWA packaging and installation discoverable and correct.
Current behavior:
- manifest.webmanifest already declares standalone, id/start_url and 192/512 icons but uses Habitica.WebApp naming. index.html has apple-touch icons; published service worker caches application assets. AppSessionController reloads explicitly persisted credentials.
Desired behavior:
- Use Companion product identity, valid install assets and visible Safari installation instructions. Preserve existing credential persistence choice and route/session recovery on standalone relaunch.
Touch:
- PWA manifest, index.html and icons, published service worker, installation guidance surface, AppSessionController and packaging/session tests.
Discovery allowance:
- Inspect affected implementation, fixtures, owned documentation and current official platform references; capture required additional work before changing unrelated contracts.
Out of scope:
- Unrelated refactors, implicit user consent, real account mutations, production publication and device qualification outside explicit owner scope.
Implementation constraints:
- Do not introduce another application shell or assume Safari tab storage transfers to a standalone installation. Require explicit sign-in when storage/session is absent; never silently persist session-only credentials. Check service-worker navigation against auth/privacy/api routes and retain network-only sensitive traffic. Real iPhone installation, safe-area/keyboard, offline/relaunch and session behavior remain separate release evidence.
Questions and required clarifications:
- [Resolved - developer request and repository evidence] Adopt the bounded behavior described in the selected contract. Platform-owned permission and selection controls remain authoritative.
Validation:
- Release packaging assertions validate manifest/icon dimensions/registration and sensitive-route cache boundaries. Synthetic navigation/session tests cover persisted and session-only choices, signed-out relaunch and unavailable storage. Add reproducible physical iPhone steps without claiming a fixture proves device behavior.
Acceptance:
- Companion install metadata, icon assets and Safari Add to Home Screen guidance pass packaging and interaction checks.
- Synthetic relaunch and cache-boundary tests preserve explicit credential choices; actual iPhone qualification remains visibly tracked by the release gate.
Documentation updates:
- Update features.md, testing.md and applicable technical.md, UX and agent integration contracts; preserve explicit unverified host/device limitations.
Risks:
- Stale account, identity, subscription or revision must not change unrelated tasks, deliver private information to another account or replay uncertain effects.

### Build accessible nested Todo task editing

```yaml
id: T-eb745ca4-9da7-4fef-8c5f-13f115b4c22a
priority: 100
status: Ready
parent_pending_ids: ["P-c565f05b-622d-450d-8b0f-6c5917163d7e"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-eb745ca4-9da7-4fef-8c5f-13f115b4c22a
related_task_id: null
relation: main
depends_on: ["T-8d5444d6-8dec-4395-ae9d-5ea6fa6d8856", "T-ea3c2df0-7c13-4a77-a920-1451801b9946", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Make complete descendant tasks usable from task and project views.
Current behavior:
- Canonical parent relationships exist, but current task lists and the planned organization surface do not supply a complete expandable tree journey.
Desired behavior:
- Create and edit descendants, move or detach exact tasks, expand/collapse levels and navigate back to the parent from phone and keyboard.
Touch:
- Todo task components, project workspace, workspace task controller/query adapters and browser qualification fixtures.
Discovery allowance:
- Inspect affected implementation, fixtures, owned documentation and current official platform references; capture required additional work before changing unrelated contracts.
Out of scope:
- Unrelated refactors, implicit user consent, real account mutations, production publication and device qualification outside explicit owner scope.
Implementation constraints:
- Reuse hierarchy-query coverage and existing create/edit commands. Preserve independent status, notes and dates per descendant. Limit visible rows/render work without a fixed schema depth restriction; avoid increasing indentation beyond usable mobile width. Provide explicit move actions, cycle feedback, parent context for filtered/paged results and non-drag alternatives. No cascading completion, deletion or implicit project move.
Questions and required clarifications:
- [Resolved - developer request and repository evidence] Adopt the bounded behavior described in the selected contract. Platform-owned permission and selection controls remain authoritative.
Validation:
- Component journeys cover multi-level creation/edit/move/collapse, keyboard focus, same names, cycles, hidden/paged descendants and partial failure. Qualify narrow widths, long labels, 200-percent text and readable themes.
Acceptance:
- Tasks and Projects expose working arbitrary-depth descendant journeys with usable mobile and keyboard controls.
- Tree operations preserve independent task lifecycle and original receipts with passing interaction and layout evidence.
Documentation updates:
- Update features.md, testing.md and applicable technical.md, UX and agent integration contracts; preserve explicit unverified host/device limitations.
Risks:
- Stale account, identity, subscription or revision must not change unrelated tasks, deliver private information to another account or replay uncertain effects.

### Expose stable arbitrary-depth Todo hierarchy queries

```yaml
id: T-8d5444d6-8dec-4395-ae9d-5ea6fa6d8856
priority: 100
status: Ready
parent_pending_ids: ["P-c565f05b-622d-450d-8b0f-6c5917163d7e"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-8d5444d6-8dec-4395-ae9d-5ea6fa6d8856
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Reuse native task parent relationships for complete task hierarchies.
Current behavior:
- ParentTaskId, relateTodo, per-task lifecycle and iterative combined cycle validation already exist in C# and TypeScript. Flat queries expose records; no dedicated tree read or complete subtree move contract exists.
Desired behavior:
- Provide account-bound hierarchy read/traversal and exact reparenting using existing independent task identities, without a fixed product depth restriction.
Touch:
- WorkspaceTaskRelationships.cs, WorkspaceRules.cs, workers/shared/workspace-domain.ts and task command/query modules, MCP schemas and shared hierarchy fixtures.
Discovery allowance:
- Inspect affected implementation, fixtures, owned documentation and current official platform references; capture required additional work before changing unrelated contracts.
Out of scope:
- Unrelated refactors, implicit user consent, real account mutations, production publication and device qualification outside explicit owner scope.
Implementation constraints:
- Use iterative traversal and explicit workload/pagination limits; arbitrary depth does not imply unbounded response size. Todo hierarchy remains separate from checklist and dependency completion. Return parent links, depth and pagination coverage without orphaning omitted ancestors. Reparenting never completes descendants or moves project membership implicitly. Missing/archived references retain clear status. Keep existing combined parent/dependency cycle rejection and unrelated task kinds unchanged.
Questions and required clarifications:
- [Resolved - developer request and repository evidence] Adopt the bounded behavior described in the selected contract. Platform-owned permission and selection controls remain authoritative.
Validation:
- C#/TypeScript parity fixtures cover at least 2000 parent levels, pagination boundaries, cycles/self-parent, missing or archived parents, reparent/detach, same-name tasks, cross-account isolation and lost response recovery.
Acceptance:
- Hierarchy queries and exact reparent commands support deep Todo chains without recursive stack exhaustion or a fixed depth cap.
- Hierarchy changes preserve task history, task identity, project membership, dependency semantics and zero implicit game effects.
Documentation updates:
- Update features.md, testing.md and applicable technical.md, UX and agent integration contracts; preserve explicit unverified host/device limitations.
Risks:
- Stale account, identity, subscription or revision must not change unrelated tasks, deliver private information to another account or replay uncertain effects.

### Execute explicitly requested task edits without redundant agent approval

```yaml
id: T-a7c06616-38a3-4cfb-8f2d-4ee41eab6b4c
priority: 100
status: Ready
parent_pending_ids: ["P-203bd87e-c49c-447b-913d-b322d6bdf0b2"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-a7c06616-38a3-4cfb-8f2d-4ee41eab6b4c
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Remove redundant conversational approval for explicit ordinary task create/edit intent.
Current behavior:
- MCP task descriptions mandate a second explicit preview approval for every create/update; durable prepared commands and request identity already exist in task-tools.ts and task-api.ts.
Desired behavior:
- Preserve internal prepare/accept validation while allowing the initiating explicit create/edit request to authorize the exact ordinary task action. Return saved state plus independent projection receipt.
Touch:
- workers/chatgpt-mcp/src/task-tools.ts, shared task-tool contracts, Companion agent skill/package guidance, Workspace prepared-command tests and integration documentation.
Discovery allowance:
- Inspect affected implementation, fixtures, owned documentation and current official platform references; capture required additional work before changing unrelated contracts.
Out of scope:
- Unrelated refactors, implicit user consent, real account mutations, production publication and device qualification outside explicit owner scope.
Implementation constraints:
- Do not relabel writes as read-only or remove authorization, independent scopes, revision checks or durable previews. The initial request covers only matching fields and task identity. Ambiguity requires clarification. Deletion, scoring, completion, spells, Cron, bulk operations and unexpected game effects retain applicable review. Host UI safeguards cannot be overridden or guaranteed absent. Reuse the original command after unknown acceptance; never recreate the task.
Questions and required clarifications:
- [Resolved - developer request and repository evidence] Adopt the bounded behavior described in the selected contract. Platform-owned permission and selection controls remain authoritative.
Validation:
- Synthetic MCP explicit-create/edit journeys cover accepted and duplicate commands, unchanged scope enforcement, ambiguous names, stale revisions, lost acknowledgement and independent saved/projection status. Metadata/guidance fixtures assert ordinary intent versus consequential-action boundaries.
Acceptance:
- Explicit ordinary task create/edit guidance permits one initiating request without a mandatory additional agent approval exchange.
- Original command identity, scope/revision validation, failure recovery and consequential-action boundaries remain covered by passing tests.
Documentation updates:
- Update features.md, testing.md and applicable technical.md, UX and agent integration contracts; preserve explicit unverified host/device limitations.
Risks:
- Stale account, identity, subscription or revision must not change unrelated tasks, deliver private information to another account or replay uncertain effects.

### Build direct title-first task capture and editing

```yaml
id: T-ea3c2df0-7c13-4a77-a920-1451801b9946
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-0ddc8784-c740-5a23-819c-b17db8f6829f"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-ea3c2df0-7c13-4a77-a920-1451801b9946
related_task_id: null
relation: main
depends_on: ["T-9f465413-725a-440e-a464-cb5c555cb87d", "T-6122dded-900d-4d92-8739-bbc344e15ea0", "T-2562b775-dd9b-43b4-b66f-9de9ee5eee07"]
```

Goal:
- Make capture and ordinary task edits immediately usable without opening a management console.
Current behavior:
- CompanionTasksPanel contains create/edit commands but is hidden behind Manage Companion tasks and exposes many fields at once. WorkspaceTaskInput and the canonical engine already support reviewed basic task operations.
Desired behavior:
- A reusable category-aware capture/editor starts with a title and exposes advanced fields on demand; completion and Habit scoring remain direct visible actions.
Touch:
- Reusable task rows/editor components, WorkspaceTaskController inputs, existing command previews and component tests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Default to native Companion persistence with explicit source and no requested game projection. Support Todo/Daily/Habit creation with clear kind labels, notes, urgency, separate planned date/deadline and relevant game details. Preserve exact reviewed command fields, original IDs, draft state, receipt recovery, keyboard entry/return focus and unavailable reasons. Optional advanced details must not hide the primary create/complete/score action. Destructive archival and requested game effects retain concrete review; no implicit live dispatch.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Component tests cover title-only creation, category-specific actions, advanced edits, cancellation, invalid dates, uncertainty/recovery, source change and focus; apply the reusable rendered UI matrix.
Acceptance:
- Category capture and existing-task primary actions are reachable without a management disclosure and use the existing exact canonical review/acceptance contract.
- Drafts, account ownership, original receipts and clear saved-versus-projected feedback survive failure/navigation with passing interaction/layout checks.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Build schedule-aware Today task groups

```yaml
id: T-8e39f2a0-a0fe-4d1a-82af-b3472da5dcae
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-7cb0bb45-a294-404c-b257-136d0f9f3de4", "P-346056d8-c254-4a1e-b860-6850c6cb470d"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-8e39f2a0-a0fe-4d1a-82af-b3472da5dcae
related_task_id: null
relation: main
depends_on: ["T-9f465413-725a-440e-a464-cb5c555cb87d", "T-ea3c2df0-7c13-4a77-a920-1451801b9946", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Implement the owner-requested Today grouping of current-day tasks and due Dailies.
Current behavior:
- WorkspaceAgenda provides planned/deadline flags and canonical Daily occurrences with retained completion/skip overrides. Legacy Habitica snapshots supply separate due information; the app has no Today page.
Desired behavior:
- A reusable Today surface groups planned/current-day task work and scheduled Daily occurrences with visible primary actions and clear completion/due state.
Touch:
- Todo presentation, Today component/page, WorkspaceAgenda/RecurrenceCalendar reads and existing task action components.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Use the selected civil day and explicit rule timezone; Dailies can repeat weekly/monthly and must not appear every day merely because kind is Daily. Keep planned dates and deadlines distinct, labelling same-day deadlines separately. Deduplicate canonical task/date rows, honour retained skipped/completed occurrences and state/archival. Unscheduled Dailies do not acquire an invented schedule. Legacy Habitica due state remains labelled and fresh rather than inferred from canonical recurrence. Viewing creates no occurrence or game effect.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Date-boundary, weekly/monthly/DST, unscheduled, paused, skipped/completed, duplicate and unavailable-timezone fixtures; component actions and rendered mobile checks.
Acceptance:
- Today groups current-day planned work and genuinely due canonical Daily occurrences without duplicate rows or invented everyday schedules.
- Primary actions preserve exact occurrence/source identity and clear freshness, empty, unavailable and completion states.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Expose Inbox and calendar planning views

```yaml
id: T-fb41407b-58d3-4dca-b389-e80bbe517831
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-7cb0bb45-a294-404c-b257-136d0f9f3de4"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-fb41407b-58d3-4dca-b389-e80bbe517831
related_task_id: null
relation: main
depends_on: ["T-8e39f2a0-a0fe-4d1a-82af-b3472da5dcae", "T-ea3c2df0-7c13-4a77-a920-1451801b9946", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Expose implemented native planning through accessible list and agenda controls.
Current behavior:
- Canonical query/agenda/PlanTask contracts exist, but Inbox, Upcoming, Week/Month, overdue/Anytime and placement controls lack dedicated production UI.
Desired behavior:
- Planning surfaces provide date/list navigation, clear planned/deadline labels and exact reviewed rescheduling/clearing without duplicating tasks.
Touch:
- Todo planning components/routes, shared source facade, WorkspaceAgenda/TaskQuery and WorkspaceTaskController PlanTask inputs.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Adopt documented Inbox/unplanned, Anytime and agenda semantics from navigation-study.md while respecting the owner Todo section. Keep a keyboard/list alternative to calendars; no drag-only operation. Use existing date-range/page limits, revision resets and explicit timezone errors. Completed history is separate. Plans/placement create no new reward or implicit projection; linked upstream schedules are unchanged unless an existing reviewed game command explicitly covers a field.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Query/placement and component tests for view membership, range changes, date clearing, stale pages, empty/error states and keyboard navigation; reusable UI matrix.
Acceptance:
- Inbox and upcoming/week/month/overdue/Anytime access uses explicit canonical membership and date semantics with stable bounded reads.
- Reviewed rescheduling and clearing preserve task identity/history and do not silently change game rewards or upstream schedules.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Expose retained task history and safe Todo restore

```yaml
id: T-54623cea-9c8f-4819-b33f-b6552c82df55
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-7cb0bb45-a294-404c-b257-136d0f9f3de4", "P-346056d8-c254-4a1e-b860-6850c6cb470d"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-54623cea-9c8f-4819-b33f-b6552c82df55
related_task_id: null
relation: main
depends_on: ["T-9f465413-725a-440e-a464-cb5c555cb87d", "T-ea3c2df0-7c13-4a77-a920-1451801b9946", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Make completion and Habit activity review available with supported exact Todo restoration.
Current behavior:
- WorkspaceCompletionHistory, WorkspaceHabitHistory and restoreTodo already retain exact events and native-only restoration cycles. The editor lacks complete history/restore controls.
Desired behavior:
- History surfaces filter and page retained activity, label historical versus current fallback text and offer restoration only for supported exact unlinked Todo completions.
Touch:
- Todo history components/routes, shared read facade, existing history engines, WorkspaceTaskController restore inputs and tests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Keep canonical completion/score acceptance separate from confirmed game effects. Preserve archived-history flags and UTC event versus civil occurrence date labels. Restore requires the exact unrestored completion and an unlinked nonarchived Todo; linked, archived-task or recurring undo stays explicitly unsupported pending separate policy. No deletion of history, negative reward, inferred streak or replay of uncertain actions.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Component tests for historical snapshots/fallback, filters/paging, archive flags, multiple cycles, restore review, unsupported targets, lost acknowledgement and no reward changes; rendered UI checks.
Acceptance:
- Users can review retained Todo/Daily completion and Habit activity with accurate source/time/history labels and bounded filtering.
- Only a supported exact native Todo restoration is offered and accepted; original history and every game-effect fence remain intact.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Add reviewed browser organization workflows

```yaml
id: T-574a6ba2-741e-4d13-b798-b909c24de93d
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-0529505a-68be-5308-a805-44a2f4ddd6a9", "P-580e55b1-e10a-4891-bd89-fdbd5c39a627"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-574a6ba2-741e-4d13-b798-b909c24de93d
related_task_id: null
relation: main
depends_on: ["T-9f465413-725a-440e-a464-cb5c555cb87d"]
```

Goal:
- Provide reusable account-bound native organization command state for browser UI.
Current behavior:
- WorkspaceOrganizationService/Engine and IWorkspaceOrganizationApi exist, but no organization controller owns browser preview/acceptance/unknown-receipt recovery for pages.
Desired behavior:
- A reusable controller supports local/cloud create/update/archive for existing organization kinds using exact previews and original receipts.
Touch:
- New focused organization controller, WorkspaceOrganizationService, IWorkspaceOrganizationApi, scoped local store and adapter tests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Follow existing task-controller ownership semantics without mixing native organization receipts with task/game receipts. Preserve exact request/command IDs across preparation loss, unknown acceptance, expiry and stale revision. Local changes use optimistic native journal commits; cloud changes use existing scoped organization-api. No arbitrary fields, credentials, silent relationship deletion or implicit executor effects. Source/account changes discard late results and preserve unresolved original intent.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Controller tests cover all native organization kinds, local/cloud parity, no-op/invalid edits, lost preview/acceptance, original recovery, authority restrictions and account changes.
Acceptance:
- Browser organization actions use one validated local/cloud preview-acceptance-recovery controller with independent exact identities.
- Native history/relationships and zero new game effects remain valid through stale revisions, unknown outcomes and account/source changes.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Expose projects goals and optional task organization

```yaml
id: T-fd2c633c-7b39-4776-a364-dacb83abaff7
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-0529505a-68be-5308-a805-44a2f4ddd6a9", "P-cea6ca25-8602-4b3a-8a4c-4a9c348dff85"]
processing_run_id: PR-9dcb5ed6-4676-4f0b-b5d6-e63ed857e0cd
root_task_id: T-fd2c633c-7b39-4776-a364-dacb83abaff7
related_task_id: null
relation: main
depends_on: ["T-574a6ba2-741e-4d13-b798-b909c24de93d", "T-ea3c2df0-7c13-4a77-a920-1451801b9946", "T-6122dded-900d-4d92-8739-bbc344e15ea0", "T-264e3162-adc3-45f0-943e-a55feccc9d8e", "T-eb745ca4-9da7-4fef-8c5f-13f115b4c22a"]
```

Goal:
- Make existing organization and lightweight task workflows usable from a phone.
Current behavior:
- Canonical categories/projects/sections/labels/goals, Todo progress, placement, relationships, states and active stages exist, with no dedicated project workspace UI.
Desired behavior:
- A working Projects surface supports optional organization CRUD/archive, project sections/task placement and goal progress; advanced task controls expose relationships and stages without mandatory hierarchy.
Touch:
- Projects page/components, organization controller, task edit components and canonical organization/query/progress services.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Reuse flat categories, one primary project and Todo-based goal progress. Cancelled/archived/ineligible work must not inflate progress; empty progress is unavailable rather than a fabricated percentage. Exact parent/dependency edits retain cycle validation and full-replacement review. Waiting/someday/current state dominates dormant Inbox/Next/InProgress stage preferences. Project board is optional with keyboard/list alternatives and no staff monitoring metrics. Native organization/state edits never imply completion or game permission.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Component journeys cover simple unassigned capture, project/section/goal creation, placement/move/archive, labels, dependency/cycle rejection, progress updates, stages and original recovery; reusable UI checks.
Acceptance:
- Projects, categories, sections, labels and goals have working optional browser controls with correct canonical progress and task placement.
- Relationship/state/stage controls retain exact review, domain validation and accessible list alternatives without mandatory workflow administration or game effects.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Expose recurring schedules and occurrence lifecycle

```yaml
id: T-68cb7da7-62e9-4136-b5c5-07da20f97c30
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-0ddc8784-c740-5a23-819c-b17db8f6829f", "P-346056d8-c254-4a1e-b860-6850c6cb470d"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-68cb7da7-62e9-4136-b5c5-07da20f97c30
related_task_id: null
relation: main
depends_on: ["T-9f465413-725a-440e-a464-cb5c555cb87d", "T-ea3c2df0-7c13-4a77-a920-1451801b9946", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Expose the already implemented Daily recurrence and native lifecycle contracts.
Current behavior:
- Canonical Daily schedule replacement, dated completion/skip and pause/resume exist; WorkspaceTaskInput and the visible editor do not expose the complete recurrence/lifecycle workflow.
Desired behavior:
- Daily editor supports explicit daily/weekly/monthly schedules and timezone, occurrence review and Companion pause/resume with retained history.
Touch:
- Daily editor/occurrence controls, typed controller inputs and existing recurrence/calendar/lifecycle validation.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Expose existing interval/start/weekday/month-day/timezone fields, supported clearing and limits. Review schedule replacement and retained completion/skip precedence. Pause is Companion-only and never promises to stop Habitica penalties. New native Dailies may remain explicitly unscheduled. No implicit type conversion, linked schedule authoring, completion-based recurrence or reward undo; unsupported controls explain the limitation.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Component and command-input tests cover daily/weekly/monthly, invalid rules/timezones, DST/date boundaries, occurrence completion/skip, pause/resume, replacement and acknowledgement loss; reusable UI matrix.
Acceptance:
- Users author/review supported Daily schedules and exact occurrence/lifecycle actions through the existing canonical contract.
- History, schedule/due provenance and native-only pause semantics remain correct; unsupported game schedule/type conversions cannot run.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Expose reviewed saved-list authoring

```yaml
id: T-66aec83d-1d40-4c42-87ec-6566e1ac3769
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-580e55b1-e10a-4891-bd89-fdbd5c39a627"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-66aec83d-1d40-4c42-87ec-6566e1ac3769
related_task_id: null
relation: main
depends_on: ["T-574a6ba2-741e-4d13-b798-b909c24de93d", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Add complete browser authoring for existing fixed-query saved task views.
Current behavior:
- CompanionTaskList selects current fixed definitions; the domain/native engine and scoped adapters support reviewed create/update/archive, but authoring controls are missing.
Desired behavior:
- Users name, inspect, edit and archive a saved query with explicit fixed-date/source/filter semantics and original native receipts.
Touch:
- Saved-list editor, organization controller, canonical query predicates and existing saved-view tests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Use existing tasks/unblockedTodos sources, exact organization references and full query replacement. Fixed dates remain fixed; no Today-relative inference or arbitrary expression language. Stage filters remain active-Todo-only and dependency candidates remain unblocked-only. Archived definitions retain history and cannot silently fall back to all tasks. Use independent organization-write cloud grants where applicable; task-write does not supply metadata permission.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Component journeys cover create/name/filter/update/clear/archive, unsupported filter combinations, stale references, query results, account changes and lost acceptance; reusable UI checks.
Acceptance:
- Saved lists have reviewed native browser create/update/archive controls and current query evaluation with no task duplication.
- Definition replacement, fixed dates, scope boundaries and original receipt recovery remain correct across invalid/stale/unknown outcomes.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Move task analytics to a dedicated statistics page

```yaml
id: T-b92d23ba-8586-4869-a6b8-f8eb180f1b02
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-346056d8-c254-4a1e-b860-6850c6cb470d", "P-0529505a-68be-5308-a805-44a2f4ddd6a9"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-b92d23ba-8586-4869-a6b8-f8eb180f1b02
related_task_id: null
relation: main
depends_on: ["T-9f465413-725a-440e-a464-cb5c555cb87d", "T-54623cea-9c8f-4819-b33f-b6552c82df55", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Keep analytics off the first task screen and provide useful correctly sourced statistics.
Current behavior:
- TasksPage places cached Habitica aggregates and charts before task groups. Canonical completion/Habit history and goal progress engines provide distinct evidence.
Desired behavior:
- A dedicated Statistics page provides period/filter controls, existing charts and supported canonical activity/progress summaries with source and history limits.
Touch:
- Statistics page/read models, existing task-history renderers, canonical history/progress engines and tests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Separate Habitica snapshot/history values, canonical accepted events and confirmed game effects. Do not invent reward totals, streaks, missing-history zeroes, complete historical denominators or reversed rewards. Preserve current week/month/year charts where supported and provide text summaries/empty/unavailable states. Use semantic theme colors plus labels rather than color-only meaning; bounded pagination/ranges cannot silently omit data from totals.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Synthetic known-history aggregate assertions, partial/missing-history, date-boundary, restore-cycle and projection-state cases; component chart/filter navigation and reusable UI checks.
Acceptance:
- A dedicated statistics surface retains supported charts and offers correctly sourced period-based task/Habit/project summaries.
- Incomplete/unavailable history and canonical-versus-game differences are explicit; charts and text summaries remain readable and accessible.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Create dedicated Todo sync and task settings

```yaml
id: T-c0bf3f10-141f-464c-bc1e-53040d67e61c
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-6b10f9d7-280d-40d5-a212-8e0f44173f5d", "P-f3f3c195-8af3-4191-94cb-0d8ca93f6c11"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-c0bf3f10-141f-464c-bc1e-53040d67e61c
related_task_id: null
relation: main
depends_on: ["T-a7dca938-7c13-4878-9363-04862fbfa04d", "T-9f465413-725a-440e-a464-cb5c555cb87d", "T-d007ac44-f38c-4069-9b70-e66adbc64bf0", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Move task source/command management away from the primary action list.
Current behavior:
- Manage Companion tasks exposes source/command controls inline above legacy lists; workspace/executor consent and operational status reside in global Settings.
Desired behavior:
- A dedicated Todo Sync/settings page owns task sources, relevant preferences, backups/transfers and operational recovery while reusing existing account/privacy controls.
Touch:
- Todo Sync page, extracted task management components, WorkspaceDataController/TaskController and existing WorkspaceSettingsPanel behavior.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Do not duplicate consent state or create a second executor policy. Separate server availability, account opt-in and tab access; show pending/blocked/unknown versus executor paused and preserve original receipt lookup. Transfer freezes/ownership and existing backup formats remain explicit; preference ownership follows the sync inventory. Copy/export diagnostics uses the supported redacted path. The primary task lists must not require opening the management page. No destructive cleanup, new connection or background execution occurs on navigation.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Component journeys cover current source, status refresh, unavailable access, separate consent, paused/unknown work, original recovery, local/cloud backup and cancelled transfer/cleanup; reusable UI checks.
Acceptance:
- Task source/sync/settings management has a dedicated working destination with accurate account/tab/executor status and relevant preferences.
- Existing consent, backup/transfer ownership and original command recovery remain single-source and unchanged by navigation or failed reads.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Compose the always-open Todo navigation and task pages

```yaml
id: T-3b76d03e-c380-4c4f-9e2b-a982c1c9850b
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-0ddc8784-c740-5a23-819c-b17db8f6829f"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-3b76d03e-c380-4c4f-9e2b-a982c1c9850b
related_task_id: null
relation: main
depends_on: ["T-ea3c2df0-7c13-4a77-a920-1451801b9946", "T-8e39f2a0-a0fe-4d1a-82af-b3472da5dcae", "T-fb41407b-58d3-4dca-b389-e80bbe517831", "T-54623cea-9c8f-4819-b33f-b6552c82df55", "T-fd2c633c-7b39-4776-a364-dacb83abaff7", "T-68cb7da7-62e9-4136-b5c5-07da20f97c30", "T-66aec83d-1d40-4c42-87ec-6566e1ac3769", "T-b92d23ba-8586-4869-a6b8-f8eb180f1b02", "T-c0bf3f10-141f-464c-bc1e-53040d67e61c", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Replace the single Tasks menu with the exact owner-requested Todo section.
Current behavior:
- AppNavMenu contains one Tasks link and TasksPage renders a long mixed dashboard. Independently implemented feature components require route composition.
Desired behavior:
- Always-expanded Todo contains Today, Habits, Dailies, Tasks and working Statistics, Sync/settings and Projects destinations; main category pages immediately show the selected category list and primary actions.
Touch:
- AppNavMenu, MainLayout, TasksPage extraction, new Todo routes and navigation/component/browser tests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Owner structure supersedes conflicting primary destinations in navigation-study.md. Use category-specific tabs plus persistent nested menu without a collapse toggle. Compose real working destinations, no placeholder links. Move statistics and management off the first list screen; keep source/freshness status concise. Retain reachable Reward controls and existing task links through safe routing compatibility. Integrate the reviewed capture, Today, planning/history, recurrence, project and saved-list components without duplicating account/workspace state. No task conversion or game effect follows from route migration.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Navigation tests cover all destinations, direct deep links, old /tasks compatibility, selected-tab/menu state, no repeated management disclosure and actual first-action visibility. Exercise synthetic journeys and the full changed-page UI/theme matrix.
Acceptance:
- The always-open Todo section and category tabs route to working task-first pages with immediate capture/complete/score access and no statistics/management barrier.
- Statistics, Sync/settings, Projects and supported advanced features remain discoverable with preserved old links, Reward access and correct shared source/recovery state.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Qualify ChatGPT, Codex, multi-user and mobile release gates

```yaml
id: T-d76d2a9a-1318-4e63-9bb4-73f620b108cc
priority: 100
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

### Serialize scoped diagnostic append and clear operations

```yaml
id: T-13c008e0-2062-4846-ab4b-0f331a280f0b
priority: 99
status: Ready
parent_pending_ids: ["P-15da69d1-68cb-47d2-b8ae-2edca2436137"]
processing_run_id: PR-aa6ec1c6-4a91-4619-8193-5ef4f8d8a54a
root_task_id: T-d007ac44-f38c-4069-9b70-e66adbc64bf0
related_task_id: T-d007ac44-f38c-4069-9b70-e66adbc64bf0
relation: followup
depends_on: []
```

Goal:
- Prevent overlapping scoped diagnostic mutations from losing committed local events.
Current behavior:
- DiagnosticsLogStore uses awaited read/modify/write append and an independent clear operation. Program.cs registers one scoped store per WebApp instance; no mutation coordinator exists.
Desired behavior:
- Append and clear mutations within the same scoped instance execute in a deterministic order, retaining the configured capacity and cancellation behavior.
Touch:
- src/Habitica.Storage/DiagnosticsLogStore.cs and deterministic storage/interleaving tests.
Discovery allowance:
- Inspect IndexedDbStorageAdapter and existing async coordination before selecting a minimal primitive.
Out of scope:
- Canonical command behavior, remote telemetry, cross-device synchronization and a cross-tab atomicity guarantee.
Implementation constraints:
- Use async coordination with release in finally; never block the UI thread. Preserve default capacity, newest-first order, clear semantics and cancellation. Logging remains best effort and cannot authorize replay. State explicitly whether cross-tab atomicity remains unavailable; an in-process semaphore cannot establish a database transaction across browser instances.
Questions and required clarifications:
- [Resolved - inspected DI scope and source] The selected correction covers concurrent calls to the same scoped store. Cross-tab coordination requires a separately verified storage contract.
Validation:
- Storage gates force two overlapping append operations and an append/clear interleaving. Verify no scoped lost update, deterministic clear order, bounded retention and cancellation before acquisition and during storage work.
Acceptance:
- Concurrent scoped append and clear operations preserve ordered committed history without exceeding configured retention.
- Coordination does not block the UI thread, leak the gate after cancellation or change original command behavior.
Documentation updates:
- Update testing.md and the diagnostic coverage contract with the exact coordination scope.
Risks:
- A local lock can be misrepresented as cross-tab atomicity; retain the explicit boundary.

### Prepare reproducible task UX and device qualification evidence

```yaml
id: T-06336249-6901-4d10-8df1-229ca9bcad6b
priority: 99
status: Ready
parent_pending_ids: ["P-c5795748-6f25-4c0a-a471-fe72c1a85862"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-06336249-6901-4d10-8df1-229ca9bcad6b
related_task_id: null
relation: main
depends_on: ["T-3b76d03e-c380-4c4f-9e2b-a982c1c9850b", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Make the remaining E01 practical checks executable against the implemented Todo routes without treating preparation as completed human research.
Current behavior:
- navigation-study.md records public competitor flows and prototype evidence; current E01 lacks authenticated journeys and actual device/assistive-technology results.
Desired behavior:
- Provide exact task journeys, synthetic setup, observation forms and a traceable local run. Keep authenticated competitor and real-device acceptance in the existing blocked release gate.
Touch:
- navigation-study.md, ai-app-testing.md, release qualification matrix, current Razor routes and existing synthetic browser fixtures.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Replace superseded prototype navigation with the owner Todo/Today/Habits/Dailies/Tasks structure. Reuse Todoist and Things official flow references as documented evidence only. Cover capture, dates, complete/restore, recurrence, projects, sync and diagnostic recovery. Record route, viewport, theme, keyboard/focus, result and evidence provenance. Define real iOS Safari/PWA, Android, virtual keyboard, safe-area, screen-reader and authenticated competitor checks with explicit unrun status and required owner/session inputs. Do not create third-party accounts, buy software or mark the release gate passed from fixtures.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- Execute the available synthetic browser journeys and negative layout/interaction controls after dependent pages exist. Check both normal theme variants and phone/desktop geometry. Review the real-device handoff checklist without claiming actual device execution.
Acceptance:
- A reproducible current-route journey pack and actual local browser evidence cover the named task flows with explicit coverage limits.
- Authenticated competitor, human and physical-device requirements remain traceable in T-d76d2a9a-1318-4e63-9bb4-73f620b108cc with missing inputs and no fabricated pass.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

### Restore archived tasks and correct native Daily occurrences

```yaml
id: T-2355607b-ba77-46c0-9495-aae61e16a8f0
priority: 99
status: Ready
parent_pending_ids: ["P-64d1995c-f862-498c-9437-e867117f2129"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-2355607b-ba77-46c0-9495-aae61e16a8f0
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Extend retained native recovery beyond completed unlinked Todos.
Current behavior:
- DeleteTask sets IsArchived; RestoreTodo rejects archived, linked and non-Todo records. Occurrence states have no retained correction operation.
Desired behavior:
- Separate unarchive from completion undo. Unarchive preserves task state; an exact unlinked Daily occurrence correction records a new causal event while preserving the original completion/skip.
Touch:
- WorkspaceCommands, WorkspaceCommandIdentity, WorkspaceRules, WorkspaceCommandEngine, history/portable validation, shared Worker commands/schema, Workspace task API and neutral fixtures.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Use explicit typed unarchive and occurrence-correction operations with unique command IDs, expected revision and exact target event/occurrence. No history deletion, reward reversal, automatic next occurrence or changed schedule. Archived references remain valid and previous organization is restored only while references remain available. Invalid references require an explicit repair preview. Reject linked corrections, pending/unknown game effects and unsupported Habit inverse events; the separate linked-recovery task owns upstream unchecking.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- Cross-runtime fixtures cover completed/skipped/archived cases, duplicate and concurrent corrections, archive-before-undo, missing references, stale revision, imports, exact zero-effect receipts and rejection of linked/uncertain work.
Acceptance:
- Unarchive and native occurrence correction retain original identities/history and produce exactly one validated causal correction without game effects.
- Malformed, stale, duplicate and linked/uncertain requests cannot delete history, dispatch effects or silently repair relationships.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

### Add reviewed own-task Habitica unchecking and reconciliation

```yaml
id: T-0a2f12ac-6df6-498f-b090-f69c2a526b2a
priority: 99
status: Ready
parent_pending_ids: ["P-64d1995c-f862-498c-9437-e867117f2129"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-0a2f12ac-6df6-498f-b090-f69c2a526b2a
related_task_id: null
relation: main
depends_on: ["T-2355607b-ba77-46c0-9495-aae61e16a8f0"]
```

Goal:
- Support explicit recovery of a confirmed linked Todo or current eligible Daily without claiming exact reward reversal.
Current behavior:
- Auth projection accepts non-Habit score up only; canonical RestoreTodo rejects linked records. Official Habitica scoreTask.js implements non-Cron down operations with resource/history changes.
Desired behavior:
- A separately reviewed linked-recovery command binds the exact upstream completed state and original confirmed Companion completion, dispatches at most once and records native recovery only from the original successful receipt.
Touch:
- Auth projection preconditions/receipts, Workspace command engine and task API, C# browser command adapters, MCP definitions, shared task fixtures and Habitica API contract.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Own non-shared tasks only; reject group/challenge tasks, missing mappings, unsupported prior-day Daily recovery, needsCron/unknown day state and overlapping pending/unknown effects. Bind current account, task type/id/fingerprint, exact completion and eligible Daily date/checklist to an expiring single-use preview. Show estimated resource fields as unknown when exact reversal is unavailable; never call the action a guaranteed refund. Keep original history and add a causal recovery event. Lost acknowledgement uses receipt lookup only; no replacement dispatch or inferred success from later task state. Existing scopes, consent, quotas and cross-tool uncertainty fences remain mandatory. Daily skip correction stays native-only. Implement and test locally without a real score or Cron call.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- Synthetic Auth/Workspace composition plus neutral C# fixtures cover confirmed/unknown/partial outcomes, repeated requests, same-day versus expired-day Daily state, account/revocation changes, shared-task denial, persistence failures and no retry. Recheck official score/controller semantics before implementation.
Acceptance:
- Exact reviewed linked recovery confirms at most one own-task dispatch and retains the original receipt plus causal native recovery.
- Unknown, stale, shared, prior-day or unverified recovery remains explicit and cannot replay, grant a refund claim or affect party management.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

### Expose extended recovery actions in task history

```yaml
id: T-8545fa1a-f9e1-4c2a-8501-30bfbae4c985
priority: 99
status: Ready
parent_pending_ids: ["P-64d1995c-f862-498c-9437-e867117f2129"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-8545fa1a-f9e1-4c2a-8501-30bfbae4c985
related_task_id: null
relation: main
depends_on: ["T-2355607b-ba77-46c0-9495-aae61e16a8f0", "T-0a2f12ac-6df6-498f-b090-f69c2a526b2a", "T-54623cea-9c8f-4819-b33f-b6552c82df55", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
```

Goal:
- Make supported recovery discoverable without hiding the operation meaning.
Current behavior:
- The planned history surface exposes only the existing unlinked Todo restore; archive and linked/occurrence recovery need separate controls.
Desired behavior:
- History and archived-task views offer exact unarchive, occurrence correction and linked-uncheck actions with supported-state reasons and original receipts.
Touch:
- Workspace history controllers, browser/native-cloud command adapters, history UI, task details and MCP recovery tool descriptions.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Use separate labels for unarchive, native correction and Habitica uncheck. Show the task/occurrence, original completion, native changes and possible game resource effects before acceptance. Never replace an unknown command, silently convert an action to an unlinked copy or erase old history. Preserve filters, focus, source identity, accessible touch targets and normal-theme readability.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- Component/transport fixtures cover every supported action, refusal explanation, lost acknowledgement, restored filters/focus and receipt lookup. Render phone/desktop normal themes through the shared UI checks.
Acceptance:
- Supported recovery actions are reachable from history and execute only the exact reviewed operation with a visible original receipt.
- Unsupported or uncertain recovery displays a concrete reason and preserves history, draft state and command identity.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

### Retain trustworthy task activity and schedule coverage

```yaml
id: T-aa82feee-97b1-4f14-8b5a-d642cca014e3
priority: 99
status: Ready
parent_pending_ids: ["P-438c79fe-385d-4657-be24-85fad9a49c31"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-aa82feee-97b1-4f14-8b5a-d642cca014e3
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Establish evidence for history completeness before calculating streaks.
Current behavior:
- Completion/Habit reads retain accepted events but lack a coverage anchor, gap intervals or historical schedule epochs.
Desired behavior:
- Portable workspace coverage records identify known intervals and schedule/state history; imported or incomplete periods remain unknown.
Touch:
- WorkspaceDocument, journal validation/serialization, command engine, recurrence/lifecycle commands, Worker schema/commands and neutral portable fixtures.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Coverage is recorded from actual retained causal journal evidence, not inferred from task creation time or a paginated response. Legacy/imported snapshots default to unknown unless a validated coverage record proves completeness. Retain effective-dated recurrence/timezone/pause/resume changes. Query clocks never mutate coverage. Gap, retention, import and correction records cannot invent missing scores. Add compatible optional data with explicit validators; no migration tooling is required for early development.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- C# and Worker fixtures cover new complete journals, legacy unknown histories, missing revisions, schedule edits, pause intervals, archive/restore, export/import and corrupted coverage assertions.
Acceptance:
- Activity coverage and historical schedule intervals round-trip with causal validation and remain unknown across unproven gaps.
- No read or import can manufacture complete history from timestamps, page counts or current recurrence settings.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

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
parent_pending_ids: ["P-438c79fe-385d-4657-be24-85fad9a49c31"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-e77f6c7c-cf2f-487d-bb82-831e6f6167f9
related_task_id: null
relation: main
depends_on: ["T-aa82feee-97b1-4f14-8b5a-d642cca014e3", "T-68cb7da7-62e9-4136-b5c5-07da20f97c30"]
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

### Add voluntary review scheduling and retained review history

```yaml
id: T-f5079dc2-61e9-4889-bbb0-9c2edb43eadd
priority: 99
status: Ready
parent_pending_ids: ["P-9dbdf081-4b8e-495c-a8fc-5c94c34d3a78"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-f5079dc2-61e9-4889-bbb0-9c2edb43eadd
related_task_id: null
relation: main
depends_on: ["T-9f465413-725a-440e-a464-cb5c555cb87d", "T-ea3c2df0-7c13-4a77-a920-1451801b9946"]
```

Goal:
- Turn existing review/stale fields into an optional, auditable task workflow.
Current behavior:
- ReviewAfterUtc/StaleSinceUtc exist in the task model but have no complete command, review-event or reset contract.
Desired behavior:
- Explicit set/snooze/clear review cues and mark-reviewed commands retain history without requiring completion or a mandatory review routine.
Touch:
- WorkspaceTaskPatch/identity, review events and validators, C# command/query services, Worker task API/schema, task editor and MCP tools.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Review cues use explicit UTC instants with a displayed timezone. Mark reviewed records one causal review event and clears the consumed review cue; another review date requires an explicit chosen value. Snooze changes the cue without claiming a review. StaleSince is a manual flag, not inferred lateness from inactivity or device clock. No automatic task-state, priority, planned-date, deadline or game change. Exact native preview/receipt ownership and late-session rejection apply; old snapshots without review events remain valid.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- Cross-runtime and component tests cover due/future cues, clear/snooze/review distinctions, repeat/idempotent commands, stale revision, imported legacy fields, timezone display, missing refs and zero game effects.
Acceptance:
- Voluntary review actions produce exact retained review/cue state with independent snooze, clear and mark-reviewed semantics.
- Review reads and actions never imply task completion, mandatory ritual, deadline failure, automatic priority or gameplay mutation.
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

### Deliver explicit in-app and foreground browser reminder cues

```yaml
id: T-b05e848e-9bb0-461c-aee0-8c4293fcbeb2
priority: 99
status: Ready
parent_pending_ids: ["P-9dbdf081-4b8e-495c-a8fc-5c94c34d3a78"]
processing_run_id: PR-1577e03d-8a2b-42f8-b3ac-caa1234a7fed
root_task_id: T-b05e848e-9bb0-461c-aee0-8c4293fcbeb2
related_task_id: null
relation: main
depends_on: ["T-f5079dc2-61e9-4889-bbb0-9c2edb43eadd", "T-8e39f2a0-a0fe-4d1a-82af-b3472da5dcae", "T-c0bf3f10-141f-464c-bc1e-53040d67e61c"]
```

Goal:
- Provide useful optional reminders with honest delivery limits.
Current behavior:
- Today grouping and review metadata do not schedule notification delivery; the PWA has no accepted reminder permission or push-subscription contract.
Desired behavior:
- An opt-in task cue appears in the app and can notify while the app is running after a separate user permission action. Missed cues remain visible after reopening.
Touch:
- Workspace reminder intent/receipts, Today and Todo settings, browser reminder service/interop, service-worker capability labels and synthetic notification fixtures.
Discovery allowance:
- Inspect the named current modules, shared fixtures and official references before editing. Capture newly required dependencies rather than widening the task silently.
Out of scope:
- Unrelated refactors, implicit enrollment or notification permission, remote publication and real-account/device operations outside applicable owner authorization.
Implementation constraints:
- Reuse the reviewed cue time and canonical Today/review queries. In-app cues work without browser permission; permission is requested only from an explicit browser-notification action. Dedupe by account, task/cue identity and schedule revision; permission denial, unsupported devices, clock changes, snooze and account changes remain visible. No periodic upstream polling or task/game effects. Foreground timers are best effort and never promise delivery from a closed tab. Background push is explicitly unavailable under the initial contract and remains part of the existing Background Automation backlog; do not silently enroll a push subscription or store notification endpoints.
- Priority 99 preserves the owner order: current sync/layout/diagnostics defects and the initial Todo interface precede advanced extensions.
Questions and required clarifications:
- [Resolved - standing owner contract adoption] Adopt the explicit bounded defaults below for local implementation and synthetic tests. Per-user consent and actual release qualification remain independent.
Validation:
- Fake-clock/browser permission fixtures verify foreground delivery, duplicate suppression, reopen catch-up, denied/unavailable permission, cross-account cleanup and no upstream/network effect. Component and rendered checks verify delivery-limit wording and accessible cue actions.
Acceptance:
- Opt-in in-app/foreground cues obey exact schedule identity and preserve missed-cue visibility without duplicate notifications.
- Denied permissions, closed-app limits and background unavailability remain explicit; reminders never grant consent, send credentials, poll Habitica or mutate tasks automatically.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

### Security Hardening, Revocation, Key Rotation, And Team Rollout

```yaml
id: T-96866c8f-9554-55d8-a638-6d8b5afaf21c
priority: 97
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
