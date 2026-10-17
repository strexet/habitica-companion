# Testing

## Application topology

`tests/Habitica.Domain.Tests/`, `tests/Habitica.Rules.Tests/`, `tests/Habitica.Api.Tests/`, `tests/Habitica.Storage.Tests/` and `tests/Habitica.Application.Tests/` contain xUnit domain, calculation, API, storage and orchestration checks. `tests/Habitica.WebApp.Tests/` uses bUnit for Razor components, page behavior and session state. `tests/Functions/` uses the Node test runner.

After the prerequisites and build in [build-and-release.md](build-and-release.md):

```sh
dotnet test Habitica.sln -m:1 -nodeReuse:false
node --test tests/Functions/*.test.mjs
```

Default checks require no live Habitica credentials. [ai-app-testing.md](ai-app-testing.md) owns browser/UI guidance. Diagnostics live checks, live mutations, deployments and real accounts require applicable authorization. Documentation migration runs no application build, application tests or live-service checks.

## Planning validation

```sh
python3 .agents/skills/skill-tree-process-future-pending/scripts/validate-future-document.py skill-tree/documents/future.md --strict --format json
python3 .agents/skills/skill-tree-repo-documentation-audit/scripts/validate-repository-documentation.py . --format json
python3 .agents/skills/skill-tree-implement-next-future-task/scripts/select-prioritized-task.py skill-tree/documents/future.md --format json
python3 .agents/skills/skill-tree-repo-documentation-audit/scripts/workflow-state.py index .
git diff --check
```

Task selection follows current priority, dependencies and unresolved questions. Metadata validation checks manifest paths/hashes, the installed skill catalog, index agreement, dependency closure and checkpoint identities without changing state. Structural validation and indexed state consistency never establish implementation readiness, external compatibility or completed acceptance. The strict prose validator applies separately; inherited prose findings require scoped cleanup and remain audit coverage gaps.

Prose lint reports located errors and review warnings. `--warnings-as-errors` or repository `--strict` elevates warnings for strict qualification. Scoped plan validation avoids rewriting unrelated current-state prose.

## Canonical workspace persistence fixtures

`tests/browser/workspace-storage.html` is a synthetic, test-only browser fixture outside the PWA build. Serve the fixture with only the real storage module and pinned Dexie vendor asset on a dedicated local origin. The fixture seeds schema 1, imports the real schema-2 module, injects a quota failure through the browser IndexedDB API, races revision-controlled writes and checks reload persistence. The fixture origin must remain separate from the app origin; no real account data or credentials belong in the fixture. C# storage tests qualify interop arguments, scoped identity and strict journal parsing; application fixtures qualify recovery and restore conflicts.

## Cloud workspace qualification

Build the Auth, MCP and Workspace packages before running `npm --prefix workers/companion-workspace test`. The workspace fixture reuses the Auth package's pinned Miniflare runtime and requires the Auth dependencies. Tests create isolated local databases and mock Habitica credential validation. The tests cover account isolation, explicit consent, command deduplication, concurrent acceptance, injected D1 append failure, canonical domain constraints, session revocation and delayed requests across deletion or disconnect. Archive capacity rejects additional commands without removing retained history.

The neutral Worker journal fixture is validated by the C# application suite, including command payload hashes. Web tests cover separate workspace consent, safe configuration, distinct management/session tokens, export identity validation and late-response cancellation. The browser storage fixture adds account-scoped workspace discovery and canonical clearing while preserving legacy data. All destructive fixture checks use a separate disposable browser origin. Real enrollment, deployment, mobile eviction, mixed PWA versions and live Habitica projection remain separate validation lanes.

## Projection core qualification

Auth projection fixtures cover precondition ownership/expiry, durable duplicate receipts, uncertain scores, positive alias reconciliation, write refusals, quota/credential pauses, simultaneous dispatch attempts, reservation failure, automated spacing, Daily preconditions and scheduled retention. Workspace fixtures cover reviewed adoption, metadata changes between preview and confirmation, deletion/edit conflicts, immutable accepted intent and ordered create-then-complete composition through the real private gateway. Every outbound Habitica response is synthetic.

The application suite validates current and legacy Worker journal fixtures and retained accepted task snapshots. Browser IndexedDB qualification rejects edits to accepted intent and verifies the intent after reload. Executor fixtures qualify persisted ownership, receipt-only restart recovery and paced dispatch orchestration. Enabled deployment remains a separate rollout lane.

