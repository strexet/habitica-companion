# Habitica Companion Client

Third-party companion client for Habitica power users. The project is focused on local data analysis, explainable recommendations, and safe assisted actions rather than replacing the official Habitica app.

Current status: initial web-app MVP implemented. The repository now contains a working Blazor WebAssembly PWA shell with Habitica credential sign-in, manual sync, cached account and task snapshots, a local dashboard, read-only task browsing, and local-data controls.

## Current MVP features

- credential sign-in with session-only default and persistent local opt-in
- manual sync against Habitica API v3
- cached account dashboard with class, stat, companion, and inventory summary cards
- local-first task snapshot storage through IndexedDB with a Dexie-backed JS module
- local-first account snapshot storage for offline dashboard access
- responsive app shell with sign-in, dashboard, tasks, and settings routes
- read-only task workspace with search, completed toggle, and freshness indicators
- sign-out for the current tab session and clear-local-data controls

## Planned feature areas

- richer inventory, equipment, party, and quest explorer views
- party buff timing recommendations
- gear sets and gear optimization
- skill macros with dry-run previews
- best task selection for skill usage
- bulk sell planning
- skill and action result estimates
- task mutation workflows after conservative guardrails are designed

## Technical baseline

- Blazor WebAssembly PWA
- .NET 8 (`net8.0`)
- MudBlazor UI components
- Local-first storage with IndexedDB behind a Dexie.js interop boundary
- Habitica API v3
- No backend in the MVP architecture

## Prerequisites

- .NET SDK `8.0.125`
- Node.js `22.x`
- npm `11.x` or compatible

## Run locally

1. Install the pinned web dependency and sync the vendored Dexie module:

   ```bash
   cd src/Habitica.WebApp
   npm install
   npm run sync:vendor
   ```

2. Restore and run the solution:

   ```bash
   dotnet restore Habitica.sln
   dotnet run --project src/Habitica.WebApp
   ```

3. Open the local Blazor development URL shown by `dotnet run`.

## Test and verify

```bash
dotnet test Habitica.sln -m:1 -nodeReuse:false
dotnet build Habitica.sln -m:1 -nodeReuse:false
```

## Habitica API header note

The app reads `Habitica:XClientHeader` from [`src/Habitica.WebApp/wwwroot/appsettings.json`](src/Habitica.WebApp/wwwroot/appsettings.json). If it is left empty, the MVP falls back to `<current-user-id>-habitica-tool` so development remains usable, but production deployment should replace that with a project-owned Habitica `x-client` header value.

## Project documents

- `PROJECT.md` - product context and goals
- `TECHNICAL.md` - stack, architecture, storage, sync, and deployment rules
- `FEATURES.md` - planned feature behavior and constraints
- `HABITICA_API.md` - Habitica API integration rules
- `RULES.md` - repository and AI-agent workflow rules

## Notes

- Credentials are treated as password-equivalent and stay local to the user's device.
- The initial task UI is intentionally read-only.
- Mutating actions are intended to be validated, previewed, and executed conservatively before they are added.
