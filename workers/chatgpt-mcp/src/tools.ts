import {diagnosticTools,handleDiagnosticTool} from './diagnostic-tools';
import {diagnosticToolNames,type DiagnosticTool} from '../../shared/diagnostics';
import {adoptionScopes,adoptionInput} from '../../shared/adoption-tools.ts';
import {adoptionTools} from './adoption-tools.ts';
import {linkedRecoveryTools} from './linked-recovery-tools';
import {linkedRecoveryScopes,linkedRecoveryInput} from '../../shared/linked-recovery-tools';
import {buildIdentity,clientBuildReport,compatibilityDiscovery} from '../../shared/build-identity';
import {reminderTools,handleReminderTool} from './reminder-tools';
import {reminderToolScopes,type ReminderTool} from '../../shared/reminder-tools';
import {noteTools,handleNoteTool} from './note-tools';
import {noteToolScopes,type NoteTool} from '../../shared/note-tools';
import {retirementTools} from './retirement-tools';
import {retirementToolScopes,retirementInput} from '../../shared/retirement-tools';
import {conversionTools} from './conversion-tools';
import {conversionToolScopes,conversionInput} from '../../shared/conversion-tools';
import {groupTagTools} from './group-tag-tools';
import {groupTagScopes,groupTagInput} from '../../shared/group-tag-tools';
import {organizationWriteTools} from './organization-tools';
import {organizationToolScopes,organizationToolInput} from '../../shared/organization-tools';
import {cronTools,handleCronTool} from './cron-tools';
import {cronToolScopes,type CronTool} from '../../shared/cron-tools';
import {spellTools,handleSpellTool} from './spell-tools';
import {spellToolScopes,type SpellTool} from '../../shared/spell-tools';
import {partyTools} from './party-tools';
import {partyToolScopes,partyInput} from '../../shared/party-tools';
import { canonicalTaskTools } from "./task-tools";
import { taskToolScopes, taskToolInput } from "../../shared/task-tools";
import { workspaceJson } from "../../shared/workspace-http";
import { Server, createMcpHandler, isLegacyRequest, WebStandardStreamableHTTPServerTransport, SUPPORTED_PROTOCOL_VERSIONS, type Tool } from "@modelcontextprotocol/server";
import { CfWorkerJsonSchemaValidator } from "@modelcontextprotocol/server/validators/cf-worker";
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
const bootstrapTools = (Object.keys(readScopes) as ReadTool[]).map(name => ({ name, description: descriptions[name], inputSchema: schemas[name].input, outputSchema: schemas[name].output, annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }, securitySchemes: [{ type: "oauth2", scopes: [...new Set(["profile:read", readScopes[name]])] }], _meta: { securitySchemes: [{ type: "oauth2", scopes: [...new Set(["profile:read", readScopes[name]])] }], ...(name === "get_profile" ? { "openai/profile": true } : {}) } })) as Tool[];

const compatibilityTool={name:'get_connector_compatibility',description:'Read actual backend release/build and tool compatibility. An optional package version/hash is a client report, never proof of installed skills. No Habitica requests or account changes.',inputSchema:{type:'object',additionalProperties:false,properties:{packageVersion:{type:'string',pattern:'^[0-9]{1,6}\\.[0-9]{1,6}\\.[0-9]{1,6}$'},packageHash:{type:'string',pattern:'^[a-f0-9]{64}$'},toolSchemaVersion:{type:'string',pattern:'^[0-9]{1,6}\\.[0-9]{1,6}\\.[0-9]{1,6}$'}}},annotations:{readOnlyHint:true,destructiveHint:false,idempotentHint:true,openWorldHint:false},securitySchemes:[{type:'oauth2',scopes:['profile:read']}] ,_meta:{securitySchemes:[{type:'oauth2',scopes:['profile:read']}]}} as Tool;
export const toolDefinitions=[compatibilityTool,...bootstrapTools,...diagnosticTools,...noteTools,...reminderTools,...adoptionTools,...linkedRecoveryTools,...retirementTools,...conversionTools,...groupTagTools,...canonicalTaskTools,...organizationWriteTools,...partyTools,...spellTools,...cronTools];
const canonicalReadNames=new Set([...canonicalTaskTools,...organizationWriteTools,...groupTagTools,...conversionTools].filter(tool=>tool.annotations?.readOnlyHint===true).map(tool=>tool.name));
function canonicalReadFailure(result?:Record<string,unknown>){
  return {isError:true,content:[{type:'text' as const,text:'Companion read is unavailable. Check the connection and respect any returned retry delay. No new action was requested.'}],
    structuredContent:{unavailable:true,...(typeof result?.error==='string'?{error:result.error}:{}),...(result?.rate!==undefined?{rate:result.rate}:{}),...(typeof result?.retryAtUtc==='string'?{retryAtUtc:result.retryAtUtc}:{})}};
}

