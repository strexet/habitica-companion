// Create a reviewable, credential-free source bundle. This command never contacts Netlify.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
const root=path.resolve(import.meta.dirname,'../..');
const destination=path.join(root,'skill-tree/artifacts/callback-egress/source');
if(fs.existsSync(destination))throw Error('Source bundle already exists; select another artifact directory after review.');
const archive=path.join(destination,'../callback-egress-source.zip');
if(fs.existsSync(archive))throw Error('Source archive already exists; refusing to retain stale archive entries.');
const own='deploy/callback-egress/';
const files=['package.json','netlify.toml','prepare-source.mjs','public/robots.txt','src/gateway.mjs','src/transport.mjs',
  'netlify/functions/callback-egress.mts','test/gateway.test.mjs','test/tls.test.mjs','test/package.test.mjs'].map(p=>own+p);
files.push('workers/chatgpt-auth/src/callback-egress-protocol.ts');
const hashes={};
for(const file of files){const bytes=fs.readFileSync(path.join(root,file));const output=path.join(destination,file);fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(output,bytes);hashes[file]=createHash('sha256').update(bytes).digest('hex');}
fs.copyFileSync(path.join(root,own,'netlify.toml'),path.join(destination,'netlify.toml'));
fs.writeFileSync(path.join(destination,'source-manifest.json'),JSON.stringify({version:'callback-egress-v1',files:hashes,publication:false},null,2)+'\n');
execFileSync('zip',['-qr','../callback-egress-source.zip','.'],{cwd:destination,timeout:10000});
console.log('Prepared skill-tree/artifacts/callback-egress/callback-egress-source.zip; no publication.');
