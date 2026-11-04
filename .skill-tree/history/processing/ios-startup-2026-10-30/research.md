# iOS startup Pending research

Scope: standalone Process Pending for the complete two-entry selection in selection.json. Processing changes planning definitions and evidence only. No application implementation, build, runtime test, real account call, migration, publication or commit occurs. checkpoint-before.json preserves the unrelated active controller; no new task belongs to the frozen snapshot.

## iOS startup incident

Pending P-7b77deae-d804-4dba-af99-262264884301 carries owner priority107 and the two original iPhone screenshots in skill-tree/history/intake/2026-10-30-ios-service-worker-redirect/. Both screenshots show the reported service-worker redirection error. The owner reports failure in Safari and the installed application; Chrome appears to work. Exact iOS/Safari/Chrome versions, failing route and cache/controller state are unknown.

| Inspected source | Observation | Contract consequence |
| --- | --- | --- |
| service-worker.published.js onInstall | Cache.addAll receives default-follow Requests including index.html with asset integrity. | A followed Pages canonicalization redirect can enter the shell cache. Preserve integrity when repairing the public shell path. |
| service-worker.published.js onFetch | Allowlisted GET navigation returns the cached index.html Response without checking redirect history or response type. | Cover navigation response compatibility and retain sensitive, unknown, foreign and non-GET bypasses. |
| onActivate and cacheName | Old cache cleanup depends on the assets-manifest version. No skipWaiting or claim path exists. | Exercise worker-only updates with an unchanged asset-manifest version, waiting-worker activation and coherent multi-client asset use. |
| js/pwa.js and index.html | Secure load registers service-worker.js; refusal marks offline support unavailable. | Recovery cannot depend exclusively on running application UI because the reported failure precedes application startup. |
| Habitica.WebApp.csproj and build.sh | Release publication substitutes the published worker and generates the assets manifest. Development worker is network-only. | Inspect the actual Release artifacts; development success cannot qualify offline repair. |
| tests/Functions/pwa.test.mjs | Cache and network responses are plain objects with kind/request markers. | Existing assertions cannot detect redirect-history rejection, Cache API serialization, real browser navigation or activation behavior. Add a failing control and browser lifecycle evidence during implementation. |
| pwa-installation.md and testing.md | Current documentation names public-cache boundaries and an unrun physical-iPhone gate. | Keep desktop/synthetic acceptance separate from the affected device. |
| deploy-cloudflare-pages.md | Existing browser migration and operational paragraphs claim worker registration is absent and recommend clearing site data. | Correct the contradictory paragraphs within the repair task; storage erasure is unsafe as the normal recovery path. |

## Official platform evidence

Sources reviewed on 2026-10-30:

