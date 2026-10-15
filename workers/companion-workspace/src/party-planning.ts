import type {PartyProof,PartyTool} from '../../shared/party-tools.ts';
import {publicPartyContext} from '../../shared/party-tools.ts';
import {WorkspaceError,uuid,workspaceTokenHash,workspaceObject} from '../../shared/workspace-http.ts';
import type {WorkspaceRow} from './executor';
export interface QueueItem {id:string;questKey:string;owner:string;label:string;createdAtUtc:string}
export interface Vote {queueItemId:string;actor:string}
export interface PartyDocument {membersCanQueue:boolean;queue:QueueItem[];votes:Vote[]}
interface Plan {party_id:string;namespace_id:string;revision:number;document_json:string;commit_tag:string}
const now=()=>new Date().toISOString();
const empty=():PartyDocument=>({membersCanQueue:true,queue:[],votes:[]});
export async function partyReceipt(env:WorkspaceEnv,profile:string,id:string,hash?:string){
 const old=await env.WORKSPACE_DB.prepare('SELECT payload_hash,receipt_json FROM authenticated_party_commands WHERE profile_id=? AND command_id=?').bind(profile,id).first<{payload_hash:string;receipt_json:string}>();
 if(!old)return null;if(hash&&old.payload_hash!==hash)throw new WorkspaceError('command_id_reused',409);return JSON.parse(old.receipt_json);
}
async function plan(env:WorkspaceEnv,partyId:string){return env.WORKSPACE_DB.prepare('SELECT * FROM authenticated_party_plans WHERE party_id=?').bind(partyId).first<Plan>();}
const workspacePredicate='EXISTS(SELECT 1 FROM workspaces w WHERE w.profile_id=? AND w.connection_id=? AND w.access_epoch=? AND w.active=1)';
const consentPredicate=`EXISTS(SELECT 1 FROM authenticated_party_consents c WHERE c.profile_id=? AND c.party_id=authenticated_party_plans.party_id AND c.namespace_id=authenticated_party_plans.namespace_id AND c.connection_id=? AND c.access_epoch=?) AND ${workspacePredicate}`;
const fence=(row:WorkspaceRow)=>[row.profile_id,row.connection_id,row.access_epoch,row.profile_id,row.connection_id,row.access_epoch];
export async function consentParty(env:WorkspaceEnv,row:WorkspaceRow,proof:PartyProof,version:unknown){
 if(version!=='party-planning-v1')throw new WorkspaceError('party_consent_required',403);
 const count=await env.WORKSPACE_DB.prepare('SELECT COUNT(DISTINCT party_id) AS n FROM authenticated_party_commands WHERE profile_id=?').bind(row.profile_id).first<{n:number}>();
 if((count?.n??0)>=64&&!await env.WORKSPACE_DB.prepare('SELECT 1 FROM authenticated_party_commands WHERE profile_id=? AND party_id=?').bind(row.profile_id,proof.party.id).first())throw new WorkspaceError('contribution_capacity',413);
 await env.WORKSPACE_DB.prepare(`INSERT OR IGNORE INTO authenticated_party_plans(party_id,namespace_id,document_json,commit_tag,updated_at_utc) SELECT ?,?,?,?,? WHERE ${workspacePredicate} AND ?>strftime('%Y-%m-%dT%H:%M:%fZ','now')`).bind(proof.party.id,crypto.randomUUID(),JSON.stringify(empty()),crypto.randomUUID(),now(),row.profile_id,row.connection_id,row.access_epoch,proof.expiresAtUtc).run();
 const state=await plan(env,proof.party.id);if(!state)throw new WorkspaceError('party_changed',409);
 const result=await env.WORKSPACE_DB.prepare(`INSERT INTO authenticated_party_consents(profile_id,party_id,namespace_id,connection_id,access_epoch,consent_version,consented_at_utc) SELECT ?,party_id,namespace_id,?,?,?,? FROM authenticated_party_plans WHERE party_id=? AND namespace_id=? AND ${workspacePredicate} AND ?>strftime('%Y-%m-%dT%H:%M:%fZ','now') ON CONFLICT(profile_id) DO UPDATE SET party_id=excluded.party_id,namespace_id=excluded.namespace_id,connection_id=excluded.connection_id,access_epoch=excluded.access_epoch,consent_version=excluded.consent_version,consented_at_utc=excluded.consented_at_utc`)
 .bind(row.profile_id,row.connection_id,row.access_epoch,version,now(),proof.party.id,state.namespace_id,row.profile_id,row.connection_id,row.access_epoch,proof.expiresAtUtc).run();
 if(!result.meta.changes)throw new WorkspaceError('party_changed',409);return {consented:true,namespaceId:state.namespace_id};
}
async function authorizedPlan(env:WorkspaceEnv,row:WorkspaceRow,proof:PartyProof){
 const state=await env.WORKSPACE_DB.prepare(`SELECT p.* FROM authenticated_party_plans p JOIN authenticated_party_consents c ON c.party_id=p.party_id AND c.namespace_id=p.namespace_id WHERE c.profile_id=? AND c.connection_id=? AND c.access_epoch=? AND p.party_id=? AND EXISTS(SELECT 1 FROM workspaces w WHERE w.profile_id=c.profile_id AND w.connection_id=c.connection_id AND w.access_epoch=c.access_epoch AND w.active=1)`).bind(row.profile_id,row.connection_id,row.access_epoch,proof.party.id).first<Plan>();
 if(!state){await env.WORKSPACE_DB.prepare('DELETE FROM authenticated_party_consents WHERE profile_id=? AND party_id<>?').bind(row.profile_id,proof.party.id).run();throw new WorkspaceError('party_consent_required',403);}return state;
}
export async function planningState(env:WorkspaceEnv,row:WorkspaceRow,proof:PartyProof){
 const p=await authorizedPlan(env,row,proof);const d:PartyDocument=JSON.parse(p.document_json);
 const scrolls=Object.entries(proof.ownedQuests??{}).sort(([a],[b])=>a.localeCompare(b)).map(([questKey,ownedCount])=>({questKey,ownedCount,availableToQueue:Math.max(0,ownedCount-d.queue.filter(q=>q.owner===row.profile_id&&q.questKey===questKey).length)}));
 return {...publicPartyContext(proof),ownScrolls:scrolls.slice(0,100),scrollOptionsTruncated:scrolls.length>100,namespaceId:p.namespace_id,revision:p.revision,membersCanQueue:d.membersCanQueue,queue:d.queue.map(q=>({id:q.id,questKey:q.questKey,contributorLabel:q.label,ownedByYou:q.owner===row.profile_id,createdAtUtc:q.createdAtUtc,voteCount:d.votes.filter(v=>v.queueItemId===q.id).length,votedByYou:d.votes.some(v=>v.queueItemId===q.id&&v.actor===row.profile_id)}))};
}
export async function planningCommand(env:WorkspaceEnv,row:WorkspaceRow,proof:PartyProof,tool:PartyTool,input:Record<string,unknown>,hash:string){
 const p=await authorizedPlan(env,row,proof);
 if(p.namespace_id!==input.expectedNamespaceId||p.revision!==input.expectedRevision)throw new WorkspaceError('stale_party_revision',409);
 if(p.revision===Number.MAX_SAFE_INTEGER)throw new WorkspaceError('revision_exhausted',409);
 const d:PartyDocument=JSON.parse(p.document_json);const timestamp=now();let queueItemId:string;
 if(tool==='queue_quest'){
  if(!d.membersCanQueue&&proof.actorUserId!==proof.party.leaderId)throw new WorkspaceError('leader_required',403);
  const key=String(input.questKey);const owned=proof.ownedQuests&&Object.hasOwn(proof.ownedQuests,key)?proof.ownedQuests[key]:0;
  if(owned<=d.queue.filter(q=>q.owner===row.profile_id&&q.questKey===key).length)throw new WorkspaceError('owned_scroll_required',403);
  if(d.queue.length>=100)throw new WorkspaceError('party_capacity',413);
  queueItemId=crypto.randomUUID();d.queue.push({id:queueItemId,questKey:key,owner:row.profile_id,label:proof.actorLabel,createdAtUtc:timestamp});
 }else{
  queueItemId=String(input.queueItemId);if(!d.queue.some(q=>q.id===queueItemId))throw new WorkspaceError('queue_item_unavailable',409);
  d.votes=d.votes.filter(v=>v.actor!==row.profile_id||v.queueItemId!==queueItemId);
  if(input.voted===true){if(d.votes.length>=500)throw new WorkspaceError('party_capacity',413);d.votes.push({actor:row.profile_id,queueItemId});}
 }
 const serialized=JSON.stringify(d);if(new TextEncoder().encode(serialized).length>65536)throw new WorkspaceError('party_capacity',413);
 const receipt={commandId:input.commandId,operation:tool,namespaceId:p.namespace_id,revision:p.revision+1,queueItemId,...(tool==='vote_quest'?{voted:input.voted}:{}),savedInCompanion:true,habiticaEffects:'none',acceptedAtUtc:timestamp};const tag=crypto.randomUUID();
 const result=await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare(`UPDATE authenticated_party_plans SET document_json=?,revision=revision+1,commit_tag=?,updated_at_utc=? WHERE party_id=? AND namespace_id=? AND revision=? AND ${consentPredicate} AND ?>strftime('%Y-%m-%dT%H:%M:%fZ','now') AND (SELECT COUNT(*) FROM authenticated_party_commands WHERE profile_id=?)<1000 AND NOT EXISTS(SELECT 1 FROM authenticated_party_commands WHERE profile_id=? AND command_id=?)`).bind(serialized,tag,timestamp,p.party_id,p.namespace_id,p.revision,...fence(row),proof.expiresAtUtc,row.profile_id,row.profile_id,input.commandId),
  env.WORKSPACE_DB.prepare('INSERT INTO authenticated_party_commands(profile_id,command_id,party_id,payload_hash,receipt_json,created_at_utc) SELECT ?,?,party_id,?,?,? FROM authenticated_party_plans WHERE party_id=? AND commit_tag=?').bind(row.profile_id,input.commandId,hash,JSON.stringify(receipt),timestamp,p.party_id,tag),
 ]);
 if(!result[0].meta.changes){const duplicate=await partyReceipt(env,row.profile_id,String(input.commandId),hash);if(duplicate)return duplicate;throw new WorkspaceError('party_conflict_or_capacity',409);}return receipt;
}
export async function partySettings(env:WorkspaceEnv,row:WorkspaceRow,proof:PartyProof,input:Record<string,unknown>){
 const p=await authorizedPlan(env,row,proof);
 if(proof.actorUserId!==proof.party.leaderId)throw new WorkspaceError('leader_required',403);
 if(uuid(input.expectedNamespaceId)!==p.namespace_id||input.expectedRevision!==p.revision)throw new WorkspaceError('stale_party_revision',409);
 if(input.confirmNamespaceId!==undefined){
  if(uuid(input.confirmNamespaceId)!==p.namespace_id)throw new WorkspaceError('confirmation_required',409);
  const result=await env.WORKSPACE_DB.prepare(`DELETE FROM authenticated_party_plans WHERE party_id=? AND namespace_id=? AND revision=? AND ${consentPredicate} AND ?>strftime('%Y-%m-%dT%H:%M:%fZ','now')`).bind(p.party_id,p.namespace_id,p.revision,...fence(row),proof.expiresAtUtc).run();
  if(!result.meta.changes)throw new WorkspaceError('party_changed',409);return {deleted:true};
 }
 if(typeof input.membersCanQueue!=='boolean')throw new WorkspaceError('invalid_settings',422);
 if(p.revision===Number.MAX_SAFE_INTEGER)throw new WorkspaceError('revision_exhausted',409);
 const d:PartyDocument=JSON.parse(p.document_json);d.membersCanQueue=input.membersCanQueue;
 const result=await env.WORKSPACE_DB.prepare(`UPDATE authenticated_party_plans SET document_json=?,revision=revision+1,commit_tag=?,updated_at_utc=? WHERE party_id=? AND namespace_id=? AND revision=? AND ${consentPredicate} AND ?>strftime('%Y-%m-%dT%H:%M:%fZ','now')`).bind(JSON.stringify(d),crypto.randomUUID(),now(),p.party_id,p.namespace_id,p.revision,...fence(row),proof.expiresAtUtc).run();
 if(!result.meta.changes)throw new WorkspaceError('party_changed',409);return {saved:true,revision:p.revision+1};
}
export async function ownContributions(env:WorkspaceEnv,profile:string,remove:boolean){
 const plans=await env.WORKSPACE_DB.prepare('SELECT DISTINCT p.* FROM authenticated_party_plans p JOIN authenticated_party_commands c ON c.party_id=p.party_id WHERE c.profile_id=? LIMIT 65').bind(profile).all<Plan>();
 if(plans.results.length>64)throw new WorkspaceError('export_capacity',413);
 const commands=await env.WORKSPACE_DB.prepare('SELECT receipt_json FROM authenticated_party_commands WHERE profile_id=? ORDER BY created_at_utc,command_id LIMIT 1001').bind(profile).all<{receipt_json:string}>();
 if(commands.results.length>1000)throw new WorkspaceError('export_capacity',413);
 const contributions=plans.results.map(p=>{const d:PartyDocument=JSON.parse(p.document_json);return {partyId:p.party_id,namespaceId:p.namespace_id,queue:d.queue.filter(q=>q.owner===profile).map(({owner,...q})=>q),votes:d.votes.filter(v=>v.actor===profile).map(({actor,...v})=>v)};});
 if(!remove)return {contributions,commands:commands.results.map(c=>JSON.parse(c.receipt_json))};
 // One batch CAS across affected namespaces; a failed predicate aborts the whole transaction.
 const statements:D1PreparedStatement[]=[];
 for(const p of plans.results){
  const d:PartyDocument=JSON.parse(p.document_json);const removed=new Set(d.queue.filter(q=>q.owner===profile).map(q=>q.id));d.queue=d.queue.filter(q=>q.owner!==profile);d.votes=d.votes.filter(v=>v.actor!==profile&&!removed.has(v.queueItemId));
  statements.push(env.WORKSPACE_DB.prepare('UPDATE authenticated_party_plans SET document_json=?,revision=revision+1,commit_tag=?,updated_at_utc=? WHERE party_id=? AND namespace_id=? AND revision=? AND revision<?').bind(JSON.stringify(d),crypto.randomUUID(),now(),p.party_id,p.namespace_id,p.revision,Number.MAX_SAFE_INTEGER));
  // SQLite check failure rolls the complete batch back on a lost revision race.
  statements.push(env.WORKSPACE_DB.prepare('INSERT INTO authenticated_party_write_guard(ok) VALUES(changes())'));
 }
 statements.push(env.WORKSPACE_DB.prepare('DELETE FROM authenticated_party_write_guard'));
 statements.push(env.WORKSPACE_DB.prepare('DELETE FROM authenticated_party_commands WHERE profile_id=?').bind(profile));
 statements.push(env.WORKSPACE_DB.prepare('DELETE FROM authenticated_party_consents WHERE profile_id=?').bind(profile));
 await env.WORKSPACE_DB.batch(statements);return {removed:true};
}
export async function planningHash(tool:string,args:Record<string,unknown>){return workspaceTokenHash(JSON.stringify([tool,...Object.keys(args).sort().map(key=>[key,args[key]])]));}
