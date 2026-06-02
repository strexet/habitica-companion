import assert from "node:assert/strict";
import test from "node:test";
import { onRequestPost, partySyncAccessTestHooks } from "../../functions/api/party-sync/[partyId].js";

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

test("tokenized invite proof parser accepts proof headers without Habitica credentials", () => {
  const request = buildTokenizedRequest("party-12345678", "proof-12345678", "t".repeat(64));

  const proof = partySyncAccessTestHooks.readAccessProof(request, "party-12345678");

  assert.equal(proof.response, undefined);
  assert.equal(proof.proofVersion, "tokenized-invite-v1");
  assert.equal(proof.proofId, "proof-12345678");
  assert.equal(proof.token, "t".repeat(64));
});

test("tokenized invite proof parser rejects malformed and unsupported proofs", async () => {
  const malformed = partySyncAccessTestHooks.readAccessProof(
    buildTokenizedRequest("party-12345678", "proof-12345678", "short"),
    "party-12345678");
  assert.equal(malformed.response.status, 401);
  assert.equal(await malformed.response.text(), "Invalid tokenized party-sync invite proof.");

  const unsupported = partySyncAccessTestHooks.readAccessProof(new Request("https://example.test", {
    headers: {
      "x-party-sync-proof-version": "tokenized-invite-v2",
    },
  }), "party-12345678");
  assert.equal(unsupported.response.status, 401);
  assert.equal(await unsupported.response.text(), "Unsupported party-sync access proof.");
});

test("tokenized invite proof access accepts valid proof and rejects expired, revoked, removed, and wrong token proofs", async () => {
  const token = "v".repeat(64);
  const tokenHash = await partySyncAccessTestHooks.hashInviteProofToken(token);
  const scenarios = [
    { expectedStatus: undefined },
    { expiresAtUtc: "2020-01-01T00:00:00.000Z", expectedStatus: 401 },
    { revokedAtUtc: "2026-06-01T00:00:00.000Z", expectedStatus: 401 },
    { removedAtUtc: "2026-06-01T00:00:00.000Z", expectedStatus: 401 },
    { tokenHash: await partySyncAccessTestHooks.hashInviteProofToken("x".repeat(64)), expectedStatus: 401 },
  ];

  for (const scenario of scenarios) {
    const db = new TokenProofDb({
      proofs: [{
        partyId: "party-12345678",
        proofId: "proof-12345678",
        tokenHash: scenario.tokenHash ?? tokenHash,
        expiresAtUtc: scenario.expiresAtUtc ?? null,
        revokedAtUtc: scenario.revokedAtUtc ?? null,
        removedAtUtc: scenario.removedAtUtc ?? null,
      }],
    });

    const access = await partySyncAccessTestHooks.resolvePartySyncAccess(
      buildTokenizedRequest("party-12345678", "proof-12345678", token),
      {},
      db,
      "party-12345678");

    assert.equal(access.response?.status, scenario.expectedStatus);
  }
});

test("tokenized invite proof access rejects wrong-party and kicked-user claims", async () => {
  const token = "v".repeat(64);
  const tokenHash = await partySyncAccessTestHooks.hashInviteProofToken(token);
  const db = new TokenProofDb({
    proofs: [{ partyId: "party-12345678", proofId: "proof-12345678", tokenHash }],
    kickedUserIds: ["user-123"],
  });

  const wrongParty = partySyncAccessTestHooks.readAccessProof(
    buildTokenizedRequest("other-party", "proof-12345678", token),
    "party-12345678");
  assert.equal(wrongParty.response.status, 403);

  const kicked = await partySyncAccessTestHooks.resolvePartySyncAccess(
    buildTokenizedRequest("party-12345678", "proof-12345678", token),
    {},
    db,
    "party-12345678");
  assert.equal(kicked.response.status, 403);
});

