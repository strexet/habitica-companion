## Store consented sanitized diagnostic bundles and expose own MCP reads

Goal:
- Provide bounded own-workspace diagnostic reads to agents from explicitly shared sanitized browser logs and separately identified server receipts.

Current behavior:
- DiagnosticsBundleExport builds at most100 sanitized browser-local entries for manual sharing. Auth OAuth has Notes/task scopes but no diagnostic read scope, and Workspace has no diagnostic bundle ingestion/read store.
- Existing diagnostics guidance correctly labels browser history unavailable to MCP. Original task/tag/organization receipts remain separate authoritative command observations.

Desired behavior:
- Add independent diagnostic consent, reviewed browser-upload acceptance and Workspace D1 retention with read-only MCP event/bundle queries. Filters include explicit UTC interval, feature/subsystem, operation, severity and exact correlation/command IDs, with stable paging and source/coverage limits.
- Apply the owner-selected seven-day deletion policy and strict quotas. Initial conservative ceilings are one MiB encoded data and2000 event rows per profile, with at most100 entries per uploaded bundle; lower limits may refuse safely without partial acceptance.

Touch:
- Diagnostics bundle/sanitizer contracts, Workspace D1 migrations and bounded ingestion/read/cleanup APIs, Auth consent/scope catalogue and profile lifecycle, MCP diagnostic schemas/handlers, C# cloud transports and neutral API/security fixtures.

Discovery allowance:
- Reuse the existing sanitized bundle and Auth identity boundaries. Inspect Workspace/Notes cleanup and quota patterns before adding diagnostic records; no raw provider tail or credential collection.

Out of scope:
- Automatic browser extraction/sync, raw logs/prompts/request bodies, gameplay repair or retries, hosted provider telemetry collection, new Notes storage, native task/tag mutations, remote migrations and publication.

Implementation constraints:
- Owner selected Workspace D1, separate consent, preview-only upload initiation, seven-day deletion and strict quotas on2026-10-28. Task/workspace/Notes grants never imply diagnostic consent or the new diagnostic read scope. Derive profile and connection ownership from validated identity, never caller account fields.
- Sanitize again on server ingestion and reads using strict bounded fields; reject unsupported versions and oversized/unallowlisted payloads. Distinguish untrusted browser occurrence times from server receipt time. An uploaded record is evidence supplied by a browser, not proof of full device history or a verified native effect.
- Persist original upload request/hash/receipt atomically. Retry the same request after response loss; conflicting content under the same ID refuses. Duplicate uploads cannot extend the original retention deadline or evade event/byte quotas.
- Expired or revoked data is immediately unreadable, with bounded scheduled physical cleanup and explicit cleanup-failure state. Profile deletion removes diagnostic data; connection/grant changes fence prior access. Document physical cleanup latency and provider backup limitations rather than claiming unmeasured deletion.
- Reads never dispatch native operations, reprepare commands, change executor state or expose secrets, personal note/task text, raw traffic or another profile. Report unknown/omitted/unavailable sources and exact original receipt provenance.

Questions and required clarifications:
- [Resolved - owner answer2026-10-28] Sanitized diagnostic storage belongs to Workspace D1 with separate consent, previewed upload, seven-day deletion and strict quotas. The choice is not authorization to enable consent or upload any actual device logs.

Validation:
- Synthetic D1/SDK/HTTP and C# cases cover independent consent/scopes, forged ownership/source fields, two profiles, revoked/changed connections, redaction/injection, unsupported/oversized bundles, atomic quota rollback, duplicate uploads, response loss and stable filtered paging.
- Clock controls enforce read expiry and cleanup, revocation/deletion, duplicates without retention extension and explicit cleanup failure. Assert zero provider calls, task/tag changes and hidden browser-log acquisition.

Acceptance:
- Only independently consented, explicitly uploaded and sanitized own-workspace bundles are retained under seven-day/time/size bounds with atomic original upload recovery and enforced deletion/revocation controls.
- Read-only MCP queries provide coherent filtered pages and clearly distinguish browser evidence, server receipts, omitted history and unavailable sources without private-data leakage or gameplay side effects.

Documentation updates:
- Update technical.md, features.md, testing.md, diagnostic privacy/retention disclosure and packaged diagnostic guidance; preserve existing manual sharing as an available source.

Risks:
- Diagnostic ingestion adds server-readable data and can accidentally create covert logging. Keep preview/consent explicit, enforce minimization and retain source/coverage labels.


## Preview and manage diagnostic sharing for agent access

Goal:
- Expose the owner-selected diagnostic sharing channel in the existing Diagnostics flow without automatic data extraction.

Current behavior:
- Diagnostics already supports a sanitized bundle preview and copy/download for manual sharing. No browser flow uploads a reviewed bundle for authorized MCP reads or manages diagnostic retention/consent.

