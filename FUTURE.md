# Future Work

Last validated: 2026-05-21.

This file tracks unimplemented work after checking the current repository against:

- `1_habitica_companion_pending_features_plan.md`
- `2_cloud_sync_split_key_refresh_instructions.md`
- existing `FUTURE.md`
- current source files under `src/`, `functions/`, `migrations/`, and `tests/`

Implemented items are removed instead of kept as strikethrough. Current implemented behavior belongs in `FEATURES.md`, with foundational architecture notes in `TECHNICAL.md` and Habitica endpoint rules in `HABITICA_API.md`.

## From `1_habitica_companion_pending_features_plan.md`

### Party Page Quest Improvements

- Add tokenized manager-invite party-sync proofs if local claims become too easy to abuse in real parties. The current access path is isolated behind `readAccessProof()` / `resolvePartySyncAccess()` so a future proof version can replace `local-claim-v1`.
- Fill remaining active quest card metadata and actions: quest owner or starter, started date, details view, participants view, and reward/details affordances when the data is available.
- Add an owner readiness mutation flow. The database field and read-only display exist, but the shared queue UI does not expose a toggle action yet.
- Add party leader queue controls for manual pinning, force-selecting, conflict resolution, and locking queue changes during selection.
- Add user-facing actions and handling for `Selected`, `InviteSent`, `Skipped`, and `Expired` queue states beyond the current queued, active, completed, and removed path.
- Add direct Habitica quest invite/start action for the selected quest owner after the exact Habitica API flow is confirmed.
- Add queue expiration and stale-owner cleanup rules.
- Add optional limited vote budgets only if requested as an advanced voting mode.
- Add historical quest analytics beyond the recent-completion list and soft queue penalty.

### Tasks Page Enhancements

- Add week/month/year period selector for task statistics.
- Add task-history histogram and month activity chart on the Tasks page.
- Add a smaller activity chart inside expanded task details.

### Dashboard Improvements

- Add a pending damage estimate box.
- Explain which damage sources are included and excluded from the estimate.
- Add warning state when estimated damage may kill or nearly kill the user.
- Add a manual Buy Health Potion action near damage information. Do not make potion purchase automatic.
- Add dashboard section cards with direct navigation.
- Add an Open Habitica button and context-sensitive Habitica links where stable URLs are known.

### Login and Refresh Improvements

- Add a redirect guard that skips the sign-in page for authenticated stored credentials without flashing the login UI.
- Return to the dashboard after the minimal successful user fetch; defer non-critical domain refreshes behind usable cached/current data.
- Add stale-while-revalidate UI behavior so cached values stay visible while stale domains refresh in the background.
- Add field/card-level refresh indicators for manual refresh and Cloudflare sync progress.
- Add subtle changed-value animation after background updates.
- Add loading skeletons where delayed content has a stable final structure.

## From `2_cloud_sync_split_key_refresh_instructions.md`

### Cloud Sync Improvements

- Expand cloud sync metadata from uploaded/failed section lists into per-section status records with section key, updated time, payload size, and status.
- Surface per-section sync status in Settings so users can see which sections succeeded, failed, or were skipped.
- Add configurable section-level sync exclusions, for example skipping diagnostics sync to save storage.
- Add cloud sync conflict resolution UI when remote and local sections diverge.
- Add diagnostics for sync section key, payload size, upload/download status, partial skipped sections, refresh domain, refresh reason, refresh duration, deduplication hit/miss, and mutation invalidation result.

### Refresh Optimization Follow-Up

- Keep visible page data interactive while background domains refresh; avoid global busy states for background-only work.
- Surface refresh status next to the affected card or field, not only as page-level busy state.
- Log request deduplication hit/miss and refresh duration per domain.
- Make mutation invalidation results visible in diagnostics.

## Other Future Features

### Gear and Equipment Planning

- Add inventory before/after stat deltas for equip actions.
- Add equipment optimization for goals such as Perception, Strength, balanced stats, boss damage, and survival.
- Allow saving optimizer recommendations as named gear sets or presets.

### Skill Macros

- Add a macro collection for predefined skill/equipment sequences.
- Add dry-run previews with planned equipment changes, target selection, mana cost, expected requests, warnings, and stop conditions.
- Keep macro execution sequential and stop on validation failures or unexpected state changes.

### Bulk Sell Planner

- Add a bulk sell helper that identifies items likely safe to sell.
- Include explanation for why each item is considered safe or unsafe.
- Require preview and explicit confirmation before any sell action.

### Action Result Estimates

- Add estimates for selected actions, including expected damage, gold, skill effects, boss progress, and player damage risk.
- Clearly distinguish exact API-returned values from local estimates and assumption-based formulas.

### UX Cleanup

- Split current party quest state and queue planning into clearer modes, such as tabs or a segmented switch.
- Add task filters for type, status, due window, and value polarity.
- Add confirmation to Settings destructive actions such as clearing local browser data.
- Reduce repeated hero/help copy for returning authenticated users.
- Add sticky first-column or label context for mobile stat tables.
- Consider compact spell cards after the current spell card layout has been tested with real use.
