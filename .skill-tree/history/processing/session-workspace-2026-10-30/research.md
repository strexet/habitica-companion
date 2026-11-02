# Session settings and Workspace Pending research

Scope: standalone Process Pending for both entries in selection.json. Only planning definitions and research evidence change. No feature implementation, build, runtime test, real account request, migration, publication or commit occurs. Source fingerprints bind inspected files; inspection includes targeted sections rather than a whole-repository audit.

## Verified session behavior

| Source | Observation | Resulting requirement |
| --- | --- | --- |
| ChatGptIntegrationClient.cs | Profile ID and management bearer live in instance fields; ForgetSession clears both. Status requires the bearer. RevalidateAsync calls EnrollProfileAsync. | Restore verified existing access separately from initial enrollment. Missing tab access is not proof of missing server enrollment or grants. |
| vault.ts enroll | Verification encrypts credentials, rotates the single management token, rewrites consent timestamps and can create or reactivate a profile. | Do not use enrollment as automatic session recovery. Recovery must not rotate unrelated tab access, rewrite consent or revive a revoked profile. |
| WorkspaceCloudClient.cs and NotesCloudClient.cs | Each connection stores an in-memory token and invokes vault enrollment before the corresponding consent-bearing connect request. | Recover Workspace and Notes access independently under retained server consent, without granting either permission from a saved preference. |
| Workspace index.ts connect and notes-api.ts connect | Connect can create or reactivate retained stores. Workspace sessions expire after eight hours; active-connection executor settings can survive reconnect. | Add existing-only recovery with epoch and current-consent checks. Preserve server execution settings and actual grant state. |
| SettingsPage.razor, WorkspaceSettingsPanel.razor and NotesPanel.razor | Consent acknowledgements are transient form fields. Workspace renewal repeats disclosure; Notes has a separate source selector. | Display persisted server state separately from new acknowledgements. Syncing checkbox booleans would not establish valid server permission. |
| CloudSyncSectionMapping.cs and StorageKeys.cs | Encrypted private sync covers listed snapshots, presets, task order, color schemes and diagnostics; connection descriptors and active canonical source are absent. | Introduce versioned account-bound non-authorizing preferences and a recovery descriptor; never serialize management, Workspace, Notes or OAuth bearers into generic sync. |
| TodoWorkspacePresentation.cs and TodoWorkspacePresentationTests.cs | Browser workspaces and the cloud workspace are enumerated independently; multiple local workspaces require exact choice. | Provide one ordinary active source. Automatically create/select the local default when unambiguous; handle legacy multiple-store consolidation explicitly. |
| NotesController.cs | Account changes reset Notes to Browser; cloud source and dirty/pending draft guards are independent of Todo selection. | Share active-source orchestration and retain draft/original-command protections. Cloud unavailability must not silently create a writable browser fork. |
| Fresh_tab_status_is_unknown_until_explicit_revalidation_without_a_new_link_ticket, Independent_tabs_require_explicit_connection_and_reread_actual_executor_state and ConnectedWorkspaceRenewalRequiresExplicitDataConsent | Tests deliberately enforce manual per-tab restoration. | Replace the intended behavior with negative controls for no enrollment, grant expansion, consent replay or executor activation during recovery. Existing tests do not qualify the requested behavior. |

The source explains why new sessions need setup again. No fresh affected-client session was observed, so server consent loss, exact deployed build behavior and a device-specific failure remain unproved.

## Lossless adoption and identity

WorkspaceHandoffService durably freezes a local task journal, preserves one transfer ID and marks the local copy cloud-owned only after acknowledgement. The server handoff retains an original hash/receipt, but requires an empty target, disabled executor and no active lease. Existing tests cover restart, lost acknowledgement and transaction failure. The archive does not contain the personal Notes store.

NotesModel derives browser ownership from the local account and cloud ownership from the pseudonymous integration profile. A project link includes exact project ID, Workspace ID, source and owner scope; retained versions and payload-hashed receipts preserve the original values. NotesService.ImportAsync refuses a different owner or a nonidentical existing notebook. Workspace journal validation binds original commands to the original Workspace ID. Naive copying, changing every ID or recreating notes/tasks through ordinary commands would lose provenance or fabricate events.

Adoption therefore needs a versioned transaction and provenance contract spanning task journals and Notes. Preserve exact source bytes, record IDs, hashes, associations, tombstones and original receipts; use explicit migration mapping where source ownership changes. Preserve original lookup identities rather than rewriting accepted commands. Distinct records must not be deduplicated by title. Existing cloud records and disjoint local records must survive consolidation; conflicting identities require an explicit review, never overwrite or guessed ownership. New imported effect intents remain paused for review. Existing cloud consent does not authorize replaying source effects.

The browser must freeze both stores before staging an immutable transfer. A server receipt commits cloud data before the browser acknowledges transfer and publishes the selected cloud source. Original receipt recovery completes interrupted acknowledgement; no new transfer ID follows uncertainty. Local frozen copies can remain recovery evidence without appearing as another ordinary writable Workspace. Capacity failures preserve source data and refuse truncation.

