# Reviewed relative-date tag membership

Status: local implementation and synthetic qualification; unpublished. Owners: saved-query and tag membership policies, group-tag preview/accept/receipt APIs, executor, Auth tag gateway, typed client and Sync route.

## Explicit day context

Relative saved lists keep automatic tag planning fenced. An explicit `prepare_group_tag_command` for a relative saved list requires `relativeDayReview:true`, exact saved-list identity, separate tag consent and existing tag-write permission. No public clock, timezone override or member-ID override is accepted. The server captures one UTC instant, stored timezone, local day, interpreted planned/deadline bounds and the complete AND-filtered selection. Native and shared policies use the existing relative query engine, including complete unblocked dependency context and missing-date semantics.

The immutable preview and queued work retain the exact evaluation context. Every task addition, retained membership and managed removal remains reviewable. Pagination never changes the selection. Empty reconcile selections require explicit interface acknowledgement. Adoption keeps exact native identity and authorizes no membership change. New task commands, definition edits, wakes and midnight never grant relative membership permission.

## Acceptance and dispatch

Preview acceptance rechecks source revision, ledger, owner/connection, consent/grant epochs, expiry and local evaluation day. Expiry is bounded by the next local midnight as well as the ordinary preview duration. UTC expiry is computed from the actual local-day boundary, including23/25-hour DST days and the date line; the same expiry protects the atomic D1 acceptance guard.

Fresh work revalidates the exact source revision, interpreted query context, current local day, complete member hash and authority before dispatch. The Auth gateway binds optional reviewed day/timezone into the original attempt hash while retaining legacy absent-field hashes. The gateway checks the day immediately before a fresh upstream request, after awaited authorization and pacing. A known day refusal before dispatch retains a refused original attempt; uncertain dispatch retains the original unknown fence.

Original receipt lookup precedes stale-day/source rejection. A later day cannot justify resending an uncertain mutation. The original attempt timestamp, IDs, reviewed context and normalized outcome remain available. Accepted and terminal-unaccepted review receipts and original work reads retain the evaluation metadata. Managed removals preserve unrelated tags; no task creation, scoring, rewards or Cron follows from tag metadata.

## Current evidence and interface

Scoped coverage reads use accepted work from the exact connection/access epoch and current tag consent. A matching source revision and currently valid evaluation day permit current pending/confirmed membership evidence. Stale or unavailable context remains explicit review-required evidence without creating new work. Canonical-only consumers retain a conservative fence unless an explicit evaluation context is supplied; stored ledger membership alone never establishes current relative-day approval.

Sync exposes a separate one-local-day review, exact timezone/day/as-of/bounds, complete task-page acknowledgement, explicit empty selection and stale-context refusal. Recorded current context remains visible in scoped coverage. New reviews remain explicit after reload. Original receipt recovery stays available after uncertainty or day change; private account/Workspace changes clear pending interface state.

Verification artifacts reside under `.skill-tree/history/validation/relative-tag-projection-2026-11-03/`. Focused native/component, actual SDK/D1/Auth gateway, synthetic provider and current Release browser controls cover context, day rollover, DST/date-line expiry, original lost replies and exact metadata effects. Physical devices, assistive technologies, hosted OAuth/CORS and installed-PWA/service-worker lifecycle remain separately unqualified. No real account/provider operation, publication or notification qualification is included.
