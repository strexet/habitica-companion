import assert from 'node:assert/strict';
import {fixture,enrolled,accounts} from '../../../chatgpt-auth/test/fixtures/oauth.mjs';
import {emptyWorkspace} from '../../../shared/workspace-domain.ts';
import {workspaceCommand} from '../../../shared/workspace-commands.ts';
import {accept} from '../../src/commands.ts';
import {emptyNotes,applyNote,noteCommand} from '../../../shared/notes.ts';
import {workspaceTokenHash} from '../../../shared/workspace-http.ts';
import {seedLegacyCommand} from './legacy-todo.mjs';
const origin='http://localhost:8789';
export async function transferSetup(t,{notes=true,collision=false,...options}={}){
 let writes=0;const f=await fixture(t,{workspace:true,...options,habitica:async request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:request.headers.get('x-api-user')===accounts[0].habiticaUserId?'Fixture A':'Fixture B'}}});}}),p=await enrolled(f);
 const call=async(path,body,token)=>{const r=await f.workspace.fetch(origin+path,{method:body===undefined?'GET':'POST',headers:{authorization:'Bearer '+(token??w?.sessionToken??p.managementToken),'content-type':'application/json'},...(body===undefined?{}:{body:JSON.stringify(body)})});return {status:r.status,body:await r.json()};};
 let w;const connected=await call('/connect',{consentVersion:'workspace-v1'},p.managementToken);assert.equal(connected.status,201);w=connected.body;
 const cloudTask=crypto.randomUUID();await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:1,operation:'createTodo',taskId:cloudTask,patch:{text:'Same title'}});
 const id=crypto.randomUUID(),sourceId=crypto.randomUUID(),projectId=crypto.randomUUID(),taskId=collision?cloudTask:crypto.randomUUID();
 let document=emptyWorkspace(sourceId);document.revision=2;document.projects=[{id:projectId,name:'Project Ж',revision:2,isArchived:false,categoryId:null}];
 const result=await accept(document,workspaceCommand({id:crypto.randomUUID(),workspaceId:sourceId,expectedRevision:2,operation:'createTodo',taskId,projectToHabitica:false,patch:{text:'Same title'}}),'2026-10-01T01:00:00Z');
 const source={document:result.document,storeVersion:4,commands:[result.entry],events:[result.event],authority:{mode:'frozen',transferId:id,cloudWorkspaceId:null}};
 const scope=await workspaceTokenHash(accounts[0].habiticaUserId);let local=emptyNotes(scope),noteId=crypto.randomUUID();
 if(notes){const n=await call('/notes/connect',{consentVersion:'notes-v1'},p.managementToken);assert.equal(n.status,201);const project={ownerScope:scope,source:'browser',workspaceId:sourceId,projectId},authority={...project,projects:document.projects};
  local=(await applyNote(local,noteCommand({id:crypto.randomUUID(),noteId,expectedNotebookRevision:0,expectedNoteRevision:0,operation:'create',title:'Заметка 😀',markdown:'Body Unicode Ж',project}),'2026-10-01T01:00:00Z',authority)).book;
  local=(await applyNote(local,noteCommand({id:crypto.randomUUID(),noteId,expectedNotebookRevision:1,expectedNoteRevision:1,operation:'update',markdown:'Later 😀'}),'2026-10-01T02:00:00Z')).book;
 }
 const artifact={version:'companion-adoption-v1',transferId:id,ownerScope:scope,targetWorkspaceId:w.workspaceId,workspaces:[{workspaceId:sourceId,archive:JSON.stringify(source)}],notes:notes?JSON.stringify(local):null};
 const api=(tool,args)=>call('/workspace-transfer/api',{tool,arguments:args});
 const stage=()=>call('/workspace-transfer/stage',{artifact:JSON.stringify(artifact)});
 const preview=(archiveConflicts=false)=>api('preview_workspace_transfer',{transferId:id,archiveConflicts});
 const acceptReview=review=>api('accept_workspace_transfer',{transferId:id,reviewHash:review.reviewHash});
 const doc=async()=>JSON.parse((await f.workspaceDb.prepare('SELECT document_json FROM workspaces WHERE profile_id=?').bind(p.profileId).first()).document_json);
 return {f,p,w,id,sourceId,source,projectId,taskId,noteId,local,artifact,api,call,stage,preview,accept:acceptReview,doc,get writes(){return writes;}};
}
