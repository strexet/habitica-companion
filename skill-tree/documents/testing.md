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
