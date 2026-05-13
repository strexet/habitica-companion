const maxBodyBytes = 2 * 1024 * 1024;
const partyIdPattern = /^[A-Za-z0-9_-]{8,128}$/;
const eventRetentionDays = 120;

export async function onRequestGet(context) {
  const { env, params, request } = context;
  const db = resolveBinding(env);
  const partyId = normalizePartyId(params.partyId);
  if (!partyId) {
    return textResponse("Invalid party id.", 400);
  }

  const membership = await verifyMembership(request, env, partyId);
  if (membership) {
    return membership;
  }

  const state = await db
    .prepare("SELECT snapshot_json, updated_at_utc FROM party_state WHERE party_id = ?")
    .bind(partyId)
    .first();
  const eventsResult = await db
    .prepare(`
      SELECT member_id, display_name, last_cron_utc, member_habitica_day_key, observed_at_utc, confidence
      FROM party_cron_events
      WHERE party_id = ?
      ORDER BY last_cron_utc ASC, member_id ASC
    `)
    .bind(partyId)
    .all();
  const events = (eventsResult.results ?? []).map(eventEntry => ({
    partyId,
    memberId: eventEntry.member_id,
    displayName: eventEntry.display_name,
    lastCronUtc: eventEntry.last_cron_utc,
    memberHabiticaDayKey: eventEntry.member_habitica_day_key,
    observedAtUtc: eventEntry.observed_at_utc,
    confidence: Number(eventEntry.confidence ?? 0),
  }));

  if (!state && events.length === 0) {
    return textResponse("No shared party sync data exists for this party.", 404);
  }

  return jsonResponse({
    updatedAtUtc: state?.updated_at_utc ?? null,
    partySnapshotJson: state?.snapshot_json ?? null,
    cronHistoryJson: JSON.stringify({
      events,
    }),
  });
}

export async function onRequestPut(context) {
  const { env, params, request } = context;
  const db = resolveBinding(env);
  const partyId = normalizePartyId(params.partyId);
  if (!partyId) {
    return textResponse("Invalid party id.", 400);
  }

  const membership = await verifyMembership(request, env, partyId);
  if (membership) {
    return membership;
  }

  const contentLength = Number(request.headers.get("content-length") ?? "0");
  if (contentLength > maxBodyBytes) {
    return textResponse("Party sync payload is too large.", 413);
  }

  const payload = await request.json();
  if (!isValidPayload(payload)) {
    return textResponse("Invalid party sync payload.", 400);
  }

  const cronHistory = JSON.parse(payload.cronHistoryJson);
  const events = Array.isArray(cronHistory?.events) ? cronHistory.events : [];
  const nowIso = new Date().toISOString();
  const pruneBeforeIso = new Date(Date.now() - eventRetentionDays * 24 * 60 * 60 * 1000).toISOString();

  await db
    .prepare(`
      INSERT INTO party_state (party_id, snapshot_json, updated_at_utc)
      VALUES (?, ?, ?)
      ON CONFLICT(party_id) DO UPDATE SET
        snapshot_json = excluded.snapshot_json,
        updated_at_utc = excluded.updated_at_utc
    `)
    .bind(partyId, payload.partySnapshotJson, nowIso)
    .run();

  if (events.length > 0) {
    const statements = events
      .filter(isValidEvent)
      .map(eventEntry => db.prepare(`
        INSERT INTO party_cron_events (
          party_id,
          member_id,
          display_name,
          last_cron_utc,
          member_habitica_day_key,
          observed_at_utc,
          confidence
        ) VALUES (?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(party_id, member_id, last_cron_utc) DO UPDATE SET
          display_name = excluded.display_name,
          member_habitica_day_key = excluded.member_habitica_day_key,
          observed_at_utc = excluded.observed_at_utc,
          confidence = excluded.confidence
      `).bind(
        partyId,
        eventEntry.memberId,
        eventEntry.displayName,
        eventEntry.lastCronUtc,
        eventEntry.memberHabiticaDayKey ?? null,
        eventEntry.observedAtUtc,
        Number(eventEntry.confidence ?? 0),
      ));
    if (statements.length > 0) {
      await db.batch(statements);
    }
  }

  await db
    .prepare("DELETE FROM party_cron_events WHERE party_id = ? AND last_cron_utc < ?")
    .bind(partyId, pruneBeforeIso)
    .run();

  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    eventCount: events.length,
  });
}

function resolveBinding(env) {
  const db = env.HABITICA_PARTY_DB;
  if (!db) {
    throw new Error("HABITICA_PARTY_DB binding is not configured.");
  }

  return db;
}

function normalizePartyId(value) {
  const partyId = Array.isArray(value) ? value[0] : value;
  return partyIdPattern.test(partyId ?? "") ? partyId : null;
}

async function verifyMembership(request, env, expectedPartyId) {
  const userId = request.headers.get("x-api-user")?.trim();
  const apiToken = request.headers.get("x-api-key")?.trim();
  if (!userId || !apiToken) {
    return textResponse("Habitica credentials are required for shared party sync.", 401);
  }

  const response = await fetch("https://habitica.com/api/v3/groups/party", {
    headers: {
      "accept": "application/json",
      "x-api-user": userId,
      "x-api-key": apiToken,
      "x-client": env.HABITICA_X_CLIENT_HEADER || "habitica-tool-author-habitica-tool",
    },
  });
  const responseText = await response.text();

  if (!response.ok) {
    return textResponse(extractErrorMessage(responseText, "Party membership verification failed."), response.status);
  }

  const document = JSON.parse(responseText);
  const actualPartyId = document?.data?._id ?? null;
  if (!actualPartyId || actualPartyId !== expectedPartyId) {
    return textResponse("Current Habitica credentials are not a member of this party.", 403);
  }

  return null;
}

function isValidPayload(value) {
  return value
    && typeof value.partySnapshotJson === "string"
    && typeof value.cronHistoryJson === "string"
    && isJson(value.partySnapshotJson)
    && isJson(value.cronHistoryJson);
}

function isJson(value) {
  try {
    JSON.parse(value);
    return true;
  } catch {
    return false;
  }
}

function isValidEvent(eventEntry) {
  return eventEntry
    && typeof eventEntry.memberId === "string"
    && typeof eventEntry.displayName === "string"
    && typeof eventEntry.lastCronUtc === "string"
    && typeof eventEntry.observedAtUtc === "string";
}

function extractErrorMessage(responseText, fallback) {
  try {
    const parsed = JSON.parse(responseText);
    return parsed?.message || fallback;
  } catch {
    return responseText || fallback;
  }
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
