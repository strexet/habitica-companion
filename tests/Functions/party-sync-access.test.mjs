import assert from "node:assert/strict";
import test from "node:test";
import { partySyncAccessTestHooks } from "../../functions/api/party-sync/[partyId].js";

test("local party-sync claim is accepted without Habitica API headers", () => {
  const request = new Request("https://example.test/api/party-sync/party-12345678", {
    headers: {
      "x-party-sync-proof-version": "local-claim-v1",
      "x-party-sync-party-id": "party-12345678",
      "x-party-sync-user-id": "user-123",
      "x-party-sync-display-name": "Mage Tester",
      "x-party-sync-leader-id": "user-123",
    },
  });

  const proof = partySyncAccessTestHooks.readAccessProof(request, "party-12345678");

  assert.equal(proof.response, undefined);
  assert.equal(proof.userId, "user-123");
  assert.equal(proof.leaderId, "user-123");
});

test("party-sync claim must match the requested party", async () => {
  const request = new Request("https://example.test/api/party-sync/party-12345678", {
    headers: {
      "x-party-sync-proof-version": "local-claim-v1",
      "x-party-sync-party-id": "other-party",
      "x-party-sync-user-id": "user-123",
      "x-party-sync-display-name": "Mage Tester",
    },
  });

  const proof = partySyncAccessTestHooks.readAccessProof(request, "party-12345678");

  assert.equal(proof.response.status, 403);
  assert.equal(await proof.response.text(), "Party-sync claim does not match the requested party.");
});

test("party-sync settings normalize to safe defaults", () => {
  assert.deepEqual(partySyncAccessTestHooks.normalizeSettings({}), {
    officerCanManageQueue: true,
    officerCanModerateMembers: true,
    officerOnlyQueueEdits: false,
    memberAutoReconcileEnabled: true,
  });

  assert.deepEqual(partySyncAccessTestHooks.normalizeSettings({
    officerCanManageQueue: false,
    officerCanModerateMembers: false,
    officerOnlyQueueEdits: true,
    memberAutoReconcileEnabled: false,
  }), {
    officerCanManageQueue: false,
    officerCanModerateMembers: false,
    officerOnlyQueueEdits: true,
    memberAutoReconcileEnabled: false,
  });
});
