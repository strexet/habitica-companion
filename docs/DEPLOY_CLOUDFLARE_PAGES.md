# Deploy to Cloudflare Pages

This app is a Blazor WebAssembly app with Cloudflare Pages Functions for personal encrypted user-data sync and shared party CRON sync. Cloudflare Pages can build the app from GitHub and serve the published static files from the `*.pages.dev` domain.

## Before editing deployment behavior

Read `RULES.md`, `TECHNICAL.md`, this document, `wrangler.toml`, `build.sh`, current Functions under `functions/api`, and migrations under `migrations`. If Cloudflare Pages, KV, D1, Wrangler, or Pages Functions behavior is unclear from this repo, verify current official Cloudflare documentation before editing commands or operational steps.

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

## First deployment

1. Push this repository to GitHub.
2. In Cloudflare, open `Workers & Pages`.
3. Create a Pages application and import the GitHub repository.
4. Use the build settings listed above.
5. Add this environment variable if you want a production Habitica `x-client` value available to the frontend and Pages Functions:

   ```text
   HABITICA_X_CLIENT_HEADER=your-habitica-user-id-habitica-tool
   ```

   This value is public in the deployed frontend. Do not put Habitica API tokens, passwords, or private keys in Cloudflare Pages environment variables for this static app.

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
