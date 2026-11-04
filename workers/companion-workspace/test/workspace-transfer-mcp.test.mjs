import test from 'node:test';
import assert from 'node:assert/strict';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
import {enrolled,accounts,authorization,exchange,resource} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {transferSetup} from './fixtures/workspace-transfer.mjs';
async function client(t,x,scope='profile:read tasks:read workspace.migrate notes.read notes.write',profile=x.p){
 const token=await exchange(x.f,await authorization(x.f,profile,scope));assert.equal(token.status,200);
 const c=new Client({name:'workspace-transfer-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+(await token.json()).access_token}},fetch:(url,init)=>x.f.mcp.fetch(String(url),init)}));return c;
}
const invoke=(c,name,args)=>c.callTool({name,arguments:args});
test('MCP SDK discovery, exact review, independent Notes permissions and immutable original receipt',async t=>{
 const x=await transferSetup(t);assert.equal((await x.stage()).status,200);const c=await client(t,x),tools=(await c.listTools()).tools;
 for(const name of ['list_workspace_transfers','get_workspace_transfer_status','preview_workspace_transfer','accept_workspace_transfer','get_workspace_transfer_receipt','get_workspace_transfer_record']){const tool=tools.find(tool=>tool.name===name);assert.ok(tool,name);assert.equal(tool.inputSchema.additionalProperties,false);}
 assert.ok(tools.find(tool=>tool.name==='accept_workspace_transfer')._meta.securitySchemes[0].scopes.includes('workspace.migrate'));
 const listed=await invoke(c,'list_workspace_transfers',{});assert.equal(listed.structuredContent.items[0].transferId,x.id);assert.equal(listed.structuredContent.browserInventory,'unknown');
 const review=await invoke(c,'preview_workspace_transfer',{transferId:x.id});assert.notEqual(review.isError,true,JSON.stringify(review));assert.equal(review.structuredContent.ready,true);assert.equal(review.structuredContent.gameEffects,0);
 const accepted=await invoke(c,'accept_workspace_transfer',{transferId:x.id,reviewHash:review.structuredContent.reviewHash});assert.notEqual(accepted.isError,true,JSON.stringify(accepted));assert.equal(accepted.structuredContent.state,'accepted');
 await x.f.workspaceDb.prepare('UPDATE workspaces SET store_version=store_version+1').run();assert.deepEqual((await invoke(c,'get_workspace_transfer_receipt',{transferId:x.id})).structuredContent,accepted.structuredContent);
 const original=await invoke(c,'get_workspace_transfer_record',{transferId:x.id,kind:'taskCommand',sourceWorkspaceId:x.sourceId,commandId:x.source.commands[0].command.id});assert.deepEqual(original.structuredContent.original,x.source.commands[0]);assert.equal(x.writes,0);
});
test('existing task/project grants cannot preview/accept migration and missing Notes grants reveal no private content',async t=>{
 const x=await transferSetup(t);assert.equal((await x.stage()).status,200);
 const old=await client(t,x,'profile:read tasks:read habitica.tasks.write workspace.organization.write');await assert.rejects(invoke(old,'preview_workspace_transfer',{transferId:x.id}),/403|insufficient_scope/);
 const noNotes=await client(t,x,'profile:read tasks:read workspace.migrate');const denied=await invoke(noNotes,'preview_workspace_transfer',{transferId:x.id});assert.equal(denied.isError,true);assert.equal(denied.structuredContent.error,'independent_notes_scope_required');assert.ok(!JSON.stringify(denied).includes('Later'));
 assert.equal((await invoke(noNotes,'get_workspace_transfer_status',{transferId:x.id})).isError,true);assert.equal((await x.doc()).tasks.length,1);assert.equal(x.writes,0);
});
test('MCP cannot fabricate browser capture, upload source bytes or select another account',async t=>{
 const x=await transferSetup(t,{notes:false}),c=await client(t,x,'profile:read tasks:read workspace.migrate');
 const missing=await invoke(c,'get_workspace_transfer_status',{transferId:x.id});assert.equal(missing.structuredContent.prerequisite,'owner_browser_capture_required');assert.equal(missing.structuredContent.outcomeUnknown,true);
 for(const patch of [{artifact:JSON.stringify(x.artifact)},{profileId:x.p.profileId},{workspaceId:x.w.workspaceId},{sourceUrl:'https://fixture.invalid/'}])assert.equal((await invoke(c,'preview_workspace_transfer',{transferId:x.id,...patch})).isError,true);
 assert.equal((await x.doc()).tasks.length,1);assert.equal(x.writes,0);
});
test('tasks-only migration has no Notes grant requirement; another profile cannot inspect staged identities',async t=>{
 const x=await transferSetup(t,{notes:false});assert.equal((await x.stage()).status,200);const c=await client(t,x,'profile:read tasks:read workspace.migrate'),review=await invoke(c,'preview_workspace_transfer',{transferId:x.id});assert.notEqual(review.isError,true,JSON.stringify(review));
 const other=await enrolled(x.f,accounts[1]);const connected=await x.f.workspace.fetch('http://localhost:8789/connect',{method:'POST',headers:{authorization:'Bearer '+other.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(connected.status,201);
 const foreign=await client(t,x,'profile:read tasks:read workspace.migrate',other),status=await invoke(foreign,'get_workspace_transfer_status',{transferId:x.id});assert.equal(status.structuredContent.state,'unavailable');assert.ok(!JSON.stringify(status).includes(x.sourceId));assert.equal((await invoke(foreign,'list_workspace_transfers',{})).structuredContent.items.length,0);assert.equal(x.writes,0);
});
test('lost MCP acceptance acknowledgement recovers original intent without another save or game call',async t=>{
 let x,lose=true;x=await transferSetup(t,{notes:false,workspaceServer:async request=>{const body=await request.clone().json(),direct=(await x.f.mf.getBindings('mcp')).WORKSPACE_TEST_DIRECT,result=await direct.fetch(request);if(body.tool==='accept_workspace_transfer'&&result.ok&&lose){lose=false;throw Error('Synthetic lost acknowledgement');}return result;}});
 assert.equal((await x.stage()).status,200);const c=await client(t,x,'profile:read tasks:read workspace.migrate'),review=(await invoke(c,'preview_workspace_transfer',{transferId:x.id})).structuredContent;
 const lost=await invoke(c,'accept_workspace_transfer',{transferId:x.id,reviewHash:review.reviewHash});assert.equal(lost.isError,true);assert.equal(lost.structuredContent.outcomeUnknown,true);assert.equal(lost.structuredContent.transferId,x.id);
 const receipt=await invoke(c,'get_workspace_transfer_receipt',{transferId:x.id});assert.equal(receipt.structuredContent.state,'accepted');assert.equal((await x.doc()).tasks.length,2);assert.deepEqual((await invoke(c,'accept_workspace_transfer',{transferId:x.id,reviewHash:review.reviewHash})).structuredContent,receipt.structuredContent);assert.equal((await x.f.workspaceDb.prepare('SELECT COUNT(*) AS count FROM workspace_transfers').first()).count,1);assert.equal(x.writes,0);
});
for(const mode of ['expired','bytes','grant','notes-revoked'])test('MCP acceptance preserves guarded original transfer on '+mode,async t=>{
 const x=await transferSetup(t);assert.equal((await x.stage()).status,200);const c=await client(t,x),review=(await invoke(c,'preview_workspace_transfer',{transferId:x.id})).structuredContent;
 if(mode==='expired')await x.f.workspaceDb.prepare("UPDATE workspace_transfers SET expires_at_utc='2000-01-01T00:00:00Z'").run();
 if(mode==='bytes')await x.f.workspaceDb.prepare("UPDATE workspace_transfer_chunks SET chunk=chunk||' ' WHERE part='tasks'").run();
 if(mode==='grant')await x.f.workspaceDb.prepare("UPDATE workspace_transfers SET review_json=json_set(review_json,'$.grantEpoch','changed')").run();
 if(mode==='notes-revoked')await x.f.workspaceDb.prepare('UPDATE notes_notebooks SET active=0').run();
 const result=await invoke(c,'accept_workspace_transfer',{transferId:x.id,reviewHash:review.reviewHash});assert.equal(result.isError,true,JSON.stringify(result));assert.equal((await x.doc()).tasks.length,1);assert.equal(x.writes,0);
});

test('MCP folder receipt discovery requires independent Notes access and returns the exact immutable source receipt',async t=>{
 const {applyFolder,folderCommand}=await import('../../shared/notes-folders.ts');const x=await transferSetup(t),folderId=crypto.randomUUID();const b=(await applyFolder(x.local,folderCommand({id:crypto.randomUUID(),folderId,expectedNotebookRevision:x.local.revision,expectedFolderRevision:0,operation:'create',ownerScope:x.local.ownerScope,source:'browser',name:'Exact private folder'}),'2026-11-01T00:00:00Z')).book;x.artifact.notes=JSON.stringify(b);assert.equal((await x.stage()).status,200);const c=await client(t,x),schema=(await c.listTools()).tools.find(t=>t.name==='get_workspace_transfer_record').inputSchema;assert.ok(schema.properties.kind.enum.includes('folderReceipt'));const preview=(await invoke(c,'preview_workspace_transfer',{transferId:x.id})).structuredContent;assert.equal(preview.counts.folders,1);assert.equal((await invoke(c,'accept_workspace_transfer',{transferId:x.id,reviewHash:preview.reviewHash})).structuredContent.state,'accepted');const args={transferId:x.id,kind:'folderReceipt',commandId:b.folderReceipts[0].commandId};assert.deepEqual((await invoke(c,'get_workspace_transfer_record',args)).structuredContent.original,b.folderReceipts[0]);const noNotes=await client(t,x,'profile:read tasks:read workspace.migrate');assert.equal((await invoke(noNotes,'get_workspace_transfer_record',args)).isError,true);assert.equal(x.writes,0);
});