MCP currently exposes task, organization and Notes command workflows but no complete cloud adoption preview/accept/original-transfer receipt workflow. Agents cannot read IndexedDB through MCP. Parity must operate on a bounded owner-staged artifact and exact transfer identity, with independent migration permission and Notes grants when Notes is included. An agent must never fabricate browser freeze evidence or infer access to local records.

## Settings inventory boundary

Persist account choices and recovery references. Read integration grants, Workspace/Notes consent, active source and executor status from verified authorities. Keep new-consent acknowledgements, one-time codes, management/session tokens, drafts, browser notification permission and device subscription identities out of generic preference replay. Existing feature-specific consent remains independent. Audit the affected Settings, Todo Sync and Notes controls against the inventory; unrelated intentional device-only state is not a synchronization defect.

Legacy clients without a durable recovery reference need a clearly disclosed one-time existing-account recovery path. The path may establish future recovery metadata after verification, but must not call enrollment when the owner intends restoration. Missing, stale, revoked or unsupported consent remains explicit. Initial cloud selection includes separate server-readable Notes and credential-storage disclosures; choosing cloud is not an executor or OAuth permission grant.

## Optional return to browser

No reverse ownership-transfer endpoint or Notes-wide transfer authority exists. Portable Workspace restore explicitly pauses execution, Notes import requires the same owner scope, and disconnect alone does not atomically freeze/export all records. Export plus source switching would therefore not implement a safe move. The optional simple return is not a small extension of existing behavior. Defer reverse transfer from the required forward-adoption scope; preserve cloud export and explicit pause/disconnect controls without advertising a completed return. A later owner selection can define reverse fencing, retained cloud data and multi-device behavior.

## Official platform context

Reviewed on 2026-10-30:

- [Microsoft Blazor WebAssembly security](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/) establishes the untrusted client boundary. Persisted preferences cannot replace server authorization. The version-specific 8.0 URL was inaccessible through the browsing tool; no version-specific claim depends on the inaccessible page.
- [Cloudflare D1 batch](https://developers.cloudflare.com/d1/worker-api/d1-database/#batch) documents transaction rollback when a statement fails. Transfer acceptance must retain explicit revision/epoch/commit guards and fixture evidence; separate IndexedDB and D1 transactions do not become one distributed transaction.

No new identity provider, credential-storage mechanism, package upgrade or assumption of an automatically renewed OAuth grant is selected.

## Decomposition and dependencies

All five outputs preserve P-66d0b256-307b-4a9d-babd-13d6e7edd9f6 provenance and priority107, above the other open work.

1. Existing-only server connection recovery supplies session and consent facts without enrollment.
2. Account preferences and shared source orchestration consume the recovery protocol and provide the source/transition state needed by migration.
3. Reviewed lossless adoption consumes the shared transition state and adds two-store freeze, cloud staging/acceptance and recovery.
4. MCP adoption/status parity consumes the reviewed server contract and original-transfer receipt.
5. Shared UI and end-to-end qualification consume the application and MCP contracts and qualify the complete owner journey.

Dependencies follow concrete protocol consumers, not shared Pending origin. Notes folders and hosted callback qualification are adjacent work, not prerequisites for session restoration or forward adoption. Future schema extensions such as folders require compatible preservation when present, without inventing an implementation dependency.

## Retained mass-spell incident

P-8e7323df-2f47-4e19-864d-821ed108e7f2 remains Pending at106. Fresh static inspection confirms the controller records acknowledged counts, failure stage, Retry-After/reset cooldown and unknown dispatch separately; AdaptiveRequestPacer and HabiticaApiClient serialize pacing/dispatch/header reconciliation. GearRecovery retains account-bound original slot identity and refuses unreviewed replacement operations. SpellOutcomesTests contains the eighteenth-call429/17of40 control and cancellation/unknown-outcome controls. No fresh test pass is claimed.

The retained 2026-10-29 release report explicitly excludes live spell reproduction. Existing accepted synthetic work and the earlier iOS processing evidence do not establish which build/controller the affected iPhone loaded or a current residual source defect. Retain the incident until sanitized post-update observations and loaded-client identity are available. Any real spell or equipment reproduction needs a bounded owner profile. No duplicate source implementation or live incident closure is produced.

## Processing boundaries

process-input.json records exact selection, dispositions, task contracts and the inspected queue hash. process-receipt.json supplies generated identities and dependency edges. Repository validators, task prose checks and diff checks qualify planning consistency only.

The existing Implement All checkpoint has a hosted callback execution stop and a frozen snapshot excluding the five new outputs. Preserve checkpoint-before.json exactly after the helper's documented processing bookkeeping; verify and remove only the new deferred IDs, processing receipt and phase/next-action changes. Do not reset the run, resume the stopped operation, expand the frozen snapshot or start another controller during standalone processing. Selecting new implementation later also needs an explicit resolution of the existing controller scope.
