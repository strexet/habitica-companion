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
