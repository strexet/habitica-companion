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

No rendered route changes in the coverage implementation. UI consumers must display unknown coverage and distinguish canonical observations from verified provider outcomes. Streak calculations and reviewed MCP/UI presentation remain separate task scope.
