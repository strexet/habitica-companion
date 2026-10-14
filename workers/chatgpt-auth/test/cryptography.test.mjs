import { test } from "node:test";
import assert from "node:assert/strict";
import { deriveProfileId, encryptCredential, decryptCredential, encode, newManagementToken, tokenHash, tokenMatches, validateKeySeparation, decode } from "../src/cryptography.ts";

const key = byte => encode(new Uint8Array(32).fill(byte));
const keys = { "1": key(1), "2": key(2) };
const credential = { habiticaUserId: "00000000-0000-4000-8000-000000000011", apiToken: "synthetic-private-token" };

test("stable opaque profile derivation separates users and canonicalizes account casing", async () => {
  const first = await deriveProfileId(credential.habiticaUserId, key(3));
  assert.equal(first, await deriveProfileId(credential.habiticaUserId, key(3)));
  assert.notEqual(first, await deriveProfileId("00000000-0000-4000-8000-000000000012", key(3)));
  assert.equal(await deriveProfileId("ABCDEF00-0000-4000-8000-000000000012", key(3)), await deriveProfileId("abcdef00-0000-4000-8000-000000000012", key(3)));
  assert.ok(!first.includes(credential.habiticaUserId));
});

test("authenticated envelopes roundtrip and use distinct random nonces", async () => {
  const first = await encryptCredential("profile-a", credential, "1", keys);
  const second = await encryptCredential("profile-a", credential, "1", keys);
  assert.deepEqual(await decryptCredential("profile-a", first, keys), credential);
  assert.notEqual(first.iv, second.iv);
  assert.notEqual(first.ciphertext, second.ciphertext);
  assert.ok(!JSON.stringify(first).includes(credential.apiToken));
});

test("wrong profile AAD, key, version, schema and modified ciphertext fail closed", async () => {
  const envelope = await encryptCredential("profile-a", credential, "1", keys);
  await assert.rejects(decryptCredential("profile-b", envelope, keys));
  await assert.rejects(decryptCredential("profile-a", envelope, { "1": key(4) }));
  await assert.rejects(decryptCredential("profile-a", { ...envelope, keyVersion: "9" }, keys));
  await assert.rejects(decryptCredential("profile-a", { ...envelope, schemaVersion: 2 }, keys));
  const modified = decode(envelope.ciphertext);
  modified[0] ^= 1;
  await assert.rejects(decryptCredential("profile-a", { ...envelope, ciphertext: encode(modified) }, keys));
});

test("key-version rotation retains access only to known key versions", async () => {
  const old = await encryptCredential("profile-a", credential, "1", keys);
  const rotated = await encryptCredential("profile-a", await decryptCredential("profile-a", old, keys), "2", keys);
  assert.deepEqual(await decryptCredential("profile-a", rotated, { "2": keys[2] }), credential);
  await assert.rejects(decryptCredential("profile-a", old, { "2": keys[2] }));
});

test("management credentials contain 256 random bits and hashes compare safely", async () => {
  const first = newManagementToken();
  const second = newManagementToken();
  assert.equal(first.length, 43);
  assert.notEqual(first, second);
  const hash = await tokenHash(first);
  assert.notEqual(hash, first);
  assert.equal(await tokenMatches(first, hash), true);
  assert.equal(await tokenMatches(second, hash), false);
  assert.equal(await tokenMatches(first, null), false);
});

test("profile derivation and credential encryption reject shared master keys", () => {
  assert.throws(() => validateKeySeparation(key(1), keys));
  assert.doesNotThrow(() => validateKeySeparation(key(3), keys));
});
