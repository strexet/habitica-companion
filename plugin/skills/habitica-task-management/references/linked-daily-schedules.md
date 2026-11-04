# Exact linked Daily schedule changes

Use the reviewed MCP contract for an explicit schedule change on an exact already-linked active Daily in the connected cloud Workspace. Native `updateTask` recurrence edits remain unlinked-only. Native pause/skip never pauses Habitica penalties.

1. Resolve the exact current Companion task, Workspace revision and mapping. Refuse uncertainty or pending game effects; titles alone never establish identity.
2. Supply a stable request ID to `prepare_task_command`, operation `editLinkedDailySchedule`, exact `taskId`, expected revision and only `patch.recurrence`. Do not supply linked context; the server reads complete provider/account facts.
3. Review the full returned `linkedSchedule`: exact Habitica ID, original and expected normalized schedule, account offset/dayStart and observation time. Review current/next native calendar dates without claiming provider due state. The preview expires after90seconds.
4. Only lossless daily/weekly/monthly rules with matching fixed zones and midnight day start are supported. Monthly updates require one day matching the start date. Retained yearly, month-week, multi-date/month-start mismatch, mixed or otherwise unsupported forms stay read-only; never discard retained fields to force acceptance.
5. Accept the reviewed server command through `edit_linked_daily_schedule`. Saving updates native recurrence and queues one metadata effect; no score, Cron, resting setting or backfill follows. Existing consent and executor authorization remain independent.
6. Reuse the exact original request after lost preparation. Recover the accepted command through `get_task_command` after lost acceptance or dispatch. The returned `scheduleEpoch` distinguishes native acceptance from the optional original normalized provider response and original attempt ID. Different normalization needs explicit reconciliation. Current field equality never proves an uncertain original write; never send a replacement attempt.

Fresh provider GETs reduce stale writes but cannot eliminate the final external race because Habitica has no conditional task PUT. Synthetic/local qualification does not grant publication, live execution or device permission.
