import test from 'node:test';
import assert from 'node:assert/strict';
import {identityEnvelope,clientBuildReport,compatibilityDiscovery} from '../../shared/build-identity.ts';
const report=(r={},a=identityEnvelope('auth'),w=identityEnvelope('workspace'))=>compatibilityDiscovery(clientBuildReport(r),a,w);
test('direct MCP has unknown installed package and coherent explicit backend identity',()=>{
 const r=report();assert.equal(r.backend.state,'coherent');assert.equal(r.package.installation,'unknown');assert.equal(r.package.packageCompatibility,'unknown');assert.match(r.backend.workers.mcp.buildHash,/^[a-f0-9]{64}$/);
});
test('client reports never establish installation and incompatible versions stay explicit',()=>{
 for(const [v,expected] of [['0.0.9','incompatible'],['0.1.0','incompatible'],['0.2.0','compatible'],['0.3.0','incompatible'],['2.0.0','incompatible']]){
  const r=report({packageVersion:v,packageHash:'a'.repeat(64),toolSchemaVersion:'2.0.0'});assert.equal(r.package.packageCompatibility,expected);assert.equal(r.package.installation,'unknown');assert.equal(r.package.schemaCompatibility,'incompatible');
 }
});
test('missing, malformed or mixed peers never claim coherent deployment',()=>{
 assert.equal(report({},null).backend.state,'unavailable');assert.equal(report({},identityEnvelope('workspace')).backend.state,'unavailable');
 assert.equal(report({},{...identityEnvelope('auth'),buildHash:'a'.repeat(64)}).backend.state,'mixed');
 const r=report({},{...identityEnvelope('auth'),secret:'do not expose',taskText:'private'});assert.equal(JSON.stringify(r).includes('private'),false);assert.equal(JSON.stringify(r).includes('do not expose'),false);
});
test('unknown selectors, arbitrary claims and malformed hashes are rejected',()=>{
 for(const r of [{profileId:'owner'},{packageVersion:'latest'},{packageHash:'secret'},{toolSchemaVersion:'1.0'},{packageVersion:null}])assert.throws(()=>clientBuildReport(r));
});

test('authenticated MCP discovery needs no workspace consent and never calls Habitica',async t=>{
 const {fixture,enrolled,authorization,exchange,resource}=await import('../../chatgpt-auth/test/fixtures/oauth.mjs');
 const {Client}=await import('../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js');
 const {StreamableHTTPClientTransport}=await import('../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js');
 let upstream=0;const f=await fixture(t,{workspace:true,habitica:request=>{upstream++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}});
 const p=await enrolled(f);const response=await exchange(f,await authorization(f,p,'profile:read'));assert.equal(response.status,200);
 const token=(await response.json()).access_token,c=new Client({name:'build-identity-fixture',version:'1.0.0'});t.after(()=>c.close());
 await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));
 const listed=(await c.listTools()).tools.find(tool=>tool.name==='get_connector_compatibility');assert.ok(listed);assert.equal(listed.annotations.readOnlyHint,true);
 const before=upstream;const result=await c.callTool({name:'get_connector_compatibility',arguments:{}});assert.notEqual(result.isError,true);
 assert.equal(result.structuredContent.backend.state,'coherent');assert.equal(result.structuredContent.package.installation,'unknown');
 const claimed=await c.callTool({name:'get_connector_compatibility',arguments:{packageVersion:'0.1.0',toolSchemaVersion:'2.0.0'}});assert.equal(claimed.structuredContent.package.packageCompatibility,'incompatible');
 const invalid=await c.callTool({name:'get_connector_compatibility',arguments:{profileId:p.profileId}});assert.equal(invalid.isError,true);
 assert.equal(upstream,before);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,0);
 assert.equal((await f.auth.fetch('http://localhost:8787/build-identity')).status,404);
 const unauthorized=await f.mcp.fetch(resource,{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({jsonrpc:'2.0',id:1,method:'tools/call',params:{name:'get_connector_compatibility',arguments:{}}})});assert.equal(unauthorized.status,401);
});
