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

  const snapshot = await response.json();
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
  return text ? `${fallback} ${text}` : fallback;
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

