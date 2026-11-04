# UX/UI production publication — 2026-11-04

Published [Habitica Companion](https://habitica-companion.pages.dev) as Pages `6d12b727-d035-4f18-b2b1-10aeb4ba91ac`. Matching Auth, Workspace and MCP bundles are active with build `42b57a6f8f92cae0dec9820059414433ef1959936bbd9793fae9c940977f1799`. Source remains dirty on local main at `d6de9811e71f93eb0f73854ebf216aa6115905ca`; no commit or push occurred. The [source receipt](source-identity-pwa-hotfix.json) binds the exact deployed payload.

## Release checks

All 3,072 automated tests pass: 1,758 .NET, 103 Functions, 244 Auth and 967 Workspace controls. All three Worker dry-run builds and the production frontend build pass. The fresh shared UI browser run passes 577/577 checks. [UI evidence reconciliation](ui-evidence-reconciliation.json) distinguishes the concurrent tooling-profile refresh from unchanged application inputs; the original full light/dark, normal/enlarged text, phone/desktop matrix remains retained.

[Public postflight](public-postflight-final.json) verifies exact bytes for 11 served assets, enabled service health, issuer/resource/PKCE discovery and no pending migrations. Auth has 15 migrations; Workspace has 23. Three additive Workspace migrations (0021–0023) precede matching code publication. Credential keys, service/storage bindings, runtime flags, cron settings and existing executor consent remain unchanged. Recovery bookmarks and predecessor versions are recorded before publication.

[Hosted browser checks](hosted-browser-smoke-final.json) pass cold startup and five routed reloads in Chromium/WebKit, at phone and desktop widths, with no page exceptions, server errors or write requests. Chromium also passes actual hosted offline startup. WebKit offline-emulation failure remains preserved; exact published assets pass the separate real-service-worker network-refusal control on loopback.

## Repairs found during release validation

- The service worker omitted `vendor/dexie.mjs`, breaking storage initialization on an offline restart. The public asset filter now includes `.mjs`; shell-v3 creates a separate cache with the same asset manifest. [Actual browser upgrade controls](pwa-module-upgrade-result.json) reproduce the old failure in Chromium/WebKit, upgrade without clearing storage, verify offline Blazor/storage startup, and preserve IndexedDB/local-storage/unrelated-cache canaries. Original hosted failures and the red Node regression remain retained.
- Eleven stale component expectations used wording replaced by the UI work. The test now verifies the current linked-recovery state, original-recovery link, disabled editing and preserved request identity across all host routes. Production component code remains unchanged by the release repair.
- Initial Functions invocation omitted the Node22 TypeScript flag; the corrected full invocation passes. Parallel Auth fixtures hit per-file deadlines and a minute-window quota expectation; a complete serialized run passes 244/244 with no product relaxation.
- Pages retained an incorrect `../../../output/wwwroot` Git-build destination from a prior direct upload. Production now uses the documented repository-root `output/wwwroot`; public overrides, source settings, preview/production runtime settings and both fail_open values are preserved.
- One initial upgrade-fixture assertion selected the hero h1 instead of the sign-in h2. The corrected semantic selector and explicit offline storage-module import pass; the original failed fixture result remains retained.

## Remaining review

- Physical iOS/Android, actual installed-PWA relaunch, native input/assistive technology and real account workflows remain owner-review gates.
- Hosted Chromium verifies actual offline startup. WebKit hosted online startup passes; WebKit network outage is verified on loopback with exact published assets because offline emulation returned a browser-internal failure.
- No real account test, consent, enrollment, gameplay operation, new subscription or scheduled operation was initiated by release checks. Existing enabled executors, flags, secrets and service bindings remain unchanged.
- No Git commit or push occurred. Publication uses the exact dirty-source and artifact hashes recorded here. A later Git push can replace the manual Pages deployment.

The existing Implement All checkpoint `WR-6a0a0290-50ab-4e5b-9173-4e676c2362fe` remains blocked at owner-controlled qualification. Publication supplies no acceptance for that gate. Six Ready UI followups remain deferred; the publication changes no queue disposition.

Close all existing app tabs/windows, then reopen the stable website or installed app so the browser can activate the matching worker. Retain application storage.

## Rollback and provenance

Use the recorded exact Worker/Pages predecessors only after checking current ownership. Additive migrations, retained records and unknown-outcome fences survive code rollback; database restore or deletion is not a routine rollback. Older clients may reject newer document fields, so inspect compatibility before a downgrade after new canonical writes. Worker runtime bytes match the reviewed bundles; Cloudflare-normalized source maps retain equal mapping/name/source payloads. The final Pages predecessor is the first UI deployment; the original pre-release Pages and Worker versions remain recorded separately.

Deployment commands follow [Cloudflare Pages CLI](https://developers.cloudflare.com/workers/wrangler/commands/pages/), [D1 migration commands](https://developers.cloudflare.com/workers/wrangler/commands/d1/), [Worker version upload](https://developers.cloudflare.com/api/resources/workers/subresources/scripts/subresources/versions/methods/create/) and [rollback limitations](https://developers.cloudflare.com/workers/versions-and-deployments/rollbacks/).

A current plugin archive is available at [habitica-companion-0.3.1-ux-ui-20261104.zip](https://habitica-companion.pages.dev/downloads/habitica-companion-0.3.1-ux-ui-20261104.zip). The previous versioned archive remains available. Backend compatibility does not prove package installation or replace host metadata refresh.
