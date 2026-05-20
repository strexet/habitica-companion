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
  if (membership.response) {
    return membership.response;
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
  const questQueue = await readQuestQueue(db, partyId);
  const questPool = await readQuestPool(db, partyId);
  const recentlyCompleted = await readRecentlyCompleted(db, partyId);

  if (!state && events.length === 0 && questQueue.length === 0 && questPool.length === 0 && recentlyCompleted.length === 0) {
    return textResponse("No shared party sync data exists for this party.", 404);
  }

  return jsonResponse({
    updatedAtUtc: state?.updated_at_utc ?? null,
    partySnapshotJson: state?.snapshot_json ?? null,
    cronHistoryJson: JSON.stringify({
      events,
    }),
    questQueue,
    questPool,
    recentlyCompleted,
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
  if (membership.response) {
    return membership.response;
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

export async function onRequestPost(context) {
  const { env, params, request } = context;
  const db = resolveBinding(env);
  const partyId = normalizePartyId(params.partyId);
  if (!partyId) {
    return textResponse("Invalid party id.", 400);
  }

  const membership = await verifyMembership(request, env, partyId);
  if (membership.response) {
    return membership.response;
  }

  const contentLength = Number(request.headers.get("content-length") ?? "0");
  if (contentLength > maxBodyBytes) {
    return textResponse("Party sync payload is too large.", 413);
  }

  const payload = await request.json();
  const nowIso = new Date().toISOString();
  switch (payload?.action) {
    case "publishQuestPool":
      return await publishQuestPool(db, partyId, membership, payload, nowIso);
    case "addQueueItem":
      return await addQueueItem(db, partyId, membership, payload, nowIso);
    case "toggleVote":
      return await toggleVote(db, partyId, membership, payload, nowIso);
    case "removeQueueItem":
      return await removeQueueItem(db, env, partyId, membership, payload, nowIso);
    case "markActive":
      return await updateQueueStatus(db, env, partyId, membership, payload, "Active", nowIso);
    case "markCompleted":
      return await markCompleted(db, env, partyId, membership, payload, nowIso);
    case "autoReconcileQuest":
      return await autoReconcileQuest(db, partyId, membership, payload, nowIso);
    default:
      return textResponse("Unsupported party quest action.", 400);
  }
}

function resolveBinding(env) {
  const db = env.HABITICA_PARTY_DB;
  if (!db) {
    throw new Error("HABITICA_PARTY_DB binding is not configured.");
  }

  return db;
}

async function readQuestQueue(db, partyId) {
  const queueResult = await db
    .prepare(`
      SELECT
        queue_item_id,
        party_id,
        quest_key,
        COALESCE(quest_name, quest_key) AS quest_name,
        COALESCE(owner_user_id, created_by_user_id) AS owner_user_id,
        COALESCE(owner_display_name, created_by_user_id) AS owner_display_name,
        status,
        created_at_utc,
        COALESCE(updated_at_utc, created_at_utc) AS updated_at_utc,
        selected_at_utc,
        started_at_utc,
        completed_at_utc,
        sort_order,
        manual_pin_rank,
        owner_ready,
        version,
        reward_summary_json
      FROM party_quest_queue
      WHERE party_id = ? AND status NOT IN ('Removed', 'Completed', 'Expired')
      ORDER BY COALESCE(manual_pin_rank, 999999) ASC, sort_order ASC, created_at_utc ASC
    `)
    .bind(partyId)
    .all();
  const voteResult = await db
    .prepare(`
      SELECT queue_item_id, voter_user_id, voter_display_name, vote_weight, created_at_utc, updated_at_utc
      FROM party_quest_votes
      WHERE party_id = ?
      ORDER BY created_at_utc ASC, voter_user_id ASC
    `)
    .bind(partyId)
    .all();
  const votesByQueueItem = new Map();
  for (const vote of voteResult.results ?? []) {
    const votes = votesByQueueItem.get(vote.queue_item_id) ?? [];
    votes.push({
      voterUserId: vote.voter_user_id,
      voterDisplayName: vote.voter_display_name ?? vote.voter_user_id,
      voteWeight: Number(vote.vote_weight ?? 1),
      createdAtUtc: vote.created_at_utc,
      updatedAtUtc: vote.updated_at_utc,
    });
    votesByQueueItem.set(vote.queue_item_id, votes);
  }

  return (queueResult.results ?? []).map(row => ({
    queueItemId: row.queue_item_id,
    partyId: row.party_id,
    questKey: row.quest_key,
    questName: row.quest_name,
    ownerUserId: row.owner_user_id,
    ownerDisplayName: row.owner_display_name,
    status: row.status ?? "Queued",
    createdAtUtc: row.created_at_utc,
    updatedAtUtc: row.updated_at_utc,
    selectedAtUtc: row.selected_at_utc,
    startedAtUtc: row.started_at_utc,
    completedAtUtc: row.completed_at_utc,
    sortOrder: Number(row.sort_order ?? 0),
    manualPinRank: row.manual_pin_rank,
    ownerReady: Number(row.owner_ready ?? 0) === 1,
    version: Number(row.version ?? 1),
    votes: votesByQueueItem.get(row.queue_item_id) ?? [],
    rewardSummary: parseStringArray(row.reward_summary_json),
  }));
}

async function readQuestPool(db, partyId) {
  const result = await db
    .prepare(`
      SELECT party_id, quest_key, quest_name, owner_user_id, owner_display_name, quest_type, reward_summary_json, available_count, last_seen_at_utc
      FROM party_quest_pool_entries
      WHERE party_id = ? AND available_count > 0
      ORDER BY quest_name ASC, owner_display_name ASC
    `)
    .bind(partyId)
    .all();
  return (result.results ?? []).map(row => ({
    partyId: row.party_id,
    questKey: row.quest_key,
    questName: row.quest_name ?? row.quest_key,
    ownerUserId: row.owner_user_id,
    ownerDisplayName: row.owner_display_name ?? row.owner_user_id,
    questType: row.quest_type ?? "Unknown",
    rewardSummary: parseStringArray(row.reward_summary_json),
    availableCount: Number(row.available_count ?? 1),
    lastSeenAtUtc: row.last_seen_at_utc,
  }));
}

async function readRecentlyCompleted(db, partyId) {
  const result = await db
    .prepare(`
      SELECT party_id, quest_key, quest_name, completed_at_utc, started_at_utc, owner_user_id, owner_display_name, participants_count, reward_summary_json, source_queue_item_id, completed_by_user_id, completed_by_display_name, completion_source
      FROM party_recently_completed_quests
      WHERE party_id = ?
      ORDER BY completed_at_utc DESC
      LIMIT 50
    `)
    .bind(partyId)
    .all();
  return (result.results ?? []).map(row => ({
    partyId: row.party_id,
    questKey: row.quest_key,
    questName: row.quest_name ?? row.quest_key,
    completedAtUtc: row.completed_at_utc,
    startedAtUtc: row.started_at_utc,
    ownerUserId: row.owner_user_id,
    ownerDisplayName: row.owner_display_name,
    participantsCount: row.participants_count,
    rewardSummary: parseStringArray(row.reward_summary_json),
    sourceQueueItemId: row.source_queue_item_id,
    completedByUserId: row.completed_by_user_id,
    completedByDisplayName: row.completed_by_display_name,
    completionSource: row.completion_source ?? "manual",
  }));
}

async function publishQuestPool(db, partyId, membership, payload, nowIso) {
  const entries = Array.isArray(payload.entries) ? payload.entries.filter(isValidPoolEntry) : [];
  await db
    .prepare("DELETE FROM party_quest_pool_entries WHERE party_id = ? AND owner_user_id = ?")
    .bind(partyId, membership.userId)
    .run();

  if (entries.length > 0) {
    await db.batch(entries.map(entry => db.prepare(`
      INSERT INTO party_quest_pool_entries (
        party_id,
        quest_key,
        quest_name,
        owner_user_id,
        owner_display_name,
        quest_type,
        reward_summary_json,
        available_count,
        last_seen_at_utc
      ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
      ON CONFLICT(party_id, quest_key, owner_user_id) DO UPDATE SET
        quest_name = excluded.quest_name,
        owner_display_name = excluded.owner_display_name,
        quest_type = excluded.quest_type,
        reward_summary_json = excluded.reward_summary_json,
        available_count = excluded.available_count,
        last_seen_at_utc = excluded.last_seen_at_utc
    `).bind(
      partyId,
      entry.questKey,
      entry.questName ?? entry.questKey,
      membership.userId,
      entry.ownerDisplayName ?? membership.displayName ?? membership.userId,
      entry.questType ?? "Unknown",
      JSON.stringify(entry.rewardSummary ?? []),
      Math.max(1, Number(entry.availableCount ?? 1)),
      nowIso,
    )));
  }

  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    questPool: await readQuestPool(db, partyId),
    questQueue: await readQuestQueue(db, partyId),
    recentlyCompleted: await readRecentlyCompleted(db, partyId),
  });
}

async function addQueueItem(db, partyId, membership, payload, nowIso) {
  if (!payload?.questKey || payload.ownerUserId !== membership.userId) {
    return textResponse("Only a quest owner can add their quest to the queue.", 403);
  }

  const queueItemId = typeof payload.queueItemId === "string" && payload.queueItemId
    ? payload.queueItemId
    : crypto.randomUUID();
  const sortOrderRow = await db
    .prepare("SELECT COALESCE(MAX(sort_order), 0) + 1 AS next_sort_order FROM party_quest_queue WHERE party_id = ?")
    .bind(partyId)
    .first();

  await db
    .prepare(`
      INSERT INTO party_quest_queue (
        party_id,
        queue_item_id,
        quest_key,
        quest_name,
        created_by_user_id,
        owner_user_id,
        owner_display_name,
        status,
        created_at_utc,
        updated_at_utc,
        sort_order,
        owner_ready,
        version,
        reward_summary_json
      ) VALUES (?, ?, ?, ?, ?, ?, ?, 'Queued', ?, ?, ?, 0, 1, ?)
    `)
    .bind(
      partyId,
      queueItemId,
      payload.questKey,
      payload.questName ?? payload.questKey,
      membership.userId,
      membership.userId,
      payload.ownerDisplayName ?? membership.displayName ?? membership.userId,
      nowIso,
      nowIso,
      Number(sortOrderRow?.next_sort_order ?? 1),
      JSON.stringify(payload.rewardSummary ?? []),
    )
    .run();

  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    questQueue: await readQuestQueue(db, partyId),
    questPool: await readQuestPool(db, partyId),
    recentlyCompleted: await readRecentlyCompleted(db, partyId),
  }, 201);
}