test("enabled invite-proof mode keeps local fallback until a proof exists and allows owner recovery", async () => {
  const tokenHash = await partySyncAccessTestHooks.hashInviteProofToken("v".repeat(64));
  const memberRequest = buildLocalRequest("party-12345678", "member-123", "Member");
  const noProofDb = new TokenProofDb({ modeEnabled: true });
  assert.equal((await partySyncAccessTestHooks.resolvePartySyncAccess(memberRequest, {}, noProofDb, "party-12345678")).response, undefined);

  const disabledDb = new TokenProofDb({
    proofs: [{ partyId: "party-12345678", proofId: "proof-12345678", tokenHash }],
  });
  assert.equal((await partySyncAccessTestHooks.resolvePartySyncAccess(memberRequest, {}, disabledDb, "party-12345678")).response, undefined);

  const proofDb = new TokenProofDb({
    modeEnabled: true,
    proofs: [{ partyId: "party-12345678", proofId: "proof-12345678", tokenHash }],
  });
  assert.equal((await partySyncAccessTestHooks.resolvePartySyncAccess(memberRequest, {}, proofDb, "party-12345678")).response.status, 401);

  const ownerRequest = buildLocalRequest("party-12345678", "owner-123", "Owner", "owner-123");
  assert.equal((await partySyncAccessTestHooks.resolvePartySyncAccess(ownerRequest, {}, proofDb, "party-12345678")).response, undefined);

  const adminDb = new TokenProofDb({
    modeEnabled: true,
    proofs: [{ partyId: "party-12345678", proofId: "proof-12345678", tokenHash }],
    adminIds: ["admin-123"],
  });
  const adminRequest = buildLocalRequest("party-12345678", "admin-123", "Admin");
  assert.equal((await partySyncAccessTestHooks.resolvePartySyncAccess(adminRequest, {}, adminDb, "party-12345678")).response, undefined);
});

test("rotated proof invalidates the old token while the replacement remains valid", async () => {
  const oldToken = "o".repeat(64);
  const newToken = "n".repeat(64);
  const db = new TokenProofDb({
    proofs: [
      {
        partyId: "party-12345678",
        proofId: "proof-old-12345678",
        tokenHash: await partySyncAccessTestHooks.hashInviteProofToken(oldToken),
        revokedAtUtc: "2026-06-02T00:00:00.000Z",
      },
      {
        partyId: "party-12345678",
        proofId: "proof-new-12345678",
        tokenHash: await partySyncAccessTestHooks.hashInviteProofToken(newToken),
      },
    ],
  });

  const oldProof = await partySyncAccessTestHooks.validateTokenizedInviteProof(db, "party-12345678", {
    proofId: "proof-old-12345678",
    token: oldToken,
  });
  const newProof = await partySyncAccessTestHooks.validateTokenizedInviteProof(db, "party-12345678", {
    proofId: "proof-new-12345678",
    token: newToken,
  });

  assert.equal(oldProof.response.status, 401);
  assert.equal(newProof.response, undefined);
});

