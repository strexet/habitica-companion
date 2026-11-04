# Native task type copies

Status: local implementation and synthetic qualification; unpublished. Owner module: Domain Workspace type-copy policy, Application command engine, Workspace Worker and reviewed task API. Entry points: `/todo/convert`, `/todo/convert/{SourceTaskId:guid}`, `prepare_task_command`, `copy_task_type` and `get_task_command`.

## Command and field semantics

`CopyTaskType` creates one new active native task of a different Todo/Daily/Habit kind. `patch.typeCopy` binds exact `sourceTaskId`, `sourceRevision`, `targetKind`, explicit `copyOrganization` and a complete recurrence only for a Daily target. Command identity v13 extends the previous hash without changing old absent-field identities. Cloud preparation allocates command and target IDs; caller target IDs are refused. One root revision guards atomic source review and target creation. Same request and command identities retain the original target across lost acknowledgements.

The default destination is unlinked. Acceptance requests no provider creation, update, tag reconciliation, score, Cron, enrollment or executor change. Ordinary task correspondence requirements still apply before game activity. Separate explicit linking remains available. Original source kind, state, organization, mappings, activity, goal membership and archive status remain unchanged. Source archival remains a separate supported command; the copy flow grants no combined archival authority.

Copied fields: exact name, notes and native urgency. The optional organization choice retains all current category, project, section and label references only when valid and active. Archived/removed/mismatched references require repair or an explicit empty destination. Original organization never changes.

Reset fields: state to Active, creation/update timestamps, game metadata and checklist/direction settings, planned/deadline/review/stale dates, Todo stage, parent/dependencies and goal membership. The new task has no transferred completion, score or occurrence history and no Habitica correspondence. A Daily target requires an explicit complete native rule, including start civil date, interval, frequency, UTC/IANA timezone and applicable weekdays/month dates. New acceptance requires an available calendar/timezone. The original rule remains on the source. Stored structural rules remain readable when a historical timezone is unavailable.

## Portable lineage and receipts

The new task carries immutable `copyLineage`: original Workspace, shallow full source snapshot, target ID/kind, organization choice, initial target revision, original command/hash, UTC copy time and optional initial Daily recurrence. The retained source snapshot clears nested lineage. Copying an earlier copy creates bounded shallow provenance and leaves the earlier task and original lineage intact.

Native validation requires the exact source identity and a compatible later source revision. Current source fields may legitimately evolve; original accepted snapshots bind immutable historic lineage. Journal validation rejects forged source snapshots, changed lineage in later entries, changed target kind and initial target fields that disagree with copied/reset semantics. Revision-offset transfer retains nested lineage and exact `SourceImports` provenance. Original byte archives and receipts remain retained; mapped outer revisions never rewrite original nested revisions.

Migration `0023-task-type-copy-previews.sql` stores the original complete before/after/copied/reset review on the preparation. Metadata is bounded to256KiB and counted in archive quota. Expiry and MCP grant invalidation release the complete unaccepted command/review byte cost; accepted original reviews remain retained with command history. Repeated preparation reads stored original fields. Expired or stale unsaved root revisions cannot authorize acceptance. Original accepted receipts remain recoverable independently of later source edits. Native copy receipts expose original lineage only for the copy operation; ordinary receipts remain unchanged. Typed transport and account-scoped controllers validate exact source, requested choices, target, hash and original receipt before reporting success. Current-state equality never proves acceptance.

## Interface, privacy and MCP parity

The conversion route offers exact-ID search and twenty-row paging over active, historical and archived sources. Duplicate names remain separate records. Different kind and complete Daily choices precede a focused copied/reset-field review. Explicit acknowledgement enables confirmation. Unknown preparation/acceptance retains original IDs, prevents replacement intents and offers original recovery. Browser mode uses atomic IndexedDB journal storage; cloud mode uses the same reviewed server API. Frozen or unavailable sources remain read-only. Switching accounts clears private source text, previews and receipts.

The existing checklist-to-project protocol remains available on the same route. Unresolved task-copy and checklist preparation cannot overlap through the page. Todo Sync can display the same original full field review during route navigation.

Agent workflows use scoped MCP tools. `prepare_task_command` with operation `copyTaskType` and `copy_task_type` require both `tasks:read` and `habitica.tasks.write`, alongside existing profile/account consent. Read-only or write-only grants cannot disclose or save a private copy review. Source availability and tool discovery grant no publication or real-service execution permission.

## Verification and limits

- Six independent portable all-kind-pair examples with independently generated v13 hashes, historical/linked originals and complete expected targets: `tests/fixtures/workspace/task-type-copy-v1.json`.
- Native engine and journal controls: reset/copy semantics, organization refusal/empty choice, atomic acceptance, duplicates, stale source/root, immutable lineage, structural historical timezone reads and portable export/import.
- Local SDK/Worker/D1 controls: server IDs, stored original reviews and receipts, all six pairs, expiry/stale refusal, cross-profile and missing-scope refusal, copy-of-copy bounds, actual revision-offset transfer and corruption refusal. Provider writes are prohibited; copy work creates no tag jobs.
- Component and typed-client controls: complete loss review, explicit acknowledgement, missing/invalid Daily rule refusal, real browser storage, unknown original recovery, malicious receipt refusal, pagination and account cleanup. Existing checklist conversion controls remain included.
- `task-type-copy-rendered-browser`: current Release routes and current Razor states under Chromium/WebKit, readable light and obsidian schemes,360/1280px widths,100/200-percent text, disabled controls, keyboard focus, geometry and contrast. Hydrated paths use isolated existing-account recovery, real local Auth/Workspace Workers/D1 and native IndexedDB.

Original check attempts, failures and final evidence reside under `.skill-tree/history/validation/task-type-copies-2026-11-03/`. Production migration/publication, real accounts, provider effects, physical devices and assistive technologies remain unqualified. Service workers are blocked in the changed-route harness; installed-PWA lifecycle evidence remains separate.
