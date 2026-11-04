# Testing

## Current policy and source-bound results

The generated `.skill-tree/installation/profile.json` owns concrete recipes, lanes and toolchain inputs; [installation context](skill-tree-installation.md) explains static qualification. The catalog below describes fixture assertions and prerequisites. A fixture description supplies no current passing result. Select the smallest relevant checks, record actual discovery/assertions, source/test/environment fingerprints, deadlines, cleanup and required skips, and require matching current evidence before acceptance.

`.skill-tree/history/validation/`, structured task outcomes and release records retain build-specific verdicts. Historical counts never establish current source qualification. Source defaults, selected deployment flags, per-account consent and verified live availability are independent. [Deployment policy](deploy-cloudflare-pages.md) owns remote prerequisites; local tests use synthetic providers unless an explicit live lane is authorized.

## Local live-test credentials

The owner may provide a root [`test-login.json`](../../test-login.json) with `userId` and `apiToken` for an explicitly authorized live test on the owner's Habitica account. The file is ignored by Git, should have owner-only file permissions, and must not be committed, deployed, included in fixtures, printed in logs, or copied into validation evidence. The credential file grants no permission for gameplay changes, party actions, cloud linking, or another live-test round; use the current user instruction and the [AI app testing guide](ai-app-testing.md) for the allowed scope. Synthetic tests use fixture identities instead.

## Browser query normalization regression

Publish tests/Habitica.BrowserChecks/Habitica.BrowserChecks.csproj with the pinned local .NET SDK to an isolated output directory, serve the isolated wwwroot over loopback HTTP and open that origin in a browser. The page must report PASS for every shared task-query-v1.json case and unchanged input. The isolated app contains synthetic embedded data and makes no service requests. The isolated app exercises the actual browser-only NFKC interop branch, including composed accents, compatibility characters, U+0130 and Greek sigma. The isolated app remains outside production publication and ordinary desktop test discovery.

## Application topology

`tests/Habitica.Domain.Tests/`, `tests/Habitica.Rules.Tests/`, `tests/Habitica.Api.Tests/`, `tests/Habitica.Storage.Tests/` and `tests/Habitica.Application.Tests/` contain xUnit domain, calculation, API, storage and orchestration checks. `tests/Habitica.WebApp.Tests/` uses bUnit for Razor components, page behavior and session state. `tests/Functions/` uses the Node test runner.

After the prerequisites and build in [build-and-release.md](build-and-release.md):

```sh
dotnet test Habitica.sln -m:1 -nodeReuse:false
node --test tests/Functions/*.test.mjs
```

Default checks require no live Habitica credentials. [ai-app-testing.md](ai-app-testing.md) owns browser/UI guidance. Diagnostics live checks, live mutations, deployments and real accounts require applicable authorization. Documentation migration runs no application build, application tests or live-service checks.

## Inventory optimizer qualification

InventoryPageTests checks Intelligence defaults for warrior, wizard, rogue, healer and unknown classes, invalid named/numeric input, explicit alternate goals across session refresh notifications and zero equipment or preset writes during preference changes. Opt-in HABITICA_BROWSER_EVIDENCE exports fresh and stale Inventory states with long recommendation names and enabled/disabled actions. The existing browser matrix exercises current published styles across responsive widths, enlarged text and deterministic light/dark schemes. Keyboard focus and seeded geometry/contrast failures provide separate evidence. Physical devices, assistive technologies and actual Habitica equipment changes remain unqualified by synthetic fixtures.

## PWA packaging and relaunch qualification

PWA packaging and navigation checks run with node --test tests/Functions/pwa.test.mjs. The fixture checks actual icon dimensions, secure registration, every current Razor app route, network-only sensitive paths and public cache installation. AppSessionController PWA tests reconstruct sessions with shared synthetic credential storage to distinguish explicit persistence, session-only sign-in, unavailable storage and cancellation. Sign-in/Settings tests export collapsed/expanded installation steps and check disclosure interactions without browser permission requests. The [physical iPhone gate](pwa-installation.md) records unrun device checks separately from desktop and synthetic evidence.

Redirect controls exercise Response bodies/headers, final URL/type restrictions, same-origin integrity requests, invalid shells and cache refusal. After a Release publish to output/pwa-shell-redirect, run node tests/browser/pwa-shell.mjs with PLAYWRIGHT_MODULE pointing to an available pinned Playwright package. The disposable loopback fixture uses persistent temporary browser profiles, the emitted worker, a synthetic integrity manifest and Pages-style index redirect. Chromium and WebKit controls demonstrate the pre-fix failure, same-manifest worker update/activation, fresh install, offline routes, failed integrity update and missing-shell behavior. Synthetic IndexedDB, local storage and an unrelated-cache subscription marker qualify preservation without platform Push API calls, notification permission or actual push enrollment. The fixture simulates unavailable network responses by closing server connections, avoiding engine-specific offline emulation. The fixture closes owned browser contexts/server and removes temporary profiles under a hard deadline. Full Blazor startup, production activation and physical iPhone behavior remain separate checks.

## Planning validation

```sh
python3 .agents/skills/skill-tree-process-future-pending/scripts/validate-future-document.py .skill-tree/documents/future.md --strict --format json
python3 .agents/skills/skill-tree-repo-documentation-audit/scripts/validate-repository-documentation.py . --format json
python3 .agents/skills/skill-tree-implement-next-future-task/scripts/select-prioritized-task.py .skill-tree/documents/future.md --format json
python3 .agents/skills/skill-tree-repo-documentation-audit/scripts/workflow-state.py index .
git diff --check
```

Task selection follows current priority, dependencies and unresolved questions. Metadata validation checks manifest paths/hashes, the installed skill catalog, index agreement, dependency closure and checkpoint identities without changing state. Structural validation and indexed state consistency never establish implementation readiness, external compatibility or completed acceptance. The complete prose check includes every canonical document plus root discovery files with `--warnings-as-errors`; exceptions require explicit source review rather than disabled diagnostics.

Prose lint reports located errors and review warnings. `--warnings-as-errors` or repository `--strict` elevates warnings for strict qualification. Scoped plan validation avoids rewriting unrelated current-state prose.

## Canonical workspace persistence fixtures

`tests/browser/workspace-storage.html` is a synthetic, test-only browser fixture outside the PWA build. Serve the fixture with only the real storage module and pinned Dexie vendor asset on a dedicated local origin. The fixture seeds schema 1, imports the real schema-2 module, injects a quota failure through the browser IndexedDB API, races revision-controlled writes and checks reload persistence. The fixture origin must remain separate from the app origin; no real account data or credentials belong in the fixture. C# storage tests qualify interop arguments, scoped identity and strict journal parsing; application fixtures qualify recovery and restore conflicts.

## Cloud workspace qualification

Build the Auth, MCP and Workspace packages before running `npm --prefix workers/companion-workspace test`. The workspace fixture reuses the Auth package's pinned Miniflare runtime and requires the Auth dependencies. Tests create isolated local databases and mock Habitica credential validation. The tests cover account isolation, explicit consent, command deduplication, concurrent acceptance, injected D1 append failure, canonical domain constraints, session revocation and delayed requests across deletion or disconnect. Archive capacity rejects additional commands without removing retained history.

The neutral Worker journal fixture is validated by the C# application suite, including command payload hashes. Web tests cover separate workspace consent, safe configuration, distinct management/session tokens, export identity validation and late-response cancellation. The browser storage fixture adds account-scoped workspace discovery and canonical clearing while preserving legacy data. All destructive fixture checks use a separate disposable browser origin. Real enrollment, deployment, mobile eviction, mixed PWA versions and live Habitica projection remain separate validation lanes.

## Projection core qualification

Auth projection fixtures cover precondition ownership/expiry, durable duplicate receipts, uncertain scores, positive alias reconciliation, write refusals, quota/credential pauses, simultaneous dispatch attempts, reservation failure, automated spacing, Daily preconditions and scheduled retention. Workspace fixtures cover reviewed adoption, metadata changes between preview and confirmation, deletion/edit conflicts, immutable accepted intent and ordered create-then-complete composition through the real private gateway. Every outbound Habitica response is synthetic.

The application suite validates current and legacy Worker journal fixtures and retained accepted task snapshots. Browser IndexedDB qualification rejects edits to accepted intent and verifies the intent after reload. Executor fixtures qualify persisted ownership, receipt-only restart recovery and paced dispatch orchestration. Current deployed activation requires separate observed release evidence.

## Hosted execution qualification

`workers/companion-workspace/test/executor.test.mjs` exercises the built Worker services and isolated D1 databases with synthetic Habitica responses. Direct execution-core calls inject only the pacing wait for deterministic time control; shipped code has no timing override. The tests assert requested delays and persisted thirty-second deadlines, while Auth tests independently reject premature calls. The scheduled handler is also invoked through the local Worker service. Synthetic Daily execution verifies account, due and score ordering across two spacing waits.

Restart fixtures lose an acknowledgement after the Auth receipt commits, construct a new execution invocation, and assert one upstream effect with recovered journal evidence. Missing receipts and revoked credential contexts remain unknown; repeated wakes cannot grow history or replay effects. Other fixtures cover opt-out during waits, quota fencing, expired undispatched leases, transfer hashes, independent identity collisions and append rollback. Application tests cover local freeze, active-attempt waiting, late ownership acknowledgement and failed freeze. Web tests cover separate consent and late-response guards.

The isolated browser storage fixture verifies the frozen authority and immutable accepted intent after full reload. The ordinary rebuilt Settings smoke uses disabled shipped cloud configuration. Component tests exercise enabled and paused execution controls against synthetic application services. The local qualification does not cover real consent, deployment scheduling or upstream mutations.

## Workspace access renewal qualification

Synthetic tests require explicit renewal consent in connected Settings, reject a late renewal after tab access is forgotten, and preserve an uncertain original command across failed workspace reload and successful receipt recovery. The recovery fixture asserts one accepted identity and one canonical task. Rebuilt default Settings/Tasks browser checks preserve the disabled source-default cloud boundary.

Setting `HABITICA_BROWSER_EVIDENCE` for the focused component tests exports synthetic HTML rendered by the actual Razor components. Serve the exported HTML with the current app and pinned MudBlazor styles on an isolated local origin for narrow-screen layout inspection. The exported fixtures qualify static layout; component tests qualify interactions, and the ordinary rebuilt app qualifies default routing and feature guards. The fixture output contains no real account data or credentials.

## Foreground traffic pacing qualification

`AdaptiveRequestPacerTests` drives one hundred actual API-client dispatches through a synthetic clock and HTTP handler. Budget tests cover early slowdown, reserve protection, remaining/reset reconciliation, lower limits, malformed reset headers, bounded failure backoff and gradual success recovery. Concurrent-call tests hold the first response, cancel a queued caller, then cancel a cooldown wait without dispatching another request. Mutation failures verify one synthetic HTTP dispatch for 429, 503 and transport failure. The API suite and app build qualify local behavior; no real account traffic occurs.

## Canonical task command qualification

`workers/companion-workspace/test/task-api.test.mjs` calls the shared browser HTTP API and the actual MCP SDK through built Worker bindings. Coverage includes server-issued preparation identity, duplicate acceptance, stale/expired/old-epoch previews, exact canonical selection, malformed fields, tenant isolation, scope challenges and independent private-service scope enforcement. Structured receipts distinguish saved canonical data from pending, confirmed, quota-paused and unknown projection states. The synthetic executor paths assert no second upstream effect when acceptance is repeated.

The suite qualifies partial updates, Todo and dated Daily completion, wrong-kind rejection, bounded positive/negative Habit events and expired unaccepted preview cleanup. Accepted preparation bindings survive cleanup. Web/controller tests qualify review-before-save, immutable original identity after acknowledgement loss, redacted failures, impossible receipt rejection, fresh mapping approval, failed-refresh editing guards and account-change response rejection. Default browser checks use disabled cloud configuration; the opt-in synthetic real-Razor fixture qualifies 360-pixel wrapping separately from component interactions.

## Authenticated party qualification

`party.test.mjs` covers verified minimal context and independent scope checks. `party-access.test.mjs` first verifies a normal read, then disconnects, deletes or replaces Workspace access while Auth reads are in flight; late facts must be discarded. `party-planning.test.mjs` covers separate consent, exact reviewed party matching, owned-scroll and prototype-key rejection, explicit vote deduplication, concurrent CAS, leader/member/outside-party permissions, namespace deletion/recreation, contributor cleanup after departure and multi-statement rollback on a stale revision. Fixtures make no upstream gameplay writes.

PWA component/client fixtures require explicit consent and destructive confirmation, hide leader-only actions from members, encode untrusted labels, discard late account responses, use management identity for own exports after disconnect and strip unknown fields from exported records. The opt-in `HABITICA_BROWSER_EVIDENCE` fixture exports actual Razor markup for phone-width layout measurement; model/host activation and live multi-user behavior remain separate rollout checks.

The gated Workspace lifecycle fixture uses a fixture-only Worker and native service bindings. The gate buffers the synthetic identity response and shares boolean state; each request owns separate polling promises and has a ten-second deadline. Concurrency stress and the current Auth/Workspace checks require all five stale-operation rejection and replacement-session success assertions. Failure diagnostics expose only status and identity-consistency booleans. The fixture qualifies local lifecycle behavior; synthetic transport failures do not establish a production session defect.

## Private spell gateway qualification

The Auth `spells.test.mjs` fixture uses synthetic accounts, OAuth grants and private Miniflare bindings. The fixture verifies allowlist/level/class/mana, stale hashes and party changes, immutable attempt receipts, overlapping requests, cross-profile denial, quota, revocation, storage failure and shared task/spell unknown-effect fencing. No real Habitica request is made. The neutral `tests/fixtures/workspace/spell-safety-v1.json` is consumed by Rules tests and Worker tests: agreement covers metadata and single-cast affordability; the server adds health, classes-enabled and three-cast limits. The fixture does not claim formula or full execution parity.

Build Auth before the Auth fixture suite so the tests exercise the changed bundle. Run source-matched Worker builds/type checks and affected Auth/Workspace/application regressions before claiming integration qualification. Checked-in integration defaults remain disabled; selected production flags and live host evidence remain separate. Workspace prepared-action qualification is described below.

## Reviewed spell action qualification

Workspace `spells.test.mjs` runs actual MCP SDK requests through OAuth, Workspace/D1 and the private Auth gateway using synthetic accounts. Cases cover a three-cast confirmation, explicit partial refusal, changed state between casts, two-minute expiry, token rotation and single use, duplicate/concurrent execution, namespace/consent/epoch revocation, isolated history export/deletion, independent scope gates and failed durable dispatch persistence. Unknown spell records continue to fence task execution after deleting expired Auth operational receipts. A lost acknowledgement is resolved through lookup only, with the remainder stopped.

The separate `spell-deadline.test.mjs` delays one buffered synthetic private response by twenty-one seconds and verifies known partial success, no second dispatch and a separate unsuccessful refresh indicator. Buffering the response before the gate avoids parking a live Miniflare stream across concurrent Worker calls. The transport fixture does not simulate or qualify real Cloudflare network latency. Both files retain the normal thirty-second per-file test deadline. Use matching Worker bundles and current affected Auth/Workspace/application checks. Settings availability depends on selected public configuration and consent. No real enrollment or upstream effect occurs.

## Overlapping page refresh qualification

Two controlled .NET regressions reproduce the original Background-versus-Visible priority failure. One holds an in-flight Party fetch while background/visible callers share the request and checks distinct caller metadata. Another holds only an older page's equipment-cache read while a newer page refresh completes, then releases the older view and checks retained current-page priorities. Both cases fail against the original source and pass after the ownership repair. Test fixtures use synthetic data and no live Habitica requests. Full app build and .NET suites qualify integration with existing refresh and storage behavior.

## UserGet readiness shape regressions

Auth read/projection and plugin/hosted-Daily fixtures model top-level `needsCron` from full UserGet. Cases verify true/false values, contradictory synthetic legacy flags, unknown/malformed values and refusal to score while Cron is pending. Four API parser cases verify server preference and the retained local fallback without using `flags.needsCron`. The broad .NET run exposed a separately repaired refresh-priority race; evidence resides in `refresh-priority.json`. The lifecycle fixture supports parallel file scheduling and retains explicit in-test concurrency assertions.

## Private Cron gateway qualification

Auth `cron.test.mjs` exercises synthetic OAuth/private bindings, fresh account/day/task/checklist/quest facts, independent scopes, replay/overlap, quota, durable reservation failure, credential/grant revocation and both directions of shared gameplay fencing. A failed response followed by advanced lastCron and needsCron=false remains unknown, fences new task/spell effects and performs no replay. Missing original receipts remain uncertain. Source-shape checks use full top-level readiness, not synthetic flags.

`tests/fixtures/workspace/cron-day-v1.json` is consumed by Domain and Worker cases. The fixture covers before/at boundaries, positive/negative and quarter-hour offsets, and leap-day rollover. Scope is fixed-offset day key/start only, without server timezone-transition, Cron eligibility or effect-formula parity. Matching Worker builds and current affected Auth/Workspace/application checks are prerequisites for integration qualification. Parallel Worker file scheduling retains every explicit concurrency assertion. Default Cron capability is unavailable. No real Habitica calls occur.

