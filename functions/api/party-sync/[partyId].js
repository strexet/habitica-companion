const maxBodyBytes = 2 * 1024 * 1024;
const partyIdPattern = /^[A-Za-z0-9_-]{8,128}$/;
const userIdPattern = /^[A-Za-z0-9_-]{3,128}$/;
const eventRetentionDays = 120;
const defaultSettings = Object.freeze({
  officerCanManageQueue: true,
  officerCanModerateMembers: true,
  officerOnlyQueueEdits: false,
  memberAutoReconcileEnabled: true,
});

export async function onRequestGet(context) {
  const { env, params, request } = context;
  const db = resolveBinding(env);
  const partyId = normalizePartyId(params.partyId);
  if (!partyId) {
    return textResponse("Invalid party id.", 400);
  }

  const access = await resolvePartySyncAccess(request, env, db, partyId);
  if (access.response) {
    return access.response;
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
    management: await buildManagementState(db, env, partyId, access),
  });
}

export async function onRequestPut(context) {
  const { env, params, request } = context;
  const db = resolveBinding(env);
  const partyId = normalizePartyId(params.partyId);
  if (!partyId) {
    return textResponse("Invalid party id.", 400);
  }

  const access = await resolvePartySyncAccess(request, env, db, partyId);
  if (access.response) {
    return access.response;
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

  const access = await resolvePartySyncAccess(request, env, db, partyId);
  if (access.response) {
    return access.response;
  }

  const contentLength = Number(request.headers.get("content-length") ?? "0");
  if (contentLength > maxBodyBytes) {
    return textResponse("Party sync payload is too large.", 413);
  }

  const payload = await request.json();
  const nowIso = new Date().toISOString();
  switch (payload?.action) {
    case "publishQuestPool":
      return await publishQuestPool(db, env, partyId, access, payload, nowIso);
    case "addQueueItem":
      return await addQueueItem(db, env, partyId, access, payload, nowIso);
    case "toggleVote":
      return await toggleVote(db, env, partyId, access, payload, nowIso);
    case "removeQueueItem":
      return await removeQueueItem(db, env, partyId, access, payload, nowIso);
    case "markActive":
      return await updateQueueStatus(db, env, partyId, access, payload, "Active", nowIso);
    case "markCompleted":
      return await markCompleted(db, env, partyId, access, payload, nowIso);
    case "autoReconcileQuest":
      return await autoReconcileQuest(db, env, partyId, access, payload, nowIso);
    case "assignOfficer":
      return await assignOfficer(db, env, partyId, access, payload, nowIso);
    case "removeOfficer":
      return await removeOfficer(db, env, partyId, access, payload, nowIso);
    case "kickMember":
      return await kickMember(db, env, partyId, access, payload, nowIso);
    case "unkickMember":
      return await unkickMember(db, env, partyId, access, payload, nowIso);
    case "updateSettings":
      return await updateSettings(db, env, partyId, access, payload, nowIso);
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

async function publishQuestPool(db, env, partyId, access, payload, nowIso) {
  const entries = Array.isArray(payload.entries) ? payload.entries.filter(isValidPoolEntry) : [];
  await db
    .prepare("DELETE FROM party_quest_pool_entries WHERE party_id = ? AND owner_user_id = ?")
    .bind(partyId, access.userId)
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
      access.userId,
      entry.ownerDisplayName ?? access.displayName ?? access.userId,
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
    management: await buildManagementState(db, env, partyId, access),
  });
}

async function addQueueItem(db, env, partyId, access, payload, nowIso) {
  if (!access.canEditQueue) {
    return textResponse("Only party sync management can edit the quest queue right now.", 403);
  }

  if (!payload?.questKey || payload.ownerUserId !== access.userId) {
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
      access.userId,
      access.userId,
      payload.ownerDisplayName ?? access.displayName ?? access.userId,
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
    management: await buildManagementState(db, env, partyId, access),
  }, 201);
}

