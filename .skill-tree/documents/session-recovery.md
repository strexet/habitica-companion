# Existing connection recovery

Status: implemented and published to the selected production website. Auth recovery, Workspace/Notes existing-only session issuance and typed application clients are available; [release evidence](../history/releases/2026-10-30-session-recovery/readme.md) records migration0013 and deployed identities. Automatic sign-in/private-sync bootstrap, durable preference storage and the single-Workspace UI are separate prioritized units. No real account, deployment or physical device is qualified by synthetic recovery fixtures.

## Ownership and authorization

Auth owns credential verification and expiring management sessions. Workspace owns independent task and Notes consent, access epochs and tab sessions. Browser settings and recovery descriptors never authorize server access. Initial enrollment, existing-connection recovery, OAuth linking and background executor activation remain separate operations.

The non-authorizing descriptor contains version `integration-recovery-v1`, the pseudonymous profileId and exact connectionId. Enrollment and successful recovery return the descriptor. The descriptor contains no token or raw Habitica identity, is not a secret, and cannot recover access without the current account credentials and retained active server consent. The typed client sends no recovery request without a structurally valid descriptor or explicit legacy bootstrap acknowledgement. Persisting/syncing descriptors is outside the recovery unit; generic exports/sync never receive session bearers.

## Auth protocol

`POST /recover` accepts habiticaUserId, apiToken, optional descriptor and optional bootstrapAcknowledged:true. A descriptor-free legacy bootstrap requires explicit acknowledgement of sending current credentials to check existing server enrollment. The request carries no new consentVersion. Requests use the existing HTTPS/allowed-origin/no-store boundary; local loopback remains fixture-only. Recovery is unavailable when ENROLLMENT_ENABLED is false.

Auth derives the expected profile, validates descriptor identity, applies five recovery admissions per profile per minute and verifies account ownership through the existing bounded Habitica v3 user read. Missing, revoked or unsupported-consent enrollment refuses. The submitted credentials must match the existing encrypted credential context; changed credentials require explicit repair/revalidation. Recovery does not rewrite ciphertext, verification/consent timestamps, connection identity, grant epoch or profile generation, and does not create linking tickets or OAuth grants.

Success returns profileId, descriptor, recovered:true, expiresAtUtc and a new temporary managementToken. `integration_recovery_sessions` stores only the token hash, profile/connection binding, current root management-token hash and timestamps. Expiry is eight hours; each profile retains at most eight recovered management sessions. Expired/obsolete rows are removed on issuance and expired rows are removed by the existing Auth scheduled cleanup. A new recovered tab does not rotate the root credential or other healthy tab sessions within the session bound.

The existing management handlers and private Workspace identity broker accept either the root token or a valid recovered session. Every recovered session remains bound to the current connection and root management-token hash. Explicit enrollment/revalidation or management rotation invalidates recovered sessions through root-anchor replacement. Full disconnect blocks ordinary access immediately; deletion removes the profile and associated recovery rows. Existing authenticated disconnect/delete cleanup retry remains available while the matching root anchor is retained for pending cleanup. Grant-only revocation keeps management authority and independent Workspace access, while actual OAuth grants remain revoked; recovery cannot recreate grants. Existing status reads return current permissions and credential-repair observations.

The response distinguishes existing_connection_missing, existing_connection_revoked, integration_consent_unsupported, recovery_descriptor_stale, recovery_acknowledgement_required, credential_repair_required and connection_changed. Invalid credentials, upstream failures and quota replies retain existing safe behavior. Missing session authority, including expired credentials, remains unauthorized; issuance expiry is explicit in the successful response.

## Workspace and Notes protocol

`POST /recover` on Workspace accepts an optional exact expectedWorkspaceId. `POST /notes/recover` accepts an optional exact expectedNotebookId. Both use management authentication through the private Auth identity service. Both require an already existing active store in the current connection with the supported independent consent version. Recovery never upserts canonical data, changes consent timestamps, reactivates disconnected stores or enables execution.

Workspace refusal distinguishes workspace_missing, workspace_disconnected, workspace_connection_changed, workspace_consent_unsupported and workspace_mismatch. Notes supplies the equivalent notes-prefixed states and notebook_mismatch. Optional expected IDs must match the authenticated store; callers cannot select another account.

Successful responses use the existing connection schema plus recovered:true, fresh hashed-at-rest eight-hour tab credentials and exact store identities. Workspace additionally reports actual executorEnabled and executorAvailable observations. Existing eight-session store bounds remain in force. Conditional inserts bind connection/access epochs; a private epoch recheck and final active-store/token join prevent issuing usable authority after concurrent revocation. Existing canonical content, history, receipts, consent and executor settings remain intact.

## Application clients

IChatGptIntegrationClient.RecoverAsync returns a token-free IntegrationRecoveryResult containing only safe state, descriptor and expiry. IWorkspaceCloudClient.RecoverConnectionAsync and INotesCloudClient.RecoverConnectionAsync consume established management access and never call enroll/connect. Initial connect and explicit credential repair retain the existing consent-bearing paths.

Responses are bounded to eight KiB and strictly parsed. Clients validate descriptor/store identity, consent version, recovered marker, token shape, expiry and Notes owner scope. Recovery HTTP operations have a twenty-second deadline. Session-generation checks discard delayed account/connection responses. The client error whitelist excludes raw response detail, credentials and tokens. No recovery response grants new OAuth permissions, changes task/game state or uploads browser records.

## Validation and limitations

recovery.test.mjs runs against isolated Miniflare Auth/Workspace/D1 instances with synthetic upstream services. Controls cover preserved profile/canonical rows, independent tabs, explicit legacy bootstrap, missing/deleted/revoked consent, credential mismatch, stale/foreign descriptors, origin rejection, expiration/rotation, quota/session caps, retained/revoked OAuth permissions, outage and access changes during private epoch roundtrips. The fixture migration lists include Auth0013.

ConnectionRecoveryTests and existing integration tests cover no-request privacy defaults, safe error states, descriptor/identity/expiry validation, token-free results, exact Workspace/Notes recovery and discarded delayed responses. Existing management/vault/Workspace/Notes regression checks remain required.

The unit adds no UI route or automatic sync behavior. UI/device, hosted OAuth discovery, production migration and actual account recovery remain separate qualification. Server recovery is an owner-authentication prerequisite rather than an agent tool for sending Habitica credentials; staged cloud adoption/MCP parity has a separate prioritized contract.