## Reviewed Cron behavior qualification

Workspace Cron tests exercise the full synthetic MCP/Auth/Workspace composition: rotating single-use previews, exact fresh preconditions, expiry, independent grants, cross-profile isolation, concurrent requests, no-party accounts, unknown day progression, retained fences, lost original acknowledgements, crash recovery and persistence failures on each side of dispatch. A confirmed original response with failed refresh remains confirmed. Tests prove task and spell effects remain fenced after Auth receipt cleanup. Default flags and fabricated action IDs cannot cause effects. Local browser Settings checks remain separate from real multi-user and mobile-device release qualification.

## Management boundary checks

Synthetic Auth tests cover current permission projection, last-use metadata, credential-repair state, cross-profile denial, CORS, grant-only revocation, access/refresh rejection, retained vault identity, management-token rotation and the durable epoch barrier without provider cleanup. Workspace cases verify old task/spell/Cron previews cannot execute after renewed grants, old in-flight epochs cannot roll back newer authority, late old previews fail, and unknown effects retain fences. Component/client checks cover separate confirmations, cancellation, token replacement, lost acknowledgements and forgotten-tab late responses. A real-Razor synthetic Settings fixture supplies narrow layout evidence; component tests qualify interactions. Real accounts, devices and production logs remain separate release checks.

## Credential key operation qualification

Synthetic private-service and direct-helper checks cover bounded resume, idempotent job identity, envelope/profile preservation, safe summaries, removal guards, atomic progress rollback, unreadable envelopes, concurrent credential replacement, active/expired ownership, missing/equal keys, changed target configuration, unsupported inventory and strict inputs. Inactive-job expiry preserves encrypted credentials. No real key migration, secret change, deployment or profile-ID key rotation occurs. Application builds and default unavailable behavior remain separate from operator qualification.

## Reconnection and inaccessible receipt regression controls

Four composition controls cover retained Workspace access after grant revocation followed by profile deletion/re-enrollment, inaccessible unknown Cron receipts, inaccessible unknown spell receipts and successful original-refusal lookup. The first three controls fail against the unrepaired source; all four pass after the boundary repair. Cases preserve canonical tasks, reject stale previews, retain task/spell/Cron gameplay fences and prove no additional upstream effects. A successful original refusal still resolves uncertainty without dispatch.

## Dev configuration qualification

`node --experimental-strip-types --test --test-timeout=15000 tests/Functions/dev-preparation.test.mjs` checks complete frontend merge, credential/URL rejection, isolated generated resources and source restoration through actual build-script success/failure controls with offline tool substitutes. Worker dry-run builds and release app publication qualify compilation separately. `dev-preparation-default-settings.jpg` records published default Settings with optional cloud capabilities disabled. Real Cloudflare resources, secrets, hosted OAuth, ChatGPT/Codex installation and devices remain external qualification lanes.

## Traffic and operational status qualification

Auth traffic controls test concurrent atomic caps, separate recovery/profile budgets, anonymous metrics, pre-auth MCP throttling, upstream refusal before HTTP and persistent 429 cooldown. Projection controls prove quota refusal preserves an unconsumed create precondition and creates no dispatched receipt. Existing vault fixtures apply the complete current schema. Workspace controls aggregate task/spell/Cron uncertainty without task content or cross-profile data and deny stale sessions. The parked identity-race fixture materializes bodyless quota responses without weakening epoch/deletion assertions.

App HTTP/component controls cover authenticated bounded status, malformed/missing/oversized counts, no automatic quota retry, manual-only execution and late account/session rejection. `operations-status-narrow.jpg` records the real Razor status layout; synthetic layout evidence does not qualify a mobile device or hosted connection.

## Complete MCP read permission challenges

The MCP read guard advertises both base profile access and the selected read-tool scope. The actual Auth/MCP fixture verifies denial of a profile-only grant, fresh consent using exactly the advertised scopes and successful account status retrieval. Negative controls must reject grants missing the required base profile scope. No write permission or live host qualification follows from the read upgrade.

## Local and hosted boundary checks

| Boundary | Required synthetic assertions | Separate hosted/device gate |
| --- | --- | --- |
| Configuration and legacy data | Public override merge/restoration, exact feature-profile availability, legacy encrypted-sync/party and portability fixtures | Selected account/resources, published settings, HTTPS and real data continuity |
| Adoption and backup | Exact IDs/history, populated-target conflicts, reviewed transfer, frozen originals and original receipt recovery | Owner browser inventory, real account consent and storage durability |
| Interrupted execution | CAS, epochs, revoke during waits, paced dispatch, deduplication and durable task/spell/Cron unknown fences | Explicit real-effect scope; live Cron requires separate authorization |
| Party and management | Membership, role checks, independent consent, contributor cleanup, revocation and guarded key operations | Consenting multi-user tests and provider log/backup review |
| Client authorization | Exact callback/issuer/audience, profile/tool scopes, refresh/recovery and cross-profile isolation | Actual ChatGPT/Codex OAuth, loaded package/discovery and model activation |
| Responsive and accessible controls | Current Razor interactions plus published-asset light/dark, phone/desktop, enlarged-text, focus and seeded detector assertions | Real iOS/Android, assistive technology and complete keyboard/touch journeys |

[Recorded E14 results](../history/validation/e14-local-qualification.json) identify historical sources and bounded assertions; current acceptance requires source-matched reruns. Local fixture results grant no hosted deployment, real enrollment, live Habitica effect or device acceptance.

## Native client and operational-log qualification

Native tool calls, ChatGPT behavior and packaged-skill installation require independent observations. Read reuse across a long interval can establish only observed continuity; hidden refresh exchanges, browser PKCE details, acknowledgement loss, denied-write scopes and grant revocation require separate controls. A second consenting account and real mobile devices remain broader release prerequisites.

Operational-log checks retain only sanitized field names, categories, status, duration and bounded counts. Provider transport envelopes, URLs, request headers, account data and payloads must be discarded before persistence. Use a hard deadline and owned-process cleanup for live tails. A successful read sample covers only the sampled path; error paths and complete provider logging require separate evidence. [Recorded native pilot evidence](../history/validation/codex-native-owner-pilot.json) preserves the original source/account observations without establishing current acceptance.

## Durable admission qualification

`upstream-pacing.test.mjs` exercises isolated D1 with independent admission callers, competing synthetic profiles, one hundred later admissions, conservative header reconciliation, hard cooldowns, gradual recovery, profile deletion and bounded expiry cleanup. An injected second-statement failure proves global/profile reservation rollback. Enabled Worker fixtures reject reads before upstream dispatch and preserve revocation precedence. The projection suite enables pacing for ambiguous scoring and retains original-receipt and no-second-effect assertions. Shipped flags remain disabled; synthetic qualification supplies no live traffic or deployment result.

## Recurrence calendar qualification

`recurrence-calendar-v1.json` is a neutral fixture consumed by Domain tests, direct TypeScript tests and a standalone Worker running in the pinned Miniflare compatibility environment. Twenty-five expansion cases and ten UTC/local-date cases cover anchored intervals, multiple weekdays/month days, short months, leap years, maximum-year bounds, DST changes, quarter-hour offsets, unsupported zones and invalid ranges. Separate checks enforce the inclusive 366-day limit and malformed-rule rejection. The Worker fixture denies every outbound request. Desktop Domain and actual Workers-runtime results remain distinct from unrun Blazor browser integration and real device qualification.

## Recurrence command qualification

Neutral command-v3 fixtures cover schedule creation, explicit clearing and combined typed game details with matching C#/TypeScript hashes. Existing v1/v2 fixtures and serialization checks qualify legacy identity/default-field preservation. Application tests cover partial edits, scheduled and unscheduled completion, duplicate acceptance, schedule clearing, retained history, malformed rules, wrong kinds and projection refusal. Journal tests reject fabricated game effects and contradictory/missing accepted recurrence snapshots.

The synthetic MCP SDK fixture inspects the advertised recurrence schema, reviews and accepts a scheduled Daily, rejects an unscheduled date, repeats the original completion identity, reads the original receipt and clears the schedule without losing history. A second synthetic profile cannot read the receipt. The fixture makes zero upstream writes. PWA recurrence controls, actual host discovery, packaged-skill activation and live tests remain unqualified.

## Recurring lifecycle qualification

Application and Worker cases exercise skipped/completed-date refusal, pause/resume, explicit rule restart, retained history, unsupported mappings and immutable receipt identity. Neutral lifecycle fixtures qualify appended operation hashes. Portable archive checks reject fabricated lifecycle game effects. The local controller test loses a skip acknowledgement and recovers one retained occurrence through the original receipt. Cloud-adapter tests require the exact execution tool and refuse contradictory game-effect receipts.

The synthetic MCP SDK fixture discovers skip_occurrence, pause_task and resume_task with write scopes, reviews and accepts each command, retains skipped/completed dates through restart and verifies zero upstream writes. Read-only grants are denied at MCP and the private Workspace service; another profile cannot read the receipt. Real host discovery, live qualification and browser action controls remain outside the local result.

## Canonical combined query qualification

The neutral task-query-v1.json fixture covers thirty query/page cases across Application, direct TypeScript and an actual local Workspace Worker: AND combinations, category inheritance/override, labels, all-state visibility, archived exclusion, inclusive civil dates, absent dates, mapping states, normalized Unicode and bounded continuation. Invalid inputs and stale revisions reject explicitly. Both pure evaluators preserve the original document.

Synthetic MCP discovers the advertised read-only filter schema, performs a combined query, rejects missing tasks:read at MCP and the private service, and isolates another profile. A document revision change rejects a continuation page. Queries create no command/preparation rows and issue zero upstream writes. Saved views, mobile filters and actual client/device qualification remain outside the local result.

## Civil agenda qualification

The neutral agenda-v1.json fixture compares ten ordered cases across Application, direct TypeScript and an actual local Workspace Worker. Cases cover plan/deadline coalescing, distinct dates, retained completed/skipped overrides after rule replacement, paused Daily history, explicit paused Habit planning, archived/completed Todo exclusion and guarded continuation. Archived occurrence dates cannot regenerate pending work; stale persisted pending records outside the replacement rule remain hidden. Separate checks reject invalid civil ranges, unavailable active timezones, stale revisions, row limits and rule-evaluation capacity; a full 366-day single-rule range remains valid. Pure evaluators preserve the source document.

Synthetic MCP discovers list_companion_agenda with read-only annotations and tasks:read, retrieves the same rows and rejects missing grants at both service boundaries. Another profile sees an empty agenda. Actual Worker capacity refusal returns an error without partial data. The checks create no command/preparation rows and issue zero upstream writes. Production calendar UI, mobile behavior and live qualification remain separate.

## Organization read qualification

The neutral organization-v1.json fixture compares thirteen list/page cases across Application, direct TypeScript and actual local Workspace/MCP calls. Coverage includes archived entities, active-parent section visibility, position ties, exact category filtering, original IDs, guarded pages and mixed/empty Todo goal progress. Invalid input fields, identities and bounds reject explicitly; stale revisions remain conflicts. Pure queries preserve the original document.

Synthetic MCP discovers all five tools with read-only annotations and tasks:read. Every tool rejects missing grants at MCP and the private service, and another profile receives no organization records. Reads create no command/preparation rows and issue zero upstream writes. Package guidance distinguishes canonical reads from separately authorized reviewed organization writes. Actual host activation, production UI and live qualification remain separate.

## Task-only status qualification

Synthetic D1/MCP tests cover all eight durable task projection states, separate requested/confirmed effect totals, partially confirmed entries, empty queues, executor opt-in versus deployment availability and oldest pending acceptance time. Prepared spell/Cron rows do not affect task totals. Status reads retain canonical task rows, executor settings, store version and preparations after the existing authorization epoch is established. A current database snapshot overrides an older in-memory row revision; stale/deleted access epochs fail.

MCP discovery exposes an empty read-only input schema under tasks:read. Extra selectors and executor inputs reject, missing grants fail at both boundaries and another profile receives an empty task queue. Malformed JSON, unknown durable states/effects and inconsistent confirmation counts return unavailable without partial data. No executor dispatch or upstream write occurs. Real host activation and live qualification remain separate.

## Canonical read recovery

Synthetic MCP service-error and unreadable-reply fixtures verify unavailable observations for canonical task, agenda, project, goal, exact-task and original-receipt reads. Read failures retain safe retry metadata without saved-state claims, optimistic counts or a request to recover an action that never occurred. Mutation failures preserve original command receipt guidance and unknown-outcome fields. Missing receipt data still cannot resolve an earlier uncertain action. Normal authorization and party/spell/Cron handling remain separate.

## Native placement qualification

Neutral planning-command-v4.json cases compare set, clear/full-label replacement and partial-date hashes across C# and TypeScript. Legacy command fixtures and default-key checks remain unchanged. Application/Worker cases cover task order, partial preservation, explicit clears, active metadata references, project/section consistency, invalid/mixed payloads, original identities, portable snapshot tampering and zero game effects across linked/pending/uncertain mappings. Daily, paused Habit and completed Todo placement retains lifecycle state.

Controller checks recover the original local plan receipt after a lost acknowledgement. Cloud adapters validate exact plan_task routing and refuse altered native previews or fabricated effects. Synthetic MCP discovers the planning schema, reviews native placement on a linked task, reuses the original prepared/accepted identity and reads one no-effect receipt. Exported history/mappings remain unchanged; write-grant and profile isolation remain enforced. Production editor controls, real host behavior and live qualification remain separate.

## Completion history qualification

The neutral completion-history-v1.json fixture compares sixteen cases across Application, direct TypeScript and actual Workspace/MCP reads. Coverage includes retained archived records, matching command snapshots, current-detail fallback, historical null notes, normalized detail search, precise UTC fraction boundaries, distinct occurrence dates, revision/stable ties and guarded pages. Pure evaluators preserve the original journal/document. A separate forty-one-record case proves that canonical history does not inherit the legacy recent-completed limit.

Synthetic service checks reject invalid filters, mismatched snapshots, malformed JSON, stale revisions and deleted/changed access epochs. An older in-memory document cannot override the current batch snapshot. MCP exposes a read-only schema under tasks:read; missing grants fail at both service boundaries and another profile sees no records. Reads preserve tasks/commands/store state after existing authorization epoch establishment and issue zero upstream writes. Production history UI remains unqualified.

## Todo restoration-cycle model qualification

The neutral todo-restoration-cycles-v1.json fixture covers twenty-six valid/invalid documents across C# and the pinned Worker runtime. Cases cover retained open/closed cycles, empty/null/absent extensions, causal overlap, duplicate references/commands, wrong task kinds, global IDs, UTC/default times, archive retention, current-state consistency and wall-clock skew. Current-state goal progress matches in both runtimes. Daily completion uniqueness remains covered by existing fixtures.

Application checks complete a restored Todo with a new event while preserving prior cycles and original identity recovery. History exposes explicit restoration metadata and omits the extension for ordinary rows. Backup restore and frozen handoff retain all cycle data. The standalone runtime fixture denies every outbound request. The model qualification covers data invariants; reviewed command qualification follows below.

## Reviewed Todo restoration qualification

Independent todo-restore-command-v5.json hashes compare exact completion, revision and target identities across C# and TypeScript; existing v1-v4 fixtures remain valid. Application and Worker checks cover repeated restore/complete cycles, retained metadata/order/history, exact references, wrong states, stale revisions, invalid/mixed patches, retained game mappings including archived identities, and portable snapshot/effect tampering. Browser controller tests recover one restoration event through the original receipt after acknowledgement loss. Cloud adapter tests reject altered completion/target/fields and game-effect receipts.

Synthetic MCP SDK checks discover the schema and write grant, prepare an exact history event, recover original preparation/acceptance identities across later completion cycles and export the intact portable journal. Profile isolation and both public/private grant fences remain enforced. Recorded upstream writes remain zero. Production controls, real host behavior and live qualification remain separate.

## Retained Habit history qualification

The neutral habit-history-v1.json fixture supplies sixteen independent expected pages and invalid filters for C#, shared TypeScript and the actual synthetic Workspace/MCP reader. Coverage includes separate same-command events, up/down selection, archived tasks/events, historical versus current details, historical null notes, dotted/sigma and compatibility text folding, inclusive seven-digit UTC bounds and guarded continuation. A forty-seven-event case proves independence from legacy recent-history limits. The neutral journal retains valid command hashes and snapshots.

Synthetic service checks retain the current coherent document, reject stale revisions, wrong snapshot shapes/operations/directions and lost access epochs, and separate unavailable data from empty history. Public/private read grants and profile isolation remain enforced. Pure and warmed service reads preserve source data and create zero preparations or upstream writes. Synthetic canonical scoring records three distinct events; replay of the same command preserves three events and the original receipt. Package schema/tool routing and read-only activity guidance are validated locally; actual host/model activation, mobile controls and streak semantics remain unqualified.

## Unblocked Todo candidate qualification

The neutral unblocked-todos-v1.json fixture provides sixteen expected pages across C#, TypeScript and synthetic Workspace/MCP reads. The fixture covers active versus waiting/someday/paused/completed/cancelled task states, Daily/Habit exclusion, completed archived dependencies, archived incomplete dependencies, mixed dependency sets, parent-link noninheritance, combined normalized details/metadata/date/mapping filters and guarded continuation. Source tasks and document order remain unchanged. Archived completed dependencies retain actual completion records.

