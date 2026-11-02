# Unified Workspace production release

All four remaining owner-selected units are accepted and published to https://habitica-companion.pages.dev on 2026-11-02. Work Through Next run `WR-59366750-0665-4112-b0bf-1cf2c1c85cd5` is finished; no additional Future root starts.

## Completed behavior

- `T-be29adc1-276c-417d-8f3d-434e34d17a0d`: account-scoped encrypted non-authorizing preferences and existing-only root/Workspace/Notes restoration. Startup waits for account metadata before choosing a writable default. Observed server permissions remain separate from new consent.
- `T-b56ed4eb-6a2a-4d48-8d2f-c1ee5729b723`: reviewed atomic adoption into the existing cloud target, exact source archives, independent Notes lifecycle, original receipt and local acknowledgement recovery. Imported unresolved effects remain paused.
- `T-fcb3bef0-1d46-433f-bf15-d90b7f359048`: six reviewed migration/discovery/receipt tools, independent `workspace.migrate` scope, Notes scope boundaries and package 0.3.0.
- `T-726f81ba-4130-4b59-b793-686298251e6c`: one active Workspace across Settings, Tasks and Notes, default browser first use, explicit cloud move, preserved drafts/conflicts, no writable browser fallback after cloud selection, original legacy-transfer recovery.

The reverse cloud-to-browser ownership protocol remains deferred. Export, pause and disconnect remain available.

## Published components

| Component | Published identity |
| --- | --- |
| chatgpt-auth | 736020af-860e-40f3-9679-1715bd25da5e |
| companion-workspace | 9dd0d74e-de60-4530-8cac-3b3f54dd47a4 |
| chatgpt-mcp | 57f72287-d25d-4d8e-ade7-72d2759a421d |
| Pages canonical production | 1dd4e8a2-33ff-454a-9076-2e95967f401e; success |
| Backend release / tool schema | 0.3.0 / 1.2.0 |
| Shared backend build | 5974025f3cdcb8e563cffca239ffb6746d4a111e6efb3a180f8cd493f58fd83b |
| Additive Workspace migration | 0018-workspace-transfers.sql |
| Frontend assets / Pages Functions | 218 / 4 |

The reviewed dirty working tree is based on `f9f71b5190086d17192642545ea3dc55610eed07`. No Git commit, push, branch switch or history change occurs. Existing unrelated dirty baseline changes remain preserved. Accepted source fingerprints and frontend/function package hashes identify the published content.

## Publication and preservation

Fresh authenticated Cloudflare preflight verifies the established account, deploy permissions, current Worker versions, D1 migration state and Time Travel bookmarks. Existing live runtime variables, resource bindings, compatibility dates/flags, observability, triggers and Pages production/preview runtime/build/Git settings are preserved. Existing CRON, executor and reminder feature flags retain the observed production values. Callback egress remains disabled. Secret values are neither replaced nor written into reports.

Workspace0018 is additive and applied before compatible Worker code. Auth0013 was already applied by the foundation release. Postflight reports no pending Auth/Workspace migrations. Code rollback does not revert database state; retain additive migration/provenance tables and use the recorded predecessor versions for an explicitly selected rollback. No rollback runs.

The config-free Pages upload includes all four Functions and acknowledges the Functions bundle in the successful upload. Canonical deployment status is `success`. Public probes return 200 for the app, app settings and service worker; the deliberately invalid private-sync identifier returns 400. Fresh anonymous Chromium renders Sign In without page errors, failed requests or Blazor error UI. No live private sign-in, account enrollment, consent change, executor opt-in, game mutation or user-data transfer occurs during release checks.

## Authenticated MCP cloud audit

Complete cloud inventory pages match exactly before and after publication. Workspace `e5a46166-614a-4e1c-b687-c252e74409b1` remains revision168/storeVersion502. Notes notebook `56f214dc-9e2d-4a22-9257-ffea7ed8b355` remains revision3.

| Cloud records | Count |
| --- | --- |
| Canonical tasks, including archives | 34 |
| Nonarchived tasks | 25: 13 active, 12 completed |
| Archived tasks | 9 |
| Nonarchived projects | 3 |
| Personal Notes / trashed Notes | 1 / 0 |
| Retained Todo/Daily completions | 18 |
| Requested / confirmed task effects | 44 / 44 |
| Pending / leased / executing / blocked / unknown effects | 0 / 0 / 0 / 0 / 0 |

Stored tag metadata has five confirmed and fifteen review-needed entries. Existing tag consent/source/counterpart review gaps remain separate from record retention; no metadata retry or consent expansion occurs. The backend reports one coherent 0.3.0 build across Auth, Workspace and MCP. Client-reported package compatibility is compatible; installation remains explicitly unknown.

The audit proves cloud record retention and release consistency. Complete browser migration remains **unverified**: a cloud MCP grant cannot enumerate an old browser IndexedDB or acknowledge local authority. Previous cloud choice alone does not prove that every browser task/Note moved. No claim that all browser data is already in cloud follows from cloud counts.

For exact owner verification, open the old browser profile at `/todo/sync#workspace-transfer`, restore existing access if needed, refresh the browser inventory and review any remaining browser data. The reviewed move retains original identities, source archives and original acceptance recovery. Owner browser capture is an explicit prerequisite; agent-side browser automation does not replace the reviewed protocol. New migration tool definitions are published, but the current host tool snapshot still lacks the six-tool family; host metadata refresh/new migration scope remains unqualified.

`mcp-cloud-audit.json` retains content-free counts, identities, revisions and equality results. No task titles, Notes Markdown or snippets enter the release report.

## Checks and limitations

969 WebApp tests, 308 Application tests and 36 Domain tests pass. D1/Notes/MCP/build-identity checks pass46 controls; OAuth/vault/recovery pass30; MCP transport pass6; final OAuth permission rendering passes after the readability repair. Real IndexedDB passes16 checks plus reload. Production frontend and all three Worker packages build successfully.

The rendered matrix passes280 cases across35 synthetic Razor/OAuth fixtures and16 route families in Gryphy Light/Obsidian Glow at360/1280 pixels and100/200-percent text. Geometry, contrast, primary controls, disabled counts and keyboard focus samples are recorded. Both seeded negative controls detect the intended failures. Standalone viewport/review screenshots replace clipped iframe captures. Physical devices, assistive technologies, hosted private login and installed-host activation remain unqualified.

Repository navigation/index validation and `git diff --check` pass. The existing stale historical-audit warning remains. Inherited prose lint remains covered by the existing Backlog task; new Unified Workspace prose passes.

The package artifact is `output/unified-workspace-plugin.zip`, version0.3.0, content hash `ba542784d462cc9a396f19be4d03db1d9c1a3068ad530005e338c658a65d7b14`, archive SHA-256 `7ccdc2d9ce2871debd8b99da96b65102fd000eb26beeb5ad860f9b3ce37bd45a`. Packaging does not establish installed host activation.

## Remaining Future

The queue contains 1 Pending, 14 Prioritized and 12 Backlog units. Pending concerns mass spell rate limits and explicit gear recovery. Prioritized groups cover four Notes-folder units, callbacks/reminder delivery and Netlify qualification, streaks, linked Daily schedules, task-type conversion, relative-date saved lists, project-filtered history, security and host/mobile qualification. The previously stopped Implement All checkpoint remains archived without activation. `remaining-future.json` records exact identities and priorities.

Copyable commit message, with no commit performed:

```text
Restore synced settings and unify cloud Workspace adoption
```
