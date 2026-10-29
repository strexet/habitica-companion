# Testing

## Local live-test credentials

The owner may provide a root [`test-login.json`](../../test-login.json) with `userId` and `apiToken` for an explicitly authorized live test on the owner's Habitica account. The file is ignored by Git, should have owner-only file permissions, and must not be committed, deployed, included in fixtures, printed in logs, or copied into validation evidence. Its presence grants no permission for gameplay changes, party actions, cloud linking, or another live-test round; use the current user instruction and the [AI app testing guide](ai-app-testing.md) for the allowed scope. Synthetic tests use fixture identities instead.

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

Synthetic API cases cover all sixteen supported class spells in normal and explicit test profiles, with targeted spell identities retained. Ordinary API defaults permit existing party-management/manual Cron routes, while explicit test profiles reject those routes before transport. Every profile rejects party departure, including encoded route variants. Controller regressions qualify task-targeted and party spells in both profiles, retaining ordinary freshness/mana and sequential execution behavior. Razor regressions cover every class in both profiles without a test-only spell disable reason.

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

WorkspaceCloudClient.ConnectionStatusTests and ChatGptIntegrationClientTests cover independent fresh clients, explicit recovery without linking/grant creation, retained server state, expired management access, missing executor flags, malformed identity and late session replies. SettingsPageTests and WorkspaceOperationsTests cover consent gating, unknown status copy, failed refresh clearing prior success, recovered executor state, read-only checks and account replacement. AppSessionControllerTests verifies direct account replacement forgets tab management without reenrollment. These checks use synthetic HTTP/component fixtures and contain no live credentials.

Synthetic real-Razor output is exported through HABITICA_BROWSER_EVIDENCE for published CSS and light/dark layout inspection. Such frames qualify rendering separately from component event tests; actual Mac/iOS, hosted enrollment and background dispatch remain outside this root validation.


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

The solution suite passes 967 tests, followed by 24 focused panel/presentation checks for final warning copy. The packaged workflow suite passes both SDK/schema controls with synthetic accounts. Local rendered fixtures use production styles across 360/390/430/768/1024/1200-pixel widths, six deterministic schemes and 100/200-percent text. Structured measurements and source fingerprints reside in projection-policy-audit.json. Physical devices, assistive technologies, installed-host skill updates and new live effects remain outside the audit evidence.

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

Coverage tests distinguish exact expected owner, foreign/missing owner, stored identity, pending/review counts and stale or changed task membership. Domain and application tests retain accepted organization hashes/receipts across additive ledger storage, logical removal, export, paused restore and frozen handoff. No native-to-Habitica execution or actual account mutation is qualified by the model milestone. Layout/styles are unchanged; dedicated tag UI and live/device checks remain separate.


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

The actual MCP SDK exercises advertised Notes schemas, separate scopes, exact clothing measurements and family/birthday retrieval, full Markdown preservation, partial edits/appends and verified replacement ranges. Ambiguous duplicate titles expose multiple exact IDs; unknown target selectors fail. Malicious note text remains returned data with no automatic dispatch. Preview before/after values expose conflicting facts without changing stored content before acceptance. These controls qualify server/tool behavior and packaged guidance, not an actual model decision.

Response-loss injection forwards real acceptance before withholding the acknowledgement; original receipt/preparation recovery and repeated acceptance preserve one append. Cross-profile, stale, expired and revoked previews refuse. Scheduled expiry and exact purge remove pending body copies. Package validation covers all six skills and generated identity descriptors. Real ChatGPT/Codex discovery, fresh-chat invocation and interpretation remain unqualified.


### Notes page qualification

NotesControllerTests, NotesPanelTests, NotesMarkdownRendererTests and NotesDiagnosticsTests verify exact Unicode/CRLF Markdown, .md import/export, safe structured preview, no remote image/executable links, conflict preservation, explicit discard/cancel, separate cloud consent, unavailable reads, source/account changes, retained versions, reviewed trash/restore/purge and original receipt recovery without duplicate writes. AppSessionController Notes controls verify physical forgetting of tab credentials on account switch and sign-out. Diagnostic operations retain outcomes and command identity without title/body content.

