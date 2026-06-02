export async function uploadPartyData(claim, partySnapshotJson, cronHistoryJson) {
  validateClaim(claim);

  const response = await fetchWithInviteProofFallback(claim, {
    method: "PUT",
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

  const response = await fetchWithInviteProofFallback(claim);

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

export async function pinQuestQueueItem(claim, queueItemId, version, pinned) {
  return await postPartyAction(claim, {
    action: "pinQueueItem",
    queueItemId,
    version,
    pinned: pinned === true,
  });
}

export async function selectQuestQueueItem(claim, queueItemId, version) {
  return await postPartyAction(claim, {
    action: "selectQueueItem",
    queueItemId,
    version,
  });
}

export async function skipQuestQueueItem(claim, queueItemId, version) {
  return await postPartyAction(claim, {
    action: "skipQueueItem",
    queueItemId,
    version,
  });
}

export async function expireQuestQueueItem(claim, queueItemId, version) {
  return await postPartyAction(claim, {
    action: "expireQueueItem",
    queueItemId,
    version,
  });
}

export async function requeueQuestQueueItem(claim, queueItemId, version) {
  return await postPartyAction(claim, {
    action: "requeueQueueItem",
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

export async function removeRecentlyCompletedQuest(claim, questKey, completedAtUtc) {
  return await postPartyAction(claim, {
    action: "removeRecentlyCompletedQuest",
    questKey,
    completedAtUtc,
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

export async function listPartySyncInviteProofs(claim) {
  return await postPartyAction(claim, {
    action: "listInviteProofs",
  }, { forceLocalClaim: true });
}

export async function createPartySyncInviteProof(claim, label, expiresAtUtc) {
  const result = await postPartyAction(claim, {
    action: "createInviteProof",
    label,
    expiresAtUtc: expiresAtUtc ?? null,
  }, { forceLocalClaim: true });
  storeIssuedInviteProof(claim.partyId, result.issuedInviteProof);
  return result;
}

export async function revokePartySyncInviteProof(claim, proofId) {
  const result = await postPartyAction(claim, {
    action: "revokeInviteProof",
    proofId,
  }, { forceLocalClaim: true });
  clearStoredInviteProofIfMatching(claim.partyId, proofId);
  return result;
}

export async function rotatePartySyncInviteProof(claim, proofId) {
  const result = await postPartyAction(claim, {
    action: "rotateInviteProof",
    proofId,
  }, { forceLocalClaim: true });
  storeIssuedInviteProof(claim.partyId, result.issuedInviteProof);
  return result;
}

export async function removePartySyncInviteProof(claim, proofId) {
  const result = await postPartyAction(claim, {
    action: "removeInviteProof",
    proofId,
  }, { forceLocalClaim: true });
  clearStoredInviteProofIfMatching(claim.partyId, proofId);
  return result;
}

export async function setPartySyncInviteProofMode(claim, enabled) {
  const result = await postPartyAction(claim, {
    action: "setInviteProofMode",
    enabled: enabled === true,
  }, { forceLocalClaim: true });
  if (!enabled) {
    clearPartySyncInviteProof(claim.partyId);
  }
  return result;
}

export function activatePartySyncInviteProof(partyId, proofId, token, label) {
  validatePartyId(partyId);
  if (!proofId?.trim() || !token?.trim()) {
    throw new Error("Invite proof id and token are required.");
  }

  writeStoredInviteProof(partyId, {
    proofId: proofId.trim(),
    token: token.trim(),
    label: label?.trim() ?? "",
  });
}

export function clearPartySyncInviteProof(partyId) {
  validatePartyId(partyId);
  window.localStorage.removeItem(buildInviteProofStorageKey(partyId));
}

async function postPartyAction(claim, body, options = {}) {
  validateClaim(claim);

  const response = await fetchWithInviteProofFallback(claim, {
    method: "POST",
    body: JSON.stringify(body),
  }, options);

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

async function fetchWithInviteProofFallback(claim, init = {}, options = {}) {
  const usesJsonBody = typeof init.body === "string";
  let response = await fetch(buildPartySyncUrl(claim), {
    ...init,
    headers: usesJsonBody ? buildJsonHeaders(claim, options) : buildClaimHeaders(claim, options),
  });
  if (response.status !== 401 || options.forceLocalClaim || !readStoredInviteProof(claim.partyId)) {
    return response;
  }

  clearPartySyncInviteProof(claim.partyId);
  response = await fetch(buildPartySyncUrl(claim), {
    ...init,
    headers: usesJsonBody ? buildJsonHeaders(claim, { forceLocalClaim: true }) : buildClaimHeaders(claim, { forceLocalClaim: true }),
  });
  return response;
}

function buildJsonHeaders(claim, options = {}) {
  return {
    ...buildClaimHeaders(claim, options),
    "content-type": "application/json",
  };
}

function buildClaimHeaders(claim, options = {}) {
  const localClaimHeaders = {
    "accept": "application/json",
    "x-party-sync-proof-version": "local-claim-v1",
    "x-party-sync-party-id": claim.partyId.trim(),
    "x-party-sync-user-id": claim.userId.trim(),
    "x-party-sync-display-name": claim.displayName.trim(),
    "x-party-sync-leader-id": claim.leaderId?.trim() ?? "",
  };
  const inviteProof = options.forceLocalClaim ? null : readStoredInviteProof(claim.partyId);
  return inviteProof
    ? {
        ...localClaimHeaders,
        "x-party-sync-proof-version": tokenizedInviteProofVersion,
        "x-party-sync-proof-id": inviteProof.proofId,
        "x-party-sync-proof-token": inviteProof.token,
      }
    : localClaimHeaders;
}

function validateClaim(claim) {
  if (!claim || !claim.partyId?.trim() || !claim.userId?.trim() || !claim.displayName?.trim()) {
    throw new Error("A local party-sync claim is required for shared party sync.");
  }

  validatePartyId(claim.partyId);
}

function storeIssuedInviteProof(partyId, issuedInviteProof) {
  if (issuedInviteProof?.proofId && issuedInviteProof?.token) {
    writeStoredInviteProof(partyId, issuedInviteProof);
  }
}

function writeStoredInviteProof(partyId, inviteProof) {
  window.localStorage.setItem(buildInviteProofStorageKey(partyId), JSON.stringify({
    proofId: inviteProof.proofId,
    token: inviteProof.token,
    label: inviteProof.label ?? "",
  }));
}

function readStoredInviteProof(partyId) {
  try {
    const stored = window.localStorage.getItem(buildInviteProofStorageKey(partyId));
    if (!stored) {
      return null;
    }

    const inviteProof = JSON.parse(stored);
    return inviteProof?.proofId?.trim() && inviteProof?.token?.trim()
      ? inviteProof
      : null;
  } catch {
    return null;
  }
}

function clearStoredInviteProofIfMatching(partyId, proofId) {
  if (readStoredInviteProof(partyId)?.proofId === proofId) {
    clearPartySyncInviteProof(partyId);
  }
}

function buildInviteProofStorageKey(partyId) {
  return `${inviteProofStoragePrefix}${partyId.trim()}`;
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

export const partySyncBridgeTestHooks = {
  buildClaimHeaders,
  clearPartySyncInviteProof,
  readStoredInviteProof,
};
const tokenizedInviteProofVersion = "tokenized-invite-v1";
const inviteProofStoragePrefix = "habitica-tool:party-sync-invite-proof:";
