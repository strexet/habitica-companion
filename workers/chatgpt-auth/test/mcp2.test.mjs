import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {Client as LegacyClient} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport as LegacyTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
import {Client,StreamableHTTPClientTransport,PROTOCOL_VERSION_META_KEY,CLIENT_CAPABILITIES_META_KEY} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/client/dist/index.mjs';
import {CfWorkerJsonSchemaValidator} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/server/dist/validators/cfWorker.mjs';
import {fixture,enrolled,authorization,exchange,accounts,resource} from './fixtures/oauth.mjs';

const modernVersion='2026-07-28',origin='http://localhost:8789';
async function grant(f,p,scopes='profile:read notes.read notes.write tasks:read'){
    const response=await exchange(f,await authorization(f,p,scopes));assert.equal(response.status,200);return (await response.json()).access_token;
}
async function client(t,f,token,modern=true,fetchOverride){
    const requests=[],C=modern?Client:LegacyClient,T=modern?StreamableHTTPClientTransport:LegacyTransport;
    const c=new C({name:modern?'genuine-modern-fixture':'genuine-v1-fixture',version:'1.0.0'},modern?{versionNegotiation:{mode:{pin:modernVersion}}}:{});
    t.after(()=>c.close());
    await c.connect(new T(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:async(url,init)=>{
        if(init?.body)requests.push(JSON.parse(init.body));
        return (fetchOverride??((url,init)=>f.mcp.fetch(String(url),init)))(url,init);
    }}));
    return {c,requests};
}
async function setup(t){
    let upstream=0;const f=await fixture(t,{workspace:true,habitica:request=>{upstream++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:request.headers.get('x-api-user')===accounts[0].habiticaUserId?'Fixture A':'Fixture B'}}});}});
    const p=await enrolled(f),r=await f.workspace.fetch(origin+'/notes/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'notes-v1'})});
    assert.equal(r.status,201);return {f,p,n:await r.json(),token:await grant(f,p),get upstream(){return upstream;}};
}
async function call(c,name,args={}){const value=await c.callTool({name,arguments:args});assert.notEqual(value.isError,true,JSON.stringify(value));return value.structuredContent;}
function envelope(method,params={},version=modernVersion){return {jsonrpc:'2.0',id:1,method,params:{...params,_meta:{[PROTOCOL_VERSION_META_KEY]:version,[CLIENT_CAPABILITIES_META_KEY]:{}}}};}
const rpc=(f,token,body,extra={},url=resource)=>f.mcp.fetch(url,{method:'POST',headers:{authorization:'Bearer '+token,'content-type':'application/json',accept:'application/json, text/event-stream','mcp-protocol-version':body.params?._meta?.[PROTOCOL_VERSION_META_KEY]??modernVersion,'mcp-method':body.method,...(body.method==='tools/call'?{'mcp-name':body.params.name}:{}),...extra},body:JSON.stringify(body)});

test('genuine v1 and pinned v2 clients discover identical complete tools and no Events capability',async t=>{
    const x=await setup(t),legacy=await client(t,x.f,x.token,false),modern=await client(t,x.f,x.token);
    const old=(await legacy.c.listTools()).tools,current=(await modern.c.listTools()).tools;
    assert.ok(current.length>60);assert.deepEqual(current,old);
    const validator=new CfWorkerJsonSchemaValidator();for(const tool of current){assert.equal(validator.getValidator(tool.inputSchema)({foreignProfileSelector:true}).valid,false,tool.name);if(tool.outputSchema)validator.getValidator(tool.outputSchema);}assert.equal(modern.c.getProtocolEra(),'modern');assert.equal(modern.c.getServerCapabilities().events,undefined);assert.equal(legacy.c.getServerCapabilities().events,undefined);
    assert.equal(legacy.requests[0].method,'initialize');assert.equal(modern.requests[0].method,'server/discover');assert.ok(modern.requests[0].params._meta[PROTOCOL_VERSION_META_KEY]);
    const before=x.upstream,compatibility=await call(modern.c,'get_connector_compatibility');assert.deepEqual(compatibility,await call(legacy.c,'get_connector_compatibility'));assert.equal(compatibility.transport.sdk.server,'2.4.0');assert.ok(compatibility.transport.protocolVersions.includes(modernVersion));assert.equal(compatibility.transport.events,false);assert.equal(x.upstream,before);
    const packages=JSON.parse(await readFile(new URL('../../chatgpt-mcp/package.json',import.meta.url),'utf8'));assert.equal(packages.devDependencies['@modelcontextprotocol/sdk'],'1.32.1');assert.equal(packages.dependencies['@modelcontextprotocol/server'],'2.4.0');
});

test('automatic v2 negotiation selects modern without initialize and a mismatched client pin refuses fallback',async t=>{
    const x=await setup(t),methods=[];
    const transport=()=>new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+x.token}},fetch:(url,init)=>{if(init?.body)methods.push(JSON.parse(init.body).method);return x.f.mcp.fetch(String(url),init);}});
    const c=new Client({name:'auto-negotiation-fixture',version:'1.0.0'},{versionNegotiation:{mode:'auto'}});t.after(()=>c.close());await c.connect(transport());assert.equal(c.getProtocolEra(),'modern');assert.equal(methods[0],'server/discover');assert.ok(!methods.includes('initialize'));
    const unsupported=new Client({name:'unsupported-pin-fixture',version:'1.0.0'},{versionNegotiation:{mode:{pin:'2099-01-01'}}});t.after(()=>unsupported.close());await assert.rejects(unsupported.connect(transport()));assert.ok(!methods.includes('initialize'));
});