Local native restore/completion tests block and reopen dependant eligibility without scoring dependants. The restored dependency can enter the list as active work, so identity assertions distinguish replacement from an assumed count decrease. Synthetic reads use current D1 state despite an earlier routing row, reject corrupt JSON/missing graph references/stale pages/lost epochs, and return unavailable without optimistic tasks or mutation uncertainty. Public/private read grants and profile isolation remain enforced. Warmed reads create no command/preparation or upstream write. Production ranking/review controls and live qualification remain separate.

## User permissions versus agent test isolation

Synthetic API cases cover all sixteen supported class spells in normal and explicit test profiles, with targeted spell identities retained. Ordinary API defaults permit existing party-management/manual Cron routes, while explicit test profiles reject the party-management/manual Cron routes before transport. Every profile rejects party departure, including encoded route variants. Controller regressions qualify task-targeted and party spells in both profiles, retaining ordinary freshness/mana and sequential execution behavior. Razor regressions cover every class in both profiles without a test-only spell disable reason.

Frontend source and deployment-generator checks require normal user permissions. The production override is inspected before the release artifact build; public settings and asset hashes are compared after publication. Public verification performs no authenticated Habitica operation and supplies no real gameplay or multi-user qualification.

## Native Todo relationship qualification

Independent task-relationships-command-v6.json cases compare parent/dependency setting, explicit clears and partial replacements across C# and TypeScript. Existing v1-v5 fixtures and absent default fields remain valid. Application/Worker checks cover exact kinds/references, duplicates/self/missing/archived targets, the 100-dependency bound, combined graph cycles, metadata/order/state/history retention, mapping fences and portable snapshot/effect tampering. Original command/request identities remain stable across duplicate acceptance and reject changed intent reuse.

Browser controller checks recover the original native receipt after lost acknowledgement and preserve parent state. Cloud adapter checks bind exact targets, parent and dependency sets and refuse altered previews or game-effect receipts. Synthetic SDK tests discover the schema/write grant, review relationships on an existing linked task, accept once and recover original receipts. Exported history/mappings remain intact; public/private grants and profile isolation remain enforced with zero upstream writes. Production controls and actual host/model activation remain unqualified.

## Organization-history model qualification

The independent organization-history-v1.json fixture supplies fifteen kind/action hashes and thirty-one portable valid/invalid documents for C# and the pinned Worker runtime. Coverage includes Unicode names, every metadata kind, partial edits, category clearing, section positions, goal task replacement, retained legacy references including sets beyond the new 100-ID replacement bound, noncascading archival, unused/empty extensions, reversed clocks with causal revisions, wrong identities/references, duplicate revisions/IDs, invalid patches, snapshot/current-state mismatch and fabricated effect fields.

Root shape validation and full-journal checksum validation are tested separately. Corrupt but well-shaped hashes cannot pass journal serialization, task acceptance or frozen handoff. Native task writes preserve organization history; task/native command IDs, accepted revisions and gameplay-event roles remain separate. Restoring a Todo changes current goal progress without changing metadata history. Browser backup/restore retains paused authority and frozen handoff retains exact history. Synthetic D1 handoff/retry/export and native task acceptance preserve the same metadata with zero upstream writes. A standalone runtime fixture denies every outbound request. Actual host activation remains unqualified.


## Native organization acceptance qualification

Independent fifteen-command create/update/archive sequences exercise every metadata kind in Application and the pinned Workers runtime. Exact payload hashes, original receipts after later archival/freeze, partial edits, category clearing, full goal replacement and retained legacy goal references remain covered. Invalid kinds/fields/references, archived parents/targets, global entity-ID collisions, foreign/stale revisions, immutable section project membership, exhausted versions and frozen/cloud authority fail without source mutation.

Application and Worker store checks cover conflicting commits, changed-intent identity reuse, unavailable storage, failed commits and lost acknowledgements with original-command recovery. Application cancellation propagates before persistence. A pinned Worker/D1 fixture verifies compare-and-swap concurrency, aborting storage failures and acknowledgement loss with exactly one retained native receipt and zero task/gameplay entries. Runtime outbound requests are denied. Synthetic unknown game mappings remain unchanged by metadata edits. Production account behavior remains separate.


## Reviewed organization adapter qualification

Synthetic actual-SDK tests discover the independent write scope and accept create/update/archive for all five metadata kinds through Auth, MCP and Workspace. Exact previews, original request retries, duplicate acceptance and receipt reads retain native snapshots, task order/history and projection mappings with zero upstream writes. Two profiles cannot access foreign previews or receipts. Existing task-write grants fail at both public MCP and private binding boundaries; the organization permission has a separate disclosure and can be omitted from the selected OAuth grant.

Preparation field whitelists reject account/URL/effect selectors and client-selected creation IDs. Stale canonical revisions, expired previews, grant changes, access-epoch changes and task/native role confusion cannot accept metadata. Retention preserves accepted native request/correlation bindings across expiry cleanup and grant changes while purging unaccepted preparations. A real synthetic D1 abort rejects acceptance without changing canonical JSON, version or byte accounting; retrying the original command recovers one receipt. Capacity refusal creates no extra preparation or gameplay row.

Typed browser checks bind requested fields, exact partial changes/clears/targets, accepted snapshots and receipt hashes while normalizing only server-issued creation/command IDs. Altered previews, effect/event claims, receipt identity/hash mismatch, expired previews and late session replies cannot report saves. Failed acknowledgement retains original-command recovery; unavailable native receipt reads follow canonical read semantics without inventing mutation outcomes. Plugin package validation and actual SDK name discovery qualify packaged guidance independently of real host activation. Production UI, live enrollment and gameplay remain outside the local result.


## Native Todo state qualification

Four independently calculated v7 hashes agree in C# and TypeScript; unused defaults and prior operation values remain stable. State/source/mapping matrices exercise every supported desired state, noncompleted legacy sources and all projection states while retaining task order/details, metadata history and mappings. Invalid desired states, completed/archived/missing or non-Todo targets, mixed patches, projection requests, stale revisions and portable snapshot/effect tampering fail.

Current goal/candidate checks distinguish native cancellation from completion and dependency satisfaction. Synthetic actual SDK exercises exact previews, all desired states, original receipts after later edits, public/private grant refusal and profile isolation with zero upstream writes. Browser controller checks recover the original native receipt after acknowledgement loss; client preview/receipt checks bind exact desired state/target and reject game effects. A synthetic project/goal workflow composes reviewed organization creation, task placement, explicit dependencies, cancellation/reactivation and separate completion without implicit game effects. Real hosts, production controls and mobile acceptance remain unqualified.

## Saved appearance convergence qualification

ColorSchemePanelTests checks two independent device stores, imported selection/custom-edit timestamps, mounted selector and JS cache updates, reload selection, unsaved-draft choices, transient random previews, local-save versus cloud-failure feedback and serialized local-save/import storage writes. AppSessionControllerTests checks remote appearance merge before narrow upload, serialization against whole-data sync and rejection of noncooperative late downloads after logout/account replacement. Default fixtures contain synthetic data only.

Browser qualification uses synthetic Razor output and the production colorSchemes.js implementation to inspect actual CSS application, preview preservation, cached selection and reload at desktop and phone widths. Component tests qualify interactive draft decisions separately. Local frames do not establish actual Mac/iOS cross-device transport, Safari keyboard, assistive technology or globally atomic KV convergence.


## Tab connection recovery regression controls

WorkspaceCloudClient.ConnectionStatusTests and ChatGptIntegrationClientTests cover independent fresh clients, explicit recovery without linking/grant creation, retained server state, expired management access, missing executor flags, malformed identity and late session replies. SettingsPageTests and WorkspaceOperationsTests cover consent gating, unknown status copy, failed refresh clearing prior success, recovered executor state, read-only checks and account replacement. AppSessionControllerTests verifies direct account replacement forgets tab management without reenrollment. The connection checks use synthetic HTTP/component fixtures and contain no live credentials.

Synthetic real-Razor output is exported through HABITICA_BROWSER_EVIDENCE for published CSS and light/dark layout inspection. Such frames qualify rendering separately from component event tests; actual Mac/iOS, hosted enrollment and background dispatch remain outside synthetic connection validation.


## Settings session action geometry

SettingsPageTests exports real session-action Razor markup with original and extended descriptions and verifies logout dispatch plus diagnostics routing. Synthetic browser checks compare existing layout with the scoped flexible-row layout at 360/390/430/768/1024/1200 CSS pixels, default light/dark schemes and 200-percent root text. Measurements include action baselines, horizontal bounds and control heights. Native keyboard activation is observed separately from component delegate assertions. Frames do not establish physical-device or assistive-technology acceptance.


Derived-foreground regression canaries exercise gold and mid-tone base surfaces through the production theme API, retaining exact authored colors when readable. Existing live-appearance, preview and saved-cache checks run with the contrast assertions. Rendered measurements and gradient coverage remain owned by the page qualification workflow.

## Reusable rendered page qualification

PageLayoutEvidence exports actual Razor output, route/state/surface inventory and exact interaction-test names when HABITICA_BROWSER_EVIDENCE is set. Existing WorkspaceLayoutEvidence forwards captures without introducing a browser dependency into default tests. Gryphy light/dark, Toxic Swamp, Boss Battle and seeded calm/moderate generated schemes are serialized from ColorSchemeCatalog. Extreme chaos remains an explicit separate choice.

The browser-qualification preparation script stages published CSS, production colorSchemes.js and fixture assets. The generated route inventory lists captured and omitted routes, forms, navigation, inline confirmations, tables, charts, logs and disabled/error/loading states. Changed routes require captures through --changed-route. Component event assertions remain independent of static fixture rendering. The matrix runs 360/390/430/768/1024/1200-pixel frames at 100/200-percent root text, with five-second case deadlines and a 300-second matrix deadline. Case identities discard late replies. Asset fingerprints identify measured inputs.

Geometry checks detect page overflow, out-of-bounds controls, intersecting peer controls and primary targets below the app 44-pixel rule. Text checks use computed foregrounds, alpha compositing through ancestor backgrounds and sampled generated PNG/vertical linear gradients. Normal/large text thresholds remain unrounded. Disabled text is excluded from contrast thresholds and counted. Unsupported paint is explicitly unqualified, never silently passing. Seeded geometry and equal-color text controls must fail. Screenshots and keyboard/focus observations complement automated samples. SVG graphics, pseudo-elements, modal-overlay lifecycle, actual assistive technology and physical devices require separate evidence. Sampled CSS checks establish bounded regressions rather than complete WCAG conformance.

Official reference context: [contrast minimum](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) defines normal/large text thresholds and inactive-control exceptions; [target size minimum](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) remains distinct from the stronger app 44-pixel primary-target contract.


Shared surface-text canaries cover gold/status/muted colors, transparent authored text, exact retention of readable foregrounds and unchanged preferences. Actual browser qualification distinguishes surface text from filled-button labels and includes default/generated palettes, enlarged Tasks grids, sign-in trust messages and primary target geometry. Decorative fill colors remain independent from derived text colors.


## Diagnostic safety and feature coverage

DiagnosticsLogConcurrencyTests force overlapping append/append and append/clear calls to the same scoped DiagnosticsLogStore. Storage gates verify newest-first committed history, configured retention and the default 250-entry limit. Cancellation before acquisition, cancellation during read/write/remove and storage failures preserve gate ownership and allow the next mutation to finish. Waiting uses asynchronous acquisition; no thread blocking or cross-tab transaction is introduced. GetRecentAsync remains an observational read of the last committed storage value.

DiagnosticsLogWriterTests exercise secret/note canaries, safe legacy serialization, bounded counters, UTC/correlation fields, failed or stalled storage, failed formatters, original exceptions, cancellation and account-change context removal. WorkspaceDiagnosticsTests exercise real local command acceptance, lost cloud acknowledgement and original recovery without extra dispatch, late account results, cancellation, bounded retention, private backup/section redaction and appearance emission without per-render flooding. WorkspaceOrganizationClientTests verify validated organization outcomes and original correlation after uncertainty. AppSessionControllerTests verify local diagnostic refresh without sign-in or snapshot refresh.

LiveTestsPageTests verify sanitized copied/downloaded JSONL and fresh local-read invocation, with an explicit unavailable message when local storage cannot refresh. Representative normal and unavailable Diagnostics Razor states belong to the rendered route inventory. Existing sync, connection, theme and command regressions retain primary behavior assertions; removed user-defined metadata receives explicit non-disclosure assertions. Synthetic checks do not qualify live server logs, agent log RPC access, actual hosted clients or physical devices.


Compact diagnostic console checks cover twelve mixed-severity rows, collapsed and expanded multiline details, exact per-entry JSON, six-entry filtered JSONL, private-data exclusion, clipboard refusal and successful retry. diagnostics-export.test.mjs exercises the actual JavaScript helper with successful, missing and denied clipboard implementations. Real-browser native Enter/Space activation and disclosure focus remain distinct from component event assertions. Synthetic fixtures never write the system clipboard or qualify physical devices. Native summary controls participate in shared geometry checks.


## Diagnostic bundle and agent guidance qualification

DiagnosticsBundleTests validate version-1 JSON round-trip readability, newest-first 100-record bounds, deduplication, combined filters, empty UTC windows, omitted counts, invalid enum rejection and credential/note/instruction canaries. LiveTestsPageTests assert no export before preview, exact reviewed copy/download content, JSON media type and preview invalidation after filter changes. The package fixture validates all five skill folders against the manifest contract and verifies each referenced tool name through the actual SDK inventory with isolated synthetic profiles. Skill validation runs in a temporary Python environment without changing repository packages. Rendered Diagnostics states cover the expanded bundle and compact list/disclosure states; real Codex/ChatGPT attachments, installed activation, physical devices and assistive technologies remain separately unqualified.


## Todo source and account boundary checks

TodoWorkspacePresentationTests cover independent browser choices without merging, offline empty-document reads, explicit load/list failures, cloud availability during browser-list failure, frozen/cloud-owned browser authority, stale revision guards, linked task provenance without duplicate Habitica cache rows, retained original acceptance across presentation recreation, unavailable-cloud receipt recovery, cancelled late reads and invalid snapshots. Gated regressions exercise account switch and same-account re-entry after sign-out, plus superseded source loads and newly prepared original commands during delayed reads. Test gates use five-second deadlines. Existing WorkspaceTaskController and WebApp tests retain command/projection behavior assertions. No real service request, gameplay effect or new page usability qualification is implied.


Synthetic Razor captures normalize bound textarea value attributes into escaped textarea text content through the existing AngleSharp parser. Static browser fixtures therefore display the same initial multiline value as the bound Blazor field. Source component markup and production binding remain unchanged. BoundFieldEvidenceTests preserve ordinary unbound fields and verify escaped closing-tag/script canaries; a real-browser value read confirms literal text without an injected script.


## Inbox and planning qualification

WorkspacePlanningQueryTests cover eligibility before paging, stage-independent Inbox membership, deadlines distinct from planning, deduplicated overdue tasks, exclusions, immutable reads, bounds and stale revision refusal. TodoPlanningPanelTests cover all six views, Monday week and leap-month boundaries, period changes, twenty-row pages, exact reviewed set/clear, original receipt recovery, retained mapping and history with no game effects, paged project selection, category/section/label changes, stale editor retention and explicit reload, empty/unavailable states and unavailable-zone whole-agenda refusal. Component fixtures validate canonical documents before rendering.

Rendered synthetic Inbox, Week, placement editor/review and unavailable/empty states use the shared browser matrix. Native controls provide keyboard navigation; component handlers and focus requests are qualified separately from static Razor rendering. Physical devices, screen readers, production publication and live task mutation remain outside local planning acceptance.


## Retained history and restoration UI qualification

TodoHistoryPanelTests use valid synthetic journals to cover original snapshots versus current fallback, retained notes, UTC event and civil Daily labels, archival flags, completion/Habit filters, direction and inclusive UTC-day bounds, twenty-row pagination, revision resets, empty/unavailable states and invalid ranges. Exact Todo restoration tests retain original history, recover the accepted original receipt after acknowledgement loss and complete a second independent cycle with no game effects. Linked, pending, unknown, conflict and retained external mappings, including archived mappings, never expose or prepare native restoration. Native keyboard disclosures and rendered theme/layout checks qualify presentation separately from Blazor event-handler tests. Physical-device, screen-reader, hosted-client and live restoration checks remain unqualified.


## Organization controller qualification

WorkspaceOrganizationControllerTests exercise local/cloud create, rename and archival across all six native kinds, independent organization history and retained project/category, section/project and goal/task relationships. Synthetic APIs assign different prepared/create identities and retain exact request correlation. Tests cover lost preparation, lost acceptance, receipt recovery without another dispatch, already accepted original preparation, stale revisions, no-op/invalid references, expiry, failed refresh, frozen/cloud-owned browser authority, account/sign-out re-entry and late responses, changed cloud sources, fabricated game effects, transport/caller cancellation and sanitized diagnostic modes/results. Original task journals and zero-effect receipts remain exact. No organization editor UI, live service mutation or device behavior is qualified by controller tests.

