import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import https from 'node:https';
import tls from 'node:tls';
import net from 'node:net';
import {sendPinned} from '../src/transport.mjs';

async function server(t,handler) {
  const dir=await fs.mkdtemp(path.join(os.tmpdir(),'callback-egress-tls-'));
  t.after(()=>fs.rm(dir,{recursive:true,force:true}));
  execFileSync('openssl',['req','-x509','-newkey','rsa:2048','-nodes','-keyout',path.join(dir,'key.pem'),'-out',path.join(dir,'cert.pem'),
    '-days','1','-subj','/CN=callback.example.org','-addext','subjectAltName=DNS:callback.example.org'],{timeout:5000,stdio:'ignore'});
  const ca=await fs.readFile(path.join(dir,'cert.pem')),key=await fs.readFile(path.join(dir,'key.pem'));
  const s=https.createServer({key,cert:ca},handler);const sockets=new Set();
  s.on('connection',socket=>{sockets.add(socket);socket.on('close',()=>sockets.delete(socket));});
  await new Promise(resolve=>s.listen(0,'127.0.0.1',resolve));
  t.after(async()=>{for(const socket of sockets)socket.destroy();await new Promise(resolve=>s.close(resolve));});
  return {ca,port:s.address().port};
}
test('actual local TLS verifies chain/original hostname, exact body, Host and SNI before HTTP',async t=>{
  const calls=[],s=await server(t,async(req,res)=>{const chunks=[];for await(const chunk of req)chunks.push(chunk);calls.push({body:Buffer.concat(chunks),host:req.headers.host,sni:req.socket.servername});res.end('{"challenge":"local"}');});
  const url=new URL(`https://callback.example.org:${s.port}/challenge?exact=1`),body=Buffer.from('{"test":"€"}\n');
  const input={url,address:'127.0.0.1',body,headers:{'content-type':'application/json'},signal:AbortSignal.timeout(1000)};
  const r=await sendPinned(input,{ca:s.ca});assert.equal(r.status,200);assert.equal(r.body.toString(),'{"challenge":"local"}');
  assert.equal(calls.length,1);assert.deepEqual(calls[0].body,body);assert.equal(calls[0].host,url.host);assert.equal(calls[0].sni,'callback.example.org');
  await assert.rejects(sendPinned({...input,signal:AbortSignal.timeout(1000)}));
  await assert.rejects(sendPinned({...input,url:new URL(`https://wrong.example.org:${s.port}/`),signal:AbortSignal.timeout(1000)},{ca:s.ca}));
  assert.equal(calls.length,1);
});
test('connected remote-address mismatch refuses before writing any HTTP data',async t=>{
  let sends=0;const s=await server(t,(_req,res)=>{sends++;res.end();});
  const url=new URL(`https://callback.example.org:${s.port}/`);
  await assert.rejects(sendPinned({url,address:'1.1.1.1',body:Buffer.from('never'),headers:{},signal:AbortSignal.timeout(1000)},
    {ca:s.ca,connect:options=>tls.connect({...options,host:'127.0.0.1'})}));
  assert.equal(sends,0);
});
test('actual TLS slow body and oversized response hit bounded deadline/bytes and close sockets',async t=>{
  const s=await server(t,(req,res)=>{if(req.url==='/large')res.end('x'.repeat(8193));else if(req.url==='/headers'){res.setHeader('x-fixture','x'.repeat(9000));res.end();}else {res.writeHead(200);res.write('partial');}});
  for(const route of ['/large','/headers','/slow']) {
    const began=Date.now();await assert.rejects(sendPinned({url:new URL(`https://callback.example.org:${s.port}${route}`),address:'127.0.0.1',body:Buffer.from('x'),headers:{},signal:AbortSignal.timeout(150)},{ca:s.ca}));
    assert.ok(Date.now()-began<1000);
  }
});
test('TLS handshake without acknowledgement is cancelled before HTTP or body writes',async t=>{
  const sockets=new Set();let bytes=0;
  const server=net.createServer(socket=>{sockets.add(socket);socket.on('data',chunk=>bytes+=chunk.length);socket.on('close',()=>sockets.delete(socket));});
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  t.after(async()=>{for(const socket of sockets)socket.destroy();await new Promise(resolve=>server.close(resolve));});
  const start=Date.now();
  await assert.rejects(sendPinned({url:new URL(`https://callback.example.org:${server.address().port}/`),address:'127.0.0.1',body:Buffer.from('never transmit HTTP'),headers:{},signal:AbortSignal.timeout(100)}));
  assert.ok(Date.now()-start<1000);assert.ok(bytes>0); // TLS ClientHello only; no completed TLS/HTTP session.
});
