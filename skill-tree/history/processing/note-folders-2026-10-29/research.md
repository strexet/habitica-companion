# Notes folder Pending research

Scope: standalone Process Pending for the complete selection.json: the Notes folder request at priority100 and the retained spell incident at priority106. Research changes planning definitions and evidence only. No implementation, build, runtime test, real account call, migration execution, publication or commit is performed.

## Owner decisions

owner-decisions.json records direct replies: single-level folders and zero-or-one folder per note. Standalone folders need no project. Project folders produce project / folder / note organization. The Altai trip example requires separate Hotels and Interesting places folders, with separate detailed notes for each hotel/place. Exact descriptions, reviews, host names, activities and food details remain ordinary full Markdown content.

## Verified current behavior

| Source | Observation | Planning consequence |
| --- | --- | --- |
| src/Habitica.Domain/Notes/NotesModel.cs | NoteVersion and NoteCommand support one optional NoteProjectLink. NotesNotebook contains note receipts and purged identities, without folder entities. JSON rejects unknown fields. | Extend the independent Notes model with explicit compatibility handling; a Workspace task section or Habitica tag is unsuitable. |
| NotesEngine.cs, NotesService.cs and indexedDbStorage.js commitNotes | Current writes guard notebook/note revisions, recover original receipts and retain immutable accepted records. The browser adapter requires receipt count equal to notebook revision. | Folder commands need distinct typed receipts and a coherent combined revision sequence; fake note commands cannot provide folder identity. |
| NotesProjects.cs and NotesQueries.cs | Project authority binds owner, source, Workspace and active canonical project. Complete-set text/trash/project filtering precedes twenty-row paging. | Folder scope and membership must use exact IDs and equivalent transactional authority; no-folder and no-project filters must remain distinct. |
| notes-api.ts and migrations0011/0017 | D1 stores Notes separately from tasks, including current project_json and retained version JSON. Atomic batches guard access, revision, previews and observed Workspace version. | Add folder persistence and atomic membership/lifecycle writes in the Notes namespace without task or game effects. |
| NotesCloudClient.cs and INotesCloudClient.cs | The browser adapter validates full backups, project metadata, filtered results and exact original receipts. Browser/cloud ownership namespaces differ. | Extend typed transport, export/import, cleanup and account/source guards alongside the folder model. |
| note-tools.ts and note-tools-api.ts | Reviewed tools cover note search/read/create/update/lifecycle and project links; no folder management/read schema exists. | Add complete MCP discovery, preview/accept and receipt recovery before accepting the final UI feature. |
| NotesPanel.razor and NotesController.cs | Current library is flat with title/Markdown search, source choice, volatile drafts, history/trash and unknown-save recovery. | Preserve editor ownership and existing recovery while adding folder navigation and placement. |
| TodoProjectsPage.razor and task T-a48aa46e-fd3c-43ff-b9b3-3becddb47599 | Current project page composes task organization/hierarchy; the existing Ready task owns project Notes composition and picker UI. | Keep the existing task unchanged as an explicit predecessor of folder UI. No duplicate project Notes task is created. |
| NoteProjectTests.cs, note-projects.test.mjs and NotesControllerTests.cs | Existing fixtures define legacy/new wire identity, exact Markdown/history, active-project acceptance, independent permissions, paging, rollback, late account responses and lost acknowledgements. | Extend meaningful controls using the existing synthetic model/storage/D1/SDK/component harnesses. Static inspection does not claim a fresh pass. |

source-fingerprints.json records SHA256 values for inspected implementation and test sources. Accepted outcome T-5c55057e-b7fb-47e8-9ddb-f0b0a8e6d373 confirms locally qualified project-link contracts and explicitly leaves project-picker/project-view UI unimplemented. Retained test results are evidence of existing coverage, not newly executed validation.

## Bounded implementation decisions

