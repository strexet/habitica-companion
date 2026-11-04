
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
```json
{
  "checks": [
    {
      "id": "relative-date-rules",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Relative saved definitions retain intent across reload and evaluate matching bounded results with an explicit clock/timezone context.",
        "Invalid combinations, unsupported timezone and stale evaluation cursors fail without changing definitions or duplicating canonical tasks."
      ],
      "sources": [
        "tests/Habitica.Application.Tests/Habitica.Application.Tests.csproj",
        "Directory.Packages.props",
        "tests/Habitica.Application.Tests/Workspace/WorkspaceSavedTaskViewTests.cs",
        "tests/Habitica.Application.Tests/Workspace/WorkspaceSavedViewCommandTests.cs",
        "src/Habitica.Domain/Workspace/WorkspaceDocument.cs"
      ],
      "parameters": {
        "test_project": "tests/Habitica.Application.Tests/Habitica.Application.Tests.csproj",
        "test_filter": "FullyQualifiedName~Saved|FullyQualifiedName~Relative",
        "report_name": "relative-date-rules.trx",
        "result_directory": ".skill-tree/artifacts/validation/T-916deee7-8be7-44aa-9cd2-b9d86e90e56d/relative-date-rules"
      },
      "assertion": "- Neutral C#/Worker fixtures cover midnight, DST, leap dates, week/year boundaries, absent dates, conflicting filters, day-changed paging, identity/export and no read mutations. Editor checks show interpreted date ranges.",
      "timing": "immediate"
    },
    {
      "id": "relative-date-wire",
      "recipe_id": "worker-focused-tests",
      "criteria": [
        "Relative saved definitions retain intent across reload and evaluate matching bounded results with an explicit clock/timezone context.",
        "Invalid combinations, unsupported timezone and stale evaluation cursors fail without changing definitions or duplicating canonical tasks."
      ],
      "sources": [
        "workers/companion-workspace/test/saved-view-api.test.mjs",
        "workers/companion-workspace/package.json"
      ],
      "parameters": {
        "test_file": "workers/companion-workspace/test/saved-view-api.test.mjs"
      },
      "assertion": "- Neutral C#/Worker fixtures cover midnight, DST, leap dates, week/year boundaries, absent dates, conflicting filters, day-changed paging, identity/export and no read mutations. Editor checks show interpreted date ranges.",
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Relative saved definitions retain intent across reload and evaluate matching bounded results with an explicit clock/timezone context.
- Invalid combinations, unsupported timezone and stale evaluation cursors fail without changing definitions or duplicating canonical tasks.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

