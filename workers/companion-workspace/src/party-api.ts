import {partyInput,publicPartyContext,type PartyTool,type PartyProof} from '../../shared/party-tools';
import {WorkspaceError} from '../../shared/workspace-http';
export class PartyApiFailure extends WorkspaceError {constructor(code:string,status:number,readonly rate:Record<string,unknown>){super(code,status);}}
export async function partyApi(env:WorkspaceEnv,who:{profileId:string;connectionId:string},tool:PartyTool,args:unknown,authorization?:string){
 if(!env.PARTY_ENABLED)throw new WorkspaceError('party_unavailable',503);
 partyInput(tool,args);
 const response=await env.AUTH_PARTY.fetch('https://party.internal/context',{method:'POST',headers:{'content-type':'application/json',...(authorization?{authorization}:{})},body:JSON.stringify({profileId:who.profileId,connectionId:who.connectionId,tool}),signal:AbortSignal.timeout(25000)});
 if(!response.ok){const result=await response.json<{error?:string;rate?:Record<string,unknown>}>();throw new PartyApiFailure(result.error??'party_unavailable',response.status,result.rate??{});}
 const proof=await response.json<PartyProof>();
 if(proof.profileId!==who.profileId||proof.connectionId!==who.connectionId||!Number.isFinite(Date.parse(proof.expiresAtUtc))||Date.parse(proof.expiresAtUtc)<=Date.now())throw new WorkspaceError('party_changed',409);
 return publicPartyContext(proof);
}
