# Originating-chat reminder capability

Checked 2026-10-27 against official OpenAI documentation and repository sources. Documentation availability establishes a protocol; local fixtures establish only synthetic behavior.

## Capability matrix

| Surface | Event ingress | Conversation binding | Reply evidence |
| --- | --- | --- | --- |
| ChatGPT Work web, desktop Work Cloud, dots | Documented MCP Events; requires MCP 2.0 / `2026-07-28` | Host supplies a subscription callback for the subscribed chat; the application receives no raw chat ID in the documented request | HTTP success acknowledges receipt; processing is asynchronous. Actual rendering requires host observation |
| Desktop local Work or Codex chat | MCP Events support is not established by the Events availability statement | No verified Companion callback-to-desktop-thread mapping | Unverified |
| Custom Codex app-server client | JSON-RPC thread/turn operations; separate transport | A recorded thread ID can be resumed within the connected server. Existing desktop-owned thread access is not established | Turn/item notifications describe the controlled session; desktop rendering remains unverified |
| Current Companion MCP | Server/core SDK `2.4.0`; modern `2026-07-28` and legacy tool clients; tools-only capabilities | No event subscription store or host callback | Events unavailable in the current backend |

The ChatGPT rows follow [official MCP Events documentation](https://developers.openai.com/plugins/build/mcp-events). The custom-client row follows [official Codex app-server documentation](https://learn.chatgpt.com/docs/app-server). Repository conclusions follow `workers/chatgpt-mcp/package.json`, the request-local entries in `src/tools.ts` and [protocol qualification](mcp-protocol-compatibility.md). The retained v1 in-memory control rejects modern discovery; production modern discovery uses the separate per-request HTTP entry.

## Required event security

MCP Events uses authenticated discovery/list/subscribe/unsubscribe, owner-bound filters, canonical subscription identity, verified HTTPS callbacks and Standard Webhooks signatures over exact event bytes. Callback verification requires a fresh challenge and matching echo. Connection-time destination validation must block non-public addresses and redirects while preserving TLS hostname verification. Subscription expiry, refresh, revocation, stable event IDs, bounded payloads and safe duplicate handling remain required. A transport acknowledgement cannot establish a displayed reply. See [MCP Events lifecycle and delivery](https://developers.openai.com/plugins/build/mcp-events).

App-server initialization precedes thread/turn methods. WebSocket authentication requires explicit configuration; WebSocket transport is experimental and unsupported for production. A new app-server process does not establish control of an existing desktop-owned thread. No local control socket should be published for Companion delivery. See [app-server protocol and authentication](https://learn.chatgpt.com/docs/app-server).

## Isolated local prototype

`tests/protocol-prototypes/mcp-events.mjs` is an offline fake-host model, imported only by `tests/Functions/mcp-events-prototype.test.mjs`. The prototype has no network fallback, listener, Worker route or app-server access. Explicit fake authorization and connection adapters separate authenticated ownership from caller fields. The fake connection supplies an approved address, original TLS hostname and no-redirect policy; resource access and expiry are checked again before handoff.

Ten local controls cover discovery, signed challenge, owner forgery, secret/endpoint refusal, invalid signature/challenge, exact-byte signature binding, expiry, revocation, duplicate identity, original uncertain receipts, 410/413 and installed-SDK incompatibility. Two model instances share an injected map to test receipt recovery; the map does not establish production persistence. The receiver independently calculates HMAC for fake packets. No official host or external signing-library interoperability is qualified.

The finite public-address set is deliberately a fake transport assertion, not a general IP classifier. Key rotation, complete protocol pagination/replay, production quotas, encryption/storage, actual DNS/TLS binding, durable concurrent subscription updates and live client rescan/consent remain implementation work. Synthetic secrets never leave the process. No actual conversation receives a message.

## Available UX alternative

Use Todo Sync for explicit device consent and `/todo/reminders` for exact task schedule preview/acceptance and original receipt recovery. Generic PWA push opens a guarded task route after fresh authenticated workspace access. Today provides separately enabled foreground cues. Schedule state and provider acceptance remain distinct from shown/read; the PWA channel supplies no originating-chat reply claim. See [push-reminder-ui.md](push-reminder-ui.md).

Production MCP Events requires owner-bound subscription consent, callback-secret storage, qualified connection-safe egress, durable event receipts and separate real-host qualification on the qualified local protocol foundation. The [Netlify callback egress candidate](callback-egress.md) supplies local transport and Auth replay-fence controls; hosted acceptance remains required before subscription activation. The earlier Auth-vault choice for Web Push does not silently authorize a new callback-secret lifecycle. Local app-server control remains outside that production channel.
