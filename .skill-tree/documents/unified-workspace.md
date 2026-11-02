# Unified Workspace

## Account preferences and restoration

`CompanionPreferencesJson` owns version 1 account preferences under `preferences/companionAccounts`. Account keys contain SHA-256 hashes of canonical Habitica account GUIDs. Each account entry carries a revision, mutation identity, exact cloud Workspace/Notebook references, an existing-connection recovery descriptor, recovery suppression flags and confirmed connection-revocation tombstones. A descriptor grants no access. Bearers, credentials, new consent acknowledgements, OAuth codes, drafts, notification permission and device subscriptions remain excluded.

`CloudSyncSection.CompanionPreferences` adds an encrypted optional section without reordering existing section identifiers. Private sync/export includes only the selected account entry. Old bundles lacking the section preserve existing preferences, including override imports. Concurrent same-revision mutations produce a deterministic conflict; cloud selection cannot silently downgrade to browser. Confirmed revocation tombstones survive newer stale metadata. IndexedDB preference commits compare exact previous bytes and retry bounded conflicts.

`CompanionConnectionCoordinator` binds restoration to the authenticated account generation. Eligible descriptors trigger existing-only account, Workspace and independent Notes recovery after cached data/private sync loads. Concurrent automatic bootstrap calls coalesce. Delayed account responses cannot select stores for a different account. Missing descriptors require the explicit existing-account recovery action; ordinary sign-in never creates consent. Actual server status supplies permissions, credential status and executor observations.

Browser first use atomically selects the sole eligible local Workspace or creates one empty default. Multiple legacy IDs remain preserved and require consolidation. Frozen/cloud local copies require original recovery. A recorded cloud source remains selected during unavailable, expired or revoked access. Task preparation and Notes writes require the shared verified source. Dirty Notes drafts and original task/organization intents retain the original source until resolution.

## Settings inventory

| Surface | Shared state | Explicit action retained |
| --- | --- | --- |
| Settings, Codex and ChatGPT | Existing connection descriptor; observed server permissions and credential status | New enrollment acknowledgement; OAuth linking; credential replacement; disconnect |
| Todo Sync | Exact active Workspace; verified availability | Existing-access retry; export; executor consent; disconnect |
| Tasks, Projects and planning routes | Shared active source; original command source during uncertainty | Command review/acceptance and original receipt recovery |
| Notes | Shared browser/cloud choice; exact cloud Notebook | Separate Notes consent; unsaved draft resolution; original command recovery |
| Private sync and portability | Non-authorizing selected-account metadata | Private sync enablement and explicit import |
| Device controls | No preference replay | Browser notification permission and device enrollment |

Automatic restoration preserves the source; only reviewed adoption moves records. Startup holds source selection until the initial account metadata refresh completes, preventing premature writable browser defaults in a new cloud session.

## Reviewed browser adoption

`WorkspaceTransferService` captures every selected-account local Workspace and the independent Notes notebook under one `companion-adoption-v1` transfer identity. Capture resolves expired leases, rejects active attempts and atomically compares the complete inventory before freezing tasks, command/event journals and Notes. Private drafts require save, export or explicit discard before capture. Frozen authority and the Notes storage revision fence reject later writes, including an already-open Notes writer. A failed capture transaction preserves every source byte and authority row.

The immutable artifact contains exact frozen journal strings, exact Notes bytes, account scope and the exact selected cloud target. Local `companion-adoption-v1:<account>` state survives restart and response loss. Capture retries return the original artifact. The server verifies existing account ownership through the Auth identity binding and requires independent active Notes consent for Notes-bearing artifacts. Browser capture is an owner prerequisite; no MCP bearer supplies browser storage authority.

Workspace migration `0018-workspace-transfers.sql` adds staged archives, bounded source fragments, Notes baseline markers and imported-effect fences. Staging accepts only canonical artifact-envelope bytes, hashes original components and bounds individual D1 values to 512 KiB. Retained source archives count toward the existing eight-MiB Workspace bound; the current document retains the one-MiB bound. Note count, history and notebook reserve limits remain enforced. Unsupported/nested provenance or exhausted capacity refuses acceptance without truncation or replacement intent.

Preview identifies source/target stores, source revisions, counts, current target versions, privacy boundaries, paused task IDs and exact identity/alias conflicts. Equal titles remain independent records. Explicit conflict archival retains the existing target or earlier exact source identity and preserves the conflicting source bytes. Canonical IDs and names remain unchanged; a conflicting source alias can remain only in the source archive. Unresolvable reference conflicts refuse acceptance.

