import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {workspaceDocument} from '../../shared/workspace-domain.ts';import {organizationCommand,organizationCommandHash} from '../../shared/workspace-organization.ts';
import {acceptOrganization} from '../src/organization-commands.ts';import {journal} from '../src/journal.ts';import {queryOrganization} from '../../shared/organization-query.ts';import {organizationToolInput} from '../../shared/organization-tools.ts';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/project-alias-v1.json',import.meta.url),'utf8')),now='2026-10-23T10:00:00Z';
const initial=()=>structuredClone(fixture.initial);
const command=(s,patch,project)=>({id:crypto.randomUUID(),workspaceId:s.document.id,expectedRevision:s.document.revision,kind:'project',operation:project?'update':'create',entityId:project??crypto.randomUUID(),patch});
async function apply(s,c){const r=await acceptOrganization(s,c,now);assert.equal(r.disposition,'accepted',r.errorCode);return journal(r.snapshot);}
test('portable alias hashes, lifecycle clear and original receipts preserve task journals',async()=>{
 let s=initial();for(const step of fixture.steps){const c=organizationCommand(step.command);assert.equal(await organizationCommandHash(c),step.payloadHash,step.name);s=await apply(s,c);assert.deepEqual(s.document.organizationHistory.at(-1).snapshot,step.snapshot);assert.deepEqual(s.commands,[]);assert.deepEqual(s.events,[]);assert.deepEqual(s.document.tasks,[]);}
 assert.deepEqual(s,fixture.final);for(const entry of s.document.organizationHistory){const r=await acceptOrganization(s,entry.command,now);assert.equal(r.disposition,'duplicate');assert.deepEqual(r.entry,entry);}
 assert.equal(s.document.projects[1].isRemoved,true);assert.equal(s.document.projects[1].alias,undefined);
});
test('invalid aliases and unrelated fields refuse without state changes',async()=>{
 const s=initial();for(const alias of ['', ' ', 'a--b', '-a','a-','a_b','a b','česky','K','<codev>','a'.repeat(65)]){assert.throws(()=>organizationCommand(command(s,{name:'Name',alias})));}
 assert.doesNotThrow(()=>organizationCommand(command(s,{name:'Name',alias:'a'.repeat(64)})));
 for(const c of [command(s,{name:'Name',alias:'valid',clearAlias:true}),{...command(s,{name:'Name',alias:'valid'}),kind:'label'},command(s,{name:'Name',clearAlias:true})])assert.equal((await acceptOrganization(s,c,now)).disposition,'invalid');
 const args={requestId:crypto.randomUUID(),kind:'project',operation:'create',patch:{name:'Name',alias:'valid'}};
 assert.throws(()=>organizationToolInput('prepare_organization_command',args),e=>e.code==='reviewed_revision_required');assert.doesNotThrow(()=>organizationToolInput('prepare_organization_command',{...args,expectedRevision:1}));
});
test('duplicate imports, archived reservation, stale competing updates and account isolation',async()=>{
 let s=initial();const a=command(s,{name:'Original',alias:'  CODEV  '});s=await apply(s,a);const before=structuredClone(s),b=command(s,{name:'Other',alias:'CodeV'});
 assert.equal((await acceptOrganization(s,b,now)).errorCode,'project_alias_in_use');s=await apply(s,command(s,{name:'Renamed'},a.entityId));assert.equal(s.document.projects[0].alias,'codev');assert.equal((await acceptOrganization(s,b,now)).disposition,'revisionConflict');
 s=await apply(s,{...command(s,null,a.entityId),operation:'archive'});assert.equal((await acceptOrganization(s,command(s,{name:'Other',alias:'CODEV'}),now)).errorCode,'project_alias_in_use');
 assert.equal((await acceptOrganization(s,command(s,{name:'Hidden edit',clearAlias:true},a.entityId),now)).errorCode,'organization_unavailable');
 const other=initial();other.document.id=crypto.randomUUID();assert.equal((await apply(other,command(other,{name:'Other account',alias:'codev'}))).document.projects[0].alias,'codev');
 const duplicate=structuredClone(before.document);duplicate.projects.push({...duplicate.projects[0],id:crypto.randomUUID()});assert.throws(()=>workspaceDocument(duplicate));duplicate.projects.pop();duplicate.projects[0].alias='CodeV';assert.throws(()=>workspaceDocument(duplicate));
 const marker=await apply(initial(),command(initial(),{name:'<codev> Marker only'}));assert.equal(marker.document.projects[0].alias,undefined);
 assert.deepEqual(s.document.tasks,before.document.tasks);assert.deepEqual(s.commands,before.commands);assert.deepEqual(s.events,before.events);
});
test('resolution returns exact owned ID, retains lifecycle and refuses ambiguity',()=>{
 const d=workspaceDocument(fixture.final.document),a=d.projects[0],b=d.projects[2];assert.equal(queryOrganization(d,'resolve_project',{reference:' NEW-42 '}).project.id,b.id);assert.equal(queryOrganization(d,'resolve_project',{reference:a.id}).project.id,a.id);
 d.projects[0].name='new-42';assert.throws(()=>queryOrganization(d,'resolve_project',{reference:'new-42'}),e=>e.code==='ambiguous_project');assert.throws(()=>queryOrganization(d,'resolve_project',{reference:'<new-42>'}),e=>e.code==='project_unavailable');
 assert.throws(()=>queryOrganization(d,'resolve_project',{reference:b.id,expectedRevision:1}),e=>e.code==='stale_revision');assert.throws(()=>queryOrganization(d,'resolve_project',{reference:b.id,profileId:crypto.randomUUID()}));
});

test('imported history rejects a hidden intermediate alias collision',async()=>{
 let s=initial();const a=command(s,{name:'A',alias:'reserved'});s=await apply(s,a);const b=command(s,{name:'B',alias:'unique'});s=await apply(s,b);s=await apply(s,command(s,{alias:'final'},b.entityId));
 const row=s.document.organizationHistory[1];row.command.patch.alias='reserved';row.snapshot.alias='reserved';row.payloadHash=await organizationCommandHash(row.command);await assert.rejects(journal(s));
});
