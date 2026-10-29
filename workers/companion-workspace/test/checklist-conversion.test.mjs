import test from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {conversionCommand,conversionHash,conversionDerivedId} from '../../shared/checklist-conversion.ts';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {journal} from '../src/journal.ts';import {acceptConversion,acceptCloudConversion} from '../src/checklist-conversion.ts';
const fixture=async name=>JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/checklist-conversion-'+name+'.json',import.meta.url),'utf8'));
function documentShape(document){const result=structuredClone(workspaceDocument(document));delete result.activityCoverage;for(const projection of result.projections)projection.id='projection:'+projection.taskId;return result;}
for(const name of ['reuse','create'])test('portable '+name+' conversion matches C# identity, exact task bindings, complete source content and historical uncertainty',async()=>{
 const x=await fixture(name),state=await journal(x.initial),command=conversionCommand(x.command),now=x.acceptedDocument.checklistConversions[0].receipts[0].acceptedAtUtc;
 assert.equal(await conversionHash(command),x.hash);assert.equal(await conversionDerivedId(command.id,'project'),x.derivedId);
 const result=await acceptConversion(state,command,now);assert.equal(result.disposition,'accepted',result.errorCode);assert.deepEqual(documentShape(result.snapshot.document),documentShape(x.acceptedDocument));assert.equal(result.snapshot.storeVersion,x.acceptedStoreVersion);await journal(result.snapshot);if(name==='create')assert.equal(result.snapshot.document.activityCoverage.length,8);
 assert.deepEqual(result.snapshot.document.tasks.find(task=>task.id===command.sourceTaskId),state.document.tasks.find(task=>task.id===command.sourceTaskId));assert.deepEqual(result.snapshot.document.completions,state.document.completions);
 const duplicate=await acceptConversion(result.snapshot,command,now);assert.equal(duplicate.disposition,'duplicate');assert.equal(duplicate.snapshot.document.revision,result.snapshot.document.revision);
 const resume={...command,id:crypto.randomUUID(),expectedRevision:result.snapshot.document.revision,items:command.items.map(item=>item.targetTaskId?{...item,mode:'reuse',notes:null,gameDetails:null}:item)},resumed=await acceptConversion(result.snapshot,resume,now);
 assert.equal(resumed.disposition,'accepted',resumed.errorCode);assert.equal(resumed.snapshot.document.tasks.length,result.snapshot.document.tasks.length);assert.equal(resumed.snapshot.commands.length,result.snapshot.commands.length);
 const partial=structuredClone(resumed.snapshot.document);partial.checklistConversions[0].receipts.at(-1).command.items.pop();assert.throws(()=>workspaceDocument(partial));
 const impossible=structuredClone(resumed.snapshot.document);impossible.checklistConversions[0].receipts.at(-1).command.expectedRevision=command.sourceRevision;assert.throws(()=>workspaceDocument(impossible));assert.deepEqual(resumed.conversion.items.map(item=>item.targetTaskId),result.conversion.items.map(item=>item.targetTaskId));
 if(name==='create'){assert.equal(result.conversion.items.filter(item=>item.targetTaskId===null).length,2);for(const entry of result.snapshot.commands.slice(state.commands.length)){assert.deepEqual(entry.effects,['create']);assert.equal(entry.confirmedEffects,0);assert.equal(entry.command.projectToHabitica,true);}}
 else {assert.equal(result.snapshot.document.tasks.filter(task=>task.isArchived).length,2);assert.equal(result.snapshot.commands.length,state.commands.length);assert.equal(result.snapshot.events.length,state.events.length);}
});
test('conversion rejects source edits, identity rebinding, incomplete selections and unapproved command fields',async()=>{
 const x=await fixture('reuse'),state=await journal(x.initial),command=conversionCommand(x.command),now=x.acceptedDocument.checklistConversions[0].receipts[0].acceptedAtUtc,saved=(await acceptConversion(state,command,now)).snapshot;
 assert.equal((await acceptConversion(saved,{...command,id:crypto.randomUUID(),conversionId:crypto.randomUUID(),expectedRevision:saved.document.revision},now)).errorCode,'resume_original_conversion_required');
 const changed={...command,id:crypto.randomUUID(),expectedRevision:saved.document.revision,items:command.items.map((item,index)=>index?item:{...item,targetTaskId:crypto.randomUUID()})};assert.equal((await acceptConversion(saved,changed,now)).errorCode,'conversion_binding_changed');
 const edited=structuredClone(saved);edited.document.revision++;edited.storeVersion++;edited.document.tasks.find(task=>task.id===command.sourceTaskId).revision=edited.document.revision;
 assert.equal((await acceptConversion(edited,{...command,id:crypto.randomUUID(),expectedRevision:edited.document.revision},now)).errorCode,'conversion_source_changed');assert.equal((await acceptConversion(edited,command,now)).disposition,'duplicate');
 assert.equal((await acceptConversion(state,{...command,items:command.items.slice(1)},now)).errorCode,'complete_checklist_mapping_required');
 for(const fields of [{profileId:crypto.randomUUID()},{items:[{...command.items[0],mode:1}]},{items:command.items.map((item,index)=>index?item:{...item,targetTaskId:command.sourceTaskId})}])assert.throws(()=>conversionCommand({...command,...fields}));
 const forged=structuredClone(saved);forged.document.checklistConversions[0].items[0].targetTaskId=command.items[1].targetTaskId;assert.throws(()=>workspaceDocument(forged.document));
 const forgedHash=structuredClone(saved);forgedHash.document.checklistConversions[0].receipts[0].payloadHash='0'.repeat(64);await assert.rejects(()=>journal(forgedHash));
});
test('local and cloud conversion ownership remain separate; imported metadata grants no owner lane',async()=>{
 const x=await fixture('reuse'),state=await journal(x.initial),now=x.acceptedDocument.checklistConversions[0].receipts[0].acceptedAtUtc;
 assert.equal((await acceptCloudConversion(state,x.command,now)).errorCode,'workspace_owned_elsewhere');state.authority={mode:'cloud',transferId:null,cloudWorkspaceId:state.document.id};assert.equal((await acceptConversion(state,x.command,now)).errorCode,'workspace_owned_elsewhere');assert.equal((await acceptCloudConversion(state,x.command,now)).disposition,'accepted');
});

