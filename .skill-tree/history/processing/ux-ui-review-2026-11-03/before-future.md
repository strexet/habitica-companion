# Future Work

Task definitions reside in `.skill-tree/documents/future.md`; `future.json` contains the generated index and authoritative active checkpoint. `.skill-tree/skill-tree.json` indexes document ownership and freshness. Current behavior belongs in features.md; architecture belongs in technical.md. Research reference material never supplies implementation selection or authorization.

## Implementor Rules

1. Read the relevant source-of-truth docs before editing:
   - UI/UX: `.skill-tree/documents/ux-ui-manifest.md`
   - Architecture, sync, storage: `.skill-tree/documents/technical.md`
   - Habitica API rules: `.skill-tree/documents/habitica-api.md`
   - Habitica party/quest link behavior: `.skill-tree/documents/habitica-deeplinks.md`
   - Cloudflare deployment and D1/KV: `.skill-tree/documents/deploy-cloudflare-pages.md`
2. Implement one entry only. Do not bundle unrelated cleanup, renames, or opportunistic refactors.
3. Respect `Touch:` and `Discovery allowance:`. Research missing paths before implementation; capture required scope changes through the selected controller instead of silently broadening the task.
4. Add or update tests next to affected code. UI behavior changes need Razor component tests where similar tests exist.
5. User-facing behavior changes must update `.skill-tree/documents/features.md`; sync architecture or backend behavior changes must update `.skill-tree/documents/technical.md`.
6. Schema changes need the next numbered migration in the owning database directory and a deployment-doc update. Existing party D1 uses `migrations/`; the proposed integration database uses `workers/chatgpt-auth/migrations/`. Never mix credential-vault migrations into party D1.
7. Never send Habitica API tokens to Cloudflare party-sync or app-data sync endpoints.
8. Keep labels short and plain. If UI copy is ambiguous, choose the smallest clear label and proceed.
9. If a needed Habitica endpoint is not documented in `.skill-tree/documents/habitica-api.md`, stop and add a follow-up entry instead of guessing.
10. For Habitica party/quest links, use stable web URLs only. Do not add `habitica://`, Android `intent://`, app-opening probes, or mobile-app-specific party/quest links unless `.skill-tree/documents/habitica-deeplinks.md` is updated with new official support.
11. Process Pending performs research only. Standalone processing reports the prioritized list and presents one recommendation last; implementation requires an actual developer selection. Explicit Work Through Next or Implement All continues only inside the selected root or frozen snapshot.
12. Select the highest numeric prioritized priority; document order breaks ties. A blocked highest-ranked task stops default selection. Exact selection never falls back to another task.
13. Record acceptance and terminal outcomes before removing tasks. An empty queue or absent task never proves completion.
14. Task dependencies use stable T- identities; Pending epic gates require researched promotion and recorded completion before dependent implementation. Readiness grants no live API, deployment, credential, commit or publication permission.


## Companion-First Integration Planning Gates

- `.skill-tree/documents/future.md` is the **only active implementation queue**. The architecture and [competitive mobile UX research](companion-first-mcp-research.md) are reference material, not a second queue.
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
- Owner-requested transfer from Companion Development, alias codev, project c5f8a175-2bee-484a-9b72-018737dc10fd, task fe2403b8-ab96-489f-b6ac-5e72f9dee4a9, task revision 115, workspace revision 158. Complete original text: .skill-tree/history/intake/codev-2026-10-28/source-tasks.json. Source completion acknowledges intake only.

Priority rationale:
- Preserve source urgent P0 ordering above the current highest open priority 105; no live spell execution or release authorization is implied.

Processing blocker:
- Static reinspection confirms acknowledged-count, quota/cooldown, unknown-outcome and original gear recovery controls remain implemented; accepted outcomes T-b89fac23-3ac2-4c64-8560-be7a5d796a81 and T-e7d542d4-4a47-4155-afc9-ab0dfb457fa0 already cover the feature scope. Fresh loaded PWA build identity and sanitized iPhone evidence remain unavailable; any live reproduction requires a bounded owner profile. No duplicate implementation or deployed incident closure follows. See .skill-tree/history/processing/note-folders-2026-10-29/research.md.

Processing blocker:
- Current controller, adaptive pacing, recovery journal and accepted synthetic outcomes already cover acknowledged counts, 429/cooldown, unknown dispatch and exact gear recovery. The 2026-10-29 release record reports publication but no live spell reproduction; fresh loaded iPhone build/controller identity and sanitized post-update incident evidence remain missing. Obtain the affected-client observations and a concrete bounded owner profile before any real spell/equipment reproduction. No duplicate implementation or incident closure follows; research: .skill-tree/history/processing/ios-startup-2026-10-30/research.md.

Processing blocker:
- Fresh static inspection confirms serialized adaptive pacing, acknowledged17of40/429 controls and account-bound explicit gear recovery. The release report excludes live spell reproduction; affected-client build/controller identity and sanitized post-update iPhone evidence remain unavailable. No residual source defect or duplicate implementation is established. Obtain affected-client observations before a separately scoped live reproduction; research: .skill-tree/history/processing/session-workspace-2026-10-30/research.md.

### Explain workspace readiness and disabled editor recovery