## Project removal qualification

Shared project-removal-v3 fixtures bind independent golden hashes in C# and TypeScript for assigned, archived and empty projects. Engine tests preserve every task, section, hierarchy edge, completion, Habit score, restoration, command and uncertain mapping; export/import validates tombstone history. Missing, mixed, reordered, stale and changed approvals are refused. Original acceptance remains idempotent. Removed references stay readable through exact project filters while Inbox and inherited category membership reflect Unassigned presentation.

Actual local SDK/Worker tests cover discovered project commands, required revision and organization scope, isolated accounts, original request/receipt recovery, no upstream effects and complete approvals above 64 KiB. Requests over one MiB fail explicitly. Browser adapter/controller tests bind approval arrays and tombstones, preserve task journals and recover uncertain local/cloud acceptance. Rendered planning fixtures qualify retained assignment labels under the shared width/theme/text matrix separately from component events. Deployment, live organization mutation and physical devices remain outside synthetic qualification.

## Todo hierarchy qualification

Shared todo-hierarchy-v1 fixtures cover stable preorder, repeated names, absolute depth, child counts, archived parent anchors, forest/subtree/children/ancestor selections and revision-bound parent coverage across page boundaries. C# and TypeScript generated chains contain 2001 independent Todos and 2000 parent edges in reverse canonical array order. Bounded pages recover every node without recursive traversal. Missing/non-Todo targets, invalid filters, broken references, self-parent and combined cycles fail explicitly. Deep reparent/detach retains project membership and requests no game effects.

Actual SDK and direct Worker tests compare hierarchy pages, enforce both read-scope boundaries, isolate accounts and recover an accepted exact move after a deliberately lost response. Original receipt retries create no duplicate command. Subsequent detach retains other task rows, metadata, completion history and game mappings. Stale continuation supplies no partial rows. Browser facade tests distinguish available hierarchy from unavailable or foreign task identities. Existing read-error and packaged workflow discovery tests include the hierarchy tool. No new tree editor markup, rendered UI journey, live account mutation or device behavior is qualified by read-model tests.

## Browser descendant workflow qualification

TodoHierarchyPanelTests exercise retained root/child traversal, one-level and whole-branch expansion/collapse, exact parent navigation, archived anchors, same-name identities, twenty-row branch and parent-choice pagination, cross-project descendants, deep links at level 2000 and source/account clearing. Edit retains parent and independent fields. Move preflight refuses combined cycles before preparation; accepted move/detach preserves descendants and project identity. Stale drafts require explicit context reload. Keyboard focus requests cover branch/editor/review transitions; typing does not trigger heading refocus.

Two-step child creation uses separate reviewed original commands. Synthetic cloud and browser-local lost acknowledgements recover both commits. Lost create/attach preparations retain original request correlation. An archived parent after creation leaves one saved independent child with no duplicate creation. Rendered route/state fixtures cover long context, project scope, deep levels, descendant editing, create/attach/move reviews and uncertain acceptance under the shared width/theme/text matrix. Physical devices, assistive technologies and hosted client behavior remain separate qualification.

## Optional organization UI qualification

TodoOrganizationPanelTests exercise category/project/section/label/goal authoring, exact rename/archive, omitted archived references, goal membership and progress updates after Todo completion, cancelled/ineligible exclusions and empty progress. Duplicate choices expose exact references. Lost native preparation/acceptance recovers original request/receipt identities. Project removal requires acknowledgment of every reference page and retains tasks, parent links and uncertain game mappings. Stale metadata requires explicit editor reload. Source changes reset metadata pagination even when workspace revisions match. The composed Projects page has one primary heading and independent branch/source controls.

TodoOrganizationCoordinationTests cover mutual task/native exclusion, local/cloud original recovery, failed refresh, rejection of silent revision rebasing, late account changes between load and preparation, returning-owner pending source restoration and reused workspace identities. TodoTaskWorkflowTests cover independent state/stage reviews, inactive-state precedence, exact dependency replacement and combined cycles, retained incompatible references, duplicate names, unsupported stage input, stale/account clearing and reviewed project/section/label/day placement. Root changes reuse the retained descendant suite and canonical native engine tests. Current rendered qualification covers metadata, goal/progress, review, uncertainty, removal, workflow and descendant states under the documented widths, themes and enlarged text. No live service mutation, deployment, device or assistive-technology result follows from synthetic tests.

## Recurring browser qualification

TodoRecurringEditorTests cover daily/weekly/monthly full replacement, applicable fields, invalid or empty intervals/start dates, unavailable timezones, duplicate/out-of-range month dates and no-op refusal before transport. Dated action cases cover saved-rule provenance, non-due weekly/monthly dates, a DST boundary, completed/skipped precedence, pause/resume and schedule clearing without history loss. Unscheduled completion stays separate from skip. Retained game mappings, including archives, prevent native preparation; existing main game-aware completion remains separate.

Typed creation carries recurrence through browser storage; invalid or unsupported rules are refused before preparation. Local/cloud lost acknowledgements and cloud lost preparation recover original command/request identities without duplicate history. Stale revision and account transitions clear or fence drafts. History uses bounded pages and unavailable zones create no fabricated due state. Habit pause exposes no type-conversion or Daily schedule fields. Component focus assertions distinguish editor activation from typing. Current-source rendered fixtures cover normal/weekly/monthly forms, exact replacement/skip review, uncertainty, paused/unscheduled history and linked/unavailable controls across shared widths, themes and enlarged text. Physical devices, hosted clients, assistive technologies and live mutations remain unqualified.


## Saved-list browser qualification

TodoSavedListPanelTests covers ten cases using the native command engine and local/cloud controller fixtures. Full-query creation evaluates current canonical tasks without duplicate tasks or task-API preparation. Name-only updates preserve omitted query; explicit empty predicates replace all filters. Unblocked source, dependency completion, active-stage semantics, invalid ranges/enums/references and no-op rejection are checked before native transport. Archived organization references remain exact. Archiving preserves native history and suppresses all-task fallback.

Lost local/cloud acceptance and cloud preparation recover the original receipt/request without a replacement. Definition and result paging, duplicate names, fixed dates after task changes, stale editor reload and account clearing are covered. The composed page has one primary heading and a direct task entrypoint. Projects recovery renders the complete saved query. Confirmed native save with unavailable task refresh retains the original receipt and suppresses false empty counts. Current-source synthetic browser fixtures cover editor, review, selected results with expanded filters, unblocked source, refusal, archive, uncertainty, unavailable refresh and both page entrypoints. Expanded shared source controls are captured separately to detect enlarged-text collisions. Rendered geometry/theme evidence remains separate from component event dispatch, hosted clients, physical devices and live changes.

## Habitica correspondence audit qualification

WorkspaceHabiticaCoveragePolicyTests cover every task kind, full child, completed archive, retained identity, missing/pending/uncertain mapping and all six organization kinds without mutating canonical data. TodoWorkspacePresentationTests checks cache reuse, updated coverage after save and workspace isolation. TodoTaskPanelTests checks missing-task and missing-tag warnings, zero incidental cloud acceptance and persistence of Urgent separately from game difficulty. Correspondence reporting does not qualify automatic mapping, upstream tag support, historical reward backfill or legacy source retirement; the five integration P0 entries retain the unresolved behavior.

Run current solution and focused panel/presentation checks for correspondence warnings, plus the packaged SDK/schema controls with synthetic accounts. Local rendered fixtures use production styles across 360/390/430/768/1024/1200-pixel widths, six deterministic schemes and 100/200-percent text. Structured measurements and source fingerprints reside in projection-policy-audit.json. Physical devices, assistive technologies, installed-host skill updates and new live effects remain outside the audit evidence.

## Projection-only repair coverage

WorkspaceCommandTests and projection-repair.test.mjs consume projection-command-v1.json to preserve cross-runtime identity. Synthetic tests cover patch-free accepted intent, unchanged content, independent native identity, create-only effects, pending/unknown original recovery, unsupported inactive/historical creation, stale revision and corrupt effects. SDK/PWA task-api tests exercise ensure_task_projection and original prepared/accepted receipts. Retained-record adoption covers archived/completed Todos and recurring kinds, matching completion state, exact fingerprint and identity, original revision recovery, tenant/access-epoch checks and absent candidates. .NET HTTP tests validate retained previews and refuse incompatible state/kind or unrelated receipt revisions. No live credentials or account effects are used.

## Habit counterpart qualification

Shared habit-commands-v9.json cases cover all Boolean direction combinations across C# and TypeScript. Application/Worker tests qualify create-only intent, effective defaults, direction refusal, unsupported fields and legacy unchanged-Habit repair. Synthetic SDK/private-gateway tests verify actual type habit creation, ownership, returned fields, later positive/negative scoring, original create uncertainty, explicit positive reconciliation without another POST, cross-profile denial and revoked consent. Workspace SDK schemas advertise optional Boolean directions.

TodoTaskPanelTests captures Habit creation review and a long Habit row with an unavailable direction. Rendered qualification spans six responsive widths, six themes and 100/200-percent text, with seeded overflow/overlap/contrast controls. Component tests establish handlers and disabled controls; static fixtures establish rendering. Physical devices, assistive technology and live game effects remain unqualified.


## Daily counterpart qualification

daily-projection-v1.json is consumed by C# and Worker tests for interval days, unordered weekdays, Monday multiweek anchors, month gaps, account offset conversion and explicit unsupported cases. Daily creation tests verify one typed create effect, no historical activity, complete schedule fingerprints, original-alias matching, changed-schedule refusal and later due-day scoring. Synthetic Auth/Workspace integration covers success, lost response recovery and expired account observations without real credentials or Cron calls.

Rendered fixtures cover the direct daily/weekly/monthly capture form, Daily review and advanced-panel review across six widths, six themes and 100/200-percent text. Component handlers separately cover missing dates, unsupported zones and retained draft input. Rendered browser evidence does not qualify physical devices, assistive technology, actual Habitica mutation, travel or DST. Earlier native recurrence tests use explicit historical fixtures where native-only lifecycle behavior is required.


## Task-link recovery qualification

TodoTaskLinkTests exercises exact unchanged linking, checkbox-only intent, definitive rejection correction/cancellation, uncertainty surviving later refusals, expired known previews, idempotent adoption recovery, account/source/type fences, completed/archived History entrypoints and confirmed adoption with failed refresh. The advanced panel verifies enabled original-link recovery outside disabled fields. Transport tests distinguish timeout/unexpected/malformed responses from known refusal and validate normalized Daily schedule metadata.

Synthetic rendered states cover create/adopt choice, exact counterpart review, definitive preparation rejection, active/historical adoption comparison, uncertain adoption and advanced-panel recovery. Qualification uses documented widths, themes and enlarged text with seeded layout/contrast failures. Component/transport evidence remains separate from actual account writes, physical devices and host refresh.


## Required task defaults qualification

task-projection-defaults-v1.json supplies nineteen shared C#/Worker cases for creation, field edits and activity across all kinds, native metadata exceptions, supported/adopted Daily schedules, historical refusal and retained mapping fences. API tests verify omitted/false/true Habit activity, exact legacy receipt recovery, paused execution with zero upstream writes and refusal of unscheduled Daily activity until a complete native schedule is reviewed. Retained lifecycle/archive tests seed historical commands rather than creating new reward-free activity.

Browser tests cover required title-first capture, explicit readiness states, creation-only Habit direction configuration, independent urgency/difficulty, exact affected-task review, Today activity and full-child capture. Package source and advertised MCP compatibility descriptions are checked separately from installed host copies. Current rendered qualification covers changed capture/status/review states across documented widths, themes and text enlargement; physical devices, assistive technology and real account execution remain separate.

An unaccepted stored preview superseded by current required-projection policy remains recoverable with original command bytes and an expired marker. Fresh acceptance remains refused; cancellation and a newly reviewed request can proceed after the original preview is known. Existing accepted receipts bypass prospective policy and remain unchanged. Synthetic API and browser expiry cases cover the recovery boundary.


## Tag ledger qualification

The shared tag-projection-v1 fixture exercises all six group kinds with same-name collisions, pending/confirmed/unknown states, archived/removed metadata, explicit membership removal, empty/partially confirmed membership, per-member original receipts, historical target IDs, stale source revisions and old exports. Negative cases cover duplicate tag/group/entity/member IDs, wrong aliases/kinds, invalid owner, malformed evidence, missing collections, unsupported state and confirmed records without original evidence or remote identity. Both C# and Worker parsers consume the same cases and round-trip full journals without game commands/events.

Coverage tests distinguish exact expected owner, foreign/missing owner, stored identity, pending/review counts and stale or changed task membership. Domain and application tests retain accepted organization hashes/receipts across additive ledger storage, logical removal, export, paused restore and frozen handoff. Model fixtures establish no native-to-Habitica execution or actual account mutation. Layout/styles are unchanged; dedicated tag UI and live/device checks remain separate.


## Tag gateway qualification

Auth tag-gateway.test.mjs uses synthetic credentials, OAuth grants, Miniflare private bindings and migration 0009. Cases cover separate owner consent, old/new scopes, authoritative identity flags, inaccessible public gateway paths, exact create/adopt/rename, same-name native tags, excluded challenge/group tags, owned/foreign/party/challenge tasks, preserved unrelated membership, idempotent receipts and no-op membership verification. Credential context, expired checks, revoked consent, adaptive admission and shared leases refuse before dispatch.

Persistence controls verify no send after reservation failure and original unknown fencing after acknowledgement storage failure. Unknown tag effects fence task writes and unknown gameplay fences new tag work. Explicit recovery reads before review, keeps the original attempt identity, distinguishes reviewed observations from original HTTP receipts, refuses absence/name/payload/permission mismatches and never resends metadata writes. Disconnect cleanup retains remote tags. Fixture text, account and token values are synthetic; no real Habitica account is contacted.

Synthetic verified-scope HTML is captured only when HABITICA_AUTH_BROWSER_EVIDENCE names a temporary evidence directory. Standalone Auth page checks cover supported responsive widths, actual enlarged text, normal contrast, controls and visible keyboard focus. PWA theme preferences are not transmitted to Auth; host OAuth behavior, physical devices and assistive technologies remain separate qualification. The existing solution tests/build and affected Worker suites still gate source completion.

## Tag reconciliation qualification

The tag-membership, tag-planner and tag-reconciliation Worker suites cover all six organization kinds, cross-project full children, category overrides, explicit goals, 237-match and 205-unblocked selections, empty/dynamic views and mapping filters. Synthetic D1 and Auth fixtures check atomic acceptance failure, scope/consent review gaps, preview-bound epochs, original receipt recovery, concurrent wakes, changed source during inspection, partial member confirmation, missing counterparts and missing registry identity. Native external tags survive membership removal; archive keeps provenance. No test dispatches live Habitica operations.

Run affected Worker dry builds before integration suites. On constrained hosts, run the complete Workspace and Auth test files with node --test-concurrency=2 while retaining the 30000 ms per-file deadline. Do not run the Worker suites concurrently with .NET or other Worker suites. The unrestricted parallel run is separate evidence and cannot be reported as passing when a file times out. Required UI, device, live enrollment and release qualification remain separate.


## Group-tag controls qualification

Synthetic group-tag API fixtures cover explicit consent, old/new agent scopes, exact duplicate-name adoption, complete reconciliation, paused unknown-receipt recovery, observed recovery without resend, stale/expired/changed-consent refusal, atomic storage rollback and bounded review retention. Tag-outcome fixtures additionally hold acceptance after preconditions and race terminal closure, preserve accepted originals, inject terminal storage failure, retain compact expired identities, recover through the actual MCP SDK and refuse non-acceptance claims for missing records or work without receipts. Typed browser and component controls require durable terminal proof before clearing sticky uncertainty. Pure status fixtures cover complete retained task counts, pagination, missing identity and stale membership. Tool schema fixtures verify additive scopes and the absence of account-selector proxies.

Browser component and adapter tests cover explicit consent, full membership-page acknowledgement, new-command acknowledgement reset, exact native IDs, uncertain acceptance and original-ID retry/recovery, strict wire counts, late workspace/session changes and the actual Todo Sync route. Rendered synthetic Razor fixtures use matching published styles under the shared responsive/text-scale/theme geometry and normal-scheme contrast matrix, with seeded detector controls. Synthetic observations do not qualify actual OAuth hosts, devices, screen readers, production dispatch or live accounts.


## Checklist conversion qualification

Neutral checklist-conversion reuse/create fixtures are produced from C# acceptance and consumed by Worker command hashing, deterministic internal IDs, parser, engine and journal tests. The ten-item scenario keeps eight active/two archived targets, duplicate names, rich UTF-8 source notes, independent target notes/urgency, intake checkmarks, unchanged completion events and historical-time uncertainty. Creation fixtures capture eight active targets while two historical gaps remain deferred. Resume, original acceptance, source edits, wrong ownership, identity rebinding and forged ledger evidence receive explicit checks.

