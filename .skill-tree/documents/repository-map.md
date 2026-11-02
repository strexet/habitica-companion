# Repository Map

## Application and services

| Path | Responsibility |
| --- | --- |
| `src/Habitica.WebApp/` | Blazor WebAssembly PWA, Razor pages, session controller, browser interop and CSS |
| `src/Habitica.Application/` | Use-case orchestration, freshness, refresh, diagnostics and portability |
| `src/Habitica.Domain/` | Habitica snapshots and deterministic domain types |
| `src/Habitica.Rules/` | Equipment, spell, pet and other calculations |
| `src/Habitica.Api/` | Habitica API v3 client and error normalization |
| `src/Habitica.Storage/` | Key-value storage abstractions, snapshot and preference stores |
| `src/Habitica.Shared/` | Shared assembly |
| `functions/api/` | Pages Functions for encrypted personal sync and shared party state |
| `migrations/` | Shared-party D1 migrations |
| `workers/chatgpt-auth/` | Optional integration Auth service, OAuth KV/integration D1 ownership and isolated fixtures |
| `workers/chatgpt-mcp/` | OAuth-protected MCP transport, reviewed tools and private Auth/Workspace bindings |
| `workers/companion-workspace/` | Canonical cloud task/organization/Notes commands, D1 journals and private lifecycle services |
| `workers/scripts/` | Bounded release-input build identity generation and consistency checks |
| `workers/shared/` | Canonical per-environment integration issuer/resource configuration |
| `deploy/` | Public frontend overrides, private deployment/package preparation and callback gateway source |
| `plugin/` | Six packaged workflow skills, plugin descriptor and placeholder MCP configuration |
| `tests/Habitica.*.Tests/` | xUnit and bUnit tests |
| `tests/Functions/` | Node test-runner tests for Functions and browser bridges |
| `.agents/skills/` | Standalone installed web-flavor Skill Tree workflows |
| `.skill-tree/documents/` | Canonical repository documents, planned task definitions and queue state |
| `.skill-tree/installation/` | Generated profile, local update manager and self-contained source bundle |
| `.skill-tree/history/` | Bounded audit/run records and compact terminal outcomes |

`src/Habitica.WebApp/Program.cs` registers application, API and storage services. `TasksPage.razor` serves `/tasks` with `TodoTaskPanel`; `TodoTasksPage.razor` serves `/todo/tasks` with `TodoHierarchyPanel`; the other `/todo/*` pages compose planning, projects, history and Sync. `NotesPage.razor` serves `/notes`; `SettingsPage.razor` serves `/settings`. Separate Auth, MCP and Workspace Workers implement optional account/profile reads, canonical task commands, authenticated party planning and reviewed spell/Cron contracts. `plugin/` contains plugin packaging and task/review skills. Checked-in remote feature defaults remain disabled. `deploy/production.frontend.json` selects enabled Auth/Workspace endpoints; [deployment documentation](deploy-cloudflare-pages.md) separates prepared production flags, recorded publication and outstanding host/device qualification.

`README.md` and `AGENTS.md` provide root discovery. `.skill-tree/skill-tree.json` indexes document ownership, hashes, policies and validation commands. `future.md` owns all active plans; `companion-first-mcp-research.md` provides reference context only.

The `workers/companion-workspace` package owns the optional canonical cloud command API and isolated D1 history. `workers/shared/workspace-domain.ts` validates the neutral canonical schema; `workspace-commands.ts` defines portable request identity; `workspace-http.ts` contains bounded request helpers. The private Auth identity broker connects the trust boundaries without exposing stored Habitica credentials. `WorkspaceSettingsPanel` and `WorkspaceDataController` own separate workspace privacy and backup controls.

The shared `projection.ts` contract contains sanitized remote task fields, fixed projection aliases and partial-update validation. The Auth projection gateway owns credential-bound preconditions and durable upstream attempt receipts. The Workspace projection adapter owns reviewed mapping and effect planning from accepted canonical task snapshots; hosted execution remains a separate orchestration layer.
