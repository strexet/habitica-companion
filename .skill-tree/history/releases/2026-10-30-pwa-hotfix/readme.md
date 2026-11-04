# Critical PWA startup production hotfix

The owner explicitly requested publication of the critical iOS Safari/installed-app startup repair. The existing production site is https://habitica-companion.pages.dev. Cloudflare confirms canonical production deployment 0ca73607-0b0d-400b-b517-43255dfeadef on branch main with deploy status success.

## Published scope

The runtime source change is limited to service-worker.published.js: validated public-shell response reconstruction removes incompatible redirect history; same-origin integrity requests and shell-v2 cache identity support fresh installation and recovery from an older worker/cache. The frontend uses the retained production public endpoints and feature settings. Pages Functions source is unchanged. No backend Worker, database migration, account, gameplay action, push enrollment or callback activation is included.

The package is built from baseline f9f71b5190086d17192642545ea3dc55610eed07 plus the reviewed working-tree repair. The deployment metadata explicitly marks the source dirty. No Git commit or push is performed; the automatic Git integration remains configured and the repository hotfix is still uncommitted.

## Publication and configuration evidence

- package.json records the exact hashes of 218 static assets, four unchanged Function source hashes, public configuration checks and worker source/published identities.
- production-build.log records the production Release publish with the existing SDK and no package upgrade. The emitted worker equals the validated source plus the SDK manifest comment; production settings are merged for publish and the checked-in source settings are restored afterward.
- deploy.log and deploy-command.json record Wrangler 4.151.0, successful upload of 29 files with 189 already present, the Functions bundle and successful deployment in 16.12 seconds.
- auth-scope.json confirms the existing OAuth account and pages:write permission without credential values.
- production-postflight.json records the canonical production identity and deploy status success. Production/preview bindings, environment variables, fail-open behavior, compatibility, usage/build settings and Git integration match preflight. Direct upload clears only production.wrangler_config_hash, a source-configuration provenance marker; the runtime settings remain unchanged.
- The rollback predecessor is ddf7814a-ba73-4988-9033-a69ba7f882f9. A code rollback would not undo user data or external effects; the publication performs neither migration nor data mutation.

The direct upload runs from an isolated temporary directory without a local Wrangler config, preserving the existing dashboard runtime settings. No Pages project or backend resource is created. The local production asset build remains available under output/pwa-hotfix; the temporary upload directory is disposable.

## Validation and remaining observations

Local acceptance is recorded in ../../validation/pwa-shell-redirect/readme.md: Release build, 19 PWA/push tests and actual emitted-worker lifecycle controls in persistent disposable Chromium 151.0.7922.34 and WebKit 26.5 profiles pass. Both engines reproduce the pre-fix redirect failure and pass fresh/updated caches, simulated network loss, failed-integrity update and synthetic data-preservation controls.

Public serving checks remain unavailable from the execution environment. Both unauthenticated HTTP and a fresh browser encounter DNS resolution failure for the production host; the web tool cannot retrieve production/deployment worker URLs. The normal in-app browser reports local browser use disabled in Work settings. No browser permission, DNS, firewall or provider protection is changed. The publication success and canonical production identity are confirmed through Cloudflare's authenticated API, not inferred from the failed public probes.

Actual iPhone Safari and installed-app relaunch remain unverified. Open the site online to permit update discovery, then close all controlled Safari tabs/windows and the installed app before reopening. An older active worker can remain until ordinary activation; no site-data clearing is required by the repair. A production deployment alone does not prove the reporting iPhone has activated the repaired worker.
