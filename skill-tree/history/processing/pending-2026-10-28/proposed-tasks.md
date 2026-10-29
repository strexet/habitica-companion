## Expose exact task adoption through shared reviewed MCP commands

Goal:
- Complete browser/MCP parity for binding an exact owned native Habitica task to an existing canonical task, including retained completed/archived Todos.

Current behavior:
- Workspace index.ts exposes browser-only /projection/preview and /projection/adopt. adoptProjection checks kind, Todo completion, native fingerprint and unique mapping. Existing replay detection depends on mutable mapping revision; task-tools.ts has no equivalent adoption tools or immutable original adoption receipt.
- TodoTaskLinkEditor already displays both exact IDs, native details, schedule differences and pending effects. Checklist conversion, tag reviews and source retirement have separate reviewed MCP contracts.

Desired behavior:
- Add shared bounded adoption preview, acceptance and original-receipt contracts used by the browser and MCP. Resolve the exact canonical task and supplied native task ID through authenticated ownership checks; retain the original mapping acceptance after later workspace revisions.
- Record a finite migration parity inventory covering source/target reads, exact native inspection, adoption, consent/tag status, conversion, retirement and original receipts. Distinguish missing agent workflows from intentional direct-user device/consent boundaries.

Touch:
- workers/companion-workspace/src/index.ts, projection-adapter.ts, a scoped preparation/receipt store and additive local migration if needed; shared/MCP task tools; Workspace cloud client, task controller/link editor; SDK, D1 and typed browser tests.

Discovery allowance:
- Reuse the current Auth inspect gateway and shared command/receipt patterns. Audit the named migration flow before adding tool schemas; record additional independent gaps as Pending.

Out of scope:
- Native task creation, scoring, deletion, content/schedule synchronization, account selection, automatic tag changes, bulk adoption, live migration, publication and credential provisioning.

Implementation constraints:
- Require exact profile, workspace, connection/access/grant epochs, canonical/native IDs, task kind/completion state, fresh native fingerprint and unresolved-effect checks. Reject duplicate native ownership and conflicting existing mappings. Never infer identity from names, aliases or a bounded recent-completed list.
- Preview every changed correspondence and disclose zero game effects. Bind acceptance to original request/command identity with atomic revision checks. Lost responses recover the original receipt; expired or known unaccepted reviews require a new review, while unknown acceptance never permits replacement.
- Support retained archived/completed task inspection without unarchiving, fabricating completion timestamps or granting rewards. Preserve mapping IDs, native task metadata, canonical content and history. Existing task read/write scopes and independent consents remain enforced.

Questions and required clarifications:
- [Resolved - repository evidence] The browser adoption backend exists and the reviewed MCP counterpart is absent. Exact-ID inspection and original metadata-only receipts close the bounded workflow gap without changing the required-correspondence policy.

Validation:
- Synthetic two-profile SDK/HTTP/D1 and C# transport/controller tests cover active and completed/archived targets, wrong kind/completion/owner, duplicate mapping, fresh/stale/expired review, changed connection, unknown effects, atomic rollback, lost acknowledgments and original receipt reads after later revisions. Assert zero native writes and scores.
- If the shared link UI changes, qualify every affected route at phone/desktop widths, normal/enlarged text and built-in light/dark themes including obsidian-glow; keep host/device evidence separate.

Acceptance:
- Browser and MCP can review and accept only an exact owned task/native correspondence with durable original-receipt recovery and zero native task or reward mutations.
- The migration parity inventory identifies supported agent steps and intentional platform boundaries; retained-history, stale-review and cross-profile controls reject uncertain identity without unsafe replay.

Documentation updates:
- Update companion-workspace-contract.md, features.md, testing.md, affected UX guidance and the packaged task skill; retain research references from skill-tree/history/processing/pending-2026-10-28/research.md.

Risks:
- An adopted wrong record can redirect later game effects. Fail closed on ownership, state, fingerprint, epoch and original-receipt uncertainty.


## Validate stored tag coverage against scoped desired membership

Goal:
- Preserve confirmed stored group/tag correspondence across workspace revisions unrelated to the desired tag or membership.

Current behavior:
- group-tag-status.ts marks membership needsReview when latest.desiredRevision differs from document.revision. group-tag-status.test.mjs explicitly expects invalidation after an otherwise unchanged revision increment.
- Both Worker retirement-policy.ts and C# WorkspaceConversionRetirementPolicy repeat the global-revision check. Current retained Companion Development evidence has an exact tag and member identities but membershipState needsReview.