The matching-source solution passes1168 tests; final focused Notes controls pass35 tests after the final layout adjustment. Release publication succeeds. Rendered qualification covers ten Notes/privacy/navigation fixtures across360/390/430/768/1024/1200-pixel widths, six deterministic themes and100/200-percent text:720 cases, zero geometry/contrast/primary-target errors and no unsupported surfaces. Two seeded cases detect overflow/overlap and contrast failures. An initial muted-version contrast failure in Toxic Swamp is corrected through the shared readable token. Native keyboard traversal confirms a visible three-pixel focus outline. Evidence resides in skill-tree/history/validation/notes-ui/result.json. Physical devices, assistive technologies and real hosted Notes enrollment remain unqualified.

### Project Notes qualification

NotesProjectPanelTests exercises browser/cloud create-in-project, attach/move/detach reviews, exact duplicate-name identities, retained association versions, unchanged Unicode/CRLF Markdown, combined text/trash/project/unassigned pages beyond twenty results, stale lifecycle labels, unavailable Workspace filters, private drafts across hiding/navigation, account changes, stale reviews, original acknowledgement-loss recovery and separate cloud consent. Archived project context disables creation while retaining existing Notes. Project-only access renders no private browser title or snippet. Existing Notes, hierarchy, organization and navigation regressions retain their original responsibilities.

The final local solution passes1441 tests; the final focused route/controller selection passes74 tests and Release publication succeeds. Rendered qualification covers33 actual Razor fixtures on /notes and /todo/projects across360/390/430/768/1024/1200-pixel widths, Gryphy Light, Gryphy Dark and Obsidian Glow, at100/200-percent text:1188 cases with zero geometry, contrast, primary-target or harness errors. The lowest measured enabled-text contrast is5.12. Seeded geometry/contrast controls detect overflow, overlap and unreadable text. Native Tab traversal in the200-percent Obsidian Glow fixture exposes a visible three-pixel focus outline. Paginated library rows grow with long titles rather than clipping controls inside a short nested scroll region. Compact source/evidence resides in skill-tree/history/validation/notes-project-ui/result.json; large synthetic fixtures and logs reside in ignored skill-tree/artifacts/notes-project-ui. Real private Notes, hosted enrollment, installed host behavior, physical devices and assistive technologies remain unqualified.

### Dedicated statistics controls

TodoStatisticsTests verify restore/recomplete cycles, more than one hundred Habit events without page truncation, independent positive/negative counts, requested versus confirmed effect prefixes, current project/goal denominators, archive retention, uncertain conversion dates, per-task value deltas, missing segments, inclusive UTC ticks, leap/year boundaries and invalid sources/filters. Eight Application controls pass. Twenty-one focused Web controls cover the dedicated filters, source separation, unavailable/account-change states, paged long-name progress and existing Tasks interactions after aggregate removal. Rendered statistics and expanded legacy detail fixtures use the shared responsive/theme harness; evidence resides in skill-tree/history/validation/todo-statistics/result.json. Hosted use, physical devices and assistive technologies remain separate qualification.

## Todo Sync composition

Synthetic component checks cover shared source selection, distinct Workspace consent and executor controls, no write on route entry, an exact original cloud receipt, late account replacement, and missing browser receipt without retry. Existing Workspace settings and tag checks continue to cover status refresh, unavailable access, separate consent, local/cloud backup, transfer cancellation and unknown original metadata. A cancelable browser-clear review names Notes and canonical workspace backups. Rendered route layout checks cover responsive and theme geometry; physical device, real OAuth grants and production account migration remain separate qualification.

### Todo category navigation checks

`AppNavMenuTests` verify the permanent nested menu, every working destination, selected-route state and signed-out absence. `TodoCategoryPageTests` render Habits, Dailies, Tasks and Today with one shared synthetic workspace; kind filters, immediate category capture, score/complete controls, hierarchy and the selected-day create link are present without preparation or acceptance. Existing Today and hierarchy interaction suites retain exact original-receipt and date behavior. Rendered fixtures cover the new menu and four category pages together with Projects, Statistics and Sync across supported viewport, theme and text settings. Browser fixtures are synthetic; hosted enrollment, physical-device touch and assistive-technology behavior remain separate checks.

The final local solution run passed all suites, including 735 WebApp tests, and the Release WebApp publish succeeded. Two rendered Todo matrix batches covered nine fixtures in 756 cases: six viewport widths, seven themes including Gryphy Light and the updated Obsidian Glow, and normal/enlarged text. No geometry, contrast, harness or unqualified-surface errors remained. Manual 360px visual inspection found category labels overlapping at 200-percent text despite a clean initial geometry result; flex wrapping removed the overlap in both light and Obsidian Glow. Seeded controls still detect overflow, overlap and contrast faults. See `skill-tree/history/validation/obsidian-glow/result.json` and `skill-tree/history/validation/todo-navigation/result.json`. Physical devices, production sessions and assistive technologies were not exercised for this milestone.

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

