# Mobile Navigation Study

## Evidence boundary

The study records public documented workflows reviewed on 2026-10-17, current repository behavior and a local synthetic prototype. Public product documentation supplies flow evidence; the study contains no authenticated competitor-app tests, measured competitor task times or human usability results. Prototype interaction checks supply local design evidence, not production feature acceptance or real device qualification.

The [research prototype](../prototypes/navigation-review.html) frames the [navigation demo](../prototypes/mobile-navigation.html) at phone, tablet and desktop widths. Demo tasks exist only in memory. The prototype has no credential form, external dependency or account connection. Browser reload resets demo state.

## Public workflow inventory

| Product and source | Documented flow | Companion implication |
| --- | --- | --- |
| [Todoist Quick Add](https://www.todoist.com/help/todoist/features/use-task-quick-add-in-todoist-va4Lhpzz) | Task entry starts from a prominent Add control; extra task actions appear during entry. Platforms include mobile. | Borrow visible capture; keep natural-language parsing optional and expose manual fields. |
| [Things scheduling](https://culturedcode.com/things/support/articles/2803579/) | Today, Upcoming, Anytime and Someday express when work becomes relevant. | Adapt list names while keeping planned date separate from deadline. |
| [Things default lists](https://culturedcode.com/things/support/articles/4001304/) | Inbox captures unprocessed work; Logbook retains completed and cancelled records. | Borrow capture-first organization and durable history; require explicit restore semantics. |
| [TickTick recurrence](https://help.ticktick.com/articles/7055782206349770752) | Recurrence can follow dates or completion; individual instances have separate editing options. | Adapt occurrence-specific controls. Avoid silently converting date recurrence to completion-based recurrence. |
| [Amazing Marvin recurrence](https://help.amazingmarvin.com/amazing-marvin/recurring-tasks) | Recurrence authoring distinguishes scheduled copies and deadlines and controls occurrence visibility. | Borrow explicit schedule/deadline language; keep Companion occurrences distinct from game rewards. |
| [Amazing Marvin organization](https://help.amazingmarvin.com/amazing-marvin/categories-projects-and-tasks) | Categories organize areas; projects represent multiple actions. | Adapt optional areas and projects; retain the adopted flat-category and single-primary-project contract. |
| [Microsoft To Do My Day](https://support.microsoft.com/en-us/todo/my-day-and-suggestions) | Tasks can enter a daily focus list without losing the original list. Unfinished daily selections remain available. | Borrow a focused Today view without deleting unfinished canonical work at midnight. |
| [Asana sections](https://help.asana.com/s/article/sections) | Sections organize tasks inside a project. | Adapt optional project sections without mandatory organization for capture. |
| [Linear custom views](https://linear.app/docs/custom-views) | Reusable filtered views provide alternative access to existing records. | Borrow saved filters without duplicating canonical tasks. |
| [Jira boards](https://www.atlassian.com/software/jira/guides/boards/overview) | Boards visualize selected workflow states. | Keep boards optional; reject sprint rituals, staff monitoring and mandatory workflow administration. |

## Borrow, adapt and reject

| Pattern | Decision | Rationale |
| --- | --- | --- |
| Persistent capture entry | Borrow | Capture remains reachable from every main destination. |
| Title-first entry with optional detail | Borrow | Casual use requires one field; hierarchy remains optional. |
| Daily focus list | Adapt | Companion combines selected tasks and recurring occurrences while preserving origin and history. |
| Scheduling and deadlines | Adapt | Distinct labels prevent a planning change from silently changing a deadline. |
| Recurring copies | Adapt | A task/date occurrence appears once; persisted completion and skip history remain separate from recurrence generation. |
| Smart lists and project views | Borrow | Queries reference existing canonical records instead of copying work. |
| Swipe-only commands and drag-only planning | Reject | Keyboard and single-pointer alternatives remain mandatory. |
| Automatic reward undo or uncertain-effect replay | Reject | Canonical restore cannot reverse an unconfirmed external effect. |
| Corporate reporting and obligatory boards | Reject | Personal work remains useful without project management rituals. |

## Personas and default visibility

A casual organizer captures a task and returns later to plan the task. Default entry therefore exposes the title and a short review. Dates, notes, labels and projects remain optional. A personal-project organizer needs projects, sections, labels and progress without mandatory hierarchy. A game-focused owner needs Dashboard, Spells, Equipment, Pets & Mounts, Party and Quests to remain findable. The prototype provides direct named destinations under More; the study does not establish whether frequent game use warrants a primary shortcut.

## Historical prototype navigation contract

The superseded prototype uses five primary destinations: Today, Inbox, Plan, Projects and More. Plan contains Upcoming, Week and Month with an agenda. More exposes completed history, search, recurring work, gameplay, connections and settings. Desktop uses the same destinations in a side rail. Task capture remains visible independently of navigation. Phone editors open as scrollable modal sheets and retain labelled manual date controls.

Unplanned, unassigned Todo capture enters Inbox and states the destination after saving. Planned date and deadline remain independent. A review describes Companion-only persistence before saving; game projection requires a separate explicit choice. Pending or uncertain game outcomes require labelled state and original receipt lookup. A local canonical completion or restore never proves a confirmed or reversed Habitica reward.

The prototype models the current own-account boundary: party and quest destinations are read-only; no departure control exists. Dashboard, spells and equipment are navigation targets rather than functioning gameplay screens. Project creation, recurrence authoring, persistence, connection setup, real sync failures and production accessibility behavior remain outside the prototype.

## Prototype journey evidence

| Journey | Local result | Limit |
| --- | --- | --- |
| Capture | Add, enter title, review and save; destination message identifies Inbox. | In-memory demo only; no durable storage. |
| Reschedule | Edit, set plan and deadline separately, review and save; task moves from Inbox to Upcoming. | Fixed synthetic dates; production timezone rules remain separate. |
| Complete and restore | Complete from Upcoming; find the record in Completed; restore the record. | No Habitica score or inverse reward. |
| Daily occurrence | Today displays one synthetic Daily row. | Schedule generation and occurrence authoring remain unimplemented in the prototype. |
| Habit event | Positive and negative controls record a synthetic counter and announce the local-only result. | No real Habitica call. |
| Project access | Projects expose named project tasks and optional capture context. | No project creation or persistent progress acceptance. |
| Calendar | Plan provides accessible day buttons and a day agenda. | No drag interaction, external calendar or timed-event support. |
| Game and connection discovery | More names gameplay and connection destinations explicitly. | Destination placeholders do not qualify the corresponding application flows. |
| Keyboard | Entry focuses the task name; Tab reaches More options; review focuses Save. | Full screen-reader and mobile keyboard qualification remain open. |

Responsive measurements cover 360, 390, 430, 768 and 1024 CSS pixels. Each measured document has matching scroll width, no visible control below forty-four pixels in height and no overlap between Add and navigation. A 360-pixel-wide, 460-pixel-high frame with 200% text retains matching scroll width and separate navigation/capture geometry. A shortened frame approximates reduced space; the approximation does not qualify an actual mobile virtual keyboard, safe-area inset, touch reach or assistive technology.

## Adoption and remaining qualification

The owner directed automatic adoption of further local contracts and explicitly included the E01 area in the implementation run. The intake permits screenshots or documented workflows for the inventory. The dated public-flow inventory satisfies that evidence alternative, and the checked prototype is adopted as the local design baseline on 2026-10-18. Current-route journey preparation and local results reside in [task-journey-qualification.md](task-journey-qualification.md), implemented under T-06336249-6901-4d10-8df1-229ca9bcad6b. Authenticated competitor journeys and actual human/device qualification remain acceptance requirements of blocked release task T-d76d2a9a-1318-4e63-9bb4-73f620b108cc; preparation never qualifies actual observations. No owner usability feedback, competitor session or real-device result is claimed. Automatic local contract adoption removes a repeated design-approval question; adoption does not qualify hands-on research.

Detailed UI units can be researched against the contract and existing canonical APIs. Owner selection on 2026-10-19 permits development implementation and feature exposure before hands-on E01 qualification; broader release retains the gate. Prototype evidence qualifies the design baseline only. Every UI unit requires feature-specific persistence, identity, error recovery, accessibility and local browser checks. Release gates retain actual two-account, client, virtual keyboard, assistive-technology and device qualification.

## Current production navigation and journey pack

The owner Todo structure supersedes the prototype navigation: Today, Habits, Dailies and Tasks are immediately visible under Todo; Projects, Statistics and Sync follow, with Inbox, Planning, Saved lists and History as secondary links. [Current task journey qualification](task-journey-qualification.md) owns exact current-route steps, reproducible synthetic setup, observation forms and explicit device/human handoff. Its local evidence does not close the deferred release gate.

## Historical prototype defaults and advanced behavior

Global Add opens title-first Todo capture. Normal native urgency and Companion-only persistence are defaults; no project, label, date, recurrence or game projection is required. Review precedes acceptance through the existing command contract. The success message names the saved destination. Advanced details expose task type, notes, separate plan/deadline dates, hierarchy and supported recurrence without requiring status administration.

Today combines planned/overdue Todo work and canonical Daily occurrences without treating Habitica due state as a Companion schedule. Plan offers Upcoming, Week and Month with a keyboard-accessible agenda alternative. Inbox contains unplanned Todos without a primary project; Anytime contains otherwise unscheduled actionable work. Projects retain optional hierarchy and Todo-based progress. More provides search, retained history, recurring work, gameplay and connection/settings destinations. A destination becomes a production navigation target only when the target route works; the prototype does not authorize placeholder production screens.

Recurrence produces one task/date occurrence; retained completed/skipped dates override generated pending work. Pause has Companion-only meaning and never promises to pause Habitica penalties. Habit events require an explicit direction. Canonical restore retains history and needs a reviewed domain contract before production exposure; a demo restore supplies no permission to reverse game rewards.

## Production acceptance carried forward

| Area | Required local production behavior | Separate qualification |
| --- | --- | --- |
| Save and recovery | Show saving, saved in Companion and game projection separately. Preserve the original command after uncertainty; offer receipt lookup instead of a new action. | Actual client/network behavior remains a live gate. |
| Unavailable and empty | Distinguish disconnected, offline/stale, failed reads, empty workspace and no filter matches. Preserve editor content on recoverable failures. | Device interruption and background behavior remain unqualified. |
| Recurrence and dates | Label planned date, deadline, occurrence date and rule timezone separately. No duplicate Daily row or silent date-to-instant conversion. | Device timezone data and actual keyboard/date-picker behavior require qualification. |
| Touch and layout | Provide visible targets at least forty-four CSS pixels high, wrapping labels, safe-area padding and capture clear of navigation. Check 360/390/430/768/1024 widths and enlarged text. | Short-frame checks approximate reduced space; real keyboard and safe-area checks remain open. |
| Keyboard and assistance | Keep labelled controls, visible focus, dialog entry/return focus, Escape handling, status announcements and alternatives to drag/swipe. | Full screen-reader and assistive-device checks remain open. |
| Game and party | Preserve direct named game destinations, resource-spending previews, own-account boundaries and permanent departure prohibition. | Shared-party effects and new live rounds remain excluded. |

Local review uses `python3 -m http.server 5189 --bind 127.0.0.1 --directory skill-tree/prototypes` from the repository root, followed by `http://127.0.0.1:5189/navigation-review.html` in a browser. Stop the review server after the review. No account sign-in is required.
