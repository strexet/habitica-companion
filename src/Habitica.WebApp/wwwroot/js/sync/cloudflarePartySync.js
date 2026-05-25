export async function uploadPartyData(claim, partySnapshotJson, cronHistoryJson) {
  validateClaim(claim);

  const response = await fetch(buildPartySyncUrl(claim), {
    method: "PUT",
    headers: buildJsonHeaders(claim),
    body: JSON.stringify({
      partySnapshotJson,
      cronHistoryJson,
    }),
  });

  if (!response.ok) {
    throw new Error(await readError(response, "Party sync upload failed."));
  }
}

export async function downloadPartyData(claim) {
  validateClaim(claim);

  const response = await fetch(buildPartySyncUrl(claim), {
    headers: buildClaimHeaders(claim),
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(await readError(response, "Party sync download failed."));
  }

  return await readJsonResponse(
    response,
    "Party sync endpoint returned an invalid response.",
  );
}

export async function publishQuestPool(claim, entries) {
  return await postPartyAction(claim, {
    action: "publishQuestPool",
    entries: entries ?? [],
  });
}

export async function addQuestQueueItem(claim, entry) {
  return await postPartyAction(claim, {
    action: "addQueueItem",
    queueItemId: crypto.randomUUID(),
    questKey: entry.questKey,
    questName: entry.questName,
    ownerUserId: entry.ownerUserId,
    ownerDisplayName: entry.ownerDisplayName,
    rewardSummary: entry.rewardSummary ?? entry.rewards ?? [],
  });
}

export async function toggleQuestVote(claim, queueItemId, voterDisplayName) {
  return await postPartyAction(claim, {
    action: "toggleVote",
    queueItemId,
    voterDisplayName,
  });
}

export async function removeQuestQueueItem(claim, queueItemId, version) {
  return await postPartyAction(claim, {
    action: "removeQueueItem",
    queueItemId,
    version,
  });
}

export async function markQuestCompleted(claim, queueItemId, version, participantsCount) {
  return await postPartyAction(claim, {
    action: "markCompleted",
    queueItemId,
    version,
    participantsCount: participantsCount ?? null,
  });
}

export async function invitePartyToQuest(claim, queueItemId, version) {
  return await postPartyAction(claim, {
    action: "inviteParty",
    queueItemId,
    version,
  });
}

export async function reconcileQuestLifecycle(claim, queueItemId, questKey, transition, participantsCount, completedByDisplayName, detectionKey) {
  return await postPartyAction(claim, {
    action: "autoReconcileQuest",
    queueItemId,
    questKey,
    transition,
    participantsCount: participantsCount ?? null,
    completedByDisplayName: completedByDisplayName ?? null,
    detectionKey: detectionKey ?? null,
  });
}

export async function recordDetectedQuestCompletion(claim, completion) {
  return await postPartyAction(claim, {
    action: "recordDetectedCompletion",
    questKey: completion.questKey,
    questName: completion.questName,
    startedAtUtc: completion.startedAtUtc ?? null,
    participantsCount: completion.participantsCount ?? null,
    rewardSummary: completion.rewardSummary ?? [],
    detectionKey: completion.detectionKey,
    completedAtUtc: completion.completedAtUtc,
  });
}

export async function assignOfficer(claim, userId, displayName) {
  return await postPartyAction(claim, {
    action: "assignOfficer",
    userId,
    displayName,
  });
}

export async function assignPartyOwner(claim, userId, displayName) {
  return await postPartyAction(claim, {
    action: "assignPartyOwner",
    userId,
    displayName,
  });
}

export async function removeOfficer(claim, userId) {
  return await postPartyAction(claim, {
    action: "removeOfficer",
    userId,
  });
}

export async function kickMember(claim, userId, displayName, reason) {
  return await postPartyAction(claim, {
    action: "kickMember",
    userId,
    displayName,
    reason: reason ?? null,
  });
}

export async function unkickMember(claim, userId) {
  return await postPartyAction(claim, {
    action: "unkickMember",
    userId,
  });
}

export async function updatePartySyncSettings(claim, settings) {
  return await postPartyAction(claim, {
    action: "updateSettings",
    settings: settings ?? {},
  });
}

async function postPartyAction(claim, body) {
  validateClaim(claim);

  const response = await fetch(buildPartySyncUrl(claim), {
    method: "POST",
    headers: buildJsonHeaders(claim),
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    throw new Error(await readError(response, "Party quest action failed."));
  }

  return await readJsonResponse(
    response,
    "Party quest endpoint returned an invalid response.",
  );
}

function buildPartySyncUrl(claim) {
  return `/api/party-sync/${encodeURIComponent(claim.partyId.trim())}`;
}

function buildJsonHeaders(claim) {
  return {
    ...buildClaimHeaders(claim),
    "content-type": "application/json",
  };
}

function buildClaimHeaders(claim) {
  return {
    "accept": "application/json",
    "x-party-sync-proof-version": normalizeProofVersion(claim.proofVersion),
    "x-party-sync-party-id": claim.partyId.trim(),
    "x-party-sync-user-id": claim.userId.trim(),
    "x-party-sync-display-name": claim.displayName.trim(),
    "x-party-sync-leader-id": claim.leaderId?.trim() ?? "",
  };
}

function validateClaim(claim) {
  if (!claim || !claim.partyId?.trim() || !claim.userId?.trim() || !claim.displayName?.trim()) {
    throw new Error("A local party-sync claim is required for shared party sync.");
  }

  validatePartyId(claim.partyId);
}

function normalizeProofVersion(proofVersion) {
  return proofVersion?.trim() || "local-claim-v1";
}

function validatePartyId(partyId) {
  if (!partyId || !partyId.trim()) {
    throw new Error("Party sync requires an active Habitica party id.");
  }
}

async function readError(response, fallback) {
  const text = await response.text();
  if (looksLikeHtml(text)) {
    return `${fallback} ${buildHtmlEndpointMessage()}`;
  }

  return text ? `${fallback} ${text}` : fallback;
}

async function readJsonResponse(response, fallback) {
  const text = await response.text();
  if (!text) {
    throw new Error(fallback);
  }

  if (looksLikeHtml(text)) {
    throw new Error(buildHtmlEndpointMessage());
  }

  try {
    return JSON.parse(text);
  } catch {
    throw new Error(`${fallback} Response body was not valid JSON.`);
  }
}

function looksLikeHtml(text) {
  const normalized = text.trimStart();
  return normalized.startsWith("<!DOCTYPE")
    || normalized.startsWith("<html")
    || normalized.startsWith("<head")
    || normalized.startsWith("<body");
}

function buildHtmlEndpointMessage() {
  return window.location.hostname === "localhost"
    || window.location.hostname === "127.0.0.1"
    ? "Party sync endpoint is not available from the local app host. Use the deployed Cloudflare Pages site or run the app through Cloudflare Pages Functions locally."
    : "Party sync endpoint returned HTML instead of JSON. Check that the Cloudflare Pages Function is deployed and the route is not falling back to the app shell.";
}