Each fresh preview issues a random review identity and fences an earlier unaccepted review of the same transfer. Acceptance rechecks the exact review hash, expiry, target store version, independent Notebook revision, access epochs, MCP grant epoch when applicable and active execution leases. One D1 transaction materializes current tasks, organization, saved views, activity records and Notes alongside the original receipt. The existing cloud Workspace identity remains canonical. `SourceImports` records source revision offsets and exact materialized record IDs; historical command/hash bytes remain in source archives. Current and retained Note project links explicitly map source/owner/Workspace references to the selected target. Notes transfer markers establish imported note revisions without fabricating note command receipts.

Imported source commands never enter the executable target journal. Unconfirmed source effects remain cold; per-task transfer fences refuse replacement game writes pending separate reviewed resolution. Source retirement histories remain archived and retired source tasks retain effect fences. Migration changes neither executor settings nor consent and dispatches zero game effects.

Local acknowledgement requires the verified original transfer ID, artifact hash and exact target identity. Only accepted acknowledgement publishes cloud selection. Frozen browser copies remain recovery evidence and never reappear as ordinary writable sources. Receipt reads remain original after later target edits; an uncertain reply requires original receipt lookup rather than a new transfer identity.

## Provenance and deletion

`/workspace-transfer/export/<transferId>` exports a private `companion-adoption-backup-v1` source artifact and original transfer receipt. Ordinary task-journal export remains separate. Exact source task, organization and Note receipts support lookup through `get_workspace_transfer_record`; source titles never determine ownership.

Personal Note fragments remain separate from task archive fragments. Explicit Note purge erases the matching retained body/history fragments across staged and accepted transfers. Notebook/profile deletion erases every retained personal Note fragment. Task journals and original task receipts remain readable. An archive containing erased Note fragments reports `transfer_source_notes_purged`; an original receipt remains historical acceptance evidence, never proof of current retention after explicit deletion. Local/offline backup copies require independent explicit local cleanup. Workspace deletion refuses destruction of archives retaining independent Notes; export and disconnect remain available.

Reverse cloud-to-browser ownership transfer remains deferred. Export, pause and disconnect do not create a writable browser fork.

## MCP parity

Tool schema `1.2.0` exposes `list_workspace_transfers`, `get_workspace_transfer_status`, `preview_workspace_transfer`, `accept_workspace_transfer`, `get_workspace_transfer_receipt` and `get_workspace_transfer_record`. Every tool uses the verified current profile; arguments accept no account selectors, uploads or URLs. The transfer list returns at most 20 identities and keeps browser inventory/local acknowledgement explicitly unknown.

`workspace.migrate` independently authorizes migration preview and acceptance. Existing task/organization grants remain insufficient. Notes-bearing status/receipt reads require `notes.read`; Notes-bearing preview/acceptance additionally requires `notes.write` and current separate Notes consent. Exact task/organization archive reads remain independent from inaccessible Notes fragments. Missing staged capture remains `owner_browser_capture_required`.

The application and MCP share acceptance guards. MCP reviews bind the current OAuth grant epoch; a changed grant requires fresh review. Preview response loss requires the stored review from status. Acceptance response loss requires the original receipt. MCP cannot acknowledge browser authority, read IndexedDB, enroll credentials, activate execution or move cloud records back to browser.

Backend release `0.3.0` supports package versions `0.2.x` and `0.3.x`; package `0.3.0` carries the reviewed adoption workflow and requires backend `0.3.x`. Existing grants retain existing permissions. Backend/package descriptors and local SDK fixtures do not establish installed host discovery, current grants or activation.


## Unified consumer qualification

`UnifiedWorkspaceFlowTests` composes the actual shared coordinator, transfer service, task presentation, Notes controller and Razor controls against genuine D1 before/accepted fixtures. Controls cover populated targets, conflict review, unsaved drafts, unknown server replies, lost local acknowledgement, restart and unavailable cloud access. Full WebApp regression passes 969 tests. Real IndexedDB covers atomic rollback, source freeze, original intent recovery and unchanged cloud-owned legacy task archives.

Legacy `workspace-handoff-v1` receipt/status reads preserve the original transfer identity and archive hash. Frozen copies acknowledge only matching accepted original receipts; explicit retries use the existing legacy protocol and never allocate a replacement transfer. Already cloud-owned copies matching the canonical target remain unchanged during remaining Notes capture. Missing or mismatched legacy proof preserves the frozen source for recovery.

Rendered evidence covers the affected Tasks, planning, projects, lists, recurring-task, history, reminder, statistics, Sync, Notes, Settings and OAuth surfaces. Synthetic interaction/storage evidence does not establish physical-device behavior, assistive-technology operation, installed-host metadata refresh or completeness of an uninspected owner browser inventory.