test('conversion tools require both write grants, bound cloud captures and refuse proxy fields',async()=>{
 const {conversionInput,conversionToolScopes}=await import('../../shared/conversion-tools.ts'),{conversionTools}=await import('../../chatgpt-mcp/src/conversion-tools.ts');assert.equal(conversionTools.length,4);assert.deepEqual(conversionToolScopes.accept_checklist_conversion,['profile:read','habitica.tasks.write','workspace.organization.write']);
 for(const tool of conversionTools){assert.equal(tool.inputSchema.additionalProperties,false);assert.deepEqual(tool.securitySchemes,tool._meta.securitySchemes);assert.ok(!Object.hasOwn(tool.inputSchema.properties,'profileId'));}
 const x=await fixture('create'),input={requestId:crypto.randomUUID(),sourceTaskId:x.command.sourceTaskId,sourceRevision:x.command.sourceRevision,expectedRevision:x.command.expectedRevision,projectName:'Reviewed project',items:x.command.items.map(item=>{const {targetTaskId,...other}=item;return other;})};assert.equal(conversionInput('prepare_checklist_conversion',input).sourceTaskId,x.command.sourceTaskId);
 assert.throws(()=>conversionInput('prepare_checklist_conversion',{...input,profileId:crypto.randomUUID()}));assert.throws(()=>conversionInput('prepare_checklist_conversion',{...input,items:Array.from({length:11},()=>({...input.items[0],sourceItemId:crypto.randomUUID()}))}));assert.throws(()=>conversionInput('prepare_checklist_conversion',{...input,items:[{...input.items[0],targetTaskId:crypto.randomUUID()}]}));
});