Desired behavior:
- Add a separate diagnostic-consent panel and explicit preview/upload flow naming the exact exporting browser, selected filters, omitted counts, seven-day retention, quotas and server-readable scope. Retain copy/download and distinguish uploaded history from unavailable devices/server telemetry.
- Show the original upload receipt, expiry/cleanup state and clear/revoke controls. Unknown acceptance preserves the original request and permits receipt lookup rather than a new upload.

Touch:
- Diagnostics page/components and controller/read facade, typed diagnostic cloud client, source-specific diagnostics state, consent/cleanup UI and affected scoped styles; component/browser qualification fixtures.

Discovery allowance:
- Use the predecessor upload/read/consent contracts and inventory every route hosting changed components. Reuse DiagnosticsBundleExport rather than creating a second logger or changing raw diagnostic capture.

Out of scope:
- Automatic uploads on navigation, background device scraping, raw log attachments, implicit consent/scope grants, native effects, actual account/device uploads and deployment.

Implementation constraints:
- Every upload follows a current sanitized preview and explicit user action. Show exact source, observed versus received timestamps, omitted/unavailable history and expiration; do not promise complete logs or an agent result from upload alone.
- Preserve source/account/workspace ownership, filters and private previews across ordinary navigation while clearing private state on account changes. Stale consent, quota refusal and expired/missing receipts remain explicit; missing receipt never proves an upload failed.
- Read/clear/revoke controls use the independent diagnostic API. Failed cleanup reports blocked access separately from confirmed deletion. Keep the current manual sharing path available when the remote channel is unavailable.

Questions and required clarifications:
- [Resolved - owner answer] Workspace D1 and the seven-day previewed sharing policy are selected. Per-user/device sharing remains an explicit browser consent/action boundary.

Validation:
- Component tests cover consent, source-specific preview/filter counts, upload, original receipt after response loss, quota/expiry, revoke/clear cleanup failure, account changes and unavailable remote sources.
- Qualify actual changed/shared routes under built-in light/dark themes including obsidian-glow, phone/desktop widths, normal/200-percent text, long identifiers, visible primary actions, disabled controls, focus, geometry and contrast. Separate native HTML/component evidence from real device/host behavior.

Acceptance:
- Users can explicitly preview, upload and manage only the intended sanitized browser bundle with a visible original receipt and independent consent/retention controls.
- Unknown uploads, omitted or unavailable history, expiry and cleanup failures remain explicit without automatic data transfer; required route/state interaction and rendered checks pass.

Documentation updates:
- Update diagnostic feature/UX/testing contracts and agent instructions with the actual available sources, seven-day retention and original-receipt workflow.

Risks:
- Consent copy or stale scoped state could upload unintended browser data. Bind preview, source and original request under the current account.


## Qualify an explicit MCP2 migration while retaining legacy clients

Goal:
- Provide a qualified protocol2026-07-28 transport foundation for future Events without breaking existing authenticated tools and legacy clients.

Current behavior:
- The repository pins @modelcontextprotocol/sdk1.32.1 and WebStandardStreamableHTTPServerTransport; installed protocol constants stop at2025-11-25. Existing offline Events controls demonstrate the incompatibility rather than production support.
- Official TypeScript SDK v2 documents separate @modelcontextprotocol/server and client packages plus explicit modern negotiation; an ordinary v2 server still needs explicit2026-07-28 opt-in. Current OAuth provider, tool schemas and every original command receipt must remain compatible.

Desired behavior:
- Construct an exact-version local migration profile and qualify the v2 web-standard/per-request transport, modern discovery/negotiation and the legacy initialize/tools path. Retain a genuine pinned v1 test client and exercise a modern client, without advertising unimplemented event capabilities.
- Record exact package/lock changes, Worker adapter/validation constraints, OAuth context mapping and rollback boundaries. Preserve default-off Events gates and existing user consents.

Touch:
- workers/chatgpt-mcp package/lock, tools.ts and tool type imports, transport/auth boundary, build identity/configuration where required; shared local SDK/HTTP composition fixtures and compatibility docs.

Discovery allowance:
- Use the official SDK v2 migration and2026-07-28 support guides. Inspect all current SDK imports and Cloudflare web-standard constraints before selecting exact reviewed package versions; do not apply a repository-wide codemod to unrelated dirty files.

Out of scope:
- Actual Events subscription/delivery, secret provisioning, remote install/deployment, mass dependency upgrades, OAuth-provider replacement without a separately captured blocker and real host/client activation.

