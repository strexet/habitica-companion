# Deploy to Cloudflare Pages

This app is a Blazor WebAssembly app with Cloudflare Pages Functions for personal encrypted user-data sync and shared party CRON sync. Cloudflare Pages can build the app from GitHub and serve the published static files from the `*.pages.dev` domain.

## Owner-first launch readiness

The owner development rollout is published directly to [habitica-companion.pages.dev](https://habitica-companion.pages.dev). The selected environment is production, with no separate dev website or backend names. The current manual Pages publication uses dirty local `develop` source at `adbfcc5` in the existing `main` production slot; no Git push occurred. Git deployment settings remain unchanged. [Plugin authorization publication evidence](../history/validation/plugin-authorization-prod-20261025.json) records the current Auth, Workspace, MCP and Pages versions and public checks. Template defaults and earlier rollout observations remain historical.

Auth, Workspace and MCP Workers are published. Production integration OAuth KV and Auth/Workspace D1 resources are separate from the retained browser-sync KV and party database. [Publication evidence](../history/validation/production-publication.json) records the deployed versions, bindings, build and public boundary checks. [The operator guide](../../deploy/README.md#current-owner-action-sign-in-and-connect) provides exact owner sign-in, consent and client setup steps.

Hosted public checks establish HTTPS, routed-page refresh, production frontend settings, backend health, OAuth discovery, anonymous-access denial and exact-origin CORS. The signed-out Settings page renders integration disclosures and sign-in requirements. The owner reports initial OAuth completion in ChatGPT and Codex, ChatGPT discovery of 25 tools and two successful reads. ChatGPT requests reconnection on the next request within one minute. Repeat-request and refresh continuity, private packaged skills, multi-user isolation and mobile-device checks remain unqualified.

The [native Codex pilot](../history/validation/codex-native-owner-pilot.json) qualifies all six account/task read tools, immediate account/task/account reuse, a successful read after 617 seconds with stable profile identity and one explicitly approved Companion-only Todo creation with readback, search and original receipt lookup. The live Auth read-log sample contains only the expected operational field schema; raw tail transport envelopes and request headers are discarded before persistence. Live gameplay flags remain disabled. ChatGPT retesting, complete token lifecycle/revocation, interrupted acknowledgement recovery, packaged skills and multi-user/mobile gates remain open.

### Completed publication prerequisites

- [x] Local traffic/operational protection and E14 adoption/recovery controls pass; [local evidence](../history/validation/e14-local-qualification.json) remains separate from real account qualification.
- [x] The owner-selected project, service origins, production slot, source commit and rollback identity are recorded. Existing browser KV and party D1 bindings are retained; no existing browser records were changed during publication.
- [x] Auth and Workspace migrations `0001`–`0007` are applied. The retained party database has no pending migrations.
- [x] All three required secret names are present on Auth; no values appear in public settings or evidence. The current credential write version is `1`; version `2` is provisioned for future guarded rotation.
- [x] The release app build passes and deployed frontend settings match the reviewed production bundle. Pages account selection uses `CLOUDFLARE_ACCOUNT_ID`; Pages configuration omits the unsupported `account_id` field. Worker configuration retains the account field.
- [x] Deployed logging settings disable invocation logs and traces; no Logpush destination or tail consumer is configured. Application read handlers emit structured correlation/tool/route/status/duration/rate metadata. Read-log payloads exclude credentials, account identifiers, task text and raw bodies; production log inspection remains a release gate. Provider retention has conservative upper bounds of seven days for logs and thirty days for D1 recovery; exact plan lookup is unavailable with the current OAuth scope. Application operational metadata has a separate thirty-day cleanup policy.
- [x] Deployment identities and rollback instructions are recorded. Website rollback cannot reverse database changes or confirmed Habitica actions. The operator can stop new enrollment or cloud access by disabling flags and redeploying the reviewed backend configuration without deleting accepted history.

### Initial capability selection

Enrollment, OAuth and Workspace are enabled in the selected production bundle, behind separate per-user consent. Enable authenticated party planning separately after membership and consent checks. Keep `CRON_ENABLED` false in Auth and Workspace. Keep private key operations disabled. Start with projection, spells and the background executor disabled; activate selected real-effect lanes only with explicit test scope and reviewed actions. Separate Cloudflare storage does not create a separate Habitica account: enabled scoring, projection and spells affect the enrolled real account.

The first owner pilot covers account/task reads and Companion-only canonical task saves. Existing browser scoring, equipment and spell controls still affect the real Habitica account; browser gameplay actions remain outside the initial pilot.

### Hosted smoke checks before owner handoff

Required owner-pilot clients: ChatGPT and Codex desktop. Both clients use the same production MCP backend with independent OAuth client registrations and grants. Record each client/version, transport, selected scopes, callback mode and sanitized result in the [client qualification matrix](../../plugin/README.md#client-qualification-matrix). Codex CLI can provide additional diagnostics; CLI success does not replace the desktop check. Account enrollment in one client does not qualify another client. Private plugin installation and skill activation require separate observations from direct MCP connectivity.

- [x] Publish the release build to the selected website and verify HTTPS, routed-page refresh, signed-out Settings, static configuration and disabled cloud effect flags. Existing browser bindings are retained; real legacy-data continuity still requires the owner browser session.
- [ ] Verify OAuth discovery, exact audience, PKCE, consent cookies and Origin/CORS separately for ChatGPT and Codex desktop against deployed addresses. Verify Codex loopback callbacks, selected ports and issuer validation for the actual client registration flow. Complete explicit account enrollment through the consent UI; never import credentials from chat or test fixtures.
- [ ] Qualify the private plugin and all three bundled skills in both clients, using explicit invocation and matching natural-language requests in fresh chats. Record actual host availability and any failed installation or activation gate.
- [ ] Save and read a canonical task, recover the original command receipt after an interrupted response, export/restore a backup and exercise revoke/disconnect/delete controls. Verify rejected cross-profile access and retained uncertainty without duplicate effects. Exercise real effects only in selected lanes.
- [ ] Confirm safe 429/backoff behavior, stalled/unknown status, secret-free logs and the stop/rollback procedure. Record unresolved hosted limitations for the owner test session.

Real multi-user isolation and mobile-device qualification require a second consenting tester and selected browser/device lanes. The selected production pilot supplies the venue for explicitly scoped multi-user and mobile release checks; the broader release gate remains open until evidence satisfies the corresponding task. Pilot preparation does not require completion of unselected planner/UX epics or the nonblocking parallel Miniflare investigation.

## Before editing deployment behavior

Read `skill-tree/documents/rules.md`, `skill-tree/documents/technical.md`, this document, `wrangler.toml`, `build.sh`, current Functions under `functions/api`, and migrations under `migrations`. If Cloudflare Pages, KV, D1, Wrangler, or Pages Functions behavior is unclear from this repo, verify current official Cloudflare documentation before editing commands or operational steps.

## Repository files

- `build.sh` - installs the pinned .NET SDK, installs npm dependencies, syncs vendored Dexie, and publishes the Blazor WebAssembly app.
- `.node-version` - asks Cloudflare Pages to use Node.js 22.
- `functions/api/sync/[syncId].js` - Cloudflare Pages Function for legacy single-blob encrypted sync (read-only fallback during migration).
- `functions/api/sync/[syncId]/section/[sectionKey].js` - Cloudflare Pages Function for per-section encrypted sync uploads and downloads (active path).
- `functions/api/sync/[syncId]/sections.js` - Cloudflare Pages Function listing all section keys for a sync id.
- `functions/api/party-sync/[partyId].js` - Cloudflare Pages Function for shared party CRON data, quest planning, and party-sync management, protected by local claims with optional tokenized invite proofs.
- `migrations/0001_party_sync.sql` and later numbered migrations - D1 schema for party state, CRON events, quest queue/vote tables, management roles, and automatic quest completion detection.
- `wrangler.toml` - local/dev binding declarations for KV and D1.

## Cloudflare Pages settings

Use these settings when creating the Pages project:

```text
Framework preset: None
Build command: ./build.sh
Build output directory: output/wwwroot
Root directory: repository root
Production branch: main
Deploy command: leave empty
```

The build script installs .NET SDK `8.0.125` by default to match `global.json`. Override it only if `global.json` is updated in the same change.

Do not set `npx wrangler deploy` as the deploy command for the Git-connected Pages project. That command deploys a Worker and does not know which static output directory to publish. Cloudflare Pages should publish the configured `output/wwwroot` directory after `build.sh` finishes.

## Cloudflare sync storage

Personal encrypted sync requires a KV namespace bound to the Pages project.

1. In Cloudflare, open `Workers & Pages`.
2. Open `KV`.
3. Create a namespace, for example `habitica_companion_sync`.
4. Open the Pages project settings.
5. Open `Bindings`.
6. Add a KV namespace binding:

   ```text
   Variable name: HABITICA_SYNC_KV
   KV namespace: habitica_companion_sync
   ```

7. Add the same binding for preview and production environments if you use both.

The personal sync Pages Functions store only encrypted payloads. Habitica User ID and API Token are used in the browser to derive the encryption key and sync id; they are not sent to the personal sync endpoints.

Cloud sync uses per-section KV records (`sync:{syncId}:section:{sectionKey}`). Each section is encrypted and uploaded independently, staying within the 2MB per-key KV limit. When the app refreshes Habitica data or completes supported local data changes, it automatically lists remote sections, downloads and merges each section into local data, then uploads each local section back to this KV namespace.

Legacy single-blob records (`sync:{syncId}`) from older deployments are automatically migrated: the app detects no sections exist, downloads the legacy blob, imports it locally, and re-uploads as individual sections. The legacy blob remains in KV but is no longer updated. No manual migration is required.

Shared party sync requires a D1 database bound to the Pages project.

1. In Cloudflare, open `Workers & Pages`.
2. Open `D1 SQL Database`.
3. Create a database, for example `habitica-companion-party-sync`.
4. Apply all migrations in order: `migrations/0001_party_sync.sql`, `migrations/0002_party_quest_queue.sql`, `migrations/0003_quest_lifecycle.sql`, `migrations/0004_party_sync_management.sql`, `migrations/0005_app_admins.sql`, `migrations/0006_detected_quest_completion.sql`, `migrations/0007_queue_selection_controls.sql`, `migrations/0008_party_sync_invite_proofs.sql`.
5. Open the Pages project settings.
6. Open `Bindings`.
7. Add a D1 database binding:

   ```text
   Variable name: HABITICA_PARTY_DB
   D1 database: habitica-companion-party-sync
   ```

8. Grant app-admin permissions (separate from party owner; grants management access across all parties) by copying `migrations/seed_app_admins.example.sql` to `migrations/seed_app_admins.sql` (gitignored), filling in Habitica user IDs, and running `wrangler d1 execute habitica-companion-party-sync --file migrations/seed_app_admins.sql`. Revoke by setting `revoked_at_utc` on the row.

The party sync Function must not receive Habitica API tokens. Browser-only `local-claim-v1` remains the default and owner/app-admin recovery path. Migration `0008` adds optional hashed `tokenized-invite-v1` proofs: owner/app admins can issue, rotate, revoke, remove, enable, and disable proofs from Party settings. D1 stores SHA-256 token hashes, labels, issuer metadata, and lifecycle timestamps, never raw reusable proof tokens. Apply migrations through `0008` before deploying Function code that reads invite-proof settings or tables.

## Optional Auth/MCP deployment proposal

The [proposed ChatGPT enrollment, privacy and retention contract](companion-first-mcp-research.md#proposed-chatgpt-enrollment-privacy-and-retention-contract) proposes separate Auth/MCP Workers, OAuth KV and integration D1 resources. Existing Pages resources remain dedicated to private and party sync. The selected production integration resources and Workers are provisioned and published; real account enrollment remains unrun. Local implementation approval and live deployment scope are separate. Provider log/backup retention must be verified before activation.

## First deployment

1. Push this repository to GitHub.
2. In Cloudflare, open `Workers & Pages`.
3. Create a Pages application and import the GitHub repository.
4. Use the build settings listed above.
5. Retain the committed project-owned `Habitica:XClientHeader`, or set a public creator/app override for the frontend build:

   ```text
   HABITICA_X_CLIENT_HEADER=your-habitica-user-id-habitica-tool
   ```

   `build.sh` applies the override to the static frontend configuration. The API client rejects missing or blank configuration before network access. The header never derives from the signed-in user. The value is public in the deployed frontend. Do not put Habitica API tokens, passwords, or private keys in Cloudflare Pages environment variables for this static app.

6. Save and deploy.
7. Open the generated `https://<project>.pages.dev` URL.
8. Check a routed page refresh, for example `https://<project>.pages.dev/tasks`.
9. In Settings, use `Upload` under encrypted Cloudflare sync. If the KV binding is missing, the upload will fail with a `HABITICA_SYNC_KV binding is not configured` error. If the D1 binding is missing, shared party sync is skipped and logged as a warning.

## Local Functions testing

Use Wrangler Pages dev for local Function parity with KV and D1 bindings:

```text
wrangler pages dev output/wwwroot
```

Create local KV/D1 resources or override the placeholder IDs in `wrangler.toml` before relying on local persistence.

## Existing browser migration

Older deployments registered a service worker and used a `_redirects` file. Current deployments rely on Cloudflare Pages native SPA behavior and do not register a service worker. Existing users should unregister the old service worker, clear site data, and hard refresh once after redeploy.

## Updates

Push to the production branch and Cloudflare Pages will rebuild automatically.

## Operational notes

- A custom domain is optional. The generated `*.pages.dev` domain works for HTTPS testing.
- The app must be served from the site root with `base href="/"`. Subpath hosting is not the baseline deployment target.
- Personal sync keeps Habitica credentials in the browser and stores only encrypted KV blobs.
- Shared party sync sends browser-local claims or optional tokenized invite proofs to the Pages Function and stores shared party data, roles, settings, kick records, and hashed proof metadata in D1.
- `service-worker*.js` files may remain in the repo, but `index.html` does not register them.

## Dedicated Worker local validation

`workers/chatgpt-auth/` and `workers/chatgpt-mcp/` own package manifests, lockfiles, generated binding declarations and Wrangler configurations. `workers/shared/resources.ts` owns the exact issuer/resource pair per environment. Source-default remote URLs and resource IDs are inert placeholders; generated dev configurations supply reviewed public runtime overrides before separately authorized deployment. No resource provisioning occurs during local validation.

```sh
cd workers/chatgpt-auth
npm ci
npm run build
npm run build:preview
npm run build:production
cd ../chatgpt-mcp
npm ci
npm run build
npm run build:preview
npm run build:production
cd ../chatgpt-auth
npm test
```

Build scripts use Wrangler dry-run. Fixtures use Miniflare with a named `AuthService` binding and isolated integration D1. Protected MCP paths require valid authorization; enrollment and OAuth are disabled in all shipped configurations. Auth alone owns `OAUTH_KV`, integration D1 and declared secret names. Preview and production use distinct integration resources and Auth Service bindings. Existing Pages resources are excluded. Secrets belong in ignored local `.dev.vars` files or separately approved Worker secret provisioning, never source/configuration.

## Credential vault local qualification

`workers/chatgpt-auth/migrations/0001-integration-profiles.sql` initializes integration-only storage. `npm test` applies the schema to disposable Miniflare D1 databases; no remote migration or real enrollment runs. `PROFILE_ID_KEY`, `CREDENTIAL_KEY_V1` and `CREDENTIAL_KEY_V2` require distinct random 256-bit base64url values in separately authorized secret provisioning. Profile derivation keys must remain stable; encryption write versions may rotate while required read keys remain available. Public configuration contains only key-version selection and the tool creator header.

`ENROLLMENT_ENABLED` defaults to false in local, preview and production configurations. Enabling enrollment is a separate activation decision after the consent UI, authorization checks, endpoint limits and deletion/revocation qualification exist. The vault supports enrollment, profile-scoped disconnect and profile deletion. Request bodies are bounded; responses omit upstream bodies and secret details.

## Browser consent local qualification

The Auth consent page uses `Referrer-Policy: same-origin` so browser form navigation preserves the exact issuer Origin without disclosing a referrer to another origin. The Auth handler rejects foreign and opaque Origin headers. Provider consent cookies retain Secure and HttpOnly protections. Consent form policy allows the Auth origin and the callback origin derived from the provider-validated request, so browser-enforced form redirects can return to the client. Exact registered redirect validation, PKCE, single-use binding, no framing and foreign/opaque Origin refusal remain enforced. Callback origins containing CSP wildcard or directive characters are rejected. A synthetic account completed first-click approval and denial redirects from `http://localhost:8787` to the separate local callback in the Codex in-app browser; manual HTTP fixtures alone do not establish browser compatibility. HTTPS deployment and other browser families require separate rollout qualification.

## Optional linking configuration

`0002-linking.sql` initializes ticket, consent-link and attempt-limit tables in the dedicated integration D1. Both Workers require explicit `OAUTH_ENABLED`; Auth enrollment additionally requires `ENROLLMENT_ENABLED`. The PWA requires a root Auth URL in `ChatGptIntegration:AuthBaseUrl`. Public PWA origin, issuer and resource are exact environment-specific entries in `workers/shared/resources.ts`; management CORS admits the configured PWA origin only. Committed default templates retain inactive placeholder addresses. The reviewed ignored production bundle supplies deployed HTTPS URLs and resource identifiers; local fixtures use disposable environments.

Activation requires secret provisioning, retention cleanup qualification, provider log/backup review and remote HTTPS/browser OAuth checks. Local test approval grants no live deployment or real enrollment. Management state remains in tab memory; lost management access requires Habitica revalidation. Cleanup blocks access before grant deletion and returns a retryable pending result if bounded grant cleanup cannot finish.

## MCP read qualification

`0003-read-state.sql` adds per-profile connection cooldown and repair state to integration D1. Local fixtures apply all three integration schemas. Build both Workers before running the Auth test suite because fixtures exercise the actual MCP service binding. MCP accepts stateless Streamable HTTP POST requests at `/mcp`; other methods return 405. Allowed Origins are the resource origin, configured PWA origin and `https://chatgpt.com`; absent Origin remains valid for native clients. Tool output omits server credential material. Local Inspector 1.0.2 CLI successfully lists tools and reads a synthetic profile. No real server enrollment or remote activation occurs during qualification.

## Optional Workspace Worker package

`workers/companion-workspace` is a third isolated Worker package with separate D1 migrations and `WORKSPACE_DB` binding. The `AUTH_IDENTITY` service binding targets the Auth Worker's private `WorkspaceIdentityService` entrypoint. The Workspace Worker has no credential keys or integration database binding. Apply Auth migration `0004-management-identity.sql` and Workspace migrations `0001-workspace.sql` and `0002-execution-ownership.sql` only inside an explicitly authorized deployment lane.

Local compilation uses `npm --prefix workers/companion-workspace run build`; preview and production scripts also produce dry-run bundles. Shipped `WORKSPACE_ENABLED` flags are false in Auth and Workspace configurations. The PWA leaves `CompanionWorkspace:BaseUrl` empty. ChatGPT linking additionally requires `ChatGptIntegration:Enabled` and the configured Auth URL; cloud workspace access requires the Auth URL without enabling ChatGPT linking. Supply the complete exact runtime origin tuple through generated dev configuration before an authorized deployment. Placeholder database IDs and remote endpoints are not deployable configuration. The implementation does not provision resources, apply remote migrations or activate an executor.

The private projection gateway requires Auth migration `0005-projection-gateway.sql` before the updated Auth package runs, including retention events. `AUTH_PROJECTION` on the Workspace Worker targets the Auth `ProjectionService` entrypoint. The Auth package defines hourly operational-metadata cleanup; local fixtures invoke the scheduled handler directly. The selected production bundle deploys the maintenance schedule with `PROJECTION_ENABLED` false. Production activation must preserve the separate workspace/executor consent gates, qualify cleanup and verify real task preconditions in the authorized rollout lane.

## Hosted execution activation boundary

The Workspace configuration declares a minute cron and keeps `EXECUTOR_ENABLED` false in every environment. The local implementation has deployed no schedule or Worker. Activation requires an authorized deployment lane, isolated resources, the current Auth/Workspace migrations and qualified service bindings. Workspace access, Auth projection access and Workspace executor configuration must all be deliberately enabled. Configuration alone cannot enable a profile: Settings requires separate `executor-v1` consent.

The executor processes one eligible profile and at most one effect per scheduled activation. The bounded initial throughput requires rollout measurement before broad adoption. Pausing or disconnecting blocks new calls while preserving accepted data; an upstream request already transmitted may finish. A credential-context change leaves uncertain original effects fenced. Workspace deletion removes runtime and handoff rows through cascading foreign keys. User history remains until workspace deletion; thirty-day operational Auth metadata has an independent retention policy.

## Canonical command service wiring

The current Workspace package requires migration `0003-prepared-commands.sql` in addition to the initial workspace/ownership migrations. The MCP `WORKSPACE_SERVER` binding targets the Workspace `WorkspaceCommandService` named entrypoint in the matching environment. The public Workspace handler never exposes that private entrypoint. Auth supports explicit `habitica.tasks.write` grants; older read grants remain read-only. The new package and scope implementation grant no deployment authorization and enable no shipped flag.

Workspace scheduled cleanup removes expired unaccepted preparation payloads in bounded transactions before optional execution. Accepted preparation bindings remain with canonical command history until workspace deletion. Qualify cleanup timing, archive accounting, scope escalation and service identity in the authorized remote rollout lane. Browser and MCP command acceptance remain available only after the separate workspace consent and configuration gates.

## Optional verified party read binding

The Workspace environments declare `AUTH_PARTY` targeting the matching Auth Worker `PartyService` entrypoint. Auth and Workspace `PARTY_ENABLED` flags ship false. `party:read` is a separate OAuth consent scope; existing profile/account/task grants do not gain party access. The MCP Worker has no direct party proof or credential binding. Public Auth URLs cannot invoke the named entrypoint.

Build Auth, MCP and Workspace with the pinned package scripts before local synthetic party fixtures. The context tool requires an active cloud workspace but creates no shared planning namespace. Queue/voting consent and persistence remain separate required work. No real service activation is part of local build or test commands.

## Authenticated planning schema and controls

Apply Workspace migration `0004-authenticated-party-planning.sql` after migrations 0001–0003 before enabling party planning in an authorized environment. The migration creates only authenticated Workspace planning tables and does not touch party-sync D1 or credential-vault migrations. Auth and Workspace `PARTY_ENABLED` flags plus PWA `CompanionWorkspace:PartyPlanningEnabled` remain false in shipped configuration.

PWA activation requires cloud workspace access and separate consent bound to the displayed verified party. OAuth readers require `party:read`; queue/vote tools require the independently consented `party.planning.write` scope. Leader settings/deletion and management-authenticated own contribution export/removal use bounded Workspace routes. Management tokens remain tab-local. Shared planning records are not part of the task-only workspace export and are not removed by cloud workspace deletion; use the separate contribution/namespace controls. No task or migration command grants deployment authorization.

## Spell gateway activation boundary

Auth `SPELLS_ENABLED` ships false in local, preview and production. `SpellService` is a named private entrypoint and has no public route. The reviewed Workspace integration below adds a private dispatch binding and requires no new Auth migration. Existing OAuth grants do not acquire the new `habitica.spells.execute` scope automatically; approval copy discloses mana use, party effects and an automatic Habitica chat notice. Deployment, real enrollment and spell casting remain outside local synthetic qualification. Do not bind MCP directly to this write service; reviewed Workspace action persistence and recovery remain mandatory for activation.

## Reviewed spell action schema and bindings

Apply Workspace migration `0005-reviewed-spell-actions.sql` after migrations 0001–0004 before enabling reviewed spell tools in an authorized environment. Each Workspace environment binds `AUTH_SPELL` to the matching Auth `SpellService`; MCP has no direct dispatch binding. Auth and Workspace `SPELLS_ENABLED`, plus the existing Workspace/party flags, ship false. No deployment occurs during local qualification.

Spell previews require separately activated authenticated planning consent and an explicit OAuth `habitica.spells.execute` grant. The hosted background-executor opt-in does not enable spell scheduling; casts require exact foreground preview approval. Operators preserve the action table alongside task history when backing up Workspace D1. The management `/spells/history` projection exports only the actor's normalized history; execution tokens and private dispatch requests are excluded. Explicit Workspace deletion cascades this private history. Shared planning deletion revokes unaccepted previews while preserving accepted private game-effect receipts and uncertainty fences. No live smoke test or host activation is implied.

## Private Cron activation boundary

Auth `CRON_ENABLED` ships false in local, preview and production. The named `CronService` entrypoint has no public route and no production MCP dispatch binding. No new Auth migration or real deployment is performed by local qualification. OAuth `habitica.cron.execute` is a separate explicit consent scope with missed-Daily damage, buff/task reset, quest progress and no-replay warnings. Existing read/task/spell grants do not acquire Cron execution. Workspace reviewed persistence and receipt recovery are locally implemented. Real multi-user activation qualification remains mandatory before live exposure.

## Reviewed Cron deployment boundary

Workspace migration `0006-reviewed-cron-actions.sql` defines typed previews and retained Cron receipts. `AUTH_CRON` binds only Workspace to Auth's private `CronService`; production MCP has no direct Cron binding. Both `CRON_ENABLED` flags remain false in local, preview and production. The existing minute Worker wakeup runs only the separately enabled task executor and never Cron. Applying migrations, setting live flags, deploying and real account/device qualification require separate authorization. Private Cron history is an additional own-data export at `/cron/history`, protected by management authentication.

## Grant revocation management deployment

Apply Auth `0006-grant-revocation.sql` and Workspace `0007-mcp-grant-epoch.sql` before deploying the corresponding management code. Existing credential enrollment remains, while pre-epoch OAuth tokens require relinking. Keep old credential-encryption keys available; grant revocation does not migrate envelopes. Management status uses GET with the existing allowed PWA origin. Grant-only revocation retains credentials and ordinary Workspace/background access; use Workspace disable/disconnect to pause the executor. Provider cleanup is bounded and can report pending cleanup; the D1 epoch denies old tool/token access immediately. Production log checks and real multi-user rollout remain unrun gates.

## Guarded credential encryption-key rotation

Apply Auth `0007-credential-key-jobs.sql` before the operator code. `KEY_OPERATIONS_ENABLED` remains false in local, preview and production. `CredentialKeyService` is a private named entrypoint with no public route and no MCP/Workspace binding. A separately authorized operator environment must supply the reviewed private caller, target/current configuration and required secrets; keys never enter operation bodies. The local implementation does not provision that caller or perform a live migration.

The guarded operator procedure is:

1. Preserve the stable `PROFILE_ID_KEY`. Configure a distinct new credential-encryption key while retaining every referenced old key. Select the new current version for writes and stop old-version writers before removal qualification.
2. Inspect private `inventory`. Missing referenced keys or unsupported metadata block removal. Start one job with explicit `jobId` and `fromVersion`; repeat the same identity after a lost start acknowledgement.
3. Invoke bounded `step` calls with the original `jobId` and numeric `limit` from one to twenty-five. Busy ownership requires waiting for the active operator. Unreadable credentials require private diagnosis and repair; error responses never contain plaintext. Resume after interruption using the same job.
4. Require complete job status, then repeat inventory and `assert-removable` for the old version. Current keys, retained references, unknown inventory or unfinished dependent jobs block a removable verdict. Recheck immediately before separately authorized secret removal. External secret configuration remains an operator responsibility.
5. Verify retained credentials and normal scoped tool behavior in the separately authorized environment. Preserve only counts/version/job summaries. Inactive job metadata expires after thirty days; expired job history can be replaced with a fresh job because already migrated envelopes no longer match the old version.

Private request shapes contain only operation metadata:

```json
{"operation":"inventory"}
{"operation":"start","jobId":"<uuid>","fromVersion":"1"}
{"operation":"step","jobId":"<same-uuid>","limit":10}
{"operation":"assert-removable","keyVersion":"1"}
```

Each line is a separate private request. No scheduler performs key migration. Existing Auth maintenance only removes bounded expired job metadata. Live rotation, key deletion, caller provisioning and deployment remain separate operations.


### Repeated-request OAuth qualification

Bootstrap read metadata includes `profile:read` and the operation scope in both security-scheme locations. Refresh requests may inherit the already bound MCP audience when `resource` is omitted; explicit audience mismatches remain denied. Temporary profile-storage failures return a temporary OAuth error without deleting recoverable grants. The access-token lifetime remains ten minutes.

The owner retest uses a refreshed ChatGPT tool definition and a fresh authorization, then separate read-only requests for account status, tasks and account status again. Repeat account status after more than ten minutes to exercise refresh. Check Codex independently while retaining the ChatGPT connection. A repeated reconnect dialog remains a failed gate; record only the failing tool and approximate elapsed time, excluding codes, tokens and account content.


### Active Codex owner-isolated lane

Normal source/client defaults and the reviewed production frontend override use `Features:OwnerIsolatedTestingEnabled=false` and `Features:PartySyncEnabled=true`. Agent isolation requires an explicit test profile and restricts party management/Start New Day only; supported class spells retain normal eligibility in both profiles. Party departure remains prohibited in every profile. The owner authorizes the frontend permission correction and public asset/settings verification; no new gameplay test round is selected. Backend party, live Cron, spells, projection and background-execution flags remain disabled under separate activation gates.

## Adaptive admission schema

Auth migration `0008-upstream-pacing.sql` defines durable shared/profile admission buckets and an expiry index. Apply the migration before running the corresponding Auth bundle in an authorized deployment lane. Profile rows reference integration profiles with cascading deletion; the schema does not touch party D1 or Workspace records. `ADAPTIVE_PACING_ENABLED` is false in shipped local, preview and production configurations. Enabled local runtime fixtures exercise admission without remote resources. Production activation and real traffic qualification remain release gates. No additional service binding, secret or scheduled trigger is required.

## Recurring command compatibility

Recurrence intents and Companion-only skip/pause/resume require matching browser, MCP and Workspace bundles. Existing command identities remain valid; older clients can reject extended fields or appended operation names. Update clients alongside the server before using the corresponding tools. Recurrence/lifecycle commands require no additional D1 migration, service binding, secret or enabled background executor. Auth migration requirements, disabled Cron and independent live/client/device release gates remain unchanged.

## Native placement compatibility

planTask adds a command operation and optional planning patch using identity v4. Existing commands retain v1/v2/v3 identities, and no D1 migration is required. Older clients can reject new planning journal entries; coordinate application/Worker/package updates before enabling the workflow. The local implementation and dry builds do not deploy the feature or qualify real clients.

## Restoration-cycle data compatibility

The optional restorations document extension retains unused legacy defaults and requires no D1 migration. Strict clients without extension support reject documents that contain restoration data. Coordinate application and Worker updates before using cycle data; compatibility cannot be inferred from a successful dry build. No live activation or migration tooling is included in model qualification.

## Reviewed Todo restoration compatibility

restoreTodo and optional patch.restoreTodo use identity v5 while unused extensions preserve existing hashes. Matching application, MCP, Workspace and task-management package updates are required before use; strict older clients can reject extended journal commands. No D1 migration, new secret or enabled background executor is needed. Dry builds and synthetic tests supply local evidence only; deployment and real client qualification remain separate.

## Production user-action correction

The existing Pages production slot receives the tested frontend with ordinary user settings and preserved browser-sync/party bindings. The public settings and application asset hashes are verified against the release artifact. The correction requires no Worker publication, database migration, secret change, background activation or real Habitica mutation. Sanitized evidence resides in skill-tree/history/validation/production-user-actions.json. Existing automatic Git deployment can replace a manual artifact later; the source defaults and generator retain ordinary user permissions for subsequent builds.

## Todo relationship compatibility

relateTodo adds optional patch.relationships and identity v6 while unused extensions preserve v1-v5. Matching application, MCP, Workspace and task-management package updates are required before use; strict older clients can reject extended journals. No D1 migration, secret or enabled executor is required. Local synthetic qualification does not publish backend tools or production relationship controls. The separately published user-action permission correction remains a frontend-only deployment.

## Native organization-history compatibility

Optional organizationHistory is retained in the existing canonical document and introduces no D1 migration, new binding or secret. Matching application/Worker validators are required before using the extension; strict older clients can reject extended documents. Existing document/archive capacity and consent remain applicable. Model qualification and synthetic handoff tests publish no organization route or backend update. The verified production user-permission artifact remains unchanged by local model work.


## Organization adapter compatibility

Reviewed organization adapters require matching Auth/MCP/Workspace builds and browser code. workspace.organization.write is independently disclosed and selected on reauthorization; existing task-write grants retain the original permissions. Native history and request bindings remain in the existing canonical document and preparation table, requiring no D1 migration, secret, binding or enabled executor. Retention/grant cleanup must include the matching native-history awareness before exposing organization tools. Local synthetic qualification does not publish the adapters, enroll accounts or activate production organization controls.


## Todo planning-state compatibility

setTodoState uses optional patch.todoState and identity v7 with coordinated application/MCP/Workspace updates. Old clients can reject used extensions and require matching validators before state authoring. Unused extensions preserve earlier encodings. No D1 migration, secret, new OAuth grant or executor flag is required. Deployment permission does not qualify live account behavior; unrun release and mobile gates remain open.


## Selected owner development activation — 2026-10-19

The owner selected featureProfile development for the existing website and reopened own-account Codex tests. Matching Auth/Workspace/MCP/application bundles expose implemented capabilities before broader release qualification. Earlier disabled-flag statements describe template/pilot defaults; explicit development generation enables recognized capability flags and frontend authenticated planning. Per-account opt-ins, OAuth grants and reviewed actions remain mandatory. Existing profile/encryption keys and current key version remain unchanged. Guarded private key availability does not provision a caller or run a migration.

Auth migration 0008 supplies adaptive pacing before activation. Remote preflight checks migration state, secret names, retained bindings and aggregate opted-in executors/queue counts without reading secret values or account content. Party departure remains prohibited; agent party mutations, real Start New Day tests and key operations remain excluded. ChatGPT, independent accounts, mobile/device and human usability evidence remain unverified and do not block owner-selected development exposure. Sanitized publication and bounded owner-test evidence retain actual version identities and observations.

Publication completed from source aab2b87 with matching Auth, Workspace, MCP and Pages bundles. All 1,291 local checks passed; public settings, boot manifest and service-worker assets match the build. Auth migration 0008 was applied; retained bindings and secret names match preflight. Executor preflight found zero opted-in workspaces, and no consent was enabled during publication.

The reopened owner sequence verified native Todo create/edit/deadline/completion/archive, dated native Daily completion/archive and seven matching original command receipts. All native commands requested no Habitica projection. The website cast one self-only Defensive Stance, spending 25 MP, and changed headgear to Dandy Hat before restoring Nameless Helm and original equipment stats. No party mutation, Start New Day, background dispatch, real key operation or ChatGPT test ran. [Sanitized owner evidence](../history/validation/development-owner-checks.json) retains exact receipts and observations; cached Codex discovery has not qualified newer tool names.

## Party quest pool hotfix qualification

The 2026-10-20 update uses two transactional D1 statements for owner-pool replacement instead of a separate delete followed by one statement per quest. No migration or binding change is required. The complete handler uses 22 statements in the synthetic 52/300-type fixtures; other access roles can add fixed management reads. The previous production deployment is `d06b3032-a927-45e9-8de3-a3ae7cfb39d9`; code rollback does not restore database contents. Publication and artifact records reside in `skill-tree/history/validation/party-quest-pool-publication.json`. Production party mutation testing remains excluded; blocked public artifact inspection is recorded explicitly.


## Ready-feature production publication

Source db79a3d is published to the existing habitica-companion Pages production slot with matching Auth, Workspace and MCP Workers. The production-ready-publication evidence records exact versions and rollback predecessors. All runtime variables, resource bindings, current credential-key version and per-account executor consent remain retained. Auth and Workspace report no pending migrations. Production preparation passes 123 Auth tests, 374 Workspace tests, Worker typechecks/dry builds and the frontend Release build. The current frontend source has 961 passing solution tests from local qualification.

The hosted browser signs in, restores existing cloud management access and renders direct capture, hierarchy, Projects and Saved lists with the existing theme. Expired tab access reports unverified status; explicit verification restores access and a fresh server check reports zero pending, stalled, uncertain or blocked commands and zero expired execution leases. Read observations do not qualify native create/update/archive dispatch. Shell artifact/health GETs return HTTP 403; deployment metadata and browser behavior do not establish remote artifact hash equality. Party writes, Start New Day, real spells, key operations and independent-user/device checks remain excluded. The separately captured bulk-casting rate-limit and gear-restoration defect remains unresolved.

The staged publication archive is output/habitica-companion-plugin-db79a3d.zip. The package retains manifest version 0.1.0 and contains the production MCP URL plus five skills from the publication revision. Current repository task-management guidance adds the required correspondence contract; the publication archive does not contain the source guidance update. Existing custom MCP connections require refreshed tool metadata after tool-schema changes; packaged skills require an updated imported package. Installed-package version discovery and backend compatibility reporting remain Pending for a later release. No runtime version-discovery feature is introduced by publication.

The 2026-10-21 frontend repair publishes source f66410c as Pages deployment e1cefdbc-74ac-4b29-88d5-c1737fa894e0. The previous Pages deployment is 8733a2ee-8c12-4ed1-a6a0-d6021d57859f; Workers, databases, bindings, secrets and consent remain unchanged. Browser NFKC normalization replaces the unsupported .NET 8 browser operation. Thirty shared query cases pass in actual WebAssembly, and the previously crashing production saved list returns the matching active Todo. Native saved-list create, filter update and archive qualification remain recorded with original command identities in production-ready-publication.json. The owner-approved source checklist transfer separately confirms one of one Habitica projection effects; completion marks plan intake rather than implementation.


## Tag gateway rollout prerequisite

Auth migration 0009 must precede deployment of the matching tag gateway/Auth cleanup handlers. The migration stores separate tag consent epochs, short-lived checks, adopted identity and bounded original-attempt observations. Existing ProjectionService bindings and Workspace/Projection flags are retained; no new secret is required. Deploy matching Auth, Workspace and MCP adapters before exposing canonical tag controls. Old organization/task OAuth grants do not gain habitica.tags.write; end users choose that permission on reauthorization and separately enable habitica-tags-v1 after the matching consent UI is available.

The local gateway milestone performs no production migration, deployment, enrollment, scope upgrade or consent activation. Canonical tag reconciliation is implemented locally; matching browser consent and repair UI remains a subsequent task. Rollback must account for any retained tag identities/receipts; a code rollback does not delete Habitica tags or undo membership. Never delete native tags as a cleanup shortcut. Public Auth HTTP routes cannot dispatch the private tag gateway; the existing restricted service binding remains the transport boundary documented by [Cloudflare service bindings](https://developers.cloudflare.com/workers/runtime-apis/bindings/service-bindings/).

## Reconciliation deployment dependency

Apply Workspace migration 0008 together with Auth migration 0009 before deploying the matching tag executor. The Workspace migration adds trusted work, source review status and preparation authority records; no new secret, binding or flag is required. Deploy matching Auth, Workspace and MCP code and the following browser consent/recovery controls as one reviewed release. Existing native task and organization grants can still save canonical metadata, but existing grants cannot acquire habitica.tags.write implicitly.

No migration, live scope upgrade, consent activation or deployment ran during the reconciliation milestone. Retain unresolved original attempts across rollback; never delete native tags to clean up a failed release.


## Group-tag controls release dependency

The local tag UI/MCP release requires Auth migration 0009, Workspace migration 0008 and Workspace migration 0009, matching Auth/Workspace/MCP bundles and the matching browser application. Dry-run builds and synthetic tests do not publish resources or apply remote migrations. Existing grants retain original scopes; requested tag operations need normal reauthorization plus separate owner tag consent. No release activates consent or executor automatically. Confirm matching schema/tool discovery and explicit consent in the separately authorized live qualification before claiming production support.


## Checklist conversion release dependency

The optional checklistConversions document field requires matching browser, Workspace, Auth and MCP readers before cloud conversion is offered. Older strict readers can reject documents containing the new field. Deploy matching qualified assets as one release sequence; do not expose a backend-only conversion release. Existing preparations retain accepted conversion bindings, so no new D1 migration, secret, feature flag or enrollment is required for the conversion backend. Existing habitica.tasks.write and workspace.organization.write grants are both required for agent writes; owner tag consent and executor remain separate. Current local dry builds and synthetic fixtures grant no deployment or live-account qualification.


## Source retirement schema publication

Source retirement requires Workspace migration 0010-source-retirement.sql before the corresponding Workspace Worker. The migration creates trusted review/recovery storage and a durable source-retirement fence with a document-update trigger. Publish matching Auth, Workspace, MCP and PWA code before enabling use of sourceRetirements in portable journals. The private gateway extends the existing projection binding; no new secret or consent is created by deployment. Existing task, organization and tag permissions remain required. An unresolved retirement must recover through the original command; database edits, fence overrides and native DELETE retries are unsupported. Local dry builds and synthetic migration fixtures do not establish remote migration or production qualification.

## Notes service publication

Notes storage requires Workspace migrations 0011-personal-notes.sql and0012-note-previews.sql before the matching Workspace Worker. Auth configuration requires the restricted NOTES_LIFECYCLE binding to Workspace NotesLifecycleService. Publish Workspace before Auth so profile cleanup can reach the new entrypoint. The deployment preparation source rewrites the binding to the selected Workspace Worker. Matching PWA and MCP publication remains necessary for the complete Notes scenario; storage-only publication supplies no usable Notes page. Notes MCP tools require matching server discovery, independent read/write grants and Notes consent.

Notes consent remains independent of workspace-v1 and existing OAuth grants. Deployment never enables per-account notes-v1 consent or adds notes.read/notes.write to an existing grant. Integration profile deletion includes active Notes tables and reports pending cleanup until the private service acknowledges deletion. Provider point-in-time backups retain the provider recovery policy. Local synthetic migration and lifecycle tests do not establish production migration or live account qualification.

## Notes release candidate 0.2.0

The owner-selected sequence completes Notes, publishes accumulated ready work, then completes the three Todo presentation units. Unrelated prioritized work remains outside this release. The candidate uses package/backend0.2.0 and tool schema1.1.0, with package/backend compatibility limited to0.2.x. Package preparation binds actual files and six skills to an explicit reviewed source revision; installation remains unknown until the client reports evidence. Existing0.1.x packaged guidance requires a new import. Direct MCP clients need refreshed tool discovery and separate Notes scopes on reauthorization; deployment never grants Notes consent.

Preflight targets the existing habitica-companion Pages production branch main and the existing Auth/MCP/Workspace Workers, without a dev suffix. All deployed runtime variables, feature flags, credential-key selection and retained resource bindings match the generated candidate. No secret value changes are required. Workspace adds the private NotesLifecycleService entrypoint before Auth binds NOTES_LIFECYCLE. Apply Auth0009 and Workspace0008 through0012 before matching Worker publication. Migrations add tables, columns and an inactive-by-default retirement fence; no existing task text is rewritten. Record D1 Time Travel bookmarks and exact Pages/Worker predecessors before changes. Code rollback does not reverse schema or user data; additive migrations remain in place during ordinary code rollback. Review active retirement fences before rolling back compatible task code.

Notes UI qualification uses1168 solution tests,35 final Notes controls and720 matching rendered cases. The candidate runs matching Worker builds, package/identity controls and synthetic backend regressions before remote migration/publication. Production smoke checks read public health, OAuth discovery and release assets only; publication is not a new live gameplay test round. Hosted Notes consent, model behavior and physical devices retain separate qualification.

The0.2.0 candidate is now published from ba5a96d to the existing production slot. Exact Pages/Worker versions, preceding deployments, D1 bookmarks and the verified package receipt reside in skill-tree/history/validation/notes-production-publication.json. Auth154, Workspace572 and package/identity controls pass against matching builds. Remote migrations have no remainder. Ordinary browser inspection confirms the production settings, OAuth Notes scopes, MCP-to-Auth health and rendered WebAssembly Notes route. Terminal403/DNS restrictions and blocked direct manifest navigation remain explicit inspection limits. The ready package is output/habitica-companion-plugin-0.2.0.zip; importing the package and separately granting Notes access remain user actions. No actual hosted Notes enrollment or gameplay test is claimed.

## Web Push subscription release prerequisites

Apply Auth0010-push-subscriptions.sql before the matching Auth Worker, including when PUSH_ENABLED is false: profile cleanup and bounded scheduled expiry reference push tables. Publish Auth PushService before Workspace binds AUTH_PUSH. Deploy matching clients with the restricted status contract. Pilot and development preparation both retain PUSH_ENABLED=false; no ordinary feature profile provisions or enables Web Push.

A separately reviewed activation must provision dedicated Auth-only PUSH_VAULT_KEY_V1 (base64url AES256), optional retained PUSH_VAULT_KEY_V2, PUSH_VAULT_CURRENT_KEY_VERSION, a distinct PUSH_VAPID_PRIVATE_JWK P-256 signing secret and the matching public PUSH_VAPID_PUBLIC_KEY. Never put private keys in source, public deployment JSON, build logs or Workspace bindings. The optional push secrets are required only for the enabled capability; existing mandatory credential-secret requirements remain intact. Verify key separation and complete sender/UI/device release gates before enabling. No actual key was provisioned, remote migration applied, provider contacted or feature deployed by local qualification. Rollback must retain compatible push cleanup/migration support and previous decryption keys until separately qualified erasure/rotation.

## Voluntary review compatibility

Publish matching browser/Auth/Workspace/MCP code before exposing patch.review, reviewEvents, review_task and get_task_reviews. The optional extension needs no migration, secret or executor activation. Older strict readers can reject used review metadata; unused records preserve prior JSON and command hashes. Existing tasks:read and habitica.tasks.write grants remain narrow and unchanged. Local synthetic qualification grants no publication or live user testing.

## Foreground reminders

The Today foreground reminder unit requires no new Worker secret, resource, migration or OAuth scope. Foreground assets deploy through the ordinary PWA package; per-browser opt-in and local receipts stay in IndexedDB. The foreground unit neither enables PUSH_ENABLED nor enrolls a subscription. Closed-app/background delivery and physical-device qualification remain separate gates; see [foreground-reminders.md](foreground-reminders.md).

## Scheduled push migration and activation

Auth migration0011-push-dispatches.sql and Workspace migration0013-scheduled-reminders.sql must precede the coordinated Auth/Workspace/MCP code, including private lifecycle cleanup. All ordinary deployment profiles keep PUSH_ENABLED, PUSH_DISPATCH_ENABLED and PUSH_REMINDERS_ENABLED false. Explicit later activation requires dedicated push vault/VAPID secrets, matching public key, valid PUSH_VAPID_SUBJECT contact and separate release/device qualification. The existing Workspace cron remains a hook behind the disabled capability; local implementation does not change live triggers. Operator guidance and uncertainty/retention rules reside in [scheduled-push-reminders.md](scheduled-push-reminders.md).

## Linked recovery migration

The next Workspace migration after scheduled reminders is `0014-linked-task-recovery.sql`. Apply migration0014 only in a separately authorized deployment using the existing rollout/backup process before serving a build that references linked recovery. The local implementation adds no deployment, account, executor or live Habitica authorization. Existing default gates remain disabled. See [linked task recovery](linked-task-recovery.md).


## Separate callback egress candidate

The owner-selected Netlify Functions Free callback gateway has separate source/configuration under deploy/callback-egress. Ordinary pilot/development preparation keeps CALLBACK_EGRESS_ENABLED=false. Apply Auth0012-callback-egress.sql on the exact selected integration database before enabling the narrow claim endpoint; no existing credential, Push or subscription permission grants this lane. The [callback egress contract](callback-egress.md) owns account selection, runtime/service-key setup, Free-plan hard-limit behavior, hosted negative controls and rollback. Source packaging and local dry builds grant no Netlify/Cloudflare publication, remote migration or real callback scope.


The selected Auth callback setup is provisioned with dispatch disabled. Exact initial deployment/base-module identities, the applied `0012` migration and pending hosted qualification are recorded in [callback-egress.md](callback-egress.md#published-auth-setup). The subsequent [2026-10-29 coordinated release](../history/releases/2026-10-29/README.md) publishes full repository Auth/Workspace/MCP source after the remaining migrations and lifecycle bindings. Web Push availability is enabled with dedicated keys and separate device/account consent; callback delivery remains disabled.
