# Explicit diagnostic sharing

## Ownership and consent

The Workspace diagnostic namespace stores explicitly accepted sanitized browser copies in D1. Diagnostic profiles require an active canonical workspace, separate diagnostics-v1 owner consent and current profile/connection/access/grant identity. Existing task, Workspace and Notes permissions supply no diagnostic consent. Browser endpoints require the existing owner management credential; MCP exposes only query_diagnostic_events and get_diagnostic_bundle under independent diagnostics.read plus profile:read. No MCP upload, consent mutation, browser storage reader, provider tail or arbitrary URL proxy exists.

Diagnostics remain supplementary evidence. Shared records do not establish complete browser/device history, current connectivity or a native effect. Reduced original task, organization and tag receipts have separate source labels. Tag reviews without an acceptance timestamp retain a review-creation time basis; the API never invents an acceptance time. Original command tools retain normal permissions and recovery semantics.

## Preview and acceptance

DiagnosticSharing converts an explicitly selected manual DiagnosticsBundle into a canonical sanitized cloud subset. The existing manual copy/download format remains available. Metadata-only preparation receives a time-bearing UUIDv7 request ID, canonical SHA-256, encoded byte count and event count. Preparation stores no event, message or payload body. The complete local preview remains necessary before explicit acceptance sends the reviewed bundle.

Acceptance sanitizes every entry again, validates the exact hash/count/bytes and binds the original review to current independent consent. The D1 batch atomically stores payload, indexed events, immutable original receipt and quota counters. An interrupted acknowledgement recovers the original request through the owner original endpoint. Different content under one request ID refuses. Repeated acceptance returns the same receipt and expiry; an already shared hash or overlapping event identity refuses another copy. Prepared IDs cannot be replaced automatically after uncertainty.

The cloud shape uses version1, browser-local-store source, explicit UTC generation, selected filters, available/matching/omitted counts and at most100 sanitized events. Operation names follow the exact reviewed DiagnosticsLogSanitizer allowlist, including app-operation fallback from local export. Unsupported cloud versions, unknown envelope/event fields and unsupported operation names refuse. Messages are reconstructed from fixed operation/result values. Credentials, task/Note text, account fields, raw headers, provider responses and arbitrary metadata never enter accepted records.

## Bounds and retention

Per profile ceilings are2000 indexed event rows, one MiB of charged storage,100 events per bundle and32 prepared uploads. Charging reserves metadata plus twice the canonical payload size, conservatively covering the stored bundle and indexed copies; byte exhaustion can refuse below the encoded-payload ceiling. Quota failure rolls back the entire acceptance.

A preparation expires for acceptance after five minutes. Request UUIDv7 time must be within the five-minute admission window, with thirty seconds of future-clock tolerance. Old request identities cannot create a new preparation after ledger deletion. Accepted payload, event indexes, request/hash metadata and original receipts expire no later than seven days after original preparation. Duplicate calls never extend the deadline. Current consent configuration remains separate from diagnostic history.

Expired and revoked data is immediately unreadable. Scheduled cleanup rotates through at most32 profiles and32 uploads per profile, retaining cleanupPending on failed or incomplete physical deletion. Expired unaccepted preparations can be removed earlier. Owner revocation first closes the consent epoch, then purges accepted/prepared copies; cleanup failure blocks reenabling. Auth profile disconnect/delete uses the restricted content lifecycle binding after credential revocation. Profile deletion removes the diagnostic namespace. Connection/grant changes fence prior access without broadening permissions.

Physical cleanup latency depends on the deployed scheduler and pending workload; no production latency measurement is available. Primary-table deletion does not erase provider recovery history. [Cloudflare D1 Time Travel](https://developers.cloudflare.com/d1/reference/time-travel/) retains independent database recovery history; the deployed plan and exact recovery window remain unverified. No database restore, provider backup deletion or remote migration is included in local qualification.

## Read identity and source coverage

Queries require explicit UTC boundaries and support feature/subsystem, exact safe operation, severity, command ID, correlation ID and source filters. Each page contains at most20 items. Continuation retains diagnostic revision, workspace store version, asOfUtc and validUntilUtc. Reads recheck authority and source revisions before returning. The cursor remains valid for at most sixty seconds and expires earlier with selected shared data. Stale pages restart; expiry cannot silently remove records between coherent pages.

Browser occurrence times remain untrusted and separate from server receipt time. Reduced server task/organization/tag sources each have a200-record ceiling and explicit omission lower bounds. The matched count covers the bounded source selection, not all historical activity. Raw provider logs, other devices, unshared browser history, expired data and unrecorded events remain unavailable. Reads neither acquire hidden browser logs nor dispatch task/tag/game operations or change executor state.

## Local validation and rollout boundary

Neutral C#/Worker fixtures bind canonical bytes, hashes, dates and sanitizer names. Synthetic HTTP/D1/SDK and typed client controls cover consent/scopes, exact original recovery, quota/transaction failure, revocation races, profile deletion, cleanup failure, expiry, safe filtered paging and private-data canaries. Workspace migration0016 and matching Auth/Workspace/MCP bundles require coordinated operator rollout. Local source preparation grants no remote migration, publication, consent activation, actual log upload, host activation or device qualification.

The Auth permission catalogue uses independent native light/dark CSS rather than PWA schemes. Local captured HTML checks cover both Auth pages at360/390/768/1200px and100/200-percent text. The Auth pages do not consume obsidian-glow; PWA diagnostic sharing route qualification belongs to the separate browser-flow unit. No real OAuth approval or sensitive data transmission occurs in the captured-page controls.

## Browser sharing flow

The shared DiagnosticSharingPanel appears on /diagnostics and the /live-tests alias. Navigation sends no diagnostic content and grants no consent. Check sharing status reads only independent consent/quota state. A separate unchecked disclosure enables diagnostic consent. Local cloud preview freezes the selected source, feature/severity/mode filters,100-entry ceiling and available/matching/omitted counts. The panel displays the complete canonical sanitized body before any upload.

Prepare upload sends only request identity, hash, encoded bytes and event count. A separate unchecked exact-bundle acknowledgment enables Upload reviewed bundle. Every acceptance attempt disables repeat acceptance and preserves the same original request until original receipt recovery or confirmed terminal expiry. Quota refusals, lost acknowledgments and missing receipts cannot silently create a replacement. Original receipts show exact upload/request/hash, server receipt time and expiry. Revoke access and clear shared records closes consent and reports physical-cleanup uncertainty separately.

The scoped browser controller retains frozen previews, filters and original IDs across ordinary navigation in the same tab. Account, Workspace and connection-session identity changes clear private state and fence late replies. Full reload/tab closure does not persist private review state. Existing manual copy/download remains available with an unavailable remote channel. Shared browser copies remain supplementary evidence; upload alone promises no agent response or native game effect.
