# Web Push Secret Custody Decision

Status: Option A selected by the owner on 2026-10-26 for local implementation. Real secret provisioning, publication and live push remain separate.
Task: T-0e166dfc-1eb0-42f7-ba4d-5949f476d31b.

## Existing boundary and missing decision

The integration contract assigns credential-encryption secrets to Auth alone. Auth configuration requires PROFILE_ID_KEY and versioned CREDENTIAL_KEY secrets. Workspace currently has no encryption-secret binding. The Web Push task requests encrypted endpoint/key storage through the Workspace-facing lifecycle but does not select the custodian for the new subscription authentication secrets or VAPID signing key. The current Auth vault contract covers Habitica credentials, rather than a Web Push purpose and lifecycle.

Repository rules section 14 requires a proposal before changing credential-storage policy or security assumptions. Implement All selects the subscription deliverable; the frozen task contract does not resolve the additional secret-custody choice. No real user enrollment, cloud secret provisioning, deployment or live push test follows from the decision.

The [current W3C Push API draft](https://www.w3.org/TR/push-api/#security-and-privacy-considerations) defines subscription authentication material and notes provider-visible timing, frequency and size. Subscription endpoints and authentication secrets must stay out of task bodies, logs, ordinary backups and MCP results.

## Option A: Auth-owned push vault (recommended)

- Auth owns an isolated push-subscription table and dedicated versioned AES-GCM keyring. Purpose-specific associated data binds profile, device, subscription revision and envelope schema. Push encryption keys remain distinct from Habitica credential keys and profile-identity keys.
- Auth holds a separate VAPID signing key. Workspace receives only opaque subscription identity, state and consent/version observations. A narrow private service binding exposes bounded own-profile lifecycle operations and later dispatch; no arbitrary URL/header proxy or plaintext key-return operation exists.
- Workspace owns reminder intent and explicit reminder consent. Browser notification permission is requested only from a direct user gesture, independently of cloud Workspace and agent enrollment. Missing capability, denied permission or missing consent causes no subscription write.
- Active subscription material remains until browser expiry, explicit device unsubscribe, reminder-consent revocation, integration disconnect or profile deletion. Revocation blocks delivery before cleanup; unsubscribe/revocation/disconnect/delete purge encrypted endpoint/auth material. Failed cleanup stays visibly pending and cannot restore delivery. Provider 404/410 removes the unusable target. Task history remains intact.
- Workspace-only disconnect pauses related reminder dispatch immediately; account-wide device subscription state remains under separate reminder consent. Deleting a Workspace cancels associated reminder intents. Profile deletion purges all push material and attributable delivery records.
- Ordinary task/Notes/device-sync exports exclude endpoint/key material. An explicit own-account privacy export can report device/subscription identities, consent, status and timestamps without endpoint/auth keys. No chat transcript or task body is collected for subscription setup.
- Later dispatch uses task-neutral notification copy and authenticated same-origin task resolution. Provider acceptance never becomes a shown/read guarantee. Endpoint validation rejects credentials, IP/private destinations, unsupported providers and redirects before network dispatch.

Effect: preserves Auth-only secret custody while adding a narrow Auth service and explicit push-specific privacy/lifecycle contract.

## Option B: Workspace-owned push vault

The same consent, encryption, lifecycle, export and validation requirements apply, but Workspace stores/decrypts subscription material and owns dedicated encryption/VAPID secrets. Auth continues to supply identity and revocation checks. The design reduces the Auth push-service surface and broadens Workspace's secret custody. Adoption requires an explicit amendment to the Auth-only custody contract and separate Workspace key-rotation/revocation qualification.

## Required implementation evidence after selection

1. Synthetic profile/device isolation, independent consent and zero page-load permission requests.
2. Envelope purpose/profile/device/revision binding, missing/wrong key refusal, key separation and sanitized failures.
3. Endpoint/redirect/SSRF refusal, duplicate identity and no returned/logged key material.
4. Expiry, unsubscribe, browser permission revocation, integration disconnect, Workspace-only disconnect and profile deletion with cleanup-failure fences.
5. Export boundaries, original lifecycle receipts and browser PushManager grant/deny/unsupported fixtures.

Actual secrets, provider dispatch, physical iPhone behavior and publication remain separate authorization and release lanes.