```yaml
id: P-10dedd84-9a90-447e-bf89-c3dcb81affc7
priority: 105
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- The reviewed session initially shows Restoring Workspace, then Browser Workspace revision 1 with disabled task, project, saved-list and refresh controls. Generic correspondence text does not identify the actual condition preventing editing. Settings separately reports unverified existing server access.

Expected behavior:
- Show the observed source, loading or terminal unavailable state and precise read-only reason together with one relevant recovery action. Preserve browser recovery copies, original pending commands and independent consent; never silently select a different cloud source or enable execution.

Current context:
- TodoWorkspacePresentation combines coordinator availability, command read-only state and organization fences. Process Pending must establish the exact cause of the observed disabled state before selecting a behavioral fix; no data loss or automatic fallback is established. Source ownership: src/Habitica.WebApp/State/TodoWorkspacePresentation.cs:85, src/Habitica.WebApp/State/CompanionConnectionCoordinator.cs, tests/Habitica.WebApp.Tests/Workspace/TodoWorkspacePresentationTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F04 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 04-today-auth-desktop-light, 23-dailies-desktop-light, 30-sync-desktop-light, 06-settings-desktop-light.

Priority rationale:
- Audit ordering: high-impact navigation, readiness or enlarged-text access. Existing priority106 spell incident remains above the audit intake.

### Make global navigation identifiable and keyboard accessible

```yaml
id: P-8e0bae93-5539-40e5-b2b7-8b52fd5fbcea
priority: 105
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- The authenticated shell menu button has no accessible name or expanded-state association. The navigation precedes more than twenty destinations without a skip-to-content link.

Expected behavior:
- Expose a meaningful menu name and current expansion state, provide a keyboard shortcut through a visible-on-focus skip link, and verify phone drawer dismissal and focus return without changing account permissions.

Current context:
- MainLayout.razor renders MudIconButton without Label, aria-label or aria-expanded. The deployed accessibility tree exposes an unnamed button; no focus-trap failure is claimed. Source ownership: src/Habitica.WebApp/Layout/MainLayout.razor:13, tests/Habitica.WebApp.Tests/Qualification/SharedLayoutEvidenceTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F01 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 02-dashboard-desktop-light, 18-navigation-phone-light.

Priority rationale:
- Audit ordering: high-impact navigation, readiness or enlarged-text access. Existing priority106 spell incident remains above the audit intake.

### Keep selected native dates readable with enlarged text

```yaml
id: P-b302aa68-7d63-4f74-9aa7-338c56700923
priority: 104
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- At 360 pixels and 200-percent text, Today allocates about 105 pixels to a native date input with a 30.4-pixel font. The selected date disappears and only the calendar affordance remains, although the input retains a valid date value.

Expected behavior:
- Reflow the date and Today action so the selected civil date remains readable and editable at phone widths and enlarged text. Extend rendered checks to native input content visibility rather than relying only on outer control bounds.

Current context:
- todo-day-controls uses minmax(0, 1fr) beside an auto-width button, with min-width zero on the input. The affected fixture passes current geometry and text sampling because native input text is outside the text-node contrast inspection. Source ownership: src/Habitica.WebApp/wwwroot/css/app.css:4679, src/Habitica.WebApp/Components/TodoTodayPanel.razor:11, tests/browser-qualification/qualification-frame.mjs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F20 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 44-local-today-phone-obsidian-200, 45-local-today-phone-light-100.

Priority rationale:
- Audit ordering: high-impact navigation, readiness or enlarged-text access. Existing priority106 spell incident remains above the audit intake.

### Provide page headings and route focus for Habits and Dailies

```yaml
id: P-bf4b432a-db9b-4315-acd0-7edfb6618aa6
priority: 104
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- The Habits and Dailies routes render the page titles as level-two headings and contain no level-one page heading. The shared shell heading precedes the content in the heading outline.

Expected behavior:
- Each route exposes one descriptive page-level heading and receives useful focus after navigation. Category tabs and immediate capture remain available.

Current context:
- TodoHabitsPage.razor and TodoDailiesPage.razor delegate the visible title to TodoTaskPanel. TodoCategoryPageTests cover the route and actions without asserting the page-level heading. Source ownership: src/Habitica.WebApp/Pages/TodoHabitsPage.razor, src/Habitica.WebApp/Pages/TodoDailiesPage.razor, tests/Habitica.WebApp.Tests/Workspace/TodoCategoryPageTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F02 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 22-habits-desktop-light, 23-dailies-desktop-light, 48-local-daily-phone-light-100.

Priority rationale:
- Audit ordering: high-impact navigation, readiness or enlarged-text access. Existing priority106 spell incident remains above the audit intake.

### Reconcile dashboard account freshness and refresh-history labels

```yaml
id: P-d3dd549c-426f-4e67-9061-df88247cba31
priority: 103
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- Dashboard simultaneously shows Account data is up to date and Account not refreshed after successful sign-in. The snapshot freshness banner and refresh-history chip use different state sources without explaining the distinction.

Expected behavior:
- Present a coherent account-readiness summary, distinguishing data loaded during sign-in from later refresh attempts where relevant. Do not manufacture missing timestamps or claim a successful refresh from absent evidence.

Current context:
- DashboardPage derives the banner from UserFreshness and the Account chip from optional UserProfile DomainRefreshState. A missing domain entry currently renders not refreshed even while a fresh account snapshot is visible. Source ownership: src/Habitica.WebApp/Pages/DashboardPage.razor:72, src/Habitica.WebApp/Pages/DashboardPage.razor:1305.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F24 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 02-dashboard-desktop-light, 57-dashboard-desktop-obsidian, 64-dashboard-phone-light.

Priority rationale:
- Audit ordering: high-impact navigation, readiness or enlarged-text access. Existing priority106 spell incident remains above the audit intake.

### Bound companion collection rendering and clarify search results

```yaml
id: P-2897d6c0-a804-41fe-9d92-091049683fa3
priority: 103
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- The reviewed expanded Pets and Mounts state produces about 231507 pixels of desktop content and 490536 pixels on a phone. Search reduces the catalog but offers no aggregate result count or single empty-result recovery; separate group notices and unfiltered surplus inventory remain lengthy.

