import Dexie from "../vendor/dexie.mjs";

const database = new Dexie("habitica-tool");

database.version(1).stores({
  kv: "&key, updatedAtUtc",
});

const kv = database.table("kv");

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
