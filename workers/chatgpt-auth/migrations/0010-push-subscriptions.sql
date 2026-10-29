CREATE TABLE integration_push_consents (
 profile_id TEXT PRIMARY KEY,
 connection_id TEXT NOT NULL,
 consent_epoch TEXT NOT NULL,
 enabled INTEGER NOT NULL CHECK(enabled IN (0,1)),
 consent_version TEXT NOT NULL,
 updated_at_utc TEXT NOT NULL
);
CREATE TABLE integration_push_devices (
 profile_id TEXT NOT NULL,
 device_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 revision INTEGER NOT NULL CHECK(revision>0 AND revision<=9007199254740991),
 subscription_id TEXT NOT NULL,
 state TEXT NOT NULL CHECK(state IN ('active','unsubscribed','revoked','expired')),
 consent_epoch TEXT NOT NULL,
 endpoint_hash TEXT,
 ciphertext TEXT,
 iv TEXT,
 key_version TEXT,
 expires_at_ms INTEGER,
 vapid_key_hash TEXT,
 commit_tag TEXT NOT NULL,
 updated_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,device_id),
 CHECK((state='active' AND endpoint_hash IS NOT NULL AND ciphertext IS NOT NULL AND iv IS NOT NULL AND key_version IS NOT NULL AND vapid_key_hash IS NOT NULL) OR (state<>'active' AND endpoint_hash IS NULL AND ciphertext IS NULL AND iv IS NULL AND key_version IS NULL AND vapid_key_hash IS NULL))
);
CREATE UNIQUE INDEX integration_push_endpoint_owner ON integration_push_devices(endpoint_hash) WHERE endpoint_hash IS NOT NULL;
CREATE INDEX integration_push_expiry ON integration_push_devices(expires_at_ms) WHERE state='active';
CREATE TABLE integration_push_commands (
 profile_id TEXT NOT NULL,
 request_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 payload_hash TEXT NOT NULL,
 result_json TEXT NOT NULL,
 created_at_utc TEXT NOT NULL,
 expires_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,request_id)
);
CREATE INDEX integration_push_command_expiry ON integration_push_commands(expires_at_utc);
