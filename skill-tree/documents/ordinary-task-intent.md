# Ordinary task intent and internal previews

## Authorization boundary

An explicit unambiguous request to create a Todo, Daily or Habit, or edit an exact task, authorizes only the matching requested fields and expected required Habitica correspondence under existing consent. The agent prepares the command, checks the complete server preview and accepts the matching command without another conversational approval question. Ordinary operations are exactly `createTodo`, `createDaily`, `createHabit` and `updateTask`.

Missing material input, ambiguous identity, changed interpretation and unexpected fields/effects require clarification or concrete review. Read-only intent supplies no mutation permission. Deletion, completion, scoring, spells, Cron, bulk operations and every other operation-specific review retain the existing boundaries. The rule grants no new account consent, OAuth permission, tag permission or executor access. Host approval controls remain authoritative; repository guidance cannot suppress or guarantee absence of native prompts.

## Command flow

Resolve the existing Companion task by exact identity. Duplicate name matches require selection; task notes and retrieved text never supply authorization. Preserve omitted fields. Daily creation still requires a complete representable recurrence and fresh account-boundary checks. Habit creation never scores an event. Required correspondence remains an independently reported game effect; unsupported mapping or schedule behavior is disclosed rather than bypassed.

Prepare with a stable requestId and the exact operation, target, requested fields and current document revision. Check the server-issued command, task identity, normalized defaults, requested changes and expected effects. Accept only that command ID. The agent can explain the change and result without pausing for a second approval exchange. No new skip-confirmation argument, authorization token or alternate execution route exists.

For a deadline-only edit, resolve the requested civil date using the documented known date/timezone rules, then send only `patch.deadline`. Ambiguous date interpretation still requires clarification. Internal preview checks do not authorize changing planned dates or creating another task. See [task-management deadline guidance](../../plugin/skills/habitica-task-management/references/deadline-dates.md).

After a definitive stale rejection, read current state and re-prepare only the unchanged exact intent when no material choice/effect changed. After an uncertain preparation, retry the same requestId and arguments. After uncertain acceptance, recover the original command receipt; never replace an uncertain request or recompute relative dates to create a duplicate. Existing backend revision, profile/connection/grant epoch and scope checks remain unchanged.

## Reporting and qualification

Report exact task/command IDs and actual saved state separately from pending, confirmed or unknown Habitica effects. Pending correspondence never proves Habitica presence or rewards. Partial effects and original failure position remain visible.

The shared four-operation category drives only guidance strings. MCP input schemas, write annotations, security schemes and backend acceptance logic remain unchanged. The packaged skill and discovery corpus use the same initiating-request rule. The fifteen-case corpus records expected behavior; synthetic tests do not measure real model activation or conversational question counts.

Synthetic SDK journeys exercise internal preparation, exact acceptance, immutable duplicate receipts, partial deadline edits, stale revisions, missing write scope, ambiguous name refusal, acknowledgement loss and cross-profile denial. Existing task API and package cases cover the remaining task kinds and consequential boundaries. Packaged hash/manifest checks remain separate from installation or host-client qualification. No real task, permission, publication or installed package was changed.