Expected behavior:
- Keep large expanded collections bounded and navigable, provide clear aggregate search feedback and a simple reset, and distinguish collection results from separate inventory-sale planning. Preserve explicit hatch, feed and sale previews.

Current context:
- PetsMountsPage renders matching pets and mounts inside each group and persists folds. The observed starting state includes saved expansion choices; no claim about a fresh profile default is made. Individual empty-group messages already exist. Source ownership: src/Habitica.WebApp/Pages/PetsMountsPage.razor:348, src/Habitica.WebApp/Pages/PetsMountsPage.razor:655, tests/Habitica.WebApp.Tests/Pages/PetsMountsPageTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F07 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 10-pets-desktop-light, 11-pets-no-results-desktop-light, 53-pets-phone-obsidian, 66-pets-phone-light.

Priority rationale:
- Audit ordering: high-impact navigation, readiness or enlarged-text access. Existing priority106 spell incident remains above the audit intake.

### Put daily work before optional reminder setup on Today

```yaml
id: P-1e35a85f-82f8-48cd-93a5-001c8fd6f539
priority: 103
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- Today places a full reminder explanation and repeated workspace correspondence text before the task groups. In the populated synthetic phone state, the first task starts several screens below the date controls; enlarged text increases the distance substantially.

Expected behavior:
- Keep the selected day and daily task groups easy to reach. Present optional reminder setup and detailed source explanations through concise summaries or disclosures, while retaining urgent readiness, pending-command and unknown-outcome notices.

Current context:
- TodoTodayPanel renders TodoReminderPanel before todo-today-groups. The current 360-pixel synthetic capture places the first task near document y=1300 at normal text and y=5442 at 200-percent text, including the synthetic evidence notice. Source ownership: src/Habitica.WebApp/Components/TodoTodayPanel.razor:17, src/Habitica.WebApp/Components/TodoReminderPanel.razor.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F05 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 45-local-today-phone-light-100, 44-local-today-phone-obsidian-200.

Priority rationale:
- Audit ordering: high-impact navigation, readiness or enlarged-text access. Existing priority106 spell incident remains above the audit intake.

### Use consistent canonical task destinations from planning views

```yaml
id: P-4870adee-f7e9-4dd3-b4c1-71b2b6b33635
priority: 103
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- Inbox and Planning Add task links lead to /tasks, while Today Add task leads to /todo/tasks. History and Statistics also return to the mixed legacy /tasks surface, which shows both Companion capture and separate Habitica records.

Expected behavior:
- Ordinary Companion capture and return links use the canonical task surface consistently. Intentional links to the separate Habitica cache remain explicitly named and retain separate identity and reward semantics.

Current context:
- TodoPlanningPanel.razor, TodoHistoryPanel.razor and TodoStatisticsPanel.razor hardcode /tasks. The /tasks route still contains a Companion editor, so the issue is context and navigation consistency rather than missing capture functionality. Source ownership: src/Habitica.WebApp/Components/TodoPlanningPanel.razor:10, src/Habitica.WebApp/Components/TodoHistoryPanel.razor:9, src/Habitica.WebApp/Components/TodoStatisticsPanel.razor:10.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F03 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 26-inbox-desktop-light, 27-planning-desktop-light, 33-habitica-tasks-desktop-light.

Priority rationale:
- Audit ordering: high-impact navigation, readiness or enlarged-text access. Existing priority106 spell incident remains above the audit intake.

### Keep disabled control text readable in built-in themes

```yaml
id: P-b7836285-3b2f-434b-8716-cd3e78482dda
priority: 102
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- Disabled buttons become very faint in Gryphy Light and remain dim in Obsidian Glow. Normal-looking disabled Daily schedule inputs and low-visibility action labels make editor readiness harder to understand.

Expected behavior:
- Use readable disabled foreground, border and surface tokens with a clear noninteractive appearance and relevant disabled reasons. Verify actual disabled controls in light and dark themes at normal and enlarged text.

Current context:
- app.css uses disabled tokens, including translucent foregrounds, and applies additional opacity to Notes buttons. The rendered matrix intentionally skips disabled text contrast, so the passing matrix does not qualify disabled readability. No WCAG text-contrast violation is asserted for inactive controls. Source ownership: src/Habitica.WebApp/wwwroot/css/app.css:3751, src/Habitica.WebApp/wwwroot/css/app.css:4749, tests/browser-qualification/qualification-frame.mjs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F19 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 23-dailies-desktop-light, 41-tasks-phone-obsidian, 45-local-today-phone-light-100.

Priority rationale:
- Audit ordering: frequent interaction clarity and readable controls. Existing priority106 spell incident remains above the audit intake.

### Give repeated record controls contextual names and disclosure state

```yaml
id: P-fa41f9c2-ce38-45c2-a42f-f3831eb48a45
priority: 102
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- Task Complete and Details, gear Equip item, member Details and Settings Exclude controls repeat without record-specific accessible names. Member detail toggles also omit expanded-state and controlled-region associations; preset-name fields rely on placeholders.

