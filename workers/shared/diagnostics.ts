import {diagnosticCatalog as catalog} from './diagnostic-catalog.ts';
import {workspaceObject as object,WorkspaceError,uuid,workspaceTokenHash} from './workspace-http.ts';
export const diagnosticConsentVersion='diagnostics-v1',diagnosticReadScope='diagnostics.read';
export const diagnosticLimits={events:2000,bytes:1024*1024,bundle:100,preparations:32,retentionMs:7*86400000,previewMs:5*60000};
export const diagnosticFeatures=['Auth','Sync','Tasks','Inventory','Party','Diagnostics','Equipment','Skills','Workspace'] as const;
export const diagnosticSeverities=['Info','Success','Warning','Error'] as const,diagnosticModes=['Local','LiveRead','LiveMutation','ReversibleTest'] as const;
const results=['Success','Refused','Unavailable','Unknown','Cancelled','AccountChanged','Observed'] as const;
export const diagnosticBytes=(value:string)=>new TextEncoder().encode(value).length;
const invalid=():never=>{throw new WorkspaceError('invalid_diagnostic_bundle',422);};
function choice(value:unknown,values:readonly string[]):string{if(typeof value!=='string'||!values.includes(value))return invalid();return value;}
export function diagnosticUtc(value:unknown):string{if(typeof value!=='string'||!/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?(?:Z|\+00:00)$/.test(value)||!Number.isFinite(Date.parse(value)))return invalid();const normalized=new Date(value).toISOString();if(normalized.slice(0,19)!==value.slice(0,19))return invalid();return normalized;}
const count=(value:unknown,max=1e9)=>Number.isSafeInteger(value)&&Number(value)>=0&&Number(value)<=max?Number(value):invalid();
export interface DiagnosticContext {result:string;durationMs:number;correlationId:string|null;commandId:string|null;acceptedCount:number|null;confirmedCount:number|null;requestedCount:number|null}
export interface DiagnosticEvent {id:string;occurredAtUtc:string;featureArea:string;operation:string;severity:string;mode:string;message:string;metadata:Record<string,string>;context:DiagnosticContext|null}
export interface DiagnosticBundle {version:1;generatedAtUtc:string;source:'browser-local-store';filters:{feature:string|null;severity:string|null;mode:string|null};availableCount:number;matchingCount:number;omittedMatchingCount:number;entries:DiagnosticEvent[]}
export function diagnosticEvent(value:unknown):DiagnosticEvent{
 const row=object(value,['id','occurredAtUtc','featureArea','operation','severity','mode','message','metadata','context']);
 if(typeof row.id!=='string'||!/^log-[a-fA-F0-9]{64}$/.test(row.id))return invalid();
 const operation=choice(row.operation,catalog.operations),severity=choice(row.severity,diagnosticSeverities),metadata:Record<string,string>={},raw=object(row.metadata);
 if(Object.keys(raw).length>64)return invalid();
 for(const key of Object.keys(raw).sort()){
  const value=raw[key];if(typeof value!=='string'||value.length>1024)continue;
  if((catalog.counts as readonly string[]).includes(key)&&/^\d+$/.test(value)&&Number(value)<=1e9)metadata[key]=String(Number(value));
  else if((catalog.measurements as readonly string[]).includes(key)&&/^\d+(?:\.\d+)?$/.test(value)&&Number(value)<=1e9)metadata[key]=value;
  else if((catalog.flags as readonly string[]).includes(key)&&/^(true|false)$/i.test(value))metadata[key]=value.toLowerCase()==='true'?'True':'False';
  else if(Object.hasOwn(catalog.choices,key)&&(catalog.choices as Record<string,readonly string[]>)[key].includes(value))metadata[key]=value;
  else if(key==='metadataStatus'&&(catalog.statuses as readonly string[]).includes(value))metadata[key]=value;
  else if((catalog.sectionLists as readonly string[]).includes(key)){const parts=value.split(',').map(v=>v.trim()).filter(Boolean);if(parts.every(v=>(catalog.sections as readonly string[]).includes(v)))metadata[key]=parts.join(', ');}
  else if(key==='sectionStatuses'||key==='sectionPayloadBytes'){const parts=value.split(',').map(v=>v.trim()).filter(Boolean);if(parts.every(v=>{const pair=v.split(':');return pair.length===2&&(catalog.sections as readonly string[]).includes(pair[0])&&(key==='sectionStatuses'?(catalog.statuses as readonly string[]).includes(pair[1]):/^\d+$/.test(pair[1])&&Number(pair[1])<=1e9);}))metadata[key]=parts.join(', ');}
 }
 let context:DiagnosticContext|null=null;
 if(row.context!=null){const c=object(row.context,['result','durationMs','correlationId','commandId','acceptedCount','confirmedCount','requestedCount']);context={result:choice(c.result,results),durationMs:count(c.durationMs,3600000),correlationId:c.correlationId==null?null:uuid(c.correlationId),commandId:c.commandId==null?null:uuid(c.commandId),acceptedCount:c.acceptedCount==null?null:count(c.acceptedCount),confirmedCount:c.confirmedCount==null?null:count(c.confirmedCount),requestedCount:c.requestedCount==null?null:count(c.requestedCount)};}
 return {id:row.id,occurredAtUtc:diagnosticUtc(row.occurredAtUtc),featureArea:choice(row.featureArea,diagnosticFeatures),operation,severity,mode:choice(row.mode,diagnosticModes),message:operation.replaceAll('-',' ')+': '+(context?.result??severity)+'.',metadata,context};
}
export function diagnosticBundle(value:unknown):DiagnosticBundle{
 const row=object(value,['version','generatedAtUtc','source','filters','availableCount','matchingCount','omittedMatchingCount','entries']);
 if(row.version!==1||row.source!=='browser-local-store'||!Array.isArray(row.entries)||row.entries.length>diagnosticLimits.bundle)return invalid();
 const filter=object(row.filters,['feature','severity','mode']),entries=row.entries.map(diagnosticEvent);
 if(new Set(entries.map(entry=>entry.id)).size!==entries.length)return invalid();
 const availableCount=count(row.availableCount),matchingCount=count(row.matchingCount),omittedMatchingCount=count(row.omittedMatchingCount);
 if(availableCount<matchingCount||matchingCount-entries.length!==omittedMatchingCount)return invalid();
 return {version:1,generatedAtUtc:diagnosticUtc(row.generatedAtUtc),source:'browser-local-store',filters:{feature:filter.feature==null?null:choice(filter.feature,diagnosticFeatures),severity:filter.severity==null?null:choice(filter.severity,diagnosticSeverities),mode:filter.mode==null?null:choice(filter.mode,diagnosticModes)},availableCount,matchingCount,omittedMatchingCount,entries};
}
export const diagnosticHash=(value:DiagnosticBundle)=>workspaceTokenHash(JSON.stringify(value));
// Time-bearing request identities fence reuse after the seven-day ledger deletion.
export function diagnosticRequestTime(value:unknown):number{const id=uuid(value);if(id[14]!=='7')throw new WorkspaceError('diagnostic_request_id_required',422);return parseInt(id.replaceAll('-','').slice(0,12),16);}
export function diagnosticRequestId(now=Date.now()):string{const bytes=crypto.getRandomValues(new Uint8Array(16));let time=now;for(let i=5;i>=0;i--){bytes[i]=time%256;time=Math.floor(time/256);}bytes[6]=(bytes[6]&15)|0x70;bytes[8]=(bytes[8]&63)|0x80;const hex=Array.from(bytes,b=>b.toString(16).padStart(2,'0')).join('');return hex.slice(0,8)+'-'+hex.slice(8,12)+'-'+hex.slice(12,16)+'-'+hex.slice(16,20)+'-'+hex.slice(20);}
export const diagnosticToolNames=['query_diagnostic_events','get_diagnostic_bundle'] as const;
export type DiagnosticTool=typeof diagnosticToolNames[number];
export function diagnosticQuery(value:unknown):Record<string,unknown>&{fromUtc:string;toUtc:string;offset:number}{
 const args=object(value,['fromUtc','toUtc','feature','operation','severity','commandId','correlationId','source','offset','expectedRevision','expectedWorkspaceVersion','asOfUtc','validUntilUtc']);
 if(args.fromUtc===undefined||args.toUtc===undefined)throw new WorkspaceError('explicit_utc_interval_required',422);
 const from=diagnosticUtc(args.fromUtc),to=diagnosticUtc(args.toUtc);if(from>to)throw new WorkspaceError('invalid_interval',422);
 if(args.feature!==undefined)choice(args.feature,diagnosticFeatures);if(args.operation!==undefined)choice(args.operation,catalog.operations);if(args.severity!==undefined)choice(args.severity,diagnosticSeverities);
 if(args.source!==undefined)choice(args.source,['browser-shared','server-receipts','both']);if(args.commandId!==undefined)uuid(args.commandId);if(args.correlationId!==undefined)uuid(args.correlationId);
 const offset=count(args.offset??0,100000);if(offset>0&&(!Number.isSafeInteger(args.expectedRevision)||!Number.isSafeInteger(args.expectedWorkspaceVersion)||args.asOfUtc===undefined||args.validUntilUtc===undefined))throw new WorkspaceError('diagnostic_page_identity_required',409);
 return {...args,fromUtc:from,toUtc:to,offset};
}
