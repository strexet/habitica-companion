Goal:
- Preserve Notes schema2 folder notebooks across reviewed browser-to-cloud Workspace adoption without dropping folder state or freezing an unsupported source.
Current behavior:
- Notes native/cloud validation supports schema2 folders and folder receipts. validateTransferNotes accepts schema1 only and omits folders/folderReceipts/folderTransfers; mergeTransfer revalidates the target with the legacy shape. CaptureAsync validates NotesJson then freezes browser authority before cloud preview.
Desired behavior:
- Existing target folder journals survive legacy imports unchanged. Source folder notebooks receive complete reviewed support with exact source provenance, or an explicit refusal before freeze while the source remains writable. No successful adoption can silently lose a folder, membership or original receipt.
Touch:
- Shared transfer/Notes validators and marker models, transfer API and immutable source chunks, browser capture/storage/transfer controller, typed transfer reviews, existing MCP transfer/source-record tools and focused tests.
Implementation constraints:
- Retain schema1 fixture compatibility and original artifact hashes. Preserve folder UUIDs, current membership, folder state, project links, bounded histories and separate command/transfer receipts. Bind owner, browser/cloud source, original Workspace/notebook revision and target identity. Detect duplicate IDs, conflicting project/folder identities, purged records, capacity and stale target state before acceptance; existing explicit archive-conflicts review never grants arbitrary rebinding. Source receipts remain immutable and discoverable through reviewed MCP parity. Preserve target notebooks even when no source Notes are imported.
- Add a local compatibility preflight before FreezeAsync. Unsupported source/target capability must leave browser authority untouched. Interruption recovery uses the original transfer/artifact/receipt; no second writable browser/cloud source, blind replay, automatic folder copy, task effects or provider mutation.
- Before UI acceptance, register concrete current Release browser validation for every changed transfer route with light/obsidian, phone/desktop, normal/enlarged text, focus, geometry, disabled controls and account cleanup. Hosted/device/installed-PWA paths remain separately qualified.
Out of scope:
- General transfer redesign, additional nesting, automatic project association, publication, live account migration and provider/game effects.
Validation:
- Extend existing neutral schema1 transfer fixtures with independently specified schema2 folders, original folder receipts, current/historical membership, active/archived/purged records, project remapping and collisions. Native/Worker parity, actual SDK/D1, IndexedDB prefreeze refusal, concurrent revision conflicts and original receipt recovery require focused controls. Preserve all old fixture hashes.
```json
{
  "checks": [
    {
      "id": "folder-transfer-wire",
      "recipe_id": "worker-focused-tests",
      "criteria": [
        "Workspace adoption preserves target and imported Notes folder identities, memberships and original receipt provenance, or refuses unsupported folder capture before any browser authority freeze."
      ],
      "sources": [
        "workers/shared/workspace-transfer.ts",
        "workers/shared/notes.ts",
        "workers/shared/notes-folders.ts",
        "workers/shared/workspace-transfer-tools.ts",
        "workers/companion-workspace/src/workspace-transfer-api.ts",
        "workers/companion-workspace/test/workspace-transfer.test.mjs",
        "workers/companion-workspace/test/workspace-transfer-mcp.test.mjs",
        "src/Habitica.Application/Workspace/WorkspaceTransferService.cs",
        "src/Habitica.WebApp/State/WorkspaceTransferController.cs",
        "src/Habitica.Storage/IndexedDbWorkspaceTransfer.cs",
        "src/Habitica.WebApp/wwwroot/js/storage/indexedDbStorage.js",
        "tests/Habitica.WebApp.Tests/Integration/WorkspaceTransferTests.cs"
      ],
      "parameters": {
        "test_file": "workers/companion-workspace/test/workspace-transfer.test.mjs"
      },
      "assertion": "Cover legacy and schema2 source/target combinations, folder membership/receipt provenance, collisions, capacity, exact project mappings and atomic original-receipt recovery.",
      "timing": "immediate"
    },
    {
      "id": "folder-transfer-mcp",
      "recipe_id": "worker-focused-tests",
      "criteria": [
        "Workspace adoption preserves target and imported Notes folder identities, memberships and original receipt provenance, or refuses unsupported folder capture before any browser authority freeze."
      ],
      "sources": [
        "workers/companion-workspace/test/workspace-transfer-mcp.test.mjs",
        "workers/shared/workspace-transfer-tools.ts",
        "workers/chatgpt-mcp/src/workspace-transfer-tools.ts"
      ],
      "parameters": {
        "test_file": "workers/companion-workspace/test/workspace-transfer-mcp.test.mjs"
      },
      "assertion": "Reviewed MCP preview/accept/receipt and exact source-record reads preserve original folder receipts and require existing migration permission.",
      "timing": "immediate"
    },
    {
      "id": "folder-transfer-browser-capture",
      "recipe_id": "dotnet-focused-tests",
      "criteria": [
        "Workspace adoption preserves target and imported Notes folder identities, memberships and original receipt provenance, or refuses unsupported folder capture before any browser authority freeze."
      ],
      "sources": [
        "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "tests/Habitica.WebApp.Tests/Integration/WorkspaceTransferTests.cs",
        "src/Habitica.Application/Workspace/WorkspaceTransferService.cs",
        "src/Habitica.WebApp/State/WorkspaceTransferController.cs",
        "src/Habitica.Storage/IndexedDbWorkspaceTransfer.cs",
        "src/Habitica.WebApp/wwwroot/js/storage/indexedDbStorage.js"
      ],
      "parameters": {
        "test_project": "tests/Habitica.WebApp.Tests/Habitica.WebApp.Tests.csproj",
        "test_filter": "FullyQualifiedName~WorkspaceTransfer",
        "report_name": "folder-transfer.trx",
        "result_directory": ".skill-tree/artifacts/validation/folder-transfer"
      },
      "assertion": "Prove unsupported schema2 capture refuses before FreezeAsync; supported capture reviews exact folders and restores original transfer after interruption without a second writable authority.",
      "timing": "immediate"
    }
  ]
}
```

Acceptance:
- Workspace adoption preserves target and imported Notes folder identities, memberships and original receipt provenance, or refuses unsupported folder capture before any browser authority freeze.
Documentation updates:
- Update canonical feature, technical, API, UX and testing contracts with exact acceptance evidence and unrun hosted/device limits.
