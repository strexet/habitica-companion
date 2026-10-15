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
      if (before.payloadHash !== after.payloadHash || stableJson(before.command) !== stableJson(after.command) || stableJson(before.taskSnapshot ?? null) !== stableJson(after.taskSnapshot ?? null)) throw new Error("Accepted command identity is immutable.");
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
