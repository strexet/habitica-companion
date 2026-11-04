# Task activity coverage

Status: implemented for accepted canonical task commands in C# and Workers. Scope is retained Companion activity. Provider history, Habitica streaks and reward outcomes require separate evidence.

## Persisted witnesses

Optional `WorkspaceDocument.activityCoverage` retains command ID, exact payload hash, task ID, resulting revision, observed previous task revision, accepted UTC instant and the accepted kind/state/archive/recurrence snapshot. Recurrence includes the historical timezone. Every accepted ordinary task command adds a witness atomically with the original command, accepted event and task snapshot. Duplicate receipt reads add nothing. Existing projection metadata revisions do not interrupt the task revision chain.

Full-journal validation binds each witness to the original command hash, accepted event, revision, timestamp and task snapshot. Creation and proven subsequent schedule/lifecycle transitions must match the command intent. Witnessed completions, Habit scores and terminal Daily occurrences require the actual retained records; corrections retain the original terminal snapshot. Coverage creates no completion, skip, restoration, score or provider request.

## Known intervals and gaps

`WorkspaceActivityCoverage.Read` and Worker `readActivityCoverage` validate the full journal before returning half-open UTC intervals `[fromUtc, throughUtc)`. Adjacent witnesses establish an interval only when the next witness observed the exact previous witnessed task revision. The historical schedule/state belongs to the left witness. Pause, resume, archive, unarchive, completion and restoration therefore retain effective-dated states. Repeated timestamps represent ordered transitions without inventing elapsed time.

Legacy/imported records without witnesses remain unknown. The first newly accepted command anchors a future segment, without backfilling the unknown prefix from creation timestamps, current recurrence or command/page counts. Removing a middle witness or retaining an unobserved task revision leaves a gap. A missing command or activity referenced by a retained witness invalidates the archive. Removing the unsupported assertion can reduce evidence to unknown; validation never reconstructs missing events.

Coverage ends at an actual accepted witness. Reads cannot advance the persisted endpoint using the query clock. Unknown prefix, gaps and tail use `unproven_history`; accepted timestamps moving backwards make the task's chronological intervals `clock_order_unknown`. An unavailable or invalid full journal produces an error, rather than an empty complete history.

Special writers without ordinary task-command witnesses, including checklist placement, source retirement and linked native recovery, leave an unproven transition. The retained receipts remain independent evidence. A subsequent ordinary command can anchor a new known suffix. No historical schedule is copied from current task settings across that gap.

## Ownership and portability

The optional field preserves schema version 1 and legacy command identities. Document serialization retains shape-validated records; only a validated full journal establishes causal coverage. Export/import round-trips the same assertions without extending coverage. Structural causal validation does not authenticate a user-edited archive against an external server signature. Storage size bounds and existing authority/profile controls remain applicable. No migration, consent, enrollment or remote publication follows from the coverage field.

No rendered route changes in the coverage implementation. UI consumers must display unknown coverage and distinguish canonical observations from verified provider outcomes. Bounded day-series calculations consume the retained full-journal coverage contract.

## Bounded Daily and Habit day series

WorkspaceActivityStreaks and Worker readActivityStatistics evaluate an exact task, explicit UTC as-of, inclusive local start date and displayed IANA timezone over at most366 civil days. Full-journal validation precedes coverage reads. Context hashes bind workspace revision, task identity, range, exact as-of and timezone. Continuations require the original expected revision and context; changing evaluation inputs refuses the old cursor. Reads create no task command, occurrence, score, backfill or notification permission.

Positive Habit events count once per local calendar day; negative events retain separate event/day counts and never erase a positive day. The accepted Habit settings effective at the requested as-of select activity frequency for a negative-only goal. Historical negative-only intervals remain neutral when a later positive goal is enabled. Missing accepted goal snapshots use an explicit currentTask fallback label without claiming historical settings. Daily terminal dates count once under the original accepted schedule timezone. A different or unavailable historical terminal timezone remains unknown rather than reinterpreting the date in a new zone. Historical recurrence and lifecycle witnesses qualify eligible, neutral and missing days. A whole proven paused/archived or unscheduled day is neutral; mixed eligibility within a day remains unknown. Explicit skipped occurrences remain neutral. Corrections after the selected as-of cannot rewrite an earlier terminal-state read.

A closed eligible day breaks a run only inside proven complete coverage. An unfinished current day retains the previous run. Unknown gaps separate observed segments and produce lower-bound or unavailable current results. A reversed accepted clock makes the current result unavailable. Longest observed values describe the selected window; unknown or open unproven context retains a lower-bound label. Lifetime best is always unavailable. Retained event counts never imply complete provider history or rewards.

Civil-day boundaries locate the first UTC instant belonging to the requested local date, including DST folds/gaps and skipped local dates. The [Microsoft ambiguous-time guidance](https://learn.microsoft.com/en-us/dotnet/standard/datetime/resolve-ambiguous-times) explains multiple possible UTC offsets; the [Intl formatToParts contract](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Intl/DateTimeFormat/formatToParts) supplies explicit Gregorian date parts for the Worker evaluator. The first-instant boundary policy is a project decision, qualified through shared DST examples.

get_task_activity_statistics exposes the bounded read through tasks:read with no account selector. A guarded D1 batch reads the exact active workspace, commands and accepted events before full-journal evaluation. Ordinary MCP grant-epoch observation can update authorization metadata independently; statistics evaluation never creates commands or game effects. The activity-review package distinguishes canonical witnesses, retained events, lower bounds and original effect receipts.

The /todo/statistics day-series section restricts exact task choices using current type/search/project filters. The section exposes local start date, IANA timezone and explicit historical UTC as-of, with twenty-row task/day paging and a separate refresh read. Current metrics never substitute zero for unavailable history. Account/source changes clear private selection and stale paging. Native controls retain keyboard focus and theme tokens; long references wrap, and native selects clip closed option text without page overflow. Local component, portable-journal, SDK/D1, Razor-state and hydrated Release fixtures remain distinct from hosted login, installed PWA, physical-device and assistive-technology qualification.
