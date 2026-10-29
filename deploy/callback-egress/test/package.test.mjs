import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {execFileSync} from 'node:child_process';

test('Netlify package pins Node22, has no dependency/key provisioning and retains bounded entry',()=>{
  const root=path.resolve(import.meta.dirname,'..'),config=fs.readFileSync(path.join(root,'netlify.toml'),'utf8');
  assert.match(config,/NODE_VERSION = "22"/);assert.doesNotMatch(config,/AWS_LAMBDA_JS_RUNTIME|GRANT_KEY|CLAIM_KEY/);
  assert.match(config,/directory = "netlify\/functions"/);assert.match(config,/node_bundler = "esbuild"/);
  const pkg=JSON.parse(fs.readFileSync(path.join(root,'package.json')));assert.equal(pkg.engines.node,'22.x');assert.equal(pkg.dependencies,undefined);
  const entry=fs.readFileSync(path.join(root,'netlify/functions/callback-egress.mts'),'utf8');assert.match(entry,/handleGateway\(request, process.env\)/);
  assert.doesNotMatch(entry,/ca:|resolve:|send:|connect:|rejectUnauthorized/);
});

test('source archive contains only allowlisted bytes and refuses stale archive reuse',()=>{
  const taskRoot=path.resolve(import.meta.dirname,'..'),repo=path.resolve(taskRoot,'../..');
  const tmp=fs.mkdtempSync(path.join(os.tmpdir(),'callback-egress-package-'));
  try {
    const prefix='deploy/callback-egress/';
    const files=['package.json','netlify.toml','prepare-source.mjs','public/robots.txt','src/gateway.mjs','src/transport.mjs',
      'netlify/functions/callback-egress.mts','test/gateway.test.mjs','test/tls.test.mjs','test/package.test.mjs'].map(x=>prefix+x);
    files.push('workers/chatgpt-auth/src/callback-egress-protocol.ts');
    for(const file of files){const target=path.join(tmp,file);fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(path.join(repo,file),target);}
    fs.writeFileSync(path.join(tmp,'.env'),'synthetic-secret-not-for-export');
    const run=()=>execFileSync(process.execPath,[path.join(tmp,prefix,'prepare-source.mjs')],{timeout:10000,stdio:'pipe'});
    run();const staging=path.join(tmp,'skill-tree/artifacts/callback-egress/source'),zip=path.join(tmp,'skill-tree/artifacts/callback-egress/callback-egress-source.zip');
    const entries=execFileSync('unzip',['-Z1',zip],{timeout:5000,encoding:'utf8'}).trim().split('\n').filter(x=>!x.endsWith('/')).sort();
    assert.deepEqual(entries,[...files,'netlify.toml','source-manifest.json'].sort());
    const manifest=JSON.parse(fs.readFileSync(path.join(staging,'source-manifest.json')));assert.deepEqual(Object.keys(manifest.files).sort(),files.sort());assert.equal(manifest.publication,false);
    assert.throws(run);fs.rmSync(staging,{recursive:true});assert.throws(run); // archive remains: update-in-place is forbidden
  } finally {fs.rmSync(tmp,{recursive:true,force:true});}
});