async function toggleVote(db, partyId, membership, payload, nowIso) {
  if (!payload?.queueItemId) {
    return textResponse("Queue item id is required.", 400);
  }

  const existing = await db
    .prepare("SELECT queue_item_id FROM party_quest_votes WHERE party_id = ? AND queue_item_id = ? AND voter_user_id = ?")
    .bind(partyId, payload.queueItemId, membership.userId)
    .first();
  if (existing) {
    await db
      .prepare("DELETE FROM party_quest_votes WHERE party_id = ? AND queue_item_id = ? AND voter_user_id = ?")
      .bind(partyId, payload.queueItemId, membership.userId)
      .run();
  } else {
    await db
      .prepare(`
        INSERT INTO party_quest_votes (party_id, queue_item_id, voter_user_id, voter_display_name, vote_weight, created_at_utc, updated_at_utc)
        VALUES (?, ?, ?, ?, 1, ?, ?)
      `)
      .bind(partyId, payload.queueItemId, membership.userId, payload.voterDisplayName ?? membership.displayName ?? membership.userId, nowIso, nowIso)
      .run();
  }

  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    questQueue: await readQuestQueue(db, partyId),
    questPool: await readQuestPool(db, partyId),
    recentlyCompleted: await readRecentlyCompleted(db, partyId),
  });
}