Synthetic Workspace API tests cover stable request/command identity, complete preview, atomic storage rollback, partial capture restart, retained accepted preparation bindings, stale/expired reviews, separate grants, refused selectors and complete consent-bound project tag intent. No fixture calls a real Habitica account. Matching Worker dry builds, Release application compilation and affected/full regression suites gate completion. Browser conversion forms have local synthetic qualification; actual native verification remains separate.


Browser conversion qualification uses the same portable engine fixtures through typed wire, controller and Razor tests. Tests exercise ten-item duplicate-name selection, independent notes/urgency, explicit active/archive choices, source edits, missing historical matches, preparation acknowledgement loss, route remount and original acceptance recovery. Wire controls reject changed source/bindings, mismatched receipts, unsupported retirement claims and late account responses. Rendered review, refusal, preparation recovery and saved/partial status states use matching published styles under the common responsive/theme/text matrix and seeded detector controls. Physical devices, assistive technology, hosted OAuth and live native execution remain unqualified.


## Converted source retirement qualification

WorkspaceConversionRetirementTests and neutral C#/Worker fixtures verify unchanged source/context, complete target mapping/lifecycle/project membership, account identity, pending effects, relationship refusals, immutable archival receipts and later independent target work. Worker source-retirement-api and Auth retirement-gateway tests exercise fresh target content/completion/ownership/tag mismatches, exact irreversible approval, manual-only dispatch, expired reviews, consent/grant denial, durable reservation failure, definitive refusal, already-absent source, timeout fences, original lookup, explicit observed absence and canonical acknowledgement loss. Source/target notes, completion history, tags and accepted commands remain preserved.

Typed browser tests cover context hash and exact wire identity, changed payload rejection, sanitized diagnostics, late account changes, independent every-page and irreversible acknowledgement, unresolved preparation, bounded verification, navigation, original recovery, expiry and distinct absent-source archival. Rendered draft, verification, complete review, uncertainty, expiry, refusal and confirmed-history states use matching published assets under the shared width/theme/text matrix and seeded detector controls. Hosted clients, physical devices, assistive technologies, real Habitica mutation and remote migration remain separate evidence.


## Foreground bulk-spell outcome qualification

Controller fixtures exercise seventeen acknowledged casts out of forty, ten acknowledged casts followed by account refresh failure, known rate refusal, cancelled/lost dispatched acknowledgement, cancellation before the next cast, partial equipment refusal, restoration refusal without toggle replay and uncertain equipment without automatic cleanup. Assertions retain original counts, stage, cooldown, stale state and redacted diagnostic fields. HTTP fixtures exercise malformed success, transport loss, queued versus dispatched cancellation, shared cooldown with concurrent read and nondefinitive server errors through the existing adaptive pacer and synthetic time.

SpellsPage result fixtures cover partial, unknown, cleanup failure, refresh failure, preparation cancellation and uncertain equipment states. Rendered qualification uses shared responsive widths, six schemes and normal/enlarged text with seeded detector controls. Component handlers and synthetic server replies remain separate from production, physical devices, assistive technologies and actual spell effects. No live spell test is required or performed for local acceptance.

### Spell recovery qualification

Synthetic controller controls cover account isolation, later selections, explicit supersession, original-state verification, successful restore, lost equip acknowledgement, cancellation after the final cast, refused/unknown manual restore, stale review identity and cancelled manual restore. Browser storage controls exercise real IndexedDB revision exclusion, immutable originals, scope isolation, reload persistence and Web Locks exclusion/release across tabs. Three recovery fixtures cover long identifiers across six themes, six widths and both normal/enlarged text. Physical-device and live-account equipment behavior require separate qualification.


## Project alias qualification

project-alias-v1.json supplies shared C#/Worker raw-command hashes and a nine-step create/rename/archive/clear/reuse/remove/clear sequence. WorkspaceProjectAliasTests and project-alias.test.mjs cover normalization, malformed/empty/oversized values, duplicate imports, archived reservation, stale revisions, independent workspaces, unchanged journals, marker refusal and name/alias ambiguity. organization-api.test.mjs exercises actual MCP schemas, authenticated resolution, alias-bearing preparation/receipt recovery, competing previews, archived clear and export preservation with synthetic upstream responses. WorkspaceOrganizationClientTests binds normalized preview values and retained clear snapshots and rejects altered aliases. Existing organization hash fixtures retain legacy encoding checks. Publication, real-account effects and browser alias-editor presentation remain separate qualification.


## Browser alias qualification

TodoProjectAliasTests qualifies optional create, separate alias/name edits, clear, invalid/duplicate values, stale drafts, source changes, original preview/receipt recovery and exact GUID placement from duplicate-name alias labels. Maximum-length archived alias references remain selected across picker pages. TodoHierarchyPanelTests and TodoSavedListPanelTests include alias-bearing project navigation and saved filters. WorkspaceChecklistConversionPanelTests verifies exact alias-labelled destination selection without preparing a project copy. Synthetic Razor fixtures cover project forms, reviews, uncertain acceptance, retained selection, planning, saved lists and conversion destinations. MCP organization fixtures qualify shorthand resolution, name/alias ambiguity and exact reviewed placement with separate command receipts. Rendered qualification uses current published assets and deterministic themes; physical devices, assistive technology, actual host reasoning and live project cleanup remain unqualified.


## Multilingual deadline contract qualification

deadline-language-v1.json contains seventeen resolved and ten clarification expectations with Russian/English prompts, explicit timezone/current-date context, exact task/request identities and ISO mutation arguments. Calendar controls reuse the existing civil-date helpers for today, tomorrow, ten days, year omission, December/leap rollover and DST near midnight. Spring/fall controls demonstrate differing results from adding fixed twenty-four-hour intervals to an instant. Invalid relative/numeric/leap strings remain rejected by canonical command validation.

The synthetic MCP fixture applies every resolved case to one existing Todo, verifies omitted text/notes/priority/plannedDate preservation, required correspondence flags and original receipts, then recovers an earlier accepted request after later edits without duplicating tasks or changing the latest deadline. Plugin/schema and ordinary task API regressions retain scope and transport checks. Clarification rows describe required host decisions, not an executed language parser. No fresh-chat ChatGPT/Codex recognition, notification delivery, live account or publication is qualified.

### Connector identity controls

Identity controls cover coherent/mixed/missing Worker metadata, reported package ranges, unknown installation identity, schema incompatibility, invalid selectors and response-field stripping. OAuth/MCP fixture coverage verifies profile-only discovery without cloud workspace consent, no Habitica request, no task command creation and unauthorized public refusal. Deterministic generator controls reject stale source metadata and unexpected reviewed input fields. Actual installed ChatGPT/Codex package discovery remains unqualified.

### Package release controls

Seven Python controls exercise deterministic archives, changed content under an unchanged semantic version, stale payload/descriptors, archive-byte tampering, symlink/credential refusal and explicit release requirements. Deployment preparation includes package/receipt checks. Portable plugin/MCP schemas validate staged artifacts; SDK fixtures check actual advertised tools and direct-MCP unknown installation. Skill validators cover all five entrypoints. Fresh-chat descriptor loading, stale host-cache replacement and imported-package installation remain unqualified host observations.

### Local Notes evidence

Notes controls exercise Unicode/CRLF/code fences, source-size bounds, malformed Unicode, exact partial updates/appends, bounded causal history, original command recovery, stale revisions, trash/restore/purge, reserved deletion capacity and lossless account-bound backup conflicts. Storage adapter checks include owner mismatch, stale writes and explicit clear. Session controls verify account switch/sign-out draft clearing.

The disposable browser fixture upgrades a version1 database to version3, preserves unrelated key/value data, checks concurrent Notes revision exclusion and immutable receipts, isolates scopes, verifies exact text/history after reload and clears only Notes. The fixture uses synthetic records without credentials or network account calls. The Notes page has the synthetic evidence below; actual hosted agent access remains separate qualification.

### Cloud Notes qualification

Portable Notes fixtures compare exact C#/Worker command hashes and accepted journals through create, partial update, append, trash, restore and purge. The fixtures include Unicode, every ASCII code point, CRLF, code fences, quotes and trailing backslashes. Miniflare controls use synthetic accounts and the real Auth/Workspace bindings to verify separate consent, isolated account/session ownership, task-export exclusion, old/read-only/revoked grants, original receipts after later edits, concurrent writes, atomic rollback, bounded search, deletion and delayed epoch races. A failed cross-service deletion blocks access and reenrollment until original cleanup succeeds.

Typed browser-client controls verify explicit Notes consent, no automatic upload, exact receipt/hash recovery after lost acknowledgement, foreign export refusal, malformed receipt refusal, unsafe endpoint refusal and late-session response rejection. Ordinary tests use no live credentials. Actual host tool discovery, physical-device behavior and remote deployment remain separate qualification; synthetic Notes page interaction is recorded below.

### Notes agent contract controls

The actual MCP SDK exercises advertised Notes schemas, separate scopes, exact clothing measurements and family/birthday retrieval, full Markdown preservation, partial edits/appends and verified replacement ranges. Ambiguous duplicate titles expose multiple exact IDs; unknown target selectors fail. Malicious note text remains returned data with no automatic dispatch. Preview before/after values expose conflicting facts without changing stored content before acceptance. The Notes SDK controls qualify server/tool behavior and packaged guidance, not an actual model decision.

Response-loss injection forwards real acceptance before withholding the acknowledgement; original receipt/preparation recovery and repeated acceptance preserve one append. Cross-profile, stale, expired and revoked previews refuse. Scheduled expiry and exact purge remove pending body copies. Package validation covers all six skills and generated identity descriptors. Real ChatGPT/Codex discovery, fresh-chat invocation and interpretation remain unqualified.


### Notes page qualification

NotesControllerTests, NotesPanelTests, NotesMarkdownRendererTests and NotesDiagnosticsTests verify exact Unicode/CRLF Markdown, .md import/export, safe structured preview, no remote image/executable links, conflict preservation, explicit discard/cancel, separate cloud consent, unavailable reads, source/account changes, retained versions, reviewed trash/restore/purge and original receipt recovery without duplicate writes. AppSessionController Notes controls verify physical forgetting of tab credentials on account switch and sign-out. Diagnostic operations retain outcomes and command identity without title/body content.

The matching-source solution passes1168 tests; final focused Notes controls pass35 tests after the final layout adjustment. Release publication succeeds. Rendered qualification covers ten Notes/privacy/navigation fixtures across360/390/430/768/1024/1200-pixel widths, six deterministic themes and100/200-percent text:720 cases, zero geometry/contrast/primary-target errors and no unsupported surfaces. Two seeded cases detect overflow/overlap and contrast failures. An initial muted-version contrast failure in Toxic Swamp is corrected through the shared readable token. Native keyboard traversal confirms a visible three-pixel focus outline. Evidence resides in .skill-tree/history/validation/notes-ui/result.json. Physical devices, assistive technologies and real hosted Notes enrollment remain unqualified.

### Project Notes qualification

NotesProjectPanelTests exercises browser/cloud create-in-project, attach/move/detach reviews, exact duplicate-name identities, retained association versions, unchanged Unicode/CRLF Markdown, combined text/trash/project/unassigned pages beyond twenty results, stale lifecycle labels, unavailable Workspace filters, private drafts across hiding/navigation, account changes, stale reviews, original acknowledgement-loss recovery and separate cloud consent. Archived project context disables creation while retaining existing Notes. Project-only access renders no private browser title or snippet. Existing Notes, hierarchy, organization and navigation regressions retain the original suite responsibilities.

The final local solution passes1441 tests; the final focused route/controller selection passes74 tests and Release publication succeeds. Rendered qualification covers33 actual Razor fixtures on /notes and /todo/projects across360/390/430/768/1024/1200-pixel widths, Gryphy Light, Gryphy Dark and Obsidian Glow, at100/200-percent text:1188 cases with zero geometry, contrast, primary-target or harness errors. The lowest measured enabled-text contrast is5.12. Seeded geometry/contrast controls detect overflow, overlap and unreadable text. Native Tab traversal in the200-percent Obsidian Glow fixture exposes a visible three-pixel focus outline. Paginated library rows grow with long titles rather than clipping controls inside a short nested scroll region. Compact source/evidence resides in .skill-tree/history/validation/notes-project-ui/result.json; large synthetic fixtures and logs reside in ignored .skill-tree/artifacts/notes-project-ui. Real private Notes, hosted enrollment, installed host behavior, physical devices and assistive technologies remain unqualified.

### Dedicated statistics controls

TodoStatisticsTests verify restore/recomplete cycles, more than one hundred Habit events without page truncation, independent positive/negative counts, requested versus confirmed effect prefixes, current project/goal denominators, archive retention, uncertain conversion dates, per-task value deltas, missing segments, inclusive UTC ticks, leap/year boundaries and invalid sources/filters. Eight Application controls pass. Twenty-one focused Web controls cover the dedicated filters, source separation, unavailable/account-change states, paged long-name progress and existing Tasks interactions after aggregate removal. Rendered statistics and expanded legacy detail fixtures use the shared responsive/theme harness; evidence resides in .skill-tree/history/validation/todo-statistics/result.json. Hosted use, physical devices and assistive technologies remain separate qualification.

## Todo Sync composition

Synthetic component checks cover shared source selection, distinct Workspace consent and executor controls, no write on route entry, an exact original cloud receipt, late account replacement, and missing browser receipt without retry. Existing Workspace settings and tag checks continue to cover status refresh, unavailable access, separate consent, local/cloud backup, transfer cancellation and unknown original metadata. A cancelable browser-clear review names Notes and canonical workspace backups. Rendered route layout checks cover responsive and theme geometry; physical device, real OAuth grants and production account migration remain separate qualification.

### Todo category navigation checks

`AppNavMenuTests` verify the permanent nested menu, every working destination, selected-route state and signed-out absence. `TodoCategoryPageTests` render Habits, Dailies, Tasks and Today with one shared synthetic workspace; kind filters, immediate category capture, score/complete controls, hierarchy and the selected-day create link are present without preparation or acceptance. Existing Today and hierarchy interaction suites retain exact original-receipt and date behavior. Rendered fixtures cover the new menu and four category pages together with Projects, Statistics and Sync across supported viewport, theme and text settings. Browser fixtures are synthetic; hosted enrollment, physical-device touch and assistive-technology behavior remain separate checks.

Todo navigation checks require matching Release assets, current component fixtures and the shared rendered matrix. Inspect category-label wrapping manually at 360px and 200-percent text in readable light and `obsidian-glow`, alongside geometry, contrast and seeded detector controls. Source-bound results reside in `.skill-tree/history/validation/obsidian-glow/result.json` and `.skill-tree/history/validation/todo-navigation/result.json`; the recorded results supply no current physical-device, production-session or assistive-technology qualification.

## Companion discovery metadata qualification

The plugin SDK fixture consumes discovery-prompts-v1.json, lists actual built MCP tools and checks expected schemas, scopes and read/write annotations without executing prompts. Direct, indirect, negative and follow-up scenarios run as deterministic metadata checks. The [host evaluation contract](companion-discovery.md) separately requires new/existing chats, explicit/automatic invocation, observed tool availability, sanitized arguments, permission behavior and results. Unrun host cells remain unrun; SDK discovery never supplies model precision or recall. Package schema and deterministic artifact checks remain applicable.

## Web Push subscription foundation

Run Auth push.test.mjs after matching Worker builds; the suite applies migration0010 and uses synthetic accounts, locally generated keys and intercepted outbound services. tests/Functions/pwa-push.test.mjs exercises production browser logic against explicit PushManager/Notification fixtures. PushSubscriptionTests and PwaPushBrowserTests verify bounded management transport, original receipt identity, cancellation, stale response refusal and preparation without permission. dev-preparation.test.mjs keeps both standard capability profiles from enabling separately provisioned Web Push. Evidence resides in history/validation/push-subscriptions/result.json. No test sends to a real provider or proves physical iPhone closed-app delivery; the device release gate remains open.

## Voluntary review qualification

WorkspaceTaskReviewTests consumes task-review-command-v10.json and exercises every action against all projection mapping states. Worker task-review.test.mjs checks matching model behavior, portable journal rejection, independent legacy hashes and authenticated SDK tools without real network/game effects. TodoReviewEditorTests and existing TodoTaskWorkflowTests export six new review states from the actual Razor flow and verify validation, original uncertain receipts, manual flags and stale/account fencing. Rendered geometry, target, focus and contrast checks remain separate from component interactions. Evidence and device limitations reside in history/validation/task-reviews/result.json.

## Qualification startup phases

The browser matrix records navigation, theme, fonts, animation-frame readiness, finite-animation readiness and measurement phases against a unique UUID case identity. Results must also match fixture, theme, width, text size and seed. Late, stale, malformed and terminal messages cannot supply a measured result. Timer cleanup cancels replaced and completed cases. The original overall case deadline remains five seconds, including all prerequisites and measurement; the complete run remains bounded to300seconds. Startup expiry is prerequisite_wait/unavailable, while expiry after measurement begins is executed_control/unavailable. Missing measurements never become passing geometry or contrast evidence.

The reproduced geometry-seed delay reaches finite CSS-transition readiness within about60milliseconds, then stalls on running250-millisecond MudBlazor button transitions. The static snapshot profile explicitly finishes running CSS transitions with playback rate1 and finite end time at most250milliseconds, using the transition final values before measurement. The profile retains natural bounded waits for other animations. No production stylesheet or animation behavior changes. Static snapshots qualify final geometry/contrast only; animation progression, intermediate paint and physical rendering remain unqualified. Bounded animation metadata retains at most twelve class/timing records without task text.

