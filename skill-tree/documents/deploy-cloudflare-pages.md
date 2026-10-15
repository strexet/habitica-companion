# Deploy to Cloudflare Pages

This app is a Blazor WebAssembly app with Cloudflare Pages Functions for personal encrypted user-data sync and shared party CRON sync. Cloudflare Pages can build the app from GitHub and serve the published static files from the `*.pages.dev` domain.

## Before editing deployment behavior

Read `skill-tree/documents/rules.md`, `skill-tree/documents/technical.md`, this document, `wrangler.toml`, `build.sh`, current Functions under `functions/api`, and migrations under `migrations`. If Cloudflare Pages, KV, D1, Wrangler, or Pages Functions behavior is unclear from this repo, verify current official Cloudflare documentation before editing commands or operational steps.

## Repository files

- `build.sh` - installs the pinned .NET SDK, installs npm dependencies, syncs vendored Dexie, and publishes the Blazor WebAssembly app.
- `.node-version` - asks Cloudflare Pages to use Node.js 22.
- `functions/api/sync/[syncId].js` - Cloudflare Pages Function for legacy single-blob encrypted sync (read-only fallback during migration).
- `functions/api/sync/[syncId]/section/[sectionKey].js` - Cloudflare Pages Function for per-section encrypted sync uploads and downloads (active path).
- `functions/api/sync/[syncId]/sections.js` - Cloudflare Pages Function listing all section keys for a sync id.
- `functions/api/party-sync/[partyId].js` - Cloudflare Pages Function for shared party CRON data, quest planning, and party-sync management, protected by local claims with optional tokenized invite proofs.
- `migrations/0001_party_sync.sql` and later numbered migrations - D1 schema for party state, CRON events, quest queue/vote tables, management roles, and automatic quest completion detection.
- `wrangler.toml` - local/dev binding declarations for KV and D1.

## Cloudflare Pages settings

Use these settings when creating the Pages project:

```text
Framework preset: None
Build command: ./build.sh
Build output directory: output/wwwroot
Root directory: repository root
Production branch: main
Deploy command: leave empty
```

The build script installs .NET SDK `8.0.125` by default to match `global.json`. Override it only if `global.json` is updated in the same change.

Do not set `npx wrangler deploy` as the deploy command for the Git-connected Pages project. That command deploys a Worker and does not know which static output directory to publish. Cloudflare Pages should publish the configured `output/wwwroot` directory after `build.sh` finishes.

## Cloudflare sync storage

Personal encrypted sync requires a KV namespace bound to the Pages project.

1. In Cloudflare, open `Workers & Pages`.
2. Open `KV`.
3. Create a namespace, for example `habitica_companion_sync`.
4. Open the Pages project settings.
5. Open `Bindings`.
6. Add a KV namespace binding:

   ```text
   Variable name: HABITICA_SYNC_KV
   KV namespace: habitica_companion_sync
   ```

7. Add the same binding for preview and production environments if you use both.

The personal sync Pages Functions store only encrypted payloads. Habitica User ID and API Token are used in the browser to derive the encryption key and sync id; they are not sent to the personal sync endpoints.

Cloud sync uses per-section KV records (`sync:{syncId}:section:{sectionKey}`). Each section is encrypted and uploaded independently, staying within the 2MB per-key KV limit. When the app refreshes Habitica data or completes supported local data changes, it automatically lists remote sections, downloads and merges each section into local data, then uploads each local section back to this KV namespace.

Legacy single-blob records (`sync:{syncId}`) from older deployments are automatically migrated: the app detects no sections exist, downloads the legacy blob, imports it locally, and re-uploads as individual sections. The legacy blob remains in KV but is no longer updated. No manual migration is required.

Shared party sync requires a D1 database bound to the Pages project.

1. In Cloudflare, open `Workers & Pages`.
2. Open `D1 SQL Database`.
3. Create a database, for example `habitica-companion-party-sync`.
4. Apply all migrations in order: `migrations/0001_party_sync.sql`, `migrations/0002_party_quest_queue.sql`, `migrations/0003_quest_lifecycle.sql`, `migrations/0004_party_sync_management.sql`, `migrations/0005_app_admins.sql`, `migrations/0006_detected_quest_completion.sql`, `migrations/0007_queue_selection_controls.sql`, `migrations/0008_party_sync_invite_proofs.sql`.
5. Open the Pages project settings.
6. Open `Bindings`.
7. Add a D1 database binding:

   ```text
   Variable name: HABITICA_PARTY_DB
   D1 database: habitica-companion-party-sync
   ```

