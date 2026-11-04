# Pending lifecycle decisions

Status: owner selected persistent Workspace diagnostic sharing and Auth-owned callback secrets on2026-10-28; owner-decisions.json records the direct replies. No production storage, enrollment, credential provisioning, protocol upgrade or live host operation is authorized by the research record.

## Diagnostic access

Current `DiagnosticsBundleExport` produces up to100 sanitized browser records for explicit manual sharing. Auth OAuth exposes no diagnostics scope, and Workspace has no diagnostic-event table or ingestion/read route. Existing task receipts describe server commands, not another browser's complete log.

Proposed persistent option: Workspace D1 retains only explicitly previewed and uploaded sanitized browser bundles under separate diagnostic consent. Start with a seven-day expiry, a one-MiB encoded profile limit and at most2000 event records per profile; enforce both time and size bounds. The limits are proposed design values. New read scope and browser upload authority stay separate from task execution. Revocation blocks reads immediately and schedules bounded deletion; explicit profile deletion removes the retained diagnostic material. An unavailable browser remains unavailable rather than triggering background extraction.

Alternative: a previewed temporary shared bundle expires within fifteen minutes and creates no long-lived history. Closed-browser or expired-bundle sources remain unavailable. Both options preserve typed original command receipts as a distinct source and prohibit raw request/response logging, prompts, secrets, task/note content or gameplay repair from a diagnostic read.

The owner selected Workspace D1, previewed upload, independent consent, seven-day deletion and strict quotas. Production implementation remains unperformed. [Official OpenAI Security and Privacy](https://developers.openai.com/plugins/guides/security-privacy) requires minimal scopes, disclosed storage and retention, deletion controls and sanitized logging. Existing Notes or workspace consent does not substitute for diagnostic consent.

## MCP Events

The installed SDK1.32.1 supports protocol versions through2025-11-25. [Official MCP Events documentation](https://developers.openai.com/plugins/build/mcp-events) requires MCP2.0 /2026-07-28 and durable subscription/callback state. Offline prototype success establishes neither current SDK compatibility nor actual host delivery.

Proposed ownership: Auth vault encrypts callback URL and signing secrets using separate versioned keys; Workspace stores opaque callback IDs, owner/resource-bound subscriptions, scheduling and original delivery receipts. Require separate subscription consent, finite expiry, refresh, key replacement/rotation, revocation and profile cleanup. Destination verification and delivery use approved connection-time public-address validation, no redirects and preserved TLS host verification. A transport acknowledgment remains distinct from the subscribed chat processing the event.

The owner selected Auth-owned callback secrets and Workspace opaque subscription/receipt state. The alternative to retain production intake unchanged was not selected; protocol compatibility and protected egress remain required implementation dependencies. The earlier Auth-vault selection covered Web Push only. Neither option grants real callback provisioning, protocol/package upgrades, publication, real host tests or access to a desktop-owned Codex thread.