Run tests/Functions/foreground-reminders.test.mjs for fake-clock production scheduling and serialized production storage transitions, alongside existing gear-recovery-storage and pwa-push controls. WorkspaceReminderCuesTests checks canonical intent and stable schedule identity; TodoReminderPanelTests covers separate opt-ins, due/future visibility, denied/unavailable/uncertain states and account callback fences. App session browser clearing includes the reminder store. tests/browser-qualification/reminder-storage.html is a disposable-origin real IndexedDB transaction control using fake notification APIs. Captured Today fixtures require normal/enlarged text in light/dark including Obsidian across phone/desktop widths, detector negatives and visible focus. These do not qualify actual OS delivery, permission prompts, assistive technology or physical devices.

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

`TodoExtendedRecoveryTests`, `TodoHistoryPanelTests`, `WorkspaceTaskControllerTests` and typed `WorkspaceCloudClientTests` cover native unarchive/correction, linked preview/accept/original receipt, lost responses, reopened identity discovery, account changes, filters/drafts and concrete refusal states. Capture current Razor fixtures with `HABITICA_BROWSER_EVIDENCE`, publish current assets locally and stage the history route through `tests/browser-qualification/prepare.py`. Measure the shared seven-theme, six-width, normal/enlarged-text matrix and seeded detectors; native keyboard interaction and Blazor component dispatch remain separate evidence. See [history recovery controls](history-recovery.md) and `skill-tree/history/validation/history-recovery/result.json`.

## Retained recovery discovery checks

`workers/companion-workspace/test/recovery-records.test.mjs` covers bounded records, source/receipt provenance, stable paging and refusal contexts. `recovery-records-api.test.mjs` exercises actual SDK and browser reads, scope/profile isolation, canonical fences and zero provider/mutation behavior. Build all three matching Worker bundles before composition tests. See [retained recovery discovery](retained-recovery-discovery.md).

## Causal task activity coverage

`WorkspaceActivityCoverageTests` and `activity-coverage.test.mjs` qualify new known segments, legacy unknown prefixes, deleted witnesses/revisions, metadata-only revision gaps, unobserved task transitions, recurrence/timezone edits, pause/resume, archive/unarchive/restoration, original correction records, lost scores/terminal sources, export/import and clock rollback. Both runtimes consume `activity-coverage-v1.json`; document-only round-trips preserve witnesses while full-journal reads validate the causal sources. Query endpoints do not alter persisted evidence. Current evidence resides in `skill-tree/history/validation/task-activity-coverage/`. No UI route changes, real provider/account/device operations or publication are included.

## Durable exact-adoption qualification

adoption-api.test.mjs covers synthetic two-profile HTTP/D1 and MCP SDK identity, retained completed/archived records, malformed targets, request reuse, expiry, stale native/canonical state, epochs, pending/unknown effects, shared lease, atomic receipt rollback, lost acknowledgements and immutable original recovery. workspace.test.mjs retains browser adoption and later game-intent regression coverage. WorkspaceCloudClient.ProjectionTests validates server-issued IDs, full receipt identity, original preview recovery and unknown versus terminal refusal. TodoTaskLinkTests checks original-review/source/account fences and proven non-acceptance cancellation. Native writes/scores remain zero in adoption fixtures. Current captured Tasks/History link states receive light/dark responsive geometry, contrast, focus, primary/disabled controls and 200-percent-text checks; host/device/assistive technology remain separate. Evidence: skill-tree/history/validation/task-adoption/.


## Scoped tag correspondence qualification

The shared conversion-retirement-prerequisites.json journal supplies C#/Worker parity for complete retained membership. Scoped status and retirement controls cover unrelated document/task/projection revisions, task metadata, exact group/lifecycle changes, native remaps, incomplete members, ambiguous intents and unknown evidence. Cloud API controls verify current connection/access-epoch provenance and zero dispatch from status reads. Retirement retains fresh native verification and separate irreversible approval. Portable stored evidence does not establish current service authority.


## Group-tag diagnostic operation qualification

