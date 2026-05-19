const textEncoder = new TextEncoder();
const textDecoder = new TextDecoder();
const namespace = "habitica-tool-cloud-sync-v1";
const iterations = 100000;

export async function uploadData(userId, apiToken, plainTextJson) {
  const identity = await deriveIdentity(userId, apiToken);
  const encryptedPayload = await encryptText(identity.key, plainTextJson);
  const response = await fetch(`/api/sync/${encodeURIComponent(identity.syncId)}`, {
    method: "PUT",
    headers: {
      "content-type": "application/json",
    },
    body: JSON.stringify(encryptedPayload),
  });

  if (!response.ok) {
    throw new Error(await readError(response, "Cloud sync upload failed."));
  }
}

export async function downloadData(userId, apiToken) {
  const identity = await deriveIdentity(userId, apiToken);
  const response = await fetch(`/api/sync/${encodeURIComponent(identity.syncId)}`, {
    headers: {
      "accept": "application/json",
    },
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(await readError(response, "Cloud sync download failed."));
  }

  const snapshot = await readJsonResponse(
    response,
    "Cloud sync endpoint returned an invalid response.",
  );
  const plainTextJson = await decryptText(identity.key, snapshot.encryptedPayload);
  return {
    plainTextJson,
    updatedAtUtc: snapshot.updatedAtUtc ?? null,
  };
}

async function deriveIdentity(userId, apiToken) {
  if (!userId || !apiToken) {
    throw new Error("Habitica credentials are required for encrypted cloud sync.");
  }

  const normalizedUserId = userId.trim();
  const keyMaterial = await crypto.subtle.importKey(
    "raw",
    textEncoder.encode(apiToken),
    "PBKDF2",
    false,
    ["deriveKey"],
  );
  const salt = textEncoder.encode(`${namespace}:key:${normalizedUserId}`);
  const key = await crypto.subtle.deriveKey(
    {
      name: "PBKDF2",
      salt,
      iterations,
      hash: "SHA-256",
    },
    keyMaterial,
    {
      name: "AES-GCM",
      length: 256,
    },
    false,
    ["encrypt", "decrypt"],
  );
  const syncIdBytes = await crypto.subtle.digest(
    "SHA-256",
    textEncoder.encode(`${namespace}:id:${normalizedUserId}:${apiToken}`),
  );

  return {
    key,
    syncId: toBase64Url(new Uint8Array(syncIdBytes)),
  };
}

async function encryptText(key, plainText) {
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const ciphertext = await crypto.subtle.encrypt(
    {
      name: "AES-GCM",
      iv,
    },
    key,
    textEncoder.encode(plainText),
  );

  return {
    schemaVersion: 1,
    crypto: {
      algorithm: "AES-GCM",
      kdf: "PBKDF2-SHA-256",
      iterations,
    },
    iv: toBase64Url(iv),
    ciphertext: toBase64Url(new Uint8Array(ciphertext)),
  };
}

async function decryptText(key, encryptedPayload) {
  if (!encryptedPayload || encryptedPayload.schemaVersion !== 1) {
    throw new Error("Cloud sync payload has an unsupported encrypted schema.");
  }

  const plainBytes = await crypto.subtle.decrypt(
    {
      name: "AES-GCM",
      iv: fromBase64Url(encryptedPayload.iv),
    },
    key,
    fromBase64Url(encryptedPayload.ciphertext),
  );
  return textDecoder.decode(plainBytes);
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
    ? "Cloud sync endpoint is not available from the local app host. Use the deployed Cloudflare Pages site or run the app through Cloudflare Pages Functions locally."
    : "Cloud sync endpoint returned HTML instead of JSON. Check that the Cloudflare Pages Function is deployed and the route is not falling back to the app shell.";
}

export async function uploadSection(userId, apiToken, sectionKey, plainTextJson) {
  const identity = await deriveIdentity(userId, apiToken);
  const encryptedPayload = await encryptText(identity.key, plainTextJson);
  const response = await fetch(`/api/sync/${encodeURIComponent(identity.syncId)}/section/${encodeURIComponent(sectionKey)}`, {
    method: "PUT",
    headers: {
      "content-type": "application/json",
    },
    body: JSON.stringify(encryptedPayload),
  });

  const result = await readJsonResponse(
    response,
    `Cloud sync section upload failed for ${sectionKey}.`,
  );

  return {
    ok: result.ok ?? response.ok,
    sectionKey: result.sectionKey ?? sectionKey,
    error: result.error ?? null,
    payloadBytes: result.payloadBytes ?? null,
    updatedAtUtc: result.updatedAtUtc ?? null,
  };
}

export async function downloadSection(userId, apiToken, sectionKey) {
  const identity = await deriveIdentity(userId, apiToken);
  const response = await fetch(`/api/sync/${encodeURIComponent(identity.syncId)}/section/${encodeURIComponent(sectionKey)}`, {
    headers: {
      "accept": "application/json",
    },
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    const errorResult = await readJsonResponse(response, `Cloud sync section download failed for ${sectionKey}.`);
    return { ok: false, error: errorResult.error ?? "download_failed", sectionKey };
  }

  const snapshot = await readJsonResponse(
    response,
    `Cloud sync section endpoint returned an invalid response for ${sectionKey}.`,
  );
  const plainTextJson = await decryptText(identity.key, snapshot.encryptedPayload);
  return {
    ok: true,
    sectionKey,
    plainTextJson,
    updatedAtUtc: snapshot.updatedAtUtc ?? null,
  };
}

export async function downloadAllSections(userId, apiToken, sectionKeys) {
  const results = [];
  for (const sectionKey of sectionKeys) {
    try {
      const result = await downloadSection(userId, apiToken, sectionKey);
      results.push(result);
    } catch (error) {
      results.push({ ok: false, sectionKey, error: error?.message ?? "download_failed" });
    }
  }

  return results;
}

export async function listSections(userId, apiToken) {
  const identity = await deriveIdentity(userId, apiToken);
  const response = await fetch(`/api/sync/${encodeURIComponent(identity.syncId)}/sections`, {
    headers: {
      "accept": "application/json",
    },
  });

  if (!response.ok) {
    return [];
  }

  const result = await readJsonResponse(response, "Cloud sync list sections failed.");
  return (result.sections ?? []).map((entry) => entry.sectionKey);
}

function toBase64Url(bytes) {
  let binary = "";
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }

  return btoa(binary)
    .replaceAll("+", "-")
    .replaceAll("/", "_")
    .replaceAll("=", "");
}

function fromBase64Url(value) {
  const padded = value
    .replaceAll("-", "+")
    .replaceAll("_", "/")
    .padEnd(Math.ceil(value.length / 4) * 4, "=");
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index);
  }

  return bytes;
}