function createServer(request: Request, env: McpEnv): Server {
  const server = new Server({ name: "habitica-companion-read-bootstrap", version: buildIdentity.backendRelease }, { capabilities: { tools: {} }, jsonSchemaValidator: new CfWorkerJsonSchemaValidator() });
  server.setRequestHandler("tools/list", async () => ({ tools: toolDefinitions }));
  server.setRequestHandler("tools/call", async call => {
    const name = call.params.name;
    if((diagnosticToolNames as readonly string[]).includes(name))return handleDiagnosticTool(request,env,name as DiagnosticTool,call.params.arguments??{});
    if(Object.hasOwn(reminderToolScopes,name))return handleReminderTool(request,env,name as ReminderTool,call.params.arguments??{});
    if(Object.hasOwn(noteToolScopes,name))return handleNoteTool(request,env,name as NoteTool,call.params.arguments??{});
    if(name==='get_connector_compatibility'){
      let report;try{report=clientBuildReport(call.params.arguments??{});}catch{return {isError:true,content:[{type:'text',text:'Invalid compatibility report. Use only documented version/hash fields.'}]};}
      const readIdentity=async(binding:Fetcher,url:string)=>{try{const response=await binding.fetch(url,{signal:AbortSignal.timeout(5000)});if(!response.ok)return null;return await response.json();}catch{return null;}};
      const [auth,workspace]=await Promise.all([readIdentity(env.AUTH_SERVER,'https://auth.internal/build-identity'),readIdentity(env.WORKSPACE_SERVER,'https://workspace.internal/build-identity')]);
      const result={...compatibilityDiscovery(report,auth,workspace),transport:{sdk:{server:"2.4.0",core:"2.4.0"},protocolVersions:["2026-07-28",...SUPPORTED_PROTOCOL_VERSIONS],legacyMode:"stateless",events:false}};
      return {content:[{type:'text',text:JSON.stringify(result)}],structuredContent:result};
    }
    if(Object.hasOwn(cronToolScopes,name))return handleCronTool(request,env,name as CronTool,call.params.arguments??{});
    if(Object.hasOwn(spellToolScopes,name))return handleSpellTool(request,env,name as SpellTool,call.params.arguments??{});
    if(Object.hasOwn(adoptionScopes,name)||Object.hasOwn(linkedRecoveryScopes,name)||Object.hasOwn(retirementToolScopes,name)||Object.hasOwn(conversionToolScopes,name)||Object.hasOwn(groupTagScopes,name)||Object.hasOwn(organizationToolScopes,name)||Object.hasOwn(taskToolScopes,name)||Object.hasOwn(partyToolScopes,name)){
      try{if(Object.hasOwn(adoptionScopes,name))adoptionInput(name,call.params.arguments??{});else if(Object.hasOwn(linkedRecoveryScopes,name))linkedRecoveryInput(name,call.params.arguments??{});else if(Object.hasOwn(retirementToolScopes,name))retirementInput(name,call.params.arguments??{});else if(Object.hasOwn(conversionToolScopes,name))conversionInput(name,call.params.arguments??{});else if(Object.hasOwn(groupTagScopes,name))groupTagInput(name,call.params.arguments??{});else if(Object.hasOwn(organizationToolScopes,name))organizationToolInput(name,call.params.arguments??{});else if(Object.hasOwn(partyToolScopes,name))partyInput(name,call.params.arguments??{});else taskToolInput(name,call.params.arguments??{});}catch{return {isError:true,content:[{type:"text",text:"Invalid Companion arguments. Use exact IDs and the documented field whitelist; no account selectors are accepted."}]};}
      const isParty=Object.hasOwn(partyToolScopes,name);const partyMutation=isParty&&!name.startsWith("get_");const canonicalRead=canonicalReadNames.has(name);
      try{
        const response=await env.WORKSPACE_SERVER.fetch("https://workspace.internal/tool",{method:"POST",headers:{authorization:request.headers.get("authorization")??"","content-type":"application/json"},body:JSON.stringify({tool:name,arguments:call.params.arguments??{}}),signal:AbortSignal.timeout(Object.hasOwn(adoptionScopes,name)?55000:Object.hasOwn(linkedRecoveryScopes,name)?55000:Object.hasOwn(retirementToolScopes,name)?55000:Object.hasOwn(groupTagScopes,name)?55000:Object.hasOwn(partyToolScopes,name)?30000:15000)});
        const result=await response.json<Record<string,unknown>>();
        if(!response.ok&&canonicalRead)return canonicalReadFailure(result);
        if(!response.ok)return {isError:true,content:[{type:"text",text:JSON.stringify({...result,recovery:isParty?(partyMutation?"Keep the original command ID. Check get_party_command after an uncertain response; never create another intent to retry.":"Party data is unavailable. Check connection and consent, and respect any returned retry delay."):"Check the original command receipt. Reuse the same request or command ID; never create a new action to retry an uncertain result."})}],structuredContent:result};
        return {content:[{type:"text",text:JSON.stringify(result)}],structuredContent:result};
      }catch{if(canonicalRead)return canonicalReadFailure();if(partyMutation)return {isError:true,content:[{type:"text",text:"Party command outcome is unknown. Check get_party_command with the original command ID; never prepare a new intent to retry."}],structuredContent:{commandId:call.params.arguments?.commandId,savedInCompanion:null,outcomeUnknown:true}};if(isParty)return {isError:true,content:[{type:"text",text:"Verified party context is unavailable. No membership or role was inferred."}],structuredContent:{unavailable:true}};return {isError:true,content:[{type:"text",text:`Command outcome is unknown. Check the original command ID with ${Object.hasOwn(adoptionScopes,name)?'get_task_adoption_command':Object.hasOwn(linkedRecoveryScopes,name)?'get_linked_task_recovery_command':Object.hasOwn(retirementToolScopes,name)?'get_source_retirement_command':Object.hasOwn(conversionToolScopes,name)?'get_checklist_conversion_command':Object.hasOwn(groupTagScopes,name)?'get_group_tag_command':Object.hasOwn(organizationToolScopes,name)?'get_organization_command':'get_task_command'}. Do not prepare a new command to retry.`}],structuredContent:{savedInCompanion:null,outcomeUnknown:true}};}
    }
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
  return server;
}
export async function handleMcp(request: Request, env: McpEnv, parsedBody: unknown): Promise<Response> {
  // Both entries receive only the request already authorized and bounded by index.ts.
  // Modern envelopes never fall through to the legacy transport, including malformed claims.
  if (!await isLegacyRequest(request, parsedBody, { maxRequestBodySize: 1024*1024 })) {
    const handler = createMcpHandler(() => createServer(request, env), { legacy: "reject", maxRequestBodySize: 1024*1024 });
    try { return await handler.fetch(request, { parsedBody }); }
    finally { await handler.close(); }
  }
  const server = createServer(request, env);
  const transport = new WebStandardStreamableHTTPServerTransport({ sessionIdGenerator: undefined, enableJsonResponse: true, maxRequestBodySize: 1024*1024 });
  try { await server.connect(transport); return await transport.handleRequest(request, { parsedBody }); }
  finally { await server.close(); }
}
export async function requestBody(request: Request): Promise<unknown> {
  return workspaceJson(request,1024*1024);
}
