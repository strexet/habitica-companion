import test from 'node:test';
import assert from 'node:assert/strict';
import {groupTagInput} from '../../shared/group-tag-tools.ts';
import {groupTagStatus} from '../../shared/group-tag-status.ts';
import {workspaceDocument,emptyWorkspace} from '../../shared/workspace-domain.ts';
import {planTags} from '../src/tag-planner.ts';
const authority=()=>({accountId:crypto.randomUUID(),connectionId:crypto.randomUUID(),scopeAllowed:true,consentEnabled:true,consentEpoch:crypto.randomUUID(),source:'browser',grantEpoch:null});
const entity=(name,extra={})=>({id:crypto.randomUUID(),revision:1,isArchived:false,name,...extra});
async function linked(){const owner=authority(),project=entity('Duplicate long project '+'x'.repeat(180)),document=workspaceDocument({...emptyWorkspace(),projects:[project]}),plan=await planTags(document,owner,crypto.randomUUID()),proof={receiptId:crypto.randomUUID(),confirmedAtUtc:new Date().toISOString(),fingerprint:'a'.repeat(64)},tagId=crypto.randomUUID();Object.assign(plan.document.tagLedger.mappings[0],{state:'confirmed',habiticaTagId:tagId,evidence:proof});for(const intent of plan.document.tagLedger.intents)Object.assign(intent,{state:'confirmed',habiticaTagId:tagId,evidence:proof});return {owner,document:workspaceDocument(plan.document),project,tagId};}
test('group correspondence reports stored evidence and old/new permission independently without claiming fresh game rewards',async()=>{
 const x=await linked();let result=groupTagStatus(x.document,x.owner);assert.equal(result.provenance,'stored-canonical-evidence');assert.equal(result.integrationComplete,true);assert.equal(result.groupCoverage.confirmed,1);assert.equal(result.groups[0].habiticaTagId,x.tagId);
 result=groupTagStatus(x.document,{...x.owner,scopeAllowed:false});assert.equal(result.integrationComplete,false);assert.equal(result.tagScopeAllowed,false);assert.equal(result.groups[0].membershipState,'confirmed');
 result=groupTagStatus(x.document,{...x.owner,accountId:crypto.randomUUID()});assert.equal(result.accountVerified,false);assert.equal(result.groups[0].habiticaTagId,null);assert.equal(result.groups[0].membershipState,'needsReview');
 result=groupTagStatus({...x.document,revision:2},x.owner);assert.equal(result.groups[0].membershipState,'confirmed');assert.throws(()=>groupTagStatus(x.document,x.owner,{expectedRevision:2}));
});
test('coverage includes retained archived/completed task mappings, unknown members and every organization page',()=>{
 const rows=Array.from({length:47},()=>entity('Same name')),taskId=crypto.randomUUID(),doc=workspaceDocument({...emptyWorkspace(),projects:rows,tasks:[{id:taskId,revision:1,isArchived:true,text:'Retained history',kind:'todo',state:'active',priority:'normal',createdAtUtc:new Date().toISOString(),updatedAtUtc:new Date().toISOString()}],projections:[{id:crypto.randomUUID(),revision:1,taskId,habiticaTaskId:crypto.randomUUID(),state:'linked'}]});
 const owner=authority(),a=groupTagStatus(doc,owner,{limit:20});assert.equal(a.taskCoverage.total,1);assert.equal(a.taskCoverage.archived,1);assert.equal(a.taskCoverage.linked,1);assert.equal(a.matchedCount,47);assert.equal(a.groupCoverage.missing,47);assert.equal(a.nextOffset,20);const b=groupTagStatus(doc,owner,{limit:100,offset:20,expectedRevision:1});assert.equal(b.groups.length,27);assert.equal(b.nextOffset,null);assert.equal(new Set([...a.groups,...b.groups].map(group=>group.entityId)).size,47);
});
test('tag tools accept exact bounded reviews and refuse account selectors, name mutation and confused operations',()=>{
 const id=crypto.randomUUID();for(const kind of ['category','project','section','label','goal','savedTaskView'])assert.equal(groupTagInput('prepare_group_tag_command',{requestId:id,operation:'reconcile',kind,entityId:id}).entityId,id);
 assert.equal(groupTagInput('prepare_group_tag_command',{requestId:id,operation:'adopt',kind:'project',entityId:id,habiticaTagId:id}).habiticaTagId,id);assert.equal(groupTagInput('prepare_group_tag_command',{requestId:id,operation:'recover',workId:id}).workId,id);
 for(const args of [{requestId:id,operation:'recover',workId:id,entityId:id},{requestId:id,operation:'reconcile',kind:'project',entityId:id,habiticaTagId:id},{requestId:id,operation:'adopt',kind:'project',entityId:id},{requestId:id,operation:'reconcile',kind:'project',entityId:id,profileId:id},{requestId:id,operation:'reconcile',kind:'project',entityId:id,name:'unreviewed'}])assert.throws(()=>groupTagInput('prepare_group_tag_command',args));
 assert.throws(()=>groupTagInput('get_group_tag_status',{limit:101}));assert.throws(()=>groupTagInput('get_group_tag_status',{expectedRevision:0}));assert.throws(()=>groupTagInput('inspect_group_tag_candidates',{kind:'party',entityId:id}));
});

test('MCP tools disclose additive scopes, exact approval and original recovery without a generic proxy',async()=>{
 const {groupTagTools}=await import('../../chatgpt-mcp/src/group-tag-tools.ts');assert.equal(groupTagTools.length,6);
 for(const tool of groupTagTools){assert.equal(tool.inputSchema.additionalProperties,false);assert.ok(tool.description.length>100);assert.deepEqual(tool.securitySchemes,tool._meta.securitySchemes);assert.ok(!Object.hasOwn(tool.inputSchema.properties,'profileId'));}
 const accept=groupTagTools.find(tool=>tool.name==='accept_group_tag_command');assert.deepEqual(accept.securitySchemes[0].scopes,['profile:read','habitica.tags.write']);assert.equal(accept.annotations.readOnlyHint,false);assert.match(accept.description,/explicitly approved original/);
 const read=groupTagTools.find(tool=>tool.name==='get_group_tag_status');assert.deepEqual(read.securitySchemes[0].scopes,['profile:read','tasks:read']);assert.equal(read.annotations.readOnlyHint,true);
});
