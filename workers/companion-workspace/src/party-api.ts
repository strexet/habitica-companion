import {requireCurrentPartyAccess} from './party-access.ts';
import {partyInput,publicPartyContext,type PartyTool,type PartyProof} from '../../shared/party-tools';
import {WorkspaceError} from '../../shared/workspace-http';
import {partyReceipt,planningHash,planningState,planningCommand} from './party-planning';
import type {WorkspaceRow} from './executor';
export class PartyApiFailure extends WorkspaceError {constructor(code:string,status:number,readonly rate:Record<string,unknown>){super(code,status);}}
export async function fetchPartyProof(env:WorkspaceEnv,who:{profileId:string;connectionId:string;accessEpoch:string},tool:PartyTool,authorization?:string){
 if(!env.PARTY_ENABLED)throw new WorkspaceError('party_unavailable',503);
 const response=await env.AUTH_PARTY.fetch('https://party.internal/context',{method:'POST',headers:{'content-type':'application/json',...(authorization?{authorization}:{})},body:JSON.stringify({profileId:who.profileId,connectionId:who.connectionId,tool}),signal:AbortSignal.timeout(25000)});
 if(!response.ok){const result=await response.json<{error?:string;rate?:Record<string,unknown>}>();
  if(response.status===404||result.error==='party_changed')await env.WORKSPACE_DB.prepare('DELETE FROM authenticated_party_consents WHERE profile_id=?').bind(who.profileId).run();
  throw new PartyApiFailure(result.error??'party_unavailable',response.status,result.rate??{});
 }
 const proof=await response.json<PartyProof>();
 if(proof.profileId!==who.profileId||proof.connectionId!==who.connectionId||!Number.isFinite(Date.parse(proof.expiresAtUtc))||Date.parse(proof.expiresAtUtc)<=Date.now())throw new WorkspaceError('party_changed',409);
 await requireCurrentPartyAccess(env,who);
 return proof;
}
export async function partyApi(env:WorkspaceEnv,row:WorkspaceRow,tool:PartyTool,args:unknown,authorization?:string){
 if(!env.PARTY_ENABLED)throw new WorkspaceError('party_unavailable',503);
 const input=partyInput(tool,args);
 if(tool==='get_party_command')return await partyReceipt(env,row.profile_id,String(input.commandId))??{commandId:input.commandId,savedInCompanion:null,outcomeUnknown:true};
 const mutating=tool==='queue_quest'||tool==='vote_quest';const hash=mutating?await planningHash(tool,input):'';
 if(mutating){const receipt=await partyReceipt(env,row.profile_id,String(input.commandId),hash);if(receipt)return receipt;}
 const proof=await fetchPartyProof(env,{profileId:row.profile_id,connectionId:row.connection_id,accessEpoch:row.access_epoch},tool,authorization);
 if(tool==='get_party_context')return publicPartyContext(proof);
 if(mutating)return planningCommand(env,row,proof,tool,input,hash);
 return planningState(env,row,proof);
}
