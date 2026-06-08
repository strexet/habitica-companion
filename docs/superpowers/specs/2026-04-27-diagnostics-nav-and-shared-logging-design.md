# Diagnostics Navigation And Shared Logging Design

Status note, 2026-06-08: this is a historical pre-implementation design snapshot. It does not describe the current app state. Use `FEATURES.md` section 17 for implemented Diagnostics behavior, `TECHNICAL.md` for logging/storage architecture, and `docs/UX_UI_MANIFEST.md` for current UI guidance.

## Goal

Add a diagnostics-forward operator surface to the existing Habitica web app without changing the current left-side expandable navigation model.

The resulting design must:

- promote `Diagnostics` to a first-class navigation entry;
- keep live tests easy to access from that page;
- add a curated preset API runner for safe live inspection;
- add a shared cross-feature diagnostics log visible from the Diagnostics console;
- preserve the rule that normal user-facing mutations belong on dedicated feature pages, not inside Diagnostics.

## Original context

At the time this design was written, the app had:

- a left drawer shell driven by `MainLayout` and `AppNavMenu`;
- read-only dashboard, tasks, inventory, and party views;
- a `Live Tests` page with a safe suite and a reversible gear roundtrip test;
- simple IndexedDB-backed key-value storage for snapshots and persistent credentials;
- no shared logging/journal facility across workflows.

The app is no longer being positioned as permanently read-only. Future features are expected to include guarded live mutations such as equipping gear and casting skills.

## Chosen approach

Use a diagnostics-forward shell while keeping the existing drawer interaction.

Key decisions:

- Keep the current left expandable drawer instead of introducing a desktop top menu bar.
- Rename the first-class navigation target from `Live Tests` to `Diagnostics`.
- Keep `Diagnostics` as a single operator route in this slice instead of splitting it into multiple child routes.
- Keep the diagnostics console visible on the right on wide screens and stack it below the tools on smaller screens.
- Add one shared persistent app log for all meaningful feature workflows.
- Keep future normal mutations on dedicated feature pages such as equipment or skills; Diagnostics is for verification, inspection, and operator tooling.

## Navigation design

Top-level drawer entries after this change:

- `Dashboard`
- `Tasks`
- `Inventory`
- `Party`
- `Diagnostics`
- `Settings`

Navigation rules:

- `Diagnostics` is shown whenever the user is authenticated or local cached state exists in a way that makes diagnostics useful.
- `Diagnostics` replaces `Live Tests` as the user-facing label in navigation.
- The shell copy should stop describing the application as read-only-only and instead describe it as a local-first companion with guarded live operations.

## Diagnostics route design

`Diagnostics` is a single page with five sections:

1. `Overview`
2. `Safe checks`
3. `Guarded tests`
4. `Preset API runner`
5. `Shared console`

Wide-screen layout:

- main diagnostics tools on the left;
- persistent console panel on the right.

Small-screen layout:

- overview first;
- safe checks and guarded tests next;
- preset runner below;
- console stacked after the tools.

### Overview

The overview strip should show:

- authentication state;
- last sync timestamp;
- cached snapshot freshness for user/tasks/party;
- recent warning/error count derived from the shared log;
- concise copy explaining that this page is for verification and troubleshooting.

### Safe checks

This section owns the existing read-only live test suite.

Behavior:

- runs sequentially only;
- uses small request counts;
- refreshes local snapshots after successful checks;
- writes start/result/failure entries into the shared log;
- surfaces request counts and pass/fail summary inline.

### Guarded tests

This section owns reversible and future destructive verification flows.

Initial contents:

- reversible gear roundtrip test.

Future contents may include:

- guarded mutation checks for equipment;
- guarded spell/skill verification flows;
- other reversible or destructive live validation actions.

Required rules for every guarded test:

- visible risk label;
- explicit warning copy;
- acknowledgement gate before enablement;
- sequential execution only;
- no burst request behavior;
- clear result state:
  - passed and restored;
  - passed but cleanup incomplete;
  - failed before mutation;
  - failed after mutation;
  - skipped.

## Preset API runner

The diagnostics page includes a curated preset runner instead of a free-form request box.

Initial presets:

- `/user` account snapshot preset;
- `/user` inventory/equipment preset;
- `/tasks/user`;
- `/groups/party`.

Preset runner rules:

- curated whitelist only in this slice;
- no arbitrary path input;
- GET-only in this slice;
- sequential execution only;
- each run writes to the shared diagnostics log;
- each result shows a redacted summary and a redacted response preview;
- request metadata must never expose the API token or raw auth headers.

The preset runner is intentionally positioned directly on the Diagnostics page, not hidden behind another route.

## Shared diagnostics log

Add one shared application log that every meaningful workflow can write to.

Purpose:

- create a single inspection surface for auth, sync, diagnostics, and future mutation flows;
- keep troubleshooting history available across reloads;
- avoid feature-specific ad hoc logging panels.

### Storage model

Store diagnostics logs in IndexedDB through a dedicated storage abstraction, separate from:

