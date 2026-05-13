const maxBodyBytes = 2 * 1024 * 1024;
const syncIdPattern = /^[A-Za-z0-9_-]{32,128}$/;

export async function onRequestGet({ env, params }) {
  const binding = resolveBinding(env);
  const syncId = normalizeSyncId(params.syncId);
  if (!syncId) {
    return textResponse("Invalid sync id.", 400);
  }

  const stored = await binding.get(storageKey(syncId), { type: "json" });
  if (!stored) {
    return textResponse("No cloud sync data exists for this identity.", 404);
  }

  return jsonResponse(stored);
}

export async function onRequestPut({ request, env, params }) {
  const binding = resolveBinding(env);
  const syncId = normalizeSyncId(params.syncId);
  if (!syncId) {
    return textResponse("Invalid sync id.", 400);
  }

  const contentLength = Number(request.headers.get("content-length") ?? "0");
  if (contentLength > maxBodyBytes) {
    return textResponse("Cloud sync payload is too large.", 413);
  }

  const encryptedPayload = await request.json();
  if (!isValidEncryptedPayload(encryptedPayload)) {
    return textResponse("Invalid encrypted sync payload.", 400);
  }

  const stored = {
    schemaVersion: 1,
    updatedAtUtc: new Date().toISOString(),
    encryptedPayload,
  };

  await binding.put(storageKey(syncId), JSON.stringify(stored));
  return jsonResponse({
    ok: true,
    updatedAtUtc: stored.updatedAtUtc,
  });
}

function resolveBinding(env) {
  const binding = env.HABITICA_SYNC_KV;
  if (!binding) {
    throw new Error("HABITICA_SYNC_KV binding is not configured.");
  }

  return binding;
}

function normalizeSyncId(value) {
  const syncId = Array.isArray(value) ? value[0] : value;
  return syncIdPattern.test(syncId ?? "") ? syncId : null;
}

function storageKey(syncId) {
  return `sync:${syncId}`;
}

function isValidEncryptedPayload(value) {
  return value
    && value.schemaVersion === 1
    && value.crypto?.algorithm === "AES-GCM"
    && value.crypto?.kdf === "PBKDF2-SHA-256"
    && typeof value.iv === "string"
    && typeof value.ciphertext === "string";
}

function jsonResponse(value, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
    },
  });
}

function textResponse(message, status) {
  return new Response(message, {
    status,
    headers: {
      "content-type": "text/plain; charset=utf-8",
      "cache-control": "no-store",
    },
  });
}

