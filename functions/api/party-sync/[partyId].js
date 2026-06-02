const maxBodyBytes = 2 * 1024 * 1024;
const partyIdPattern = /^[A-Za-z0-9_-]{8,128}$/;
const userIdPattern = /^[A-Za-z0-9_-]{3,128}$/;
const proofIdPattern = /^[A-Za-z0-9_-]{8,128}$/;
const tokenizedInviteProofVersion = "tokenized-invite-v1";
const eventRetentionDays = 120;
const selectionExpirationHours = 72;
const staleQuestOwnerDays = 30;
const defaultSettings = Object.freeze({
  officerCanManageQueue: true,
  officerCanModerateMembers: true,
  officerOnlyQueueEdits: false,
  memberAutoReconcileEnabled: true,
  tokenizedInviteProofModeEnabled: false,
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
  await cleanupQueueState(db, partyId, new Date().toISOString());
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
    case "pinQueueItem":
      return await pinQueueItem(db, env, partyId, access, payload, nowIso);
    case "selectQueueItem":
      return await selectQueueItem(db, env, partyId, access, payload, nowIso);
    case "skipQueueItem":
      return await setQueueLifecycleStatus(db, env, partyId, access, payload, "Skipped", nowIso);
    case "expireQueueItem":
      return await setQueueLifecycleStatus(db, env, partyId, access, payload, "Expired", nowIso);
    case "requeueQueueItem":
      return await setQueueLifecycleStatus(db, env, partyId, access, payload, "Queued", nowIso);
    case "markActive":
      return await updateQueueStatus(db, env, partyId, access, payload, "Active", nowIso);
    case "inviteParty":
      return await updateQueueStatus(db, env, partyId, access, payload, "InviteSent", nowIso);
    case "markCompleted":
      return await markCompleted(db, env, partyId, access, payload, nowIso);
    case "removeRecentlyCompletedQuest":
      return await removeRecentlyCompletedQuest(db, env, partyId, access, payload, nowIso);
    case "autoReconcileQuest":
      return await autoReconcileQuest(db, env, partyId, access, payload, nowIso);
    case "recordDetectedCompletion":
      return await recordDetectedCompletion(db, env, partyId, access, payload, nowIso);
    case "assignOfficer":
      return await assignOfficer(db, env, partyId, access, payload, nowIso);
    case "assignPartyOwner":
      return await assignPartyOwner(db, env, partyId, access, payload, nowIso);
    case "removeOfficer":
      return await removeOfficer(db, env, partyId, access, payload, nowIso);
    case "kickMember":
      return await kickMember(db, env, partyId, access, payload, nowIso);
    case "unkickMember":
      return await unkickMember(db, env, partyId, access, payload, nowIso);
    case "updateSettings":
      return await updateSettings(db, env, partyId, access, payload, nowIso);
    case "listInviteProofs":
      return await listInviteProofs(db, env, partyId, access, nowIso);
    case "createInviteProof":
      return await createInviteProof(db, env, partyId, access, payload, nowIso);
    case "revokeInviteProof":
      return await revokeInviteProof(db, env, partyId, access, payload, nowIso);
    case "rotateInviteProof":
      return await rotateInviteProof(db, env, partyId, access, payload, nowIso);
    case "removeInviteProof":
      return await removeInviteProof(db, env, partyId, access, payload, nowIso);
    case "setInviteProofMode":
      return await setInviteProofMode(db, env, partyId, access, payload, nowIso);
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
        selected_expires_at_utc,
        sort_order,
        manual_pin_rank,
        owner_ready,
        version,
        reward_summary_json
      FROM party_quest_queue
      WHERE party_id = ? AND status NOT IN ('Removed', 'Completed')
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
    expiresAtUtc: row.selected_expires_at_utc,
    sortOrder: Number(row.sort_order ?? 0),
    manualPinRank: row.manual_pin_rank,
    ownerReady: Number(row.owner_ready ?? 0) === 1,
    version: Number(row.version ?? 1),
    votes: votesByQueueItem.get(row.queue_item_id) ?? [],
    rewardSummary: parseStringArray(row.reward_summary_json),
  }));
}

