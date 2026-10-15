CREATE TABLE authenticated_party_plans (
 party_id TEXT PRIMARY KEY,
 namespace_id TEXT NOT NULL UNIQUE,
 revision INTEGER NOT NULL DEFAULT 1,
 document_json TEXT NOT NULL,
 commit_tag TEXT NOT NULL,
 updated_at_utc TEXT NOT NULL
);
CREATE TABLE authenticated_party_consents (
 profile_id TEXT PRIMARY KEY,
 party_id TEXT NOT NULL REFERENCES authenticated_party_plans(party_id) ON DELETE CASCADE,
 namespace_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 consent_version TEXT NOT NULL,
 consented_at_utc TEXT NOT NULL
);
CREATE TABLE authenticated_party_commands (
 profile_id TEXT NOT NULL,
 command_id TEXT NOT NULL,
 party_id TEXT NOT NULL REFERENCES authenticated_party_plans(party_id) ON DELETE CASCADE,
 payload_hash TEXT NOT NULL,
 receipt_json TEXT NOT NULL,
 created_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,command_id)
);
CREATE INDEX authenticated_party_commands_actor ON authenticated_party_commands(profile_id,party_id);
CREATE TABLE authenticated_party_write_guard (ok INTEGER NOT NULL CHECK(ok=1));