test("owner can enable, issue, list, rotate, revoke, remove, and disable invite proofs", async () => {
  const db = new InviteProofActionDb();

  assert.equal((await postPartyAction(db, "owner-123", "Owner", {
    action: "setInviteProofMode",
    enabled: true,
  }, "owner-123")).status, 200);
  assert.equal(db.modeEnabled, true);

  const issuedResponse = await postPartyAction(db, "owner-123", "Owner", {
    action: "createInviteProof",
    label: "Family devices",
  }, "owner-123");
  const issued = await issuedResponse.json();
  assert.equal(issuedResponse.status, 200);
  assert.equal(issued.management.inviteProofMode.inviteProofs.length, 1);
  assert.equal(issued.issuedInviteProof.token.length, 64);

  const listed = await (await postPartyAction(db, "owner-123", "Owner", {
    action: "listInviteProofs",
  }, "owner-123")).json();
  assert.equal(listed.management.inviteProofMode.inviteProofs[0].label, "Family devices");

  const rotated = await (await postPartyAction(db, "owner-123", "Owner", {
    action: "rotateInviteProof",
    proofId: issued.issuedInviteProof.proofId,
  }, "owner-123")).json();
  assert.notEqual(rotated.issuedInviteProof.proofId, issued.issuedInviteProof.proofId);
  assert.equal(db.proofs.find(proof => proof.proofId === issued.issuedInviteProof.proofId).revokedAtUtc !== null, true);

  assert.equal((await postPartyAction(db, "owner-123", "Owner", {
    action: "revokeInviteProof",
    proofId: rotated.issuedInviteProof.proofId,
  }, "owner-123")).status, 200);
  assert.equal(db.proofs.find(proof => proof.proofId === rotated.issuedInviteProof.proofId).revokedAtUtc !== null, true);

  assert.equal((await postPartyAction(db, "owner-123", "Owner", {
    action: "removeInviteProof",
    proofId: rotated.issuedInviteProof.proofId,
  }, "owner-123")).status, 200);
  assert.equal(db.proofs.find(proof => proof.proofId === rotated.issuedInviteProof.proofId).removedAtUtc !== null, true);

  assert.equal((await postPartyAction(db, "owner-123", "Owner", {
    action: "setInviteProofMode",
    enabled: false,
  }, "owner-123")).status, 200);
  assert.equal(db.modeEnabled, false);
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

test("automatic quest completion detection keys are trimmed and bounded", () => {
  assert.equal(partySyncAccessTestHooks.normalizeDetectionKey(" habitica-chat-boss:dragon:chat-1 "), "habitica-chat-boss:dragon:chat-1");
  assert.equal(partySyncAccessTestHooks.normalizeDetectionKey(""), null);
  assert.equal(partySyncAccessTestHooks.normalizeDetectionKey("x".repeat(300)).length, 240);
});

test("app admin can assign the party owner role", async () => {
  const db = new FakePartySyncDb({
    adminIds: ["admin-123"],
    members: [
      { memberId: "admin-123", displayName: "Admin" },
      { memberId: "member-123", displayName: "Beta" },
    ],
  });

  const response = await postPartyAction(db, "admin-123", "Admin", {
    action: "assignPartyOwner",
    userId: "member-123",
    displayName: "Beta",
  });

  assert.equal(response.status, 200);
  const body = await response.json();
  assert.equal(body.management.ownerUserId, "member-123");
  assert.equal(body.management.ownerDisplayName, "Beta");
  assert.equal(db.roles.length, 1);
  assert.equal(db.roles[0].role, "Owner");
  assert.equal(db.roles[0].assignedByUserId, "admin-123");
});

test("non-admin callers cannot assign the party owner role", async () => {
  const db = new FakePartySyncDb({
    adminIds: ["admin-123"],
    members: [
      { memberId: "leader-123", displayName: "Leader" },
      { memberId: "member-123", displayName: "Beta" },
    ],
  });

  const response = await postPartyAction(db, "leader-123", "Leader", {
    action: "assignPartyOwner",
    userId: "member-123",
    displayName: "Beta",
  }, "leader-123");

  assert.equal(response.status, 403);
  assert.equal(await response.text(), "Only app admins can assign the party owner role.");
  assert.equal(db.roles.length, 0);
});

async function postPartyAction(db, userId, displayName, body, leaderId = "leader-123") {
  return await onRequestPost({
    env: { HABITICA_PARTY_DB: db },
    params: { partyId: "party-12345678" },
    request: new Request("https://example.test/api/party-sync/party-12345678", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-party-sync-proof-version": "local-claim-v1",
        "x-party-sync-party-id": "party-12345678",
        "x-party-sync-user-id": userId,
        "x-party-sync-display-name": displayName,
        "x-party-sync-leader-id": leaderId,
      },
      body: JSON.stringify(body),
    }),
  });
}

class FakePartySyncDb {
  constructor({ adminIds = [], members = [], roles = [] } = {}) {
    this.adminIds = adminIds;
    this.members = members;
    this.roles = roles;
  }

  prepare(sql) {
    return new FakePartySyncStatement(this, sql);
  }
}

class FakePartySyncStatement {
  constructor(db, sql) {
    this.db = db;
    this.sql = sql;
    this.args = [];
  }

  bind(...args) {
    this.args = args;
    return this;
  }

  async first() {
    const sql = normalizeSql(this.sql);
    if (sql.includes("from party_sync_settings")) {
      return null;
    }
    if (sql.includes("select role") && sql.includes("from party_sync_roles")) {
      const role = this.db.roles.find(item => isOwnerRole(item.role));
      return role ? { role: role.role } : null;
    }
    if (sql.includes("select user_id, display_name") && sql.includes("from party_sync_roles")) {
      const [partyId, role] = this.args;
      const row = this.db.roles.find(item =>
        item.partyId === partyId
          && item.role === role
          && !item.revokedAtUtc);
      return row ? roleRow(row) : null;
    }
    if (sql.includes("from party_sync_roles") && sql.includes("role = 'officer'")) {
      const [partyId, userId] = this.args;
      return this.db.roles.some(item =>
        item.partyId === partyId
          && item.userId === userId
          && item.role === "Officer"
          && !item.revokedAtUtc)
        ? { user_id: userId }
        : null;
    }
    if (sql.includes("from party_sync_kicks")) {
      return null;
    }
    if (sql.includes("select snapshot_json from party_state")) {
      return {
        snapshot_json: JSON.stringify({
          members: this.db.members,
        }),
      };
    }

    return null;
  }

