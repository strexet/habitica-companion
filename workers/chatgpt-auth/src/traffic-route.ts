import {activeProfile,VaultError} from './vault';
import {networkBudget,profileBudget,trafficFailure} from './traffic';
import {workspaceJson,workspaceObject,WorkspaceError} from '../../shared/workspace-http';
export async function trafficOperation(request:Request,env:AuthEnv){
 try{
  if(request.method!=='POST')throw new WorkspaceError('invalid_request');
  const body=workspaceObject(await workspaceJson(request),['lane','profileId','connectionId','recovery']);
  if(body.lane!=='mcp'&&body.lane!=='workspace')throw new WorkspaceError('invalid_request');
  if(body.profileId===undefined){if(body.connectionId!==undefined||body.recovery!==undefined)throw new WorkspaceError('invalid_request');await networkBudget(env,request,body.lane);}
  else{
   if(typeof body.profileId!=='string'||typeof body.connectionId!=='string'||typeof body.recovery!=='boolean')throw new WorkspaceError('invalid_request');
   await activeProfile(env,body.profileId,body.connectionId);await profileBudget(env,body.profileId,body.lane,body.recovery);
  }
  return new Response(null,{status:204,headers:{'cache-control':'no-store'}});
 }catch(error){return trafficFailure(error)??Response.json({error:'traffic_unavailable'},{status:error instanceof WorkspaceError||error instanceof VaultError?error.status:503,headers:{'cache-control':'no-store'}});}
}