- [Cloudflare Pages serving behavior](https://developers.cloudflare.com/pages/configuration/serving-pages/) documents HTML canonicalization redirects, including folder index paths. Local source precaches an HTML index resource. The exact production /index.html redirect chain is unobserved.
- [Fetch Standard HTTP fetch](https://fetch.spec.whatwg.org/#http-fetch) rejects a service-worker response with a multi-entry URL list when the original request redirect mode differs from follow. The response history, rather than a visible final 200 status alone, therefore matters.
- [Microsoft Blazor PWA guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/progressive-web-app/?view=aspnetcore-8.0#background-updates) explains Release-only offline behavior, waiting workers and activation after clients close. Immediate activation can compromise build consistency; a published fix does not establish delivery to an existing client.

The local worker/cache path is a supported defect candidate consistent with the screenshot error, not a reproduced diagnosis of the owner's device. No WebKit-version-specific defect or Chrome exemption is asserted. WebKit source and third-party search results did not supply additional necessary evidence and do not establish the actual device execution path.

Public production reads through the web tool were inaccessible. Four unauthenticated terminal GET attempts for /index.html, /, /service-worker.js and /service-worker-assets.js each failed DNS resolution before transport. Each request had a twelve-second deadline; no request reached the host. Classify the observations as transport uncertainty, not executed application controls or evidence of a production HTTP failure. No credential, protection change or alternate publication was used.

## Bounded task decision

Promote the iOS intake into one independent priority107 main task for redirect-safe public-shell installation/serving, existing-cache update recovery and reproducible local browser validation. No schema, API, MCP, account or gameplay change is needed. Existing agent-required application workflows are unchanged; no new browser-only agent workflow is introduced.

The contract includes a pre-fix negative control, actual published-worker behavior, unchanged-manifest update recovery, online/offline route checks, invalid/foreign response refusal, failed installation/repair, durable-data and subscription canaries, and an isolated loopback browser lifecycle. Available Chromium/WebKit engines must be identified; missing required browser evidence prevents local acceptance rather than being silently replaced by mocks. No package upgrade is selected.

Physical incident closure remains an explicit separate gate: capture the affected device/version, exact URL, loaded worker/cache identity, fresh Safari and installed-app launches and post-update observations. The existing T-d76d2a9a-1318-4e63-9bb4-73f620b108cc owns broad physical-device release qualification. The broad gate is contextual, not a new dependency blocking local source repair. Source acceptance must state the remaining production/device gap. Publication and physical-device operations need separately selected scope.

## Retained mass-spell incident

Pending P-8e7323df-2f47-4e19-864d-821ed108e7f2 remains priority106. The complete original source in codev-2026-10-28/source-tasks.json includes the 17/40 incident, Error10/10 followup failures, suppressed restore errors, weak diagnostic fields, competing traffic and unknown-outcome safety requirements. Source completion acknowledges intake only.

Fresh static inspection confirms:

- AppSessionController.CastSpellAsync increments completed only after an acknowledged cast, captures the original failure stage and separates cast/equipment uncertainty, cancellation, cooldown, cleanup and refresh. Followups use a separate bounded lifetime. Counts survive cleanup and refresh failures; no cast retry is scheduled.
- HabiticaApiClient.SendForDocumentAsync owns one SemaphoreSlim across pacing admission, dispatch and header reconciliation. AdaptiveRequestPacer retains remaining/reset observations, reserve, Retry-After pauses and gradual recovery. The local gate does not prove coordination with independent external clients.
- AppSessionController.GearRecovery.cs preserves account-bound original/temporary slots, revisions, explicit reviewed operation identity, separate restoration/supersession and refusal of automatic uncertain-toggle replay. The accepted storage/browser tests qualify persistence and locking only within recorded local bounds.
- AppSessionController.SpellOutcomesTests.cs explicitly covers the eighteenth-call429 with17of40 acknowledgements, all casts followed by failed refresh, dispatched uncertainty, cancellation and failed/uncertain equipment cleanup. Retained accepted outcomes T-b89fac23-3ac2-4c64-8560-be7a5d796a81 and T-e7d542d4-4a47-4155-afc9-ab0dfb457fa0 document synthetic qualification. No fresh test pass is claimed.
- The 2026-10-29 release README reports coordinated publication and explicitly states no live spell reproduction. The release record does not identify the worker actually loaded by the affected iPhone.

No residual source defect is established by current evidence, so no duplicate implementation contract is produced. Retain Pending until fresh affected-client build/controller identity and sanitized post-update incident evidence are available. Any real 40-cast/equipment reproduction requires a bounded owner profile. The unrelated startup screenshots neither reproduce nor close the spell incident. Existing release/device limitations remain intact.

## Processing and validation boundaries

source-fingerprints.json binds inspected sources. process-input.json records exact dispositions and task scope. process-receipt.json records produced identities and provenance. Only planning/prose/index/dependency and diff checks run during processing. The helper's unrelated active-run additions are removed under the writer lock after verifying the expected helper mutation, preserving the original checkpoint exactly. The standalone receipt remains durable outside the active controller.

Standalone processing reports the complete refreshed prioritized list and retains the mass-spell blocker. Implementation or root-run creation waits for an actual owner selection under the Process Pending contract. No checkpoint alone resumes execution.