Desired behavior:
- Share a deterministic scoped desired-evidence check across status and retirement readiness. Compare the exact group identity/name/lifecycle, native tag identity and complete desired canonical/native member set, preserving proven membership when only unrelated task, projection or organization records change.

Touch:
- workers/shared/group-tag-status.ts, tag-membership.ts, tag-projection.ts and relevant planner/validation helpers; Worker retirement-policy.ts and C# WorkspaceConversionRetirementPolicy; portable fixtures, status/API and retirement tests.

Discovery allowance:
- Trace the existing desired-membership and retained-receipt structures before choosing an additive fingerprint or equivalent complete comparison. Preserve older provable evidence; unavailable proof remains needsReview.

Out of scope:
- New native verification, tag dispatch, consent changes, synthetic confirmation, automatic member removal, retired-source resurrection, live cleanup and release operations.

Implementation constraints:
- Keep workspace revision as the coherent paging guard; remove only the false equivalence between a global revision change and changed desired tag content. Names, group lifecycle, member changes, native task/tag remaps, account/connection changes, conflicts, ambiguous latest intents, missing member evidence and unknown results must still require review.
- Compare complete sets deterministically and require confirmed per-member evidence. Generic task text, priority, deadline, score and unrelated projection revision changes cannot invalidate unchanged membership merely by advancing global counters.
- Stored confirmation never claims a fresh provider observation. Retirement continues to require separate fresh native verification and exact source/replacement checks. Keep Auth scopes, consent, ownership and uncertainty fences unchanged.

Questions and required clarifications:
- [Resolved - code and retained observation] The global-revision comparisons cause the false invalidation; the requested fix is scoped evidence reuse, with no permission to infer provider freshness.

Validation:
- Neutral C#/Worker fixtures and API tests cover unrelated document increments, task completion and metadata changes with unchanged membership; project rename/archive/removal, task move, exact native remap, account change, ambiguous intents, missing evidence and incomplete/unknown members must remain reviewable.
- Retirement-policy tests assert parity with status and preserve the fresh-provider gate, source identity and original receipts. Read-only status must produce zero writes and dispatches.

Acceptance:
- Unrelated workspace revisions preserve proven stored tag confirmation while exact desired group, native identity and complete member evidence remain unchanged.
- Status and retirement policy agree on scoped correspondence and still reject changed, incomplete, ambiguous or uncertain evidence without claiming fresh native verification.

Documentation updates:
- Update required-correspondence and tag status contracts, features.md and testing.md with the stored-evidence/fresh-observation distinction.

Risks:
- Ignoring relevant changes could hide a real correspondence gap. Centralize the comparison and retain conservative unknown states when older records cannot prove the required scope.


## Recover terminal non-acceptance of stale tag reviews safely

Goal:
- Release stranded tag-review UI intent only after the original review is durably proven unaccepted; preserve genuinely uncertain accepted operations.

Current behavior:
- group-tag-reviews.ts rejects stale document/ledger/authority reviews, but original() returns savedInCompanion null and outcomeUnknown true whenever receipt_json is absent. Expired unaccepted rows are later deleted by group-tag-retention.ts.
- WorkspaceTagPanel retains acceptanceUnknown after an earlier uncertain attempt even when a later response reports known non-acceptance. The exact original 5e625e2a-6f33-4515-a344-55ca2eb3a8bf still reads unknown; the observation does not prove successful acceptance or safe replay.

Desired behavior:
- Introduce typed prepared, terminal-unaccepted, accepted and accepted-unknown review outcomes with durable original identity. Atomically invalidate or close a stale/expired original review before allowing a replacement; original acceptance wins any concurrent race.
- Use the same original outcome contract in browser and MCP. The browser offers a fresh review only for proven terminal non-acceptance and continues original receipt lookup for ambiguous saves or native work.

Touch:
- group-tag-reviews.ts, group-tag-retention.ts, existing review migration/schema and group-tag API; shared/MCP schemas; WorkspaceCloudClient.Tags.cs, WorkspaceTagPanel.razor and typed contracts; D1, SDK, component and fault tests.

Discovery allowance:
- Inspect receipt/work insertion guards and retention before defining terminal tombstones. Reuse original durable review IDs; do not classify a bare missing row or HTTP409 as failure.

Out of scope:
- Relaxing reviewed membership/fingerprint checks, automatically accepting fresh previews, native tag retries, granting tag scopes/consent, gameplay changes and live recovery of the recorded production command.