## Hosted execution qualification

`workers/companion-workspace/test/executor.test.mjs` exercises the built Worker services and isolated D1 databases with synthetic Habitica responses. Direct execution-core calls inject only the pacing wait for deterministic time control; shipped code has no timing override. The tests assert requested delays and persisted thirty-second deadlines, while Auth tests independently reject premature calls. The scheduled handler is also invoked through the local Worker service. Synthetic Daily execution verifies account, due and score ordering across two spacing waits.

Restart fixtures lose an acknowledgement after the Auth receipt commits, construct a new execution invocation, and assert one upstream effect with recovered journal evidence. Missing receipts and revoked credential contexts remain unknown; repeated wakes cannot grow history or replay effects. Other fixtures cover opt-out during waits, quota fencing, expired undispatched leases, transfer hashes, independent identity collisions and append rollback. Application tests cover local freeze, active-attempt waiting, late ownership acknowledgement and failed freeze. Web tests cover separate consent and late-response guards.

The isolated browser storage fixture verifies the frozen authority and immutable accepted intent after full reload. The ordinary rebuilt Settings smoke uses disabled shipped cloud configuration. Component tests exercise enabled and paused execution controls against synthetic application services. The local qualification does not cover real consent, deployment scheduling or upstream mutations.

## Workspace access renewal qualification

Synthetic tests require explicit renewal consent in connected Settings, reject a late renewal after tab access is forgotten, and preserve an uncertain original command across failed workspace reload and successful receipt recovery. The recovery fixture asserts one accepted identity and one canonical task. Rebuilt default Settings/Tasks browser checks preserve the disabled shipped cloud boundary.

Setting `HABITICA_BROWSER_EVIDENCE` for the focused component tests exports synthetic HTML rendered by the actual Razor components. Serve the exported HTML with the current app and pinned MudBlazor styles on an isolated local origin for narrow-screen layout inspection. The exported fixtures qualify static layout; component tests qualify interactions, and the ordinary rebuilt app qualifies default routing and feature guards. The fixture output contains no real account data or credentials.

## Foreground traffic pacing qualification

`AdaptiveRequestPacerTests` drives one hundred actual API-client dispatches through a synthetic clock and HTTP handler. Budget tests cover early slowdown, reserve protection, remaining/reset reconciliation, lower limits, malformed reset headers, bounded failure backoff and gradual success recovery. Concurrent-call tests hold the first response, cancel a queued caller, then cancel a cooldown wait without dispatching another request. Mutation failures verify one synthetic HTTP dispatch for 429, 503 and transport failure. The API suite and app build qualify local behavior; no real account traffic occurs.

## Canonical task command qualification

`workers/companion-workspace/test/task-api.test.mjs` calls the shared browser HTTP API and the actual MCP SDK through built Worker bindings. Coverage includes server-issued preparation identity, duplicate acceptance, stale/expired/old-epoch previews, exact canonical selection, malformed fields, tenant isolation, scope challenges and independent private-service scope enforcement. Structured receipts distinguish saved canonical data from pending, confirmed, quota-paused and unknown projection states. The synthetic executor paths assert no second upstream effect when acceptance is repeated.

The suite qualifies partial updates, Todo and dated Daily completion, wrong-kind rejection, bounded positive/negative Habit events and expired unaccepted preview cleanup. Accepted preparation bindings survive cleanup. Web/controller tests qualify review-before-save, immutable original identity after acknowledgement loss, redacted failures, impossible receipt rejection, fresh mapping approval, failed-refresh editing guards and account-change response rejection. Default browser checks use disabled cloud configuration; the opt-in synthetic real-Razor fixture qualifies 360-pixel wrapping separately from component interactions.

## Authenticated party qualification

`party.test.mjs` covers verified minimal context and independent scope checks. `party-access.test.mjs` first verifies a normal read, then disconnects, deletes or replaces Workspace access while Auth reads are in flight; late facts must be discarded. `party-planning.test.mjs` covers separate consent, exact reviewed party matching, owned-scroll and prototype-key rejection, explicit vote deduplication, concurrent CAS, leader/member/outside-party permissions, namespace deletion/recreation, contributor cleanup after departure and multi-statement rollback on a stale revision. Fixtures make no upstream gameplay writes.