WorkspaceCloudClient tag observers qualify eight fixed operation names, original command/correlation context, read/mutation modes and unknown-result preservation under synthetic transport failure. Diagnostics bundle controls round-trip the exact allowlist through retained storage, repeated sanitization and explicit shared-bundle parsing. Unknown names and private tag/task/credential canaries remain sanitized. No automatic upload, live account call or UI layout change is included.


## Shared diagnostic boundary qualification

DiagnosticSharingTests and diagnostic-protocol.test.mjs share diagnostic-sharing-v1.json for canonical UTF-8 bytes, hashes, UTC milliseconds and repeated sanitizer parity. Diagnostic API controls exercise independent consent/scopes, metadata-only preparation, atomic quota/receipt rollback, lost acknowledgements, revocation races, two profiles, expiry, old request refusal, cleanup failure, forged ownership/source fields, bounded filtered paging and actual SDK reads. Typed clients refuse altered identity/hash, raw failures and late account responses. Synthetic providers observe no diagnostic-driven calls or gameplay changes. Auth permission disclosure and remaining host/device qualification stay separate from local API evidence.

DiagnosticSharingPanelTests exercises explicit consent/preview/preparation/upload,100-entry omission counts, frozen navigation state, identity changes and late replies, lost/missing originals, no replacement/repeated upload, quota refusal, expiry, revoke/cleanup failure and manual fallback. Captured actual Razor markup covers both /diagnostics and /live-tests; rendered theme/width/text measurements supplement component interactions. Physical devices, assistive technology and live sensitive uploads remain unqualified.

Notes project qualification retains unchanged notes-wire-v1 and adds notes-project-wire-v2 for C#/Worker metadata bytes, hashes and exact source text. Synthetic D1/SDK cases cover ownership/source/lifecycle, complete-set paging, scope isolation, rollback and original recovery. Direct-source D1 controls hold acceptance before commit to change the project revision, and simulate lost commit acknowledgments. The isolated localhost browser harness validates actual IndexedDB cross-table project guards and cleanup of generated synthetic rows. Real Notes, devices and hosted migrations remain unqualified.

## MCP protocol migration qualification

The pinned server/core/client SDK2.4.0 and genuine legacy SDK1.32.1 fixtures exercise matching MCP/Auth/Workspace dry bundles. The Auth suite passes209 tests, including six modern/legacy cases for discovery, automatic/pinned negotiation, complete tool/schema equality, interpreter-backed Worker schema validation, Notes acceptance with lost acknowledgement and cross-era original recovery, malformed/unsupported envelopes and standard headers, content types, audience/Origin/scopes/body bounds, concurrent owners and changed grant epochs. The Workspace suite passes713 controls without transport failures; the retained v1 Events prototype passes10 controls. All three Worker type checks and dry-run builds pass with unchanged runtime/configuration pins and no unsafe-eval binding.

Compatibility discovery reports the modern revision explicitly because the SDK public SUPPORTED_PROTOCOL_VERSIONS constant contains legacy revisions only. The exact SDK/package lock, qualified protocol set, build hashes and independent installed-package uncertainty remain separate. Compact source/evidence resides in skill-tree/history/validation/mcp2-migration/result.json. Hosted activation, callback subscriptions, real event delivery, remote migrations and device/host observation remain unqualified. [mcp-protocol-compatibility.md](mcp-protocol-compatibility.md) owns transport and rollback details.


## Callback egress candidate qualification

Build Auth through its existing npm build script before running `node --experimental-strip-types --test --test-concurrency=1 --test-timeout=30000 workers/chatgpt-auth/test/callback-egress.test.mjs`. Run `npm test` from deploy/callback-egress on Node22. The gateway controls cover public/special/mixed address refusal, exact-byte grants, actual local TLS chain/hostname/address checks, redirects, byte limits, deadlines, one-use concurrency, acknowledgement loss and secret-free output. The Worker controls use actual synthetic Miniflare/D1 reservations and compose the production gateway with the claim route. Dev preparation must retain CALLBACK_EGRESS_ENABLED=false in both ordinary profiles.

Retain function bundling, Auth type/dry build, negative controls, source fingerprints and profile results under skill-tree/history/validation/callback-egress-netlify. The [callback egress contract](callback-egress.md) and hosted profile require separate selected-account publication and owned DNS/TLS fixtures. Local loopback TLS and service documentation never establish Netlify DNS/TLS behavior, provider logging, cold starts or a rendered chat reply.