Expected behavior:
- Keep short visible labels while exposing the affected record or sync section in accessible names, persistent input labels and disclosure state. Verify keyboard and screen-reader-oriented navigation without changing action authorization.

Current context:
- The current accessibility snapshots and Razor source expose repeated generic controls. Duplicate button text alone is treated as a navigation risk rather than a blanket compliance failure; the unnamed global menu remains a separate finding. Source ownership: src/Habitica.WebApp/Pages/PartyPage.razor:795, src/Habitica.WebApp/Pages/InventoryPage.razor:237, src/Habitica.WebApp/Pages/SettingsPage.razor:270.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F18 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 09-equipment-expanded-desktop-light, 13-party-details-desktop-light, 33-habitica-tasks-desktop-light, 06-settings-desktop-light.

Priority rationale:
- Audit ordering: frequent interaction clarity and readable controls. Existing priority106 spell incident remains above the audit intake.

### Identify the exact invalid saved-list fields and preserve focus

```yaml
id: P-4cc94496-9709-4454-9fd6-ae1f0227a1b7
priority: 102
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- Saved-list validation can return one broad paragraph listing date ordering, relative presets, timezone, search length, stage and source constraints. The paragraph does not identify the specific invalid field or associate the error with the affected control.

Expected behavior:
- Name the actual invalid constraint beside the affected field, expose the association to assistive technology, preserve entered values and move or link focus usefully. Retain rejection of conflicting dates and unsupported definitions.

Current context:
- TodoSavedListDraft.Build throws a generic aggregate instruction when query validity fails. TodoSavedListPanel renders the message as a top-level alert without field-level invalid state. Source ownership: src/Habitica.WebApp/State/TodoSavedListDraft.cs:45, src/Habitica.WebApp/Components/TodoSavedListPanel.razor:12.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F12 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 46-local-lists-phone-light-100.

Priority rationale:
- Audit ordering: frequent interaction clarity and readable controls. Existing priority106 spell incident remains above the audit intake.

### Place required Daily schedule fields before the add action

```yaml
id: P-d3155b74-ab23-4ed6-8073-e794af664988
priority: 102
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- Daily capture places Add Daily immediately after the title and before required repeat, interval, start-date and timezone fields. On phones the primary action appears before the information needed to submit a valid Daily.

Expected behavior:
- Present Daily capture as one ordered form with required schedule inputs preceding the primary review or add action. Preserve title-first Todo and Habit capture, explicit schedule semantics and required Habitica correspondence.

Current context:
- The current linked-daily-editor capture shows the same action ordering as the deployed /todo/dailies route. No schedule backfill, Cron or provider penalty change is authorized. Source ownership: src/Habitica.WebApp/Components/TodoTaskPanel.razor, tests/Habitica.WebApp.Tests/Workspace/TodoTaskPanelTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F09 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 23-dailies-desktop-light, 48-local-daily-phone-light-100.

Priority rationale:
- Audit ordering: frequent interaction clarity and readable controls. Existing priority106 spell incident remains above the audit intake.

### Distinguish unchecked push capability from unsupported browsers

```yaml
id: P-1ffff778-5db1-447c-95f7-fde01d72e70c
priority: 101
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: null
related_task_id: null
relation: main
depends_on: []
```

Description:
- Before Check push setup runs, the push panel displays Browser capability unprepared and Permission unsupported together. No browser capability observation has established unsupported permission at that point.

Expected behavior:
- Use an explicit not-checked state until capability is observed, then distinguish available, installation-required, denied and unsupported outcomes. Permission prompts remain restricted to the explicit device action.

Current context:
- PushDeviceController initializes BrowserPushCapability with unprepared and unsupported. The issue is status presentation, not evidence of failed notification delivery. Source ownership: src/Habitica.WebApp/State/PushDeviceController.cs:18, src/Habitica.WebApp/Components/PushDevicePanel.razor:11.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F16 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 31-reminders-desktop-light.

Priority rationale:
- Audit ordering: frequent interaction clarity and readable controls. Existing priority106 spell incident remains above the audit intake.

### Show explicit unset values in task-copy reviews

```yaml
id: P-16d346af-15c0-4b5f-9d5a-b80d50d2259f
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
- Task type-copy review prints empty values between labels for original planned date, deadline, review, parent, dependencies and target organization fields. Empty separators make absent data difficult to distinguish from an incomplete review.

Expected behavior:
- Render clear None or Not set values for absent fields and empty collections, with a scannable before-and-after summary. Retain exact source and target identities, copied and reset disclosures and original receipt recovery.

Current context:
- TaskTypeCopyReview interpolates nullable values and empty string.Join results directly. The current Daily copy screenshot shows labels followed only by separators. Source ownership: src/Habitica.WebApp/Components/TaskTypeCopyReview.razor:7, tests/Habitica.WebApp.Tests/Workspace/TaskTypeConversionPanelTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F23 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 49-local-type-copy-phone-obsidian-100.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Format private-sync timestamps without literal format suffixes

```yaml
id: P-23e38a95-a8c7-4748-b8bf-6f1bcfcda84b
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
- Settings cloud-sync rows display timestamps ending in a literal colon-g suffix, including the offset followed by :g. The presentation leaks the Razor formatting expression into visible account metadata.

