# Retained recovery discovery

`list_task_recovery_records` provides read-only agent discovery for the [history recovery browser](history-recovery.md). `list_companion_tasks` continues to expose active nonarchived task queries; exact `get_task_recovery` reads remain available for known task IDs.

## Input and ownership

The MCP tool requires `profile:read` and `tasks:read`. Workspace identity and profile come from the authenticated service context. Input accepts only required `recordType` (`archivedTasks` or `terminalOccurrences`), optional current-title/notes `query` up to 256 characters, `limit` 1–100 (default 20), `offset` 0–100000 and `expectedRevision`. Every nonzero offset requires the returned revision. Malformed, stale, account/profile/workspace selectors and unbounded requests are refused.

The reader loads the owned active document, command entries and events in one D1 transaction and validates the canonical journal. Unavailable or corrupt source data fails explicitly. The browser task API uses the same reader. No provider request, command, consent, credential, execution token or game effect is created. Grant synchronization and existing admission remain separate security prerequisites.

## Records and meaning

Archived tasks include Todos, Dailies and Habits without requiring a completion event. Ordering uses current `updatedAtUtc` descending and task ID as the stable tie-breaker. Terminal Daily records include nonarchived completed/skipped occurrences, ordered by civil date descending and occurrence ID. Pending occurrences remain absent from the terminal list. Search uses the existing canonical text-folding contract over current task title/notes; `detailSource` explicitly reports `currentTask`.

Each item returns exact task, record, optional occurrence and completion IDs. `originalSourceCommandId` names a unique matching retained archive or terminal-occurrence command; missing/ambiguous proof reports `sourceAvailability: unavailable`. Current archived state can support a reviewed native unarchive without inventing the original archive event or timestamp. Terminal correction requires the exact zero-game-effect source command and eligible current state.

`nativeAction` reports the existing reviewed operation, `canReview` and a concrete reason. Retained game identities, unresolved effects and canonical recovery/retirement fences block native review. Missing organization references require explicit repair. Canonical observations grant no write permission or acceptance. `latestRecoveryReceipt` reports one retained historical unarchive, Todo restoration or occurrence correction reference by canonical revision; a prior occurrence cycle never proves recovery of the current terminal source.

A linked current completion can report `requiresFreshReview` or `requiresOriginalReceipt`. The discovery reader does not verify fresh Habitica ownership, current account day, Cron readiness or an original Auth score receipt. Linked uncheck remains governed by the separate [preview/accept/original-receipt contract](linked-task-recovery.md).

## Next operation

Use exact returned IDs with `get_task_recovery`, then the reviewed `prepare_task_command`, `unarchive_task` or `correct_occurrence` and original `get_task_command` receipt. Supported linked completions use the separate linked review tools. Native and linked actions retain distinct approval, identity and uncertainty boundaries. Missing retained records do not establish inactivity, missed dates, streaks or game outcomes.

Synthetic query and MCP/browser tests cover owned discovery, pagination, text search, source/receipt references, missing proof, game fences, scopes and profile isolation. Provider, live account, hosted-client and physical-device qualification remains separate.
