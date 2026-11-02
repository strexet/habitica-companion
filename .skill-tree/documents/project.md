# Habitica Companion Client

## Purpose and users

Habitica Companion is a third-party client for Habitica planning, local analysis, explainable recommendations and guarded actions. Primary users are experienced Habitica users, party coordinators and users managing tasks, projects, personal Notes and optimization workflows. Developers and users inspecting cached account/task data are additional audiences.

The official Habitica client remains the authority for Habitica social and game systems. Companion provides canonical task planning and personal knowledge workflows alongside Habitica snapshots and assisted actions. [features.md](features.md) owns detailed implemented behavior and limitations; [future.md](future.md) owns proposed work and unfinished acceptance.

## Current scope

- Blazor WebAssembly PWA with responsive desktop, tablet and phone routes, explicit sign-in and offline cached views.
- Habitica account, task, inventory, equipment, party and quest snapshots with freshness-aware analysis.
- Gear recommendations/presets, spell target/effect estimates, pet/mount planning and previewed inventory actions.
- Canonical Todo, Daily and Habit workspaces with projects, relationships, recurrence, saved lists, retained activity and original command receipts.
- Personal Markdown Notes with exact identities, project associations, retained versions and separate export/erasure controls.
- Optional consented Auth/MCP/Workspace services for guarded agent reads/commands, canonical cloud storage and owner-reviewed browser-to-cloud adoption.
- Foreground reminders, voluntary reviews and independently gated Push subscription/scheduling/dispatch.
- Diagnostics and local/encrypted-cloud portability with explicit privacy boundaries.

Source availability, configured feature flags, current deployment, installed packages and host/device qualification remain independent facts. [technical.md](technical.md), [deployment policy](deploy-cloudflare-pages.md) and source-bound history define the corresponding boundaries.

## Product principles

### Explain recommendations

Recommendations expose input snapshots, selected factors, assumptions, uncertainty and calculation versions. Equipment comparisons distinguish stat changes from gameplay estimates. Spell and quest estimates distinguish local approximations from provider-returned values. Missing or stale inputs require visible limitations rather than invented precision.

### Guard mutations

Habitica changes require current preconditions and explicit intent. Multi-step, destructive and batch operations require reviewed previews, applicable confirmation, sequential execution, partial-success reporting and durable logs. Unknown outcomes require original receipt recovery; an uncertain result never authorizes blind replay. The application must not secretly execute macros, sell inventory or change gameplay.

### Prefer local reads

Cached Habitica snapshots support responsive analysis with fewer network requests. Canonical local journals and Notes retain explicit ownership; cloud-owned or frozen browser copies cannot become independent writable sources. Offline screens establish neither fresh Habitica state nor remote execution authority.

### Keep credential choices explicit

Browser sign-in defaults to session-only credentials. Persistent browser storage requires opt-in. Separate integration enrollment sends credentials to the configured Auth Worker only with explicit consent and stores an encrypted vault envelope. MCP receives sanitized models and receipts; encrypted app-data sync and debug exports exclude raw credentials. [The credential contract](technical.md#integration-credential-vault) owns storage, recovery, rotation and deletion.

### Respect provider and account boundaries

Habitica API v3 requests require the configured creator header, rate-limit handling and conservative retries under [habitica-api.md](habitica-api.md). The application distinguishes canonical data saves, retained mapping evidence and confirmed Habitica effects. Task/group correspondence, scopes, consent and original receipt fences remain mandatory.

## User flows and UX

The browser user signs in, reviews cached data and freshness, selects an analysis/planning feature and inspects the resulting explanation. A supported mutation presents applicable review and confirmation before execution, then updates local state or reports partial/unknown outcomes. Optional cloud enrollment, Workspace/Notes consent, executor activation, package installation and notification permission require distinct user choices.

The interface uses clear dashboards, readable tables, compact explanations, visible warnings, source/sync status and reviewable configuration. [ux-ui-manifest.md](ux-ui-manifest.md) owns current routes, interaction patterns, theme/reflow and accessibility requirements. Companion exposes no hidden destructive behavior.

## Constraints and non-goals

Habitica social replacement, chat replacement, real-time multiplayer and complex game-like rendering remain outside current product scope. Credential storage outside the separately consented Auth vault and invisible background automation are unsupported. Native shells require a separate architecture decision; the current client remains a browser PWA.

[repository-map.md](repository-map.md) owns physical modules; [rules.md](rules.md) owns agent collaboration and authorization. Product-scope changes require matching technical and feature documentation without duplicating the active task queue.