- Folder scope is fixed to the owning Notes namespace and either no project or one exact project. A folder has no parent folder. Moving whole folders between scopes is excluded; users can create a target folder and move notes through reviewed commands.
- Each note has zero or one folder. A project-folder member has the same project association as the folder; a standalone-folder member has no project. A note without a folder can still belong to a project.
- Clearing folder membership preserves the existing project association. A cross-scope note move must expose and explicitly bind the required project change atomically; ordinary title/body edits preserve unseen folder/project metadata.
- Create/rename/remove-empty are the bounded folder lifecycle. Removal refuses current members including trashed notes, preventing later restore from losing placement. Historical references retain a removed identity tombstone. No bulk/cascading note deletion is introduced.
- Folder IDs establish identity; duplicate labels never select a write target. Names are private Notes data. Project-only access grants no folder-name, note-title or snippet read.
- New project folders and assignments require current active own-project authority; stale references remain readable, content-editable and explicitly detachable. No implicit detach follows project archival/removal or a failed metadata lookup.
- Existing accepted note command hashes, exact source, IDs, retained versions and original receipts remain unchanged. Extended backup/wire handling must preserve legacy input and reject unsupported destructive writes. Existing source isolation, account consent, quotas and lifecycle deletion rules remain binding.

The first two product decisions come directly from the owner replies. Empty-only removal, immutable folder scope and explicit project-change review are conservative limits of the researched implementation contracts, rather than claims of additional owner requests. Recursive folders, multi-folder membership, shared folders and whole-folder scope moves require separate future intent.

## Decomposition and dependencies

Four priority100 units share Pending origin P-51d5734b-82dc-4f91-82c6-d4eca50a3114:

1. folder-model supplies versioned folder commands, local persistence, filtering, backup and portable fixtures.
2. folder-cloud consumes the model/fixtures and supplies D1/API/client behavior.
3. folder-mcp consumes cloud acceptance and supplies reviewed agent parity for the full management and note-placement workflow.
4. folder-ui consumes MCP parity plus the existing project Notes UI task, then qualifies the general Notes and project journeys.

Dependencies follow actual produced schemas, transports and project-view components. The units remain independent main task definitions with explicit depends_on edges; no source/sibling relationship or active-run membership is inferred. Existing project UI task provenance, priority and body remain unchanged.

## Retained spell incident

AppSessionController.cs CastSpellAsync still counts acknowledged calls separately from the requested count and exposes dispatched uncertainty, quota/cooldown, cleanup and refresh states. AppSessionController.SpellOutcomesTests.cs covers the eighteenth-call429 with17of40 acknowledgements, dispatched transport/cancellation uncertainty, pre-dispatch cancellation and restoration refusal. AdaptiveRequestPacer.cs and HabiticaApiClient.cs retain serialized admission/header reconciliation, reserve, Retry-After/reset pauses and recovery without mutation replay.

Accepted outcomes T-b89fac23-3ac2-4c64-8560-be7a5d796a81 and T-e7d542d4-4a47-4155-afc9-ab0dfb457fa0 cover the local feature and durable original gear recovery. The retained source incident still lacks current loaded PWA build identity and sanitized physical-iPhone evidence. P-8e7323df-2f47-4e19-864d-821ed108e7f2 therefore remains Pending for incident qualification, without a duplicate implementation task. Any live reproduction needs a concrete bounded owner profile. No fresh runtime/provider result or incident closure follows static research.

## Validation and controller boundaries

Only planning/prose/index/dependency consistency checks are executed for processing. Implementation contracts require neutral C#/TypeScript fixtures, disposable IndexedDB/D1/SDK controls, browser-client/component interactions and rendered light/dark Notes/project routes including obsidian-glow at phone/desktop widths and normal/200-percent text. Device, assistive-technology and hosted-user paths remain explicitly separate.

No new third-party behavior is assumed: planning uses the existing stack and named local transaction/test contracts. Official platform research is required during implementation only for new platform semantics. No package upgrade is proposed.

The pre-existing Implement All checkpoint is stopped by an owner stop request. Standalone processing preserves the checkpoint and frozen snapshot; new folder units remain outside the old run. The processing receipt is stored separately. Standalone Process Pending prints the complete prioritized list and awaits an actual owner next-action selection. No root selection, continuation or execution is inferred from Ready state.
