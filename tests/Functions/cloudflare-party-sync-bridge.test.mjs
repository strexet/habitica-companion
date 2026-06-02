import assert from "node:assert/strict";
import test from "node:test";

const values = new Map();
globalThis.window = {
  localStorage: {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: key => values.delete(key),
  },
};

const {
  activatePartySyncInviteProof,
  partySyncBridgeTestHooks,
} = await import("../../src/Habitica.WebApp/wwwroot/js/sync/cloudflarePartySync.js");

const claim = {
  partyId: "party-12345678",
  userId: "user-123",
  displayName: "Mage Tester",
  leaderId: "leader-123",
};

test("browser bridge keeps local claim headers when no invite proof is active", () => {
  partySyncBridgeTestHooks.clearPartySyncInviteProof(claim.partyId);

  const headers = partySyncBridgeTestHooks.buildClaimHeaders(claim);

  assert.equal(headers["x-party-sync-proof-version"], "local-claim-v1");
  assert.equal(headers["x-party-sync-proof-token"], undefined);
  assert.equal(headers["x-api-key"], undefined);
  assert.equal(headers["x-api-user"], undefined);
});

test("browser bridge sends tokenized proof headers only after local activation", () => {
  activatePartySyncInviteProof(claim.partyId, "proof-12345678", "t".repeat(64), "Family devices");

  const headers = partySyncBridgeTestHooks.buildClaimHeaders(claim);

  assert.equal(headers["x-party-sync-proof-version"], "tokenized-invite-v1");
  assert.equal(headers["x-party-sync-proof-id"], "proof-12345678");
  assert.equal(headers["x-party-sync-proof-token"], "t".repeat(64));
  assert.equal(headers["authorization"], undefined);
});

test("browser bridge uses local claims for manager recovery actions", () => {
  activatePartySyncInviteProof(claim.partyId, "proof-12345678", "t".repeat(64), "Family devices");

  const headers = partySyncBridgeTestHooks.buildClaimHeaders(claim, { forceLocalClaim: true });

  assert.equal(headers["x-party-sync-proof-version"], "local-claim-v1");
  assert.equal(headers["x-party-sync-proof-token"], undefined);
});
