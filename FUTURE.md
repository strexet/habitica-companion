# Future Work

This file tracks items from `habitica_companion_pending_features_plan.md` that were not implemented in the Party Quest Improvements pass.

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

## Inventory Final Improvement

- Full-width vertical preset card layout.
- Highest-stat highlighting for normal equipment, best-in-category cards, battle gear cards, and saved preset item cards.

## Tasks Page Enhancements

- Foldable To-Dos, Dailies, and Habits categories with per-user local preferences.
- Per-category completed visibility controls.
- Task scoring/completion actions.
- Habit multi-score controls.
- Continuous task-value background tinting.
- Expandable task details with week/month/year statistics.
- Task activity histograms and activity charts.

## Dashboard Improvements

- Pending damage estimate and near-death warning.
- Buy Health Potion action.
- Dashboard navigation cards for major app sections.
- Dashboard and context-sensitive `Open Habitica` links.

## Login and Refresh Improvements

- Login redirect guard that skips the sign-in page for authenticated stored credentials without flashing the login UI.
- Staged refresh coordinator with domain-specific refresh keys.
- Narrow Habitica API endpoint refreshes by visible page/domain.
- Stale-while-revalidate UI state and field/card-level refresh indicators.
- Dependency-based invalidation after mutations.
- Request scheduling, deduplication, and current-page refresh priority.
