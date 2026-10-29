# MCP protocol compatibility

## Packages and wire entries

The local MCP Worker pins @modelcontextprotocol/server2.4.0 and @modelcontextprotocol/core2.4.0. Synthetic modern clients pin @modelcontextprotocol/client2.4.0; genuine legacy clients retain @modelcontextprotocol/sdk1.32.1. The OAuth provider remains1.2.3, Node remains22 and Wrangler remains4.151.0. The package lock adds the three v2 packages without changing any pre-existing package version.

Authenticated bounded requests enter the existing OAuthResourceServer and index.ts guards before protocol classification. isLegacyRequest routes legacy traffic to WebStandardStreamableHTTPServerTransport with stateless sessions and JSON responses. Modern envelope claims, including malformed claims, enter createMcpHandler with legacy rejection and a fresh server factory. Default modern response shaping returns JSON for ordinary tools. A modern validation failure never falls through to legacy dispatch. Existing allowed Origins, audience/profile/connection/grant validation, quotas and individual tool scopes remain authoritative.

Both entries use the same tool definitions, handlers and original command contracts. The request and environment remain lexical inputs to the fresh server factory; no module-level request authentication or SDK client identity supplies account authority. Low-level request registration uses method strings. Tool schema annotations use JSON-compatible property types without changing advertised schema bytes. CfWorkerJsonSchemaValidator supplies the interpreter-backed Worker validator; no dynamic-code permission or unsafe-eval binding is enabled.

Modern discovery uses protocol2026-07-28 and the per-request metadata envelope with matching standard headers. Pinned clients refuse unsupported revisions rather than falling back. The v2 public SUPPORTED_PROTOCOL_VERSIONS constant lists legacy revisions; get_connector_compatibility explicitly reports the qualified modern revision alongside the legacy set, exact server/core versions, stateless legacy mode and events:false. Backend release, tool schema compatibility and unknown installed-package identity remain separate from transport versions.

## Qualification and limits

The local profile resides in skill-tree/history/validation/mcp2-migration/profile.json. Synthetic Miniflare/D1 controls use real pinned legacy/modern clients against matching MCP/Auth/Workspace bundles. Controls cover discovery, automatic and pinned negotiation, complete tool/schema equality, Worker schema validation, original Notes receipt recovery after response loss, scope/Origin/audience denial, bounded requests, malformed and unsupported envelopes, two concurrent owners and changed grant epochs. Existing task/organization/Notes regressions retain original command identities and authorization boundaries. Aggregate evidence resides in skill-tree/history/validation/mcp2-migration/result.json.

Events capabilities, subscriptions, callback secrets, event delivery and real host activation remain unavailable. The migration creates no new consent, background work or account operation. Actual ChatGPT/Codex discovery, deployed configuration and devices require separately selected qualification. The retained in-memory v1 prototype describes legacy incompatibility only; modern behavior requires the HTTP entry.

## Rollback boundary

The selected dirty MCP sources and package lock are copied to ignored skill-tree/artifacts/mcp2-migration/baseline before migration. A local rollback restores only migration-owned changes from the selected baseline, reinstalls the exact retained lock and regenerates matching Worker build identity. HEAD, stash or unrelated dirty files never replace the selected baseline. Worker code rollback does not modify Notes, task receipts, database schemas, keys or remote resources. Publication remains a separate owner action.

## Official references

- [SDK v1 to v2 migration](https://ts.sdk.modelcontextprotocol.io/v2/migration/upgrade-to-v2)
- [Protocol2026-07-28 support](https://ts.sdk.modelcontextprotocol.io/v2/migration/support-2026-07-28)
- [Web-standard runtime entry](https://ts.sdk.modelcontextprotocol.io/v2/serving/web-standard.html)
- [OpenAI MCP Events requirements](https://developers.openai.com/plugins/build/mcp-events)