Expected behavior:
- Render concise valid localized timestamps with a clear date and time context and no literal format markers. Preserve UTC storage and the original synchronization state.

Current context:
- SettingsPage renders status.UpdatedAtUtc.ToLocalTime() followed by :g outside a formatted expression. Existing component tests pass without asserting the final timestamp string. Source ownership: src/Habitica.WebApp/Pages/SettingsPage.razor:261, tests/Habitica.WebApp.Tests/Pages/SettingsPageTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F22 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 06-settings-desktop-light.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Prevent mid-word wrapping in equipment count summaries

```yaml
id: P-ae822766-780c-4584-a33f-d8978ee060c0
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
- At desktop width, the equipment item-count card gives the Hatching potions label a very narrow column beside the image and count. The label breaks into fragments such as Hatchi, ng, potio and ns in both reviewed built-in themes.

Expected behavior:
- Keep summary labels readable through appropriate wrapping or stacking without crowding counts and artwork. Verify narrow cards at desktop and phone widths, long labels and enlarged text.

Current context:
- InventoryPage RenderImageCountChip composes a fixed art slot, numeric count and text inside a nested summary grid. The symptom is visible at 1280 pixels, not only on phones. Source ownership: src/Habitica.WebApp/Pages/InventoryPage.razor:75, src/Habitica.WebApp/wwwroot/css/app.css:899.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F21 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 08-equipment-desktop-light, 58-equipment-desktop-obsidian.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Use named day controls for repeating push reminders

```yaml
id: P-a8c875ae-145d-4273-9d4b-fbf76d22e7f8
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
- Weekly reminder setup asks for comma-separated weekday numbers from zero through six. Monthly setup similarly relies on a raw comma-separated list. Ordinary schedule selection requires knowledge of transport-oriented encodings.

Expected behavior:
- Offer named weekday choices and clear month-day selection with specific validation and a readable review. Preserve exact recurrence, timezone, DST, end-date, device consent and original-command behavior.

Current context:
- TaskPushReminderEditor uses text inputs for schedule-days and schedule-month-days. TodoRecurringEditor already provides named weekday checkboxes as a nearby interaction pattern. Source ownership: src/Habitica.WebApp/Components/TaskPushReminderEditor.razor:28, src/Habitica.WebApp/Components/TodoRecurringEditor.razor:27.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F17 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 51-local-reminders-phone-obsidian-100.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Organize Settings around clear account and data-management sections

```yaml
id: P-497c699c-dd18-4f62-a9ba-d3bccfc2cda0
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
- Settings begins with iPhone installation guidance before the page-level account heading and stacks session, appearance, integration, shared planning, three backup systems and destructive local clearing in one long page. The reviewed phone state is about 6467 pixels tall.

Expected behavior:
- Provide scannable section navigation and plain distinctions between browser data, private device sync and readable cloud Workspace data. Keep current connection state and likely next actions visible while preserving independent consent and destructive confirmation boundaries.

Current context:
- Existing Settings Danger Zone Confirmation covers destructive confirmation, not Settings information architecture. Returning User Copy Compression remains separate from account and data-management grouping. Source ownership: src/Habitica.WebApp/Pages/SettingsPage.razor, tests/Habitica.WebApp.Tests/Pages/SettingsPageTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F15 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 06-settings-desktop-light, 63-settings-desktop-obsidian, 69-settings-phone-light-stable.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Open Projects on existing work and reveal management on demand

```yaml
id: P-356b4e1b-8559-4922-9ec8-fbf713f5eedb
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
- Projects places category selection, a new-project form and alias guidance before existing project records, the project task selector and linked Notes. The normal phone state spans roughly 3006 pixels before the page ends.

Expected behavior:
- Make existing projects and associated tasks and Notes the direct browsing path. Keep creation, optional organization and record management available through clear deliberate actions with the same reviews and retained identity rules.

Current context:
- TodoProjectsPage composes TodoOrganizationPanel before project task navigation. The request concerns entry hierarchy rather than changes to optional organization or project membership semantics. Source ownership: src/Habitica.WebApp/Pages/TodoProjectsPage.razor, src/Habitica.WebApp/Components/TodoOrganizationPanel.razor.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F14 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 24-projects-desktop-light, 50-local-projects-phone-light-100.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Make phone note capture reach the editor directly

```yaml
id: P-ec998b4e-f73d-47cc-a884-06b5619f717c
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
- The phone Notes view stacks source guidance, project and folder filters, library results and pagination before the editor. New note focuses the editor heading but leaves substantial preceding library and association controls on screen before the title and body.

Expected behavior:
- Provide a clear phone transition between finding notes and editing a note, with the title and writing area promptly reachable after New note. Preserve unsaved drafts, project and folder context, original receipt recovery and keyboard return paths.

Current context:
- The deployed New note interaction correctly moves focus to the editor heading; focus is not absent. Current local folder support adds further controls to the same stacked layout. Source ownership: src/Habitica.WebApp/Components/NotesPanel.razor, tests/Habitica.WebApp.Tests/Notes/NotesPanelTests.cs, tests/Habitica.WebApp.Tests/Notes/NotesFolderPanelTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F13 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 36-notes-phone-light, 37-notes-new-phone-light, 47-local-notes-phone-obsidian-200.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Shorten saved-list authoring and prioritize existing lists

```yaml
id: P-980fdd4a-8af6-47cb-a2ab-0cca80f54139
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
- Saved lists opens a complete filter editor before saved definitions and results. The current phone example is about 3387 pixels tall and exposes source, type, state, urgency, organization, mapping and fixed and relative date controls together.