async function toggleVote(db, env, partyId, access, payload, nowIso) {
  if (!payload?.queueItemId) {
    return textResponse("Queue item id is required.", 400);
  }

  const existing = await db
    .prepare("SELECT queue_item_id FROM party_quest_votes WHERE party_id = ? AND queue_item_id = ? AND voter_user_id = ?")
    .bind(partyId, payload.queueItemId, access.userId)
    .first();
  if (existing) {
    await db
      .prepare("DELETE FROM party_quest_votes WHERE party_id = ? AND queue_item_id = ? AND voter_user_id = ?")
      .bind(partyId, payload.queueItemId, access.userId)
      .run();
  } else {
    await db
      .prepare(`
        INSERT INTO party_quest_votes (party_id, queue_item_id, voter_user_id, voter_display_name, vote_weight, created_at_utc, updated_at_utc)
        VALUES (?, ?, ?, ?, 1, ?, ?)
      `)
      .bind(partyId, payload.queueItemId, access.userId, payload.voterDisplayName ?? access.displayName ?? access.userId, nowIso, nowIso)
      .run();
  }

  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    questQueue: await readQuestQueue(db, partyId),
    questPool: await readQuestPool(db, partyId),
    recentlyCompleted: await readRecentlyCompleted(db, partyId),
    management: await buildManagementState(db, env, partyId, access),
  });
}

async function removeQueueItem(db, env, partyId, access, payload, nowIso) {
  const item = await db
    .prepare("SELECT owner_user_id, created_by_user_id, version FROM party_quest_queue WHERE party_id = ? AND queue_item_id = ?")
    .bind(partyId, payload?.queueItemId)
    .first();
  if (!item) {
    return textResponse("Queue item was not found.", 404);
  }

  const ownsQuest = (item.owner_user_id ?? item.created_by_user_id) === access.userId;
  if (!access.canEditQueue) {
    return textResponse("Only party sync management can edit the quest queue right now.", 403);
  }
  if (!ownsQuest && !access.canManageQueue) {
    return textResponse("Only the quest owner or party sync management can remove this queue item.", 403);
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
    management: await buildManagementState(db, env, partyId, access),
  });
}

async function updateQueueStatus(db, env, partyId, access, payload, status, nowIso) {
  const item = await db
    .prepare("SELECT owner_user_id, created_by_user_id, version FROM party_quest_queue WHERE party_id = ? AND queue_item_id = ?")
    .bind(partyId, payload?.queueItemId)
    .first();
  if (!item) {
    return textResponse("Queue item was not found.", 404);
  }

  const ownsQuest = (item.owner_user_id ?? item.created_by_user_id) === access.userId;
  if (!access.canEditQueue) {
    return textResponse("Only party sync management can edit the quest queue right now.", 403);
  }
  if (!ownsQuest && !access.canManageQueue) {
    return textResponse("Only the quest owner or party sync management can update this queue item.", 403);
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
    management: await buildManagementState(db, env, partyId, access),
  });
}

async function markCompleted(db, env, partyId, access, payload, nowIso) {
  const statusResponse = await updateQueueStatus(db, env, partyId, access, payload, "Completed", nowIso);
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
    management: await buildManagementState(db, env, partyId, access),
  });
}

async function autoReconcileQuest(db, env, partyId, access, payload, nowIso) {
  const transition = payload?.transition;
  const queueItemId = payload?.queueItemId;
  const questKey = payload?.questKey;
  if (!transition || !queueItemId || !questKey) {
    return textResponse("Missing transition, queueItemId, or questKey.", 400);
  }
  if (transition !== "activate" && transition !== "complete") {
    return textResponse("Transition must be 'activate' or 'complete'.", 400);
  }
  if (!access.settings.memberAutoReconcileEnabled && !access.canManageQueue) {
    return textResponse("Party sync management has disabled member queue reconciliation.", 403);
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
        management: await buildManagementState(db, env, partyId, access),
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
        management: await buildManagementState(db, env, partyId, access),
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
            access.userId,
            payload.completedByDisplayName ?? access.userId,
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
    management: await buildManagementState(db, env, partyId, access),
  });
}

