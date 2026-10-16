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
| `workers/chatgpt-auth/` | Optional integration Auth scaffold, OAuth KV/integration D1 ownership and isolated fixtures |
| `workers/chatgpt-mcp/` | Optional MCP scaffold and narrow Auth Service Binding |
| `workers/shared/` | Canonical per-environment integration issuer/resource configuration |
| `deploy/` | Deployment support |
| `tests/Habitica.*.Tests/` | xUnit and bUnit tests |
| `tests/Functions/` | Node test-runner tests for Functions and browser bridges |
| `.agents/skills/` | Standalone installed generic Skill Tree workflows |
| `skill-tree/documents/` | Canonical repository documents, planned task definitions and queue state |
| `skill-tree/history/` | Bounded audit/run records and compact terminal outcomes |

`src/Habitica.WebApp/Program.cs` registers application, API and storage services. `TasksPage.razor` serves `/tasks`; `SettingsPage.razor` serves `/settings`. Separate Auth, MCP and Workspace Workers implement optional account/profile reads, canonical task commands, authenticated party planning and reviewed spell/Cron contracts. `plugin/` contains plugin packaging and task/review skills. Shipped remote feature flags remain disabled; hosted enrollment and real-account qualification remain outstanding.

`README.md` and `AGENTS.md` provide root discovery. `skill-tree/skill-tree.json` indexes document ownership, hashes, policies and validation commands. `future.md` owns all active plans; `companion-first-mcp-research.md` provides reference context only.

The `workers/companion-workspace` package owns the optional canonical cloud command API and isolated D1 history. `workers/shared/workspace-domain.ts` validates the neutral canonical schema; `workspace-commands.ts` defines portable request identity; `workspace-http.ts` contains bounded request helpers. The private Auth identity broker connects the trust boundaries without exposing stored Habitica credentials. `WorkspaceSettingsPanel` and `WorkspaceDataController` own separate workspace privacy and backup controls.

The shared `projection.ts` contract contains sanitized remote task fields, fixed projection aliases and partial-update validation. The Auth projection gateway owns credential-bound preconditions and durable upstream attempt receipts. The Workspace projection adapter owns reviewed mapping and effect planning from accepted canonical task snapshots; hosted execution remains a separate orchestration layer.
