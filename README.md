# Habitica Companion Client

Third-party companion client for Habitica power users. The project is focused on local data analysis, explainable recommendations, and safe assisted actions rather than replacing the official Habitica app.

Current status: web app implemented and expanding through the active `.skill-tree/documents/future.md` queue. The repository contains a Blazor WebAssembly PWA shell with Habitica credential sign-in, staged refresh, cached account/task/party/inventory snapshots, local dashboards, task and spell actions with freshness gates, pets and mounts planning, shared party quest planning, diagnostics, and local/cloud data controls.

## Current major features

- credential sign-in with session-only default and persistent local opt-in
- manual and page-prioritized refresh against Habitica API v3
- cached account dashboard with class stats, Start New Day, pending-damage, armoire, gem-for-gold, and health-potion helper cards
- Dashboard `Start New Day` action with optional gear optimization when the current user needs Cron
- Equipment workspace with slot-grouped owned gear keys, battle presets, highest-stat highlights, equipment optimizer recommendations, and guarded equip actions
- Pets & Mounts workspace with companion collection browsing, hatching/feed planners, fast equip actions, and guarded bulk sell planning
- party overview with cached party summary, member CRON timing, tokenless party-sync claims, Officers, settings, and kick management
- Quests workspace with cached quest progress, shared quest pool, shared queue voting, recently completed quest history, and guarded quest lifecycle actions
- spells workspace with target selection, approximate effect previews, dynamic gear recommendations, sequential casting, and Cron-sensitive buff warning
- diagnostics workspace with safe checks, guarded reversible tests, curated API presets, and a shared redacted log console
- canonical Todo, Daily and Habit workspaces with reviewed commands, original receipts, planning, projects, saved lists and retained activity
- personal Markdown Notes with project associations, separate export/erasure and reviewed MCP access
- owner-reviewed browser-to-cloud Workspace adoption, preserving task and Notes identities
- foreground reminders, voluntary task reviews and gated Web Push scheduling
- local-first task snapshot storage through IndexedDB with a Dexie-backed JS module
- local-first account snapshot storage for offline dashboard access
- encrypted per-section Cloudflare sync for portable app data, with legacy single-blob restore fallback
- responsive app shell with sign-in, dashboard, tasks, equipment, pets and mounts, party, quests, spells, settings, diagnostics, and privacy routes
- task workspace with search, filters, completed toggles, detail panels, history charts, manual ordering controls, freshness indicators, inline scoring/checkoff, and Habit multi-score controls
- color schemes (on Settings, Dashboard, and Sign-in) with built-in presets, shell/control theming, task min/base/max shade ramps, random themes with a chaos slider, and copy/paste custom presets saved as portable user data
- sign-out for the current tab session and clear-local-data controls

## Active future work areas

[future.md](.skill-tree/documents/future.md) owns remaining work, including Notes folders, event subscriptions/delivery and host/mobile qualification. Auth, MCP and Workspace Workers, reviewed task/organization/Notes tools, and unified Workspace adoption are implemented. Source defaults disable optional integration; the separate [production frontend override](deploy/production.frontend.json) enables the selected Auth and Workspace endpoints. [Deployment documentation](.skill-tree/documents/deploy-cloudflare-pages.md) owns configuration and rollout boundaries; release records establish publication evidence for the recorded build only.

- party quest workspace modes and history analytics
- skill macros with dry-run previews
- task mutation dry-run summaries and remaining guarded-action estimate coverage

## Technical baseline

- Blazor WebAssembly PWA
- .NET 8 (`net8.0`)
- MudBlazor UI components
- Local-first storage with IndexedDB behind a Dexie.js interop boundary
- Cloudflare Pages Functions/KV for encrypted app-data sync
- Cloudflare Pages Functions/D1 for shared party quest planning state
- Optional Auth/MCP/Workspace Workers for encrypted enrollment, reviewed agent commands and canonical cloud storage
- Habitica API v3

## Prerequisites

- .NET SDK `8.0.125`
- Node.js `22.x`
- npm `11.x` or compatible

## Run locally

1. Install the pinned web dependency and sync the vendored Dexie module:

   ```bash
   cd src/Habitica.WebApp
   npm ci
   npm run sync:vendor
   ```

2. Return to the repository root, then restore and run the solution:

   ```bash
   cd ../..
   dotnet restore Habitica.sln
   dotnet run --project src/Habitica.WebApp
   ```

3. Open the local Blazor development URL shown by `dotnet run`.

## Test and verify

```bash
dotnet build Habitica.sln -m:1 -nodeReuse:false
dotnet test Habitica.sln -m:1 -nodeReuse:false
```

## Deploy

For Cloudflare Pages deployment, use [`.skill-tree/documents/deploy-cloudflare-pages.md`](.skill-tree/documents/deploy-cloudflare-pages.md). The documented hosted path supports the Blazor WebAssembly app without a custom domain.

## Habitica API header note

The app reads the public, project-owned `Habitica:XClientHeader` from [`src/Habitica.WebApp/wwwroot/appsettings.json`](src/Habitica.WebApp/wwwroot/appsettings.json). The committed value identifies the tool creator and keeps the `habitica-tool` application suffix for every signed-in user. Missing or blank configuration prevents API-client initialization; no end-user fallback exists. Forks and local experiments must supply a separate creator/app identity through configuration. `HABITICA_X_CLIENT_HEADER` overrides the value during `build.sh`; never include an API token in the header.

## Project documents

[Document manifest](.skill-tree/skill-tree.json) indexes canonical documentation, queue state and audit freshness. [Repository map](.skill-tree/documents/repository-map.md), [build and release](.skill-tree/documents/build-and-release.md), [testing](.skill-tree/documents/testing.md) and [dependencies](.skill-tree/documents/dependencies.md) provide toolchain context.

- `.skill-tree/documents/project.md` - product context and goals
- `.skill-tree/documents/technical.md` - stack, architecture, storage, sync, and deployment rules
- `.skill-tree/documents/features.md` - current implemented feature behavior and partial limitations
- `.skill-tree/documents/future.md` - single active implementation queue and validated backlog
- `.skill-tree/documents/habitica-api.md` - Habitica API integration rules
- `.skill-tree/documents/ai-app-testing.md` - browser UI/UX testing guide for AI agents
- `.skill-tree/documents/rules.md` - repository and AI-agent workflow rules

## Notes

- Browser sign-in defaults to session-only credentials; persistent browser storage requires opt-in. Separate integration enrollment sends credentials to the configured Auth Worker only after explicit consent and stores an encrypted vault envelope. Credentials never enter app-data sync or MCP tool responses.
- Habitica-changing actions are explicit, freshness-gated, and run through the application/API layers.
- Larger or destructive future actions should keep using validation, preview, confirmation, and follow-up refresh.

## Optional workflow plugin

[`plugin/`](plugin/README.md) packages daily review, account review and reviewed canonical task-management skills with the remote MCP declaration. The committed package endpoint uses an inactive `.invalid` placeholder. A prepared private package can select an approved endpoint; package installation and discovery remain separate from backend publication and OAuth enrollment. No Habitica token is copied into ChatGPT. Local package tests use isolated synthetic profiles.
