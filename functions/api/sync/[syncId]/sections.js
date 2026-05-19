const syncIdPattern = /^[A-Za-z0-9_-]{32,128}$/;

export async function onRequestGet({ env, params }) {
  try {
    const binding = resolveBinding(env);
    const syncId = normalizeSyncId(params.syncId);
    if (!syncId) {
      return jsonResponse({ ok: false, error: "invalid_parameters" }, 400);
    }

    const prefix = `sync:${syncId}:section:`;
    const listed = await binding.list({ prefix });
    const sectionKeys = listed.keys.map((entry) => {
      const sectionKey = entry.name.slice(prefix.length);
      return { sectionKey };
    });

    return jsonResponse({
      ok: true,
      syncId,
      sections: sectionKeys,
    });
  } catch (error) {
    console.error("cloud-sync list sections failed", error);
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

function jsonResponse(value, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
    },
  });
}