async function removeQueueItem(db, env, partyId, membership, payload, nowIso) {
  const item = await db
    .prepare("SELECT owner_user_id, created_by_user_id, version FROM party_quest_queue WHERE party_id = ? AND queue_item_id = ?")
    .bind(partyId, payload?.queueItemId)
    .first();
  if (!item) {
    return textResponse("Queue item was not found.", 404);
  }

  const isOwner = (item.owner_user_id ?? item.created_by_user_id) === membership.userId;
  const isLeader = membership.leaderId === membership.userId;
  const isAdmin = isPartySyncAdmin(env, membership.userId);
  if (!isOwner && !isLeader && !isAdmin) {
    return textResponse("Only the quest owner or party leader can remove this queue item.", 403);
  }

  if (payload.version && Number(payload.version) !== Number(item.version ?? 1)) {
    return textResponse("Queue item changed before this request. Refresh and try again.", 409);
  }

  await db
    .prepare(`
      UPDATE party_quest_queue
      SET status = 'Removed', updated_at_utc = ?, version = COALESCE(version, 1) + 1
      WHERE party_id = ? AND queue_item_id = ?
    `)
    .bind(nowIso, partyId, payload.queueItemId)
    .run();

  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    questQueue: await readQuestQueue(db, partyId),
    questPool: await readQuestPool(db, partyId),
    recentlyCompleted: await readRecentlyCompleted(db, partyId),
  });
}

