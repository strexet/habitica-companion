# Public PWA shell repair qualification

Task: T-c913de5b-0eff-4973-be07-089ea4391c1b. Scope: local worker repair and synthetic qualification, without deployment or affected-device incident closure.

## Delivered behavior

The published worker validates the public root/index HTML response, reconstructs trusted followed-redirect responses without redirect history, and retains content/security headers and asset integrity. Same-origin install requests refuse foreign fetch paths. The shell-v2 cache suffix separates a worker-only repair from an older cache with the same asset-manifest version. Ordinary activation removes obsolete public build caches; no forced activation, application storage clearing or Push API operation is added. Invalid/missing/unavailable caches preserve the original network path and explicit offline failure.

## Checks

- Release publication uses SDK8.0.125 with --no-restore --disable-build-servers -m:1 -nodeReuse:false -p:UseSharedCompilation=false and output/pwa-shell-redirect. publish-final.log records success. The first shared-build-server attempt timed out after180seconds; serial publication passes. The existing optional wasm-tools warning remains.
- artifact-identity.json compares the emitted worker with the exact source plus the SDK-generated manifest comment and records the bootstrap/manifest hashes.
- node-tests.log records19passing PWA, push-browser and notification-handler controls, including public-route/private-route boundaries, redirect response/body/header semantics, invalid shell refusal, install failure and cache refusal.
- browser-results.json records Chromium151.0.7922.34 and WebKit26.5 using the actual emitted worker with a synthetic integrity manifest and HTML shell. Both engines reproduce the pre-fix navigation failure from a cached redirected200 response. Both pass fresh installation, same-manifest worker update/activation, root/nested/query navigation with unavailable server responses, failed-integrity update refusal and missing-shell fallback/failure.
- Persistent disposable profiles retain synthetic local storage, IndexedDB and an unrelated-cache subscription marker across app-tab closure, upgrade and offline navigation. No platform Push API is invoked. The normal cleanup removes owned contexts, profiles and server; the fixture also has a135second execution deadline and bounded cleanup. The outer launcher supplies a160second deadline and process-group cleanup.
- Queue/index, repository consistency, changed-prose and diff checks pass. The existing historical-audit freshness warning remains.

## Fixture findings and limits

Earlier fixture attempts used an incorrect relative Cache API deletion key from a nested route, unsupported network interception/offline emulation assumptions and unbounded optional platform calls. The retained control logs document the failed harness attempts. Stage bounds, direct server connection failure, absolute deletion keys and synthetic-only subscription markers supply the final controls. Ephemeral WebKit lost an unrelated-cache canary after the last page closed; persistent disposable profiles pass the unchanged preservation requirement. No private-browsing storage guarantee follows.

The fixture does not run the full Blazor application. The Release compiler and artifact comparison qualify packaging; the synthetic document qualifies the exact worker's cache/navigation lifecycle. Physical Safari, installed iPhone PWA startup, real notification subscriptions, assistive technology, production publication and the worker actually loaded by the reporting device remain unverified. The existing physical-device release gate remains open. No visible route UI changes occur.

## Controller scope

The owner explicitly selected the new iOS task through Work Through Next. parked-controller.json preserves the unrelated stopped Implement All checkpoint byte-for-byte as structured state. The new bounded root does not expand the earlier frozen snapshot. Successful local completion restores the parked checkpoint without resuming Netlify work or another root. No commit is authorized.
