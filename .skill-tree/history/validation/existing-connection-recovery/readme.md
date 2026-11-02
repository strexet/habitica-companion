# Existing connection recovery acceptance

Task: T-1481b473-244b-4918-b498-775fd1efcaf4. Controller: WR-13d5cdea-5809-4560-aa67-683a4416507c. Scope is the existing-only server protocol and typed clients, without automatic startup/sync integration, record adoption or changed UI routes.

## Delivered behavior

- Auth verifies current credentials against existing enrollment and issues independent eight-hour management sessions. Recovery changes no profile credentials, consent, connection identity, OAuth grants, linking tickets or executor settings.
- Workspace and Notes issue sessions only for existing active independently consented stores. Optional exact IDs, connection/access epochs and final authority checks guard every response. Existing records and original receipts remain intact.
- Recovery metadata contains only a versioned pseudonymous routing reference. The typed client requires a descriptor or an explicit legacy-bootstrap acknowledgement before sending credentials, validates bounded responses and expiry, excludes bearer tokens from public results and discards delayed account responses.
- Explicit root management rotation and full revocation invalidate recovered authority. Grant-only revocation retains owner management access but does not recreate OAuth grants. Conditional session pruning cannot remove a newer parallel session after stale issuance fails.
- Auth0013 adds hashed recovery-session storage and bounded/expired cleanup. No new key, database, provider, package upgrade or feature activation is required.

## Final-source checks

| Check | Result | Evidence |
| --- | --- | --- |
| Auth types and local dry build | Pass | reviewed-auth-build.json and .log |
| Workspace types and local dry build | Pass | reviewed-workspace-build.json and .log |
| MCP types and local dry build | Pass | reviewed-mcp-build.json and .log |
| WebApp integration tests | 219 passed, including 14 new recovery cases | client-tests-final.json and .log |
| Auth recovery/vault/management/connection/OAuth continuity | 34 passed, including 13 recovery cases | reviewed-auth-tests.json and .log |
| Workspace/Notes/executor/project-link/operations regressions | 41 passed | workspace-regression-tests.json and .log |
| Task/documentation/index/dependency and prose checks | Pass; historical audit remains stale | repository-validation.json |
| Build identity and whitespace | Pass | repository-validation.json |

The earlier full Auth run passed226 tests while implementation/review was progressing. The final-source focused Auth result above includes the additional concurrent-pruning controls and supplies current acceptance evidence; the earlier full run is supplementary evidence rather than a final whole-suite claim. Runtime fixtures use disposable local D1 and synthetic upstream services. No real account request, game effect, remote migration, hosted publication or device operation ran.

The first new server fixture used a raw Note command where the API requires a notebook-bound envelope. The corrected fixture passes; the original failing log is retained. Initial type checking identified the existing consent_version field missing from the WorkspaceRow type. Review found the stale-session pruning race and added explicit successful-issuance guards plus two passing parallel-session controls. No two unchanged unresolved executed failures triggered the stop rule.

## Contract clarification

The generated task originally requested distinct deleted and never-enrolled states. The existing privacy contract removes attributable audit metadata on profile deletion. The task now records the evidence-backed distinction: both states report absent enrollment, known session expiry comes from issued-session metadata, and retained revocation remains explicit. No deletion-tracking retention policy was introduced; the recorded acceptance criterion remains unchanged.

## Workflow and remaining scope

The owner selected the exact Work Through Next task after the explicit checkpoint-switch recommendation. scope-switch.json records the transition. The old Implement All checkpoint remains intact at skill-tree/history/checkpoints/implement-all-WR-5663ac54-9d1e-4145-8792-17c83b2b8bd3.json, including the hosted callback stop. The current run contains only the selected task, captures no required or nonblocking Pending work and starts no dependent root automatically.

The remaining priority107 units own synced account preferences/shared source, lossless browser-to-cloud adoption, reviewed MCP adoption parity and final single-Workspace presentation. New-session UI behavior is not fixed by the protocol foundation alone. No commit or deployment occurs. Auth0013 must be applied during a separately authorized release before serving the new Auth code; physical-device, live-account and installed-host qualification remain unrun.

timing.json separates controller elapsed/approximate active time, recorded tool-process wall time and prerequisite waiting. Every recorded build/test control has a hard deadline and cleanup reserve. The timing approximation includes source/document tool overhead and establishes no cumulative task budget.

## Platform references

[Cloudflare D1 batch](https://developers.cloudflare.com/d1/worker-api/d1-database/#batch) documents transactional rollback on statement failure. Conditional insertion and cleanup predicates remain necessary when a statement changes zero rows. [D1 foreign-key guidance](https://developers.cloudflare.com/d1/sql-api/foreign-keys/) supplies the migration context; fixture controls verify the actual local schema behavior. Both sources were checked on2026-10-30. No hosted result is inferred from platform documentation.
