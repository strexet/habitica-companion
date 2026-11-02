import Dexie from "../../vendor/dexie.mjs";

const database = new Dexie("habitica-tool");

database.version(1).stores({
  kv: "&key, updatedAtUtc",
});

database.version(2).stores({
  kv: "&key, updatedAtUtc",
  workspaces: "&[scope+id], scope, updatedAtUtc",
  workspaceCommands: "&[scope+workspaceId+id], [scope+workspaceId]",
  workspaceEvents: "&[scope+workspaceId+id], [scope+workspaceId]",
});

database.version(3).stores({
  kv: "&key, updatedAtUtc",
  workspaces: "&[scope+id], scope, updatedAtUtc",
  workspaceCommands: "&[scope+workspaceId+id], [scope+workspaceId]",
  workspaceEvents: "&[scope+workspaceId+id], [scope+workspaceId]",
  notes: "&scope",
});
const notes = database.table("notes");

const kv = database.table("kv");
const workspaces = database.table("workspaces");
const commands = database.table("workspaceCommands");
const events = database.table("workspaceEvents");

export async function getJson(key) {
  const record = await kv.get(key);
  return record?.jsonText ?? null;
}

export async function setJson(key, jsonText) {
  await kv.put({
    key,
    jsonText,
    updatedAtUtc: new Date().toISOString(),
  });
}

export async function remove(key) {
  await kv.delete(key);
}

const notificationPrefix='notification-state-v1:';
function notificationKey(key) {
  if(typeof key!=='string'||!/^notification-state-v1:[0-9a-f-]{36}:prf_[A-Za-z0-9_-]{43}:[0-9a-f-]{36}:(device|[0-9a-f-]{36})$/.test(key))throw new Error('Invalid notification state context.');
}
export async function getNotificationState(key) {
  notificationKey(key);const row=await kv.get(key);return row?.jsonText??null;
}
export async function commitNotificationState(key,expectedRevision,json) {
  notificationKey(key);
  if(!Number.isSafeInteger(expectedRevision)||expectedRevision<0||typeof json!=='string'||json.length>16384)throw new Error('Invalid notification state.');
  const value=JSON.parse(json);
  if(value?.revision!==expectedRevision+1||Object.keys(value).some(k=>!['revision','binding','pending'].includes(k)))throw new Error('Invalid notification state.');
  // Persistent typed records carry opaque binding/original-request metadata only.
  const forbidden=new Set(['endpoint','keys','p256dh','auth','acceptToken','managementToken','apiToken','subscription']);
  const stack=[value];while(stack.length){const item=stack.pop();if(item&&typeof item==='object')for(const [name,child] of Object.entries(item)){if(forbidden.has(name))throw new Error('Secret material cannot be persisted.');if(child&&typeof child==='object')stack.push(child);}}
  return database.transaction('rw',kv,async()=>{
    const old=await kv.get(key),revision=old?JSON.parse(old.jsonText).revision:0;
    if(revision!==expectedRevision)return false;
    await kv.put({key,jsonText:json,updatedAtUtc:new Date().toISOString()});return true;
  });
}
export async function clearNotificationStates(){await kv.where('key').startsWith(notificationPrefix).delete();}

