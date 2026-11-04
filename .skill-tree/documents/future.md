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

### Preserve optional validation metadata during installed workflow task rewrites

```yaml
id: P-f6d09ee5-4b46-4dd6-9843-bdd82419df43
priority: 23
status: Pending
parent_pending_ids: []
processing_run_id: null
root_task_id: T-96866c8f-9554-55d8-a638-6d8b5afaf21c
related_task_id: T-d76d2a9a-1318-4e63-9bb4-73f620b108cc
relation: followup
depends_on: []
```

Description:
- The installed rewrite_meta helper rebuilds headers from FIELDS and drops OPTIONAL_FIELDS. Adding a required dependency to a before-dependent-work parent then fails atomic validation before writing. The current controller uses a documented canonical-transaction wrapper to preserve existing optional fields without modifying installed packages.

Expected behavior:
- A separately maintained Skill Tree package preserves validation_group_id and boundary_validation across dependency and provenance rewrites, with regression coverage and an explicitly reviewed installation update.

Source:
- .skill-tree/history/processing/ux-ui-adapter-inputs-2026-11-04/metadata-preservation.json

Processing blocker:
- The installed package drops optional metadata during canonical dependency/provenance rewrites. The current operation preserves existing flags through a documented canonical-transaction wrapper. A permanent upstream Skill Tree package fix and reviewed installation update require a separate maintenance scope; no source-repository edit or implicit reinstall is authorized by the app UI run. Evidence: .skill-tree/history/processing/ux-ui-adapter-inputs-2026-11-04/metadata-preservation.json.

## Prioritized Next Changes

Highest numeric priority first; unmet dependencies and blocking questions stop implementation. Imported source provenance and product decisions remain distinct from newly executed research.

### Keep header sync states readable with enlarged phone text

```yaml
id: T-6bb354c0-1f8e-4619-8696-a4bdc4b4741e
priority: 104
status: Ready
parent_pending_ids: ["P-375aba91-d916-46f4-8310-aa7b51810a45"]
processing_run_id: PR-edb0232f-548c-4ab6-b918-e9993e00497e
root_task_id: T-d0aa6c80-6cb1-4df2-bcbd-256168fd7b12
related_task_id: T-1fa4460c-6493-446e-9bc3-bd33d976404f
relation: followup
depends_on: ["T-e2d64818-cd7a-4edb-a8a9-fb345fe1cdd0"]
```

Goal:
Expose a complete understandable sync-state label or concise summary with full detail available through an accessible explicit path, while retaining menu and Refresh access.

Current behavior:
MainLayout emits failed, stale, cloud-syncing and timestamp labels. The phone slot is bounded by25vw and sync-chip applies nowrap/hidden ellipsis. Current360/200 native screenshots still expose only a prefix. Shared header/main clearance and compact navigation outcomes preserve access but do not establish complete status wording.

