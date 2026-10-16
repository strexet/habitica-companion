import test from 'node:test';import assert from 'node:assert/strict';
import {fixture,enrolled,authorization,exchange,authPage,issuer,resource} from './fixtures/oauth.mjs';
const clientId='https://chatgpt.com/oauth/codex/client.json';
test('Codex-shaped CIMD loopback flow accepts selected ports with exact callback, issuer and audience',async t=>{
 const f=await fixture(t,{clientMetadata:{client_id:clientId,client_name:'Fixture <script>untrusted</script>',redirect_uris:['http://127.0.0.1/callback'],token_endpoint_auth_method:'none'}});const profile=await enrolled(f);
 for(const port of [43127,43128]){
  const redirect=`http://127.0.0.1:${port}/callback`;
  const grant=await authorization(f,profile,'profile:read account:read',{client_id:clientId,redirect_uri:redirect});assert.equal(grant.callback,redirect);
  const response=await exchange(f,grant);assert.equal(response.status,200);const tokens=await response.json();
  const validation=await(await f.internal.fetch('https://auth.internal/validate',{method:'POST',body:JSON.stringify({resource,token:tokens.access_token})})).json();assert.equal(validation.props.profileId,profile.profileId);assert.equal(validation.clientId,clientId);assert.equal(validation.audience,resource);
  const refreshed=await f.auth.fetch(issuer+'/oauth/token',{method:'POST',headers:{'content-type':'application/x-www-form-urlencoded'},body:new URLSearchParams({grant_type:'refresh_token',refresh_token:tokens.refresh_token,client_id:clientId,resource})});assert.equal(refreshed.status,200);
 }
 for(const redirect of ['http://localhost:43127/callback','http://127.0.0.1:43127/different'])assert.equal((await authPage(f,'profile:read',{client_id:clientId,redirect_uri:redirect})).response.status,400);
});