const reminderPrefix = 'foreground-reminders-v1:';
function reminderKey(context) {
  if (typeof context !== 'string' || !/^[0-9a-f-]{36}:(browser|cloud):[0-9a-f-]{36}$/.test(context)) throw new Error('Invalid reminder context.');
  return reminderPrefix + context;
}
function reminderState(row) {
  if (!row) return {version:1,inApp:false,browser:false,receipts:[]};
  const value = JSON.parse(row.jsonText);
  if (value?.version !== 1 || typeof value.inApp !== 'boolean' || typeof value.browser !== 'boolean' || !Array.isArray(value.receipts) || value.receipts.length > 2048
      || value.receipts.some(r => !/^[a-f0-9]{64}$/.test(r.id) || !['attempted','shown','unknown'].includes(r.state) || !Number.isSafeInteger(r.atMs))
      || new Set(value.receipts.map(r => r.id)).size !== value.receipts.length) throw new Error('Reminder state unavailable.');
  return value;
}
export async function readReminderState(context) { return reminderState(await kv.get(reminderKey(context))); }
export async function configureReminderState(context, kind, enabled) {
  if (!['inApp','browser'].includes(kind) || typeof enabled !== 'boolean') throw new Error('Invalid reminder preference.');
  const key = reminderKey(context);
  return database.transaction('rw',kv,async () => {
    const value = reminderState(await kv.get(key));
    if (kind === 'browser' && enabled && !value.inApp) throw new Error('Enable in-app cues first.');
    value[kind] = enabled; if (!value.inApp) value.browser = false;
    await kv.put({key,jsonText:JSON.stringify(value),updatedAtUtc:new Date().toISOString()});return value;
  });
}
// A durable claim precedes dispatch. Lost acknowledgement is never a second notification.
export async function claimReminder(context, id, atMs) {
  if (!/^[a-f0-9]{64}$/.test(id) || !Number.isSafeInteger(atMs)) throw new Error('Invalid reminder identity.');
  const key = reminderKey(context);
  return database.transaction('rw',kv,async () => {
    const value = reminderState(await kv.get(key));
    if (!value.inApp || !value.browser) return 'disabled';
    if (value.receipts.some(r => r.id === id)) return 'duplicate';
    if (value.receipts.length >= 2048) return 'receipt-capacity';
    value.receipts.push({id,state:'attempted',atMs});
    await kv.put({key,jsonText:JSON.stringify(value),updatedAtUtc:new Date().toISOString()});return 'claimed';
  });
}
export async function finishReminder(context, id, state) {
  if (!['shown','unknown'].includes(state)) throw new Error('Invalid reminder result.');
  const key = reminderKey(context);
  return database.transaction('rw',kv,async () => {
    const value = reminderState(await kv.get(key)), row = value.receipts.find(r => r.id === id);
    if (!row || row.state !== 'attempted') return;
    row.state = state;await kv.put({key,jsonText:JSON.stringify(value),updatedAtUtc:new Date().toISOString()});
  });
}
export async function clearReminderStates() { await kv.where('key').startsWith(reminderPrefix).delete(); }


