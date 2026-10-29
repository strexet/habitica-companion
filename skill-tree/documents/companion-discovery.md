# Companion Discovery Qualification

## Capability decision

Installation, authorization, advertised tool availability, model selection and successful execution are separate observations. Clear metadata improves relevant selection but establishes no guarantee of invocation in every new or existing conversation. Custom MCP connections and installed skill packages also have separate update paths. The decision follows the official [metadata guide](https://developers.openai.com/plugins/guides/optimize-metadata) and [connection/testing guide](https://developers.openai.com/plugins/deploy/connect-chatgpt), reviewed 2026-10-26.

The local list_companion_tasks description identifies task review, related project/agenda/history alternatives and the read-only boundary. Tool names, schemas, grants, annotations and command execution remain unchanged. The package contains no instruction to activate on unrelated requests or override another integration. The [prompt corpus](../../tests/fixtures/plugin/discovery-prompts-v1.json) records expectations rather than model observations.

## Reproducible host evaluation

Run every corpus case in both new and existing conversations. For an existing conversation, establish only the synthetic context recorded by the case. A new conversation must receive the required context explicitly; unresolved source or task identity requires clarification. UUID examples are synthetic schema inputs, never real-account targets. Use an explicitly selected host/account session and separately authorized writes. The corpus records initial tool selection. Explicit unambiguous ordinary create/edit journeys continue through a matching internally checked preview without a redundant agent approval question; real writes still require the selected run scope and existing consent.

For each host, first record client/version, installation method, package content hash when available, backend/tool schema identity and the advertised tool set. An absent package hash stays unknown. Run an explicit plugin-selection baseline and an unmentioned-plugin trial separately; record invocation mode. Manual selection does not establish automatic recall. Test missing/revoked scopes separately from available-tool selection.

| Case / chat / invocation mode | Available tools | Selected tools | Arguments | Permissions and confirmation | Result |
| --- | --- | --- | --- | --- | --- |
| Each corpus ID / new or existing / manual or automatic | Exact discovered names, or unavailable | Exact actual call sequence, no call, or unknown | Sanitized actual fields; redact private content | Existing grants, refusal, consent request and preview approval | Success, wrong selection, no selection, auth failure, transport failure, schema failure or unrun |

The acceptance record must keep expected and observed columns separate. Compute precision/recall only for executed model trials with a known available-tool set. A synthetic SDK listTools or schema check supplies no model trial and no precision/recall score. Missing identifiers, ambiguous candidates and absent matching validated previews must not produce execution. Consequential-action review remains separate. Store no credentials, full chat transcript or private task text in committed evidence.

## Selection and connection recovery

- For a supported request without automatic selection, select Habitica Companion explicitly using the host's plugin selector. In ChatGPT, the official custom-MCP test flow uses the @ selector and a new conversation.
- For a missing tool, compare the actual advertised definition with the expected backend/tool contract. A custom-MCP metadata change requires server update/restart, connection refresh and a new-conversation test. Published plugin metadata can remain held for review; an available old definition is separate from local source.
- For an absent skill or stale package content hash, import the reviewed package through the host's supported update path. Reconnecting OAuth alone cannot update imported skill files.
- For authorization failures, follow the existing Companion connection flow. Additional grants and separate Workspace/Notes consent remain explicit. Never paste Habitica credentials into chat or silently broaden scopes.
- For rate limits, honor the returned delay. For an uncertain command, recover the original receipt; connection repair never grants replay permission.

The official [troubleshooting guide](https://developers.openai.com/plugins/deploy/troubleshooting) separates missing discovery, authentication and transport failures. No-call observations during failed authentication must not be scored as metadata selection failures.

## Evidence boundary

The corpus and actual SDK metadata regression qualify advertised schema compatibility and unchanged permission boundaries. Live ChatGPT/Codex selection, imported-skill activation, publication, mobile clients and independent-user behavior require separate selected host trials. No model-routing result is inferred from deterministic fixture expectations.