async function assignOfficer(db, env, partyId, access, payload, nowIso) {
  if (!access.canManageOfficers) {
    return textResponse("Only the party owner or app admins can assign Officers.", 403);
  }

  const user = readTargetUser(payload);
  if (user.response) {
    return user.response;
  }
  if (access.leaderId && user.userId === access.leaderId) {
    return textResponse("The party owner does not need the Officer role.", 409);
  }

  await db
    .prepare(`
      UPDATE party_sync_roles
      SET revoked_by_user_id = ?, revoked_by_display_name = ?, revoked_at_utc = ?
      WHERE party_id = ? AND user_id = ? AND role = 'Officer' AND revoked_at_utc IS NULL
    `)
    .bind(access.userId, access.displayName, nowIso, partyId, user.userId)
    .run();
  await db
    .prepare(`
      INSERT INTO party_sync_roles (
        party_id, user_id, display_name, role,
        assigned_by_user_id, assigned_by_display_name, assigned_at_utc
      ) VALUES (?, ?, ?, 'Officer', ?, ?, ?)
    `)
    .bind(partyId, user.userId, user.displayName, access.userId, access.displayName, nowIso)
    .run();

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function removeOfficer(db, env, partyId, access, payload, nowIso) {
  if (!access.canManageOfficers) {
    return textResponse("Only the party owner or app admins can remove Officers.", 403);
  }

  const userId = payload?.userId?.trim();
  if (!userIdPattern.test(userId ?? "")) {
    return textResponse("Officer user id is required.", 400);
  }

  await db
    .prepare(`
      UPDATE party_sync_roles
      SET revoked_by_user_id = ?, revoked_by_display_name = ?, revoked_at_utc = ?
      WHERE party_id = ? AND user_id = ? AND role = 'Officer' AND revoked_at_utc IS NULL
    `)
    .bind(access.userId, access.displayName, nowIso, partyId, userId)
    .run();

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function kickMember(db, env, partyId, access, payload, nowIso) {
  if (!access.canModerateMembers) {
    return textResponse("Only party sync management can remove members from party sync.", 403);
  }

  const user = readTargetUser(payload);
  if (user.response) {
    return user.response;
  }
  if (user.userId === access.userId) {
    return textResponse("You cannot remove yourself from party sync.", 409);
  }
  if (access.leaderId && user.userId === access.leaderId) {
    return textResponse("Officers cannot remove the party owner from party sync.", 403);
  }
  if (await isAppAdmin(db, user.userId)) {
    return textResponse("App admins cannot be removed from party sync.", 403);
  }
  if (await hasActiveOfficerRole(db, partyId, user.userId) && !access.isOwner && !access.isAdmin) {
    return textResponse("Officers cannot remove other Officers from party sync.", 403);
  }

  await db
    .prepare(`
      UPDATE party_sync_kicks
      SET revoked_by_user_id = ?, revoked_by_display_name = ?, revoked_at_utc = ?
      WHERE party_id = ? AND user_id = ? AND revoked_at_utc IS NULL
    `)
    .bind(access.userId, access.displayName, nowIso, partyId, user.userId)
    .run();
  await db
    .prepare(`
      INSERT INTO party_sync_kicks (
        party_id, user_id, display_name,
        kicked_by_user_id, kicked_by_display_name, kicked_at_utc, reason
      ) VALUES (?, ?, ?, ?, ?, ?, ?)
    `)
    .bind(
      partyId,
      user.userId,
      user.displayName,
      access.userId,
      access.displayName,
      nowIso,
      typeof payload.reason === "string" && payload.reason.trim() ? payload.reason.trim().slice(0, 240) : null,
    )
    .run();

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function unkickMember(db, env, partyId, access, payload, nowIso) {
  if (!access.canModerateMembers) {
    return textResponse("Only party sync management can restore members to party sync.", 403);
  }

  const userId = payload?.userId?.trim();
  if (!userIdPattern.test(userId ?? "")) {
    return textResponse("Member user id is required.", 400);
  }

  await db
    .prepare(`
      UPDATE party_sync_kicks
      SET revoked_by_user_id = ?, revoked_by_display_name = ?, revoked_at_utc = ?
      WHERE party_id = ? AND user_id = ? AND revoked_at_utc IS NULL
    `)
    .bind(access.userId, access.displayName, nowIso, partyId, userId)
    .run();

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function updateSettings(db, env, partyId, access, payload, nowIso) {
  if (!access.canManageSettings) {
    return textResponse("Only the party owner or app admins can update party sync settings.", 403);
  }

  const settings = normalizeSettings(payload?.settings);
  await db
    .prepare(`
      INSERT INTO party_sync_settings (
        party_id,
        officer_can_manage_queue,
        officer_can_moderate_members,
        officer_only_queue_edits,
        member_auto_reconcile_enabled,
        updated_by_user_id,
        updated_by_display_name,
        updated_at_utc
      ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
      ON CONFLICT(party_id) DO UPDATE SET
        officer_can_manage_queue = excluded.officer_can_manage_queue,
        officer_can_moderate_members = excluded.officer_can_moderate_members,
        officer_only_queue_edits = excluded.officer_only_queue_edits,
        member_auto_reconcile_enabled = excluded.member_auto_reconcile_enabled,
        updated_by_user_id = excluded.updated_by_user_id,
        updated_by_display_name = excluded.updated_by_display_name,
        updated_at_utc = excluded.updated_at_utc
    `)
    .bind(
      partyId,
      settings.officerCanManageQueue ? 1 : 0,
      settings.officerCanModerateMembers ? 1 : 0,
      settings.officerOnlyQueueEdits ? 1 : 0,
      settings.memberAutoReconcileEnabled ? 1 : 0,
      access.userId,
      access.displayName,
      nowIso,
    )
    .run();

  access.settings = settings;
  access.canManageQueue = access.isOwner || access.isAdmin || (access.isOfficer && settings.officerCanManageQueue);
  access.canModerateMembers = access.isOwner || access.isAdmin || (access.isOfficer && settings.officerCanModerateMembers);
  access.canEditQueue = access.canManageQueue || !settings.officerOnlyQueueEdits;
  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function partyQuestStateResponse(db, env, partyId, access, nowIso) {
  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    questQueue: await readQuestQueue(db, partyId),
    questPool: await readQuestPool(db, partyId),
    recentlyCompleted: await readRecentlyCompleted(db, partyId),
    management: await buildManagementState(db, env, partyId, access),
  });
}

function readTargetUser(payload) {
  const userId = payload?.userId?.trim();
  const displayName = payload?.displayName?.trim();
  if (!userIdPattern.test(userId ?? "") || !displayName) {
    return { response: textResponse("Target user id and display name are required.", 400) };
  }

  return {
    userId,
    displayName: displayName.slice(0, 120),
  };
}

function normalizeSettings(value) {
  return {
    officerCanManageQueue: value?.officerCanManageQueue !== false,
    officerCanModerateMembers: value?.officerCanModerateMembers !== false,
    officerOnlyQueueEdits: value?.officerOnlyQueueEdits === true,
    memberAutoReconcileEnabled: value?.memberAutoReconcileEnabled !== false,
  };
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

async function resolvePartySyncAccess(request, env, db, expectedPartyId) {
  const proof = readAccessProof(request, expectedPartyId);
  if (proof.response) {
    return proof;
  }

  const settings = await readPartySyncSettings(db, expectedPartyId);
  const isAdmin = await isAppAdmin(db, proof.userId);
  const isOwner = !!proof.leaderId && proof.leaderId === proof.userId;
  const isOfficer = await hasActiveOfficerRole(db, expectedPartyId, proof.userId);
  const isKicked = await hasActiveKick(db, expectedPartyId, proof.userId);
  if (isKicked && !isOwner && !isAdmin) {
    return { response: textResponse("This user was removed from party sync by party management.", 403) };
  }

  const canManageSettings = isOwner || isAdmin;
  const canManageOfficers = isOwner || isAdmin;
  const canManageQueue = isOwner || isAdmin || (isOfficer && settings.officerCanManageQueue);
  const canModerateMembers = isOwner || isAdmin || (isOfficer && settings.officerCanModerateMembers);
  const canEditQueue = canManageQueue || !settings.officerOnlyQueueEdits;
  return {
    ...proof,
    isAdmin,
    isOwner,
    isOfficer,
    isKicked,
    settings,
    canManageSettings,
    canManageOfficers,
    canManageQueue,
    canModerateMembers,
    canEditQueue,
  };
}

function readAccessProof(request, expectedPartyId) {
  const proofVersion = request.headers.get("x-party-sync-proof-version")?.trim() || "local-claim-v1";
  if (proofVersion !== "local-claim-v1") {
    return { response: textResponse("Unsupported party-sync access proof.", 401) };
  }

  const partyId = request.headers.get("x-party-sync-party-id")?.trim();
  const userId = request.headers.get("x-party-sync-user-id")?.trim();
  const displayName = request.headers.get("x-party-sync-display-name")?.trim();
  const leaderId = request.headers.get("x-party-sync-leader-id")?.trim() || null;
  if (partyId !== expectedPartyId) {
    return { response: textResponse("Party-sync claim does not match the requested party.", 403) };
  }
  if (!userIdPattern.test(userId ?? "") || !displayName || displayName.length > 120) {
    return { response: textResponse("Invalid local party-sync claim.", 401) };
  }
  if (leaderId && !userIdPattern.test(leaderId)) {
    return { response: textResponse("Invalid local party-sync leader claim.", 401) };
  }

  return {
    proofVersion,
    partyId,
    userId,
    displayName,
    leaderId,
  };
}

async function readPartySyncSettings(db, partyId) {
  const row = await db
    .prepare(`
      SELECT officer_can_manage_queue, officer_can_moderate_members, officer_only_queue_edits, member_auto_reconcile_enabled
      FROM party_sync_settings
      WHERE party_id = ?
    `)
    .bind(partyId)
    .first();
  if (!row) {
    return { ...defaultSettings };
  }

  return {
    officerCanManageQueue: Number(row.officer_can_manage_queue ?? 1) === 1,
    officerCanModerateMembers: Number(row.officer_can_moderate_members ?? 1) === 1,
    officerOnlyQueueEdits: Number(row.officer_only_queue_edits ?? 0) === 1,
    memberAutoReconcileEnabled: Number(row.member_auto_reconcile_enabled ?? 1) === 1,
  };
}

async function hasActiveOfficerRole(db, partyId, userId) {
  const row = await db
    .prepare(`
      SELECT user_id
      FROM party_sync_roles
      WHERE party_id = ? AND user_id = ? AND role = 'Officer' AND revoked_at_utc IS NULL
      LIMIT 1
    `)
    .bind(partyId, userId)
    .first();
  return !!row;
}

async function hasActiveKick(db, partyId, userId) {
  const row = await db
    .prepare(`
      SELECT user_id
      FROM party_sync_kicks
      WHERE party_id = ? AND user_id = ? AND revoked_at_utc IS NULL
      LIMIT 1
    `)
    .bind(partyId, userId)
    .first();
  return !!row;
}

async function buildManagementState(db, env, partyId, access) {
  const settings = access?.settings ?? await readPartySyncSettings(db, partyId);
  const officers = await readActiveOfficers(db, partyId);
  const appAdmins = (await getAppAdminIds(db)).map(userId => ({
    userId,
    displayName: userId,
  }));
  const currentUserCanViewManagement = access.isOwner || access.isAdmin || access.isOfficer;
  const kicks = currentUserCanViewManagement ? await readActiveKicks(db, partyId) : [];

  return {
    ownerUserId: access.leaderId ?? null,
    ownerDisplayName: access.isOwner ? access.displayName : access.leaderId ?? null,
    appAdmins,
    officers,
    kicks,
    settings,
    currentUserIsOwner: !!access.isOwner,
    currentUserIsAdmin: !!access.isAdmin,
    currentUserIsOfficer: !!access.isOfficer,
    currentUserCanManageSettings: !!access.canManageSettings,
    currentUserCanManageOfficers: !!access.canManageOfficers,
    currentUserCanManageQueue: !!access.canManageQueue,
    currentUserCanModerateMembers: !!access.canModerateMembers,
    currentUserIsKicked: !!access.isKicked,
  };
}

async function readActiveOfficers(db, partyId) {
  const result = await db
    .prepare(`
      SELECT user_id, display_name, assigned_by_user_id, assigned_by_display_name, assigned_at_utc
      FROM party_sync_roles
      WHERE party_id = ? AND role = 'Officer' AND revoked_at_utc IS NULL
      ORDER BY display_name ASC, user_id ASC
    `)
    .bind(partyId)
    .all();
  return (result.results ?? []).map(row => ({
    userId: row.user_id,
    displayName: row.display_name ?? row.user_id,
    assignedByUserId: row.assigned_by_user_id,
    assignedByDisplayName: row.assigned_by_display_name,
    assignedAtUtc: row.assigned_at_utc,
  }));
}

async function readActiveKicks(db, partyId) {
  const result = await db
    .prepare(`
      SELECT user_id, display_name, kicked_by_user_id, kicked_by_display_name, kicked_at_utc, reason
      FROM party_sync_kicks
      WHERE party_id = ? AND revoked_at_utc IS NULL
      ORDER BY kicked_at_utc DESC, display_name ASC
    `)
    .bind(partyId)
    .all();
  return (result.results ?? []).map(row => ({
    userId: row.user_id,
    displayName: row.display_name ?? row.user_id,
    kickedByUserId: row.kicked_by_user_id,
    kickedByDisplayName: row.kicked_by_display_name,
    kickedAtUtc: row.kicked_at_utc,
    reason: row.reason,
  }));
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

async function isAppAdmin(db, userId) {
  if (!userId || !db) {
    return false;
  }
  const ids = await getAppAdminIds(db);
  const target = userId.toLowerCase();
  return ids.some(value => value.toLowerCase() === target);
}

async function getAppAdminIds(db) {
  if (!db) {
    return [];
  }
  const result = await db
    .prepare("SELECT user_id FROM app_admins WHERE revoked_at_utc IS NULL")
    .all();
  const rows = result?.results ?? [];
  return rows
    .map(row => (typeof row.user_id === "string" ? row.user_id.trim() : ""))
    .filter(Boolean);
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

export const partySyncAccessTestHooks = {
  normalizeSettings,
  readAccessProof,
};