Implementation constraints:
- A current read showing no receipt is insufficient while a concurrent acceptance remains possible. Terminal non-acceptance must fence future acceptance atomically under the exact review/profile/epoch and persist bounded original outcome evidence.
- Keep missing, unreadable, purged-without-proof, partially committed, executing and unknown native outcomes uncertain. Never clear an uncertainty fence based on expiry, a generic error string or observation of a changed tag alone.
- New review confirmation must cover the new complete member/removal disclosure. Preserve original request/command IDs, accepted work IDs, native attempt IDs, authority changes and receipt recovery while background execution is paused.

Questions and required clarifications:
- [Resolved - source evidence] The defect is outcome classification and UI recovery after proven non-acceptance. The exact-document review precondition remains binding; no weaker automatic acceptance policy is requested.

Validation:
- D1 race/fault tests cover stale/expired rejection before acceptance, delayed acceptance competing with terminal closure, acknowledgment loss after commit, deletion/retention, changed connection/grant/consent, missing records and unresolved native work. Count native writes to prove zero replay.
- Typed MCP/HTTP and component tests cover sticky unknown followed by proven terminal refusal, original receipt recovery and fresh reviewed replacement. Qualify changed tag UI routes across required themes, widths, text scales, focus, disabled controls and contrast.

Acceptance:
- The original tag review can be durably classified as terminal unaccepted without racing or overwriting an accepted operation, and accepted/unknown records retain original-receipt recovery.
- Browser and MCP permit a new review only after proven terminal non-acceptance; missing evidence and genuine uncertainty remain fenced with no repeated native effect.

Documentation updates:
- Update tag-review recovery/retention, features.md, testing.md and UX copy with explicit original outcome semantics and real-host limits.

Risks:
- False non-acceptance can duplicate native mutations. Storage failure, unavailable proof and ambiguous races must preserve uncertainty rather than unblocking replacement.


## Persist versioned project links in existing Notes and MCP contracts

Goal:
- Add one optional canonical project association to personal Notes without replacing the current notebook, storage or note identity.

Current behavior:
- NoteVersion and NoteCommand in NotesModel.cs/shared notes.ts contain title, Markdown and lifecycle metadata but no project association. D1 stores personal_notes, versions and receipts; Notes uses independent consent and notes.read/notes.write.
- Notes command hashes serialize exact existing JSON fields; notes-wire-v1.json verifies C#/Worker parity. search_notes filters text/trash before twenty-row paging and has no project filter.

Desired behavior:
- Persist optional projectId with every relevant note version and reviewed create/update intent; explicit clearProject detaches, while omitted association fields preserve the current link. Legacy documents default to unassigned.
- Expose exact project and unassigned search filters, current and exact-version association metadata, matching browser cloud transports and reviewed MCP previews/receipts. Resolve a display name only when canonical workspace read access is available.

Touch:
- Domain NotesModel/JSON, Application NotesEngine/Service/cloud contracts, IndexedDbNotesDatabase, shared notes/note-tools and MCP note tools, notes-api/note-tools-api, additive D1 migration if necessary, cloud client/controller compatibility and neutral storage/API fixtures.

Discovery allowance:
- Inspect the existing Notes receipt/hash and version model plus local/cloud project authorities. Retain one existing notebook store; choose additive optional fields and source/workspace binding without shadow note copies.

Out of scope:
- Multiple project membership, tags/folders, task conversion, adding Notes to taskIds, native Habitica projection, Markdown rewriting, automatic cloud upload, broad project management permissions and remote migration/publication.

Implementation constraints:
- Preserve noteId/notebookId, created timestamps, exact Markdown/title bytes, existing contentHash meaning, revision/history/trash/export/restore and durable receipts. Legacy absent fields and old command hashes remain compatible; any new command identity version needs neutral C#/Worker fixtures.
- Verify exact project ID, owner, workspace/source and active lifecycle for new or changed links. A stale archived/removed/unavailable link remains visible, editable, rebindable or clearable; project removal never deletes Notes. Identical names or IDs in another owner/source cannot satisfy a link.
- Keep project association revisioned under existing optimistic concurrency. Association-only updates preserve Markdown and retain the prior link in history; trash/restore/version recovery follows explicit retained metadata. Source switching never copies or rebinds links implicitly.
- Retain Notes consent and read/write scopes; ordinary task/project grants cannot reveal private note bodies. Project-name lookup requires applicable workspace read access and grants no project mutation. Filter the complete eligible set before paging; stale notebook revisions restart pages.

