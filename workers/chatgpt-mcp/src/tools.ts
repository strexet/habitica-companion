import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { WebStandardStreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/webStandardStreamableHttp.js";
import { CallToolRequestSchema, ListToolsRequestSchema, type Tool } from "@modelcontextprotocol/sdk/types.js";
import { readInput, readScopes, type ReadTool } from "../../shared/read-tools";

const string = { type: "string" };
const nullableBoolean = { type: ["boolean", "null"] };
const task = { type: "object", additionalProperties: false, properties: { id: string, type: { type: "string", enum: ["habit", "daily", "todo", "reward"] }, text: string, completed: nullableBoolean, isDue: nullableBoolean, provenance: { const: "habitica-legacy-bootstrap" } }, required: ["id", "type", "text", "completed", "isDue", "provenance"] };
const object = (properties: Record<string, unknown>, required: string[] = Object.keys(properties)) => ({ type: "object", properties, required, additionalProperties: false });
const retrieval = { provenance: { const: "habitica-legacy-bootstrap" }, retrievedAtUtc: { type: "string", format: "date-time" }, completedTodoCoverage: { enum: ["excluded", "30-most-recent-todos"] } };
const schemas = {
  get_profile: { input: object({}), output: object({ id: { type: "string", minLength: 1, pattern: "\\S" }, name: string, nickname: string }, ["id"]) },
  get_account_status: { input: object({}), output: object({ class: string, level: { type: "number" }, hp: { type: "number" }, maxHp: { type: "number" }, mp: { type: "number" }, maxMp: { type: "number" }, xp: { type: "number" }, nextLevelTarget: { type: "number" }, gp: { type: "number" }, needsCron: nullableBoolean, partyId: string, retrievedAtUtc: { type: "string", format: "date-time" } }, ["needsCron", "retrievedAtUtc"]) },
  list_tasks: { input: object({ type: { type: "string", enum: ["habit", "daily", "todo", "reward"] }, completed: { type: "boolean" }, dueOnly: { type: "boolean" }, query: { type: "string", maxLength: 256 }, limit: { type: "integer", minimum: 1, maximum: 100 } }, []), output: object({ ...retrieval, tasks: { type: "array", items: task, maxItems: 100 }, matchedCount: { type: "integer", minimum: 0 }, truncated: { type: "boolean" }, unknownDueCount: { type: "integer", minimum: 0 } }) },
  get_task: { input: object({ taskId: { type: "string", pattern: "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$" } }), output: object({ ...retrieval, task: { anyOf: [task, { type: "null" }] } }) },
};
const descriptions: Record<ReadTool, string> = {
  get_profile: "Identify the Habitica account represented by the current request credentials. Returns stable opaque identity and available display labels only.",
  get_account_status: "Read current Habitica stats and known Cron requirement. Optional unavailable stats are omitted; unknown needsCron is null. Does not start a new day.",
  list_tasks: "Read the legacy Habitica task bootstrap view, not Companion projects, goals, plans or completed history. dueOnly returns only confirmed server-evaluated due Dailies. Completed To Dos cover at most the 30 most recent entries.",
  get_task: "Find a stable Habitica task ID in the legacy bootstrap view. Searches active tasks and at most 30 recent completed To Dos. Null means unavailable in this bounded view, not necessarily deleted. IDs require verified mapping before any future Companion mutation.",
};
export const toolDefinitions = (Object.keys(readScopes) as ReadTool[]).map(name => ({ name, description: descriptions[name], inputSchema: schemas[name].input, outputSchema: schemas[name].output, annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }, securitySchemes: [{ type: "oauth2", scopes: [readScopes[name]] }], _meta: { securitySchemes: [{ type: "oauth2", scopes: [readScopes[name]] }], ...(name === "get_profile" ? { "openai/profile": true } : {}) } })) as Tool[];

export async function handleMcp(request: Request, env: McpEnv, parsedBody: unknown): Promise<Response> {
  const server = new Server({ name: "habitica-companion-read-bootstrap", version: "0.1.0" }, { capabilities: { tools: {} } });
  server.setRequestHandler(ListToolsRequestSchema, async () => ({ tools: toolDefinitions }));
  server.setRequestHandler(CallToolRequestSchema, async call => {
    const name = call.params.name;
    try { readInput(name, call.params.arguments ?? {}); }
    catch { return { isError: true, content: [{ type: "text", text: "Invalid tool arguments. No account selectors are accepted." }] }; }
    try {
      const response = await env.AUTH_SERVER.fetch("https://auth.internal/read", { method: "POST", headers: { authorization: request.headers.get("authorization") ?? "", "content-type": "application/json" }, body: JSON.stringify({ tool: name, arguments: call.params.arguments ?? {} }), signal: AbortSignal.timeout(25000) });
      const result = await response.json() as { data?: Record<string, unknown>; error?: string; correlationId?: string; rate?: Record<string, unknown> };
      if (!response.ok || !result.data) {
        const messages: Record<string, string> = { credential_repair_required: "Habitica credentials need repair. Revalidate the account in Companion Settings.", rate_limited: "Habitica rate limit reached. Respect the returned retry delay before requesting again.", unauthorized: "Reconnect the Companion account.", insufficient_scope: "Additional access consent is required." };
        return { isError: true, content: [{ type: "text", text: messages[result.error ?? ""] ?? "Habitica read is unavailable. No automatic retry was attempted." }], _meta: { correlationId: result.correlationId, rate: result.rate ?? {} } };
      }
      return { content: [{ type: "text", text: JSON.stringify(result.data) }], structuredContent: result.data, _meta: { correlationId: result.correlationId, rate: result.rate ?? {} } };
    } catch { return { isError: true, content: [{ type: "text", text: "Companion authorization service is unavailable." }] }; }
  });
  const transport = new WebStandardStreamableHTTPServerTransport({ sessionIdGenerator: undefined, enableJsonResponse: true, maxRequestBodySize: 65536 });
  try { await server.connect(transport); return await transport.handleRequest(request, { parsedBody }); }
  finally { await server.close(); }
}
export async function requestBody(request: Request): Promise<unknown> {
  const reader = request.body?.getReader(); if (!reader) return undefined;
  const chunks: Uint8Array[] = []; let size = 0;
  try {
    while (true) { const { done, value } = await reader.read(); if (done) break; size += value.length; if (size > 65536) { await reader.cancel(); throw new Error("Body too large."); } chunks.push(value); }
  } finally { reader.releaseLock(); }
  const body = new Uint8Array(size); let offset = 0; for (const chunk of chunks) { body.set(chunk, offset); offset += chunk.length; }
  return JSON.parse(new TextDecoder().decode(body));
}
