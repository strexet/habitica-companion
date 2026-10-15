import {activeProfile,credentialKeys,VaultError} from './vault';
import {decryptCredential} from './cryptography';
import {upstream,ReadFailure} from './reads';
import {validatedToken} from './oauth';
import {resourceConfiguration} from '../../shared/resources';
import {partyToolScopes,type PartyProof} from '../../shared/party-tools';
import {uuid,workspaceObject,workspaceJson,WorkspaceError} from '../../shared/workspace-http';

const label=(value:unknown,max=256)=>typeof value==='string'?value.slice(0,max):'';
export async function verifiedParty(env:AuthEnv,profileId:string,connectionId:string,includeOwnedQuests=false):Promise<PartyProof>{
 const profile=await activeProfile(env,profileId,connectionId);
 if(profile.credential_schema_version!==1)throw new WorkspaceError('party_unavailable',503);
 const credential=await decryptCredential(profileId,{ciphertext:profile.credential_ciphertext!,iv:profile.credential_iv!,keyVersion:profile.credential_key_version!,schemaVersion:1},credentialKeys(env));
 const result=await upstream(env,profileId,connectionId,'user?userFields=_id,profile,party'+(includeOwnedQuests?',items':''));
 const proof=await verifyPartyUser(env,profileId,connectionId,result.data,credential.habiticaUserId);
 const user=workspaceObject(result.data);
 let ownedQuests:Record<string,number>|undefined;
 if(includeOwnedQuests){
  const quests=workspaceObject(workspaceObject(user.items??{}).quests??{});ownedQuests={};
  if(Object.keys(quests).length>2048)throw new WorkspaceError('inventory_capacity',413);
  for(const [key,count] of Object.entries(quests))if(/^[A-Za-z0-9_-]{1,128}$/.test(key)&&Number.isSafeInteger(count)&&Number(count)>0)ownedQuests[key]=Number(count);
 }
 return {...proof,...(ownedQuests?{ownedQuests}:{})};
}
export async function verifyPartyUser(env:AuthEnv,profileId:string,connectionId:string,userValue:unknown,expectedActorId:string):Promise<PartyProof>{
 const user=workspaceObject(userValue);const actorUserId=uuid(user._id??user.id);
 if(actorUserId!==expectedActorId)throw new WorkspaceError('identity_mismatch',502);
 const membership=workspaceObject(user.party??{});
 if(membership._id===undefined||membership._id===null||membership._id==='')throw new WorkspaceError('party_unavailable',404);
 const partyId=uuid(membership._id);
 const group=workspaceObject((await upstream(env,profileId,connectionId,'groups/party')).data);
 if(uuid(group._id??group.id)!==partyId||group.type!=='party')throw new WorkspaceError('party_changed',409);
 const leaderId=uuid(group.leader);
 if(group.memberCount!==undefined&&(!Number.isSafeInteger(group.memberCount)||Number(group.memberCount)<1))throw new WorkspaceError('invalid_party_response',502);
 const quest=workspaceObject(group.quest??{});
 if(quest.key!==undefined&&quest.key!==null&&(typeof quest.key!=='string'||quest.key.length>128)||quest.active!==undefined&&typeof quest.active!=='boolean')throw new WorkspaceError('invalid_party_response',502);
 await activeProfile(env,profileId,connectionId);
 const now=Date.now();
 return {profileId,connectionId,actorUserId,actorLabel:label(workspaceObject(user.profile??{}).name,120),party:{id:partyId,name:label(group.name),leaderId,memberCount:typeof group.memberCount==='number'?group.memberCount:null,quest:{key:typeof quest.key==='string'?quest.key:null,active:typeof quest.active==='boolean'?quest.active:null}},observedAtUtc:new Date(now).toISOString(),expiresAtUtc:new Date(now+30000).toISOString()};
}
export async function partyProofOperation(request:Request,env:AuthEnv):Promise<Response>{
 try{
  if(!env.WORKSPACE_ENABLED||!env.PARTY_ENABLED||request.method!=='POST')throw new WorkspaceError('party_unavailable',503);
  const body=workspaceObject(await workspaceJson(request),['profileId','connectionId','tool']);
  if(typeof body.profileId!=='string'||typeof body.connectionId!=='string'||typeof body.tool!=='string'||!Object.hasOwn(partyToolScopes,body.tool))throw new WorkspaceError('invalid_request',400);
  // Only the Workspace service owns this binding. OAuth-origin calls also revalidate scope here.
  const authorization=request.headers.get('authorization');
  if(authorization){
   const token=authorization.match(/^Bearer (\S+)$/)?.[1];const who=token?await validatedToken(env,resourceConfiguration(env.ENVIRONMENT).resource,token):null;
   if(!who||who.props.profileId!==body.profileId||who.props.connectionId!==body.connectionId)throw new WorkspaceError('unauthorized',401);
   if(!who.scope?.includes('profile:read')||!who.scope.includes(partyToolScopes[body.tool as keyof typeof partyToolScopes]))throw new WorkspaceError('insufficient_scope',403);
  }
  return Response.json(await verifiedParty(env,body.profileId,body.connectionId,body.tool==='queue_quest'||body.tool==='get_quest_planning_state'),{headers:{'cache-control':'no-store'}});
 }catch(error){
  const known=error instanceof WorkspaceError||error instanceof VaultError||error instanceof ReadFailure;
  return Response.json({error:known?error instanceof VaultError?'unauthorized':error.code:'party_unavailable',...(error instanceof ReadFailure?{rate:error.rate}:{})},{status:known?error.status:503,headers:{'cache-control':'no-store'}});
 }
}
