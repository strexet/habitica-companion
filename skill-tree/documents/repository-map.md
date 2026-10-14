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

`src/Habitica.WebApp/Program.cs` registers application, API and storage services. `TasksPage.razor` serves `/tasks`; `SettingsPage.razor` serves `/settings`. Separate Auth/MCP scaffolds exist under `workers/`; protected operations are denied. Plugin packaging and MCP tools remain absent.

`README.md` and `AGENTS.md` provide root discovery. `skill-tree/skill-tree.json` indexes document ownership, hashes, policies and validation commands. `future.md` owns all active plans; `companion-first-mcp-research.md` provides reference context only.
