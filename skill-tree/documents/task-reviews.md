# Voluntary Task Review

## Current contract

reviewTask changes only optional review/stale metadata on one exact nonarchived canonical task. The command never completes, reopens, reprioritizes, plans, scores or dispatches a game effect. Existing linked/pending/unknown mappings and accepted game commands remain untouched. Review cues are independent of all-day deadlines, planned dates, recurrence and notification permission. No routine is mandatory and no inactivity duration automatically marks work stale.

patch.review contains action and explicit timeZoneId. Supported display zones are UTC and available IANA region identifiers; unknown zones refuse. Scheduling inputs are exact UTC instants with seconds and up to seven fractional digits, rather than ambiguous civil-date shorthand. The editor displays the exact UTC instant, chosen display zone and offset. A user choosing a local civil time must resolve DST ambiguity explicitly before entering the UTC instant. No local-time conversion silently selects a fold or shifts a nonexistent time.

| Action | Explicit input | Result |
| --- | --- | --- |
| set | reviewAfterUtc | Replace or introduce the review cue; past cues can be explicitly due |
| snooze | reviewAfterUtc | Require an existing cue and a strictly later future instant; no review is claimed |
| clear | No instant | Clear the review cue without a completed review |
| markReviewed | No instant | Record one voluntary review and clear the consumed cue; a new date needs a separate explicit set |
| setStale | Manually chosen nonfuture staleSinceUtc | Set the independent manual flag; no clock-based inference |
| clearStale | No instant | Remove the manual flag without claiming a review |

Every accepted action retains one TaskReviewEvent with exact task/command identities, revision, action, UTC acceptance time, display zone and resulting cue/flag values. Only markReviewed represents a completed review. Clear and snooze remain distinguishable historical actions. MarkReviewed retains a separate stale flag until an explicit clearStale. Task archival retains review history. Legacy review/stale fields remain valid without invented past events.

## Ownership, compatibility and recovery

The optional reviewEvents document collection is absent from old snapshots; command patch.review uses identity v10 while unused extensions preserve previous hashes and enum numbers. Strict older clients can reject documents containing the extension. Coordinated browser/Auth/Workspace/MCP readers are required before publication; no D1 migration, secret, new OAuth scope or executor activation is introduced.

All actions use existing exact preview/accept/original receipt machinery and workspace revision guards. Repeated original command IDs add no history; changed payloads conflict and stale revisions refuse. Browser account/source changes discard stale drafts/responses. Unknown acceptance recovers through the original get_task_command identity, without a replacement intent or game replay. C# and Worker validators check task references, event identity/revision uniqueness, causal journal receipts, current cue state and zero effects. Document capacity bounds include retained review events.

get_task_reviews provides bounded read-only history for an exact task ID, including archived tasks. Newest revision first; pages require unchanged task ID and returned revision. Current task fields come from get_companion_task; history does not infer task completion or reward eligibility. The Organize editor on /todo/tasks exposes voluntary review inputs, due/future observations, manual flags and retained actions through the same native command review panel. Disabled stale/source/unknown states preserve original receipt recovery. Review editor availability grants no browser notification permission and sends no push message.

## Qualification boundary

Shared six-action v10 hashes are checked independently by C# and Worker code. Synthetic model tests cover every mapping state, seven action transitions, legacy fields, fractional UTC, invalid/mixed/projected intents, stale revision, idempotency, forged archive history and query paging. An authenticated actual SDK fixture checks advertised schemas, narrow read/write grants, exact preparation/acceptance/recovery, cross-profile refusal and zero upstream game writes. Component tests exercise UTC/timezone validation, due/future/legacy fields, clear/snooze/review distinctions, manual flags, stale context and account changes. Rendered theme/size evidence is recorded separately in history/validation/task-reviews/result.json. Physical devices, assistive technology, live host behavior and publication remain unqualified.