Questions and required clarifications:
- [Resolved - source contract] One optional project per note, existing storage preservation, exact IDs, explicit clearing, stale-link visibility and no cascade deletion are selected requirements. Cross-owner/source link laundering is forbidden.

Validation:
- Neutral C#/Worker legacy and extended command fixtures cover exact JSON/hash compatibility, metadata-only edits, omitted/clear fields, retained versions and unchanged Markdown hashes. Local storage and D1 tests cover read/write, archive/trash/export/restore and conflict/rollback behavior.
- SDK/API tests cover more than twenty matching notes, text/trash/project/unassigned filters, page stability, duplicate names, missing/archived/removed projects, two profiles, denied read/write scopes, changed epochs and recovery after response loss. Existing Notes UI continues editing without dropping unseen association metadata.

Acceptance:
- Existing Notes storage round-trips optional versioned project links and legacy unassigned documents without changing note identity, Markdown or historical command/hash semantics.
- Reviewed browser/MCP transports support exact attach/move/detach and correctly paged project/unassigned reads while preserving ownership, private-content access, concurrency and original-receipt recovery.

Documentation updates:
- Update technical.md, Notes feature/permission/portability contracts, testing.md and packaged Notes guidance; record local synthetic evidence separately from host/device qualification.

Risks:
- A metadata edit could overwrite text or leak private Notes through a project endpoint. Enforce omission semantics, revision checks and independent Notes authorization at every transport.


## Expose project-linked Notes in Notes and project views

Goal:
- Make the optional project context usable in the existing Notes editor and exact Companion project view.

Current behavior:
- NotesPanel.razor offers a standalone library/editor, source selection, draft/conflict recovery and version/trash controls. TodoProjectsPage.razor has canonical project task navigation but no linked Notes section.
- The backend project-link contract is a separate required predecessor. No new Notes store or task entity is needed.

Desired behavior:
- Add a project picker and explicit Unassigned action to the Notes editor, plus combined text/trash/project/unassigned filtering without changing the general all-notes view.
- Add a Notes section for an exact project with paged search, open/create-in-project and reviewed attach/detach of existing Notes. Preserve private drafts, original command ownership and independent source/consent state.

Touch:
- NotesController/drafts, NotesPanel.razor, TodoProjectsPage.razor and the actual project components/read facade, Notes cloud adapters, scoped styles and focused component/browser qualification fixtures.

Discovery allowance:
- Read the predecessor schema and current project selection before editing. Inventory all routes sharing changed components and preserve exact project IDs instead of name matching.

Out of scope:
- Changing Notes persistence or backend schema beyond the predecessor, multiple project selection, implicit uploads, task/game effects, project deletion cascades and real account/device/publication operations.

Implementation constraints:
- Show exact current project context and explicit stale/archived/removed/unavailable link state. New links require eligible canonical projects; existing stale Notes remain visible and detachable. Never infer ownership from a label or silently drop a link during a text save.
- Keep ordinary Notes editing, source switching, trash/history/export, unknown acceptance and keep/export/discard draft handling. Reassociation is a versioned Notes command; show the exact before/after project in review and recover the original receipt after acknowledgment loss.
- Project-only access cannot render private Notes or snippets. Missing Notes consent/read scope shows an explicit unavailable state. No default background transfer or broadened permission follows navigation.

Questions and required clarifications:
- [Resolved - source requirements] The existing Notes editor remains the primary editor; the project view supplies additional context and entry points. Backend acceptance and paging must be complete before UI implementation.

Validation:
- Component journeys cover project selection, unassigned filter, create/open/attach/move/detach, more than twenty results, stale link repair, account/source changes, retained versions, draft/conflict preservation and original-receipt uncertainty.
- Render every changed/shared route in built-in light and dark themes including obsidian-glow, phone/desktop widths, normal/200-percent text, long project/note names, visible primary actions, disabled controls, keyboard focus, geometry and contrast. Record untested real devices, hosts and assistive technology.

Acceptance:
- Users can create, find, open and attach/detach an existing note from an exact project and the general Notes view without losing content, IDs, history or private drafts.
- Stale links, absent Notes permission, revision conflicts and unknown acceptance remain explicit; project navigation does not leak private content or create task/game effects, and required rendered controls pass.

Documentation updates:
- Update features.md, ux-ui-manifest.md and testing.md for the actual Notes/project routes and verified local interaction boundaries.

Risks:
- Shared component changes can hide primary actions or discard scoped drafts. Preserve existing editor ownership and qualify every affected route/state.