QualificationCase/QualificationTimer fixtures cover exact identity, stale/late messages, terminal states, early timer wakes, cleanup, unchanged overall deadlines, unavailable states and restricted transition settling. Fresh-origin and warm browser controls reproduce seeded overflow/overlap and contrast detections with retained failed attempts. Evidence resides in history/validation/qualification-startup/result.json; task-reviews/browser.json retains the matching positive matrix. Local server expiration is an independent prerequisite and grants no case pass.

## Foreground reminder controls

Run tests/Functions/foreground-reminders.test.mjs for fake-clock production scheduling and serialized production storage transitions, alongside existing gear-recovery-storage and pwa-push controls. WorkspaceReminderCuesTests checks canonical intent and stable schedule identity; TodoReminderPanelTests covers separate opt-ins, due/future visibility, denied/unavailable/uncertain states and account callback fences. App session browser clearing includes the reminder store. tests/browser-qualification/reminder-storage.html is a disposable-origin real IndexedDB transaction control using fake notification APIs. Captured Today fixtures require normal/enlarged text in light/dark including Obsidian across phone/desktop widths, detector negatives and visible focus. The reminder fixtures do not qualify actual OS delivery, permission prompts, assistive technology or physical devices.

## Scheduled push controls

reminder-calendar.test.mjs checks explicit civil/DST and bounded recurrence semantics. reminder-api.test.mjs exercises actual D1/browser API and authenticated SDK preparation/acceptance, scope/account refusal, exact original receipts, duplicate cron wakes, changed/cancelled intents, profile/Workspace lifecycle, expired cues, bounded retry/capacity and retained attempt histories with fake clocks. Auth push-dispatch.test.mjs independently decrypts payloads/verifies VAPID through Node crypto and checks durable quota/unknown/redirect/provider-removal behavior through the private service. A Miniflare outbound proxy normalizes Request.redirect metadata; redirect qualification therefore asserts one original destination, returned3xx refusal and no followed request. Local synthetic controls do not establish real closed-app delivery, OS permission, iPhone behavior, hosted agent adherence or publication.

Push setup qualification covers device gesture/consent, denied/unsupported/install states, one-time/repeat edits, cancellation, uncertain original results, fresh authenticated notification open and safe sign-in return. Real IndexedDB helper controls and multi-theme rendered fixtures supplement C#/Node assertions. Physical iPhone closed-app delivery and real provider calls remain unrun; see [push-reminder-ui.md](push-reminder-ui.md).

Ten offline MCP Events controls exercise authenticated filter ownership, challenge/signature, fake endpoint policy, expiry/revocation, dedupe, uncertain receipts and actual installed-SDK rejection of Events discovery. Synthetic host acceptance proves no rendered chat reply. Production DNS/TLS, encrypted storage and host/device qualification remain unrun; see [originating-chat-reminders.md](originating-chat-reminders.md).

Ordinary-task qualification combines a fifteen-case expected-guidance corpus, actual SDK create/edit and original-receipt journeys, existing task/scope/identity regressions and package integrity checks. Real host prompts and model activation remain unmeasured; see [ordinary-task-intent.md](ordinary-task-intent.md).

## Reproducible current task journeys

[task-journey-qualification.md](task-journey-qualification.md) documents an eight-journey, sixteen-state pack selected from fresh bUnit Razor captures. Current Todo routes replace historical prototype navigation. The local run records component TRX, source/assets, browser matrix, seeded detector controls and native focus/disclosure observations separately. Host/account/device/human requirements remain traceable and unrun; fixture results cannot close T-d76d2a9a-1318-4e63-9bb4-73f620b108cc.

## Native task recovery qualification

WorkspaceTaskRecoveryTests and task-recovery.test.mjs cover independently encoded v11 hashes, unarchive across Todo/Daily/Habit states, completed/skipped Daily cycles, exact original snapshot retention, concurrent/stale/duplicate/foreign requests, archival-before-correction, explicit reference repair, retained game fences, unavailable source receipts, portable imports and zero-effect receipts. A neutral Worker journal validates again in C# with the same source command identities. WorkspaceCloudClient recovery tests refuse altered targets and any scoring receipt. The synthetic MCP fixture discovers all three recovery tools, exercises original preparation/acceptance/receipt recovery, scoped-write denial and cross-account isolation, and observes zero upstream task/tag writes. Local evidence resides in history/validation/native-task-recovery/result.json. Current History UI, real Habitica unchecking, hosted clients and devices remain separate qualification.

## Private Auth recovery gateway checks

task-recovery-gateway.test.mjs uses isolated synthetic Auth/Workspace bindings to exercise Todo/Daily review and down receipts, unknown network/HTTP/malformed outcomes, original recovery after token removal, target/context/checklist drift, foreign/shared/challenge copies, stale/prior-day/needsCron state, token operation scope, ordinary non-Habit down refusal, profile/grant revocation, lease replacement, quota exhaustion, reservation failure, receipt persistence failure and unresolved cross-tool work. Fixtures never call the real provider or Cron endpoint. Canonical/public linked recovery and native post-receipt history remain separate parent-task checks.

## Original score receipt source verification

Projection gateway fixtures verify exact canonical source binding, same-attempt recovery, wrong command/completion/hash/effect/target, foreign profile/connection, unknown/legacy receipts, malformed identity/date and Habit-context refusal. Lookup preserves upstream request counts. The executor Daily composition verifies the source through the retained dispatched attempt token after prepared_request_json cleanup. Canonical/public linked recovery remains a separate parent integration boundary.

## Linked task recovery checks

After generating build identity and building all three Worker bundles, run `node --experimental-strip-types --test --test-concurrency=1 --test-timeout=30000 workers/companion-workspace/test/linked-recovery-api.test.mjs` from the repository root. The synthetic suite composes genuine source scoring, reviewed recovery endpoints, original-receipt lookup, native-save rollback, durable unknown fencing, concurrency and refusal controls against synthetic providers only. `WorkspaceLinkedRecoveryTests` consumes neutral Todo/Daily journals; `WorkspaceCloudClientTests` validates the typed review and receipt. See [linked task recovery](linked-task-recovery.md) and retained evidence for boundaries.

## History recovery UI verification

`TodoExtendedRecoveryTests`, `TodoHistoryPanelTests`, `WorkspaceTaskControllerTests` and typed `WorkspaceCloudClientTests` cover native unarchive/correction, linked preview/accept/original receipt, lost responses, reopened identity discovery, account changes, filters/drafts and concrete refusal states. Capture current Razor fixtures with `HABITICA_BROWSER_EVIDENCE`, publish current assets locally and stage the history route through `tests/browser-qualification/prepare.py`. Measure the shared seven-theme, six-width, normal/enlarged-text matrix and seeded detectors; native keyboard interaction and Blazor component dispatch remain separate evidence. See [history recovery controls](history-recovery.md) and `.skill-tree/history/validation/history-recovery/result.json`.

## Retained recovery discovery checks

`workers/companion-workspace/test/recovery-records.test.mjs` covers bounded records, source/receipt provenance, stable paging and refusal contexts. `recovery-records-api.test.mjs` exercises actual SDK and browser reads, scope/profile isolation, canonical fences and zero provider/mutation behavior. Build all three matching Worker bundles before composition tests. See [retained recovery discovery](retained-recovery-discovery.md).

## Causal task activity coverage

`WorkspaceActivityCoverageTests` and `activity-coverage.test.mjs` qualify new known segments, legacy unknown prefixes, deleted witnesses/revisions, metadata-only revision gaps, unobserved task transitions, recurrence/timezone edits, pause/resume, archive/unarchive/restoration, original correction records, lost scores/terminal sources, export/import and clock rollback. Both runtimes consume `activity-coverage-v1.json`; document-only round-trips preserve witnesses while full-journal reads validate the causal sources. Query endpoints do not alter persisted evidence. Current evidence resides in `.skill-tree/history/validation/task-activity-coverage/`. No UI route changes, real provider/account/device operations or publication are included.

## Durable exact-adoption qualification

adoption-api.test.mjs covers synthetic two-profile HTTP/D1 and MCP SDK identity, retained completed/archived records, malformed targets, request reuse, expiry, stale native/canonical state, epochs, pending/unknown effects, shared lease, atomic receipt rollback, lost acknowledgements and immutable original recovery. workspace.test.mjs retains browser adoption and later game-intent regression coverage. WorkspaceCloudClient.ProjectionTests validates server-issued IDs, full receipt identity, original preview recovery and unknown versus terminal refusal. TodoTaskLinkTests checks original-review/source/account fences and proven non-acceptance cancellation. Native writes/scores remain zero in adoption fixtures. Current captured Tasks/History link states receive light/dark responsive geometry, contrast, focus, primary/disabled controls and 200-percent-text checks; host/device/assistive technology remain separate. Evidence: .skill-tree/history/validation/task-adoption/.


## Scoped tag correspondence qualification

The shared conversion-retirement-prerequisites.json journal supplies C#/Worker parity for complete retained membership. Scoped status and retirement controls cover unrelated document/task/projection revisions, task metadata, exact group/lifecycle changes, native remaps, incomplete members, ambiguous intents and unknown evidence. Cloud API controls verify current connection/access-epoch provenance and zero dispatch from status reads. Retirement retains fresh native verification and separate irreversible approval. Portable stored evidence does not establish current service authority.


## Group-tag diagnostic operation qualification

WorkspaceCloudClient tag observers qualify eight fixed operation names, original command/correlation context, read/mutation modes and unknown-result preservation under synthetic transport failure. Diagnostics bundle controls round-trip the exact allowlist through retained storage, repeated sanitization and explicit shared-bundle parsing. Unknown names and private tag/task/credential canaries remain sanitized. No automatic upload, live account call or UI layout change is included.


## Shared diagnostic boundary qualification

DiagnosticSharingTests and diagnostic-protocol.test.mjs share diagnostic-sharing-v1.json for canonical UTF-8 bytes, hashes, UTC milliseconds and repeated sanitizer parity. Diagnostic API controls exercise independent consent/scopes, metadata-only preparation, atomic quota/receipt rollback, lost acknowledgements, revocation races, two profiles, expiry, old request refusal, cleanup failure, forged ownership/source fields, bounded filtered paging and actual SDK reads. Typed clients refuse altered identity/hash, raw failures and late account responses. Synthetic providers observe no diagnostic-driven calls or gameplay changes. Auth permission disclosure and remaining host/device qualification stay separate from local API evidence.

DiagnosticSharingPanelTests exercises explicit consent/preview/preparation/upload,100-entry omission counts, frozen navigation state, identity changes and late replies, lost/missing originals, no replacement/repeated upload, quota refusal, expiry, revoke/cleanup failure and manual fallback. Captured actual Razor markup covers both /diagnostics and /live-tests; rendered theme/width/text measurements supplement component interactions. Physical devices, assistive technology and live sensitive uploads remain unqualified.

Notes project qualification retains unchanged notes-wire-v1 and adds notes-project-wire-v2 for C#/Worker metadata bytes, hashes and exact source text. Synthetic D1/SDK cases cover ownership/source/lifecycle, complete-set paging, scope isolation, rollback and original recovery. Direct-source D1 controls hold acceptance before commit to change the project revision, and simulate lost commit acknowledgments. The isolated localhost browser harness validates actual IndexedDB cross-table project guards and cleanup of generated synthetic rows. Real Notes, devices and hosted migrations remain unqualified.

## MCP protocol migration qualification

The pinned server/core/client SDK2.4.0 and genuine legacy SDK1.32.1 fixtures exercise matching MCP/Auth/Workspace dry bundles. The Auth protocol fixture covers modern/legacy cases for discovery, automatic/pinned negotiation, complete tool/schema equality, interpreter-backed Worker schema validation, Notes acceptance with lost acknowledgement and cross-era original recovery, malformed/unsupported envelopes and standard headers, content types, audience/Origin/scopes/body bounds, concurrent owners and changed grant epochs. Workspace regressions and the retained v1 Events refusal controls require matching source/build inputs. Run all three Worker type checks and dry bundles with the pinned runtime and no unsafe-eval binding before claiming current local transport acceptance.

Compatibility discovery reports the modern revision explicitly because the SDK public SUPPORTED_PROTOCOL_VERSIONS constant contains legacy revisions only. The exact SDK/package lock, qualified protocol set, build hashes and independent installed-package uncertainty remain separate. Compact source/evidence resides in .skill-tree/history/validation/mcp2-migration/result.json. Hosted activation, callback subscriptions, real event delivery, remote migrations and device/host observation remain unqualified. [mcp-protocol-compatibility.md](mcp-protocol-compatibility.md) owns transport and rollback details.


## Callback egress candidate qualification

Build Auth through the existing Auth npm build script before running `node --experimental-strip-types --test --test-concurrency=1 --test-timeout=30000 workers/chatgpt-auth/test/callback-egress.test.mjs`. Run `npm test` from deploy/callback-egress on Node22. The gateway controls cover public/special/mixed address refusal, exact-byte grants, actual local TLS chain/hostname/address checks, redirects, byte limits, deadlines, one-use concurrency, acknowledgement loss and secret-free output. The Worker controls use actual synthetic Miniflare/D1 reservations and compose the production gateway with the claim route. Dev preparation must retain CALLBACK_EGRESS_ENABLED=false in both ordinary profiles.

Retain function bundling, Auth type/dry build, negative controls, source fingerprints and profile results under .skill-tree/history/validation/callback-egress-netlify. The [callback egress contract](callback-egress.md) and hosted profile require separate selected-account publication and owned DNS/TLS fixtures. Local loopback TLS and service documentation never establish Netlify DNS/TLS behavior, provider logging, cold starts or a rendered chat reply.

## Existing connection recovery controls

ConnectionRecoveryTests and the WebApp integration suite cover typed existing-only Auth/Workspace/Notes recovery, no-request consent defaults, safe errors, exact identities, expiry bounds and delayed response rejection. Auth recovery.test.mjs covers isolated D1/runtime preservation, independent sessions, legacy bootstrap, missing/revoked/unsupported stores, origin/descriptor/credential refusal, quota, expiry/rotation, OAuth grant observations, outage and concurrent access changes. Vault/management and Workspace/Notes regression suites retain initial-consent, revocation, original-receipt and record-preservation checks. [The recovery contract](session-recovery.md) defines validation scope; detailed results and operation deadlines reside in .skill-tree/history/validation/existing-connection-recovery/. No changed UI route, real account, hosted migration or device test is implied.

## Account preference restoration

`CompanionPreferencesTests` covers account isolation, secret rejection, merge conflicts, tombstones, legacy overrides, coalesced existing-only recovery, offline/revoked access, explicit legacy recovery, account switches and disconnect suppression. `tests/browser/companion-preferences.mjs` exercises real isolated IndexedDB concurrency, ambiguous/frozen stores, quota rollback and reload persistence. Evidence resides in `.skill-tree/history/validation/unified-workspace/`.

`workspace-transfer.test.mjs` exercises populated targets, multiple sources, Notes project rebinding/history, removed projects, trash/purge, identity and alias conflicts, stale versions/epochs, capacity, atomic rollback, original receipts, imported effect fences and retained Note erasure. D1-produced synthetic journals/Notes under `tests/fixtures/workspace-transfer/` pass application deserialization in `WorkspaceTransferTests`. The real IndexedDB fixture covers atomic two-store freeze/rollback and exact acknowledgement.

`workspace-transfer-mcp.test.mjs` uses the MCP SDK for discovery, independent migration/Notes scopes, owner capture prerequisites, cross-profile isolation, modified archive bytes, expiry, grant/consent changes and lost acceptance acknowledgement. OAuth catalogue tests disclose migration separately and retain only owner-selected scopes. Package build/verification checks actual source payload hashes without installing a host package.


### Unified Workspace consumer checks

The coordinated `UnifiedWorkspaceFlowTests` journey verifies browser capture, populated-target review, preserved task/Notes identities, conflict acknowledgement, dirty drafts, response loss, local acknowledgement loss and new-session recovery. Source-bound component, D1/MCP/Notes/build-identity and OAuth/vault/recovery results reside in `.skill-tree/history/validation/unified-workspace/`. The corresponding `rendered-matrix-summary.json` identifies measured light/dark, phone/desktop and normal/enlarged-text fixtures. Current acceptance requires matching sources and assets. Rendered checks do not establish a live private browser migration or physical-device qualification.

## Skill Tree Web profile

Concrete workflow recipes reside in `.skill-tree/installation/profile.json`; [installation state](skill-tree-installation.md) records static specialization and unavailable required lanes. Refresh uses the local manager after relevant toolchain/configuration changes. Current queue validation prose and acceptance remain authoritative; unknown hosted/browser/device checks are mandatory blockers, never substituted by generic static evidence. Workflow installation runs no product build, tests or live checks.

## Notification release qualification

