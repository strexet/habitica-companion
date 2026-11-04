Goal:
- Enable explicitly reviewed Habitica tag projection for relative saved lists with exact local evaluation-day context and no unreviewed midnight effects.
Current behavior:
- Relative definitions/read pagination retain explicit timezone and day context. Desired groups expose relative_dates_require_review and retain membership; planner/executor/manual reviews refuse new work. The compatibility fence preserves absolute groups and original unknown receipts.
Desired behavior:
- A separate preview captures one server UTC instant, stored timezone, local day, interpreted bounds, exact native member IDs and complete additions/removals. Acceptance and fresh dispatch require the same reviewed day, source revision, ledger, account/consent epoch and original command identity.
Touch:
- Native/shared relative query and tag membership policies, tag work/review storage and hashes, planner/executor/reconciliation, reviewed group-tag MCP tools, typed clients and Sync review/status interface.
Implementation constraints:
- Preserve every existing native tag, ledger identity, unrelated upstream tag and unreviewed membership. Retain the unavailable fence until the complete new contract is supported. No scheduler, wake, definition edit or local-day rollover grants permission to change membership. Stale-day/revision/consent refuses fresh effects; recovery of an original uncertain dispatch precedes new context validation and never creates a replacement request.
- Bind the interpreted evaluation context and complete member pages into immutable preview/acceptance identity. Empty selection requires explicit complete review and never follows from unavailable data. Old absolute command identities, group semantics and accepted original receipts remain compatible. New work uses existing permissions and reviewed preview/accept/receipt MCP parity; no browser-only agent workflow.
- Register concrete current Release browser coverage for changed Sync/review routes with light/obsidian, phone/desktop, normal/enlarged text, focus/disabled/geometry, day-rollover restart and private account cleanup before implementation acceptance.
Out of scope:
- Automatic daily membership refresh, background consent, task creation/scoring, unrelated tags, publication and real provider execution without a bounded owner profile.
Validation:
- Independent UTC/local-midnight, date-line, DST, week/year and missing-date cases cover complete AND-filtered membership. Actual SDK/D1/synthetic provider tests prove reviewed exact additions/removals, stale-day and consent refusal, old queued-work fencing, concurrent/expired previews and original lost-ack receipt recovery with at most one dispatch.
```json
{
  "checks": [
    {
      "id": "day-bound-tag-review",
      "recipe_id": "worker-focused-tests",
      "criteria": [
        "Relative saved-list tag membership changes only through an explicitly reviewed exact evaluation-day contract, with stale-day refusal and original receipt recovery preserving all unreviewed upstream state."
      ],
      "sources": [
        "workers/shared/relative-saved-dates.ts",
        "workers/shared/tag-membership.ts",
        "workers/shared/group-tag-tools.ts",
        "workers/companion-workspace/src/group-tag-reviews.ts",
        "workers/companion-workspace/src/tag-planner.ts",
        "workers/companion-workspace/src/tag-executor.ts",
        "workers/companion-workspace/src/tag-reconciliation.ts",
        "workers/companion-workspace/test/group-tag-api.test.mjs",
        "workers/companion-workspace/test/tag-reconciliation.test.mjs",
        "src/Habitica.Application/Workspace/WorkspaceTagCorrespondence.cs"
      ],
      "parameters": {
        "test_file": "workers/companion-workspace/test/group-tag-api.test.mjs"
      },
      "assertion": "Actual SDK/Auth/Workspace/D1 preview binds one server evaluation instant/day/timezone and complete additions/removals; stale day/revision/consent refuses without effects and original unknown receipts remain recoverable.",
      "timing": "immediate"
    },
    {
      "id": "day-bound-tag-execution",
      "recipe_id": "worker-focused-tests",
      "criteria": [
        "Relative saved-list tag membership changes only through an explicitly reviewed exact evaluation-day contract, with stale-day refusal and original receipt recovery preserving all unreviewed upstream state."
      ],
      "sources": [
        "workers/shared/relative-saved-dates.ts",
        "workers/shared/tag-membership.ts",
        "workers/shared/group-tag-tools.ts",
        "workers/companion-workspace/src/group-tag-reviews.ts",
        "workers/companion-workspace/src/tag-planner.ts",
        "workers/companion-workspace/src/tag-executor.ts",
        "workers/companion-workspace/src/tag-reconciliation.ts",
        "workers/companion-workspace/test/group-tag-api.test.mjs",
        "workers/companion-workspace/test/tag-reconciliation.test.mjs",
        "src/Habitica.Application/Workspace/WorkspaceTagCorrespondence.cs"
      ],
      "parameters": {
        "test_file": "workers/companion-workspace/test/tag-reconciliation.test.mjs"
      },
      "assertion": "Synthetic provider counters prove no wake or midnight automatically dispatches unreviewed membership; changed context stops queued work before fresh HTTP and retains original uncertainty fences.",
      "timing": "immediate"
    },
    {
      "id": "day-bound-tag-components",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Relative saved-list tag membership changes only through an explicitly reviewed exact evaluation-day contract, with stale-day refusal and original receipt recovery preserving all unreviewed upstream state."
      ],
      "sources": [
        "src/Habitica.WebApp/Components/WorkspaceTagPanel.razor",
        "tests/Habitica.WebApp.Tests/Workspace/WorkspaceTagPanelTests.cs",
        "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj"
      ],
      "parameters": {
        "test_project": "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "test_filter": "FullyQualifiedName~WorkspaceTagPanel",
        "report_name": "day-bound-tags.trx",
        "result_directory": ".skill-tree/artifacts/validation/day-bound-tags"
      },
      "assertion": "Typed review displays exact date/timezone/bounds and every member page, disables incomplete/stale acceptance and preserves original-command recovery after reload/account changes.",
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Relative saved-list tag membership changes only through an explicitly reviewed exact evaluation-day contract, with stale-day refusal and original receipt recovery preserving all unreviewed upstream state.
Documentation updates:
- Update canonical feature, technical, API, UX and testing contracts with exact acceptance evidence and unrun hosted/device limits.