async function updateQueueStatus(db, env, partyId, membership, payload, status, nowIso) {
  const item = await db
    .prepare("SELECT owner_user_id, created_by_user_id, version FROM party_quest_queue WHERE party_id = ? AND queue_item_id = ?")
    .bind(partyId, payload?.queueItemId)
    .first();
  if (!item) {
    return textResponse("Queue item was not found.", 404);
  }

  const isOwner = (item.owner_user_id ?? item.created_by_user_id) === membership.userId;
  const isLeader = membership.leaderId === membership.userId;
  const isAdmin = isPartySyncAdmin(env, membership.userId);
  if (!isOwner && !isLeader && !isAdmin) {
    return textResponse("Only the quest owner or party leader can update this queue item.", 403);
  }

  if (payload.version && Number(payload.version) !== Number(item.version ?? 1)) {
    return textResponse("Queue item changed before this request. Refresh and try again.", 409);
  }

  const timestampColumn = status === "Active" ? "started_at_utc" : "updated_at_utc";
  await db
    .prepare(`
      UPDATE party_quest_queue
      SET status = ?, ${timestampColumn} = ?, updated_at_utc = ?, version = COALESCE(version, 1) + 1
      WHERE party_id = ? AND queue_item_id = ?
    `)
    .bind(status, nowIso, nowIso, partyId, payload.queueItemId)
    .run();

  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    questQueue: await readQuestQueue(db, partyId),
    questPool: await readQuestPool(db, partyId),
    recentlyCompleted: await readRecentlyCompleted(db, partyId),
  });
}

async function markCompleted(db, env, partyId, membership, payload, nowIso) {
  const statusResponse = await updateQueueStatus(db, env, partyId, membership, payload, "Completed", nowIso);
  if (!statusResponse.ok) {
    return statusResponse;
  }

  const item = await db
    .prepare(`
      SELECT queue_item_id, quest_key, quest_name, owner_user_id, owner_display_name, started_at_utc, reward_summary_json
      FROM party_quest_queue
      WHERE party_id = ? AND queue_item_id = ?
    `)
    .bind(partyId, payload.queueItemId)
    .first();
  if (item) {
    await db
      .prepare(`
        INSERT INTO party_recently_completed_quests (
          party_id,
          quest_key,
          quest_name,
          completed_at_utc,
          started_at_utc,
          owner_user_id,
          owner_display_name,
          participants_count,
          reward_summary_json,
          source_queue_item_id
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
      `)
      .bind(
        partyId,
        item.quest_key,
        item.quest_name ?? item.quest_key,
        nowIso,
        item.started_at_utc,
        item.owner_user_id,
        item.owner_display_name,
        payload.participantsCount ?? null,
        item.reward_summary_json,
        item.queue_item_id,
      )
      .run();
  }

  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    questQueue: await readQuestQueue(db, partyId),
    questPool: await readQuestPool(db, partyId),
    recentlyCompleted: await readRecentlyCompleted(db, partyId),
  });
}