`tests/Functions/subscription-contracts.py` performs serial affected Worker dry builds, actual local Auth/Workspace/MCP/D1 subscription controls, native gateway TLS/address controls and an independent signed receiver. `workers/companion-workspace/test/reminder-events.test.mjs` composes generic due events, current consent/resource gates, stable IDs, bounded retries,410/413, unknown receipts, lost acknowledgments and restart recovery. The OAuth Events permission render matrix covers built-in light/obsidian declarations at360/1280px with100/200percent root text, visible focus/disabled controls and contrast; it uses actual Worker-rendered HTML in a static local fixture and does not establish physical-device or live OAuth-host interaction. Normal hosted gateway transport is separate evidence; actual subscribed-chat response and dedicated hosted DNS/TLS negative fixtures remain unqualified. See [event-subscriptions.md](event-subscriptions.md) and [reminder-events.md](reminder-events.md).

## Local Notes folder qualification

Focused Notes application/storage suites cover schema2 compatibility, exact placement history, immutable receipts, source/account guards, capacity, empty-only removal and original-result recovery. tests/browser/notes-folders.mjs serves the actual IndexedDB adapter and the neutral notes-folder-wire-v3.json fixture on an ephemeral owned loopback origin. Isolated persistent Chromium/WebKit profiles cover upgrade, concurrent folder writes, transactional project-authority races, rollback, receipt retention and exact reload. The fixture closes owned contexts/server and removes temporary profiles. No real account, hosted service, permission gesture or physical device is exercised.

Cloud folder qualification uses the focused Notes owner API, existing project and agent regressions, and typed NotesCloudClient fixtures. Portable C#/Worker folder commands retain exact hashes; actual D1 controls cover folder-only journals, Unicode discovery/paging, membership history, receipt rollback, project races, original lost acknowledgments, expired sessions and notebook/profile cleanup. SQL write gates distinguish acceptance from read batches. The typed client rejects foreign/malformed folder metadata and omits absent first-page revision guards. Migration0021 remains local-only until a separately authorized publication.

The focused note-folder-tools.test.mjs suite uses actual SDK discovery and server-owned folder previews, including original request recovery after dropped prepare/accept responses. Negative controls distinguish an isError tool result from transport401/403 or unknown-tool refusal; a successful synthetic tool response must fail the refusal oracle. Pinned SDK discovery retains OAuth security metadata under _meta.securitySchemes. Existing Notes/project SDK regressions preserve legacy behavior. No actual host/model, real private content or browser automation is used as an agent workflow.

### Notes folder UI qualification

The Notes component filter covers standalone/project folder capture, rename, exact reviewed moves and detach, retained content/history, dirty drafts, duplicate labels, trashed members, stale project context, original unknown receipt recovery, late-account responses and replaced notebook refusal. Folder diagnostics exclude private names. Historical version drafts restore exact folder links.

The notes-rendered-browser recipe builds the current Release app, captures current Razor states and stages production styles through tests/browser-qualification/prepare.py. Matrix mode measures eight folder states on both routes across gryphy-light/obsidian-glow,360/1280 widths and100/200-percent text in Chromium/WebKit. Geometry, text contrast, marked primary actions, disabled controls, native keyboard focus and long-name wrapping are required. Production controls remain unchanged; the static matrix opens the Manage folders disclosure for measurement.

Journey mode runs actual Release Blazor routes with generated persistent profiles and real IndexedDB. Synthetic Habitica GET responses never leave the browser. Standalone/project folder capture, explicit association acceptance, exact Unicode search/reload and unsaved unfiled Markdown import qualify locally. Normal in-app Projects navigation preserves the authenticated session; a fresh-project login/startup path remains separate. Both browser modes bind an owned ephemeral loopback server to source/build fingerprints, enforce deadlines and remove generated profiles. Original logs and failed controls remain under notes-folders-ui validation history. Service workers are blocked; hosted OAuth, real cloud subscriptions, physical devices and assistive technologies remain unqualified.

### Coverage-aware activity day-series controls

The Application Activity/Streak filter exercises effective historical schedules, duplicate positive events, negative-only frequency, explicit skips, pauses, corrections, missing coverage, current-day grace, context paging and DST civil boundaries. The neutral activity-streaks-v1.json fixture contains twenty independently asserted full-journal examples. Pure Node controls compare complete portable outputs and reject malformed identities, stale contexts and fabricated witnesses. Actual SDK/local Worker/D1 controls additionally check tool annotations, task-read scope separation, exact active-workspace authority, two profiles and unchanged canonical documents. The first scoped MCP observation retains the existing grant-epoch metadata fence.

ActivityStreakPanel component controls exercise exact task selection, verified/lower-bound/unavailable labels, negative-only frequency, twenty-row paging, long names, invalid zones, private-account cleanup and no commands. The activity-rendered-browser recipe builds the current Release app and stages current Razor/production assets. Seven statistics states cover light/obsidian,360/1280 widths and100/200-percent text in Chromium/WebKit, with geometry, contrast, visible keyboard focus and existing chart values. Separate hydrated Release journeys read real isolated IndexedDB through the actual statistics route, explicit historical as-of, UTC/New_York conversion, invalid-zone refusal and unchanged journals. Synthetic provider GET replies never leave the browser; service workers are blocked. Hosted OAuth, installed-PWA behavior, physical devices and assistive technologies remain unqualified.

### Reviewed linked Daily schedules

Application schedule controls and twenty neutral examples cover lossless mappings, inactive default weekdays, month gaps, account day/zone differences, unsupported retained fields, exact native epochs, stale refusal and v12 command hashes. Actual local Auth controls check fresh external/account observation, original normalized receipts, unknown fencing and destructive ordinary monthly PUT refusal. SDK/Worker/D1 controls exercise the reviewed MCP operation, required write scope, exact original receipts, lost Auth-to-Workspace acknowledgement and retained unexpected normalization without scores or Cron. Existing recurrence controls preserve ordinary linked-edit refusal and prior wire hashes.

Component/typed-client controls cover fresh linked cloud review, unavailable browser/unknown/conflict controls, complete field/date disclosure, pending original receipts, lost acceptance, malformed/unrequested response refusal and immediate private-account cleanup. The linked-daily-rendered-browser recipe builds the current Release app, captures current Razor states and stages current production styles. Nine states cover gryphy-light/obsidian-glow,360/1280 widths and100/200-percent text in Chromium/WebKit. Actual hydrated `/todo/dailies` and `/todo/sync` journeys restore existing synthetic account/Workspace access, use real local Auth/Workspace Workers/D1 and native IndexedDB, review a fresh schedule and save one pending metadata command. Exact local CORS uses owned localhost5081; an occupied port must fail without disturbing another process. The fixture forbids provider writes and leaves the executor disabled. Cleanup closes all generated Workers, profiles and the owned server. Service-worker lifecycle, hosted publication, real accounts/devices and assistive technologies remain unqualified. Original check receipts and failures remain under linked-daily-schedules validation history.

### Native task type copies

`TaskTypeConversionPolicyTests` shares six independently generated kind-pair/hash examples with `task-type-copy.test.mjs`. Native/Worker journals validate reset fields, stale revisions, shallow immutable lineage and portable transfer. Actual local SDK/D1 tests verify original prepared reviews/receipts, zero provider/tag effects and private scope/profile fences. `TaskTypeConversionPanelTests` and typed transport controls cover explicit review, Daily inputs, browser atomic saves, unknown recovery and account cleanup, alongside existing checklist regression controls. `task-type-copy-rendered-browser` publishes current Release assets locally, captures current Razor states and validates light/obsidian360/1280px100/200-percent layouts and focus in Chromium/WebKit. Hydrated conversion/Sync navigation uses actual isolated Auth/Workspace Workers/D1 and native IndexedDB. No hosted, real-account, device, assistive-technology or service-worker qualification follows from local evidence. Details: [native task copies](task-type-copies.md).

### Relative saved-list dates

`RelativeSavedTaskViewTests` and `relative-saved-dates.test.mjs` share sixteen independent civil-calendar examples plus a separately constructed v5 hash. Current native, actual SDK/Worker/D1, typed/component and retained tag-membership/reconciliation controls cover date selection, rollover, preserved dependency context, unsupported input and original identity. Relative group projection remains explicitly unavailable; a conversion control proves old queued tag work cannot dispatch or clear upstream membership. `relative-saved-rendered-browser` publishes current assets only to an owned loopback server and checks Lists/Sync in Chromium/WebKit with light/obsidian phone/desktop normal/enlarged text, focus and disabled controls. Actual local recovery, durable relative intent and native IndexedDB are exercised without real accounts or provider writes. Physical/assistive-technology, hosted and service-worker paths remain separate. Details: [relative saved dates](relative-saved-dates.md).

## Project-filtered retained history

Project-retained history uses23 neutral current-versus-accepted-placement examples, focused native/component suites, actual SDK/isolated D1 reads and the `project-history-rendered-browser` recipe. Current Release History/Projects qualification covers160 light/obsidian phone/desktop normal/enlarged-text state cases plus two hydrated scoped-navigation/lost-acknowledgement original-receipt journeys. Original events remain retained with one restore acceptance and zero provider writes. Evidence and unqualified paths: [project history](project-retained-history.md).

## Folder-aware Workspace adoption

Folder adoption adds schema2 source/target, imported baseline, collision, privacy-purge and original folder-receipt controls to existing transfer suites. A cross-runtime retained browser fixture detects UTC encoding rewrites before immutable IndexedDB commits. Current Release Sync qualification covers readable light/obsidian, phone/desktop, normal/enlarged text, geometry/focus/disabled controls and hydrated local Worker/D1/IndexedDB transfers. See [folder adoption](notes-folder-adoption.md) for actual evidence and limits.

## Reviewed relative-date tag membership

Relative tag controls cover explicit opt-in, full member changes, unchanged automatic fences, native/shared date parity, exact original SDK receipts, midnight terminal refusal, queued stale-work refusal and Auth final-dispatch guards. DST/date-line cases verify the atomic expiry boundary. Synthetic provider tests preserve unrelated tags and recover a lost applied reply after a day change without resending. Current Release Sync tests cover required light/obsidian, phone/desktop, normal/enlarged-text geometry/focus and actual isolated Workers/D1/IndexedDB. See [relative tag reviews](relative-tag-reviews.md).

## Shared UX and UI qualification

`ux-ui-rendered-browser` invokes `python3 tests/browser-qualification/ux-ui.py {task_key} {report_directory}`. The exact task key resolves one structured check in future.md, or the immutable completed validation plan for completed-task revalidation. The report directory must match the selected contract and reside in owned validation history. Missing journey adapters, route/state captures, schemes or interaction provenance prevent qualification. Declared native-date fixtures require nonempty date evidence. Native-date journeys are required only when declared by the adapter or returned as journey evidence; non-date surfaces require no invented date controls. Strict dependent adapters cover every selected route through hydrated semantic checks. Adapter declarations reside in tests/browser-qualification/ux-ui-adapters.json; each dependent task supplies the affected route/state selection and reviewed hydrated journey module. Existing specialist recipes remain independent.

The runner publishes the current Release, exports current Razor states through a focused nonzero bUnit suite, stages the published assets and verifies a nonce-bound owned loopback server/build identity. Browser sessions use the existing pinned Playwright1.62.1 Chromium/WebKit runtime and isolated persistent profiles. Synthetic Habitica GET responses and fixed placeholder artwork replies never leave the browser; other external requests abort. Hard execution deadlines reserve cleanup, and normal/failure exits close contexts/server and remove profiles. Interrupted cleanup remains unavailable until verified. Large screenshots and logs reside in ignored artifacts; compact reports retain exact artifact references and counts.

Extended frame inspection opts in through disabledMinimum and extended query parameters. Disabled text uses an explicit product 3:1 readability floor, including native control values and foreground/background group opacity. The [W3C text-contrast explanation](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) exempts inactive controls; the product floor is not an inactive-control conformance claim. Legacy specialist runners retain the existing disabled-text exemption until an affected task opts in. Unsupported paint/filter/blend states remain unqualified.

Native date qualification captures selected-value paint and an otherwise identical hidden-text image, then compares native text ink against a full-width same-engine reference. Selected values and outer geometry alone never establish readability. At least 97-percent ink retention and usable reference pixels are required. Reference images, actual images and exact engine/font/value context remain available for visual review. Missing reference paint yields unavailable evidence. Native pixel evidence is not OCR, physical-device or assistive-technology qualification.

The shared harness adapter covers 360/390/430/768/1024/1280-pixel widths, 100/200-percent text, gryphy-light/gryphy-dark/obsidian-glow and Chromium/WebKit. Readable positive controls and deliberately unreadable disabled/clipped-date controls validate the oracle. Actual hydrated Today/Tasks journeys exercise date editing, Today reset, routing and visible keyboard focus. Independent native paint controls verify disabled-control refusal without requiring a writable application workspace. The harness-only baseline mode records unfixed application observations separately from harness assertions. Dependent task runs reject required product errors or unavailable native evidence. Additional built-in schemes can be exported with HABITICA_BROWSER_ALL_SCHEMES=1; the default evidence exporter preserves the existing specialist scheme selection.

## Full-shell content visibility

Full-layout evidence embeds the stylesheet emitted by the current App theme provider. The exporter preserves body style nodes and requires the MudBlazor geometry variables. Fixture bodies contain visible content and a primary control; missing body evidence cannot qualify a shell. Ordinary component exports retain the existing container contract.

The shared frame checks main-landmark and marked body clearance from the fixed header. Independent rectangle canaries reject complete/partial overlap and missing content. Hydrated navigation journeys measure actual header/main geometry before accepting static cases. Shared dense-bar height, drawer clipping and content offsets use one rem-scaled framework height; normal and enlarged-text layouts keep the main landmark below the header. The shell-content-visibility adapter covers seven representative routes with the declared widths, text scales, light/dark schemes and Chromium/WebKit. Existing navigation interaction results remain distinct from the corrected visibility evidence.

## Workspace readiness qualification

The workspace readiness journey runs the current Release through the five task routes. A temporary native IndexedDB write transaction holds real reads for loading assertions; an invalid synthetic stored revision produces a terminal failure. The original document is restored before keyboard reload, and empty command/event/task journals verify no application write. Component tests separately cover coordinator states, frozen authority, command uncertainty, organization fences and account/source cancellation. No rewritten production assets or live provider access participate.

## Parallel palette qualification

The reviewed UI adapter uses one isolated persistent profile per engine on this host. Every launch settles before work or cleanup; each worker keeps its own native browser databases. Independent white/black paint controls run once per worker at every width and text scale. Every product fixture and hydrated route journey still runs every declared palette, width, text scale and engine. Coverage controls reject missing, duplicate and out-of-scope cases. The normal browser/controller deadlines are 560/900 seconds. The explicit 24-built-in matrix uses 1200/1500 seconds because it has eight times the palette cases; this changes an operation envelope, not a task budget. Every operation reserves 30 seconds for explicit context/server/profile cleanup.

Native control values, including textareas, are sampled through control geometry and computed text fill once; ordinary DOM text uses text ranges. The disabled textarea negative control must fail through native value sampling. Terminal receipt identities include session, engine, palette, width, text size and fixture, with a bounded 30-second frame wait and recorded inspection timing. Build/test processes remain separate from an active browser matrix to avoid host contention.

The disabled-palette adapter keeps a separate hydrated app page beside static fixture inspection. It verifies one cold start per engine, then traverses the complete route/matrix inventory with the real retained session. Static fixture inspection permits responsive layout settlement before the next interactive case. Full case coverage remains mandatory; a deadline-interrupted prefix is not accepted or reused.

The native-date reflow journey edits the real Today date field, compares its selected-value ink with a same-engine full-width reference, resets to the device civil day through keyboard activation, and checks control bounds/focus plus unchanged native type and limits. The pre-fix current-build run failed the ink control even with a valid input value. Physical calendar popovers, mobile keyboards and additional locales remain separately unqualified.

The workspace-readiness matrix includes native Today date paint, synthetic task preview, visible original-request heading focus, cancellation and unchanged accepted journals. WebKit native keyboard traversal uses Option+Tab on the current host; a native date/button diagnostic records plain Tab skipping buttons. Other platform keyboard settings and physical devices remain unqualified.

Workspace reload exceptions have a public component regression: a read failure offers reload and does not invent an uncertain save or original receipt. Mutation uncertainty retains its existing recovery wording. Original-request browser checks also require the focused heading below the fixed header and inside the viewport.

Category-heading qualification covers direct loads, keyboard category navigation, back/forward heading focus and viewport clearance, retained unsaved text and unchanged accepted journals. Real Razor captures include empty, long populated and frozen read-only Habits/Dailies at all required widths, text sizes and three built-in schemes. Device history and screen-reader announcements remain unqualified.

Every UX/UI hydrated journey now records the observed root scheme ID and computed root font size. A requested dark scheme cannot pass on a light page, and a 200-percent case cannot pass at the normal root font. Category direct loads explicitly apply the selected fixture palette; navigation fixtures seed the matching persisted preference so embedded appearance controls do not reset the selected palette. These fixture controls do not qualify real-device appearance persistence.

Canonical-destination qualification inventories Planning, Inbox, History and Statistics return actions, exact selected workspace retention, scoped History parameters after browser Back, keyboard activation and the explicitly named provider-cache route. Existing exact-record adoption and unknown-receipt tests retain separate canonical/provider IDs. Native date paint in changed planning/statistics views remains a required product check.

