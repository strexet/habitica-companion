# Existing-session recovery production release

The owner requested publication of the accepted recovery foundation and a read-only MCP check of existing cloud data. The selected target is the existing https://habitica-companion.pages.dev production site and the established Auth/Workspace/MCP Workers. No new resource, credential, account enrollment, record migration or game operation is selected.

## Published components

| Component | Published identity |
| --- | --- |
| Auth | c7b56b8d-f4c7-4927-aff4-fb1f07578567 |
| Workspace | c352b2cf-4a01-48c3-ada1-44c8b74b1c70 |
| MCP | 450abcfa-f983-4644-9af2-04a18f2be27e |
| Shared backend build | 814af366208757d981e73db053e7bfb6def10773934ad9279a41c7537cddb677 |
| Backend release / tool schema | 0.2.0 / 1.1.0 |
| Canonical Pages production | 0021196b-b5c0-4cf6-98da-2b551488394d; success |
| Additive Auth migration | 0013-recovery-sessions.sql |

The source is the reviewed dirty working tree based on f9f71b5190086d17192642545ea3dc55610eed07. No Git commit, push, branch switch or history change occurs. package.json records hashes for218 frontend assets; functions-package.json records the four unchanged Pages Functions sources. Existing-only recovery and typed client methods are published. Automatic startup/private-sync orchestration, browser-to-cloud adoption and the single-Workspace UI remain separate prioritized units.

## Preflight and preservation

Authenticated Cloudflare inspection confirms the existing account and Workers/D1/Pages write permissions without retaining token values or audit-author metadata. Worker predecessors are Auth7fd550da-895b-4615-8eb5-93378ab0a4bc, Workspace1ed64582-45eb-4004-9424-5f312705ca62 and MCP0c6fdccc-2886-44f3-be61-785921c6340d. The preceding canonical Pages deployment is0ca73607-0b0d-400b-b517-43255dfeadef. D1 Time Travel bookmarks are retained in the two bookmark files.

Live runtime variables, resource bindings, compatibility dates/flags, observability and other checked Worker settings match the selected production configs before and after publication. Existing triggers and per-account consent/executor state are retained. Callback delivery remains disabled; no new capability flag or user grant is activated. Secrets are neither replaced nor read into release reports.

Auth0013 is applied before matching Worker code. Workspace requires no migration; postflight lists no pending Auth or Workspace migrations. Code rollback does not reverse database contents; retain the additive table and compatible cleanup behavior. Published identities and predecessors provide an operator rollback reference, without an unrequested rollback operation.

The Release publish uses the established production frontend override and restores checked-in source settings afterward. A config-free Pages upload directory preserves existing dashboard settings and includes all four Pages Functions. Postflight confirms unchanged production/preview runtime configuration, build settings and Git integration.

## Publication correction

The initial Pages deployment3796d44f omitted the Functions bundle. The omission was detected from upload evidence and corrected with a second upload using identical218 assets and the four unchanged Functions sources. The corrected canonical deployment0021196b-b5c0-4cf6-98da-2b551488394d reports success and explicitly acknowledges the Functions bundle. The initial deployment is superseded, not the final release. The invalid-sync-ID probe returns400 on the final site; no real private-sync write was sent.

## MCP cloud audit

The authenticated native MCP tools report Workspace e5a46166-614a-4e1c-b687-c252e74409b1 at revision164 and Notes notebook56f214dc-9e2d-4a22-9257-ffea7ed8b355 at revision3. Inventory pages are complete with no continuation offsets.

| Cloud record set | Observed count |
| --- | --- |
| All canonical tasks, including archived | 32 |
| Nonarchived tasks | 25:14 active and11 completed |
| Archived tasks | 7 |
| Nonarchived projects | 3 |
| Personal Notes | 1 |
| Trashed Notes | 0 |
| Retained Todo/Daily completion events | 17 |
| Nonarchived categories / labels / goals / saved views | 0 /0 /0 /0 |

The nonarchived task records, project records and Note search results match exactly before and after publication; Workspace and Notes revisions remain unchanged. All42 requested task/game effects remain confirmed, with zero pending, leased, executing, blocked or unknown effects. Stored tag/mapping review gaps remain separate and are not evidence of missing cloud records. No repair or replay is performed.

The audit proves existing cloud records and stable data across this release. The audit does not establish complete browser-to-cloud migration: current MCP tools expose neither the browser inventory nor an original complete cross-store transfer receipt. Previous cloud selection alone does not prove browser Notes/tasks were uploaded. The existing handoff accepts only task archives into an empty target, and current Notes source switching does not move browser records. T-b56ed4eb-6a2a-4d48-8d2f-c1ee5729b723 and T-fcb3bef0-1d46-433f-bf15-d90b7f359048 own the adoption/provenance and MCP parity gaps. No browser automation or direct provider-data copy substitutes for the reviewed migration workflow.

mcp-cloud-audit.json retains content-free counts, revisions, identities and equality results. The report retains no task titles, note bodies or search snippets.

## Checks and limits

The accepted local foundation has219 client integration tests,34 final Auth controls,41 Workspace/Notes regression controls and matching Worker type/dry-build checks. Production-specific dry builds and the frontend Release publish pass. Native MCP postflight reports coherent matching build identity across all three deployed Workers and preserves existing account/tool access.

Authenticated Pages postflight confirms canonical production success. Public site and service-worker probes return200; the invalid sync-ID probe returns400. Served appsettings.json, service-worker.js and service-worker-assets.js match the exact published package. Device activation of an older cached worker is not established by publication. Actual credential-based recovery, physical devices and assistive technologies remain unrun.

Automatic sign-in/private-sync recovery and the single-Workspace experience are not delivered by the protocol foundation alone. Four priority107 units remain for account preferences/shared source, record adoption, MCP adoption parity and final UI qualification. The full queue contains1 Pending,18 Prioritized and12 Backlog entries. The old stopped Implement All checkpoint remains preserved, without resumption or snapshot expansion.

## Platform references

[D1 migration guidance](https://developers.cloudflare.com/d1/reference/migrations/) supplies the migration workflow. [Worker rollback limits](https://developers.cloudflare.com/workers/versions-and-deployments/rollbacks/) keep code and data rollback separate. [Pages direct upload](https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration/) supplies the existing-site publication context. Local pinned Wrangler help and actual provider receipts establish the executed commands and results.
