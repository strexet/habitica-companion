# Future Work

This file tracks items from `habitica_companion_pending_features_plan.md` that remain unimplemented.

## Party Quest Improvements

- Replace party-sync credential-header membership verification with a tokenless/signed membership proof so Habitica API tokens are never sent to Cloudflare party-sync endpoints.
- Add explicit owner readiness toggle in the shared queue UI.
- Add party leader controls for manual pinning, force-selecting, resolving conflicts, and locking queue changes during selection.
- Add queue states and actions for `Selected`, `InviteSent`, `Skipped`, and `Expired` beyond the implemented queued/active/completed/removed storage path.
- Add automatic queue lifecycle reconciliation from Habitica active quest changes: queued -> active -> completed.
- Add direct Habitica quest invite/start action for the selected quest owner after confirming the exact API flow.
- Add limited vote budgets as an optional advanced voting mode.
- Add queue expiration and stale-owner cleanup rules.
- Add historical quest analytics beyond the recent-completion list and queue penalty.

## CRON Button and Buff Warning

- Dashboard `Start New Day` button with confirmation.
- Habitica Cron API wrapper and post-Cron targeted refresh.
- Buff warning before casting not-Croned stat buffs.
- Per-user, per-Habitica-day local warning suppression.

## Tasks Page Enhancements

- Task scoring/completion actions.
- Habit multi-score controls.
- Expandable task details with week/month/year statistics.
- Task activity histograms and activity charts.

## Dashboard Improvements

- Pending damage estimate and near-death warning.
- Buy Health Potion action.
- Dashboard navigation cards for major app sections.
- Dashboard and context-sensitive `Open Habitica` links.

## Login and Refresh Improvements

- Login redirect guard that skips the sign-in page for authenticated stored credentials without flashing the login UI.
- ~~Staged refresh coordinator with domain-specific refresh keys.~~ Implemented: `RefreshCoordinator` in `Habitica.Application.Sync` with domain-level dedup, priority scheduling, and per-domain callbacks.
- ~~Narrow Habitica API endpoint refreshes by visible page/domain.~~ Implemented: `RefreshForPageAsync` dispatches page-route→domain mapping with visible/background priorities.
- Stale-while-revalidate UI state and field/card-level refresh indicators.
- ~~Dependency-based invalidation after mutations.~~ Implemented: `DomainInvalidationMap` maps mutations to affected domains; cloud sync is fire-and-forget after mutations.
- ~~Request scheduling, deduplication, and current-page refresh priority.~~ Implemented: `RefreshCoordinator` deduplicates in-flight domain refreshes and schedules by `RefreshPriority`.

## Cloud Sync Improvements

- ~~Split single-blob cloud sync into per-section encrypted KV records to avoid 2MB payload limit.~~ Implemented: per-section upload/download via `CloudSyncSectionMapping`, legacy single-blob backward compat with auto-migration.
- Per-section sync status reporting in Settings UI (show which sections succeeded/failed/skipped).
- Configurable section-level sync exclusions (e.g., skip diagnostics sync to save space).
- Cloud sync conflict resolution UI when remote and local sections diverge.