- credentials;
- snapshots;
- cached read models.

Retention policy:

- small capped history;
- recommended initial cap: latest `250` entries;
- newest-first ordering for display.

Required user action:

- `Clear logs` to wipe the retained diagnostics history.

### Log entry shape

Each entry should include:

- stable entry id;
- timestamp;
- feature area:
  - `auth`
  - `sync`
  - `tasks`
  - `inventory`
  - `party`
  - `diagnostics`
  - future values such as `equipment`, `skills`
- operation id/name:
  - `sign-in`
  - `manual-refresh`
  - `safe-live-tests`
  - `preset-user-account`
  - `reversible-gear-roundtrip`
  - future operations such as `equip-item`, `cast-skill`
- severity:
  - `info`
  - `success`
  - `warning`
  - `error`
- mode:
  - `local`
  - `live-read`
  - `live-mutation`
  - `reversible-test`
- short human-readable message;
- redacted structured metadata dictionary.

### Metadata rules

Safe metadata may include:

- request count;
- selected preset id;
- snapshot freshness states;
- snapshot timestamps;
- target task/item/party ids when those are not secret;
- restore/rollback state;
- dry-run/confirmed/executed state;
- error category and normalized message.

Unsafe data must never be logged:

- API token;
- raw authentication headers;
- full raw user payload by default;
- any future secret fields;
- debug exports that embed credentials.

### Logging scope

The shared log should record workflow-level events, not noisy UI internals.

Log examples that should exist:

- sign-in started;
- sign-in succeeded;
- sign-in failed with redacted normalized message;
- manual refresh succeeded with request count;
- safe live suite completed;
- reversible gear test restored original item;
- preset API runner fetched `/tasks/user`;
- future equipment action validated snapshot mismatch and stopped.

Log examples that should not exist:

- component rendered;
- input value changed;
- drawer opened/closed;
- purely local UI filter typing.

## Diagnostics console behavior

The right-side console panel reads the shared log.

Required capabilities:

- newest-first event stream;
- filters by feature area;
- filters by severity;
- filters by mode;
- select an entry to inspect details;
- clear logs action;
- optional redacted export/copy for a selected entry or selected result summary.

The console is a viewer for the shared log, not a separate data source.

## Future mutation boundary

Future normal user-facing mutations do not belong on Diagnostics.

Examples:

- equip item;
- cast skill;
- use consumable;
- other normal user operations.

These belong on dedicated feature pages or workflows, but they must emit logs into the shared diagnostics log so Diagnostics remains the inspection hub.

## Data flow

Planned flow for diagnostics interactions:

1. User opens `Diagnostics`.
2. Page reads the current session state plus retained diagnostics log history.
3. User runs a safe suite, guarded test, or preset request.
4. Application workflow executes sequentially.
5. Workflow writes start/result/failure entries to the diagnostics log store.
6. Any updated snapshots are persisted through the existing stores.
7. Diagnostics UI refreshes results and console state from the shared log and updated snapshot/session state.

Planned flow for future normal mutations:

1. User runs action from a dedicated feature page.
2. Feature workflow validates current snapshot.
3. Workflow executes guarded sequential API steps.
4. Workflow writes validation, execution, restore/rollback, and final result entries to the shared diagnostics log.
5. Diagnostics page later displays those entries without owning the action itself.

## Error handling

Diagnostics behavior must:

- keep using normalized, redacted error messages;
- preserve existing cached snapshots when live operations fail, unless a successful update replaced them;
- log failed operations with safe metadata and normalized messages;
- distinguish between:
  - failure before any live mutation;
  - failure after mutation started;
  - failure during restore/rollback;
  - skipped due to validation or missing prerequisites.

## Testing strategy

Required tests for this design:

- navigation tests for `Diagnostics` entry visibility and `Live Tests` replacement;
- page tests for diagnostics section rendering;
- page tests for preset runner controls;
- page tests for console filters and rendered log entries;
- storage tests for diagnostics log retention and clearing;
- workflow tests for shared log writes during safe checks;
- workflow tests for shared log writes during guarded test outcomes;
- redaction tests ensuring secrets are not persisted to diagnostics log metadata;
- session/controller tests proving diagnostics state refreshes after runs.

Live diagnostics functionality must remain opt-in and separate from default CI behavior.

## Documentation impact

Implementation based on this design will require updates to:

- `FEATURES.md`
  - navigation feature details
  - diagnostics feature section
  - live tests feature status/behavior
  - shared logging behavior
- `README.md`
  - diagnostics route and purpose
  - preset API runner and console notes
- `HABITICA_API.md`
  - only if preset runner behavior or endpoint usage notes need clarification

`TECHNICAL.md` should only change if implementation alters architecture materially beyond adding a new store/workflow within the existing stack.

## Out of scope for this design slice

This design does not include:

- free-form API request execution;
- arbitrary HTTP method execution from Diagnostics;
- moving normal mutation workflows into Diagnostics;
- new gameplay mutation pages such as full equipment management or skills UI;
- backend services;
- non-Habitica external diagnostics integrations.