  async all() {
    const sql = normalizeSql(this.sql);
    if (sql.includes("from app_admins")) {
      return {
        results: this.db.adminIds.map(userId => ({ user_id: userId })),
      };
    }
    if (sql.includes("from party_sync_roles") && sql.includes("role = 'officer'")) {
      return {
        results: this.db.roles
          .filter(item => item.role === "Officer" && !item.revokedAtUtc)
          .map(roleRow),
      };
    }

    return { results: [] };
  }

  async run() {
    const sql = normalizeSql(this.sql);
    if (sql.startsWith("update party_sync_roles")) {
      const [revokedByUserId, revokedByDisplayName, revokedAtUtc, partyId, role] = this.args;
      for (const item of this.db.roles) {
        if (item.partyId === partyId && item.role === role && !item.revokedAtUtc) {
          item.revokedByUserId = revokedByUserId;
          item.revokedByDisplayName = revokedByDisplayName;
          item.revokedAtUtc = revokedAtUtc;
        }
      }
    }
    if (sql.startsWith("insert into party_sync_roles")) {
      const [partyId, userId, displayName, role, assignedByUserId, assignedByDisplayName, assignedAtUtc] = this.args;
      this.db.roles.push({
        partyId,
        userId,
        displayName,
        role,
        assignedByUserId,
        assignedByDisplayName,
        assignedAtUtc,
        revokedAtUtc: null,
      });
    }

    return { success: true };
  }
}

function normalizeSql(sql) {
  return sql.replace(/\s+/g, " ").trim().toLowerCase();
}

function isOwnerRole(role) {
  return ["owner", "partyowner", "party-owner", "party owner"].includes(role.toLowerCase());
}

function roleRow(row) {
  return {
    user_id: row.userId,
    display_name: row.displayName,
    assigned_by_user_id: row.assignedByUserId,
    assigned_by_display_name: row.assignedByDisplayName,
    assigned_at_utc: row.assignedAtUtc,
  };
}

function buildLocalRequest(partyId, userId, displayName, leaderId = "leader-123") {
  return new Request(`https://example.test/api/party-sync/${partyId}`, {
    headers: {
      "x-party-sync-proof-version": "local-claim-v1",
      "x-party-sync-party-id": partyId,
      "x-party-sync-user-id": userId,
      "x-party-sync-display-name": displayName,
      "x-party-sync-leader-id": leaderId,
    },
  });
}

function buildTokenizedRequest(partyId, proofId, token) {
  return new Request(`https://example.test/api/party-sync/${partyId}`, {
    headers: {
      "x-party-sync-proof-version": "tokenized-invite-v1",
      "x-party-sync-party-id": partyId,
      "x-party-sync-user-id": "user-123",
      "x-party-sync-display-name": "Mage Tester",
      "x-party-sync-leader-id": "leader-123",
      "x-party-sync-proof-id": proofId,
      "x-party-sync-proof-token": token,
    },
  });
}

class TokenProofDb {
  constructor({ modeEnabled = false, proofs = [], kickedUserIds = [], adminIds = [] } = {}) {
    this.modeEnabled = modeEnabled;
    this.proofs = proofs;
    this.kickedUserIds = kickedUserIds;
    this.adminIds = adminIds;
  }

  prepare(sql) {
    return new TokenProofStatement(this, sql);
  }
}

class TokenProofStatement {
  constructor(db, sql) {
    this.db = db;
    this.sql = normalizeSql(sql);
    this.args = [];
  }

  bind(...args) {
    this.args = args;
    return this;
  }

