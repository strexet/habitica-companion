
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
- [Resolved - repository and owner evidence] The owner request and inspected implementation establish the requested behavior. Source evidence and remaining validation limits are recorded in .skill-tree/history/validation/pending-processing-research.json.
Validation:
- Cross-runtime/component tests cover two projects, project reassignment after completion, archived/removed references, Unassigned, completed archives, duplicate names, text/date intersections, paging, stale revision, original restore receipt and project-to-history navigation.
```json
{
  "checks": [
    {
      "id": "history-components",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Project-filtered completion/Habit history is reachable and truthful about current versus historical placement, with all existing safe recovery semantics retained."
      ],
      "sources": [
        "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "Directory.Packages.props",
        "tests/Habitica.WebApp.Tests/Workspace/TodoHistoryPanelTests.cs",
        "src/Habitica.Domain/Workspace/WorkspaceDocument.cs"
      ],
      "parameters": {
        "test_project": "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "test_filter": "FullyQualifiedName~TodoHistory|FullyQualifiedName~History",
        "report_name": "history-components.trx",
        "result_directory": ".skill-tree/artifacts/validation/T-16e1b491-18c3-498e-82da-81a97ff1294a/history-components"
      },
      "assertion": "- Cross-runtime/component tests cover two projects, project reassignment after completion, archived/removed references, Unassigned, completed archives, duplicate names, text/date intersections, paging, stale revision, original restore receipt and project-to-history navigation.",
      "timing": "immediate"
    },
    {
      "id": "history-wire",
      "recipe_id": "worker-focused-tests",
      "criteria": [
        "Project-filtered completion/Habit history is reachable and truthful about current versus historical placement, with all existing safe recovery semantics retained."
      ],
      "sources": [
        "workers/companion-workspace/test/completion-history.test.mjs",
        "workers/companion-workspace/package.json"
      ],
      "parameters": {
        "test_file": "workers/companion-workspace/test/completion-history.test.mjs"
      },
      "assertion": "- Cross-runtime/component tests cover two projects, project reassignment after completion, archived/removed references, Unassigned, completed archives, duplicate names, text/date intersections, paging, stale revision, original restore receipt and project-to-history navigation.",
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Project-filtered completion/Habit history is reachable and truthful about current versus historical placement, with all existing safe recovery semantics retained.
Documentation updates:
- Update the relevant feature, technical, API, testing and UX contracts for delivered behavior. Keep actual qualification separate from unrun host, device and live checks.
Risks:
- Stale identity, partial acceptance or ambiguous outcome can duplicate effects or misstate saved state. Preserve original command identity and explicit recovery.

## Backlog

Deferred work requires an explicit promotion request before implementation.