async function autoReconcileQuest(db, partyId, membership, payload, nowIso) {
  const transition = payload?.transition;
  const queueItemId = payload?.queueItemId;
  const questKey = payload?.questKey;
  if (!transition || !queueItemId || !questKey) {
    return textResponse("Missing transition, queueItemId, or questKey.", 400);
  }
  if (transition !== "activate" && transition !== "complete") {
    return textResponse("Transition must be 'activate' or 'complete'.", 400);
  }

  const item = await db
    .prepare("SELECT quest_key, status, version, started_at_utc FROM party_quest_queue WHERE party_id = ? AND queue_item_id = ?")
    .bind(partyId, queueItemId)
    .first();
  if (!item) {
    return textResponse("Queue item was not found.", 404);
  }
  if (item.quest_key !== questKey) {
    return textResponse("Quest key does not match queue item.", 409);
  }

  if (transition === "activate") {
    if (item.status === "Active") {
      return jsonResponse({
        ok: true,
        alreadyInState: true,
        updatedAtUtc: nowIso,
        questQueue: await readQuestQueue(db, partyId),
        questPool: await readQuestPool(db, partyId),
        recentlyCompleted: await readRecentlyCompleted(db, partyId),
      });
    }
    if (item.status !== "Queued" && item.status !== "Selected" && item.status !== "InviteSent") {
      return textResponse(`Cannot activate quest in '${item.status}' state.`, 409);
    }
    await db
      .prepare(`
        UPDATE party_quest_queue
        SET status = 'Active', started_at_utc = ?, updated_at_utc = ?, version = COALESCE(version, 1) + 1
        WHERE party_id = ? AND queue_item_id = ?
      `)
      .bind(nowIso, nowIso, partyId, queueItemId)
      .run();
  }

  if (transition === "complete") {
    if (item.status === "Completed") {
      return jsonResponse({
        ok: true,
        alreadyInState: true,
        updatedAtUtc: nowIso,
        questQueue: await readQuestQueue(db, partyId),
        questPool: await readQuestPool(db, partyId),
        recentlyCompleted: await readRecentlyCompleted(db, partyId),
      });
    }
    if (item.status !== "Active") {
      return textResponse(`Cannot complete quest in '${item.status}' state.`, 409);
    }
    await db
      .prepare(`
        UPDATE party_quest_queue
        SET status = 'Completed', completed_at_utc = ?, updated_at_utc = ?, version = COALESCE(version, 1) + 1
        WHERE party_id = ? AND queue_item_id = ?
      `)
      .bind(nowIso, nowIso, partyId, queueItemId)
      .run();

    const completedItem = await db
      .prepare("SELECT quest_key, quest_name, owner_user_id, owner_display_name, started_at_utc, reward_summary_json FROM party_quest_queue WHERE party_id = ? AND queue_item_id = ?")
      .bind(partyId, queueItemId)
      .first();
    if (completedItem) {
      try {
        await db
          .prepare(`
            INSERT INTO party_recently_completed_quests (
              party_id, quest_key, quest_name, completed_at_utc, started_at_utc,
              owner_user_id, owner_display_name, participants_count,
              reward_summary_json, source_queue_item_id,
              completed_by_user_id, completed_by_display_name, completion_source
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'auto')
          `)
          .bind(
            partyId,
            completedItem.quest_key,
            completedItem.quest_name ?? completedItem.quest_key,
            nowIso,
            completedItem.started_at_utc ?? item.started_at_utc,
            completedItem.owner_user_id,
            completedItem.owner_display_name,
            payload.participantsCount ?? null,
            completedItem.reward_summary_json,
            queueItemId,
            membership.userId,
            payload.completedByDisplayName ?? membership.userId,
          )
          .run();
      } catch (insertError) {
        // unique constraint on source_queue_item_id — another user already recorded completion
      }
    }
  }

  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    questQueue: await readQuestQueue(db, partyId),
    questPool: await readQuestPool(db, partyId),
    recentlyCompleted: await readRecentlyCompleted(db, partyId),
  });
}

function isValidPoolEntry(entry) {
  return entry
    && typeof entry.questKey === "string"
    && entry.questKey.length > 0
    && Number(entry.availableCount ?? 1) > 0;
}

function parseStringArray(jsonText) {
  if (!jsonText) {
    return [];
  }

  try {
    const parsed = JSON.parse(jsonText);
    return Array.isArray(parsed)
      ? parsed.filter(value => typeof value === "string")
      : [];
  } catch {
    return [];
  }
}

function normalizePartyId(value) {
  const partyId = Array.isArray(value) ? value[0] : value;
  return partyIdPattern.test(partyId ?? "") ? partyId : null;
}

async function verifyMembership(request, env, expectedPartyId) {
  const userId = request.headers.get("x-api-user")?.trim();
  const apiToken = request.headers.get("x-api-key")?.trim();
  if (!userId || !apiToken) {
    return { response: textResponse("Habitica credentials are required for shared party sync.", 401) };
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
    return { response: textResponse(extractErrorMessage(responseText, "Party membership verification failed."), response.status) };
  }

  const document = JSON.parse(responseText);
  const actualPartyId = document?.data?._id ?? null;
  if (!actualPartyId || actualPartyId !== expectedPartyId) {
    return { response: textResponse("Current Habitica credentials are not a member of this party.", 403) };
  }

  return {
    userId,
    displayName: document?.data?.members?.[userId]?.profile?.name ?? null,
    leaderId: document?.data?.leader?._id ?? document?.data?.leader ?? null,
  };
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

function isPartySyncAdmin(env, userId) {
  if (!userId) {
    return false;
  }

  const configured = env.HABITICA_PARTY_ADMIN_USER_IDS || env.PARTY_SYNC_ADMIN_USER_IDS || "";
  return configured
    .split(",")
    .map(value => value.trim())
    .filter(Boolean)
    .some(value => value.toLowerCase() === userId.toLowerCase());
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
