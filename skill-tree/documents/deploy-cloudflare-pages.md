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
