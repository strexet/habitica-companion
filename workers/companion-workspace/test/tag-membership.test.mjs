import test from 'node:test';
import assert from 'node:assert/strict';
import {emptyWorkspace,workspaceDocument} from '../../shared/workspace-domain.ts';
import {desiredTagMembership} from '../../shared/tag-membership.ts';
import {acceptOrganization} from '../src/organization-commands.ts';
import {projectRemovalFrom} from '../../shared/workspace-organization.ts';
const entity=(name,extra={})=>({id:crypto.randomUUID(),revision:1,isArchived:false,name,...extra});
const task=(name,extra={})=>({id:crypto.randomUUID(),revision:1,text:name,kind:'todo',state:'active',priority:'normal',createdAtUtc:'2026-10-22T00:00:00Z',updatedAtUtc:'2026-10-22T00:00:00Z',...extra});
function fixture(){
 const a=entity('A'),b=entity('B'),p=entity('P',{categoryId:a.id}),q=entity('Q',{categoryId:a.id}),section=entity('S',{projectId:p.id,position:0}),label=entity('L');
 const parent=task('Parent',{projectId:p.id,sectionId:section.id,labelIds:[label.id]}),child=task('Child',{parentTaskId:parent.id,projectId:q.id,categoryId:b.id}),goal=entity('Goal',{taskIds:[child.id]});
 const document=workspaceDocument({...emptyWorkspace(),categories:[a,b],projects:[p,q],sections:[section],labels:[label],goals:[goal],tasks:[parent,child]});return {document,a,b,p,q,section,label,parent,child,goal};
}
const members=(result,entity)=>result.groups.find(group=>group.entityId===entity.id).taskIds;
test('all organization membership uses own relationships, direct category override and explicit goals without parent inheritance',()=>{
 const x=fixture(),before=JSON.stringify(x.document),result=desiredTagMembership(x.document);
 assert.deepEqual(members(result,x.p),[x.parent.id]);assert.deepEqual(members(result,x.q),[x.child.id]);assert.deepEqual(members(result,x.section),[x.parent.id]);assert.deepEqual(members(result,x.a),[x.parent.id]);assert.deepEqual(members(result,x.b),[x.child.id]);assert.deepEqual(members(result,x.label),[x.parent.id]);assert.deepEqual(members(result,x.goal),[x.child.id]);
 assert.equal(result.sourceRevision,x.document.revision);assert.equal(JSON.stringify(x.document),before);assert.equal(x.document.projections.length,0);assert.equal(x.document.completions.length,0);
});
test('saved selections materialize every revision-bound page and follow mapping filters without first-page truncation',()=>{
 const x=fixture(),rows=Array.from({length:237},(_,i)=>task('Selected '+i));
 const view=entity('All matches',{query:{source:'tasks',filter:{query:'Selected',projectionState:'unlinked'}}}),empty=entity('Empty',{query:{source:'tasks',filter:{query:'Never matches'}}});
 const document=workspaceDocument({...x.document,savedTaskViews:[view,empty],tasks:[...x.document.tasks,...rows]});
 let result=desiredTagMembership(document);assert.deepEqual(members(result,view),rows.map(row=>row.id));assert.deepEqual(members(result,empty),[]);
 const mapped=workspaceDocument({...document,revision:2,projections:[{id:crypto.randomUUID(),revision:2,taskId:rows[200].id,state:'linked',habiticaTaskId:crypto.randomUUID()}]});
 result=desiredTagMembership(mapped);assert.equal(result.sourceRevision,2);assert.equal(members(result,view).length,236);assert.ok(!members(result,view).includes(rows[200].id));
});
test('unblocked saved selection includes every matching active Todo and preserves dependency exclusion',()=>{
 const rows=Array.from({length:205},(_,i)=>task('Unblocked '+i)),blocked=task('Blocked',{dependsOn:[rows[0].id]}),view=entity('Next',{query:{source:'unblockedTodos',filter:{}}});
 const document=workspaceDocument({...emptyWorkspace(),tasks:[...rows,blocked],savedTaskViews:[view]});assert.deepEqual(members(desiredTagMembership(document),view),rows.map(row=>row.id));
});
test('archival retains membership while logical removal retires only active owned membership and category inheritance',async()=>{
 const x=fixture(),history=task('Retained completion',{projectId:x.p.id,state:'completed'}),archived=task('Retained archive',{projectId:x.p.id,isArchived:true});
 const document=workspaceDocument({...x.document,tasks:[...x.document.tasks,history,archived],completions:[{id:crypto.randomUUID(),revision:1,taskId:history.id,commandId:crypto.randomUUID(),completedAtUtc:'2026-10-22T00:00:00Z'}]});
 const retired=await acceptOrganization({document,storeVersion:1,commands:[],events:[]},{id:crypto.randomUUID(),workspaceId:document.id,expectedRevision:1,kind:'project',operation:'remove',entityId:x.p.id,patch:{removal:projectRemovalFrom(document,x.p.id)}},'2026-10-22T00:00:00Z');assert.equal(retired.disposition,'accepted');
 const result=desiredTagMembership(retired.snapshot.document);assert.deepEqual(members(result,x.p),[history.id,archived.id]);assert.deepEqual(members(result,x.section),[]);assert.deepEqual(members(result,x.a),[]);
 const frozen=workspaceDocument({...document,projects:document.projects.map(row=>row.id===x.p.id?{...row,isArchived:true}:row)});assert.equal(desiredTagMembership(frozen).groups.find(group=>group.entityId===x.p.id).retainMembership,true);
});