Implementation constraints:
- Planning creates no package-upgrade authorization. A selected implementation must establish applicable owner scope for the exact local SDK migration before changing pins; no package upgrade is performed during Process Pending.
- Preserve validated resource audience, profile/connection/grant epochs, scopes, Origins, bounded requests, quotas, annotations and original command/hash semantics in both protocol eras. A transport wire difference cannot broaden task or Notes authority.
- Use qualified Worker-safe schema validation and request-local auth context. No eval-dependent validator workaround, unsafe global cross-profile server state or anonymous protocol bypass is allowed. Legacy clients stay functional and Events capabilities remain absent until actual handlers qualify.

Questions and required clarifications:
- [Resolved - official/repository evidence] SDK v1 cannot serve the required protocol; official v2 supplies an explicit migration path. Exact versions and Worker compatibility are implementation qualification outputs, not assumptions about the installed host.

Validation:
- Pinned legacy and modern clients exercise discovery, negotiation, complete tool listings/schemas, authorized reads and original command recovery through matching local Worker bundles. Negative controls cover wrong resources/Origins, denied scopes, cross-profile context, revoked epochs, malformed requests and unsupported protocol requests.
- Run current synthetic SDK/D1 regressions and matching three-Worker dry-run builds after the scoped migration. Report exact dependency/runtime limitations; actual ChatGPT/Codex discovery remains separate.

Acceptance:
- A bounded explicit SDK/transport migration supports protocol2026-07-28 discovery and legacy tool clients with unchanged authorization and original command/receipt behavior in local Worker qualification.
- Events remain unavailable until implemented, and package, Worker validation, request-context and protocol-era compatibility evidence identifies the exact qualified versions and remaining host limits.

Documentation updates:
- Update technical.md, build/compatibility metadata, originating-chat-reminders.md and testing.md with exact protocol/SDK qualification boundaries and rollback notes.

Risks:
- An SDK major migration can change auth context, schemas or old clients silently. Keep genuine legacy controls and fail closed on unsupported transports or protocol claims.


## Implement Auth-owned callback secrets and reviewed event subscriptions

Goal:
- Implement the separately consented subscription foundation using the owner-selected Auth-vault callback boundary and exact resource ownership.

Current behavior:
- The current MCP exposes tools only; no production Events subscription store, callback vault or protected callback transport exists. The offline prototype uses fake authorization/network adapters and does not establish production persistence, encryption or egress safety.

Desired behavior:
- Add authenticated Events discovery/list/subscribe/unsubscribe with deterministic subscription identity, finite expiry/refresh and original lifecycle receipts. Auth encrypts callback URL and signing secrets under separate versioned keys; Workspace stores opaque callback IDs, owner/resource-bound subscriptions and receipts.
- Activate a subscription only after signed challenge verification through a qualified connection-safe HTTPS adapter. Raw callback URL/secrets never enter Workspace records, MCP results, exported workspaces or diagnostic logs.

Touch:
- Auth private callback-vault lifecycle and narrow service binding, Workspace subscription/migration/consent records, MCP2 method handlers and scope catalogue, profile/grant cleanup and offline/local Worker security fixtures.

Discovery allowance:
- Reuse existing credential/push vault encryption and cleanup patterns with separate keys and consent. Verify an actual supported callback transport before activation; official TCP socket or fetch documentation alone does not prove validated-address/TLS-host binding. Capture a required transport blocker if the deployment adapter cannot meet the contract.

Out of scope:
- Sending application reminder events, reusing Web Push consent/keys, accepting arbitrary account/desktop-thread selectors, exposing a local app-server, actual key/callback provisioning, real subscriptions and publication.

Implementation constraints:
- [Owner decision2026-10-28] Auth owns encrypted callback URL/signing secrets; Workspace owns opaque IDs, subscriptions and receipts. Require independent subscription consent, current resource access, short-lived verification challenges, expiry/refresh, replacement-secret rotation and cleanup on revoke/disconnect/delete.
- Validate exact event/filter schemas and current principal ownership. Canonicalize subscription arguments and deduplicate same owner/callback/event identity without extending an expired or revoked authority context.
- Require HTTPS, connection-time public-destination enforcement, original-host TLS verification, bounded time/bytes, no redirects and no private/local address access for challenge and later delivery. DNS preflight followed by unconstrained fetch is insufficient; fail closed when secure connection binding is unavailable.
- Persist lifecycle changes and original IDs atomically across concurrent subscribe/refresh/unsubscribe. A challenge acknowledgment or stored callback is not proof of a rendered chat response. Preserve legacy tools, existing grants and all default-off rollout gates.

Questions and required clarifications:
- [Resolved - explicit owner answer] Callback URL/signing-secret ownership is Auth vault with separate consent and revocation cleanup; the prior Web Push decision is no longer used as a substitute. Protocol compatibility is a required predecessor.