test('modern reviewed Notes acceptance and v1 original recovery preserve exact content and one receipt after response loss',async t=>{
    const x=await setup(t);let lose=true;
    const modern=await client(t,x.f,x.token,true,async(url,init)=>{const request=JSON.parse(init.body),response=await x.f.mcp.fetch(String(url),init);if(lose&&request.params?.name==='accept_note_command'){lose=false;await response.text();throw new Error('Synthetic acknowledgement loss');}return response;});
    const before=x.upstream,prepared=await call(modern.c,'prepare_note_command',{requestId:crypto.randomUUID(),notebookId:x.n.notebookId,operation:'create',expectedNotebookRevision:0,expectedNoteRevision:0,title:'Private synthetic document',markdown:'# Exact\r\n🧥\r\n```\r\nsource\r\n```\r\n'});
    await assert.rejects(modern.c.callTool({name:'accept_note_command',arguments:{notebookId:x.n.notebookId,commandId:prepared.preparedCommandId}}));
    const legacy=await client(t,x.f,x.token,false),receipt=await call(legacy.c,'get_note_command',{notebookId:x.n.notebookId,commandId:prepared.preparedCommandId});assert.equal(receipt.receipt.payloadHash,prepared.payloadHash);
    const duplicate=await call(modern.c,'accept_note_command',{notebookId:x.n.notebookId,commandId:prepared.preparedCommandId});assert.deepEqual(duplicate.receipt,receipt.receipt);
    const note=await call(modern.c,'get_note',{noteId:prepared.command.noteId,noteRevision:receipt.receipt.noteRevision});assert.equal(note.note.markdown,prepared.after.markdown);assert.equal(x.upstream,before);
    const row=await x.f.workspaceDb.prepare('SELECT count(*) n FROM personal_note_receipts').first();assert.equal(row.n,1);
});

test('modern malformed envelopes, unsupported versions, content types and headers never fall back to legacy tools',async t=>{
    const x=await setup(t),valid=envelope('tools/list');
    for(const [body,headers,status] of [
        [envelope('tools/list',{},'2099-01-01'),{},400],
        [{...valid,params:{_meta:{[PROTOCOL_VERSION_META_KEY]:modernVersion,[CLIENT_CAPABILITIES_META_KEY]:'bad'}}},{},400],
        [{jsonrpc:'2.0',id:1,method:'tools/list',params:{}},{'mcp-protocol-version':modernVersion},400],
        [valid,{'mcp-protocol-version':'2025-11-25'},400],
        [valid,{'content-type':'text/plain; a=application/json'},415],
    ]){const r=await rpc(x.f,x.token,body,headers);assert.equal(r.status,status,await r.clone().text());assert.equal(r.headers.get('cache-control'),'no-store');}
    const denied=await rpc(x.f,x.token,envelope('events/subscribe',{name:'not-implemented'}));assert.equal((await denied.json()).error.code,-32601);
});

test('modern requests retain authenticated Origin, audience, scopes and bounded bodies before dispatch',async t=>{
    const x=await setup(t),before=x.upstream;
    assert.equal((await rpc(x.f,x.token,envelope('tools/list'),{origin:'https://evil.invalid'})).status,403);
    assert.equal((await rpc(x.f,'invalid-token',envelope('server/discover'))).status,401);
    const readOnly=await grant(x.f,x.p,'profile:read tasks:read');assert.equal((await rpc(x.f,readOnly,envelope('tools/call',{name:'get_note',arguments:{noteId:crypto.randomUUID()}}))).status,403);
    const excessive=await x.f.mcp.fetch(resource,{method:'POST',headers:{authorization:'Bearer '+readOnly,'content-type':'application/json'},body:' '.repeat(1024*1024+1)});assert.equal(excessive.status,400);
    const wrongAudience=await exchange(x.f,await authorization(x.f,x.p,'profile:read'),{resource:'https://wrong.invalid/mcp'});assert.equal(wrongAudience.status,400);assert.equal(x.upstream,before);
});

test('per-request modern authorization cannot transfer Notes receipts between concurrent profiles or survive revoke',async t=>{
    const x=await setup(t),other=await enrolled(x.f,accounts[1]),otherToken=await grant(x.f,other),a=await client(t,x.f,x.token),b=await client(t,x.f,otherToken);
    const prepared=await call(a.c,'prepare_note_command',{requestId:crypto.randomUUID(),notebookId:x.n.notebookId,operation:'create',expectedNotebookRevision:0,expectedNoteRevision:0,title:'Private A',markdown:'No B access'});await call(a.c,'accept_note_command',{notebookId:x.n.notebookId,commandId:prepared.preparedCommandId});
    const results=await Promise.all(Array.from({length:4},(_,i)=>(i%2?b.c:a.c).callTool({name:'get_note_command',arguments:{notebookId:x.n.notebookId,commandId:prepared.preparedCommandId}})));
    assert.equal(results.filter(r=>r.isError!==true).length,2);assert.ok(results.filter(r=>r.isError===true).every(r=>!JSON.stringify(r).includes('Private A')));
    await x.f.db.prepare('UPDATE integration_profiles SET grant_epoch=? WHERE profile_id=?').bind(crypto.randomUUID(),x.p.profileId).run();
    assert.equal((await rpc(x.f,x.token,envelope('tools/list'))).status,401);
});