function workspaceScope(scope) {
  if (typeof scope !== "string" || !/^[A-Za-z0-9_-]{1,128}$/.test(scope)) throw new Error("Invalid local account scope.");
}
function workspaceId(id) {
  if (typeof id !== "string" || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(id)) throw new Error("Invalid workspace identity.");
}
export async function getWorkspace(scope, id) {
  workspaceScope(scope); workspaceId(id);
  return database.transaction("r", workspaces, commands, events, async () => {
    const row = await workspaces.get([scope, id]);
    if (!row) return null;
    const commandRows = await commands.where("[scope+workspaceId]").equals([scope, id]).toArray();
    const eventRows = await events.where("[scope+workspaceId]").equals([scope, id]).toArray();
    commandRows.sort((a, b) => a.position - b.position); eventRows.sort((a, b) => a.position - b.position);
    return JSON.stringify({ document: JSON.parse(row.documentJson), storeVersion: row.storeVersion,
      authority: row.authorityJson ? JSON.parse(row.authorityJson) : null,
      commands: commandRows.map(value => JSON.parse(value.json)), events: eventRows.map(value => JSON.parse(value.json)) });
  });
}
function stableJson(value) {
  if (value === null || typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) return "[" + value.map(stableJson).join(",") + "]";
  return "{" + Object.keys(value).sort().map(key => JSON.stringify(key) + ":" + stableJson(value[key])).join(",") + "}";
}
function commandIdentityJson(value) {
  const normalized = structuredClone(value);
  if (normalized?.patch?.habiticaDetails === null) delete normalized.patch.habiticaDetails;
  return stableJson(normalized);
}
function taskIntentJson(value) {
  const normalized = structuredClone(value ?? null);
  if (normalized?.habiticaDetails === null) delete normalized.habiticaDetails;
  return stableJson(normalized);
}
export async function commitWorkspace(scope, expectedStoreVersion, json) {
  workspaceScope(scope);
  if (typeof json !== "string" || json.length > 16 * 1024 * 1024 || !Number.isSafeInteger(expectedStoreVersion) || expectedStoreVersion < 0) throw new Error("Invalid workspace commit.");
  const value = JSON.parse(json); const id = value?.document?.id; workspaceId(id);
  if (!Number.isSafeInteger(value.storeVersion) || value.storeVersion < 1 || value.storeVersion <= expectedStoreVersion
    || expectedStoreVersion > 0 && value.storeVersion !== expectedStoreVersion + 1 || !Array.isArray(value.commands) || !Array.isArray(value.events ?? [])) throw new Error("Invalid workspace revision.");
  const nextCommands = value.commands.map((entry, position) => {
    workspaceId(entry?.command?.id);
    return { scope, workspaceId: id, id: entry.command.id, position, json: JSON.stringify(entry) };
  });
  const nextEvents = (value.events ?? []).map((entry, position) => {
    workspaceId(entry?.id);
    return { scope, workspaceId: id, id: entry.id, position, json: JSON.stringify(entry) };
  });
  if (new Set(nextCommands.map(row => row.id)).size !== nextCommands.length || new Set(nextEvents.map(row => row.id)).size !== nextEvents.length) throw new Error("Duplicate journal identity.");
  return database.transaction("rw", workspaces, commands, events, async () => {
    const current = await workspaces.get([scope, id]);
    if ((current?.storeVersion ?? 0) !== expectedStoreVersion) return false;
    const oldCommands = await commands.where("[scope+workspaceId]").equals([scope, id]).toArray();
    const oldEvents = await events.where("[scope+workspaceId]").equals([scope, id]).toArray();
    const commandById = new Map(nextCommands.map(row => [row.id, row]));
    const eventById = new Map(nextEvents.map(row => [row.id, row]));
    for (const old of oldCommands) {
      const next = commandById.get(old.id);
      if (!next || next.position !== old.position) throw new Error("Command history cannot be removed or reordered.");
      const before = JSON.parse(old.json); const after = JSON.parse(next.json);
      if (before.payloadHash !== after.payloadHash || commandIdentityJson(before.command) !== commandIdentityJson(after.command) || taskIntentJson(before.taskSnapshot) !== taskIntentJson(after.taskSnapshot)) throw new Error("Accepted command identity is immutable.");
    }
    for (const old of oldEvents) {
      const next = eventById.get(old.id);
      if (!next || next.position !== old.position || stableJson(JSON.parse(next.json)) !== stableJson(JSON.parse(old.json))) throw new Error("Journal events are immutable.");
    }
    const oldCommandById = new Map(oldCommands.map(row => [row.id, row.json]));
    const oldEventIds = new Set(oldEvents.map(row => row.id));
    await workspaces.put({ scope, id, storeVersion: value.storeVersion, documentJson: JSON.stringify(value.document), authorityJson: JSON.stringify(value.authority ?? null), updatedAtUtc: new Date().toISOString() });
    await commands.bulkPut(nextCommands.filter(row => oldCommandById.get(row.id) !== row.json));
    await events.bulkPut(nextEvents.filter(row => !oldEventIds.has(row.id)));
    return true;
  });
}
export async function clearWorkspaces() {
  await database.transaction("rw", workspaces, commands, events, async () => {
    await workspaces.clear(); await commands.clear(); await events.clear();
  });
}

export async function listWorkspaces(scope) {
    if (!/^[A-Za-z0-9_-]{1,128}$/.test(scope)) throw new Error('invalid_scope');
    const rows = await database.table('workspaces').where('scope').equals(scope).toArray();
    return rows.map(row => row.id);
}