  async first() {
    if (this.sql.includes("from party_sync_settings")) {
      return {
        tokenized_invite_proof_mode_enabled: this.db.modeEnabled ? 1 : 0,
      };
    }
    if (this.sql.includes("select role") && this.sql.includes("from party_sync_roles")) {
      return null;
    }
    if (this.sql.includes("from party_sync_roles")) {
      return null;
    }
    if (this.sql.includes("from party_sync_kicks")) {
      return this.db.kickedUserIds.includes(this.args[1]) ? { user_id: this.args[1] } : null;
    }
    if (this.sql.includes("from party_sync_invite_proofs")) {
      const [partyId, proofIdOrTimestamp] = this.args;
      if (this.sql.includes("select proof_id")) {
        const proof = this.db.proofs.find(item =>
          item.partyId === partyId
          && !item.revokedAtUtc
          && !item.removedAtUtc
          && (!item.expiresAtUtc || item.expiresAtUtc > proofIdOrTimestamp));
        return proof ? { proof_id: proof.proofId } : null;
      }

      const proof = this.db.proofs.find(item => item.partyId === partyId && item.proofId === proofIdOrTimestamp);
      return proof
        ? {
            token_hash: proof.tokenHash,
            expires_at_utc: proof.expiresAtUtc ?? null,
            revoked_at_utc: proof.revokedAtUtc ?? null,
            removed_at_utc: proof.removedAtUtc ?? null,
          }
        : null;
    }

    return null;
  }

  async all() {
    if (this.sql.includes("from app_admins")) {
      return { results: this.db.adminIds.map(userId => ({ user_id: userId })) };
    }

    return { results: [] };
  }
}

class InviteProofActionDb extends TokenProofDb {
  constructor() {
    super();
    this.proofs = [];
  }

  prepare(sql) {
    return new InviteProofActionStatement(this, sql);
  }

  async batch(statements) {
    for (const statement of statements) {
      await statement.run();
    }
  }
}

class InviteProofActionStatement extends TokenProofStatement {
  async first() {
    if (this.sql.includes("select display_label, expires_at_utc") && this.sql.includes("from party_sync_invite_proofs")) {
      const [partyId, proofId] = this.args;
      const proof = this.db.proofs.find(item => item.partyId === partyId && item.proofId === proofId && !item.removedAtUtc);
      return proof
        ? {
            display_label: proof.label,
            expires_at_utc: proof.expiresAtUtc ?? null,
          }
        : null;
    }
    if (this.sql.includes("select coalesce(max(sort_order)")) {
      return { next_sort_order: 1 };
    }

    return await super.first();
  }

  async all() {
    if (this.sql.includes("from party_quest_queue")
      || this.sql.includes("from party_quest_votes")
      || this.sql.includes("from party_quest_pool_entries")
      || this.sql.includes("from party_recently_completed_quests")
      || this.sql.includes("from party_sync_kicks")) {
      return { results: [] };
    }
    if (this.sql.includes("select proof_id, display_label") && this.sql.includes("from party_sync_invite_proofs")) {
      return {
        results: this.db.proofs.map(proof => ({
          proof_id: proof.proofId,
          display_label: proof.label,
          issued_by_user_id: proof.issuedByUserId,
          issued_by_display_name: proof.issuedByDisplayName,
          issued_at_utc: proof.issuedAtUtc,
          expires_at_utc: proof.expiresAtUtc ?? null,
          revoked_at_utc: proof.revokedAtUtc ?? null,
          removed_at_utc: proof.removedAtUtc ?? null,
        })),
      };
    }

    return await super.all();
  }

  async run() {
    if (this.sql.startsWith("insert into party_sync_settings")) {
      this.db.modeEnabled = Number(this.args[1]) === 1;
      return { success: true };
    }
    if (this.sql.startsWith("insert into party_sync_invite_proofs")) {
      const [partyId, proofId, tokenHash, label, issuedByUserId, issuedByDisplayName, issuedAtUtc, expiresAtUtc] = this.args;
      this.db.proofs.push({
        partyId,
        proofId,
        tokenHash,
        label,
        issuedByUserId,
        issuedByDisplayName,
        issuedAtUtc,
        expiresAtUtc,
        revokedAtUtc: null,
        removedAtUtc: null,
      });
      return { success: true };
    }
    if (this.sql.startsWith("update party_sync_invite_proofs") && this.sql.includes("removed_by_user_id")) {
      const [,,,,,, partyId, proofId] = this.args;
      const proof = this.db.proofs.find(item => item.partyId === partyId && item.proofId === proofId);
      if (proof) {
        proof.revokedAtUtc ??= this.args[2];
        proof.removedAtUtc = this.args[5];
      }
      return { success: true };
    }
    if (this.sql.startsWith("update party_sync_invite_proofs")) {
      const [,, revokedAtUtc, partyId, proofId] = this.args;
      const proof = this.db.proofs.find(item => item.partyId === partyId && item.proofId === proofId);
      if (proof) {
        proof.revokedAtUtc = revokedAtUtc;
      }
      return { success: true };
    }

    return { success: true };
  }
}
