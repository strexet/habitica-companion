# Project-filtered retained task history

Status: local implementation; unpublished. Owner modules: Application completion/Habit queries, shared Worker history/organization reads, reviewed MCP schemas, History and Projects routes.

## Current placement and retained events

Completion and Habit history accept optional exact `projectId` or `unassigned:true`. Both selectors cannot combine. Project selection reads the current retained canonical task assignment, rather than placement in an accepted event snapshot. Text, task, activity kind, direction and UTC bounds retain AND semantics. Full matching counts precede stable ordering and revision-bound paging. Existing absent-field encodings and unfiltered rows remain unchanged.

An exact selector matches the raw retained project ID, including archived and logically removed projects. Current Unassigned includes null placement and references to a retained removed project. Archived projects remain assigned. Exact removed-project selection remains distinct from Unassigned. A missing exact project fails `history_project_not_found` rather than silently returning all tasks or an empty successful page.

Filtered rows include optional `currentPlacement`: exact project ID/name, project archive/removal flags, Unassigned status and `basis:currentRetainedTaskAssignment`. The current placement label does not establish event-time membership. Accepted title/notes snapshots, current-detail fallback provenance, precise UTC timestamps, archive flags, completion/restoration/correction identities and Habit direction remain unchanged. Archived completed tasks stay discoverable.

## Interface and agent discovery

`/todo/history` exposes all retained project identities with full UUIDs and archive/removal labels, including duplicate names. Project context and each filtered event explicitly describe current assignment. `/todo/projects` provides a general history entry and per-project links carrying exact project, Workspace and browser/cloud source identities. Malformed or mismatched links refuse event fallback. Manual project selection or clearing filters explicitly starts a current read. Account/source changes clear private history state.

The existing `list_projects` MCP read accepts `includeRetained:true` for exact retired identity discovery, with unchanged category intersection, full counts and revision-bound pages. The default remains active-only. `get_completed_history` and `get_habit_history` expose the same project/Unassigned selectors with existing `tasks:read` permission. No account selector, publication, background execution or mutation permission follows from discovery.

The project selector applies to completion/Habit event history. The existing archived-task and terminal-occurrence recovery browser retains independent discovery filters and exact recovery identities. Native Todo restoration retains the original completion and requires reviewed acceptance. Linked, archived and uncertain recovery guards remain unchanged. Lost acknowledgement requires the original command receipt; current state equality never proves acceptance.

## Verification boundaries

A shared neutral fixture contains23 explicit completion/Habit examples with current reassignment after accepted snapshots, duplicate project names, archived/removed identities, null assignment, UTC/text intersections and guarded pages. Removed project fixtures retain actual-shaped removal provenance. Native and shared controls preserve original events and document bytes. Actual local SDK/Auth/Workspace/D1 controls exercise retained discovery, exact project queries, stale/malformed/conflicting selectors and cross-profile refusal with zero provider writes.

Component controls cover scoped navigation, private account cleanup, archived/Unassigned behavior and original native restore receipt recovery. The browser recipe qualifies current Release History/Projects routes,160 Razor state combinations across Chromium/WebKit, readable light and obsidian schemes, phone/desktop widths, normal/enlarged text, geometry, contrast, focus and disabled controls, plus two hydrated scoped-navigation/original-receipt journeys with isolated Workers/D1 and native IndexedDB.

Evidence resides under `.skill-tree/history/validation/project-retained-history-2026-11-03/`. Physical devices, assistive technologies, production OAuth/CORS and installed-PWA/service-worker lifecycle remain unqualified. Changed-route harnesses block service workers. No new publication or real account/provider operation is included.
