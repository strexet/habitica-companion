import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { workspaceCommand, commandHash } from '../../shared/workspace-commands.ts';
const fixture = JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/command-v1.json', import.meta.url), 'utf8'));
test('neutral command fixture preserves UTF-8 identity across C# and Worker contracts', async () => {
  const command = workspaceCommand(fixture.command);
  assert.equal(await commandHash(command), fixture.payloadHash);
  assert.notEqual(await commandHash({ ...command, patch: { ...command.patch, notes: null } }), fixture.payloadHash);
});
test('Worker command contract rejects selectors, numeric coercion and ignored fields', () => {
  const valid = fixture.command;
  for (const bad of [{ ...valid, profileId: 'other' }, { ...valid, count: '1' }, { ...valid, direction: 'up' }, { ...valid, expectedRevision: Number.MAX_SAFE_INTEGER + 1 }, { ...valid, patch: { ...valid.patch, userId: 'other' } }, { ...valid, patch: { ...valid.patch, priority: 2 } }, { ...valid, patch: { ...valid.patch, deadline: '2026-02-30' } }, { ...valid, operation: 'genericProxy' }, { ...valid, operation: ['createTodo'] }, { ...valid, patch: { ...valid.patch, priority: ['high'] } }, { ...valid, patch: { ...valid.patch, text: '\u0085' } }]) assert.throws(() => workspaceCommand(bad));
});

test('extended task settings have portable identity and reject invalid nested payloads',async()=>{
 const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/command-v2.json',import.meta.url),'utf8'));
 const command=workspaceCommand(fixture.command);assert.equal(await commandHash(command),fixture.payloadHash);
 const details=command.patch.habiticaDetails;
 for(const invalid of [{difficulty:3},{difficulty:'1'},{attribute:'strength'},{attribute:['str']},{checklist:[{id:crypto.randomUUID(),text:'Bad',completed:'false'}]},{checklist:[details.checklist[0],details.checklist[0]]},{checklist:[{...details.checklist[0],text:'x'.repeat(257)}]},{endpoint:'other'},{}])assert.throws(()=>workspaceCommand({...fixture.command,patch:{habiticaDetails:invalid}}));
 const changed=structuredClone(command);changed.patch.habiticaDetails.checklist[0].completed=true;assert.notEqual(await commandHash(changed),fixture.payloadHash);
});


test('native lifecycle hashes match portable fixtures and deletion rejects unused payload fields',async()=>{
 const rows=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/lifecycle-commands.json',import.meta.url),'utf8'));
 for(const row of rows){const command=workspaceCommand(row.command);assert.equal(await commandHash(command),row.payloadHash);}
 const deletion=rows.find(row=>row.command.operation==='deleteTask').command;
 for(const invalid of [{...deletion,patch:{text:'Unexpected'}},{...deletion,count:2},{...deletion,direction:'up'},{...deletion,occurrenceDate:'2026-10-17'}])assert.throws(()=>workspaceCommand(invalid));
});
