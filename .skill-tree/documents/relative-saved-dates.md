# Relative dates in saved task lists

Status: local implementation and synthetic qualification; unpublished. Owner modules: Workspace saved-query models, Application read/organization engines, shared Worker query/organization adapters, saved-list editor and reviewed MCP tools. Current routed entry points: `/todo/lists` and relative tag-coverage disclosure in `/todo/sync`.

## Stored intent and date rules

Optional `plannedRelative` and `deadlineRelative` selectors are independent. Supported presets: `today`, `beforeToday`, `nextSevenDays`, `thisWeek` and `noDate`. A relative selector conflicts with an absolute from/through range on the same field. An absolute range on the other date field remains valid. Relative queries require an explicit stored UTC/IANA `timeZoneId`; a timezone without any relative selector is invalid. Fixed definitions retain original encodings and semantics. Names never supply date intent.

Each relative read captures one explicit UTC as-of instant and resolves the local civil date in the saved timezone. `today` includes the resolved civil date. `beforeToday` includes assigned dates strictly before the evaluation day. `nextSevenDays` includes the evaluation day and six following dates. `thisWeek` includes Monday through Sunday of the evaluation week. `noDate` matches only absent dates. Calendar bounds use civil-day arithmetic rather than elapsed hours around DST. Unsupported zones or unrepresentable boundary dates fail explicitly, without fallback or partial results.

Stored structural definitions remain readable when a historical timezone is unavailable. Current evaluation and new relative query authoring require an available timezone. Existing task, organization, archived-reference, stage and dependency filters remain effective. The unblocked source keeps the complete prerequisite document while applying relative/missing-date eligibility to candidates; result filtering never deletes dependency records.

## Evaluation and pagination

A relative result includes `evaluation`: `asOfUtc`, `timeZoneId`, `localDate`, and separate interpreted `planned`/`deadline` bounds. Bounds retain the preset and inclusive `from`/`through`; `noDate` has null bounds and explicit missing-date semantics. Absolute-only responses omit the extension.

Continue relative pages using the exact view ID, document revision and returned `evaluation.localDate` as `expectedEvaluationDay`. Offsets above zero require the day context. Local-day changes fail `stale_evaluation_day`; revision changes fail `stale_revision`. A caller explicitly restarts at offset0 to capture current bounds. Each read uses a single clock; pagination stability follows the unchanged local evaluation day and document revision. No caller-controlled public `asOfUtc`, predicate override, account selector or stored result IDs are accepted.

Native evaluators require an explicit UTC clock for relative reads. Public Worker reads capture server UTC once; UI reads use the explicit clock service and stored timezone. Reads change no definitions, task fields, completion/score history, archive state or provider data. Typed clients validate exact view/Workspace/revision/paging identities, context-day agreement and independently recomputed bounds before returning a result.

## Organization identity and interface

Organization command v5 binds the complete relative selectors and timezone using length-prefixed UTF8 fields. Old absent-field command hashes remain unchanged. Creation/full query replacement/rename/archive retain the existing reviewed organization preview, atomic acceptance and original receipt recovery. A later day never rewrites the saved definition or duplicates canonical tasks.

The saved-list editor exposes separate relative choices, explicit timezone, fixed fields and complete interpreted review/results. Conflicting fixed values remain visible until an explicit clear action; no switch silently discards a range. Relative-mode empty fixed fields are disabled. Missing/unsupported context refuses preparation. Day-changed results offer an explicit restart, without an all-task fallback. Account/source changes clear private selected definitions and context. The retained route-agnostic `CompanionTaskList` consumer also receives an explicit UTC clock and preserves the evaluation day across pages; component evidence does not establish a separate production route.

## Habitica tag boundary

Relative saved-list authoring and reads grant no automatic tag permission. Default planning keeps `relative_dates_require_review` and retains current upstream membership. Explicit day-bound preview/accept/receipt contracts now permit separately approved relative-group reconciliation and exact adoption. The review captures UTC as-of, stored timezone, local day, interpreted bounds and complete additions/removals. Stale day/revision/authority refuses fresh effects; original uncertain receipts remain recoverable before any replacement action. See [relative tag reviews](relative-tag-reviews.md) for current coverage, gateway guards and qualification limits.

## Verification and limits

- Sixteen independent Python datetime/ZoneInfo examples cover UTC/local midnight, DST gaps/overlaps, leap dates, Monday/Sunday and year/week boundaries.
- `relative-saved-command-v5.json` independently constructs the UTF8 command hash; C#/Worker controls retain earlier command behavior and original receipts.
- Native/shared read controls cover all presets, separate date fields, missing dates, full dependency context, conflicting filters, unavailable zones, bound overflow, revision/day cursors and no mutation.
- Local SDK/Workers/D1 controls cover real reviewed definition authoring, interpreted results, full byte/state preservation on reads, original receipts, zero task/provider effects and explicit invalid-context refusal. Existing tag-membership/reconciliation controls retain prior behavior; relative conversion fences old queued work.
- Typed/component controls cover complete review, explicit clearing, disabled controls, rollover/restart, private account cleanup, retained consumers and unavailable tag coverage with original recovery.
- `relative-saved-rendered-browser` uses current Release assets, current Razor states, readable light/obsidian schemes, phone/desktop widths, normal/enlarged text, keyboard focus/geometry/contrast and actual hydrated saved-list/Sync journeys through isolated Auth/Workspace Workers/D1 and native IndexedDB.

Evidence resides under `.skill-tree/history/validation/relative-saved-dates-2026-11-03/`. No publication, real account, provider mutation, actual device permission or notification operation follows from local qualification. Physical devices, assistive technologies, production OAuth/CORS and installed-PWA/service-worker lifecycle remain unqualified. Service workers are blocked in the changed-route harness.
