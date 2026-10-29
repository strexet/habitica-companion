import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {journal} from '../src/journal.ts';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/tag-projection-v1.json',import.meta.url),'utf8'));
for(const row of fixture.cases)test('portable tag ledger: '+row.name,async()=>{
 const original=JSON.stringify(row.document);
 if(!row.valid){assert.throws(()=>workspaceDocument(row.document),undefined,row.name);return;}
 const document=workspaceDocument(row.document);assert.deepEqual(workspaceDocument(JSON.parse(JSON.stringify(document))),document);
 const archive=await journal({document,storeVersion:document.revision,commands:[],events:[]});assert.deepEqual(archive.document,document);
 assert.equal(document.tasks.length,row.document.tasks.length);assert.equal(document.completions.length,0);assert.equal(document.habitScores.length,0);assert.equal(archive.commands.length,0);
 assert.equal(JSON.stringify(row.document),original);
 if(!row.document.tagLedger)assert.equal(Object.hasOwn(document,'tagLedger'),false);
});

test('native receipts survive an additive ledger and logical project removal without new task effects',async()=>{
 const {acceptOrganization}=await import('../src/organization-commands.ts');
 const {projectRemovalFrom,organizationCommand,organizationCommandHash}=await import('../../shared/workspace-organization.ts');
 const document=workspaceDocument(fixture.cases[0].document),project=document.projects[0],before=JSON.stringify(document);
 const command=organizationCommand({id:crypto.randomUUID(),workspaceId:document.id,expectedRevision:document.revision,kind:'project',operation:'remove',entityId:project.id,patch:{removal:projectRemovalFrom(document,project.id)}});
 const result=await acceptOrganization({document,storeVersion:5,commands:[],events:[]},command,'2026-10-21T00:00:00Z');
 assert.equal(result.disposition,'accepted');assert.equal(result.snapshot.document.projects[0].isRemoved,true);assert.deepEqual(result.snapshot.document.tagLedger,document.tagLedger);
 assert.deepEqual(result.snapshot.commands,[]);assert.deepEqual(result.snapshot.events,[]);assert.equal(result.entry.payloadHash,await organizationCommandHash(command));assert.equal(JSON.stringify(document),before);
 const history=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/organization-history-v1.json',import.meta.url),'utf8'));
 const native=workspaceDocument(history.cases.find(row=>row.name==='all entity kinds preserve create and partial update snapshots').document),original=JSON.stringify(native.organizationHistory),p=native.projects[0];
 const {tagAlias}=await import('../../shared/tag-projection.ts');
 native.tagLedger={accountId:document.tagLedger.accountId,mappings:[{...document.tagLedger.mappings[0],id:crypto.randomUUID(),entityId:p.id,kind:'project',revision:native.revision,desiredRevision:native.revision,desiredName:p.name,correlationAlias:tagAlias(native.id,'project',p.id)}],intents:[]};
 const restored=await journal({document:native,storeVersion:native.revision,commands:[],events:[]});assert.equal(JSON.stringify(restored.document.organizationHistory),original);assert.deepEqual(restored.commands,[]);
});