Expected behavior:
- Make existing definitions and matching results easy to reach, with a compact common creation path and additional filters on demand. Preserve complete reviewed definitions, explicit date conflicts and unavailable-result safeguards.

Current context:
- TodoSavedListPanel already collapses exact saved filters in results; the authoring form remains fully expanded. Relative-date support is implemented locally and must be included in the design research. Source ownership: src/Habitica.WebApp/Components/TodoSavedListPanel.razor:30, tests/Habitica.WebApp.Tests/Workspace/TodoSavedListPanelTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F11 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 28-lists-desktop-light, 46-local-lists-phone-light-100.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Offer understandable supported timezone choices for linked Dailies

```yaml
id: P-d5ab0ee8-193d-4bc0-81e6-e0daf24ac330
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
- Linked Daily creation asks for raw UTC or Etc/GMT identifiers with reversed offset signs. The recurring editor suggests Europe/Paris even when the selected linked schedule only accepts a supported fixed zone.

Expected behavior:
- Show understandable supported offset choices and context-specific labels, retaining exact normalized timezone identity in reviews. Unsupported DST and nonmidnight day-start behavior remains explicitly unavailable rather than silently converted.

Current context:
- TodoTaskPanel and TodoRecurringEditor have different supported linked and unlinked schedule constraints. Process Pending must preserve the linked-daily-schedules contract and fixed-offset sign semantics. Source ownership: src/Habitica.WebApp/Components/TodoTaskPanel.razor, src/Habitica.WebApp/Components/TodoRecurringEditor.razor:24, .skill-tree/documents/linked-daily-schedules.md.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F10 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 48-local-daily-phone-light-100.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Add direct discovery for equipment slots and owned items

```yaml
id: P-0e537656-6bec-434b-8f6f-6f8a6fd73ff2
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
- Equipment shows loadout, optimizer, presets and repeated category grids before many owned items. The reviewed account has 291 items and an approximately 7526-pixel desktop page, with no item search or compact slot navigation.

Expected behavior:
- Provide a direct way to find an owned item or reach a slot without scanning the full page. Keep equipped identity, comparisons, optimizer choices and explicit equipment actions intact.

Current context:
- InventoryPage uses category expansion and best-in-category summaries, which should remain useful after discovery improves. No optimizer or Habitica equipment mutation change is requested. Source ownership: src/Habitica.WebApp/Pages/InventoryPage.razor, tests/Habitica.WebApp.Tests/Pages/InventoryPageTests.cs.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F08 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 08-equipment-desktop-light, 09-equipment-expanded-desktop-light, 65-equipment-phone-light.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Make primary planning navigation reachable without a long drawer scan

```yaml
id: P-49528543-08db-4727-a2ca-bf390aa3b7d5
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
- The drawer starts with a large explanatory hero and presents more than twenty destinations. At 1280 by 720 pixels, primary planning destinations and account tools require independent drawer scrolling. Naming mixes Todo, Tasks and Habitica tasks and Rewards.

Expected behavior:
- Expose a clear compact hierarchy for frequent planning and game destinations, make the active section apparent, and keep common capture and account navigation easy to find. Retain direct routes and a clearly separate Habitica cache entry.

Current context:
- The scope is shell navigation and naming. Existing Returning User Copy Compression explicitly excludes navigation redesign and remains a separate backlog item. Source ownership: src/Habitica.WebApp/Layout/MainLayout.razor:53, src/Habitica.WebApp/Components/Navigation/AppNavMenu.razor.

Source:
- Owner-requested full UX and UI review on 2026-11-03; finding F06 in .skill-tree/history/reviews/ux-ui-2026-11-03/report.md. Current-run screenshots: 02-dashboard-desktop-light, 18-navigation-phone-light, 57-dashboard-desktop-obsidian.

Priority rationale:
- Audit ordering: bounded usability or presentation defect. Existing priority106 spell incident remains above the audit intake.

### Qualify hosted callback DNS and TLS failure injection

```yaml
id: P-b1c8f9b2-fe66-406a-aa04-70aab3a38f9b
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
- Exercise owner-controlled DNS answer changes, private and mixed address rejection, invalid TLS certificates and stalled TLS handshakes against the deployed callback gateway. Independent authoritative DNS and a raw public TLS fixture are unavailable on the current provider-managed project hostnames.

Expected behavior:
- Retain source-bound hosted refusal and receiver counters for every DNS/TLS scenario when an owned fixture becomes available. Keep the production destination and certificate guards enforced; report unavailable hosted coverage explicitly. Dedicated DNS/TLS infrastructure is not a prerequisite for the owner-selected normal-delivery smoke task.

