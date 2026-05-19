const maxBodyBytes = 2 * 1024 * 1024;
const syncIdPattern = /^[A-Za-z0-9_-]{32,128}$/;
const sectionKeyPattern = /^[a-z][a-z0-9-]{1,63}$/;

export async function onRequestGet({ env, params }) {
  try {
    const binding = resolveBinding(env);
    const syncId = normalizeSyncId(params.syncId);
    const sectionKey = normalizeSectionKey(params.sectionKey);
    if (!syncId || !sectionKey) {
      return jsonResponse({ ok: false, error: "invalid_parameters" }, 400);
    }

    const stored = await binding.get(storageKey(syncId, sectionKey), { type: "json" });
    if (!stored) {
      return jsonResponse({ ok: false, error: "not_found", sectionKey }, 404);
    }

    return jsonResponse(stored);
  } catch (error) {
    console.error("cloud-sync section get failed", error);
    return jsonResponse({
      ok: false,
      error: "cloud_sync_worker_exception",
      message: error?.message ?? "Unknown cloud sync error."
    }, 500);
  }
}

export async function onRequestPut({ request, env, params }) {
  try {
    const binding = resolveBinding(env);
    const syncId = normalizeSyncId(params.syncId);
    const sectionKey = normalizeSectionKey(params.sectionKey);
    if (!syncId || !sectionKey) {
      return jsonResponse({ ok: false, error: "invalid_parameters" }, 400);
    }

    const contentLength = Number(request.headers.get("content-length") ?? "0");
    if (contentLength > maxBodyBytes) {
      return jsonResponse({
        ok: false,
        error: "payload_too_large",
        sectionKey,
        payloadBytes: contentLength,
        maxPayloadBytes: maxBodyBytes
      }, 413);
    }

    const encryptedPayload = await request.json();
    if (!isValidEncryptedPayload(encryptedPayload)) {
      return jsonResponse({ ok: false, error: "invalid_payload", sectionKey }, 400);
    }

    const stored = {
      schemaVersion: 2,
      sectionKey,
      updatedAtUtc: new Date().toISOString(),
      encryptedPayload,
    };

    await binding.put(storageKey(syncId, sectionKey), JSON.stringify(stored));
    return jsonResponse({
      ok: true,
      sectionKey,
      updatedAtUtc: stored.updatedAtUtc,
    });
  } catch (error) {
    console.error("cloud-sync section put failed", error);
    return jsonResponse({
      ok: false,
      error: "cloud_sync_worker_exception",
      message: error?.message ?? "Unknown cloud sync error."
    }, 500);
  }
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

function normalizeSectionKey(value) {
  const sectionKey = Array.isArray(value) ? value[0] : value;
  return sectionKeyPattern.test(sectionKey ?? "") ? sectionKey : null;
}

function storageKey(syncId, sectionKey) {
  return `sync:${syncId}:section:${sectionKey}`;
}

function isValidEncryptedPayload(value) {
  return value
    && (value.schemaVersion === 1 || value.schemaVersion === 2)
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