Touch:
- `src/Habitica.WebApp/Layout/MainLayout.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `tests/Habitica.WebApp.Tests/Qualification/SharedLayoutEvidenceTests.cs`
- `tests/Habitica.WebApp.Tests/AppNavMenuTests.cs`

Implementation constraints:
Presentation only. Preserve status precedence, refresh ownership, busy guards and cache decisions. Do not weaken fonts, contrast, controls or qualification to hide clipping. Local synthetic tests/builds require the applicable standing owner scope. No live provider, deployment, enrollment, real permission or publication is implied.

Validation:
Use focused runnable behavior or rendered Red before the scoped fix. Preserve original failed controls and source/test/environment evidence. Missing required evidence blocks completion.
```json
{
  "checks": [
    {
      "id": "header-sync-labels-behavior",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Complete sync-state wording remains understandable for fresh, failed, stale and busy states with long times and enlarged phone text.",
        "Menu and Refresh retain44-pixel targets, visible focus and access; full status/time detail is available without relying only on hover."
      ],
      "sources": [
        "src/Habitica.WebApp/Layout/MainLayout.razor",
        "src/Habitica.WebApp/wwwroot/css/app.css",
        "tests/Habitica.WebApp.Tests/Qualification/SharedLayoutEvidenceTests.cs",
        "tests/Habitica.WebApp.Tests/AppNavMenuTests.cs"
      ],
      "parameters": {
        "test_project": "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "test_filter": "FullyQualifiedName~SharedLayoutEvidenceTests|FullyQualifiedName~AppNavMenuTests",
        "report_name": "header-sync-labels.trx",
        "result_directory": ".skill-tree/history/validation/ux-ui/header-sync-labels/unit"
      },
      "assertion": "Exercise the stated independent outcomes with controlled synthetic collaborators, nonzero discovery, original failure evidence and no required skips. Presentation only. Preserve status precedence, refresh ownership, busy guards and cache decisions. Do not weaken fonts, contrast, controls or qualification to hide clipping.",
      "timing": "immediate"
    },
    {
      "id": "header-sync-labels-rendered",
      "recipe_id": "ux-ui-rendered-browser",
      "criteria": [
        "Current light/dark phone/desktop rendered geometry and native disclosure behavior qualify without changing sync decisions."
      ],
      "sources": [
        "src/Habitica.WebApp/Layout/MainLayout.razor",
        "src/Habitica.WebApp/wwwroot/css/app.css",
        "tests/Habitica.WebApp.Tests/Qualification/SharedLayoutEvidenceTests.cs",
        "tests/Habitica.WebApp.Tests/AppNavMenuTests.cs",
        "tests/browser-qualification/qualification-core.mjs",
        "tests/browser-qualification/qualification-frame.mjs",
        "tests/Habitica.WebApp.Tests/Qualification/PageLayoutEvidence.cs"
      ],
      "parameters": {
        "task_key": "header-sync-labels",
        "routes": [
          "/dashboard",
          "/todo/today",
          "/todo/tasks",
          "/settings"
        ],
        "cases": "MainLayout emits failed, stale, cloud-syncing and timestamp labels. The phone slot is bounded by25vw and sync-chip applies nowrap/hidden ellipsis. Current360/200 native screenshots still expose only a prefix. Shared header/main clearance and compact navigation outcomes preserve access but do not establish complete status wording.",
        "matrix": {
          "widths": [
            360,
            390,
            430,
            768,
            1024,
            1280
          ],
          "text_percent": [
            100,
            200
          ],
          "schemes": [
            "gryphy-light",
            "gryphy-dark",
            "obsidian-glow"
          ],
          "engines": [
            "chromium",
            "webkit"
          ],
          "require": [
            "44-pixel primary targets",
            "visible focus and actions",
            "disabled-state refusal",
            "complete ordinary words",
            "long-content geometry",
            "enabled and disabled contrast"
          ],
          "disabled_text_product_minimum": 3,
          "evidence": "Current Release identity, actual Razor states, hydrated task-specific journey, screenshots, limitations and owned cleanup."
        },
        "report_directory": ".skill-tree/history/validation/ux-ui/header-sync-labels/browser"
      },
      "assertion": "Current light/dark phone/desktop rendered geometry and native disclosure behavior qualify without changing sync decisions. Implement a reviewed task-specific adapter with owned synthetic state and current routing; static captures alone cannot qualify interactive assertions.",
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Complete sync-state wording remains understandable for fresh, failed, stale and busy states with long times and enlarged phone text.
- Menu and Refresh retain44-pixel targets, visible focus and access; full status/time detail is available without relying only on hover.
- Current light/dark phone/desktop rendered geometry and native disclosure behavior qualify without changing sync decisions.

Documentation updates:
Update current behavior and qualification limits in testing.md and ux-ui-manifest.md when relevant. Keep investigation findings and original failure evidence in structured history.

Research provenance:
- `.skill-tree/history/processing/ux-ui-all-followups-2026-11-04/research.json`
- Original Pending source/context and artifact references remain in the processing selection receipt.

### Clear stale review guidance after known unaccepted task cancellation

```yaml
id: T-f21b11ff-1224-486a-b729-e2f440f4920b
priority: 102
status: Ready
parent_pending_ids: ["P-d70cd514-c714-48e7-8e9d-f2c10e3399d4"]
processing_run_id: PR-edb0232f-548c-4ab6-b918-e9993e00497e
root_task_id: T-e23a628d-1649-4e5e-bb14-17d2194dc4fa
related_task_id: T-e23a628d-1649-4e5e-bb14-17d2194dc4fa
relation: followup
depends_on: ["T-e2d64818-cd7a-4edb-a8a9-fb345fe1cdd0"]
```

Goal:
After a known unaccepted task preview is cancelled, present guidance consistent with current readiness while retaining original unknown-command fences and unrelated errors.

Current behavior:
WorkspaceTaskController.CancelPreview clears Message and refuses unknown preparation/acceptance. TodoWorkspacePresentation.CancelPreview forwards cancellation without updating the copied Message; RunAsync also retains Message when the controller message becomes null. Current cancelled native previews preserve journals but leave earlier review guidance.

Touch:
- `src/Habitica.WebApp/State/TodoWorkspacePresentation.cs`
- `src/Habitica.WebApp/State/WorkspaceTaskController.cs`
- `tests/Habitica.WebApp.Tests/Workspace/TodoWorkspacePresentationTests.cs`
- `tests/Habitica.WebApp.Tests/Workspace/TodoTaskPanelTests.cs`
- `tests/Habitica.WebApp.Tests/Workspace/TaskTypeConversionPanelTests.cs`

Implementation constraints:
Update message ownership at the actual known-cancellation boundary. Preserve current controller refusal order, source authority, accepted receipts and draft isolation. No acceptance, replay or source switch is implied. Local synthetic tests/builds require the applicable standing owner scope. No live provider, deployment, enrollment, real permission or publication is implied.

Validation:
Use focused runnable behavior or rendered Red before the scoped fix. Preserve original failed controls and source/test/environment evidence. Missing required evidence blocks completion.
```json
{
  "checks": [
    {
      "id": "cancelled-review-guidance-behavior",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Known unaccepted cancellation removes obsolete review-before-saving guidance and leaves the exact journal and draft unchanged.",
        "Unknown preparation/acceptance still refuses cancellation and retains original request, receipt and refusal guidance; completed receipts and unrelated errors are not silently erased."
      ],
      "sources": [
        "src/Habitica.WebApp/State/TodoWorkspacePresentation.cs",
        "src/Habitica.WebApp/State/WorkspaceTaskController.cs",
        "tests/Habitica.WebApp.Tests/Workspace/TodoWorkspacePresentationTests.cs",
        "tests/Habitica.WebApp.Tests/Workspace/TodoTaskPanelTests.cs",
        "tests/Habitica.WebApp.Tests/Workspace/TaskTypeConversionPanelTests.cs"
      ],
      "parameters": {
        "test_project": "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "test_filter": "FullyQualifiedName~TodoWorkspacePresentationTests|FullyQualifiedName~TodoTaskPanelTests|FullyQualifiedName~TaskTypeConversionPanelTests",
        "report_name": "cancelled-review-guidance.trx",
        "result_directory": ".skill-tree/history/validation/ux-ui/cancelled-review-guidance/unit"
      },
      "assertion": "Exercise the stated independent outcomes with controlled synthetic collaborators, nonzero discovery, original failure evidence and no required skips. Update message ownership at the actual known-cancellation boundary. Preserve current controller refusal order, source authority, accepted receipts and draft isolation. No acceptance, replay or source switch is implied.",
      "timing": "immediate"
    },
    {
      "id": "cancelled-review-guidance-rendered",
      "recipe_id": "ux-ui-rendered-browser",
      "criteria": [
        "Task, Today and type-copy cancellation render correct readiness and keyboard continuation across the required matrix."
      ],
      "sources": [
        "src/Habitica.WebApp/State/TodoWorkspacePresentation.cs",
        "src/Habitica.WebApp/State/WorkspaceTaskController.cs",
        "tests/Habitica.WebApp.Tests/Workspace/TodoWorkspacePresentationTests.cs",
        "tests/Habitica.WebApp.Tests/Workspace/TodoTaskPanelTests.cs",
        "tests/Habitica.WebApp.Tests/Workspace/TaskTypeConversionPanelTests.cs",
        "tests/browser-qualification/qualification-core.mjs",
        "tests/browser-qualification/qualification-frame.mjs",
        "tests/Habitica.WebApp.Tests/Qualification/PageLayoutEvidence.cs"
      ],
      "parameters": {
        "task_key": "cancelled-review-guidance",
        "routes": [
          "/todo/today",
          "/todo/tasks",
          "/todo/convert"
        ],
        "cases": "WorkspaceTaskController.CancelPreview clears Message and refuses unknown preparation/acceptance. TodoWorkspacePresentation.CancelPreview forwards cancellation without updating the copied Message; RunAsync also retains Message when the controller message becomes null. Current cancelled native previews preserve journals but leave earlier review guidance.",
        "matrix": {
          "widths": [
            360,
            390,
            430,
            768,
            1024,
            1280
          ],
          "text_percent": [
            100,
            200
          ],
          "schemes": [
            "gryphy-light",
            "gryphy-dark",
            "obsidian-glow"
          ],
          "engines": [
            "chromium",
            "webkit"
          ],
          "require": [
            "44-pixel primary targets",
            "visible focus and actions",
            "disabled-state refusal",
            "complete ordinary words",
            "long-content geometry",
            "enabled and disabled contrast"
          ],
          "disabled_text_product_minimum": 3,
          "evidence": "Current Release identity, actual Razor states, hydrated task-specific journey, screenshots, limitations and owned cleanup."
        },
        "report_directory": ".skill-tree/history/validation/ux-ui/cancelled-review-guidance/browser"
      },
      "assertion": "Task, Today and type-copy cancellation render correct readiness and keyboard continuation across the required matrix. Implement a reviewed task-specific adapter with owned synthetic state and current routing; static captures alone cannot qualify interactive assertions.",
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Known unaccepted cancellation removes obsolete review-before-saving guidance and leaves the exact journal and draft unchanged.
- Unknown preparation/acceptance still refuses cancellation and retains original request, receipt and refusal guidance; completed receipts and unrelated errors are not silently erased.
- Task, Today and type-copy cancellation render correct readiness and keyboard continuation across the required matrix.

Documentation updates:
Update current behavior and qualification limits in testing.md and ux-ui-manifest.md when relevant. Keep investigation findings and original failure evidence in structured history.

Research provenance:
- `.skill-tree/history/processing/ux-ui-all-followups-2026-11-04/research.json`
- Original Pending source/context and artifact references remain in the processing selection receipt.

### Diagnose and stabilize sign-in overlap in gear-recovery cancellation checks

```yaml
id: T-7c76be74-16ad-44ac-a078-46ad8a8e6c6a
priority: 102
status: Ready
parent_pending_ids: ["P-612f62db-56ca-44cd-8f23-95609d268da9"]
processing_run_id: PR-edb0232f-548c-4ab6-b918-e9993e00497e
root_task_id: T-21881802-f186-479d-9111-295192ce2de8
related_task_id: T-aae13d52-cf6d-44e4-bdb3-b0ece9715b69
relation: followup
depends_on: []
```

Goal:
Establish deterministic synthetic readiness and controlled interleavings around cancellation; identify a fixture race or evidence-backed application defect without weakening recovery assertions.

Current behavior:
RecoverySetup awaits SignInAsync, while CompleteSignInRefreshAsync continues through an unawaited background path. Recovery refuses State.IsBusy before acquiring the recovery lease. The original119-test prerequisite failed once before browser launch; the observed guard result does not establish a production cause.

Touch:
- `src/Habitica.WebApp/State/AppSessionController.cs`
- `src/Habitica.WebApp/State/AppSessionController.GearRecovery.cs`
- `tests/Habitica.WebApp.Tests/State/AppSessionController.GearRecoveryTests.cs`
- `tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs`

Implementation constraints:
Investigation precedes any production fix. Use bounded synthetic collaborators and explicit readiness signals. No live equipment, spell, credential or provider operation. An unexplained failure remains open rather than becoming a claimed production diagnosis. Local synthetic tests/builds require the applicable standing owner scope. No live provider, deployment, enrollment, real permission or publication is implied.

Validation:
Use focused runnable behavior or rendered Red before the scoped fix. Preserve original failed controls and source/test/environment evidence. Missing required evidence blocks completion.
```json
{
  "checks": [
    {
      "id": "gear-recovery-readiness-behavior",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Controlled deferred collaborators establish the exact sign-in/background/recovery ordering and explain the observed guard failure or retain an explicit evidence-backed blocker.",
        "Cancellation retains the original journal, releases busy/lease state, permits no duplicate equip or cast, and preserves account-change guards under deterministic interleavings.",
        "Focused repeated controls use explicit completion barriers and preserve every original failure; no arbitrary sleep, retry-until-pass or assertion weakening establishes acceptance."
      ],
      "sources": [
        "src/Habitica.WebApp/State/AppSessionController.cs",
        "src/Habitica.WebApp/State/AppSessionController.GearRecovery.cs",
        "tests/Habitica.WebApp.Tests/State/AppSessionController.GearRecoveryTests.cs",
        "tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs"
      ],
      "parameters": {
        "test_project": "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "test_filter": "FullyQualifiedName~AppSessionControllerTests",
        "report_name": "gear-recovery-readiness.trx",
        "result_directory": ".skill-tree/history/validation/ux-ui/gear-recovery-readiness/unit"
      },
      "assertion": "Exercise the stated independent outcomes with controlled synthetic collaborators, nonzero discovery, original failure evidence and no required skips. Investigation precedes any production fix. Use bounded synthetic collaborators and explicit readiness signals. No live equipment, spell, credential or provider operation. An unexplained failure remains open rather than becoming a claimed production diagnosis.",
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Controlled deferred collaborators establish the exact sign-in/background/recovery ordering and explain the observed guard failure or retain an explicit evidence-backed blocker.
- Cancellation retains the original journal, releases busy/lease state, permits no duplicate equip or cast, and preserves account-change guards under deterministic interleavings.
- Focused repeated controls use explicit completion barriers and preserve every original failure; no arbitrary sleep, retry-until-pass or assertion weakening establishes acceptance.

Documentation updates:
Update current behavior and qualification limits in testing.md and ux-ui-manifest.md when relevant. Keep investigation findings and original failure evidence in structured history.

Research provenance:
- `.skill-tree/history/processing/ux-ui-all-followups-2026-11-04/research.json`
- Original Pending source/context and artifact references remain in the processing selection receipt.

### Present concise Party sync failures with separate sanitized diagnostics

```yaml
id: T-bc1d9dd4-ab18-4c3d-b7dd-c0d53e5372aa
priority: 101
status: Ready
parent_pending_ids: ["P-5a241695-798e-48df-8a0f-989ba8ceaee8"]
processing_run_id: PR-edb0232f-548c-4ab6-b918-e9993e00497e
root_task_id: T-84818186-be62-46d9-be33-c237da0054f2
related_task_id: T-ef51ab99-1f6a-42db-bf0d-91375ee0b2fb
relation: followup
depends_on: ["T-e2d64818-cd7a-4edb-a8a9-fb345fe1cdd0"]
```

Goal:
Show concise actionable Party-sync guidance while retaining sanitized technical diagnostics separately and preserving cached Party reads and execution fences.

Current behavior:
The Party bridge throws descriptive local-host/HTML response errors. The background sign-in error path forwards exception.Message to shared ErrorMessage and sanitized diagnostics, so JSInterop message/stack content appears in the user banner. The prior reflow repair bounds the banner but does not shorten failure copy.

Touch:
- `src/Habitica.WebApp/wwwroot/js/sync/cloudflarePartySync.js`
- `src/Habitica.WebApp/State/AppSessionController.cs`
- `src/Habitica.WebApp/Layout/MainLayout.razor`
- `src/Habitica.Domain/Diagnostics/DiagnosticsLogSanitizer.cs`
- `tests/Functions/cloudflare-party-sync-bridge.test.mjs`
- `tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs`
- `tests/Habitica.WebApp.Tests/Pages/PartyPageTests.cs`

Implementation constraints:
Scope normalization to the Party-sync failure path. Preserve existing rate-limit, consent, account and mutation boundaries. Do not hide unrelated errors, expose response bodies or imply automatic retry or configuration changes. Local synthetic tests/builds require the applicable standing owner scope. No live provider, deployment, enrollment, real permission or publication is implied.

Validation:
Use focused runnable behavior or rendered Red before the scoped fix. Preserve original failed controls and source/test/environment evidence. Missing required evidence blocks completion.
```json
{
  "checks": [
    {
      "id": "party-sync-failure-copy-behavior",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Local-host unavailable, HTML response, malformed JSON and transport failures produce concise classified user guidance without a raw stack or duplicated implementation details.",
        "Sanitized diagnostics retain useful failure context without credentials, private headers or unbounded response bodies; cached Party access and sync/mutation guards remain unchanged."
      ],
      "sources": [
        "src/Habitica.WebApp/wwwroot/js/sync/cloudflarePartySync.js",
        "src/Habitica.WebApp/State/AppSessionController.cs",
        "src/Habitica.WebApp/Layout/MainLayout.razor",
        "src/Habitica.Domain/Diagnostics/DiagnosticsLogSanitizer.cs",
        "tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs",
        "tests/Habitica.WebApp.Tests/Pages/PartyPageTests.cs"
      ],
      "parameters": {
        "test_project": "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "test_filter": "FullyQualifiedName~AppSessionControllerTests|FullyQualifiedName~PartyPageTests",
        "report_name": "party-sync-failure-copy.trx",
        "result_directory": ".skill-tree/history/validation/ux-ui/party-sync-failure-copy/unit"
      },
      "assertion": "Exercise the stated independent outcomes with controlled synthetic collaborators, nonzero discovery, original failure evidence and no required skips. Scope normalization to the Party-sync failure path. Preserve existing rate-limit, consent, account and mutation boundaries. Do not hide unrelated errors, expose response bodies or imply automatic retry or configuration changes.",
      "timing": "immediate"
    },
    {
      "id": "party-sync-failure-copy-bridge",
      "recipe_id": "node-focused-tests",
      "criteria": [
        "Local-host unavailable, HTML response, malformed JSON and transport failures produce concise classified user guidance without a raw stack or duplicated implementation details.",
        "Sanitized diagnostics retain useful failure context without credentials, private headers or unbounded response bodies; cached Party access and sync/mutation guards remain unchanged."
      ],
      "sources": [
        "src/Habitica.WebApp/wwwroot/js/sync/cloudflarePartySync.js",
        "tests/Functions/cloudflare-party-sync-bridge.test.mjs"
      ],
      "parameters": {
        "test_file": "tests/Functions/cloudflare-party-sync-bridge.test.mjs"
      },
      "assertion": "Exercise actual bridge classification and response/refusal boundaries with synthetic responses and no real provider traffic.",
      "timing": "immediate"
    },
    {
      "id": "party-sync-failure-copy-rendered",
      "recipe_id": "ux-ui-rendered-browser",
      "criteria": [
        "Party and shared-warning surfaces remain readable with keyboard-reachable relevant actions in the required rendered matrix."
      ],
      "sources": [
        "src/Habitica.WebApp/wwwroot/js/sync/cloudflarePartySync.js",
        "src/Habitica.WebApp/State/AppSessionController.cs",
        "src/Habitica.WebApp/Layout/MainLayout.razor",
        "src/Habitica.Domain/Diagnostics/DiagnosticsLogSanitizer.cs",
        "tests/Functions/cloudflare-party-sync-bridge.test.mjs",
        "tests/Habitica.WebApp.Tests/State/AppSessionControllerTests.cs",
        "tests/Habitica.WebApp.Tests/Pages/PartyPageTests.cs",
        "tests/browser-qualification/qualification-core.mjs",
        "tests/browser-qualification/qualification-frame.mjs",
        "tests/Habitica.WebApp.Tests/Qualification/PageLayoutEvidence.cs"
      ],
      "parameters": {
        "task_key": "party-sync-failure-copy",
        "routes": [
          "/party",
          "/dashboard"
        ],
        "cases": "The Party bridge throws descriptive local-host/HTML response errors. The background sign-in error path forwards exception.Message to shared ErrorMessage and sanitized diagnostics, so JSInterop message/stack content appears in the user banner. The prior reflow repair bounds the banner but does not shorten failure copy.",
        "matrix": {
          "widths": [
            360,
            390,
            430,
            768,
            1024,
            1280
          ],
          "text_percent": [
            100,
            200
          ],
          "schemes": [
            "gryphy-light",
            "gryphy-dark",
            "obsidian-glow"
          ],
          "engines": [
            "chromium",
            "webkit"
          ],
          "require": [
            "44-pixel primary targets",
            "visible focus and actions",
            "disabled-state refusal",
            "complete ordinary words",
            "long-content geometry",
            "enabled and disabled contrast"
          ],
          "disabled_text_product_minimum": 3,
          "evidence": "Current Release identity, actual Razor states, hydrated task-specific journey, screenshots, limitations and owned cleanup."
        },
        "report_directory": ".skill-tree/history/validation/ux-ui/party-sync-failure-copy/browser"
      },
      "assertion": "Party and shared-warning surfaces remain readable with keyboard-reachable relevant actions in the required rendered matrix. Implement a reviewed task-specific adapter with owned synthetic state and current routing; static captures alone cannot qualify interactive assertions.",
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Local-host unavailable, HTML response, malformed JSON and transport failures produce concise classified user guidance without a raw stack or duplicated implementation details.
- Sanitized diagnostics retain useful failure context without credentials, private headers or unbounded response bodies; cached Party access and sync/mutation guards remain unchanged.
- Party and shared-warning surfaces remain readable with keyboard-reachable relevant actions in the required rendered matrix.

Documentation updates:
Update current behavior and qualification limits in testing.md and ux-ui-manifest.md when relevant. Keep investigation findings and original failure evidence in structured history.

Research provenance:
- `.skill-tree/history/processing/ux-ui-all-followups-2026-11-04/research.json`
- Original Pending source/context and artifact references remain in the processing selection receipt.

### Keep Projects management labels readable at enlarged phone widths

```yaml
id: T-6ec3d90c-0cb8-4968-b794-d68f9cde898c
priority: 100
status: Ready
parent_pending_ids: ["P-3663dfd8-1ecc-4dc3-9577-fc0fd9afb49f"]
processing_run_id: PR-9724479e-a259-40ca-9850-0e5e70e1a4b3
root_task_id: T-96866c8f-9554-55d8-a638-6d8b5afaf21c
related_task_id: T-4b41e76a-ff2d-4e1d-a24d-c494f49ce784
relation: followup
depends_on: []
```

Goal:
Keep optional Projects management copy readable with enlarged phone text.

Current behavior:
The current post-focus-repair native screenshots show mid-word breaks in Organization management and Open management at360px200percent text. The on-demand management surface remains at the end of Projects. Outer document geometry alone does not detect the word break.

Touch:
- `src/Habitica.WebApp/Pages/TodoProjectsPage.razor`
- `src/Habitica.WebApp/Components/TodoOrganizationPanel.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `tests/browser-qualification/ux-ui-projects-browse-journey.mjs`
- `tests/browser-qualification/ux-ui-adapters.json`
- `tests/Habitica.WebApp.Tests/Workspace/TodoOrganizationPanelTests.cs`

Implementation constraints:
Scope spacing/reflow to Projects management. Do not shrink normal/enlarged typography, hide labels, weaken contrast, replace native controls or change original management/draft/receipt behavior. Register reviewed new adapter inputs before execution. Preserve source/consent/unknown-outcome fences; no provider or command acceptance.

Validation:
Use exploratory rendered Red before scoped layout changes. Existing organization component behavior contracts remain required regression coverage; no new pure markup test is required for a CSS-only repair. Add native word-line geometry and an independent narrow negative to a reviewed Projects adapter, retaining every declared route/palette/width/text case.
```json
{
  "checks": [
    {
      "assertion": "Use actual current Release Projects with paired typed Browser fixtures. Establish rendered Red for split management heading/action words, then require complete word-line geometry at unchanged normal font sizes, native keyboard focus,44-pixel actions, original locked reviews and unchanged storage. Include an independent narrower-than-word negative control; no static markup or outer viewport width substitutes for readability.",
      "criteria": [
        "Projects management headings and primary Open/Close management labels retain complete ordinary words in the documented normal/enlarged phone/desktop light and dark matrix.",
        "Management remains on demand; opening, closing and original-review locks preserve exact project and Notes drafts, selected source and keyboard focus without accepting commands.",
        "A native rendered word-line control rejects the original narrow layout and an independent narrow negative, while current scoped product states pass with full geometry, disabled, focus, contrast and cleanup evidence."
      ],
      "id": "projects-management-word-reflow",
      "parameters": {
        "cases": "Closed/on-demand management, open management, frozen original review, long heading/action labels, source and draft preservation, ordinary-word negative control.",
        "matrix": {
          "disabled_text_product_minimum": 3,
          "engines": [
            "chromium",
            "webkit"
          ],
          "evidence": "current Release identity, route/state inventory, semantic interactions, screenshots, assertions, limitations and owned-session cleanup",
          "require": [
            "primary targets at least 44 CSS pixels where the documented primary-action contract applies",
            "visible primary actions",
            "disabled states and reasons",
            "visible keyboard focus",
            "no unintended overlap or clipping",
            "readable native input contents",
            "enabled text contrast",
            "long-content reflow"
          ],
          "schemes": [
            "gryphy-light",
            "gryphy-dark",
            "obsidian-glow"
          ],
          "text_percent": [
            100,
            200
          ],
          "widths": [
            360,
            390,
            430,
            768,
            1024,
            1280
          ]
        },
        "report_directory": ".skill-tree/history/validation/ux-ui/projects-management-reflow/browser",
        "routes": [
          "/todo/projects"
        ],
        "task_key": "projects-management-reflow"
      },
      "recipe_id": "ux-ui-rendered-browser",
      "sources": [
        "src/Habitica.WebApp/Pages/TodoProjectsPage.razor",
        "src/Habitica.WebApp/Components/TodoOrganizationPanel.razor",
        "src/Habitica.WebApp/wwwroot/css/app.css",
        "tests/browser-qualification/ux-ui-projects-browse-journey.mjs",
        "tests/browser-qualification/ux-ui-adapters.json",
        "tests/Habitica.WebApp.Tests/Workspace/TodoOrganizationPanelTests.cs"
      ],
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Projects management headings and primary Open/Close management labels retain complete ordinary words in the documented normal/enlarged phone/desktop light and dark matrix.
- Management remains on demand; opening, closing and original-review locks preserve exact project and Notes drafts, selected source and keyboard focus without accepting commands.
- A native rendered word-line control rejects the original narrow layout and an independent narrow negative, while current scoped product states pass with full geometry, disabled, focus, contrast and cleanup evidence.

### Expose complete retained title and Notes during frozen task reviews

```yaml
id: T-ecb2732c-1fe8-4ccf-b4a0-fbd7ae798f44
priority: 100
status: Ready
parent_pending_ids: ["P-972b21ed-ac92-48d4-ba32-ca8ef24be8a7"]
processing_run_id: PR-edb0232f-548c-4ab6-b918-e9993e00497e
root_task_id: T-4afee12c-2759-40f6-adb4-39b5029a3928
related_task_id: T-4afee12c-2759-40f6-adb4-39b5029a3928
relation: followup
depends_on: ["T-e2d64818-cd7a-4edb-a8a9-fb345fe1cdd0"]
```

Goal:
Offer complete exact retained title and Notes through a keyboard-accessible read-only presentation while editing remains fenced.

Current behavior:
TodoTaskPanel retains the exact draft title and Notes inside a disabled fieldset. A native disabled single-line title cannot offer ordinary keyboard scrolling/selection, and current360/200 captures show only a prefix. The3:1 disabled-text outcome qualifies contrast, not complete retained-value inspectability.

Touch:
- `src/Habitica.WebApp/Components/TodoTaskPanel.razor`
- `src/Habitica.WebApp/wwwroot/css/app.css`
- `tests/Habitica.WebApp.Tests/Workspace/TodoTaskPanelTests.cs`
- `tests/Habitica.WebApp.Tests/Workspace/TodoWorkspacePresentationTests.cs`

Implementation constraints:
Read-only presentation only. Use the current draft and active ownership context, render untrusted text safely and avoid new persistence, clipboard side effects, enabled editing or command dispatch. Local synthetic tests/builds require the applicable standing owner scope. No live provider, deployment, enrollment, real permission or publication is implied.

Validation:
Use focused runnable behavior or rendered Red before the scoped fix. Preserve original failed controls and source/test/environment evidence. Missing required evidence blocks completion.
```json
{
  "checks": [
    {
      "id": "frozen-value-inspection-behavior",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Long retained title and Notes are fully inspectable through wrapped read-only text or an equivalent keyboard-accessible disclosure while native editing remains unavailable.",
        "Inspection preserves exact private draft values, original pending command ownership and unknown-outcome fences; account/source changes remove prior private content."
      ],
      "sources": [
        "src/Habitica.WebApp/Components/TodoTaskPanel.razor",
        "src/Habitica.WebApp/wwwroot/css/app.css",
        "tests/Habitica.WebApp.Tests/Workspace/TodoTaskPanelTests.cs",
        "tests/Habitica.WebApp.Tests/Workspace/TodoWorkspacePresentationTests.cs"
      ],
      "parameters": {
        "test_project": "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "test_filter": "FullyQualifiedName~TodoTaskPanelTests|FullyQualifiedName~TodoWorkspacePresentationTests",
        "report_name": "frozen-value-inspection.trx",
        "result_directory": ".skill-tree/history/validation/ux-ui/frozen-value-inspection/unit"
      },
      "assertion": "Exercise the stated independent outcomes with controlled synthetic collaborators, nonzero discovery, original failure evidence and no required skips. Read-only presentation only. Use the current draft and active ownership context, render untrusted text safely and avoid new persistence, clipboard side effects, enabled editing or command dispatch.",
      "timing": "immediate"
    },
    {
      "id": "frozen-value-inspection-rendered",
      "recipe_id": "ux-ui-rendered-browser",
      "criteria": [
        "Normal/enlarged light/dark phone/desktop states qualify complete value access, visible focus, disabled refusal and safe text encoding; physical assistive-technology gaps remain explicit."
      ],
      "sources": [
        "src/Habitica.WebApp/Components/TodoTaskPanel.razor",
        "src/Habitica.WebApp/wwwroot/css/app.css",
        "tests/Habitica.WebApp.Tests/Workspace/TodoTaskPanelTests.cs",
        "tests/Habitica.WebApp.Tests/Workspace/TodoWorkspacePresentationTests.cs",
        "tests/browser-qualification/qualification-core.mjs",
        "tests/browser-qualification/qualification-frame.mjs",
        "tests/Habitica.WebApp.Tests/Qualification/PageLayoutEvidence.cs"
      ],
      "parameters": {
        "task_key": "frozen-value-inspection",
        "routes": [
          "/todo/tasks",
          "/todo/dailies",
          "/todo/habits"
        ],
        "cases": "TodoTaskPanel retains the exact draft title and Notes inside a disabled fieldset. A native disabled single-line title cannot offer ordinary keyboard scrolling/selection, and current360/200 captures show only a prefix. The3:1 disabled-text outcome qualifies contrast, not complete retained-value inspectability.",
        "matrix": {
          "widths": [
            360,
            390,
            430,
            768,
            1024,
            1280
          ],
          "text_percent": [
            100,
            200
          ],
          "schemes": [
            "gryphy-light",
            "gryphy-dark",
            "obsidian-glow"
          ],
          "engines": [
            "chromium",
            "webkit"
          ],
          "require": [
            "44-pixel primary targets",
            "visible focus and actions",
            "disabled-state refusal",
            "complete ordinary words",
            "long-content geometry",
            "enabled and disabled contrast"
          ],
          "disabled_text_product_minimum": 3,
          "evidence": "Current Release identity, actual Razor states, hydrated task-specific journey, screenshots, limitations and owned cleanup."
        },
        "report_directory": ".skill-tree/history/validation/ux-ui/frozen-value-inspection/browser"
      },
      "assertion": "Normal/enlarged light/dark phone/desktop states qualify complete value access, visible focus, disabled refusal and safe text encoding; physical assistive-technology gaps remain explicit. Implement a reviewed task-specific adapter with owned synthetic state and current routing; static captures alone cannot qualify interactive assertions.",
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Long retained title and Notes are fully inspectable through wrapped read-only text or an equivalent keyboard-accessible disclosure while native editing remains unavailable.
- Inspection preserves exact private draft values, original pending command ownership and unknown-outcome fences; account/source changes remove prior private content.
- Normal/enlarged light/dark phone/desktop states qualify complete value access, visible focus, disabled refusal and safe text encoding; physical assistive-technology gaps remain explicit.

Documentation updates:
Update current behavior and qualification limits in testing.md and ux-ui-manifest.md when relevant. Keep investigation findings and original failure evidence in structured history.

Research provenance:
- `.skill-tree/history/processing/ux-ui-all-followups-2026-11-04/research.json`
- Original Pending source/context and artifact references remain in the processing selection receipt.

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
depends_on: ["T-3afa6af6-cc43-4ba9-818e-5cbd48bcf17e", "T-0b3a2a66-56a8-4b0b-8f47-9fb2853d3735", "T-29ef8846-6aa7-4255-990d-a8651e689ec2", "T-34491faa-090e-4d97-b496-1fe947770688", "T-bf42656d-57e8-4a20-98b6-cb1b01aafd58", "T-159a415e-0d56-4028-96a4-339a5d5040f8", "T-8f8f1dba-9d90-4bf0-8958-fea5ede83941", "T-88a59bd6-a8d4-4cb2-9c0b-e32fecc96108", "T-b38dad4c-c3fa-431a-a429-50d6fed97650", "T-70901e97-738c-42c2-ae50-68dd71417a32", "T-65810da7-9524-45ce-b280-668d04c86441", "T-13e3754f-774b-443f-8997-e1d677f3c079"]
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