Current context:
- Independent fixture qualification deferred from selected task T-ec421773-5864-4bc5-b17f-ce04e0d818c2, with inherited completed root T-6a8db6be-b721-4b88-b62b-c6566d759080. The installed intake helper requires an open prioritized root for a structural followup; no inherited identity is rewritten. The normal-delivery revision preserves local DNS/TLS tests and production guards. No passing hosted evidence is implied.

Source:
- Direct repository-owner request in the current chat to defer unavailable hosted failure tests and check normal operation first.

Priority rationale:
- Nonblocking work deferred from the selected priority101 hosted task; the repository owner selects normal HTTPS delivery before dedicated DNS/TLS failure infrastructure.

Processing blocker:
- Owned authoritative DNS and raw public TLS fixture identities, allocation and cleanup scope remain unavailable. Existing normal HTTPS evidence cannot establish the deferred failure controls. Keep the owner-deferred hosted test Pending; no resource purchase or live scope is inferred. Research: .skill-tree/history/processing/implement-all-followups-2026-11-03/research.json.

## Prioritized Next Changes

Highest numeric priority first; unmet dependencies and blocking questions stop implementation. Imported source provenance and product decisions remain distinct from newly executed research.

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
boundary_validation: before-dependent-work
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
```json
{
  "checks": [
    {
      "id": "desktop-client-gates",
      "recipe_id": "desktop-host-qualification",
      "criteria": [
        "ChatGPT and Codex desktop each complete the documented owner-first pilot OAuth, scoped-tool, original-receipt, revocation and plugin/skill checks with client/version and sanitized evidence recorded; success in one client never substitutes for the other.",
        "Two consenting team accounts complete independent real enrollment, packaged-tool and revocation flows with no cross-profile effects.",
        "Real iOS Safari/PWA, Android, virtual keyboard, safe-area, screen-reader and authenticated competitor task-flow checks plus redacted production-log inspection qualify release or retain explicit failed gates."
      ],
      "sources": [
        ".skill-tree/documents/testing.md",
        ".skill-tree/documents/technical.md"
      ],
      "parameters": {},
      "assertion": "- Verify local environment/resource separation and prepare exact owner/teammate/device checks. Maintain independent ChatGPT and Codex desktop results in the plugin client matrix; direct MCP access, packaged skills and optional CLI diagnostics remain distinct checks. Execute real checks only inside separately authorized scope.",
      "timing": "before-dependent-work"
    },
    {
      "id": "consenting-team-gates",
      "recipe_id": "consenting-team-qualification",
      "criteria": [
        "ChatGPT and Codex desktop each complete the documented owner-first pilot OAuth, scoped-tool, original-receipt, revocation and plugin/skill checks with client/version and sanitized evidence recorded; success in one client never substitutes for the other.",
        "Two consenting team accounts complete independent real enrollment, packaged-tool and revocation flows with no cross-profile effects.",
        "Real iOS Safari/PWA, Android, virtual keyboard, safe-area, screen-reader and authenticated competitor task-flow checks plus redacted production-log inspection qualify release or retain explicit failed gates."
      ],
      "sources": [
        ".skill-tree/documents/testing.md",
        ".skill-tree/documents/technical.md"
      ],
      "parameters": {},
      "assertion": "- Verify local environment/resource separation and prepare exact owner/teammate/device checks. Maintain independent ChatGPT and Codex desktop results in the plugin client matrix; direct MCP access, packaged skills and optional CLI diagnostics remain distinct checks. Execute real checks only inside separately authorized scope.",
      "timing": "before-dependent-work"
    },
    {
      "id": "physical-device-gates",
      "recipe_id": "physical-device-qualification",
      "criteria": [
        "ChatGPT and Codex desktop each complete the documented owner-first pilot OAuth, scoped-tool, original-receipt, revocation and plugin/skill checks with client/version and sanitized evidence recorded; success in one client never substitutes for the other.",
        "Two consenting team accounts complete independent real enrollment, packaged-tool and revocation flows with no cross-profile effects.",
        "Real iOS Safari/PWA, Android, virtual keyboard, safe-area, screen-reader and authenticated competitor task-flow checks plus redacted production-log inspection qualify release or retain explicit failed gates."
      ],
      "sources": [
        ".skill-tree/documents/testing.md",
        ".skill-tree/documents/technical.md"
      ],
      "parameters": {},
      "assertion": "- Verify local environment/resource separation and prepare exact owner/teammate/device checks. Maintain independent ChatGPT and Codex desktop results in the plugin client matrix; direct MCP access, packaged skills and optional CLI diagnostics remain distinct checks. Execute real checks only inside separately authorized scope.",
      "timing": "before-dependent-work"
    }
  ]
}
```

