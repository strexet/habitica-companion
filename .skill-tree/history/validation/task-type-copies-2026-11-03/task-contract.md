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
```json
{
  "checks": [
    {
      "id": "conversion-rules",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Type conversion creates exactly one reviewed native target with retained lineage and an accurate copied/reset-field preview.",
        "Original history/kind/mappings and upstream records remain intact; stale or uncertain acceptance cannot duplicate the target or silently archive linked work."
      ],
      "sources": [
        "tests/Habitica.Application.Tests/Habitica.Application.Tests.csproj",
        "Directory.Packages.props",
        "tests/Habitica.Application.Tests/Workspace/WorkspaceConversionRetirementTests.cs",
        "tests/Habitica.Application.Tests/Workspace/WorkspaceChecklistConversionTests.cs",
        "src/Habitica.Domain/Workspace/WorkspaceDocument.cs"
      ],
      "parameters": {
        "test_project": "tests/Habitica.Application.Tests/Habitica.Application.Tests.csproj",
        "test_filter": "FullyQualifiedName~Conversion",
        "report_name": "conversion-rules.trx",
        "result_directory": ".skill-tree/artifacts/validation/T-70251ccf-7d6a-4e89-b4d4-98cd15c9d528/conversion-rules"
      },
      "assertion": "- Domain/Worker/bUnit cases cover all Todo/Daily/Habit pairs, historical and linked sources, invalid recurrence, organization references, stale source, uncertain commands, duplicate acceptance and export/import lineage.",
      "timing": "immediate"
    },
    {
      "id": "conversion-components",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Type conversion creates exactly one reviewed native target with retained lineage and an accurate copied/reset-field preview.",
        "Original history/kind/mappings and upstream records remain intact; stale or uncertain acceptance cannot duplicate the target or silently archive linked work."
      ],
      "sources": [
        "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "Directory.Packages.props",
        "tests/Habitica.WebApp.Tests/Integration/WorkspaceCloudClient.ConversionTests.cs",
        "tests/Habitica.WebApp.Tests/Workspace/WorkspaceChecklistConversionPanelTests.cs",
        "tests/Habitica.WebApp.Tests/Workspace/ChecklistConversionFixture.cs",
        "tests/Habitica.WebApp.Tests/Workspace/WorkspaceConversionControllerTests.cs",
        "src/Habitica.Domain/Workspace/WorkspaceDocument.cs"
      ],
      "parameters": {
        "test_project": "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "test_filter": "FullyQualifiedName~Conversion",
        "report_name": "conversion-components.trx",
        "result_directory": ".skill-tree/artifacts/validation/T-70251ccf-7d6a-4e89-b4d4-98cd15c9d528/conversion-components"
      },
      "assertion": "- Domain/Worker/bUnit cases cover all Todo/Daily/Habit pairs, historical and linked sources, invalid recurrence, organization references, stale source, uncertain commands, duplicate acceptance and export/import lineage.",
      "timing": "immediate"
    },
    {
      "id": "conversion-wire",
      "recipe_id": "worker-focused-tests",
      "criteria": [
        "Type conversion creates exactly one reviewed native target with retained lineage and an accurate copied/reset-field preview.",
        "Original history/kind/mappings and upstream records remain intact; stale or uncertain acceptance cannot duplicate the target or silently archive linked work."
      ],
      "sources": [
        "workers/companion-workspace/test/conversion-api.test.mjs",
        "workers/companion-workspace/package.json"
      ],
      "parameters": {
        "test_file": "workers/companion-workspace/test/conversion-api.test.mjs"
      },
      "assertion": "- Domain/Worker/bUnit cases cover all Todo/Daily/Habit pairs, historical and linked sources, invalid recurrence, organization references, stale source, uncertain commands, duplicate acceptance and export/import lineage.",
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Type conversion creates exactly one reviewed native target with retained lineage and an accurate copied/reset-field preview.
- Original history/kind/mappings and upstream records remain intact; stale or uncertain acceptance cannot duplicate the target or silently archive linked work.
Documentation updates:
- Update features.md, testing.md and the relevant workspace/UX contract; record actual source coverage and unavailable live/device evidence separately.
Risks:
- Incorrect historical coverage, changed task identity or stale account/revision could misrepresent recovery or trigger an unintended game effect. Preserve exact command identity and reject uncertainty.

