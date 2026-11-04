import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';

export const eventName='task.reminder.due';
export const eventsScope='events.subscriptions.write';
export const eventToolNames=['prepare_event_consent','accept_event_consent','get_event_consent','get_event_delivery','get_event_subscription'] as const;
export type EventTool=typeof eventToolNames[number];
export interface EventPrincipal {profileId:string;connectionId:string;grantEpoch:string}
export interface EventContext extends EventPrincipal {accessEpoch:string;taskId:string;consentEpoch:string;workspaceRevision:number;cue:string|null;cueRevision:number}
export interface EventSubscription extends EventContext {subscriptionId:string;callbackId:string;revision:number;state:string;expiresAt:number;operationId:string}
export function eventArguments(value:unknown):{taskId:string}{const v=workspaceObject(value,['taskId']);return {taskId:uuid(v.taskId)};}
export function eventRequest(value:unknown,subscribe=true){
 const v=workspaceObject(value,subscribe?['name','arguments','delivery','cursor','ttlMs']:['name','arguments','delivery']);
 if(v.name!==eventName||subscribe&&v.cursor!==null&&v.cursor!==undefined)throw new WorkspaceError('invalid_event',422);
 const args=eventArguments(v.arguments),delivery=workspaceObject(v.delivery,subscribe?['mode','url','secret']:['mode','url']);
 if(delivery.mode!=='webhook'||typeof delivery.url!=='string'||delivery.url.length>2048)throw new WorkspaceError('invalid_delivery',422);
 let url;try{url=new URL(delivery.url);}catch{throw new WorkspaceError('invalid_delivery',422);}
 if(url.protocol!=='https:'||url.username||url.password||url.hash||url.port||!url.hostname.includes('.'))throw new WorkspaceError('invalid_delivery',422);
 let secret='';if(subscribe){
  if(typeof delivery.secret!=='string'||!/^whsec_[A-Za-z0-9+/]+={0,2}$/.test(delivery.secret))throw new WorkspaceError('invalid_signing_key',422);
  secret=delivery.secret;let bytes;try{bytes=Uint8Array.from(atob(secret.slice(6)),c=>c.charCodeAt(0));}catch{throw new WorkspaceError('invalid_signing_key',422);}
  if(bytes.length<24||bytes.length>64||btoa(String.fromCharCode(...bytes))!==secret.slice(6))throw new WorkspaceError('invalid_signing_key',422);
 }
 if(subscribe&&v.ttlMs!==undefined&&v.ttlMs!==null&&(!Number.isSafeInteger(v.ttlMs)||Number(v.ttlMs)<=0))throw new WorkspaceError('invalid_expiry',422);
 return {name:eventName,arguments:args,url:url.href,secret,ttlMs:Math.min(86400000,Math.max(60000,Number(v.ttlMs??3600000)))};
}
export const eventDefinition={name:eventName,description:'A generic reminder for one explicitly consented Companion task. Callback acknowledgment does not establish a chat response.',delivery:['webhook'],inputSchema:{type:'object',additionalProperties:false,required:['taskId'],properties:{taskId:{type:'string',format:'uuid'}}},payloadSchema:{type:'object',additionalProperties:false,required:['taskId'],properties:{taskId:{type:'string',format:'uuid'}}}};
const schema=(properties:Record<string,unknown>,required=Object.keys(properties))=>({type:'object' as const,additionalProperties:false,properties,required});
const id={type:'string',format:'uuid'},text={type:'string',maxLength:256};
export const eventTools=eventToolNames.map(name=>({name,description:name==='prepare_event_consent'?'Preview separate Events consent for one exact task and its current review cue. Review before acceptance; no task or game effect.':name==='accept_event_consent'?'Accept an explicitly reviewed Events consent preview with its original token and hash. Does not create a callback subscription.':name==='get_event_consent'?'Recover the original consent command or read separate exact-task Events consent.':'Read the original event delivery receipt. Host acceptance is not a rendered chat response; unknown delivery must not be replayed.',inputSchema:name==='prepare_event_consent'?schema({requestId:id,taskId:id,enabled:{type:'boolean'},expectedWorkspaceRevision:{type:'integer',minimum:0}}):name==='accept_event_consent'?schema({commandId:id,acceptToken:text,expectedHash:text}):name==='get_event_consent'?schema({taskId:id,commandId:id},['taskId']):name==='get_event_subscription'?schema({subscriptionId:{type:'string',pattern:'^[a-f0-9]{64}$'}}):schema({eventId:id}),annotations:{readOnlyHint:name.startsWith('get_'),destructiveHint:name==='accept_event_consent',idempotentHint:true,openWorldHint:false},securitySchemes:[{type:'oauth2',scopes:['profile:read','tasks:read',eventsScope]}]}));
