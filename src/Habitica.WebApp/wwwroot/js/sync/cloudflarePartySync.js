export async function uploadPartyData(userId, apiToken, partyId, partySnapshotJson, cronHistoryJson) {
  validateCredentials(userId, apiToken);
  validatePartyId(partyId);

  const response = await fetch(`/api/party-sync/${encodeURIComponent(partyId)}`, {
    method: "PUT",
    headers: {
      "accept": "application/json",
      "content-type": "application/json",
      "x-api-user": userId.trim(),
      "x-api-key": apiToken.trim(),
    },
    body: JSON.stringify({
      partySnapshotJson,
      cronHistoryJson,
    }),
  });

  if (!response.ok) {
    throw new Error(await readError(response, "Party sync upload failed."));
  }
}

export async function downloadPartyData(userId, apiToken, partyId) {
  validateCredentials(userId, apiToken);
  validatePartyId(partyId);

  const response = await fetch(`/api/party-sync/${encodeURIComponent(partyId)}`, {
    headers: {
      "accept": "application/json",
      "x-api-user": userId.trim(),
      "x-api-key": apiToken.trim(),
    },
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

export async function publishQuestPool(userId, apiToken, partyId, entries) {
  return await postPartyAction(userId, apiToken, partyId, {
    action: "publishQuestPool",
    entries: entries ?? [],
  });
}

export async function addQuestQueueItem(userId, apiToken, partyId, entry) {
  return await postPartyAction(userId, apiToken, partyId, {
    action: "addQueueItem",
    queueItemId: crypto.randomUUID(),
    questKey: entry.questKey,
    questName: entry.questName,
    ownerUserId: entry.ownerUserId,
    ownerDisplayName: entry.ownerDisplayName,
    rewardSummary: entry.rewardSummary ?? entry.rewards ?? [],
  });
}

export async function toggleQuestVote(userId, apiToken, partyId, queueItemId, voterDisplayName) {
  return await postPartyAction(userId, apiToken, partyId, {
    action: "toggleVote",
    queueItemId,
    voterDisplayName,
  });
}

export async function removeQuestQueueItem(userId, apiToken, partyId, queueItemId, version) {
  return await postPartyAction(userId, apiToken, partyId, {
    action: "removeQueueItem",
    queueItemId,
    version,
  });
}

async function postPartyAction(userId, apiToken, partyId, body) {
  validateCredentials(userId, apiToken);
  validatePartyId(partyId);

  const response = await fetch(`/api/party-sync/${encodeURIComponent(partyId)}`, {
    method: "POST",
    headers: {
      "accept": "application/json",
      "content-type": "application/json",
      "x-api-user": userId.trim(),
      "x-api-key": apiToken.trim(),
    },
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

function validateCredentials(userId, apiToken) {
  if (!userId || !apiToken) {
    throw new Error("Habitica credentials are required for party sync.");
  }
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
