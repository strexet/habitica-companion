# Habitica Companion Client

Third-party companion client for Habitica power users. The project is focused on local data analysis, explainable recommendations, and safe assisted actions rather than replacing the official Habitica app.

Current status: web-app MVP implemented and expanding into guarded actions. The repository contains a Blazor WebAssembly PWA shell with Habitica credential sign-in, staged refresh, cached account/task/party/inventory snapshots, local dashboards, task and spell actions with freshness gates, shared party quest planning, diagnostics, and local/cloud data controls.

## Current MVP features

- credential sign-in with session-only default and persistent local opt-in
- manual and page-prioritized refresh against Habitica API v3
- cached account dashboard with class, stat, companion, and inventory summary cards
- Dashboard `Start New Day` action when the current user needs Cron
- inventory and equipment explorer with slot-grouped owned gear keys, battle presets, highest-stat highlights, and guarded equip actions
- party overview with cached quest progress, member CRON timing, shared quest pool, shared queue voting, and recently completed quest history
- spells workspace with target selection, approximate effect previews, dynamic gear recommendations, sequential casting, and Cron-sensitive buff warning
- diagnostics workspace with safe checks, guarded reversible tests, curated API presets, and a shared redacted log console
- local-first task snapshot storage through IndexedDB with a Dexie-backed JS module
- local-first account snapshot storage for offline dashboard access
- encrypted per-section Cloudflare sync for portable app data, with legacy single-blob restore fallback
- responsive app shell with sign-in, dashboard, inventory, party, diagnostics, tasks, and settings routes
- task workspace with search, filters, completed toggles, detail panels, freshness indicators, inline scoring/checkoff, and Habit multi-score controls
- sign-out for the current tab session and clear-local-data controls

## Planned feature areas

- richer quest explorer and party member views
- tokenless party-sync membership proof
- dashboard pending damage estimates and health-potion helper
- task history statistics and charts
- gear optimization
- skill macros with dry-run previews
- bulk sell planning
- skill and action result estimates
- per-section cloud sync status and conflict UI

## Technical baseline

- Blazor WebAssembly PWA
- .NET 8 (`net8.0`)
- MudBlazor UI components
- Local-first storage with IndexedDB behind a Dexie.js interop boundary
- Cloudflare Pages Functions/KV for encrypted app-data sync
- Cloudflare Pages Functions/D1 for shared party quest planning state
- Habitica API v3

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

## Deploy

For Cloudflare Pages deployment, use [`docs/DEPLOY_CLOUDFLARE_PAGES.md`](docs/DEPLOY_CLOUDFLARE_PAGES.md). This is the simplest hosted path for the Blazor WebAssembly app and works without a custom domain.

## Habitica API header note

The app reads `Habitica:XClientHeader` from [`src/Habitica.WebApp/wwwroot/appsettings.json`](src/Habitica.WebApp/wwwroot/appsettings.json). If it is left empty, the MVP falls back to `<current-user-id>-habitica-tool` so development remains usable, but production deployment should replace that with a project-owned Habitica `x-client` header value.

## Project documents

- `PROJECT.md` - product context and goals
- `TECHNICAL.md` - stack, architecture, storage, sync, and deployment rules
- `FEATURES.md` - implemented and planned feature behavior
- `FUTURE.md` - validated remaining backlog
- `HABITICA_API.md` - Habitica API integration rules
- `RULES.md` - repository and AI-agent workflow rules

## Notes

- Credentials are treated as password-equivalent and stay local to the user's device.
- Habitica-changing actions are explicit, freshness-gated, and run through the application/API layers.
- Larger or destructive future actions should keep using validation, preview, confirmation, and follow-up refresh.