// Recovery records stay outside portable settings and workspace exports.
export async function getGearRecovery(scope) {
  workspaceScope(scope);
  return getJson("spell-gear-recovery:" + scope);
}
export async function commitGearRecovery(scope, expectedRevision, json) {
  workspaceScope(scope);
  if (!Number.isSafeInteger(expectedRevision) || expectedRevision < 0 || typeof json !== "string" || json.length > 16384)
    throw new Error("Invalid gear recovery commit.");
  const next = JSON.parse(json);
  validateGearRecovery(next);
  if (next.revision !== expectedRevision + 1) throw new Error("Invalid gear recovery revision.");
  const key = "spell-gear-recovery:" + scope;
  return database.transaction("rw", kv, async () => {
    const row = await kv.get(key);
    const current = row ? JSON.parse(row.jsonText) : null;
    if ((current?.revision ?? 0) !== expectedRevision) return false;
    if (current) {
      validateGearRecovery(current);
      const terminal = current.slots.every(slot => slot.status === 3 || slot.status === 4);
      if (next.operationId !== current.operationId) {
        if (!terminal || next.slots.some(slot => slot.status !== 0)) throw new Error("Unresolved gear recovery.");
      } else {
        if (next.slots.length !== current.slots.length) throw new Error("Recovery slots are immutable.");
        for (let i = 0; i < current.slots.length; i++) {
          const before = current.slots[i], after = next.slots[i];
          if (before.slot !== after.slot || before.originalKey !== after.originalKey || before.temporaryKey !== after.temporaryKey
              || (before.status === 3 || before.status === 4) && after.status !== before.status)
            throw new Error("Recovery identity is immutable.");
          // A dispatched toggle cannot return to a sendable state without a fresh observation.
          // Reconciliation terminates a slot or records an acknowledged temporary selection.
          const allowed = [[0,1,3,4], [1,2,3,4], [1,2,3,4], [3], [4]][before.status];
          if (!allowed.includes(after.status)) throw new Error("Invalid gear recovery transition.");
        }
      }
    } else if (next.slots.some(slot => slot.status !== 0)) throw new Error("Recovery must precede dispatch.");
    await kv.put({ key, jsonText: json, updatedAtUtc: new Date().toISOString() });
    return true;
  });
}
function validateGearRecovery(value) {
  workspaceId(value?.operationId);
  if (value.operationId === "00000000-0000-0000-0000-000000000000") throw new Error("Invalid gear recovery identity.");
  const names = new Set(["Head", "Head Accessory", "Eyewear", "Armor", "Body", "Weapon", "Shield", "Back"]);
  if (!Number.isSafeInteger(value.revision) || value.revision < 1 || !Array.isArray(value.slots)
      || value.slots.length < 1 || value.slots.length > 8 || new Set(value.slots.map(slot => slot?.slot)).size !== value.slots.length
      || value.slots.some(slot => !slot || !names.has(slot.slot) || typeof slot.originalKey !== "string" || typeof slot.temporaryKey !== "string"
          || !/^[A-Za-z0-9_]{0,128}$/.test(slot.originalKey) || !/^[A-Za-z0-9_]{0,128}$/.test(slot.temporaryKey)
          || slot.originalKey === slot.temporaryKey || !Number.isInteger(slot.status) || slot.status < 0 || slot.status > 4))
    throw new Error("Invalid gear recovery journal.");
}

const recoveryLeases = new Map();
export async function acquireGearRecovery(scope, lease) {
  workspaceScope(scope); workspaceId(lease);
  if (!globalThis.navigator?.locks) throw new Error("Exclusive recovery locks unavailable.");
  return new Promise((resolve, reject) => {
    navigator.locks.request("habitica-gear:" + scope, { ifAvailable: true }, async lock => {
      if (!lock) { resolve(false); return; }
      await new Promise(release => { recoveryLeases.set(lease, release); resolve(true); });
    }).catch(reject);
  });
}
export function releaseGearRecovery(lease) {
  const release = recoveryLeases.get(lease);
  if (release) { recoveryLeases.delete(lease); release(); }
}

function noteScope(scope) {
  if (typeof scope !== "string" || !/^[a-f0-9]{64}$/.test(scope)) throw new Error("Invalid notes account scope.");
}
export async function getNotes(scope) { noteScope(scope); return (await notes.get(scope))?.json ?? null; }
export async function commitNotes(scope, expectedRevision, json) {
  noteScope(scope);
  if (!Number.isSafeInteger(expectedRevision) || expectedRevision < 0 || typeof json !== "string" || new TextEncoder().encode(json).length > 16*1024*1024) throw new Error("Invalid notes commit.");
  const next=JSON.parse(json);
  if(next.schemaVersion!==1 || next.ownerScope!==scope || !Number.isSafeInteger(next.revision) || next.revision<=expectedRevision
    || expectedRevision>0&&next.revision!==expectedRevision+1 || !Array.isArray(next.receipts) || next.receipts.length!==next.revision || !Array.isArray(next.purged)) throw new Error("Invalid notes revision.");
  return database.transaction("rw",notes,async()=>{
    const row=await notes.get(scope);
    if((row?.revision??0)!==expectedRevision)return false;
    if(row){
      const previous=JSON.parse(row.json);
      if(previous.receipts.some((receipt,index)=>stableJson(receipt)!==stableJson(next.receipts[index]))
        ||previous.purged.some((item,index)=>stableJson(item)!==stableJson(next.purged[index])))throw new Error("Notes receipts and purged identities are immutable.");
    }
    await notes.put({scope,revision:next.revision,json});return true;
  });
}
export async function clearNotes() { await notes.clear(); }
