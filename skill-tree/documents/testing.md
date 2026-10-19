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
