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

### Define optional review reminders and relative saved-list policy

```yaml
id: P-9dbdf081-4b8e-495c-a8fc-5c94c34d3a78
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
- Research optional review/reminder scheduling and meaningful review history plus relative-date saved-view presets beyond the supported fixed-query saved-list editor.

Expected behavior:
- A researched native contract defines voluntary review state/history and exact relative-clock/date semantics without mandatory rituals, false lateness or game penalties; notification delivery requires an explicit supported scope.

Current context:
- ReviewAfterUtc/StaleSinceUtc fields do not define review-event history, reset rules or notification delivery. Portable saved definitions currently retain fixed dates only. Processing blocker: distinguish manual review cues, browser reminders and server notification scope, and define relative-view timezone/clock/evaluation semantics before persistence changes. Reuse the separate Today agenda rather than create duplicate date logic. Fixed saved-list authoring is promoted separately.

Source:
- Remaining intent from processed P-580e55b1-e10a-4891-bd89-fdbd5c39a627 and original E12 P-0018dcbc-f179-5b27-b0a8-8d57cf2fcb4d; source-backed decomposition on 2026-10-19.

### Define streak coverage linked schedules and task conversion

```yaml
id: P-438c79fe-385d-4657-be24-85fad9a49c31
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
- Research sufficient-history streak semantics, Habitica-linked recurrence and task-type conversion beyond the supported native Daily schedule and activity-review UI.

Expected behavior:
- A researched contract distinguishes known history coverage from unknown gaps and defines safe linked schedule/type conversion without duplicated occurrences, scores or invented streaks.

Current context:
- Retained score/completion reads contain accepted events but no complete-history coverage anchor or streak policy. Existing recurrence authoring is canonical Daily-only; native lifecycle does not alter Habitica penalties. Processing blocker: establish coverage/reset/timezone rules and exact linked mapping/conversion semantics before implementation. Known recurring/editor/history controls are promoted separately.

Source:
- Remaining intent from processed P-346056d8-c254-4a1e-b860-6850c6cb470d and original E11 P-f2bb7e37-6700-58ac-aa56-b537d193c777; source-backed decomposition on 2026-10-19.

### Define linked archived and recurring recovery policies

```yaml
id: P-64d1995c-f862-498c-9437-e867117f2129
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
- Research the remaining E09 recovery scope for Habitica-linked work, archived tasks and recurring occurrences after the supported native unlinked Todo restore UI.

Expected behavior:
- A researched recovery contract names exact supported operations, retained history and game-effect boundaries; uncertain upstream outcomes never authorize reward reversal or replay.

Current context:
- Current restoreTodo only reopens an exact completed nonarchived unlinked Todo. Linked-game recovery, archived-task restoration and recurring undo lack accepted behavior and sufficient upstream reconciliation evidence. Known mobile planning/history work is promoted separately. Processing blocker: define supported recovery semantics and upstream evidence requirements before implementing additional recovery operations.

Source:
- Remaining intent from processed P-7cb0bb45-a294-404c-b257-136d0f9f3de4 and original E09 P-c8632138-39c6-5797-8e03-a3414824ee96; source-backed decomposition on 2026-10-19.

### E01 Hands-on competitor and mobile workflow qualification

```yaml
id: P-c5795748-6f25-4c0a-a471-fe72c1a85862
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
- Complete the source research hands-on competitor/mobile workflow qualification and use the findings to refine the adopted navigation baseline before broader release. Owner-selected development implementation and exposure may proceed before qualification.

Expected behavior:
- Document actual observed journeys and limitations without presenting public workflow documentation, a synthetic prototype or automatic contract adoption as human/device evidence.

Current context:
- The dated documented-flow inventory, personas, borrow/adapt/reject matrix and local prototype journeys/layout are complete. The local baseline is adopted under standing direction. Authenticated competitor sessions, human usability, virtual keyboard, safe areas and assistive technology remain unqualified. Owner-only Codex live testing is reopened; authenticated competitor sessions and real human/device usability remain unqualified.

Source:
- Remaining E01 intent from P-4f98b2b3-af24-5424-b2f9-066913f7133d and companion-first-mcp-research.md research completion gap; local contract adoption does not waive the explicit hands-on gate.

Processing blocker:
- Documented competitor workflows and the local baseline exist, but authenticated competitor journeys, human usability, actual mobile keyboard/safe-area and assistive-technology observations are absent. Actual selected sessions/testers/device evidence is required; source inspection or resized fixtures cannot complete the deliverable. Development UI work remains independent under the owner boundary.