The required Planning/Statistics date repair uses real header subtrees, retaining the original Statistics wrapper so descendant responsive rules remain applicable. All selected-date captures compare unfocused native ink with a full-width same-engine reference; multiple probes retain distinct artifact paths. Hydrated tests edit an exact past civil date, retain min/max and selected source, inspect focus below the fixed header and assert unchanged task/command journals. Physical native pickers and other locales remain unqualified.

Today work-first qualification covers populated/empty days, native selected-date paint, original review/cancel fences, keyboard disclosure order, local in-app preference retention and unchanged notification permission/task journals. Reminder configuration snapshots explicitly open the real disclosure before serialization; closed summaries are captured separately. Uncertain delivery remains outside the collapsed configuration in component checks.

Bounded companion checks cover large synthetic owned/unowned collections, retained expansion, disjoint exact-key pages, query reset, full matching hatch/feed queues and separate sale scope. Razor captures retain real search/group ancestry and label partial surfaces. Hydrated tests use a hashed owned local synthetic provider GET fixture; fixture validation rejects external/traversing paths and the runner still blocks provider writes. Explicit hatch preview is cancelled before execution, cached inventory remains unchanged, and physical devices remain unqualified.

Dashboard freshness qualification separates snapshot retrieval provenance from domain refresh history. Component transitions cover fresh/stale/missing data, fetching/success/failure, unavailable timestamps and sign-out/account switch. The hydrated dashboard journey holds a synthetic account GET, fails that read while preserving cached readiness, then retries successfully. Eight real Razor status/action fixtures, including the retained signed-out cache, cover the declared light/dark, width and text matrix. Physical devices and assistive technology remain unqualified.

Dashboard status fixtures retain the complete Appearance card, including the owning responsive flex row. The native journey checks viewport bounds, 44-pixel action geometry, ordinary-word label width, keyboard focus and disclosure open/close behavior. The original squeezed-label screenshot remains the regression source. Synthetic failed-refresh responses use a non-retried 400 read failure to keep the matrix bounded; provider behavior is unchanged.

Dashboard controller qualification also signs in, performs a real synthetic manual refresh, replaces the account directly and signs out. The regression requires the replacement account to have no inherited UserProfile refresh entry; snapshot and command freshness gates remain unchanged.

Daily capture-order qualification uses a failing rendered DOM/geometry regression, public incomplete/invalid schedule submissions, valid weekly/monthly preview/cancel and disabled pending capture. Native keyboard journeys traverse the final schedule input before Add Daily and retain Todo/Habit title Enter behavior without accepting commands. Full real task-panel fixtures cover ready and review-frozen capture for all three kinds.

Native date reference inputs preserve effective fieldset disability and the fractional pixel origin of the measured field. The positive native canary now inherits disability from a fieldset and asserts reference-state parity; independent full-width and clipped negatives remain. The97-percent ink floor is unchanged. This prevents a complete disabled date from being compared against an enabled, differently rasterized reference.

Native date references also retain the measured backdrop beneath translucent input surfaces. A hidden-input screenshot preserves the same underlying pixels for the full-width reference; actual/reference hidden-text pairs then compare glyph paint over equivalent backgrounds. Backdrop artifacts are retained beside value/hidden-value images.

The independent positive native canary uses a translucent date field over a fixed pastel gradient and inherited fieldset disability. A white-backdrop substitute cannot qualify this full-date control. Canary colors remain independent of product schemes.

Daily enlarged-date regression evidence retains the360-pixel/200-percent selected-value screenshot with the missing day, plus the same-engine complete reference. The scoped padding repair must pass native ink qualification for both enabled and review-frozen forms; keyboard weekly/monthly review and cancellation remain required.

Native field probes center the actual input before collecting paint and backdrop geometry. A review insertion can otherwise leave a partially visible input behind the fixed app bar; such a header screenshot supplies no native-value evidence.

Saved-list field-error qualification covers single/multiple typed constraints, unsupported choices, invalid dates, paired ranges, fixed-relative conflicts, missing/unavailable timezones, bounded search, stage/source conflicts and empty picker identities. Component checks retain exact drafts, reject command preparation and preserve null/empty search roundtrips. Native checks prove keyboard correction, first-error focus and associations, explicit source-conflict correction and cancelled successful reviews without writes. Corrected frozen Razor fixtures retain four selected dates; enabled native probes cover planned/deadline fields.

Owned native-select diagnostics show that lowercase type-ahead commits Todo from Daily in both pinned headless macOS engines, emitting input/change events; ArrowUp alone does not commit in these drivers. Saved-list keyboard correction uses the qualified type-ahead path. The original and extended standalone diagnostics retain actual values/events and verified browser cleanup. Native popup arrow interactions remain separately unqualified.

Saved-list focus diagnostics independently reproduce a native date focused beneath a fixed header and show that scoped field scroll margin reveals the control in both engines at16/32-pixel root text. The product journey requires the actual focused error-control center to be unobscured and visibly outlined.

Saved-list reveal controls cover a retained focused field, a user focus change, removal during navigation and inherited disability. A current native trace confirms that WebKit can retain focus on an offscreen date after validation reflow even with scroll margin; the product post-layout reveal is required. Native checks wait up to three seconds for the same focused field to be fully visible and unobscured.

A native DOM regression retains the first one-field timezone alert, changes the timezone failure reason and reviews again. The prior alert must be disconnected, proving a fresh summary insertion rather than unchanged count-only text. This check qualifies announcement structure, not synthesized speech or screen-reader delivery.

Game-control qualification uses same-named long Party members, gear and retained presets, stable open/closed regions, contextual RenderFragment actions and persistent input labels. Hashed owned synthetic party/content/user GET fixtures use exact endpoint mappings; external and traversal filenames are rejected. Native journeys exercise disclosure keyboard behavior and preset draft/removal cancellation without equipment mutation or preset saves. Existing Equipment UI is battle-only; Costume actions and physical assistive technology remain unqualified.

Party reflow qualification retains the full overview and closed/open same-named member fixtures. The original enlarged-phone overflow receipt supplies the failing regression; native checks retain exact disclosure state, focus and ordinary-word identity width across the complete matrix.

Party native checks include actual document bounds with the local endpoint warning present. Static Party fixture success alone does not establish hydrated viewport geometry; screenshot review rejected the first automated pass because the shared warning widened the document.

The Party native journey explicitly reloads once after initial account readiness to exercise a retained Party snapshot available on first render. That source path initiates the shared quest-state read and exposes the local endpoint warning. A cold load without a retained Party snapshot can omit the read, so the warning check cannot rely on incidental prior matrix cases.

Equipment readability qualification measures actual ordinary-word identity width and intact stat tokens in real Razor captures and hydrated pages. A page can fit the viewport while gear text collapses beside artwork; such glyph-wide columns fail independently of outer geometry. Pure controls reject narrow copy, wrapped stat values and absent text geometry. The original433-case automated pass remains unaccepted after screenshot review found this gap.

Equipment word-width checks also inspect rendered button labels using the actual text-transform and font. Screenshot review checks nested-card spacing and complete recommendation words; outer document geometry and intact stat tokens alone are insufficient.

Fixed Equipment action words must fit completely, including the uppercase recommendation label. Full parent-width use alone does not excuse a split action word: native enlarged-phone evidence measured the word at284.46 pixels against a219-pixel label area. Reduced phone nesting and scoped preset-action inline padding reserve complete word width without reducing type size.

Equipment action words also require one actual text-range line box per word. A native outlined Save recommendation label split the final letter despite fitting the previous two-pixel width tolerance; the current independent negative control rejects that exact case. Missing word line boxes remain unavailable.

Each native equipment journey runs independent Arial word controls before application load: one complete reference and a word container half a pixel too narrow. Actual Range line boxes must show one versus two lines, and the latter must fail inside the old two-pixel geometry tolerance. The controls use explicit black/white paint and no application CSS; all product matrices remain required.

Game accessible-name regressions include whitespace-equivalent Party members, gear and presets. Two focused failures precede the comparison repair. Native provider fixtures deliberately use different raw whitespace, and accessible-role lookup uses the browser-normalized name. Separate real Razor fixtures retain both exact-name and whitespace-equivalent collisions across the full matrix.

Task/private-sync naming qualification covers same visible task names with different raw whitespace across Todo, Habit and Daily rows, native Notes disclosures, exact second-record previews, unchanged journals and original disabled review fences. C# ready snapshots supply native synthetic journal data, including valid Daily recurrence fields. Private-sync checks name each section, prove independent keyboard changes and restore original state without sync upload. Native selected Daily dates, focus and complete light/dark width/text matrices remain required.

Hierarchy control naming uses same-name/whitespace-equivalent Tasks and Project-mode fixtures, exact completion review and cancellation, native Notes/References keyboard toggles, focused context, parent return and edit cancellation. The owned synthetic journal must remain unchanged. Physical devices and assistive technology remain unqualified.

Repeated Daily occurrence date inputs use scoped half-rem inline padding, matching the bounded creation date fields. Qualification compares selected native date paint in both ready and original-review-disabled states; exact per-task date drafts and command fences remain unchanged. Native popovers, physical devices, other locales and assistive technology remain unqualified.

Shared raw task-list rows allocate identity and action columns with available-width18rem minima, stacking when both columns cannot fit. The rule avoids auto-width actions collapsing task names and Notes into glyph-wide columns. Native recurring-row qualification checks available ordinary-word width, visible Notes disclosure, exact action previews and selected Daily occurrence ink. Hierarchy and organization rows retain separate layout ownership.

Recurring row qualification also requires each ordinary word in actual rendered action labels to occupy one line. Native Range ink rectangles cover uppercase labels in the original-review-disabled state; outer width alone cannot qualify word readability. The initial recurring-row automated pass was withheld after manual Schedule/occurrences wrap detection.

The recurring-row native word-line check includes original review actions. Phone action/review buttons retain44-pixel minima while scoped reduced inline padding avoids splitting fixed action words at200-percent text. Earlier automated reports remain unaccepted when manual review finds a missing readability condition.

Settings phone sizing bounds cumulative nested padding: root cards use three-quarter-rem padding, nested action/status cards use half-rem, and private-sync rows use three-eighth-rem. Scoped action padding retains44-pixel minima. The private-section fixture captures owning Settings markup; native checks retain intact action words, available ordinary-word width for section names, exact labels and independent exclusion toggles. Section order and data/consent actions remain unchanged; physical devices, native file dialogs and assistive technology remain unqualified.

Push device presentation distinguishes not checked, checking, observed capability and failed browser checks without changing the BrowserPushCapability wire record. Permission default displays as not requested; denied, granted, installation-required, insecure and unsupported observations remain distinct. Account/source changes reset observations and late/disposed probes are discarded. Browser module read failure retains the existing unprepared wire state. Setup checks request no permission; enrollment retains explicit acknowledgement and direct device gesture, original consent and pending-request recovery. Controller/component qualification includes late probes and actual PwaPushBrowser module failure. Physical devices, service-worker enrollment and notification permission prompts remain unqualified by synthetic review.

The authenticated compact drawer puts canonical Add task and Today/Inbox/Plan/Projects before direct Habits/Dailies/Tasks and secondary tools. More planning and Game tools use native disclosures with the active route group open on entry/navigation. Notes, Settings and Diagnostics remain direct; all prior route URLs and permissions remain. A compact brand/account summary replaces the drawer hero. Qualification covers exact active links including nested Tasks, native summary keyboard operation, drawer Escape/focus, route/capture reachability and original synthetic journal preservation. Physical devices and assistive technology remain unqualified.

Collapsing an active secondary navigation group retains one current-section marker on the summary. Expanding the group leaves the exact selected link as the only visible current cue. Native qualification asserts a single visible cue after keyboard disclosure collapse; ordinary focus styling remains independent.

Equipment discovery retains public component cases for301 owned records, translated/case-insensitive names and keys, combined slot/query empty feedback, forced category reveal,20-item result pages, preserved folds, exact loadout/optimizer/preset drafts and no equipment mutations. Native slot choice retains focus until explicit View results/Enter; empty recovery returns to search. Current route/theme/width qualification and device/AT limits remain separately recorded.

Owned-equipment native slot captions reserve actual selected-label ink plus the native arrow area. The303-item fixture covers both Head Accessory and the compact Two-handed caption with exact full slot values. Scoped8px phone inline gutters/padding retain complete captions at200-percent text without reducing type size.

Equipment paging focus waits for settled visible heading geometry. Enlarged WebKit anchoring persisted after a12-second diagnostic; the existing guarded field-focus reveal runs after FocusAsync and two animation frames. Each module reference is released within the async focus call, with disposal/account checks and no repeated focus after user movement. Physical keyboard/device behavior remains unqualified.

The daily-timezones adapter covers readable exact-ID selection, observed UTC+7 mismatch correction without automatic draft replacement, frozen review/cancel and independent native IANA review on the current Release routes. Full surrounding task-panel fixtures cover linked editors and unsupported account settings. Selected option serialization is asserted directly; linked offset captions require measured ink width within available area including native-arrow reserve. Synthetic provider observations grant no live schedule mutation or connected-device qualification.

The saved-list-layout adapter uses a real C# journal with25 existing definitions/tasks, native list/result pagination, compact New list/edit/close/resume, hidden-filter review, invalid-field reveal and fixed/relative conflict correction. Current Release synthetic browser checks preserve the original journal and cancel every preview. Empty/unavailable-source states remain scoped component fixtures. Physical devices, assistive technology and provider execution remain unqualified.

The phone-notes-editor adapter captures public Notes component transitions and uses an actual C#26-note schema2 notebook plus the matching canonical project journal. Native cases cover library paging, entry/back/resume, retained version drafts, the existing unsaved-change guard, cancelled folder preview, exact project/folder context, route persistence and responsive resize. Fixed action words use native Range line counts with an independent wide/narrow negative control. Original pending receipt recovery and conflict states remain scoped component fixtures. No provider write, note/folder acceptance, service worker, physical-device, virtual-keyboard or assistive-technology result is implied.

The projects-browse-first adapter covers a complete Projects page, hidden/default management, selected work, empty state, closed drafts and exact alias review. Native data comes from a C# canonical journal with26 active projects, an archived reference and assigned/unassigned tasks, paired with a real schema2 Notes notebook/folder/history. Previews are cancelled; notebook and canonical journal remain exact. Stored alias conflicts, archived history and original receipt fences retain focused component coverage. No provider operation, consent, publication or device qualification is implied.

The settings-sections adapter combines actual signed-in/out, fresh-access and management component captures with current Release native section-link focus/scroll, installation/appearance disclosure, independent private-sync exclusions and canceled browser-removal review across the full six-width,100/200-percent, three-palette Chromium/WebKit matrix. Synthetic journeys execute no upload, download, restore, consent or removal acceptance. Native file dialogs, live connected server paths, physical devices and assistive technology remain separate.

The named-reminder-days matrix combines actual component empty/retained/pending/unknown/paused states with a hydrated Release journey through real controllers, typed HTTP adapters and native IndexedDB. Owned synthetic recovery/read/preview replies use an independently emitted C# identity-hash control; only preparation and local discard occur. Native weekday/month-date selection, associated errors, exact revision/cue/DST/device identities and selected-date paint cover all declared widths, text sizes and palettes under Chromium/WebKit. Worker/D1, real scheduling, provider delivery, native notification permission, physical devices and AT remain unqualified.

Equipment count qualification measures actual word Range lines, unbroken numeric paint, artwork/count/label overlap and reserved image dimensions. Real unavailable-image requests exercise the original fallback. Component captures include zero, large counts and longer-label stress; native Release uses exact parsed synthetic counts1234/20/321/42 with unchanged snapshot assertions. Physical devices and AT remain unqualified.

Private-sync timestamp checks control en-US/en-GB culture, compare rendered local text, require unchanged UTC machine metadata, reject a literal format suffix and preserve missing-payload/status behavior. Actual component timestamp rows run through the full rendered matrix. The hydrated Settings journey qualifies navigation and safeguards with no native-session sync activity; upload/download and physical devices remain separate.

Task-copy placeholder checks cover null/populated dates, stage, parent and organization, empty/nonempty lists, zero-valued enums, HTML encoding, exact read-only review inputs and all supported kind pairs. Actual owning review captures plus a hydrated typed-Workspace journey retain exact previews across Convert/Tasks/Habits/Dailies, cancel without acceptance and compare unchanged journals. Full light/dark responsive matrix covers reading order and fixed metadata words; physical devices, AT and live providers remain separate.

The rendered-browser recipe registers every reviewed adapter journey, local module dependency and declared synthetic fixture JSON as an explicit source input. Actual workflow temporary-copy controls reject journey, data and adapter-map drift before profile refresh; refreshed source bytes change the environment identity. Current matching receipts and full rendered contracts remain separate from registration checks.

Notes entry readiness uses a held actual Notes store in bUnit and native IndexedDB readwrite holds in Chromium/WebKit. Verify readable disabled New note during initialization, release in finally, one keyboard entry with visible focus, exact unsaved text and unchanged notebook/task journal. Original all-palette failure remains retained. The new journey participates in registered qualification source identity.
