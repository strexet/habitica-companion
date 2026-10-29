CREATE TABLE integration_tag_consents (
 profile_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 consent_version TEXT NOT NULL,
 consent_epoch TEXT NOT NULL,
 granted_at_utc TEXT NOT NULL,
 revoked_at_utc TEXT,
 PRIMARY KEY(profile_id,connection_id)
);
CREATE TABLE integration_tag_checks (
 token_hash TEXT PRIMARY KEY,
 profile_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 consent_epoch TEXT NOT NULL,
 context_hash TEXT NOT NULL,
 payload_json TEXT NOT NULL,
 expires_at_utc TEXT NOT NULL
);
CREATE INDEX integration_tag_checks_expiry ON integration_tag_checks(expires_at_utc);
CREATE TABLE integration_owned_tags (
 profile_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 workspace_id TEXT NOT NULL,
 entity_id TEXT NOT NULL,
 kind TEXT NOT NULL,
 tag_id TEXT NOT NULL,
 receipt_id TEXT NOT NULL,
 fingerprint TEXT NOT NULL,
 verified_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,connection_id,workspace_id,entity_id,kind),
 UNIQUE(profile_id,connection_id,tag_id)
);
CREATE TABLE integration_tag_attempt_details (
 profile_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 attempt_id TEXT NOT NULL,
 observation_json TEXT NOT NULL,
 created_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,connection_id,attempt_id)
);