Validation:
- Local Auth/Workspace/MCP2 tests cover exact-byte signed challenges, wrong echo/signature, non-public/redirect/rebinding targets, TLS hostname, denied resource access, two profiles, changed grants/connections, deterministic duplicates, concurrent refresh/unsubscribe, rotation, expiry and cleanup failure.
- Inspect exports/logs/database rows to prove raw callback credentials stay only in encrypted Auth storage. Unsupported transport/configuration keeps subscriptions inactive and emits no application data; fake transport assertions remain labelled synthetic.

Acceptance:
- Separately consented exact-resource subscriptions persist idempotently with Auth-only encrypted callback secrets, finite lifecycle and cleanup while Workspace retains only opaque callback identity and original receipts.
- No subscription activates without qualified signed challenge and connection-safe HTTPS destination controls; denied ownership, expiry, revocation, races and unsupported transport fail closed without secret leakage or a chat-delivery claim.

Documentation updates:
- Update technical/privacy/retention and originating-chat-reminders contracts, scope/consent guidance and testing.md; distinguish local security qualification from actual host callbacks.

Risks:
- A callback is both a secret-bearing delivery channel and an outbound network target. Enforce vault isolation, resource authorization and protected egress before any data transfer.


## Deliver due task reminder events with durable original receipts

Goal:
- Add the default-off exact-task reminder Events channel on the qualified protocol and subscription foundation.

Current behavior:
- Durable reminder state and Web Push have separate schedule/receipt contracts. The Events prototype can model signed delivery but is not a production sender and has no actual host reply evidence.

Desired behavior:
- Match due exact-task reminder state to independently authorized live subscriptions and send generic schema-valid signed events through the Auth-owned protected callback gateway. Persist stable event IDs, original attempt receipts, expiry and connection/resource fences.
- Report scheduled, accepted-by-host, refused, retryable and unknown delivery separately from an actual subscribed-chat response; unsubscribed, revoked and expired resources stop delivery.

Touch:
- Workspace reminder/event outbox and scheduler, shared event payload/status contracts, Auth private signing/dispatch gateway, Events subscription lifecycle, MCP read-only original event status and synthetic composition fixtures.

Discovery allowance:
- Reuse existing reminder civil-time/DST and original delivery identity rules without coupling Events permission to push/foreground consent. Validate the final protocol/event schema against the official profile and the completed predecessor contracts.

Out of scope:
- Raw conversation IDs, local Codex control sockets, task completion/scoring, implicit subscription or notification consent, actual chat messages, enabling rollout gates, key provisioning and hosted/device publication.

Implementation constraints:
- Use generic minimum payloads with exact task/event identity and no private Notes, task text, callback URL or signing secret. Enforce current account/connection/grant/resource authorization again before dispatch.
- Persist dispatch intent before the send; preserve event ID across only contract-permitted bounded transient retries, with fresh signing timestamp/signature and exact body bytes. Stop on410/413, expired subscriptions, revoke and exhausted attempts. Original uncertainty never authorizes a new event identity or a duplicated game action.
- Use the predecessor protected HTTPS adapter and Auth vault. Keep server schedule state, transport receipt and actual host rendering distinct; a2xx callback alone cannot confirm a subscribed-chat response or Codex support.
- Retain default-off configuration and bounded quotas/cleanup. Any actual host rescan/subscription/callback operation requires a separately selected qualification profile; local acceptance supplies no real-chat delivery claim.

Questions and required clarifications:
- [Resolved - owner/repository scope] Auth-vault ownership and separate consent are selected. Protocol migration and verified subscription transport/lifecycle are explicit required dependencies; live host/device operations remain outside local implementation scope.

Validation:
- Synthetic end-to-end due/cancel/edit/DST cases cover matching/nonmatching exact tasks, repeated wakeups, account and permission changes, signed body/header identity, duplicates, bursts, expiry/restart, transient retry limits,410/413 and original unknown receipts.
- Assert no unmatched/unsubscribed delivery, secret exposure, native task effects or feedback loops. Retain a concrete owner-run host qualification checklist for rescan, subscribe, callback verification, actual response and unsubscribe, with each live outcome left unqualified until observed.

Acceptance:
- Only due authorized exact-task subscriptions produce bounded signed Events with stable original identities, durable lifecycle receipts and enforced retry/revocation/expiry controls.
- Public status and documentation distinguish local scheduling, host transport acknowledgment, uncertainty and actual chat response; no default gate, unrelated consent or live provider/chat operation changes during local qualification.

Documentation updates:
- Update features.md, originating-chat-reminders.md, protocol/scope/retention contracts, testing.md and packaged reminder guidance with the implemented channel and explicit host/device limits.

Risks:
- A retry or stale subscription can duplicate notifications or leak task context. Preserve exact event identity, minimal payloads and current resource/connection checks at every dispatch.