PWA component/client fixtures require explicit consent and destructive confirmation, hide leader-only actions from members, encode untrusted labels, discard late account responses, use management identity for own exports after disconnect and strip unknown fields from exported records. The opt-in `HABITICA_BROWSER_EVIDENCE` fixture exports actual Razor markup for phone-width layout measurement; model/host activation and live multi-user behavior remain separate rollout checks.

The gated Workspace lifecycle fixture uses a fixture-only Worker and native service bindings. The gate buffers the synthetic identity response and shares boolean state; each request owns its polling promises and has a ten-second deadline. Eight concurrent focused runs and the full parallel Auth and Workspace suites retain all five stale-operation rejection and replacement-session success assertions. Failure diagnostics expose only status and identity-consistency booleans. The fixture qualifies local lifecycle behavior; synthetic transport failures do not establish a production session defect.

## Private spell gateway qualification

The Auth `spells.test.mjs` fixture uses synthetic accounts, OAuth grants and private Miniflare bindings. The fixture verifies allowlist/level/class/mana, stale hashes and party changes, immutable attempt receipts, overlapping requests, cross-profile denial, quota, revocation, storage failure and shared task/spell unknown-effect fencing. No real Habitica request is made. The neutral `tests/fixtures/workspace/spell-safety-v1.json` is consumed by Rules tests and Worker tests: agreement covers metadata and single-cast affordability; the server adds health, classes-enabled and three-cast limits. The fixture does not claim formula or full execution parity.

Build Auth before the Auth fixture suite so the tests exercise the changed bundle. All Worker builds/type checks, full Auth/Workspace suites and the app build/.NET suites qualify this private milestone. Default integration stays disabled; no new PWA control or live host activation is introduced. Workspace prepared-action qualification is described below.

## Reviewed spell action qualification

Workspace `spells.test.mjs` runs actual MCP SDK requests through OAuth, Workspace/D1 and the private Auth gateway using synthetic accounts. Cases cover a three-cast confirmation, explicit partial refusal, changed state between casts, two-minute expiry, token rotation and single use, duplicate/concurrent execution, namespace/consent/epoch revocation, isolated history export/deletion, independent scope gates and failed durable dispatch persistence. Unknown spell records continue to fence task execution after deleting expired Auth operational receipts. A lost acknowledgement is resolved through lookup only, with the remainder stopped.

The separate `spell-deadline.test.mjs` delays one buffered synthetic private response by twenty-one seconds and verifies known partial success, no second dispatch and a separate unsuccessful refresh indicator. Buffering the response before the gate avoids parking a live Miniflare stream across concurrent Worker calls. This transport fixture does not simulate or qualify real Cloudflare network latency. Both files retain the normal thirty-second per-file test deadline. All three Worker builds, Auth/Workspace suites, the app build and .NET suites run for this milestone. Default Settings still displays unavailable cloud integration controls. No real enrollment or upstream effect occurs.

## Overlapping page refresh qualification

Two controlled .NET regressions reproduce the original Background-versus-Visible priority failure. One holds an in-flight Party fetch while background/visible callers share the request and checks distinct caller metadata. Another holds only an older page's equipment-cache read while a newer page refresh completes, then releases the older view and checks retained current-page priorities. Both cases fail against the original source and pass after the ownership repair. Test fixtures use synthetic data and no live Habitica requests. Full app build and .NET suites qualify integration with existing refresh and storage behavior.

## UserGet readiness shape regressions

Auth read/projection and plugin/hosted-Daily fixtures model top-level `needsCron` from full UserGet. Cases verify true/false values, contradictory synthetic legacy flags, unknown/malformed values and refusal to score while Cron is pending. Four API parser cases verify server preference and the retained local fallback without using `flags.needsCron`. The broad .NET run exposed a separately repaired refresh-priority race; evidence resides in `refresh-priority.json`. The lifecycle fixture supports parallel file scheduling and retains explicit in-test concurrency assertions.

## Private Cron gateway qualification

Auth `cron.test.mjs` exercises synthetic OAuth/private bindings, fresh account/day/task/checklist/quest facts, independent scopes, replay/overlap, quota, durable reservation failure, credential/grant revocation and both directions of shared gameplay fencing. A failed response followed by advanced lastCron and needsCron=false remains unknown, fences new task/spell effects and performs no replay. Missing original receipts remain uncertain. Source-shape checks use full top-level readiness, not synthetic flags.

