import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import vm from 'node:vm';

// Exercise production journal functions with serialized transactions. Real IndexedDB
// scheduling remains browser qualification, separate from this transition control.
async function fixture() {
  const rows = new Map(); let gate = Promise.resolve();
  const kv = {get: async key => rows.get(key), put: async row => rows.set(row.key, row)};
  const database = {table: () => kv, version: () => ({stores() {}}),
    transaction: (...args) => {const call = gate.then(args.at(-1)); gate = call.catch(() => {}); return call;}};
  const source = (await readFile(new URL('../../src/Habitica.WebApp/wwwroot/js/storage/indexedDbStorage.js', import.meta.url), 'utf8'))
    .replace(/^import .*;$/m, '').replace('const database = new Dexie("habitica-tool");', '')
    .replaceAll('export async function', 'async function').replaceAll('export function', 'function');
  const context = vm.createContext({database, Date, structuredClone});
  vm.runInContext(source, context);
  return {commit: (scope, rev, value) => context.commitGearRecovery(scope, rev, JSON.stringify(value)),
    load: scope => context.getGearRecovery(scope)};
}
const initial = () => ({operationId:'11111111-1111-4111-8111-111111111111', revision:1,
  slots:[{slot:'Weapon',originalKey:'weapon_warrior_1',temporaryKey:'weapon_wizard_1',status:0}]});
const change = (value, status) => ({...value, revision:value.revision+1, slots:value.slots.map(slot => ({...slot,status}))});

test('duplicate revision reserves a toggle once and account scopes stay separate', async () => {
  const f = await fixture(); const value = initial();
  assert.equal(await f.commit('owner_a',0,value),true);
  assert.equal(await f.load('owner_b'),null);
  const results = await Promise.all([f.commit('owner_a',1,change(value,1)),f.commit('owner_a',1,change(value,1))]);
  assert.deepEqual(results,[true,false]);
});
test('unresolved operation cannot be replaced and original slots remain immutable',async () => {
  const f = await fixture(); const value=initial(); await f.commit('owner',0,value);
  await assert.rejects(f.commit('owner',1,{...value,revision:2,operationId:'22222222-2222-4222-8222-222222222222'}),/Unresolved/);
  const changed=change(value,1);changed.slots[0].originalKey='weapon_warrior_2';
  await assert.rejects(f.commit('owner',1,changed),/immutable/);
});
test('lost acknowledgement stays reserved until observation and terminal slots cannot reopen', async () => {
  const f=await fixture();let value=initial();await f.commit('owner',0,value);
  let next=change(value,1);await f.commit('owner',value.revision,next);value=next;
  await assert.rejects(f.commit('owner',value.revision,change(value,0)),/transition/);
  next=change(value,3);await f.commit('owner',value.revision,next);value=next;
  await assert.rejects(f.commit('owner',value.revision,change(value,1)),/immutable/);
  const replacement={...initial(),revision:value.revision+1,operationId:'22222222-2222-4222-8222-222222222222'};
  assert.equal(await f.commit('owner',value.revision,replacement),true);
});
test('invalid scope, slot and nonprepared initial records are rejected',async () => {
  const f=await fixture();const value=initial();
  await assert.rejects(f.commit('../owner',0,value),/scope/);
  await assert.rejects(f.commit('owner',0,{...value,slots:[{...value.slots[0],slot:'Other'}]}),/journal/);
  await assert.rejects(f.commit('owner',0,{...value,slots:[{...value.slots[0],status:1}]}),/precede dispatch/);
});