8. Grant app-admin permissions (separate from party owner; grants management access across all parties) by copying `migrations/seed_app_admins.example.sql` to `migrations/seed_app_admins.sql` (gitignored), filling in Habitica user IDs, and running `wrangler d1 execute habitica-companion-party-sync --file migrations/seed_app_admins.sql`. Revoke by setting `revoked_at_utc` on the row.

The party sync Function must not receive Habitica API tokens. Browser-only `local-claim-v1` remains the default and owner/app-admin recovery path. Migration `0008` adds optional hashed `tokenized-invite-v1` proofs: owner/app admins can issue, rotate, revoke, remove, enable, and disable proofs from Party settings. D1 stores SHA-256 token hashes, labels, issuer metadata, and lifecycle timestamps, never raw reusable proof tokens. Apply migrations through `0008` before deploying Function code that reads invite-proof settings or tables.

## Optional Auth/MCP deployment proposal

The [proposed ChatGPT enrollment, privacy and retention contract](companion-first-mcp-research.md#proposed-chatgpt-enrollment-privacy-and-retention-contract) proposes separate Auth/MCP Workers, OAuth KV and integration D1 resources. Existing Pages resources remain dedicated to private and party sync. Worker scaffold source exists; Cloudflare resources and remote enrollment are absent. Local implementation approval and live deployment scope are separate. Provider log/backup retention must be verified before activation.

## First deployment

1. Push this repository to GitHub.
2. In Cloudflare, open `Workers & Pages`.
3. Create a Pages application and import the GitHub repository.
4. Use the build settings listed above.
5. Retain the committed project-owned `Habitica:XClientHeader`, or set a public creator/app override for the frontend build:

   ```text
   HABITICA_X_CLIENT_HEADER=your-habitica-user-id-habitica-tool
   ```

   `build.sh` applies the override to the static frontend configuration. The API client rejects missing or blank configuration before network access. The header never derives from the signed-in user. The value is public in the deployed frontend. Do not put Habitica API tokens, passwords, or private keys in Cloudflare Pages environment variables for this static app.

6. Save and deploy.
7. Open the generated `https://<project>.pages.dev` URL.
8. Check a routed page refresh, for example `https://<project>.pages.dev/tasks`.
9. In Settings, use `Upload` under encrypted Cloudflare sync. If the KV binding is missing, the upload will fail with a `HABITICA_SYNC_KV binding is not configured` error. If the D1 binding is missing, shared party sync is skipped and logged as a warning.

## Local Functions testing

Use Wrangler Pages dev for local Function parity with KV and D1 bindings:

```text
wrangler pages dev output/wwwroot
```

Create local KV/D1 resources or override the placeholder IDs in `wrangler.toml` before relying on local persistence.

## Existing browser migration

Older deployments registered a service worker and used a `_redirects` file. Current deployments rely on Cloudflare Pages native SPA behavior and do not register a service worker. Existing users should unregister the old service worker, clear site data, and hard refresh once after redeploy.

## Updates

Push to the production branch and Cloudflare Pages will rebuild automatically.

## Operational notes

- A custom domain is optional. The generated `*.pages.dev` domain works for HTTPS testing.
- The app must be served from the site root with `base href="/"`. Subpath hosting is not the baseline deployment target.
- Personal sync keeps Habitica credentials in the browser and stores only encrypted KV blobs.
- Shared party sync sends browser-local claims or optional tokenized invite proofs to the Pages Function and stores shared party data, roles, settings, kick records, and hashed proof metadata in D1.
- `service-worker*.js` files may remain in the repo, but `index.html` does not register them.

## Dedicated Worker local validation

`workers/chatgpt-auth/` and `workers/chatgpt-mcp/` own package manifests, lockfiles, generated binding declarations and Wrangler configurations. `workers/shared/resources.ts` owns the exact issuer/resource pair per environment. Remote URLs and resource IDs are inert placeholders requiring operator replacement before separately authorized deployment. No resource provisioning occurs during local validation.

```sh
cd workers/chatgpt-auth
npm ci
npm run build
npm run build:preview
npm run build:production
cd ../chatgpt-mcp
npm ci
npm run build
npm run build:preview
npm run build:production
cd ../chatgpt-auth
npm test
```

Build scripts use Wrangler dry-run. Fixtures use Miniflare with a named `AuthService` binding and isolated integration D1. Protected MCP paths require valid authorization; enrollment and OAuth are disabled in all shipped configurations. Auth alone owns `OAUTH_KV`, integration D1 and declared secret names. Preview and production use distinct integration resources and Auth Service bindings. Existing Pages resources are excluded. Secrets belong in ignored local `.dev.vars` files or separately approved Worker secret provisioning, never source/configuration.

## Credential vault local qualification

`workers/chatgpt-auth/migrations/0001-integration-profiles.sql` initializes integration-only storage. `npm test` applies the schema to disposable Miniflare D1 databases; no remote migration or real enrollment runs. `PROFILE_ID_KEY`, `CREDENTIAL_KEY_V1` and `CREDENTIAL_KEY_V2` require distinct random 256-bit base64url values in separately authorized secret provisioning. Profile derivation keys must remain stable; encryption write versions may rotate while required read keys remain available. Public configuration contains only key-version selection and the tool creator header.

`ENROLLMENT_ENABLED` defaults to false in local, preview and production configurations. Enabling enrollment is a separate activation decision after the consent UI, authorization checks, endpoint limits and deletion/revocation qualification exist. The vault supports enrollment, profile-scoped disconnect and profile deletion. Request bodies are bounded; responses omit upstream bodies and secret details.

## Browser consent local qualification

The Auth consent page uses `Referrer-Policy: same-origin` so browser form navigation preserves the exact issuer Origin without disclosing a referrer to another origin. The Auth handler rejects foreign and opaque Origin headers. Provider consent cookies retain Secure and HttpOnly protections. A synthetic account completed code verification in the Codex in-app browser on `http://localhost:8787`; manual HTTP fixture cookies alone do not establish browser compatibility. HTTPS deployment and other browser families require separate rollout qualification.

## Optional linking configuration

`0002-linking.sql` initializes ticket, consent-link and attempt-limit tables in the dedicated integration D1. Both Workers require explicit `OAUTH_ENABLED`; Auth enrollment additionally requires `ENROLLMENT_ENABLED`. The PWA requires a root Auth URL in `ChatGptIntegration:AuthBaseUrl`. Public PWA origin, issuer and resource are exact environment-specific entries in `workers/shared/resources.ts`; management CORS admits the configured PWA origin only. Remote HTTPS URLs and resource identifiers remain inert placeholders pending deployment review. Local fixtures enable flags only inside disposable environments.

Activation requires secret provisioning, retention cleanup qualification, provider log/backup review and remote HTTPS/browser OAuth checks. Local test approval grants no live deployment or real enrollment. Management state remains in tab memory; lost management access requires Habitica revalidation. Cleanup blocks access before grant deletion and returns a retryable pending result if bounded grant cleanup cannot finish.

## MCP read qualification

`0003-read-state.sql` adds per-profile connection cooldown and repair state to integration D1. Local fixtures apply all three integration schemas. Build both Workers before running the Auth test suite because fixtures exercise the actual MCP service binding. MCP accepts stateless Streamable HTTP POST requests at `/mcp`; other methods return 405. Allowed Origins are the resource origin, configured PWA origin and `https://chatgpt.com`; absent Origin remains valid for native clients. Tool output omits server credential material. Local Inspector 1.0.2 CLI successfully lists tools and reads a synthetic profile. No real server enrollment or remote activation occurs during qualification.

## Optional Workspace Worker package

`workers/companion-workspace` is a third isolated Worker package with separate D1 migrations and `WORKSPACE_DB` binding. The `AUTH_IDENTITY` service binding targets the Auth Worker's private `WorkspaceIdentityService` entrypoint. The Workspace Worker has no credential keys or integration database binding. Apply Auth migration `0004-management-identity.sql` and Workspace migrations `0001-workspace.sql` and `0002-execution-ownership.sql` only inside an explicitly authorized deployment lane.

Local compilation uses `npm --prefix workers/companion-workspace run build`; preview and production scripts also produce dry-run bundles. Shipped `WORKSPACE_ENABLED` flags are false in Auth and Workspace configurations. The PWA leaves `CompanionWorkspace:BaseUrl` empty. ChatGPT linking additionally requires `ChatGptIntegration:Enabled` and the configured Auth URL; cloud workspace access requires the Auth URL without enabling ChatGPT linking. Configure exact origins in the shared resources module before an authorized deployment. Placeholder database IDs and remote endpoints are not deployable configuration. The implementation does not provision resources, apply remote migrations or activate an executor.

The private projection gateway requires Auth migration `0005-projection-gateway.sql` before the updated Auth package runs, including retention events. `AUTH_PROJECTION` on the Workspace Worker targets the Auth `ProjectionService` entrypoint. The Auth package defines hourly operational-metadata cleanup; local fixtures invoke the scheduled handler directly. No schedule or Worker has been deployed by the local implementation. `PROJECTION_ENABLED` remains false. Production activation must preserve the separate workspace/executor consent gates, qualify cleanup and verify real task preconditions in the authorized rollout lane.

## Hosted execution activation boundary

The Workspace configuration declares a minute cron and keeps `EXECUTOR_ENABLED` false in every environment. The local implementation has deployed no schedule or Worker. Activation requires an authorized deployment lane, isolated resources, the current Auth/Workspace migrations and qualified service bindings. Workspace access, Auth projection access and Workspace executor configuration must all be deliberately enabled. Configuration alone cannot enable a profile: Settings requires separate `executor-v1` consent.

The executor processes one eligible profile and at most one effect per scheduled activation. The bounded initial throughput requires rollout measurement before broad adoption. Pausing or disconnecting blocks new calls while preserving accepted data; an upstream request already transmitted may finish. A credential-context change leaves uncertain original effects fenced. Workspace deletion removes runtime and handoff rows through cascading foreign keys. User history remains until workspace deletion; thirty-day operational Auth metadata has an independent retention policy.

## Canonical command service wiring

The current Workspace package requires migration `0003-prepared-commands.sql` in addition to the initial workspace/ownership migrations. The MCP `WORKSPACE_SERVER` binding targets the Workspace `WorkspaceCommandService` named entrypoint in the matching environment. The public Workspace handler never exposes that private entrypoint. Auth supports explicit `habitica.tasks.write` grants; older read grants remain read-only. The new package and scope implementation grant no deployment authorization and enable no shipped flag.

Workspace scheduled cleanup removes expired unaccepted preparation payloads in bounded transactions before optional execution. Accepted preparation bindings remain with canonical command history until workspace deletion. Qualify cleanup timing, archive accounting, scope escalation and service identity in the authorized remote rollout lane. Browser and MCP command acceptance remain available only after the separate workspace consent and configuration gates.

## Optional verified party read binding

The Workspace environments declare `AUTH_PARTY` targeting the matching Auth Worker `PartyService` entrypoint. Auth and Workspace `PARTY_ENABLED` flags ship false. `party:read` is a separate OAuth consent scope; existing profile/account/task grants do not gain party access. The MCP Worker has no direct party proof or credential binding. Public Auth URLs cannot invoke the named entrypoint.

Build Auth, MCP and Workspace with the pinned package scripts before local synthetic party fixtures. The context tool requires an active cloud workspace but creates no shared planning namespace. Queue/voting consent and persistence remain separate required work. No real service activation is part of local build or test commands.

## Authenticated planning schema and controls

Apply Workspace migration `0004-authenticated-party-planning.sql` after migrations 0001–0003 before enabling party planning in an authorized environment. The migration creates only authenticated Workspace planning tables and does not touch party-sync D1 or credential-vault migrations. Auth and Workspace `PARTY_ENABLED` flags plus PWA `CompanionWorkspace:PartyPlanningEnabled` remain false in shipped configuration.

PWA activation requires cloud workspace access and separate consent bound to the displayed verified party. OAuth readers require `party:read`; queue/vote tools require the independently consented `party.planning.write` scope. Leader settings/deletion and management-authenticated own contribution export/removal use bounded Workspace routes. Management tokens remain tab-local. Shared planning records are not part of the task-only workspace export and are not removed by cloud workspace deletion; use the separate contribution/namespace controls. No task or migration command grants deployment authorization.

## Spell gateway activation boundary

Auth `SPELLS_ENABLED` ships false in local, preview and production. `SpellService` is a named private entrypoint and has no public route. This milestone adds no production service binding for spell dispatch and requires no new Auth migration. Existing OAuth grants do not acquire the new `habitica.spells.execute` scope automatically; approval copy discloses mana use, party effects and an automatic Habitica chat notice. Deployment, real enrollment and spell casting remain outside local synthetic qualification. Do not bind MCP directly to this write service; reviewed Workspace action persistence and recovery must precede activation.