Acceptance:
- ChatGPT and Codex desktop each complete the documented owner-first pilot OAuth, scoped-tool, original-receipt, revocation and plugin/skill checks with client/version and sanitized evidence recorded; success in one client never substitutes for the other.
- Two consenting team accounts complete independent real enrollment, packaged-tool and revocation flows with no cross-profile effects.
- Real iOS Safari/PWA, Android, virtual keyboard, safe-area, screen-reader and authenticated competitor task-flow checks plus redacted production-log inspection qualify release or retain explicit failed gates.
Documentation updates:
- Deployment and release qualification matrix; preserve unrun gates in future.md.
Risks:
- Exposing live effects or claiming release success from synthetic evidence.

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
boundary_validation: before-dependent-work
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
- Direct tests, `.skill-tree/documents/features.md`, `.skill-tree/documents/technical.md`, `.skill-tree/documents/rules.md`, UX and deployment documentation.

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
```json
{
  "checks": [
    {
      "id": "security-isolation",
      "recipe_id": "worker-focused-tests",
      "criteria": [
        "a user can fully remove server-side Habitica credentials.",
        "revocation becomes effective immediately for new tool calls.",
        "Two consenting team accounts complete the scoped enrollment/tool/revocation smoke flow with separate vault identities and no cross-profile access.",
        "no secret appears in production logs.",
        "non-opted-in Companion users remain local-only."
      ],
      "sources": [
        "workers/chatgpt-auth/test/vault.test.mjs",
        "workers/chatgpt-auth/package.json"
      ],
      "parameters": {
        "test_file": "workers/chatgpt-auth/test/vault.test.mjs"
      },
      "assertion": "- cross-user isolation.\n- OAuth revocation.\n- full credential deletion.\n- management-token rotation.\n- encryption-key rotation.\n- expired access/refresh token.\n- invalid resource/audience.\n- invalid scopes.\n- pairing brute-force controls.\n- production/preview separation.\n- log scan for secrets.\n- upstream 429 behavior.\n- account reconnect preserves profile ID.\n- In an explicitly authorized rollout environment, verify two consenting team accounts can enroll independently, use the packaged tools and revoke access without cross-profile effects.",
      "timing": "immediate"
    },
    {
      "id": "consenting-team-rollout",
      "recipe_id": "consenting-team-qualification",
      "criteria": [
        "a user can fully remove server-side Habitica credentials.",
        "revocation becomes effective immediately for new tool calls.",
        "Two consenting team accounts complete the scoped enrollment/tool/revocation smoke flow with separate vault identities and no cross-profile access.",
        "no secret appears in production logs.",
        "non-opted-in Companion users remain local-only."
      ],
      "sources": [
        ".skill-tree/documents/testing.md",
        ".skill-tree/documents/technical.md"
      ],
      "parameters": {},
      "assertion": "- cross-user isolation.\n- OAuth revocation.\n- full credential deletion.\n- management-token rotation.\n- encryption-key rotation.\n- expired access/refresh token.\n- invalid resource/audience.\n- invalid scopes.\n- pairing brute-force controls.\n- production/preview separation.\n- log scan for secrets.\n- upstream 429 behavior.\n- account reconnect preserves profile ID.\n- In an explicitly authorized rollout environment, verify two consenting team accounts can enroll independently, use the packaged tools and revoke access without cross-profile effects.",
      "timing": "before-dependent-work"
    },
    {
      "id": "production-log-redaction",
      "recipe_id": "production-log-qualification",
      "criteria": [
        "a user can fully remove server-side Habitica credentials.",
        "revocation becomes effective immediately for new tool calls.",
        "Two consenting team accounts complete the scoped enrollment/tool/revocation smoke flow with separate vault identities and no cross-profile access.",
        "no secret appears in production logs.",
        "non-opted-in Companion users remain local-only."
      ],
      "sources": [
        ".skill-tree/documents/testing.md",
        ".skill-tree/documents/technical.md"
      ],
      "parameters": {},
      "assertion": "- cross-user isolation.\n- OAuth revocation.\n- full credential deletion.\n- management-token rotation.\n- encryption-key rotation.\n- expired access/refresh token.\n- invalid resource/audience.\n- invalid scopes.\n- pairing brute-force controls.\n- production/preview separation.\n- log scan for secrets.\n- upstream 429 behavior.\n- account reconnect preserves profile ID.\n- In an explicitly authorized rollout environment, verify two consenting team accounts can enroll independently, use the packaged tools and revoke access without cross-profile effects.",
      "timing": "before-dependent-work"
    }
  ]
}
```

Acceptance:
- a user can fully remove server-side Habitica credentials.
- revocation becomes effective immediately for new tool calls.
- Two consenting team accounts complete the scoped enrollment/tool/revocation smoke flow with separate vault identities and no cross-profile access.
- no secret appears in production logs.
- non-opted-in Companion users remain local-only.

Documentation updates:
- `.skill-tree/documents/features.md`
- `.skill-tree/documents/technical.md`
- `.skill-tree/documents/rules.md`
- `.skill-tree/documents/deploy-cloudflare-pages.md`
- `src/Habitica.WebApp/Pages/PrivacyPage.razor`
- `.skill-tree/documents/ux-ui-manifest.md`

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
- `.skill-tree/documents/features.md`
- `.skill-tree/documents/ux-ui-manifest.md` if shared navigation or mode guidance changes

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
- `.skill-tree/documents/features.md`

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
- `.skill-tree/documents/features.md`
- `.skill-tree/documents/technical.md` if storage or execution architecture changes

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
- confirm each skill endpoint and target semantic against `.skill-tree/documents/habitica-api.md` and current Habitica API docs before execution support is enabled.

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
- `.skill-tree/documents/habitica-api.md` if endpoint response assumptions are added or corrected
- `.skill-tree/documents/features.md`

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
- `.skill-tree/documents/features.md`

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
- `.skill-tree/documents/ux-ui-manifest.md` if shared copy rules change

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
- `.skill-tree/documents/ux-ui-manifest.md` if table guidance changes

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
- `.skill-tree/documents/features.md`

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
