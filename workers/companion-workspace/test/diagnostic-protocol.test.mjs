import test from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {diagnosticBundle,diagnosticHash,diagnosticBytes,diagnosticRequestId,diagnosticRequestTime,diagnosticQuery} from '../../shared/diagnostics.ts';
import {diagnosticCatalog} from '../../shared/diagnostic-catalog.ts';
test('neutral C#/Worker upload bytes and hashes match reviewed canonical sanitization',async()=>{
 const x=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/diagnostic-sharing-v1.json',import.meta.url),'utf8')),b=diagnosticBundle(x.bundle);assert.equal(await diagnosticHash(b),x.hash);assert.equal(diagnosticBytes(JSON.stringify(b)),x.payloadBytes);assert.deepEqual(b,x.bundle);
 const raw=structuredClone(b);raw.entries[0].message='private notes credential-canary';raw.entries[0].metadata.authorization='credential-canary';assert.deepEqual(diagnosticBundle(raw),b);
 for(const mutate of [x=>x.version=2,x=>x.entries[0].operation='workspace-tag-private-title',x=>x.profileId='prf_foreign',x=>x.entries[0].accountId=crypto.randomUUID(),x=>x.entries[0].occurredAtUtc='2026-02-30T00:00:00Z',x=>x.entries.push(x.entries[0])]){const bad=structuredClone(b);mutate(bad);assert.throws(()=>diagnosticBundle(bad));}
 const at=Date.now();assert.equal(diagnosticRequestTime(diagnosticRequestId(at)),at);assert.throws(()=>diagnosticRequestTime(crypto.randomUUID()));assert.throws(()=>diagnosticQuery({}));
});
test('Worker catalog matches the exact reviewed C# sanitizer operation set',async()=>{
 const source=await readFile(new URL('../../../src/Habitica.Domain/Diagnostics/DiagnosticsLogSanitizer.cs',import.meta.url),'utf8'),body=source.split(' Operations =')[1].split('};')[0],operations=Array.from(body.matchAll(/"([^"\\]*)"/g),m=>m[1]);assert.deepEqual([...diagnosticCatalog.operations].sort(),[...operations,'app-operation'].sort());
});
