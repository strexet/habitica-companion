import {workspaceObject,WorkspaceError,uuid} from './workspace-http.ts';
export const partyToolScopes={get_party_context:'party:read',get_quest_planning_state:'party:read',get_party_command:'party:read',queue_quest:'party.planning.write',vote_quest:'party.planning.write'} as const;
export type PartyTool=keyof typeof partyToolScopes;
export interface PartyProof {
 profileId:string;connectionId:string;actorUserId:string;actorLabel:string;
 party:{id:string;name:string;leaderId:string;memberCount:number|null;quest:{key:string|null;active:boolean|null}};
 ownedQuests?:Record<string,number>;observedAtUtc:string;expiresAtUtc:string;
}
export function partyInput(tool:string,value:unknown):Record<string,unknown>{
 if(!Object.hasOwn(partyToolScopes,tool))throw new WorkspaceError('invalid_tool',422);
 const allowed=tool==='queue_quest'?['commandId','expectedNamespaceId','expectedRevision','questKey']:tool==='vote_quest'?['commandId','expectedNamespaceId','expectedRevision','queueItemId','voted']:tool==='get_party_command'?['commandId']:[];
 const input=workspaceObject(value,allowed);
 if(allowed.length){input.commandId=uuid(input.commandId);}
 if(tool==='queue_quest'||tool==='vote_quest'){
  input.expectedNamespaceId=uuid(input.expectedNamespaceId);
  if(!Number.isSafeInteger(input.expectedRevision)||Number(input.expectedRevision)<1)throw new WorkspaceError('invalid_revision',422);
  if(tool==='queue_quest'&&(typeof input.questKey!=='string'||!/^[A-Za-z0-9_-]{1,128}$/.test(input.questKey)))throw new WorkspaceError('invalid_quest',422);
  if(tool==='vote_quest'){input.queueItemId=uuid(input.queueItemId);if(typeof input.voted!=='boolean')throw new WorkspaceError('invalid_vote',422);}
 }
 return input;
}
export function publicPartyContext(proof:PartyProof){
 return {party:{id:proof.party.id,name:proof.party.name,memberCount:proof.party.memberCount,quest:proof.party.quest},memberRole:proof.actorUserId===proof.party.leaderId?'leader':'member',retrievedAtUtc:proof.observedAtUtc,provenance:'habitica-verified-party',planningSource:'separate-authenticated-namespace'};
}