## Prioritized Next Changes

Highest numeric priority first; unmet dependencies and blocking questions stop implementation. Imported source provenance and product decisions remain distinct from newly executed research.

### Converge saved themes across device sync

```yaml
id: T-31fbbe84-a4ab-4bc7-9509-647666e6e017
priority: 101
status: Ready
parent_pending_ids: ["P-f3f3c195-8af3-4191-94cb-0d8ca93f6c11"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-31fbbe84-a4ab-4bc7-9509-647666e6e017
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Make saved appearance converge and apply after encrypted app-data sync.
Current behavior:
- ColorSchemePanel loads on initialization and fires section uploads without exposing failures. Full sync merges storage through LocalUserDataPortabilityService; no imported-theme application notification reaches the open panel or fast cache. Section-only uploads bypass the full merge path.
Desired behavior:
- A newer saved selection/custom edit reaches independent browser stores, updates current controls and document styling, and survives reload without stale fast-cache replacement.
Touch:
- ColorSchemeService, ColorSchemePanel, AppSessionController sync methods, LocalUserDataPortabilityService, colorSchemes.js and existing theme/sync tests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Reuse timestamp-based merge and serialized account-bound sync. Inspect concurrent whole/section uploads and merge newer remote preferences before replacing the remote section. Do not stamp a new selection merely by reading, sync transient random previews, copy credentials or claim globally atomic KV convergence. Inventory task-view/fold preferences as explicitly local versus portable without silently changing the portability contract. Discard late account results; preserve unsaved edits with an explicit conflict choice.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Two independent local stores and controlled transport test remote import while a panel is open, competing edits, stale cache, sign-out, upload failure and transient previews. Browser checks exercise current theme application and reload; actual Mac/iOS evidence remains a release check.
Acceptance:
- Saved theme import updates rendered appearance, current selector and fast cache without a new user edit or unrelated action.
- Concurrent and failed sync preserves account boundaries, documented merge semantics and explicit failure state; transient previews remain local.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Distinguish server connections from tab management access

```yaml
id: T-a7dca938-7c13-4878-9363-04862fbfa04d
priority: 101
status: Ready
parent_pending_ids: ["P-f3f3c195-8af3-4191-94cb-0d8ca93f6c11"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-a7dca938-7c13-4878-9363-04862fbfa04d
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Stop presenting missing local management access as disabled server enrollment.
Current behavior:
- SettingsPage.HasChatGptConnection and WorkspaceSettingsPanel.CloudWorkspaceId depend on in-memory clients. Closing a tab forgets credentials; the first-connection form appears again. Existing authenticated status, explicit revalidation and workspace renewal are available.
Desired behavior:
- Connection cards distinguish unverified account state, current-tab management access, confirmed server enrollment and executor enabled/paused state. Explicit recovery retains server data and existing consent decisions.
Touch:
- SettingsPage, WorkspaceSettingsPanel, ChatGptIntegrationClient, WorkspaceCloudClient, WorkspaceDataController, integration/status tests and privacy/connection docs.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Absent management access means unknown until explicit verification, never a fabricated disabled state. Reuse supported consented revalidation/renewal; no automatic credential upload, persistent management-token sync, permission grant, background opt-in, profile deletion or token rotation. Reread executor state after connection recovery and explicit status refresh rather than trusting stale local booleans. Explain pending work paused by missing executor consent without retrying commands.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Independent client instances simulate lost-tab access, retained enrollment, executor state changed elsewhere, failed status, expired session and account switch. Component tests verify clear recovery actions and separate consent.
Acceptance:
- Fresh-tab connection UI reports unknown/unverified management access and offers explicit recovery without implying account disconnection.
- Successful verification displays actual enrollment/executor state while failures and account changes cannot show stale success or grant new access.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Align Settings session action cards

```yaml
id: T-2bf91411-423e-483b-95a7-92b9fdc69445
priority: 100
status: Ready
parent_pending_ids: ["P-24ec4579-ff37-44c6-9085-56fefe830888"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-2bf91411-423e-483b-95a7-92b9fdc69445
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Repair uneven Settings action placement shown in the owner screenshot.
Current behavior:
- settings-grid stretches action-card elements, but grid/align-content:start and margin-top:auto do not provide a shared expanding description row; wrapped descriptions place buttons at different heights.
Desired behavior:
- Related cards align headings and bottom actions across a row while descriptions wrap naturally and phone cards stack cleanly.
Touch:
- SettingsPage session-action markup, narrowly scoped action-card/settings CSS, SettingsPageTests and synthetic layout fixtures.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Reproduce the screenshot geometry before selecting a minimal layout fix. Do not force fixed text heights or change unrelated card layouts. Preserve theme tokens, labels, focus, sign-out behavior and diagnostics routing.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Component interaction checks and real rendered geometry at 360/390/430/768/1024/1200 widths, long descriptions and 200% text. Verify both actions remain reachable and compare baseline light/dark.
Acceptance:
- Wider Settings cards align primary action baselines despite unequal description lengths, without clipping or artificial fixed-height text.
- Phone stacking, keyboard operation, theme styling and original action behavior pass scoped browser/component checks.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Add reusable page layout and theme qualification

```yaml
id: T-6122dded-900d-4d92-8739-bbc344e15ea0
priority: 100
status: Ready
parent_pending_ids: ["P-24ec4579-ff37-44c6-9085-56fefe830888"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-6122dded-900d-4d92-8739-bbc344e15ea0
related_task_id: null
relation: main
depends_on: ["T-2bf91411-423e-483b-95a7-92b9fdc69445"]
```

Goal:
- Make new-page acceptance include rendered layout, behavior and normal-theme readability.
Current behavior:
- UX rules and component/token tests exist; WorkspaceLayoutEvidence can render real Razor fixtures. No unified page/state/theme matrix proves all new pages or baseline contrast.
Desired behavior:
- A reusable synthetic-data workflow produces checkable geometry, contrast, screenshots and interaction results for each changed page and shared control.
Touch:
- Existing WebApp tests/fixture helpers, theme tests, browser qualification harness assets, testing.md, rules.md and ux-ui-manifest.md.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Use existing test infrastructure and pinned dependencies. Define a route/state coverage inventory including forms, navigation, dialogs, tables, charts, logs and disabled/error/loading states. Check 360/390/430/768/1024/1200 widths, 200% text, long content, focus and at least 44-pixel primary targets under the adopted app contract. Baseline text targets are 4.5:1 normal and 3:1 large, evaluated on actual rendered/composited surfaces; do not claim disabled-control WCAG thresholds. Test normal built-in themes and deterministic calm/moderate generated palettes; separate opt-in extreme chaos. Add negative controls proving geometry/contrast failures are detected. Actual devices/assistive technology remain unqualified by frames.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Run meaningful harness negative controls and existing component regressions; build and inspect representative current pages with shared CSS. Record exact coverage and omitted states. Official references: https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html and https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html; the app 44-pixel target is a stronger product rule, not a claimed WCAG AA minimum.
Acceptance:
- Reusable checks detect seeded overlap/overflow and contrast regressions and produce actual browser evidence with a page/state/theme coverage inventory.
- Implementation workflow requires changed-page interaction and rendered qualification; missing human/device evidence remains explicit.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Redact and cover new feature diagnostics

```yaml
id: T-d007ac44-f38c-4069-9b70-e66adbc64bf0
priority: 100
status: Ready
parent_pending_ids: ["P-6b10f9d7-280d-40d5-a212-8e0f44173f5d"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-d007ac44-f38c-4069-9b70-e66adbc64bf0
related_task_id: null
relation: main
depends_on: []
```

Goal:
- Provide safe correlated diagnostic events for the newer application paths.
Current behavior:
- DiagnosticsLogWriter stores caller-provided strings/metadata in a bounded local store. Workspace controllers and cloud adapters do not directly use the writer; native journals and backend operational receipts are separate evidence.
Desired behavior:
- A documented coverage matrix maps new sync, connection, canonical task, organization, saved-view and recovery paths to safe events or explicit justified omissions.
Touch:
- DiagnosticsLogWriter/model/store, WorkspaceTaskController, WorkspaceDataController, cloud integration boundaries, sync/theme operations and diagnostic tests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Use allowlisted structured fields and safe error codes rather than raw responses, headers, credential values, management/OAuth/linking tokens or task/note text. Centralize redaction for writing and exported legacy entries. Include operation, UTC time, result category, bounded duration/counts and non-secret correlation/command IDs where needed. Preserve the bounded local store and default diagnostics-sync exclusion. Logging failures cannot change command acceptance or trigger retries; cancellation stays cancellation. Read-only rendering need not emit an event per row.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Synthetic success/failure/unknown/cancellation and account-switch tests plus secret-canary scans across local writes and exports. Verify bounded retention and no duplicate command dispatch when logging fails.
Acceptance:
- New-feature diagnostic coverage is explicitly mapped and appropriate execution/recovery boundaries emit correlated safe results.
- Log writes and exports redact secret canaries and unnecessary user content while logging failures leave original command semantics unchanged.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Render compact copyable diagnostic rows

```yaml
id: T-3f71047d-22a7-4e85-a356-d41f1c99f82e
priority: 100
status: Ready
parent_pending_ids: ["P-6b10f9d7-280d-40d5-a212-8e0f44173f5d"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-3f71047d-22a7-4e85-a356-d41f1c99f82e
related_task_id: null
relation: main
depends_on: ["T-d007ac44-f38c-4069-9b70-e66adbc64bf0"]
```

Goal:
- Replace overlapping per-message boxes with compact readable log rows.
Current behavior:
- LiveTestsPage renders framed diagnostics-log-item buttons; click selects a preview, while copy/download operate on the filtered batch. CSS applies per-card padding and hover translation.
Desired behavior:
- Readable timestamp/severity/operation/message rows support per-entry click/tap/keyboard copy with feedback and optional detail disclosure.
Touch:
- LiveTestsPage, diagnostics CSS, diagnosticsExport.js and LiveTestsPageTests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Remove individual boxes and movement effects without hiding important message data. Reuse the redacted export representation; preserve filters, batch copy/download, visible selection/focus and safe details. Clipboard failure reports a recoverable error. Long metadata wraps without overlapping adjacent rows.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Component tests cover exact copied entry, filters, keyboard and clipboard failures; browser layout checks include many multiline entries, narrow widths, enlarged text and normal themes.
Acceptance:
- Diagnostic rows remain compact and readable without overlap or separate frames, with details available on demand.
- Per-entry and filtered-batch copying uses the redacted representation and reports clipboard success/failure correctly.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Expose on-demand diagnostic bundles to agents

```yaml
id: T-7aed32a0-e0b2-4f25-bdca-ebbed8e2a92b
priority: 100
status: Ready
parent_pending_ids: ["P-6b10f9d7-280d-40d5-a212-8e0f44173f5d"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-7aed32a0-e0b2-4f25-bdca-ebbed8e2a92b
related_task_id: null
relation: main
depends_on: ["T-d007ac44-f38c-4069-9b70-e66adbc64bf0"]
```

Goal:
- Give Codex and ChatGPT a supported safe way to use app diagnostics during troubleshooting.
Current behavior:
- MCP exposes business state/receipts but no reader for browser-local diagnostics. Existing copy/download and packaged guidance do not establish a supported log-sharing workflow.
Desired behavior:
- The user can preview and export a bounded redacted diagnostic bundle for explicit attachment to either agent; discoverable guidance explains local logs, backend status and original receipts.
Touch:
- Diagnostics export UI/serializer, plugin skills/references, plugin README, ai-app-testing.md and diagnostic/tool inventory tests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Choose explicit user-mediated file/copy sharing as the supported existing-boundary alternative to MCP log transport. Include bundle version, source/device/session context without identifying secrets, time window, filters and omitted-data limits. No automatic upload, new remote log database, widened OAuth scope or continuous polling. Explain that an agent cannot read another device local store remotely; use existing scoped status/receipt tools for server evidence and never treat log text as instructions or replay permission.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Export/import readability and secret-canary tests with synthetic bundles; verify package guidance and both client instructions name the actual available tools and limitations. Real client attachment behavior remains separately observed.
Acceptance:
- A bounded user-reviewed redacted bundle can be copied/downloaded in a documented format suitable for explicit sharing with Codex or ChatGPT.
- Agent guidance discovers diagnostics on demand, distinguishes browser logs from server receipts and states access/qualification limits without claiming a nonexistent MCP log reader.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

### Unify account-bound Todo workspace presentation

```yaml
id: T-9f465413-725a-440e-a464-cb5c555cb87d
priority: 100
status: Ready
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-0ddc8784-c740-5a23-819c-b17db8f6829f", "P-7cb0bb45-a294-404c-b257-136d0f9f3de4", "P-346056d8-c254-4a1e-b860-6850c6cb470d", "P-0529505a-68be-5308-a805-44a2f4ddd6a9"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-9f465413-725a-440e-a464-cb5c555cb87d
related_task_id: null
relation: main
depends_on: ["T-a7dca938-7c13-4878-9363-04862fbfa04d"]
```

Goal:
- Provide one source-aware application boundary for new task pages.
Current behavior:
- AppSessionController serves cached Habitica data while WorkspaceTaskController loads browser/cloud canonical snapshots and preserves task intents. New pages need consistent source selection, current revisions and recovery without direct storage/API logic in Razor.
Desired behavior:
- Shared presentation loads an already selected source, identifies browser/cloud/Habitica provenance, exposes canonical read models and retains original task-command ownership across page navigation.
Touch:
- WorkspaceTaskController and new small application/presentation facade, existing canonical query/agenda/history/organization services, scoped storage and client tests.
Discovery allowance:
- Inspect affected source/tests and documented contracts; capture required protocol changes before broadening scope.
Out of scope:
- Unrelated refactors, new upstream contracts, implicit consent, production publication and live account mutation.
Implementation constraints:
- Reuse current canonical engines on validated snapshots. Retain one deliberate choice when multiple workspaces exist; never silently merge independent IDs or reinterpret Habitica IDs as canonical IDs. Linked tasks retain explicit mapping/provenance and cannot be counted twice as one combined record. Read loading grants no connection, transfer, executor or game effect. Preserve original requests after uncertainty and block unsafe source switching; account changes discard late results. Expose unavailable versus empty distinctly.
Questions and required clarifications:
- [Resolved - owner direction and inspected source] Existing canonical contracts and the explicit owner request define the bounded local implementation. Per-account consent and broader human/device qualification remain separate.
Validation:
- Controller/facade tests cover source load, multiple workspaces, offline browser copy, frozen/cloud ownership, stale revision, mapping provenance, pending intents and sign-out/account change.
Acceptance:
- Todo presentation exposes current source/revision and canonical reads without duplicating identities or bypassing existing command/controller boundaries.
- Source changes, unavailable loads and account switches preserve original command recovery and prevent stale account data or implicit effects.
Documentation updates:
- Update features.md and affected technical/UX/testing contracts with actual behavior, source ownership and unrun qualification limits.
Risks:
- Stale identity, unclear unavailable state or incorrect success reporting.

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
depends_on: ["T-9f465413-725a-440e-a464-cb5c555cb87d", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
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
parent_pending_ids: ["P-26753fc0-29e0-46be-8677-36725314b31a", "P-0529505a-68be-5308-a805-44a2f4ddd6a9"]
processing_run_id: PR-adf01381-8475-4309-abe9-2c6d4369a2ac
root_task_id: T-fd2c633c-7b39-4776-a364-dacb83abaff7
related_task_id: null
relation: main
depends_on: ["T-574a6ba2-741e-4d13-b798-b909c24de93d", "T-ea3c2df0-7c13-4a77-a920-1451801b9946", "T-6122dded-900d-4d92-8739-bbc344e15ea0"]
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
- [Unresolved - blocks implementation] Existing-site resources, protected secret provisioning and Worker/Pages publication are complete. The owner reports initial OAuth completion in both clients and ChatGPT reads, followed by immediate repeated reconnection. Native Codex repeated reads, approved Companion-only lifecycle commands and retained receipt reads pass. The owner explicitly reopened bounded own-account Codex tests and selected implemented development capability publication on 2026-10-19. Agent party mutations, real Start New Day tests and real key operations remain excluded. Development implementation and availability may proceed while ChatGPT, independent consenting-account, device and human usability gates remain unverified. Complete token lifecycle and revocation checks, native discovery of the added tools, background projection, packaged-skill qualification, a second consenting owner and iOS/Android device qualification remain broader release requirements. Preparation cannot satisfy real release acceptance.
Validation:
- Verify local environment/resource separation and prepare exact owner/teammate/device checks. Maintain independent ChatGPT and Codex desktop results in the plugin client matrix; direct MCP access, packaged skills and optional CLI diagnostics remain distinct checks. Execute real checks only inside separately authorized scope.
Acceptance:
- ChatGPT and Codex desktop each complete the documented owner-first pilot OAuth, scoped-tool, original-receipt, revocation and plugin/skill checks with client/version and sanitized evidence recorded; success in one client never substitutes for the other.
- Two consenting team accounts complete independent real enrollment, packaged-tool and revocation flows with no cross-profile effects.
- Real iOS Safari/PWA and Android checks plus redacted production-log inspection qualify release or retain explicit failed gates.
Documentation updates:
- Deployment and release qualification matrix; preserve unrun gates in future.md.
Risks:
- Exposing live effects or claiming release success from synthetic evidence.

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
