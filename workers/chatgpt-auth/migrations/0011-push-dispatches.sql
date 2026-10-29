CREATE TABLE integration_push_dispatches (
 profile_id TEXT NOT NULL,
 dispatch_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 device_id TEXT NOT NULL,
 subscription_id TEXT NOT NULL,
 device_revision INTEGER NOT NULL,
 consent_epoch TEXT NOT NULL,
 payload_hash TEXT NOT NULL,
 expires_at_ms INTEGER NOT NULL,
 state TEXT NOT NULL CHECK(state IN ('admitted','dispatched','accepted','refused','unknown','retryable')),
 code TEXT NOT NULL,
 http_status INTEGER,
 retry_after_ms INTEGER,
 created_at_ms INTEGER NOT NULL,
 updated_at_ms INTEGER NOT NULL,
 PRIMARY KEY(profile_id,dispatch_id)
);
CREATE INDEX integration_push_dispatch_quota ON integration_push_dispatches(profile_id,created_at_ms);
CREATE INDEX integration_push_dispatch_cooldown ON integration_push_dispatches(profile_id,retry_after_ms);
