import {workspaceObject,WorkspaceError} from './workspace-http';
export const partyToolScopes={get_party_context:'party:read'} as const;
export type PartyTool=keyof typeof partyToolScopes;
export interface PartyProof {
 profileId:string;connectionId:string;actorUserId:string;actorLabel:string;
 party:{id:string;name:string;leaderId:string;memberCount:number|null;quest:{key:string|null;active:boolean|null}};
 observedAtUtc:string;expiresAtUtc:string;
}
export function partyInput(tool:string,value:unknown):Record<string,unknown>{
 if(!Object.hasOwn(partyToolScopes,tool))throw new WorkspaceError('invalid_tool',422);
 return workspaceObject(value,[]);
}
export function publicPartyContext(proof:PartyProof){
 return {party:{id:proof.party.id,name:proof.party.name,memberCount:proof.party.memberCount,quest:proof.party.quest},memberRole:proof.actorUserId===proof.party.leaderId?'leader':'member',retrievedAtUtc:proof.observedAtUtc,provenance:'habitica-verified-party',planningSource:'separate-authenticated-namespace'};
}