`tests/fixtures/workspace/cron-day-v1.json` is consumed by Domain and Worker cases. The fixture covers before/at boundaries, positive/negative and quarter-hour offsets, and leap-day rollover. Scope is fixed-offset day key/start only, without server timezone-transition, Cron eligibility or effect-formula parity. All three Worker builds, full Auth/Workspace suites and the app build/.NET suites qualify this private milestone. Parallel Worker file scheduling retains every explicit concurrency assertion. Default Cron capability is unavailable. No real Habitica calls occur.

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

The MCP read guard advertises both base profile access and the selected read-tool scope. The actual Auth/MCP fixture verifies denial of a profile-only grant, fresh consent using exactly the advertised scopes and successful account status retrieval. The failed missing-profile control remains bounded historical evidence. No write permission or live host qualification follows from the read upgrade.

## E14 local dev qualification matrix

Status: local checks pass. [Structured evidence](../history/validation/e14-local-qualification.json) records source fingerprints, suite counts and scope. All account/upstream fixtures use synthetic identities and responses.

| Boundary | Passing local coverage | Remaining live gate |
| --- | --- | --- |
| Dev defaults and legacy data | Isolated config generation, complete frontend merge/restoration, disabled effects, encrypted sync/party Functions and legacy portability fixtures | Cloudflare resource ownership, published configuration and HTTPS |
| Adoption and backup | Canonical identities/history retained; invalid imports and conflicting existing workspaces rejected; adoption requires review | Real account adoption and browser storage durability |
| Interrupted ownership and storage | Freeze failure rejects upload, lost acknowledgement retains identity, restored in-flight effects remain unknown, CAS rejects stale writes | Physical device eviction and mixed installed versions |
| Hosted commands and effects | Concurrent deduplication, epoch isolation, revoke during waits, original receipt recovery, unknown task/spell/Cron fences | Selected real-effect lanes after initial pilot; live Cron stays off |
| Party and management | Verified membership, leader/member permissions, separate consent, contributor removal/export, revocation and guarded key operations | Consenting multi-user checks and provider retention/log review |
| Client authorization | Codex-shaped CIMD loopback ports with exact callback/issuer/audience, refresh and complete read-scope upgrade | Actual ChatGPT/Codex desktop OAuth, plugin installation and skill activation |
| Responsive and accessible controls | Five real-Razor fixtures at 320/390/1024 pixels plus 320 pixels with 200% root text; all twenty cases fit without clipped controls; native keyboard focus reaches saved-work status | Real iOS/Android, assistive technology and end-to-end keyboard flows |

Local suites pass 102 Auth, 82 Workspace, 545 .NET and 22 Functions/configuration tests. Forty-three focused UI controls supplement full .NET coverage. App release publication, three Worker dry-run builds and generated Pages Functions compilation pass. Static fixture layout and one native focus observation provide bounded accessibility evidence; component tests qualify interactions separately. No hosted deployment, real enrollment, live Habitica effect or device acceptance follows from local checks.


## Native Codex owner pilot

[Native pilot evidence](../history/validation/codex-native-owner-pilot.json) records the actual Codex MCP surface, six account/task read tools, immediate repeated reads, successful native reuse after 617 seconds with stable profile identity and one explicitly approved Companion-only Todo save. The test task and original command receipt remain available, with Habitica projection not requested and zero confirmed game effects. Original-receipt lookup passes; simulated acknowledgement loss, denied-write scopes and real grant revocation require separate checks.

A bounded Auth live tail captures the application read-log field schema and status only. The log parser discards provider transport envelopes, URLs, request headers, account data and payloads before persistence; the tail process exits gracefully. [Cloudflare real-time log documentation](https://developers.cloudflare.com/workers/observability/logs/real-time-logs/) defines the provider envelope containing request metadata. A successful read sample qualifies only the sampled path; error paths and full production-log coverage remain open.

Native tool execution establishes neither ChatGPT behavior nor packaged-skill installation. A second consenting account and real mobile devices remain release prerequisites. Read reuse across a long interval can establish native session continuity; hidden client refresh exchanges and browser-side PKCE details require separate evidence.

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