async function cleanupQueueState(db, partyId, nowIso) {
  const selectedExpiryIso = new Date(Date.parse(nowIso) - selectionExpirationHours * 60 * 60 * 1000).toISOString();
  await db
    .prepare(`
      UPDATE party_quest_queue
      SET status = 'Expired',
          expired_at_utc = ?,
          updated_at_utc = ?,
          version = COALESCE(version, 1) + 1
      WHERE party_id = ?
        AND status = 'Selected'
        AND (
          (selected_expires_at_utc IS NOT NULL AND selected_expires_at_utc <= ?)
          OR (selected_expires_at_utc IS NULL AND selected_at_utc IS NOT NULL AND selected_at_utc <= ?)
        )
    `)
    .bind(nowIso, nowIso, partyId, nowIso, selectedExpiryIso)
    .run();

  const staleOwnerIso = new Date(Date.parse(nowIso) - staleQuestOwnerDays * 24 * 60 * 60 * 1000).toISOString();
  await db
    .prepare(`
      UPDATE party_quest_queue
      SET status = 'Expired',
          expired_at_utc = ?,
          updated_at_utc = ?,
          version = COALESCE(version, 1) + 1
      WHERE party_id = ?
        AND status IN ('Queued', 'Skipped')
        AND NOT EXISTS (
          SELECT 1
          FROM party_quest_pool_entries pool
          WHERE pool.party_id = party_quest_queue.party_id
            AND pool.quest_key = party_quest_queue.quest_key
            AND pool.owner_user_id = party_quest_queue.owner_user_id
            AND pool.available_count > 0
            AND pool.last_seen_at_utc >= ?
        )
    `)
    .bind(nowIso, nowIso, partyId, staleOwnerIso)
    .run();
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
      SELECT party_id, quest_key, quest_name, completed_at_utc, started_at_utc, owner_user_id, owner_display_name, participants_count, reward_summary_json, source_queue_item_id, completed_by_user_id, completed_by_display_name, completion_source, detection_key
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
    detectionKey: row.detection_key,
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

  await cleanupQueueState(db, partyId, nowIso);
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
  await cleanupQueueState(db, partyId, nowIso);
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
  await cleanupQueueState(db, partyId, nowIso);
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
  await cleanupQueueState(db, partyId, nowIso);
  const item = await db
    .prepare("SELECT owner_user_id, created_by_user_id, status, version FROM party_quest_queue WHERE party_id = ? AND queue_item_id = ?")
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

async function pinQueueItem(db, env, partyId, access, payload, nowIso) {
  await cleanupQueueState(db, partyId, nowIso);
  if (!access.canManageQueue) {
    return textResponse("Only party sync management can pin queue items.", 403);
  }

  const item = await readMutableQueueItem(db, partyId, payload);
  if (item.response) {
    return item.response;
  }

  const pinned = payload.pinned === true;
  let pinRank = null;
  if (pinned) {
    const rankRow = await db
      .prepare("SELECT COALESCE(MIN(manual_pin_rank), 0) - 1 AS next_pin_rank FROM party_quest_queue WHERE party_id = ? AND manual_pin_rank IS NOT NULL")
      .bind(partyId)
      .first();
    pinRank = Number(rankRow?.next_pin_rank ?? -1);
  }

  await db
    .prepare(`
      UPDATE party_quest_queue
      SET manual_pin_rank = ?, updated_at_utc = ?, version = COALESCE(version, 1) + 1
      WHERE party_id = ? AND queue_item_id = ?
    `)
    .bind(pinRank, nowIso, partyId, payload.queueItemId)
    .run();

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function selectQueueItem(db, env, partyId, access, payload, nowIso) {
  await cleanupQueueState(db, partyId, nowIso);
  if (!access.canManageQueue) {
    return textResponse("Only party sync management can select queue items.", 403);
  }

  const item = await readMutableQueueItem(db, partyId, payload);
  if (item.response) {
    return item.response;
  }
  if (item.status !== "Queued" && item.status !== "Skipped" && item.status !== "Expired" && item.status !== "Selected") {
    return textResponse(`Cannot select quest in '${item.status}' state.`, 409);
  }

  const selected = await db
    .prepare("SELECT queue_item_id FROM party_quest_queue WHERE party_id = ? AND status = 'Selected' AND queue_item_id <> ? LIMIT 1")
    .bind(partyId, payload.queueItemId)
    .first();

  if (selected) {
    const rankRow = await db
      .prepare("SELECT COALESCE(MIN(manual_pin_rank), 0) - 1 AS next_pin_rank FROM party_quest_queue WHERE party_id = ? AND manual_pin_rank IS NOT NULL")
      .bind(partyId)
      .first();
    const returnPinRank = Number(rankRow?.next_pin_rank ?? -1);
    await db
      .prepare(`
        UPDATE party_quest_queue
        SET status = 'Queued',
            selected_at_utc = NULL,
            selected_expires_at_utc = NULL,
            manual_pin_rank = ?,
            updated_at_utc = ?,
            version = COALESCE(version, 1) + 1
        WHERE party_id = ? AND status = 'Selected' AND queue_item_id <> ?
      `)
      .bind(returnPinRank, nowIso, partyId, payload.queueItemId)
      .run();
  }

  const expiresAtIso = new Date(Date.parse(nowIso) + selectionExpirationHours * 60 * 60 * 1000).toISOString();
  await db
    .prepare(`
      UPDATE party_quest_queue
      SET status = 'Selected',
          selected_at_utc = COALESCE(selected_at_utc, ?),
          selected_expires_at_utc = ?,
          expired_at_utc = NULL,
          updated_at_utc = ?,
          version = COALESCE(version, 1) + 1
      WHERE party_id = ? AND queue_item_id = ?
    `)
    .bind(nowIso, expiresAtIso, nowIso, partyId, payload.queueItemId)
    .run();

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function setQueueLifecycleStatus(db, env, partyId, access, payload, status, nowIso) {
  await cleanupQueueState(db, partyId, nowIso);
  const item = await readMutableQueueItem(db, partyId, payload);
  if (item.response) {
    return item.response;
  }

  const ownsQuest = (item.owner_user_id ?? item.created_by_user_id) === access.userId;
  if (!access.canManageQueue && !ownsQuest) {
    return textResponse("Only the quest owner or party sync management can update this queue item.", 403);
  }

  if (status === "Skipped" && item.status !== "Selected") {
    return textResponse("Only selected quests can be skipped.", 409);
  }
  if (status === "Expired" && item.status === "Active") {
    return textResponse("Active quests cannot be expired manually.", 409);
  }
  if (status === "Queued" && item.status !== "Selected" && item.status !== "Skipped" && item.status !== "Expired") {
    return textResponse("Only next, skipped, or expired quests can return to queue.", 409);
  }

  const selectedAtSql = status === "Queued" ? "NULL" : "selected_at_utc";
  const expiresAtSql = status === "Queued" || status === "Skipped" || status === "Expired" ? "NULL" : "selected_expires_at_utc";
  const expiredAtSql = status === "Expired" ? "?" : "NULL";
  const pinRankSql = status === "Queued" ? "?" : "manual_pin_rank";
  let returnPinRank = null;
  if (status === "Queued") {
    const rankRow = await db
      .prepare("SELECT COALESCE(MIN(manual_pin_rank), 0) - 1 AS next_pin_rank FROM party_quest_queue WHERE party_id = ? AND manual_pin_rank IS NOT NULL")
      .bind(partyId)
      .first();
    returnPinRank = Number(rankRow?.next_pin_rank ?? -1);
  }
  const bindings = status === "Expired"
    ? [status, nowIso, nowIso, partyId, payload.queueItemId]
    : status === "Queued"
      ? [status, returnPinRank, nowIso, partyId, payload.queueItemId]
    : [status, nowIso, partyId, payload.queueItemId];

  await db
    .prepare(`
      UPDATE party_quest_queue
      SET status = ?,
          selected_at_utc = ${selectedAtSql},
          selected_expires_at_utc = ${expiresAtSql},
          expired_at_utc = ${expiredAtSql},
          manual_pin_rank = ${pinRankSql},
          updated_at_utc = ?,
          version = COALESCE(version, 1) + 1
      WHERE party_id = ? AND queue_item_id = ?
    `)
    .bind(...bindings)
    .run();

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function readMutableQueueItem(db, partyId, payload) {
  if (!payload?.queueItemId) {
    return { response: textResponse("Queue item id is required.", 400) };
  }

  const item = await db
    .prepare("SELECT queue_item_id, owner_user_id, created_by_user_id, status, version FROM party_quest_queue WHERE party_id = ? AND queue_item_id = ?")
    .bind(partyId, payload.queueItemId)
    .first();
  if (!item) {
    return { response: textResponse("Queue item was not found.", 404) };
  }

  if (payload.version && Number(payload.version) !== Number(item.version ?? 1)) {
    return { response: textResponse("Queue item changed before this request. Refresh and try again.", 409) };
  }

  return item;
}

async function updateQueueStatus(db, env, partyId, access, payload, status, nowIso) {
  await cleanupQueueState(db, partyId, nowIso);
  const item = await db
    .prepare("SELECT owner_user_id, created_by_user_id, status, version FROM party_quest_queue WHERE party_id = ? AND queue_item_id = ?")
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

  if (status === "InviteSent") {
    if (item.status !== "Selected") {
      return textResponse("Select the quest as Next Quest before inviting.", 409);
    }

    const selected = await db
      .prepare("SELECT queue_item_id, status FROM party_quest_queue WHERE party_id = ? AND status IN ('Selected', 'InviteSent') AND queue_item_id <> ? LIMIT 1")
      .bind(partyId, payload.queueItemId)
      .first();
    if (selected) {
      return selected.status === "InviteSent"
        ? textResponse("Another quest invitation is already pending in Habitica.", 409)
        : textResponse("Another quest is already selected. Select this quest before inviting.", 409);
    }

    const expiresAtIso = new Date(Date.parse(nowIso) + selectionExpirationHours * 60 * 60 * 1000).toISOString();
    await db
      .prepare(`
        UPDATE party_quest_queue
        SET status = ?,
            selected_at_utc = COALESCE(selected_at_utc, ?),
            selected_expires_at_utc = COALESCE(selected_expires_at_utc, ?),
            updated_at_utc = ?,
            version = COALESCE(version, 1) + 1
        WHERE party_id = ? AND queue_item_id = ?
      `)
      .bind(status, nowIso, expiresAtIso, nowIso, partyId, payload.queueItemId)
      .run();

    return await partyQuestStateResponse(db, env, partyId, access, nowIso);
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
        INSERT OR IGNORE INTO party_recently_completed_quests (
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

async function removeRecentlyCompletedQuest(db, env, partyId, access, payload, nowIso) {
  if (!access.isOwner && !access.isAdmin && !access.isOfficer) {
    return textResponse("Only party sync owner, app admins, or Officers can remove completed quest history.", 403);
  }
  if (!payload?.questKey || !payload.completedAtUtc) {
    return textResponse("Quest key and completed timestamp are required.", 400);
  }

  await db
    .prepare(`
      DELETE FROM party_recently_completed_quests
      WHERE party_id = ? AND quest_key = ? AND completed_at_utc = ?
    `)
    .bind(partyId, payload.questKey, payload.completedAtUtc)
    .run();

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
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
              completed_by_user_id, completed_by_display_name, completion_source, detection_key
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'auto', ?)
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
            normalizeDetectionKey(payload.detectionKey),
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

async function recordDetectedCompletion(db, env, partyId, access, payload, nowIso) {
  if (!access.settings.memberAutoReconcileEnabled && !access.canManageQueue) {
    return textResponse("Party sync management has disabled member queue reconciliation.", 403);
  }

  const questKey = typeof payload?.questKey === "string" ? payload.questKey.trim() : "";
  const detectionKey = normalizeDetectionKey(payload?.detectionKey);
  if (!questKey || !detectionKey) {
    return textResponse("Quest key and detection key are required.", 400);
  }

  await db
    .prepare(`
      INSERT OR IGNORE INTO party_recently_completed_quests (
        party_id,
        quest_key,
        quest_name,
        completed_at_utc,
        started_at_utc,
        owner_user_id,
        owner_display_name,
        participants_count,
        reward_summary_json,
        source_queue_item_id,
        completed_by_user_id,
        completed_by_display_name,
        completion_source,
        detection_key
      ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, NULL, ?, ?, 'auto', ?)
    `)
    .bind(
      partyId,
      questKey,
      typeof payload.questName === "string" && payload.questName.trim() ? payload.questName.trim() : questKey,
      typeof payload.completedAtUtc === "string" && payload.completedAtUtc ? payload.completedAtUtc : nowIso,
      typeof payload.startedAtUtc === "string" && payload.startedAtUtc ? payload.startedAtUtc : null,
      null,
      null,
      Number.isFinite(Number(payload.participantsCount)) ? Number(payload.participantsCount) : null,
      JSON.stringify(Array.isArray(payload.rewardSummary) ? payload.rewardSummary.filter(value => typeof value === "string") : []),
      access.userId,
      access.displayName ?? access.userId,
      detectionKey,
    )
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

async function assignOfficer(db, env, partyId, access, payload, nowIso) {
  if (!access.canManageOfficers) {
    return textResponse("Only the party owner or app admins can assign Officers.", 403);
  }

  const user = readTargetUser(payload);
  if (user.response) {
    return user.response;
  }
  const ownerUserId = access.activeOwner?.userId ?? access.leaderId;
  if (ownerUserId && user.userId === ownerUserId) {
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

async function assignPartyOwner(db, env, partyId, access, payload, nowIso) {
  if (!access.isAdmin) {
    return textResponse("Only app admins can assign the party owner role.", 403);
  }

  const user = readTargetUser(payload);
  if (user.response) {
    return user.response;
  }
  if (!await isCurrentPartyMember(db, partyId, user.userId)) {
    return textResponse("Party owner must be a current party member.", 409);
  }

  const ownerRole = await readOwnerRoleValue(db, partyId);
  await db
    .prepare(`
      UPDATE party_sync_roles
      SET revoked_by_user_id = ?, revoked_by_display_name = ?, revoked_at_utc = ?
      WHERE party_id = ? AND role = ? AND revoked_at_utc IS NULL
    `)
    .bind(access.userId, access.displayName, nowIso, partyId, ownerRole)
    .run();
  await db
    .prepare(`
      INSERT INTO party_sync_roles (
        party_id, user_id, display_name, role,
        assigned_by_user_id, assigned_by_display_name, assigned_at_utc
      ) VALUES (?, ?, ?, ?, ?, ?, ?)
    `)
    .bind(partyId, user.userId, user.displayName, ownerRole, access.userId, access.displayName, nowIso)
    .run();

  const isAssignedOwner = user.userId === access.userId;
  const updatedAccess = {
    ...access,
    activeOwner: {
      userId: user.userId,
      displayName: user.displayName,
    },
    isOwner: isAssignedOwner,
    canManageSettings: isAssignedOwner || access.isAdmin,
    canManageOfficers: isAssignedOwner || access.isAdmin,
    canManageProofs: isAssignedOwner || access.isAdmin,
    canManageQueue: isAssignedOwner || access.isAdmin || (access.isOfficer && access.settings.officerCanManageQueue),
    canModerateMembers: isAssignedOwner || access.isAdmin || (access.isOfficer && access.settings.officerCanModerateMembers),
    canEditQueue: isAssignedOwner || access.isAdmin || (access.isOfficer && access.settings.officerCanManageQueue) || !access.settings.officerOnlyQueueEdits,
  };

  return await partyQuestStateResponse(db, env, partyId, updatedAccess, nowIso);
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

  access.settings = {
    ...access.settings,
    ...settings,
  };
  access.canManageQueue = access.isOwner || access.isAdmin || (access.isOfficer && settings.officerCanManageQueue);
  access.canModerateMembers = access.isOwner || access.isAdmin || (access.isOfficer && settings.officerCanModerateMembers);
  access.canEditQueue = access.canManageQueue || !settings.officerOnlyQueueEdits;
  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function listInviteProofs(db, env, partyId, access, nowIso) {
  if (!access.canManageProofs) {
    return textResponse("Only the party owner or app admins can list invite proofs.", 403);
  }

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function createInviteProof(db, env, partyId, access, payload, nowIso) {
  if (!access.canManageProofs) {
    return textResponse("Only the party owner or app admins can issue invite proofs.", 403);
  }

  const label = normalizeInviteProofLabel(payload?.label);
  if (!label) {
    return textResponse("Invite proof label is required.", 400);
  }

  const expiresAtUtc = normalizeFutureTimestamp(payload?.expiresAtUtc, nowIso);
  if (expiresAtUtc.response) {
    return expiresAtUtc.response;
  }

  const issuedInviteProof = await insertInviteProof(
    db,
    partyId,
    label,
    expiresAtUtc.value,
    access,
    nowIso);
  return await partyQuestStateResponse(db, env, partyId, access, nowIso, {
    issuedInviteProof,
  });
}

async function revokeInviteProof(db, env, partyId, access, payload, nowIso) {
  if (!access.canManageProofs) {
    return textResponse("Only the party owner or app admins can revoke invite proofs.", 403);
  }

  const proofId = normalizeProofId(payload?.proofId);
  if (!proofId) {
    return textResponse("Invite proof id is required.", 400);
  }

  await db
    .prepare(`
      UPDATE party_sync_invite_proofs
      SET revoked_by_user_id = ?,
          revoked_by_display_name = ?,
          revoked_at_utc = ?
      WHERE party_id = ?
        AND proof_id = ?
        AND revoked_at_utc IS NULL
        AND removed_at_utc IS NULL
    `)
    .bind(access.userId, access.displayName, nowIso, partyId, proofId)
    .run();

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function rotateInviteProof(db, env, partyId, access, payload, nowIso) {
  if (!access.canManageProofs) {
    return textResponse("Only the party owner or app admins can rotate invite proofs.", 403);
  }

  const proofId = normalizeProofId(payload?.proofId);
  if (!proofId) {
    return textResponse("Invite proof id is required.", 400);
  }

  const existing = await db
    .prepare(`
      SELECT display_label, expires_at_utc
      FROM party_sync_invite_proofs
      WHERE party_id = ? AND proof_id = ? AND removed_at_utc IS NULL
      LIMIT 1
    `)
    .bind(partyId, proofId)
    .first();
  if (!existing) {
    return textResponse("Invite proof was not found.", 404);
  }

  const expiresAtUtc = normalizeFutureTimestamp(payload?.expiresAtUtc ?? existing.expires_at_utc, nowIso);
  if (expiresAtUtc.response) {
    return expiresAtUtc.response;
  }

  await db
    .prepare(`
      UPDATE party_sync_invite_proofs
      SET revoked_by_user_id = ?,
          revoked_by_display_name = ?,
          revoked_at_utc = ?
      WHERE party_id = ? AND proof_id = ? AND revoked_at_utc IS NULL
    `)
    .bind(access.userId, access.displayName, nowIso, partyId, proofId)
    .run();

  const issuedInviteProof = await insertInviteProof(
    db,
    partyId,
    normalizeInviteProofLabel(payload?.label) ?? existing.display_label,
    expiresAtUtc.value,
    access,
    nowIso);
  return await partyQuestStateResponse(db, env, partyId, access, nowIso, {
    issuedInviteProof,
  });
}

async function removeInviteProof(db, env, partyId, access, payload, nowIso) {
  if (!access.canManageProofs) {
    return textResponse("Only the party owner or app admins can remove invite proofs.", 403);
  }

  const proofId = normalizeProofId(payload?.proofId);
  if (!proofId) {
    return textResponse("Invite proof id is required.", 400);
  }

  await db
    .prepare(`
      UPDATE party_sync_invite_proofs
      SET revoked_by_user_id = COALESCE(revoked_by_user_id, ?),
          revoked_by_display_name = COALESCE(revoked_by_display_name, ?),
          revoked_at_utc = COALESCE(revoked_at_utc, ?),
          removed_by_user_id = ?,
          removed_by_display_name = ?,
          removed_at_utc = ?
      WHERE party_id = ? AND proof_id = ? AND removed_at_utc IS NULL
    `)
    .bind(access.userId, access.displayName, nowIso, access.userId, access.displayName, nowIso, partyId, proofId)
    .run();

  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function setInviteProofMode(db, env, partyId, access, payload, nowIso) {
  if (!access.canManageProofs) {
    return textResponse("Only the party owner or app admins can change invite proof mode.", 403);
  }

  const enabled = payload?.enabled === true;
  await db
    .prepare(`
      INSERT INTO party_sync_settings (
        party_id,
        officer_can_manage_queue,
        officer_can_moderate_members,
        officer_only_queue_edits,
        member_auto_reconcile_enabled,
        tokenized_invite_proof_mode_enabled,
        updated_by_user_id,
        updated_by_display_name,
        updated_at_utc
      ) VALUES (?, 1, 1, 0, 1, ?, ?, ?, ?)
      ON CONFLICT(party_id) DO UPDATE SET
        tokenized_invite_proof_mode_enabled = excluded.tokenized_invite_proof_mode_enabled,
        updated_by_user_id = excluded.updated_by_user_id,
        updated_by_display_name = excluded.updated_by_display_name,
        updated_at_utc = excluded.updated_at_utc
    `)
    .bind(partyId, enabled ? 1 : 0, access.userId, access.displayName, nowIso)
    .run();

  access.settings = {
    ...access.settings,
    tokenizedInviteProofModeEnabled: enabled,
  };
  return await partyQuestStateResponse(db, env, partyId, access, nowIso);
}

async function insertInviteProof(db, partyId, label, expiresAtUtc, access, nowIso) {
  const proofId = crypto.randomUUID();
  const token = `${crypto.randomUUID()}${crypto.randomUUID()}`.replaceAll("-", "");
  const tokenHash = await hashInviteProofToken(token);
  await db
    .prepare(`
      INSERT INTO party_sync_invite_proofs (
        party_id,
        proof_id,
        token_hash,
        display_label,
        issued_by_user_id,
        issued_by_display_name,
        issued_at_utc,
        expires_at_utc
      ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
    `)
    .bind(partyId, proofId, tokenHash, label, access.userId, access.displayName, nowIso, expiresAtUtc)
    .run();

  return {
    proofId,
    token,
    label,
    issuedAtUtc: nowIso,
    expiresAtUtc,
  };
}

async function partyQuestStateResponse(db, env, partyId, access, nowIso, additionalState = {}) {
  await cleanupQueueState(db, partyId, nowIso);
  return jsonResponse({
    ok: true,
    updatedAtUtc: nowIso,
    questQueue: await readQuestQueue(db, partyId),
    questPool: await readQuestPool(db, partyId),
    recentlyCompleted: await readRecentlyCompleted(db, partyId),
    management: await buildManagementState(db, env, partyId, access),
    ...additionalState,
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

function normalizeInviteProofLabel(value) {
  return typeof value === "string" && value.trim()
    ? value.trim().slice(0, 120)
    : null;
}

function normalizeProofId(value) {
  return typeof value === "string" && proofIdPattern.test(value.trim())
    ? value.trim()
    : null;
}

function normalizeFutureTimestamp(value, nowIso) {
  if (value === null || value === undefined || value === "") {
    return { value: null };
  }

  const timestamp = typeof value === "string" ? Date.parse(value) : NaN;
  if (!Number.isFinite(timestamp) || timestamp <= Date.parse(nowIso)) {
    return { response: textResponse("Invite proof expiry must be a future timestamp.", 400) };
  }

  return { value: new Date(timestamp).toISOString() };
}

async function hashInviteProofToken(token) {
  const bytes = new TextEncoder().encode(token);
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(digest), value => value.toString(16).padStart(2, "0")).join("");
}

function normalizeDetectionKey(value) {
  return typeof value === "string" && value.trim()
    ? value.trim().slice(0, 240)
    : null;
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
  const activeOwner = await readActivePartyOwner(db, expectedPartyId);
  const isOwner = activeOwner
    ? activeOwner.userId === proof.userId
    : !!proof.leaderId && proof.leaderId === proof.userId;
  const isOfficer = await hasActiveOfficerRole(db, expectedPartyId, proof.userId);
  const isKicked = await hasActiveKick(db, expectedPartyId, proof.userId);
  if (isKicked && !isOwner && !isAdmin) {
    return { response: textResponse("This user was removed from party sync by party management.", 403) };
  }

  const hasActiveInviteProof = await hasAnyActiveInviteProof(db, expectedPartyId);
  if (proof.proofVersion === tokenizedInviteProofVersion) {
    const validation = await validateTokenizedInviteProof(db, expectedPartyId, proof);
    if (validation.response) {
      return validation;
    }
  } else if (settings.tokenizedInviteProofModeEnabled && hasActiveInviteProof && !isOwner && !isAdmin) {
    return { response: textResponse("An active tokenized party-sync invite proof is required.", 401) };
  }

  const canManageSettings = isOwner || isAdmin;
  const canManageOfficers = isOwner || isAdmin;
  const canManageProofs = isOwner || isAdmin;
  const canManageQueue = isOwner || isAdmin || (isOfficer && settings.officerCanManageQueue);
  const canModerateMembers = isOwner || isAdmin || (isOfficer && settings.officerCanModerateMembers);
  const canEditQueue = canManageQueue || !settings.officerOnlyQueueEdits;
  return {
    ...proof,
    isAdmin,
    isOwner,
    isOfficer,
    isKicked,
    activeOwner,
    settings,
    canManageSettings,
    canManageOfficers,
    canManageProofs,
    canManageQueue,
    canModerateMembers,
    canEditQueue,
  };
}

function readAccessProof(request, expectedPartyId) {
  const proofVersion = request.headers.get("x-party-sync-proof-version")?.trim() || "local-claim-v1";
  if (proofVersion !== "local-claim-v1" && proofVersion !== tokenizedInviteProofVersion) {
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

  if (proofVersion === tokenizedInviteProofVersion) {
    const proofId = request.headers.get("x-party-sync-proof-id")?.trim();
    const token = request.headers.get("x-party-sync-proof-token")?.trim();
    if (!proofIdPattern.test(proofId ?? "") || !token || token.length < 32 || token.length > 512) {
      return { response: textResponse("Invalid tokenized party-sync invite proof.", 401) };
    }

    return {
      proofVersion,
      partyId,
      userId,
      displayName,
      leaderId,
      proofId,
      token,
    };
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
      SELECT officer_can_manage_queue, officer_can_moderate_members, officer_only_queue_edits, member_auto_reconcile_enabled, tokenized_invite_proof_mode_enabled
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
    tokenizedInviteProofModeEnabled: Number(row.tokenized_invite_proof_mode_enabled ?? 0) === 1,
  };
}

async function validateTokenizedInviteProof(db, partyId, proof) {
  const row = await db
    .prepare(`
      SELECT token_hash, expires_at_utc, revoked_at_utc, removed_at_utc
      FROM party_sync_invite_proofs
      WHERE party_id = ? AND proof_id = ?
      LIMIT 1
    `)
    .bind(partyId, proof.proofId)
    .first();
  if (!row) {
    return { response: textResponse("Tokenized party-sync invite proof was not found.", 401) };
  }
  if (row.removed_at_utc) {
    return { response: textResponse("Tokenized party-sync invite proof was removed.", 401) };
  }
  if (row.revoked_at_utc) {
    return { response: textResponse("Tokenized party-sync invite proof was revoked.", 401) };
  }
  if (row.expires_at_utc && Date.parse(row.expires_at_utc) <= Date.now()) {
    return { response: textResponse("Tokenized party-sync invite proof expired.", 401) };
  }
  if (await hashInviteProofToken(proof.token) !== row.token_hash) {
    return { response: textResponse("Tokenized party-sync invite proof is invalid.", 401) };
  }

  return { proofId: proof.proofId };
}

async function hasAnyActiveInviteProof(db, partyId) {
  const row = await db
    .prepare(`
      SELECT proof_id
      FROM party_sync_invite_proofs
      WHERE party_id = ?
        AND revoked_at_utc IS NULL
        AND removed_at_utc IS NULL
        AND (expires_at_utc IS NULL OR expires_at_utc > ?)
      LIMIT 1
    `)
    .bind(partyId, new Date().toISOString())
    .first();
  return !!row;
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

async function readOwnerRoleValue(db, partyId) {
  const row = await db
    .prepare(`
      SELECT role
      FROM party_sync_roles
      WHERE party_id = ?
        AND lower(role) IN ('owner', 'partyowner', 'party-owner', 'party owner')
      ORDER BY assigned_at_utc DESC
      LIMIT 1
    `)
    .bind(partyId)
    .first();
  return typeof row?.role === "string" && row.role.trim()
    ? row.role.trim()
    : "Owner";
}

async function readActivePartyOwner(db, partyId) {
  const ownerRole = await readOwnerRoleValue(db, partyId);
  const row = await db
    .prepare(`
      SELECT user_id, display_name, assigned_by_user_id, assigned_by_display_name, assigned_at_utc
      FROM party_sync_roles
      WHERE party_id = ? AND role = ? AND revoked_at_utc IS NULL
      ORDER BY assigned_at_utc DESC
      LIMIT 1
    `)
    .bind(partyId, ownerRole)
    .first();
  return row
    ? {
        userId: row.user_id,
        displayName: row.display_name ?? row.user_id,
        assignedByUserId: row.assigned_by_user_id,
        assignedByDisplayName: row.assigned_by_display_name,
        assignedAtUtc: row.assigned_at_utc,
      }
    : null;
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
  const activeOwner = access?.activeOwner ?? await readActivePartyOwner(db, partyId);
  const officers = await readActiveOfficers(db, partyId);
  const appAdmins = (await getAppAdminIds(db)).map(userId => ({
    userId,
    displayName: userId,
  }));
  const currentUserCanViewManagement = access.isOwner || access.isAdmin || access.isOfficer;
  const kicks = currentUserCanViewManagement ? await readActiveKicks(db, partyId) : [];
  const currentUserCanManageProofs = !!access.canManageProofs;
  const inviteProofs = currentUserCanManageProofs ? await readInviteProofs(db, partyId) : [];
  const hasActiveInviteProof = inviteProofs.some(proof => proof.status === "active")
    || await hasAnyActiveInviteProof(db, partyId);
  const inviteProofAccessStatus = settings.tokenizedInviteProofModeEnabled
    ? (access.proofVersion === tokenizedInviteProofVersion ? "active-proof" : "fallback-local-claim")
    : "disabled";

  return {
    ownerUserId: activeOwner?.userId ?? access.leaderId ?? null,
    ownerDisplayName: activeOwner?.displayName ?? (access.isOwner ? access.displayName : access.leaderId ?? null),
    appAdmins,
    officers,
    kicks,
    settings,
    currentUserIsOwner: !!access.isOwner,
    currentUserIsAdmin: !!access.isAdmin,
    currentUserIsOfficer: !!access.isOfficer,
    currentUserCanManageSettings: !!access.canManageSettings,
    currentUserCanManageOfficers: !!access.canManageOfficers,
    currentUserCanManageProofs,
    currentUserCanManageQueue: !!access.canManageQueue,
    currentUserCanModerateMembers: !!access.canModerateMembers,
    currentUserIsKicked: !!access.isKicked,
    inviteProofMode: {
      enabled: settings.tokenizedInviteProofModeEnabled,
      accessStatus: inviteProofAccessStatus,
      hasActiveProof: hasActiveInviteProof,
      activeProofId: access.proofVersion === tokenizedInviteProofVersion ? access.proofId : null,
      inviteProofs,
    },
  };
}

async function readInviteProofs(db, partyId) {
  const result = await db
    .prepare(`
      SELECT proof_id, display_label, issued_by_user_id, issued_by_display_name, issued_at_utc,
             expires_at_utc, revoked_at_utc, removed_at_utc
      FROM party_sync_invite_proofs
      WHERE party_id = ?
      ORDER BY issued_at_utc DESC, proof_id ASC
    `)
    .bind(partyId)
    .all();
  const now = Date.now();
  return (result.results ?? []).map(row => ({
    proofId: row.proof_id,
    label: row.display_label,
    issuedByUserId: row.issued_by_user_id,
    issuedByDisplayName: row.issued_by_display_name,
    issuedAtUtc: row.issued_at_utc,
    expiresAtUtc: row.expires_at_utc,
    revokedAtUtc: row.revoked_at_utc,
    removedAtUtc: row.removed_at_utc,
    status: row.removed_at_utc
      ? "removed"
      : row.revoked_at_utc
        ? "revoked"
        : row.expires_at_utc && Date.parse(row.expires_at_utc) <= now
          ? "expired"
          : "active",
  }));
}

async function isCurrentPartyMember(db, partyId, userId) {
  const row = await db
    .prepare("SELECT snapshot_json FROM party_state WHERE party_id = ?")
    .bind(partyId)
    .first();
  if (!row?.snapshot_json) {
    return false;
  }

  try {
    const snapshot = JSON.parse(row.snapshot_json);
    const members = Array.isArray(snapshot?.members) ? snapshot.members : [];
    return members.some(member => {
      const memberId = member?.memberId ?? member?.MemberId ?? member?.id ?? member?._id;
      return memberId === userId;
    });
  } catch {
    return false;
  }
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
  hashInviteProofToken,
  normalizeDetectionKey,
  normalizeSettings,
  readAccessProof,
  resolvePartySyncAccess,
  validateTokenizedInviteProof,
};
